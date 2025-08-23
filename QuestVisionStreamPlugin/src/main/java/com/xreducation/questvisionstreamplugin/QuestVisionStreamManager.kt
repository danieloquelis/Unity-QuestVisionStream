package com.xreducation.questvisionstreamplugin

import android.app.Activity
import android.util.Log
import org.json.JSONObject
import org.webrtc.*
import com.xreducation.questvisionstreamplugin.capture.PixelDataVideoCapturer
import com.xreducation.questvisionstreamplugin.signaling.SignalingClient
import com.xreducation.questvisionstreamplugin.util.EglUtils
import com.xreducation.questvisionstreamplugin.util.PeerConnectionFactoryProvider
import com.xreducation.questvisionstreamplugin.util.SignalMessages
import com.xreducation.questvisionstreamplugin.util.UnityBridge
import com.xreducation.questvisionstreamplugin.util.Channels
import com.xreducation.questvisionstreamplugin.util.PluginConfig
import java.nio.ByteBuffer.wrap

class QuestVisionStreamManager(private val activity: Activity) {
    private lateinit var peerConnectionFactory: PeerConnectionFactory
    private var peerConnection: PeerConnection? = null
    private var videoTrack: VideoTrack? = null
    private var signalingClient: SignalingClient? = null
    private var eglBase: EglBase? = null
    private var pixelDataCapturer: PixelDataVideoCapturer? = null
    private var usePixelDataMethod = false
    private var iceServers: MutableList<PeerConnection.IceServer> = mutableListOf()
    private var dataChannel: DataChannel? = null
    private val dataChannelByLabel: MutableMap<String, DataChannel> = mutableMapOf()
    private var config: PluginConfig = PluginConfig()

    private var unityCallbackGameObject: String = "QuestVisionStreamReceiver"
    private var unityCallbackMethod: String = "OnDetections"

    init {
        try {
            System.loadLibrary("jingle_peerconnection_so")
            Log.i(TAG, "WebRTC native library loaded successfully")
        } catch (e: UnsatisfiedLinkError) {
            Log.e(TAG, "Failed to load WebRTC library: ${e.message}")
        }
        initPeerConnectionFactory()
    }

    private fun initPeerConnectionFactory() {
        eglBase = EglUtils.eglBase
        peerConnectionFactory = PeerConnectionFactoryProvider.create(activity, eglBase!!)
        Log.i(TAG, "PeerConnectionFactory initialized")
    }

    fun setIceServers(servers: List<String>) {
        iceServers.clear()
        for (url in servers) {
            iceServers.add(PeerConnection.IceServer.builder(url).createIceServer())
            Log.i(TAG, "ICE server added: $url")
        }
    }

    fun addTurnServer(url: String, username: String, credential: String) {
        try {
            val server = PeerConnection.IceServer
                .builder(url)
                .setUsername(username)
                .setPassword(credential)
                .createIceServer()
            iceServers.add(server)
            Log.i(TAG, "TURN server added: $url (u=${username.take(4)}**)")
        } catch (e: Exception) {
            Log.e(TAG, "Failed to add TURN server: $url", e)
        }
    }

    // Configure Unity message target (GameObject and method)
    fun setUnityMessageTarget(gameObjectName: String, methodName: String) {
        unityCallbackGameObject = gameObjectName
        unityCallbackMethod = methodName
        Log.i(TAG, "Unity message target set to $unityCallbackGameObject.$unityCallbackMethod")
    }

    fun connectToSignalingServer(url: String) {
        Log.i(TAG, "Connecting to signaling server: $url")
        signalingClient = SignalingClient(url) { msg ->
            Log.i(TAG, "Received signal: $msg")
            handleRemoteMessage(msg)
        }
        signalingClient?.connect()
    }

    fun setExternalTexture(texPtr: Long, width: Int, height: Int) {
        Log.i(TAG, "Setting external texture $width x $height, texPtr: $texPtr")
        if (texPtr == 0L) {
            startPixelDataCapture(width, height)
        } else {
            Log.w(TAG, "Texture pointer method not fully implemented; falling back to pixel data method")
            startPixelDataCapture(width, height)
        }
        createPeerConnection(videoTrack!!)
    }

    fun setTargetFps(fps: Int) {
        config = config.copy(targetFps = fps)
        pixelDataCapturer?.setTargetFps(fps)
    }

    fun setDesiredResolution(width: Int, height: Int) {
        config = config.copy(desiredWidth = width, desiredHeight = height)
        // Capture restart is not automatic to avoid renegotiation surprises.
        // Unity should call setExternalTexture again if it wants to apply size change.
    }
    
