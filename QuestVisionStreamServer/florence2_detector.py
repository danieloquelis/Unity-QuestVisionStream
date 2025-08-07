# florence2_detector.py
import cv2
import torch
import numpy as np
from typing import List, Dict, Any, Optional
from PIL import Image
from transformers import AutoProcessor, AutoModelForCausalLM

# --- config ---
MODEL_NAME = "microsoft/Florence-2-base"   # or "microsoft/Florence-2-large" if you have headroom
CONF_THRES = 0.30
DETECTION_PROMPT = "<OD>"
IGNORE_CLASSES = {"person", "car", "truck", "bus", "motorcycle", "bicycle"}

DEVICE = (
    "cuda" if torch.cuda.is_available()
    else ("mps" if getattr(torch.backends, "mps", None) and torch.backends.mps.is_available() else "cpu")
)

print(f"[Florence2] Loading model on {DEVICE}...")
processor = AutoProcessor.from_pretrained(MODEL_NAME, trust_remote_code=True)
# float32 is safest across devices; you can try torch.float16 on CUDA for more speed
model = AutoModelForCausalLM.from_pretrained(
    MODEL_NAME, trust_remote_code=True, torch_dtype=torch.float32
).to(DEVICE)
model.eval()
print("[Florence2] Model loaded!")

def _move_to_device(batch, device):
    try:
        return batch.to(device)
    except AttributeError:
        for k, v in batch.items():
            if isinstance(v, torch.Tensor):
                batch[k] = v.to(device)
        return batch

def _parse_od_result(parsed: Any, task: str):
    """
    Normalize Florence-2 <OD> outputs to (bboxes, labels, scores).
    Supports multiple shapes returned by different lib versions.
    """
    if parsed is None:
        return [], [], []

    data = None
    if isinstance(parsed, dict):
        data = parsed.get(task) or parsed.get("OD") or parsed.get("<OD>") or parsed

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
    Run Florence-2 zero-shot OD on a BGR image.
    Mutates `img` in-place to draw boxes; returns (img, detections).
    detections: [{label, conf, bbox:[x1,y1,x2,y2]}]
    """
    if img is None or img.size == 0:
        return img, []

    H, W = img.shape[:2]
    # Florence expects RGB PIL image
    pil_image = Image.fromarray(cv2.cvtColor(img, cv2.COLOR_BGR2RGB))

    inputs = processor(text=DETECTION_PROMPT, images=pil_image, return_tensors="pt")
    inputs = _move_to_device(inputs, DEVICE)

    generated_ids = model.generate(
        **inputs,
        max_new_tokens=256,   # keep small for realtime
        num_beams=1,          # greedy for speed/stability
        do_sample=False
    )

    generated_text = processor.batch_decode(generated_ids, skip_special_tokens=False)[0]
    parsed_answer = processor.post_process_generation(
        generated_text,
        task=DETECTION_PROMPT,
        image_size=(W, H),  # try to get pixel coords directly
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
        # If normalized (<=1), convert to pixels
        if max(x2, y2) <= 1.5:
            x1, x2 = x1 * W, x2 * W
            y1, y2 = y1 * H, y2 * H

        # clip + int
        x1 = int(max(0, min(round(x1), W - 1)))
        y1 = int(max(0, min(round(y1), H - 1)))
        x2 = int(max(0, min(round(x2), W - 1)))
        y2 = int(max(0, min(round(y2), H - 1)))
        if x2 <= x1 or y2 <= y1:
            continue

        # draw (mutates `img`, which VideoProcessor is already displaying)
        cv2.rectangle(img, (x1, y1), (x2, y2), (0, 255, 0), 2)
        txt = f"{label} {score:.2f}" if label else f"{score:.2f}"
        cv2.putText(img, txt, (x1, max(0, y1 - 6)),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 2, cv2.LINE_AA)

        detections.append({
            "label": label or "object",
            "conf": score,
            "bbox": [float(x1), float(y1), float(x2), float(y2)],
        })

    return img, detections
