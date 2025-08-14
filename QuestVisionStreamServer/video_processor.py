import cv2
import asyncio
from typing import Optional, Callable, Any, Dict, List
import json

class VideoProcessor:
    def __init__(self, 
                 enable_display: bool = True,
                 flip_vertical: bool = True,
                 flip_horizontal: bool = False,
                 rotate_180: bool = False,
                 log_interval: int = 30):
        self.enable_display = enable_display
        self.flip_vertical = flip_vertical
        self.flip_horizontal = flip_horizontal
        self.rotate_180 = rotate_180
        self.log_interval = log_interval
        
        self.frame_count = 0
        self.last_fps_time = 0
        self.fps_frame_count = 0
        self.frame_callback: Optional[Callable] = None
        self.data_channel_sender = None  # function to send JSON over data channel
    
    def set_frame_callback(self, callback: Callable):
        self.frame_callback = callback

    def set_data_channel_sender(self, sender: Callable[[Dict[str, Any]], None]):
        self.data_channel_sender = sender
    
    def process_frame(self, frame) -> Optional[cv2.Mat]:
        try:
            img = frame.to_ndarray(format="bgr24")
            
            if img is None or img.size == 0:
                return None
            
            if self.rotate_180:
                img = cv2.rotate(img, cv2.ROTATE_180)
            else:
                if self.flip_vertical:
                    img = cv2.flip(img, 0)
                if self.flip_horizontal:
                    img = cv2.flip(img, 1)
            
            return img
            
        except Exception as e:
            print(f"[VideoProcessor] Error processing frame: {e}")
            return None
    
    def display_frame(self, img: cv2.Mat) -> bool:
        if not self.enable_display:
            return True
        
        cv2.imshow("Quest PCA Stream", img)
        
        key = cv2.waitKey(1) & 0xFF
        if key == ord("q"):
            return False
        
        return True
    
    def log_frame_info(self, frame, fps: float):
        if self.frame_count % self.log_interval == 0:
            print(f"[VideoProcessor] Frame {self.frame_count} | Size: {frame.width}x{frame.height} | FPS: {fps:.1f}")
    
    async def process_video_stream(self, track):
        print("[VideoProcessor] Starting video processing...")
        
        try:
            while True:
                frame = await track.recv()
                self.frame_count += 1
                self.fps_frame_count += 1
                
                should_log = self.frame_count % self.log_interval == 0
                current_time = asyncio.get_event_loop().time() if should_log else 0
                
                if should_log:
                    fps = self.fps_frame_count / (current_time - self.last_fps_time) if (current_time - self.last_fps_time) > 0 else 0
                    self.log_frame_info(frame, fps)
                    self.last_fps_time = current_time
                    self.fps_frame_count = 0
                
                processed_img = self.process_frame(frame)
                if processed_img is None:
                    continue
                
                if self.frame_callback:
                    # Expect callback to optionally return detections to forward
                    detections = self.frame_callback(processed_img, frame)
                    if detections is not None and self.data_channel_sender is not None:
                        try:
                            height, width = processed_img.shape[:2]
                            self.data_channel_sender({
                                "type": "detections",
                                "frame": self.frame_count,
                                "width": int(width),
                                "height": int(height),
                                "detections": detections,
                            })
                        except Exception as e:
                            print(f"[VideoProcessor] Failed to send detections: {e}")
                
                if not self.display_frame(processed_img):
                    break
                    
        except Exception as e:
            print(f"[VideoProcessor] Video processing ended: {e}")
        finally:
            cv2.destroyAllWindows()
    
    def cleanup(self):
        cv2.destroyAllWindows() 