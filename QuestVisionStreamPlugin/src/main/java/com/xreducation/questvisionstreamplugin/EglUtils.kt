package com.xreducation.questvisionstreamplugin

import org.webrtc.EglBase

object EglUtils {
    val eglBase: EglBase by lazy {
        EglBase.create()
    }
}
