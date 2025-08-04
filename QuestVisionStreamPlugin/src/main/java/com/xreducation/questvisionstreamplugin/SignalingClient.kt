package com.xreducation.questvisionstreamplugin

import okhttp3.*
import android.util.Log
import java.util.concurrent.TimeUnit

class SignalingClient(
    private val serverUrl: String,
    private val onMessage: (String) -> Unit
) {
    private val client = OkHttpClient.Builder()
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(0, TimeUnit.MILLISECONDS) // no timeout
        .build()

    private var webSocket: WebSocket? = null

    fun connect() {
        val request = Request.Builder().url(serverUrl).build()
        Log.i("QuestVisionStreamPlugin", "Attempting WebSocket connection to $serverUrl")

        webSocket = client.newWebSocket(request, object : WebSocketListener() {
            override fun onOpen(ws: WebSocket, response: Response) {
                Log.i("QuestVisionStreamPlugin", "WebSocket connection established")
            }

            override fun onMessage(ws: WebSocket, text: String) {
                Log.i("QuestVisionStreamPlugin", "Signal message received: $text")
                onMessage(text)
            }

            override fun onClosed(ws: WebSocket, code: Int, reason: String) {
                Log.w("QuestVisionStreamPlugin", "WebSocket closed: $code / $reason")
            }

            override fun onFailure(ws: WebSocket, t: Throwable, response: Response?) {
                Log.e("QuestVisionStreamPlugin", "WebSocket failure: ${t.message}")
            }
        })
    }

    fun send(message: String) {
        val sent = webSocket?.send(message) ?: false
        if (sent) {
            Log.i("QuestVisionStreamPlugin", "Message sent: $message")
        } else {
            Log.e("QuestVisionStreamPlugin", "Failed to send message — WebSocket not open")
        }
    }
}
