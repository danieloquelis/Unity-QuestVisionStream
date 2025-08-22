import cv2
import numpy as np
import torch
from typing import List, Dict, Any
from PIL import Image
from transformers import AutoProcessor, AutoModelForZeroShotObjectDetection

MODEL_ID = "IDEA-Research/grounding-dino-tiny"    # try: "IDEA-Research/grounding-dino-base" for higher accuracy
PROMPT = "glasses"                                # e.g., "glasses", or "person . laptop ."
CONF_THRES = 0.35                                 # post-process 'threshold'
TEXT_THRES = 0.25                                 # text alignment threshold
DEVICE = (
    "mps" if getattr(torch.backends, "mps", None) and torch.backends.mps.is_available()
    else ("cuda" if torch.cuda.is_available() else "cpu")
)

print(f"Loading Grounding DINO ({MODEL_ID}) on {DEVICE}...")
_processor = AutoProcessor.from_pretrained(MODEL_ID)
_model = AutoModelForZeroShotObjectDetection.from_pretrained(MODEL_ID).to(DEVICE)
_model.eval()
print("Grounding DINO model loaded!")

def _to_pil(bgr: np.ndarray) -> Image.Image:
    return Image.fromarray(cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB))

def detect_objects(img: np.ndarray, frame=None):
    """
    Returns (img_with_drawings, detections)
    detections: List[ { 'label': str, 'conf': float, 'bbox': [x1,y1,x2,y2] } ]
    """
    try:
        H, W = img.shape[:2]
        pil = _to_pil(img)

        # DINO expects list-of-list text for batching
        inputs = _processor(images=pil, text=[[PROMPT]], return_tensors="pt").to(DEVICE)

        with torch.inference_mode():
            outputs = _model(**inputs)

        results = _processor.post_process_grounded_object_detection(
            outputs=outputs,
            input_ids=inputs["input_ids"],
            threshold=CONF_THRES,
            text_threshold=TEXT_THRES,
            target_sizes=[(H, W)]
        )[0]

        boxes = results.get("boxes", [])
        labels = results.get("labels", [])
        scores = results.get("scores", [])

        detections: List[Dict[str, Any]] = []
        for (x1, y1, x2, y2), label, score in zip(boxes, labels, scores):
            # to python ints/floats
            x1, y1, x2, y2 = map(lambda v: int(v.item() if hasattr(v, "item") else v), (x1, y1, x2, y2))
            conf = float(score.item() if hasattr(score, "item") else score)
            text = str(label)

            if x2 <= x1 or y2 <= y1:
                continue

            # draw
            cv2.rectangle(img, (x1, y1), (x2, y2), (0, 255, 0), 2)
            cv2.putText(img, f"{text} {conf:.2f}", (x1, max(0, y1 - 6)),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 2, cv2.LINE_AA)

            detections.append({
                "label": text,
                "conf": conf,
                "bbox": [float(x1), float(y1), float(x2), float(y2)],
            })

        return img, detections

    except Exception as e:
        # keep the server alive on occasional model hiccups
        print(f"[GroundingDINO] detect_objects error: {e}")
        return img, []