    fun updateFrameData(pixelData: ByteArray, width: Int, height: Int) {
        if (usePixelDataMethod && pixelDataCapturer != null) {
            pixelDataCapturer?.updateFrame(pixelData, width, height)
        } else {
            Log.w(TAG, "updateFrameData called but pixel data method not active")
        }
    }
    
    fun updateFrameDataYUV(yData: ByteArray, uData: ByteArray, vData: ByteArray, width: Int, height: Int) {
        if (usePixelDataMethod && pixelDataCapturer != null) {
            pixelDataCapturer?.updateFrameYUV(yData, uData, vData, width, height)
            Log.d(TAG, "Received YUV data: Y=${yData.size}, U=${uData.size}, V=${vData.size}")
        } else {
            Log.w(TAG, "updateFrameDataYUV called but pixel data method not active")
        }
    }

    private fun createPeerConnection(videoTrack: VideoTrack) {
        val config = buildRtcConfig()
        Log.i(TAG, "Creating PeerConnection with ${iceServers.size} ICE servers...")
        peerConnection = peerConnectionFactory.createPeerConnection(config, object : PeerConnection.Observer {
            override fun onIceCandidate(candidate: IceCandidate) {
                Log.i(TAG, "Sending ICE candidate")
                sendSignal(SignalMessages.candidate(candidate))
            }

            override fun onIceGatheringChange(newState: PeerConnection.IceGatheringState) {
                Log.i(TAG, "ICE gathering state changed: $newState")
            }

            override fun onConnectionChange(newState: PeerConnection.PeerConnectionState) {
                Log.i(TAG, "PeerConnection state: $newState")
                if (newState == PeerConnection.PeerConnectionState.CONNECTED) {
                    UnityBridge.send(unityCallbackGameObject, "OnPeerConnectionStarted", "")
                }
                if (newState == PeerConnection.PeerConnectionState.CLOSED || newState == PeerConnection.PeerConnectionState.FAILED || newState == PeerConnection.PeerConnectionState.DISCONNECTED) {
                    UnityBridge.send(unityCallbackGameObject, "OnPeerConnectionClosed", "")
                }
            }

            override fun onIceConnectionChange(newState: PeerConnection.IceConnectionState) {
                Log.i(TAG, "ICE connection state: $newState")
            }

            override fun onRenegotiationNeeded() {
                Log.i(TAG, "Renegotiation needed")
            }

            override fun onSignalingChange(newState: PeerConnection.SignalingState) {
                Log.i(TAG, "Signaling state: $newState")
            }

            override fun onTrack(transceiver: RtpTransceiver) {
                Log.i(TAG, "Remote track added: ${transceiver.receiver.track()?.kind()}")
            }

            override fun onDataChannel(channel: DataChannel) {
                Log.i(TAG, "DataChannel opened: ${channel.label()}")
                registerDataChannel(channel)
            }

            override fun onAddStream(stream: MediaStream) {}
            override fun onRemoveStream(stream: MediaStream) {}
            override fun onIceCandidatesRemoved(p0: Array<IceCandidate>) {}
            override fun onStandardizedIceConnectionChange(newState: PeerConnection.IceConnectionState) {}
            override fun onIceConnectionReceivingChange(p0: Boolean) {}
        })

        peerConnection?.addTrack(videoTrack, listOf(STREAM_ID))
        Log.i(TAG, "Video track added to PeerConnection")

        // Create data channel proactively as offerer so server can receive it
        createDataChannel()
        createOffer()
    }

    private fun createOffer() {
        val mediaConstraints = MediaConstraints()
        Log.i(TAG, "Creating WebRTC offer...")
        peerConnection?.createOffer(object : SdpObserver {
            override fun onCreateSuccess(desc: SessionDescription) {
                Log.i(TAG, "Offer created successfully")
                peerConnection?.setLocalDescription(this, desc)
                sendSignal(SignalMessages.offer(desc.description))
                Log.i(TAG, "Offer sent to signaling server")
            }

            override fun onCreateFailure(error: String) {
                Log.e(TAG, "Offer creation failed: $error")
            }

            override fun onSetSuccess() {
                Log.i(TAG, "Local description set successfully")
            }

            override fun onSetFailure(error: String) {
                Log.e(TAG, "SetLocalDescription failed: $error")
            }
        }, mediaConstraints)
    }

    private fun handleRemoteMessage(message: String) {
        val json = JSONObject(message)
        when (json.getString("type")) {
            "answer" -> handleAnswer(json)
            "candidate" -> handleCandidate(json)
        }
    }

