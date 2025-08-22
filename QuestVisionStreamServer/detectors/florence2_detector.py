import torch
import numpy as np
from typing import List, Dict, Any, Optional, Tuple
from PIL import Image
from transformers import AutoProcessor, AutoModelForCausalLM

MODEL_NAME = "microsoft/Florence-2-base"
CONF_THRES = 0.30
DETECTION_PROMPT = "<OD>"
IGNORE_CLASSES = {"person", "car", "truck", "bus", "motorcycle", "bicycle"}

# Run Florence once every N frames (1 = every frame, 2 = every 2nd frame, etc.)
FRAME_SKIP = 2

# Optionally downscale before Florence to save compute (None to disable)
# e.g., DOWNSCALE = (320, 240)
DOWNSCALE: Optional[Tuple[int, int]] = None

# Does not perform well on MPS
DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

print(f"[Florence2] Loading model on {DEVICE}...")
processor = AutoProcessor.from_pretrained(MODEL_NAME, trust_remote_code=True)
model = AutoModelForCausalLM.from_pretrained(
    MODEL_NAME,
    trust_remote_code=True,
    torch_dtype=torch.float32,    # use fp16 later if you want more speed
    attn_implementation="eager",  # avoid SDPA/flash attention checks
).to(DEVICE)
model.eval()
MODEL_DTYPE = next(model.parameters()).dtype
print("[Florence2] Model loaded! dtype:", MODEL_DTYPE)

# internal counters / cache
_frame_count = 0
_last_detections: List[Dict[str, Any]] = []


def _move_to_device(batch: Dict[str, Any], device: str, model_dtype: torch.dtype):
    out: Dict[str, Any] = {}
    for k, v in batch.items():
        if isinstance(v, torch.Tensor):
            if k in ("input_ids", "attention_mask", "position_ids"):
                out[k] = v.to(device=device, dtype=torch.long)
            elif k == "pixel_values":
                out[k] = v.to(device=device, dtype=model_dtype)
            else:
                out[k] = v.to(device=device, dtype=(model_dtype if v.is_floating_point() else v.dtype))
        else:
            out[k] = v
    return out


def _parse_od_result(parsed: Any, task: str):
    if parsed is None:
        return [], [], []
    data = parsed.get(task) if isinstance(parsed, dict) else None
    if isinstance(parsed, dict) and data is None:
        data = parsed.get("OD") or parsed.get("<OD>")
    data = data or parsed

    if isinstance(data, dict) and ("bboxes" in data or "boxes" in data):
        bboxes = data.get("bboxes") or data.get("boxes") or []
        labels = data.get("labels") or [""] * len(bboxes)
        scores = data.get("scores") or [1.0] * len(bboxes)
        return bboxes, labels, scores

    if isinstance(data, list):
        bboxes, labels, scores = [], [], []
        for item in data:
            if not isinstance(item, dict):
                continue
            box = item.get("bbox") or item.get("box")
            if box is None:
                continue
            bboxes.append(box)
            labels.append(item.get("label", ""))
            scores.append(item.get("score", 1.0))
        return bboxes, labels, scores

    return [], [], []


@torch.inference_mode()
def detect_objects(img: np.ndarray, frame: Optional[Any] = None):
    """
    Backward-compatible: returns (img, detections).
    - Runs Florence every FRAME_SKIP frames.
    - On skipped frames, returns last detections.
    - No drawing (img is returned unmodified).
    """
    global _frame_count, _last_detections

    if img is None or img.size == 0:
        return img, []

    _frame_count += 1
    run_now = (_frame_count % FRAME_SKIP == 0)

    if not run_now:
        # skip Florence, return cached detections
        return img, _last_detections

    # Optionally downscale to speed up Florence
    orig_h, orig_w = img.shape[:2]
    pil_image_full = Image.fromarray(img[..., ::-1])  # BGR -> RGB
    if DOWNSCALE is not None:
        dw, dh = DOWNSCALE
        pil_for_model = pil_image_full.resize((dw, dh))
        scale_w, scale_h = orig_w / dw, orig_h / dh
    else:
        pil_for_model = pil_image_full
        scale_w, scale_h = 1.0, 1.0

    inputs = processor(text=DETECTION_PROMPT, images=pil_for_model, return_tensors="pt")
    inputs = _move_to_device(inputs, DEVICE, MODEL_DTYPE)

    # keep generation tiny for speed
    generated_ids = model.generate(
        **inputs,
        max_new_tokens=256,      
        num_beams=1,             # greedy
        do_sample=False,
        use_cache=False,         # avoid KV cache issue
        return_dict_in_generate=False,
    )

    generated_text = processor.batch_decode(generated_ids, skip_special_tokens=False)[0]
    # image_size must match what was fed to the model
    if DOWNSCALE is not None:
        img_w, img_h = DOWNSCALE
    else:
        img_w, img_h = orig_w, orig_h

    parsed_answer = processor.post_process_generation(
        generated_text,
        task=DETECTION_PROMPT,
        image_size=(img_w, img_h),
    )

    bboxes, labels, scores = _parse_od_result(parsed_answer, DETECTION_PROMPT)

    detections: List[Dict[str, Any]] = []
    for bbox, label, score in zip(bboxes, labels, scores):
        score = float(score) if score is not None else 1.0
        if score < CONF_THRES:
            continue
        if label and label.lower() in IGNORE_CLASSES:
            continue

        x1, y1, x2, y2 = bbox
        # If normalized (<=1), convert to pixels in model space
        if max(x2, y2) <= 1.5:
            x1, x2 = x1 * img_w, x2 * img_w
            y1, y2 = y1 * img_h, y2 * img_h

        # Map back to original image scale if downscaled
        x1, x2 = x1 * scale_w, x2 * scale_w
        y1, y2 = y1 * scale_h, y2 * scale_h

        x1 = int(max(0, min(round(x1), orig_w - 1)))
        y1 = int(max(0, min(round(y1), orig_h - 1)))
        x2 = int(max(0, min(round(x2), orig_w - 1)))
        y2 = int(max(0, min(round(y2), orig_h - 1)))
        if x2 <= x1 or y2 <= y1:
            continue

        detections.append({
            "label": label or "object",
            "conf": score,
            "bbox": [float(x1), float(y1), float(x2), float(y2)],
        })

    _last_detections = detections
    return img, detections
