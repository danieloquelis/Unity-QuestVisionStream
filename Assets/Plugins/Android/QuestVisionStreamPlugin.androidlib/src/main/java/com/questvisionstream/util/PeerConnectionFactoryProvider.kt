package com.questvisionstream.util

import android.app.Activity
import android.util.Log
import org.webrtc.DefaultVideoDecoderFactory
import org.webrtc.DefaultVideoEncoderFactory
import org.webrtc.EglBase
import org.webrtc.PeerConnectionFactory

object PeerConnectionFactoryProvider {
	fun create(activity: Activity, eglBase: EglBase): PeerConnectionFactory {
		Log.i("QuestVisionStreamPlugin", "Initializing PeerConnectionFactory...")
		PeerConnectionFactory.initialize(
			PeerConnectionFactory.InitializationOptions.builder(activity)
				.setEnableInternalTracer(true)
				.createInitializationOptions()
		)
		val encoder = DefaultVideoEncoderFactory(eglBase.eglBaseContext, true, true)
		val decoder = DefaultVideoDecoderFactory(eglBase.eglBaseContext)
		return PeerConnectionFactory.builder()
			.setVideoEncoderFactory(encoder)
			.setVideoDecoderFactory(decoder)
			.createPeerConnectionFactory()
	}
}


