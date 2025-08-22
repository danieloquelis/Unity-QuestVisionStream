import asyncio
import json
import argparse
from video_processor import VideoProcessor
from webrtc_server import WebRTCServer
from config import *
from detectors import get_detector

def parse_arguments():
    parser = argparse.ArgumentParser(description='QuestVisionStream Server')
    parser.add_argument('--detector', 
                       choices=['yolo', 'florence2', 'owl2', 'body'], 
                       default='yolo',
                       help='Type of detector to use (default: yolo)')
    return parser.parse_args()

async def main():
    args = parse_arguments()
    
    detector_func = get_detector(args.detector)
    if detector_func is None:
        print(f"Error: Unknown detector '{args.detector}'")
        return
    
    print(f"WebSocket server: ws://{HOST}:{PORT}")
    print(f"Using detector: {args.detector}")
    
    video_processor = VideoProcessor(
        enable_display=ENABLE_DISPLAY,
        flip_vertical=FLIP_VERTICAL,
        flip_horizontal=FLIP_HORIZONTAL,
        rotate_180=ROTATE_180,
        log_interval=LOG_INTERVAL
    )
    
    def frame_callback(img, frame):
        if args.detector == 'body':
            # body_tracker returns different format
            return detector_func(img, frame)
        else:
            # Other detectors return (img, detections)
            processed_img, detections = detector_func(img, frame)
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
