package com.questvisionstream.util

import org.json.JSONObject
import org.webrtc.IceCandidate

object SignalMessages {
	fun offer(sdp: String): JSONObject = JSONObject().apply {
		put("type", "offer")
		put("sdp", sdp)
	}

	fun candidate(c: IceCandidate): JSONObject = JSONObject().apply {
		put("type", "candidate")
		put("candidate", c.sdp)
		put("sdpMid", c.sdpMid)
		put("sdpMLineIndex", c.sdpMLineIndex)
	}
}


