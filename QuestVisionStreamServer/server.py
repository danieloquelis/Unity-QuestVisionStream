import asyncio
import json
import cv2
import websockets
import time
from aiortc import (
    RTCPeerConnection,
    RTCSessionDescription,
    RTCConfiguration,
    RTCIceServer,
)
from aiortc.sdp import candidate_from_sdp

# 🚀 PERFORMANCE SETTINGS
ENABLE_DISPLAY = True      # Set to False for headless mode (fastest)
LOG_INTERVAL = 30          # Log every N frames (30 = ~every 10 seconds at 3 FPS)
MAX_FPS_DISPLAY = 30       # Limit display refresh rate
ENABLE_DEBUG_KEYS = True   # Enable keyboard shortcuts

# 🚀 IMAGE PROCESSING SETTINGS
FLIP_VERTICAL = True       # Fix upside-down image (Unity → OpenCV coordinate fix)
FLIP_HORIZONTAL = False    # Mirror image horizontally if needed
ROTATE_180 = False         # Rotate image 180 degrees (alternative to flipping both axes)

pcs = set()

async def signaling_handler(websocket):
    print("Quest connected for signaling")

    # Use STUN for gathering public ICE candidates
    config = RTCConfiguration(
        iceServers=[RTCIceServer(urls=["stun:stun.l.google.com:19302"])]
    )
    pc = RTCPeerConnection(configuration=config)
    pcs.add(pc)

    offer_received = False
    video_task = None

    @pc.on("iceconnectionstatechange")
    async def on_ice_state_change():
        print(f"[Server] ICE connection state changed: {pc.iceConnectionState}")

    @pc.on("connectionstatechange")
    async def on_connection_state_change():
        print(f"[Server] PeerConnection state changed: {pc.connectionState}")

    @pc.on("track")
    async def on_track(track):
        print(f"[Server] Track received: {track.kind}")
        if track.kind == "video":
            print(f"[Server] Starting video processing for track: {track.id}")
            print(f"[Server] Track ready state: {track.readyState}")
            
            # Create a separate task for video processing
            async def process_video():
                frame_count = 0
                last_log_time = asyncio.get_event_loop().time()
                last_fps_time = last_log_time
                fps_frame_count = 0
                
                try:
                    print("[Server] 🚀 Entering OPTIMIZED video processing loop...")
                    while True:
                        try:
                            # 🚀 OPTIMIZED: Removed per-frame logging
                            frame = await track.recv()
                            frame_count += 1
                            fps_frame_count += 1
                            
                            # 🚀 OPTIMIZED: Only calculate time when needed for logging
                            should_log = frame_count % LOG_INTERVAL == 0
                            current_time = asyncio.get_event_loop().time() if should_log else 0
                            
                            # 🚀 OPTIMIZED: Configurable logging frequency
                            if should_log:
                                fps = fps_frame_count / (current_time - last_fps_time) if (current_time - last_fps_time) > 0 else 0
                                print(f"[Server] 📊 Frame {frame_count} | Size: {frame.width}x{frame.height} | FPS: {fps:.1f} | Format: {frame.format}")
                                last_log_time = current_time
                                last_fps_time = current_time
                                fps_frame_count = 0
                            
                            # 🚀 OPTIMIZED: Conditional display processing
                            if ENABLE_DISPLAY:
                                # Fast frame conversion
                                img = frame.to_ndarray(format="bgr24")
                                
                                # Quick validation without logging
                                if img is not None and img.size > 0:
                                    # 🚀 IMAGE TRANSFORMATIONS: Fix Unity → OpenCV coordinate differences
                                    if ROTATE_180:
                                        # Rotate 180 degrees (fastest for complete flip)
                                        img = cv2.rotate(img, cv2.ROTATE_180)
                                    else:
                                        # Individual axis flips (more control)
                                        if FLIP_VERTICAL:
                                            img = cv2.flip(img, 0)  # 0 = flip vertically
                                        if FLIP_HORIZONTAL:
                                            img = cv2.flip(img, 1)  # 1 = flip horizontally
                                    
                                    # Non-blocking display update
                                    cv2.imshow("Quest PCA Stream", img)
                                    
                                    # Non-blocking key check
                                    if ENABLE_DEBUG_KEYS:
                                        key = cv2.waitKey(1) & 0xFF
                                        if key == ord("q"):
                                            print("[Server] Quit requested by user")
                                            break
                                        elif key == ord("f"):
                                            # Toggle fullscreen with 'f' key
                                            cv2.setWindowProperty("Quest PCA Stream", cv2.WND_PROP_FULLSCREEN, 
                                                                cv2.WINDOW_FULLSCREEN if cv2.getWindowProperty("Quest PCA Stream", cv2.WND_PROP_FULLSCREEN) == 0 else cv2.WINDOW_NORMAL)
                                        elif key == ord("s"):
                                            # Save screenshot with 's' key (already transformed)
                                            timestamp = int(time.time())
                                            filename = f"quest_frame_{timestamp}.jpg"
                                            cv2.imwrite(filename, img)
                                            print(f"[Server] 📸 Screenshot saved: {filename} ({frame.width}x{frame.height})")
                                        elif key == ord("r"):
                                            # Save raw frame without transformations
                                            raw_img = frame.to_ndarray(format="bgr24")
                                            timestamp = int(time.time())
                                            filename = f"quest_frame_raw_{timestamp}.jpg"
                                            cv2.imwrite(filename, raw_img)
                                            print(f"[Server] 📸 Raw screenshot saved: {filename}")
                                    else:
                                        cv2.waitKey(1)  # Still need this for OpenCV window updates
                                else:
                                    # Only log errors occasionally
                                    if frame_count % 100 == 0:
                                        print(f"[Server] ⚠️ Warning: Received empty frame at count {frame_count}")
                            else:
                                # 🚀 HEADLESS MODE: Maximum performance, no display
                                if should_log:
                                    print(f"[Server] 🚀 HEADLESS: Processing frame {frame_count} (no display)")
                                
                        except Exception as e:
                            print(f"[Server] ❌ Error processing video frame {frame_count}: {e}")
                            # 🚀 OPTIMIZED: Only print traceback for first few errors
                            if frame_count < 10:
                                import traceback
                                traceback.print_exc()
                            await asyncio.sleep(0.01)  # Shorter pause for faster recovery
                            
                except Exception as e:
                    print(f"[Server] Video processing task ended: {e}")
                    import traceback
                    traceback.print_exc()
                finally:
                    print("[Server] Video processing task cleanup")
                    cv2.destroyAllWindows()
            
            # Start video processing in a separate task
            video_task = asyncio.create_task(process_video())
            print("[Server] Video processing task started")

    try:
        async for message in websocket:
            data = json.loads(message)
            print(f"[Server] Received: {data['type']}")

            if data["type"] == "offer":
                if offer_received:
                    print("[Server] Ignoring duplicate offer")
                    continue

                offer_received = True
                print("[Server] Offer received")
                offer = RTCSessionDescription(sdp=data["sdp"], type="offer")
                await pc.setRemoteDescription(offer)

                answer = await pc.createAnswer()
                await pc.setLocalDescription(answer)

                response = {"type": "answer", "sdp": pc.localDescription.sdp}
                await websocket.send(json.dumps(response))
                print("[Server] Answer sent")

            elif data["type"] == "candidate":
                print("[Server] ICE candidate received")
                # ✅ Properly parse candidate string
                cand = candidate_from_sdp(data["candidate"])
                cand.sdpMid = data["sdpMid"]
                cand.sdpMLineIndex = int(data["sdpMLineIndex"])
                await pc.addIceCandidate(cand)

    except websockets.ConnectionClosed:
        print("[Server] Quest disconnected")
    except Exception as e:
        print(f"[Server] Error: {e}")
    finally:
        # Cancel video processing task if it exists
        if video_task and not video_task.done():
            video_task.cancel()
            try:
                await video_task
            except asyncio.CancelledError:
                pass
        
        await pc.close()
        pcs.discard(pc)
        cv2.destroyAllWindows()

