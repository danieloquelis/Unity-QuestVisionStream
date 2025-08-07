import time
import cv2
from ultralytics import YOLO
import torch

# --- config ---
MODEL_PATH = "yolo11n.pt"  # or "yolov8n.pt"
IMG_SIZE = 640
CONF_THRES = 0.25
DEVICE = "mps" if torch.backends.mps.is_available() else "cpu"  # Apple GPU if available
CAM_INDEX = 0

# --- load model ---
model = YOLO(MODEL_PATH)

# --- open camera ---
cap = cv2.VideoCapture(CAM_INDEX)
cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)  # camera 640x480 is fine; YOLO will letterbox to 640 square
cap.set(cv2.CAP_PROP_FPS, 30)            # hint to camera driver; actual FPS may vary

# --- main loop ---
prev_t = time.time()
while True:
    ok, frame = cap.read()
    if not ok:
        break

    # Inference (single image). Ultralytics handles BGR numpy arrays directly.
    # device=DEVICE ensures MPS on Apple Silicon if available.
    results = model(frame, imgsz=IMG_SIZE, conf=CONF_THRES, device=DEVICE, verbose=False)[0]

    # Draw detections
    if results.boxes is not None:
        for box in results.boxes:
            x1, y1, x2, y2 = box.xyxy[0].tolist()
            cls_id = int(box.cls[0])
            conf = float(box.conf[0])
            label = f"{results.names[cls_id]} {conf:.2f}"

            cv2.rectangle(frame, (int(x1), int(y1)), (int(x2), int(y2)), (0, 255, 0), 2)
            cv2.putText(frame, label, (int(x1), int(y1) - 6),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 2, cv2.LINE_AA)

    # FPS overlay
    now = time.time()
    fps = 1.0 / max(now - prev_t, 1e-6)
    prev_t = now
    cv2.putText(frame, f"FPS: {fps:.1f}   Device: {DEVICE}",
                (8, 20), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2, cv2.LINE_AA)

    cv2.imshow("YOLO live", frame)
    if cv2.waitKey(1) & 0xFF == 27:  # ESC to quit
        break

cap.release()
cv2.destroyAllWindows()
