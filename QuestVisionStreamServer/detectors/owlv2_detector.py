import cv2
import numpy as np
import torch
from typing import List, Dict, Any
from PIL import Image
from transformers import AutoProcessor, AutoModelForZeroShotObjectDetection

MODEL_ID = "google/owlv2-base-patch16-ensemble"    # alt: "google/owlv2-large-patch14"
TEXT_QUERIES = ["glasses", "scissors", "phone"]    # edit freely
CONF_THRES = 0.30
DEVICE = (
    "mps" if getattr(torch.backends, "mps", None) and torch.backends.mps.is_available()
    else ("cuda" if torch.cuda.is_available() else "cpu")
)

print(f"Loading OWLv2 ({MODEL_ID}) on {DEVICE}...")
_processor = AutoProcessor.from_pretrained(MODEL_ID)
_model = AutoModelForZeroShotObjectDetection.from_pretrained(MODEL_ID).to(DEVICE)
_model.eval()
print("OWLv2 model loaded!")

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

        # OWLv2 expects list-of-list for text batch
        inputs = _processor(images=pil, text=[TEXT_QUERIES], return_tensors="pt").to(DEVICE)

        with torch.inference_mode():
            outputs = _model(**inputs)

        results = _processor.post_process_object_detection(
            outputs=outputs,
            target_sizes=[(H, W)],
            threshold=CONF_THRES
        )[0]

        boxes = results.get("boxes", [])
        scores = results.get("scores", [])
        labels = results.get("labels", [])

        # move to CPU lists if needed
        if hasattr(boxes, "cpu"):
            boxes = boxes.cpu()
        if hasattr(scores, "cpu"):
            scores = scores.cpu()
        if hasattr(labels, "cpu"):
            labels = labels.cpu()

        detections: List[Dict[str, Any]] = []
        for box, score, lab in zip(boxes, scores, labels):
            x1, y1, x2, y2 = [int(v) for v in (box.tolist() if hasattr(box, "tolist") else box)]
            conf = float(score.item() if hasattr(score, "item") else score)
            idx = int(lab.item() if hasattr(lab, "item") else lab)
            text = TEXT_QUERIES[idx] if 0 <= idx < len(TEXT_QUERIES) else str(idx)


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
        print(f"[OWLv2] detect_objects error: {e}")
        return img, []