async def main():
    print("🚀 OPTIMIZED Quest Vision Stream Server")
    print("=" * 60)
    print(f"📺 Display mode: {'Enabled' if ENABLE_DISPLAY else 'HEADLESS (fastest)'}")
    print(f"📊 Log interval: Every {LOG_INTERVAL} frames")
    print(f"🌐 WebSocket server: ws://0.0.0.0:3000")
    print()
    print("🖼️  IMAGE PROCESSING:")
    if ROTATE_180:
        print("   🔄 Rotate 180°: Enabled")
    else:
        print(f"   ↕️  Flip vertical: {'✅' if FLIP_VERTICAL else '❌'} (fixes upside-down)")
        print(f"   ↔️  Flip horizontal: {'✅' if FLIP_HORIZONTAL else '❌'}")
    print()
    if ENABLE_DEBUG_KEYS:
        print("⌨️  DEBUG KEYS:")
        print("   q = Quit")
        print("   f = Toggle fullscreen")
        print("   s = Save screenshot (processed)")
        print("   r = Save raw screenshot (unprocessed)")
    else:
        print("⌨️  Debug keys: Disabled")
    print("=" * 60)
    
    async with websockets.serve(signaling_handler, "0.0.0.0", 3000, ping_interval=20, ping_timeout=20):
        await asyncio.Future()

if __name__ == "__main__":
    asyncio.run(main())
