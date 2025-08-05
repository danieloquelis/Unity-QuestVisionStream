import asyncio
import json
import websockets
from aiortc import (
    RTCPeerConnection,
    RTCSessionDescription,
    RTCConfiguration,
    RTCIceServer,
)
from aiortc.sdp import candidate_from_sdp
from video_processor import VideoProcessor

class WebRTCServer:
    def __init__(self, host: str = "0.0.0.0", port: int = 3000, video_processor: VideoProcessor = None):
        self.host = host
        self.port = port
        self.video_processor = video_processor or VideoProcessor()
        self.pcs = set()
    
    def set_video_processor(self, video_processor: VideoProcessor):
        self.video_processor = video_processor
    
    async def handle_signaling(self, websocket):
        print("[WebRTC] Quest connected for signaling")
        
        # Configure WebRTC with STUN server
        config = RTCConfiguration(
            iceServers=[RTCIceServer(urls=["stun:stun.l.google.com:19302"])]
        )
        pc = RTCPeerConnection(configuration=config)
        self.pcs.add(pc)
        
        offer_received = False
        video_task = None
        
        @pc.on("iceconnectionstatechange")
        async def on_ice_state_change():
            print(f"[WebRTC] ICE connection state: {pc.iceConnectionState}")
        
        @pc.on("connectionstatechange")
        async def on_connection_state_change():
            print(f"[WebRTC] Connection state: {pc.connectionState}")
        
        @pc.on("track")
        async def on_track(track):
            print(f"[WebRTC] Track received: {track.kind}")
            if track.kind == "video":
                print(f"[WebRTC] Starting video processing for track: {track.id}")
                
                # Start video processing in separate task
                video_task = asyncio.create_task(
                    self.video_processor.process_video_stream(track)
                )
                print("[WebRTC] Video processing task started")
        
        try:
            async for message in websocket:
                data = json.loads(message)
                print(f"[WebRTC] Received: {data['type']}")
                
                if data["type"] == "offer":
                    if offer_received:
                        print("[WebRTC] Ignoring duplicate offer")
                        continue
                    
                    offer_received = True
                    print("[WebRTC] Processing offer...")
                    
                    offer = RTCSessionDescription(sdp=data["sdp"], type="offer")
                    await pc.setRemoteDescription(offer)
                    
                    answer = await pc.createAnswer()
                    await pc.setLocalDescription(answer)
                    
                    response = {"type": "answer", "sdp": pc.localDescription.sdp}
                    await websocket.send(json.dumps(response))
                    print("[WebRTC] Answer sent")
                
                elif data["type"] == "candidate":
                    print("[WebRTC] Processing ICE candidate...")
                    cand = candidate_from_sdp(data["candidate"])
                    cand.sdpMid = data["sdpMid"]
                    cand.sdpMLineIndex = int(data["sdpMLineIndex"])
                    await pc.addIceCandidate(cand)
        
        except websockets.ConnectionClosed:
            print("[WebRTC] Quest disconnected")
        except Exception as e:
            print(f"[WebRTC] Error: {e}")
        finally:
            # Clean up video processing task
            if video_task and not video_task.done():
                video_task.cancel()
                try:
                    await video_task
                except asyncio.CancelledError:
                    pass
            
            await pc.close()
            self.pcs.discard(pc)
            self.video_processor.cleanup()
    
    async def start(self):
        print(f"[WebRTC] Starting server on {self.host}:{self.port}")
        
        async with websockets.serve(
            self.handle_signaling, 
            self.host, 
            self.port, 
            ping_interval=20, 
            ping_timeout=20
        ):
            await asyncio.Future()
    
    def cleanup(self):
        for pc in self.pcs:
            asyncio.create_task(pc.close())
        self.pcs.clear()
        self.video_processor.cleanup() 