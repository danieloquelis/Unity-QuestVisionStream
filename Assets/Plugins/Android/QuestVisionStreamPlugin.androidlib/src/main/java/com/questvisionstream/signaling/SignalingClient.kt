package com.questvisionstream.signaling

import okhttp3.*
import android.util.Log
import java.util.concurrent.TimeUnit

class SignalingClient(
	private val serverUrl: String,
	private val onMessage: (String) -> Unit
) {
	private val client = OkHttpClient.Builder()
		.connectTimeout(10, TimeUnit.SECONDS)
		.readTimeout(0, TimeUnit.MILLISECONDS)
		.build()

	private var webSocket: WebSocket? = null

	fun connect() {
		val request = Request.Builder().url(serverUrl).build()
		Log.i(TAG, "Attempting WebSocket connection to $serverUrl")

		webSocket = client.newWebSocket(request, object : WebSocketListener() {
			override fun onOpen(ws: WebSocket, response: Response) {
				Log.i(TAG, "WebSocket connection established")
			}

			override fun onMessage(ws: WebSocket, text: String) {
				Log.i(TAG, "Signal message received: $text")
				onMessage(text)
			}

			override fun onClosed(ws: WebSocket, code: Int, reason: String) {
				Log.w(TAG, "WebSocket closed: $code / $reason")
			}

			override fun onFailure(ws: WebSocket, t: Throwable, response: Response?) {
				Log.e(TAG, "WebSocket failure: ${t.message}")
			}
		})
	}

	fun send(message: String) {
		val sent = webSocket?.send(message) ?: false
		if (sent) Log.i(TAG, "Message sent: $message") else Log.e(TAG, "Failed to send message — WebSocket not open")
	}

	private companion object { const val TAG = "QuestVisionStreamPlugin" }
}


