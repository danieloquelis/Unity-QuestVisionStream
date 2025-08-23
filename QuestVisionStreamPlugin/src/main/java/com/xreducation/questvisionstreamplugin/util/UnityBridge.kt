package com.xreducation.questvisionstreamplugin.util

object UnityBridge {
	fun send(gameObject: String, method: String, message: String) {
		try {
			val unityPlayerClass = Class.forName("com.unity3d.player.UnityPlayer")
			val sendMethod = unityPlayerClass.getMethod(
				"UnitySendMessage",
				String::class.java,
				String::class.java,
				String::class.java
			)
			sendMethod.invoke(null, gameObject, method, message)
		} catch (_: Exception) { }
	}
}


