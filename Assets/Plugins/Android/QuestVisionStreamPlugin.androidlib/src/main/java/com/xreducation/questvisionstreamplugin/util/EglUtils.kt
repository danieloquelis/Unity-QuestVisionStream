package com.xreducation.questvisionstreamplugin.util

import org.webrtc.EglBase

object EglUtils {
	val eglBase: EglBase by lazy { EglBase.create() }
}


