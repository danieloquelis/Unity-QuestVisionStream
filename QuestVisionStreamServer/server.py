import asyncio
from video_processor import VideoProcessor
from webrtc_server import WebRTCServer
from config import *

async def main():
    print(f"WebSocket server: ws://{HOST}:{PORT}")
    
    video_processor = VideoProcessor(
        enable_display=ENABLE_DISPLAY,
        flip_vertical=FLIP_VERTICAL,
        flip_horizontal=FLIP_HORIZONTAL,
        rotate_180=ROTATE_180,
        log_interval=LOG_INTERVAL
    )
    
    server = WebRTCServer(
        host=HOST,
        port=PORT,
        video_processor=video_processor
    )
    
    try:
        await server.start()
    except KeyboardInterrupt:
        print("\nShutting down...")
    finally:
        server.cleanup()

if __name__ == "__main__":
    asyncio.run(main())
