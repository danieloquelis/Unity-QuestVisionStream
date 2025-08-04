// QuestVisionStreamManager.kt - Updated version
package com.xreducation.questvisionstreamplugin

import android.app.Activity
import android.util.Log
import org.json.JSONObject
import org.webrtc.*

class QuestVisionStreamManager(private val activity: Activity) {
    private lateinit var peerConnectionFactory: PeerConnectionFactory
    private var peerConnection: PeerConnection? = null
    private var videoTrack: VideoTrack? = null
    private var signalingClient: SignalingClient? = null
    private var eglBase: EglBase? = null
    private var videoCapturer: VideoCapturer? = null
    private var pixelDataCapturer: PixelDataVideoCapturer? = null
    private var usePixelDataMethod = false

    // ADDED: Hold ICE servers passed from Unity
    private var iceServers: MutableList<PeerConnection.IceServer> = mutableListOf()

    init {
        try {
            System.loadLibrary("jingle_peerconnection_so")
            Log.i("QuestVisionStreamPlugin", "WebRTC native library loaded successfully")
        } catch (e: UnsatisfiedLinkError) {
            Log.e("QuestVisionStreamPlugin", "Failed to load WebRTC library: ${e.message}")
        }
        initPeerConnectionFactory()
    }

    private fun initPeerConnectionFactory() {
        eglBase = EglUtils.eglBase
        Log.i("QuestVisionStreamPlugin", "Initializing PeerConnectionFactory...")

        val encoderFactory = DefaultVideoEncoderFactory(eglBase!!.eglBaseContext, true, true)
        val decoderFactory = DefaultVideoDecoderFactory(eglBase!!.eglBaseContext)

        PeerConnectionFactory.initialize(
            PeerConnectionFactory.InitializationOptions.builder(activity)
                .setEnableInternalTracer(true)
                .createInitializationOptions()
        )

        peerConnectionFactory = PeerConnectionFactory.builder()
            .setVideoEncoderFactory(encoderFactory)
            .setVideoDecoderFactory(decoderFactory)
            .createPeerConnectionFactory()

        Log.i("QuestVisionStreamPlugin", "PeerConnectionFactory initialized successfully")
    }

    // ADDED: Method to receive ICE servers from Unity
    fun setIceServers(servers: List<String>) {
        iceServers.clear()
        for (url in servers) {
            iceServers.add(PeerConnection.IceServer.builder(url).createIceServer())
            Log.i("QuestVisionStreamPlugin", "ICE server added: $url")
        }
    }

    fun connectToSignalingServer(url: String) {
        Log.i("QuestVisionStreamPlugin", "Connecting to signaling server: $url")
        signalingClient = SignalingClient(url) { msg ->
            Log.i("QuestVisionStreamPlugin", "Received signal: $msg")
            handleRemoteMessage(msg)
        }
        signalingClient?.connect()
    }

    fun setExternalTexture(texPtr: Long, width: Int, height: Int) {
        Log.i("QuestVisionStreamPlugin", "Setting external texture $width x $height, texPtr: $texPtr")

        if (texPtr == 0L) {
            // Use pixel data method
            usePixelDataMethod = true
            Log.i("QuestVisionStreamPlugin", "Using pixel data method")
            
            pixelDataCapturer = PixelDataVideoCapturer(width, height)
            val source = peerConnectionFactory.createVideoSource(false)
            
            pixelDataCapturer?.initializePixelCapture(activity, source.capturerObserver)
            pixelDataCapturer?.startCapture(width, height, 15) // Lower framerate for CPU method
            Log.i("QuestVisionStreamPlugin", "Pixel data capturer started")
            
            videoTrack = peerConnectionFactory.createVideoTrack("ARDAMSv0", source)
        } else {
            // Texture pointer method is not fully implemented yet
            Log.w("QuestVisionStreamPlugin", "⚠️ Texture pointer method not fully implemented")
            Log.w("QuestVisionStreamPlugin", "Falling back to pixel data method for now")
            Log.w("QuestVisionStreamPlugin", "Please set usePixelDataMethod = true in Unity for best results")
            
            // Fall back to pixel data method
            usePixelDataMethod = true
            pixelDataCapturer = PixelDataVideoCapturer(width, height)
            val source = peerConnectionFactory.createVideoSource(false)
            
            pixelDataCapturer?.initializePixelCapture(activity, source.capturerObserver)
            pixelDataCapturer?.startCapture(width, height, 15)
            Log.i("QuestVisionStreamPlugin", "Fallback pixel data capturer started")
            
            videoTrack = peerConnectionFactory.createVideoTrack("ARDAMSv0", source)
            
            // TODO: Implement proper texture method later
            // videoCapturer = UnityTextureVideoCapturer(texPtr, width, height)
            // val helper = SurfaceTextureHelper.create("CaptureThread", eglBase!!.eglBaseContext)
            // videoCapturer?.initialize(helper, activity, source.capturerObserver)
            // videoCapturer?.startCapture(width, height, 30)
        }

        createPeerConnection(videoTrack!!)
    }
    
