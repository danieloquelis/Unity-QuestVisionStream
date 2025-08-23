package com.questvisionstream.util

import org.webrtc.EglBase

object EglUtils {
	val eglBase: EglBase by lazy { EglBase.create() }
}


