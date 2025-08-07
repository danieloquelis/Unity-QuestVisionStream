# body_tracking.py
import cv2
import numpy as np
import mediapipe as mp

mp_pose = mp.solutions.pose
mp_drawing = mp.solutions.drawing_utils

pose_detector = mp_pose.Pose(
    static_image_mode=False,
    model_complexity=1,
    enable_segmentation=False,
    min_detection_confidence=0.5,
    min_tracking_confidence=0.5
)

def track_body(img: np.ndarray, frame=None) -> np.ndarray:
    """
    Detects and draws body landmarks on the input image using MediaPipe Pose.
    """
    # Convert BGR image to RGB for MediaPipe
    rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)

    # Run MediaPipe Pose
    results = pose_detector.process(rgb)

    if results.pose_landmarks:
        mp_drawing.draw_landmarks(
            img,
            results.pose_landmarks,
            mp_pose.POSE_CONNECTIONS,
            mp_drawing.DrawingSpec(color=(0, 255, 0), thickness=2, circle_radius=2),
            mp_drawing.DrawingSpec(color=(0, 0, 255), thickness=2)
        )

    return img
