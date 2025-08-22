import cv2
import numpy as np
import torch
from ultralytics import YOLO
from typing import List, Dict, Any

MODEL_PATH = "models/yolo11n.pt"
CONF_THRES = 0.6
DEVICE = (
    "mps" if torch.backends.mps.is_available() else
    ("cuda" if torch.cuda.is_available() else "cpu")
)

print(f"Loading YOLO model on {DEVICE}...")
model = YOLO(MODEL_PATH)
print("YOLO model loaded!")

IGNORE_CLASSES = {"person", "car", "truck", "bus", "motorcycle", "bicycle"}

def detect_objects(img: np.ndarray, frame=None):
    results = model(img, conf=CONF_THRES, device=DEVICE, verbose=False)[0]

    detections: List[Dict[str, Any]] = []
    if results.boxes is not None:
        for box in results.boxes:
            x1, y1, x2, y2 = box.xyxy[0].tolist()
            cls_id = int(box.cls[0]) if box.cls is not None else -1
            conf = float(box.conf[0]) if box.conf is not None else 0.0
            label = results.names[cls_id] if cls_id >= 0 else "object"

            # Filter out ignored classes
            if label in IGNORE_CLASSES:
                continue

            # Draw for debug window
            cv2.rectangle(img, (int(x1), int(y1)), (int(x2), int(y2)), (0, 255, 0), 2)
            cv2.putText(img, f"{label} {conf:.2f}", (int(x1), int(y1) - 6),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 2, cv2.LINE_AA)

            detections.append({
                "label": label,
                "conf": conf,
                "bbox": [float(x1), float(y1), float(x2), float(y2)],
            })

    return img, detections