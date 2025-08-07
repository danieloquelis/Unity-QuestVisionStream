import time
import cv2
import torch
import numpy as np
from PIL import Image
from transformers import AutoProcessor, AutoModelForCausalLM

# --- config ---
MODEL_NAME = "microsoft/Florence-2-base"  # base is faster; switch to -large if you have headroom
DEVICE = (
    "cuda" if torch.cuda.is_available()
    else ("mps" if getattr(torch.backends, "mps", None) and torch.backends.mps.is_available() else "cpu")
)
CAM_INDEX = 0
CONF_THRES = 0.3
DETECTION_PROMPT = "<OD>"

print(f"Loading Florence-2 model on device: {DEVICE}")
print("This may take a moment on first run...")

# --- load model ---
processor = AutoProcessor.from_pretrained(MODEL_NAME, trust_remote_code=True)
# use float32 for MPS/CPU; use float16 on CUDA if you want more speed (and set autocast)
model = AutoModelForCausalLM.from_pretrained(
    MODEL_NAME, trust_remote_code=True, torch_dtype=torch.float32
).to(DEVICE)
model.eval()

# --- open camera ---
cap = cv2.VideoCapture(CAM_INDEX)
cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
cap.set(cv2.CAP_PROP_FPS, 30)

if not cap.isOpened():
    raise RuntimeError("Could not open camera. Try a different CAM_INDEX (e.g., 1) or check permissions.")

print("Starting Florence-2 real-time object detection...")
print("Press ESC to quit")

def move_to_device(batch, device):
    # processor returns a BatchFeature that supports .to(device), but be robust:
    try:
        return batch.to(device)
    except AttributeError:
        for k, v in batch.items():
            if isinstance(v, torch.Tensor):
                batch[k] = v.to(device)
        return batch

def parse_od_result(parsed, task):
    """
    Normalize Florence-2 <OD> outputs to a tuple (bboxes, labels, scores).
    Handles multiple return shapes across versions.
    """
    if parsed is None:
        return [], [], []

    # Most common: parsed is a dict keyed by the task ("<OD>")
    data = None
    if isinstance(parsed, dict):
        data = parsed.get(task) or parsed.get("OD") or parsed.get("<OD>") or parsed

    # Case A: dict with array fields
    if isinstance(data, dict) and ("bboxes" in data or "boxes" in data):
        bboxes = data.get("bboxes") or data.get("boxes") or []
        labels = data.get("labels") or [""] * len(bboxes)
        scores = data.get("scores") or [1.0] * len(bboxes)
        return bboxes, labels, scores

    # Case B: list of {bbox,label,score} dicts
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

    # Fallback: nothing usable
    return [], [], []

prev_t = time.time()
while True:
    ok, frame = cap.read()
    if not ok:
        print("Camera frame not available.")
        break

    H, W = frame.shape[:2]

    # Convert BGR to RGB for Florence-2
    rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    pil_image = Image.fromarray(rgb_frame)

    # Prepare inputs
    inputs = processor(text=DETECTION_PROMPT, images=pil_image, return_tensors="pt")
    inputs = move_to_device(inputs, DEVICE)

    # Inference
    with torch.inference_mode():
        generated_ids = model.generate(
            **inputs,
            max_new_tokens=256,   # keep small for realtime
            num_beams=1,          # greedy is faster + more stable on MPS
            do_sample=False
        )

    # Decode + post-process
    generated_text = processor.batch_decode(generated_ids, skip_special_tokens=False)[0]
    parsed_answer = processor.post_process_generation(
        generated_text,
        task=DETECTION_PROMPT,
        image_size=(W, H),  # let processor scale to pixel coords when possible
    )

    bboxes, labels, scores = parse_od_result(parsed_answer, DETECTION_PROMPT)

    # Draw detections
    det_count = 0
    for bbox, label, score in zip(bboxes, labels, scores):
        if score is not None and score < CONF_THRES:
            continue

        # Florence may return normalized or pixel coords; detect and convert if needed
        x1, y1, x2, y2 = bbox
        if max(x2, y2) <= 1.5:  # likely normalized
            x1, x2 = x1 * W, x2 * W
            y1, y2 = y1 * H, y2 * H

        x1, y1, x2, y2 = map(int, (round(x1), round(y1), round(x2), round(y2)))
        x1 = max(0, min(x1, W - 1)); x2 = max(0, min(x2, W - 1))
        y1 = max(0, min(y1, H - 1)); y2 = max(0, min(y2, H - 1))
        if x2 <= x1 or y2 <= y1:
            continue

        det_count += 1
        cv2.rectangle(frame, (x1, y1), (x2, y2), (0, 255, 0), 2)
        label_text = f"{label} {score:.2f}" if score is not None else f"{label}"
        cv2.putText(frame, label_text, (x1, max(0, y1 - 6)),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 2, cv2.LINE_AA)

    # FPS overlay
    now = time.time()
    fps = 1.0 / max(now - prev_t, 1e-6)
    prev_t = now
    cv2.putText(frame, f"FPS: {fps:.1f}   Device: {DEVICE}",
                (8, 20), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2, cv2.LINE_AA)

    # Detection count overlay
    cv2.putText(frame, f"Detections: {det_count}",
                (8, 50), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2, cv2.LINE_AA)

    cv2.imshow("Florence-2 Object Detection", frame)
    if cv2.waitKey(1) & 0xFF == 27:  # ESC
        break

cap.release()
cv2.destroyAllWindows()
