import asyncio
import json
from video_processor import VideoProcessor
from webrtc_server import WebRTCServer
from config import *
# from body_tracker import track_body
# from yolo_detector import detect_objects
from florence2_detector import detect_objects

async def main():
    print(f"WebSocket server: ws://{HOST}:{PORT}")
    
    video_processor = VideoProcessor(
        enable_display=ENABLE_DISPLAY,
        flip_vertical=FLIP_VERTICAL,
        flip_horizontal=FLIP_HORIZONTAL,
        rotate_180=ROTATE_180,
        log_interval=LOG_INTERVAL
    )
    
    # video_processor.set_frame_callback(track_body)
    def frame_callback(img, frame):
        # detect_objects now returns (img, detections)
        processed_img, detections = detect_objects(img, frame)
        return detections

    video_processor.set_frame_callback(frame_callback)

    server = WebRTCServer(
        host=HOST,
        port=PORT,
        video_processor=video_processor
    )

    # Wire data channel sender so the processor can emit detections per frame
    def send_over_dc(payload: dict):
        try:
            if server.detections_channel and server.detections_channel.readyState == "open":
                server.detections_channel.send(json.dumps(payload))
        except Exception as e:
            print(f"[Server] Failed to send over DC: {e}")

    video_processor.set_data_channel_sender(send_over_dc)
    
    try:
        await server.start()
    except KeyboardInterrupt:
        print("\nShutting down...")
    finally:
        server.cleanup()

if __name__ == "__main__":
    asyncio.run(main())