    private fun handleAnswer(json: JSONObject) {
        val sdp = SessionDescription(SessionDescription.Type.ANSWER, json.getString("sdp"))
        Log.i(TAG, "Received answer from server")
        peerConnection?.setRemoteDescription(object : SdpObserver {
            override fun onSetSuccess() { Log.i(TAG, "Remote answer applied") }
            override fun onSetFailure(error: String) { Log.e(TAG, "SetRemoteDescription failed: $error") }
            override fun onCreateSuccess(p0: SessionDescription?) {}
            override fun onCreateFailure(p0: String?) {}
        }, sdp)
    }

    private fun handleCandidate(json: JSONObject) {
        Log.i(TAG, "Received ICE candidate from server")
        val candidate = IceCandidate(
            json.getString("sdpMid"),
            json.getInt("sdpMLineIndex"),
            json.getString("candidate")
        )
        peerConnection?.addIceCandidate(candidate)
    }

    // Create and register a data channel for detections
    private fun createDataChannel() {
        if (peerConnection == null) return
        if (dataChannel != null) return
        val init = DataChannel.Init()
        dataChannel = peerConnection!!.createDataChannel(Channels.DETECTIONS, init)
        Log.i(TAG, "Created DataChannel 'detections' as offerer")
        registerDataChannel(dataChannel!!)
    }

    fun createCustomDataChannel(name: String) {
        if (peerConnection == null) return
        if (name.isBlank()) return
        val init = DataChannel.Init()
        val ch = peerConnection!!.createDataChannel(name, init)
        Log.i(TAG, "Created DataChannel '$name'")
        registerDataChannel(ch)
    }

    private fun registerDataChannel(channel: DataChannel) {
        dataChannel = channel
        dataChannelByLabel[channel.label()] = channel
        channel.registerObserver(object : DataChannel.Observer {
            override fun onBufferedAmountChange(previousAmount: Long) {
                // No-op
            }

            override fun onStateChange() { }

            override fun onMessage(buffer: DataChannel.Buffer) {
                try {
                    if (!buffer.binary) {
                        val data = ByteArray(buffer.data.remaining())
                        buffer.data.get(data)
                        val message = String(data, Charsets.UTF_8)
                        Log.d(TAG, "DataChannel message: ${message.take(128)}...")
                        activity.runOnUiThread {
                            UnityBridge.send(unityCallbackGameObject, unityCallbackMethod, message)
                        }
                    } else {
                        // ignore binary
                    }
                } catch (e: Exception) {
                    // ignore
                }
            }
        })
    }

    // Allow Unity to send a message over data channel if needed
    fun sendDataChannelMessage(message: String) {
        try {
            val channel = dataChannel
            if (channel == null || channel.state() != DataChannel.State.OPEN) {
                Log.w(TAG, "DataChannel not open; cannot send")
                return
            }
            val buffer = DataChannel.Buffer(wrap(message.toByteArray(Charsets.UTF_8)), false)
            channel.send(buffer)
            Log.d(TAG, "Sent DataChannel message (${message.length} bytes)")
        } catch (e: Exception) {
            Log.e(TAG, "Failed to send DataChannel message", e)
        }
    }

    fun sendDataChannelMessageOn(name: String, message: String) {
        val dc = dataChannelByLabel[name]
        if (dc != null && dc.state() == DataChannel.State.OPEN) {
            val buffer = DataChannel.Buffer(wrap(message.toByteArray(Charsets.UTF_8)), false)
            dc.send(buffer)
            return
        }
        Log.w(TAG, "Channel '$name' not open; cannot send")
    }

    private fun startPixelDataCapture(width: Int, height: Int) {
        usePixelDataMethod = true
        val source = peerConnectionFactory.createVideoSource(false)
        pixelDataCapturer = PixelDataVideoCapturer(width, height, config.targetFps)
        pixelDataCapturer?.initializePixelCapture(activity, source.capturerObserver)
        pixelDataCapturer?.startCapture(width, height, config.targetFps)
        Log.i(TAG, "Pixel data capturer started")
        videoTrack = peerConnectionFactory.createVideoTrack(TRACK_ID, source)
    }

    private fun buildRtcConfig(): PeerConnection.RTCConfiguration =
        PeerConnection.RTCConfiguration(iceServers).apply {
            sdpSemantics = PeerConnection.SdpSemantics.UNIFIED_PLAN
        }

    private fun sendSignal(payload: JSONObject) {
        signalingClient?.send(payload.toString())
    }
    
    private companion object {
        const val TAG = "QuestVisionStreamPlugin"
        const val TRACK_ID = "ARDAMSv0"
        const val STREAM_ID = "ARDAMS"
    }
}