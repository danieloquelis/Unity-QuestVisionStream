import asyncio
import json
import cv2
import websockets
from aiortc import (
    RTCPeerConnection,
    RTCSessionDescription,
    RTCConfiguration,
    RTCIceServer,
)
from aiortc.sdp import candidate_from_sdp

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
                try:
                    print("[Server] Entering video processing loop...")
                    while True:
                        try:
                            print(f"[Server] Waiting for frame {frame_count + 1}...")
                            frame = await track.recv()
                            frame_count += 1
                            current_time = asyncio.get_event_loop().time()
                            
                            print(f"[Server] ✅ Received frame {frame_count}, size: {frame.width}x{frame.height}, format: {frame.format}")
                            
                            # Log every 30 frames or every 5 seconds
                            if frame_count % 30 == 0 or (current_time - last_log_time) > 5:
                                print(f"[Server] Frame stats - Total: {frame_count}, Size: {frame.width}x{frame.height}, Time: {current_time - last_log_time:.2f}s")
                                last_log_time = current_time
                            
                            img = frame.to_ndarray(format="bgr24")
                            
                            # Validate image data
                            if img is not None and img.size > 0:
                                print(f"[Server] Image converted successfully, shape: {img.shape}, dtype: {img.dtype}")
                                cv2.imshow("Quest PCA Stream", img)
                                key = cv2.waitKey(1) & 0xFF
                                if key == ord("q"):
                                    print("[Server] Quit requested by user")
                                    break
                            else:
                                print("[Server] ⚠️ Warning: Received empty or invalid frame")
                                
                        except Exception as e:
                            print(f"[Server] ❌ Error processing video frame: {e}")
                            import traceback
                            traceback.print_exc()
                            await asyncio.sleep(0.1)  # Brief pause before retrying
                            
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
    print("Signaling server starting on ws://0.0.0.0:3000")
    async with websockets.serve(signaling_handler, "0.0.0.0", 3000, ping_interval=20, ping_timeout=20):
        await asyncio.Future()

if __name__ == "__main__":
    asyncio.run(main())