    fun updateFrameData(pixelData: ByteArray, width: Int, height: Int) {
        if (usePixelDataMethod && pixelDataCapturer != null) {
            pixelDataCapturer?.updateFrame(pixelData, width, height)
        } else {
            Log.w("QuestVisionStreamPlugin", "updateFrameData called but pixel data method not active")
        }
    }

    private fun createPeerConnection(videoTrack: VideoTrack) {
        // ADDED: use ICE servers set from Unity (fallback to empty if none)
        val config = PeerConnection.RTCConfiguration(iceServers).apply {
            sdpSemantics = PeerConnection.SdpSemantics.UNIFIED_PLAN
        }

        Log.i("QuestVisionStreamPlugin", "Creating PeerConnection with ${iceServers.size} ICE servers...")
        peerConnection = peerConnectionFactory.createPeerConnection(config, object : PeerConnection.Observer {
            override fun onIceCandidate(candidate: IceCandidate) {
                val json = JSONObject().apply {
                    put("type", "candidate")
                    put("candidate", candidate.sdp)
                    put("sdpMid", candidate.sdpMid)
                    put("sdpMLineIndex", candidate.sdpMLineIndex)
                }
                Log.i("QuestVisionStreamPlugin", "Sending ICE candidate")
                signalingClient?.send(json.toString())
            }

            override fun onIceGatheringChange(newState: PeerConnection.IceGatheringState) {
                Log.i("QuestVisionStreamPlugin", "ICE gathering state changed: $newState")
            }

            override fun onConnectionChange(newState: PeerConnection.PeerConnectionState) {
                Log.i("QuestVisionStreamPlugin", "PeerConnection state: $newState")
            }

            override fun onIceConnectionChange(newState: PeerConnection.IceConnectionState) {
                Log.i("QuestVisionStreamPlugin", "ICE connection state: $newState")
            }

            override fun onRenegotiationNeeded() {
                Log.i("QuestVisionStreamPlugin", "Renegotiation needed")
            }

            override fun onSignalingChange(newState: PeerConnection.SignalingState) {
                Log.i("QuestVisionStreamPlugin", "Signaling state: $newState")
            }

            override fun onTrack(transceiver: RtpTransceiver) {
                Log.i("QuestVisionStreamPlugin", "Remote track added: ${transceiver.receiver.track()?.kind()}")
            }

            override fun onDataChannel(channel: DataChannel) {
                Log.i("QuestVisionStreamPlugin", "DataChannel opened: ${channel.label()}")
            }

            override fun onAddStream(stream: MediaStream) {}
            override fun onRemoveStream(stream: MediaStream) {}
            override fun onIceCandidatesRemoved(p0: Array<IceCandidate>) {}
            override fun onStandardizedIceConnectionChange(newState: PeerConnection.IceConnectionState) {}
            override fun onIceConnectionReceivingChange(p0: Boolean) {}
        })

        peerConnection?.addTrack(videoTrack, listOf("ARDAMS"))
        Log.i("QuestVisionStreamPlugin", "Video track added to PeerConnection")
        createOffer()
    }

    private fun createOffer() {
        val mediaConstraints = MediaConstraints()
        Log.i("QuestVisionStreamPlugin", "Creating WebRTC offer...")
        peerConnection?.createOffer(object : SdpObserver {
            override fun onCreateSuccess(desc: SessionDescription) {
                Log.i("QuestVisionStreamPlugin", "Offer created successfully")
                peerConnection?.setLocalDescription(this, desc)
                val json = JSONObject()
                json.put("type", "offer")
                json.put("sdp", desc.description)
                signalingClient?.send(json.toString())
                Log.i("QuestVisionStreamPlugin", "Offer sent to signaling server")
            }

            override fun onCreateFailure(error: String) {
                Log.e("QuestVisionStreamPlugin", "Offer creation failed: $error")
            }

            override fun onSetSuccess() {
                Log.i("QuestVisionStreamPlugin", "Local description set successfully")
            }

            override fun onSetFailure(error: String) {
                Log.e("QuestVisionStreamPlugin", "SetLocalDescription failed: $error")
            }
        }, mediaConstraints)
    }

    private fun handleRemoteMessage(message: String) {
        val json = JSONObject(message)
        when (json.getString("type")) {
            "answer" -> {
                val sdp = SessionDescription(SessionDescription.Type.ANSWER, json.getString("sdp"))
                Log.i("QuestVisionStreamPlugin", "Received answer from server")
                peerConnection?.setRemoteDescription(object : SdpObserver {
                    override fun onSetSuccess() {
                        Log.i("QuestVisionStreamPlugin", "Remote answer applied")
                    }
                    override fun onSetFailure(error: String) {
                        Log.e("QuestVisionStreamPlugin", "SetRemoteDescription failed: $error")
                    }
                    override fun onCreateSuccess(p0: SessionDescription?) {}
                    override fun onCreateFailure(p0: String?) {}
                }, sdp)
            }
            "candidate" -> {
                Log.i("QuestVisionStreamPlugin", "Received ICE candidate from server")
                val candidate = IceCandidate(
                    json.getString("sdpMid"),
                    json.getInt("sdpMLineIndex"),
                    json.getString("candidate")
                )
                peerConnection?.addIceCandidate(candidate)
            }
        }
    }
}