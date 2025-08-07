import asyncio
import json
import websockets
from aiortc import (
    RTCPeerConnection,
    RTCSessionDescription,
    RTCConfiguration,
    RTCIceServer,
    RTCDataChannel,
)
from aiortc.sdp import candidate_from_sdp, candidate_to_sdp
from video_processor import VideoProcessor

class WebRTCServer:
    def __init__(self, host: str = "0.0.0.0", port: int = 3000, video_processor: VideoProcessor = None):
        self.host = host
        self.port = port
        self.video_processor = video_processor or VideoProcessor()
        self.pcs = set()
        from typing import Optional
        self.detections_channel: Optional[RTCDataChannel] = None
    
    def set_video_processor(self, video_processor: VideoProcessor):
        self.video_processor = video_processor
    
    async def handle_signaling(self, websocket):
        print("[WebRTC] Quest connected")
        
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
            print(f"[WebRTC] ICE: {pc.iceConnectionState}")
        
        @pc.on("connectionstatechange")
        async def on_connection_state_change():
            print(f"[WebRTC] PC: {pc.connectionState}")
        
        @pc.on("track")
        async def on_track(track):
            print(f"[WebRTC] Track: {track.kind}")
            if track.kind == "video":
                print(f"[WebRTC] Video: {track.id}")
                # Start video processing in separate task
                nonlocal video_task
                video_task = asyncio.create_task(
                    self.video_processor.process_video_stream(track)
                )
                print("[WebRTC] Processing started")

        @pc.on("datachannel")
        def on_datachannel(channel: RTCDataChannel):
            print(f"[WebRTC] DC: {channel.label}")
            if channel.label == "detections":
                self.detections_channel = channel
                @channel.on("open")
                def _on_open():
                    try:
                        channel.send(json.dumps({"type": "ready"}))
                    except Exception:
                        pass
                @channel.on("message")
                def _on_message(message):
                    # No-op; Quest may send messages if needed
                    return

        @pc.on("icecandidate")
        async def on_icecandidate(cand):
            if cand is None:
                return
            payload = {
                "type": "candidate",
                "candidate": candidate_to_sdp(cand),
                "sdpMid": cand.sdpMid,
                "sdpMLineIndex": cand.sdpMLineIndex,
            }
            try:
                await websocket.send(json.dumps(payload))
            except Exception:
                pass
        
        try:
            async for message in websocket:
                data = json.loads(message)
                # messages: offer | candidate
                
                if data["type"] == "offer":
                    if offer_received:
                        # ignore duplicate
                        continue
                    
                    offer_received = True
                    print("[WebRTC] Offer")
                    
                    offer = RTCSessionDescription(sdp=data["sdp"], type="offer")
                    await pc.setRemoteDescription(offer)
                    
                    answer = await pc.createAnswer()
                    await pc.setLocalDescription(answer)
                    
                    response = {"type": "answer", "sdp": pc.localDescription.sdp}
                    await websocket.send(json.dumps(response))
                    print("[WebRTC] Answer")
                
                elif data["type"] == "candidate":
                    # add remote candidate
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