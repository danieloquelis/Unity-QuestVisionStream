package com.questvisionstream.capture

import android.content.Context
import android.graphics.SurfaceTexture
import android.opengl.GLES11Ext
import android.opengl.GLES20
import android.util.Log
import android.view.Surface
import org.webrtc.CapturerObserver
import org.webrtc.SurfaceTextureHelper
import org.webrtc.VideoCapturer
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicLong

/**
 * Scaffold for a GPU texture-based capturer. Not fully implemented; preserves previous behavior.
 */
class UnityTextureVideoCapturer(
	private val unityTexturePtr: Long,
	private val width: Int,	private val height: Int
) : VideoCapturer {

	private var observer: CapturerObserver? = null
	private var textureHelper: SurfaceTextureHelper? = null
	private var surfaceTexture: SurfaceTexture? = null
	private var surface: Surface? = null
	private val isCapturing = AtomicBoolean(false)
	private val frameCounter = AtomicLong(0)
	private var glTextureId: Int = 0

	override fun initialize(helper: SurfaceTextureHelper, context: Context, obs: CapturerObserver) {
		textureHelper = helper
		observer = obs
		Log.i(TAG, "Initializing with Unity texture ptr: $unityTexturePtr, size: ${width}x${height}")
		textureHelper?.handler?.post { initializeOpenGL() }
	}

	private fun initializeOpenGL() {
		val textures = IntArray(1)
		GLES20.glGenTextures(1, textures, 0)
		glTextureId = textures[0]
		GLES20.glBindTexture(GLES11Ext.GL_TEXTURE_EXTERNAL_OES, glTextureId)
		GLES20.glTexParameterf(GLES11Ext.GL_TEXTURE_EXTERNAL_OES, GLES20.GL_TEXTURE_MIN_FILTER, GLES20.GL_LINEAR.toFloat())
		GLES20.glTexParameterf(GLES11Ext.GL_TEXTURE_EXTERNAL_OES, GLES20.GL_TEXTURE_MAG_FILTER, GLES20.GL_LINEAR.toFloat())
		GLES20.glTexParameteri(GLES11Ext.GL_TEXTURE_EXTERNAL_OES, GLES20.GL_TEXTURE_WRAP_S, GLES20.GL_CLAMP_TO_EDGE)
		GLES20.glTexParameteri(GLES11Ext.GL_TEXTURE_EXTERNAL_OES, GLES20.GL_TEXTURE_WRAP_T, GLES20.GL_CLAMP_TO_EDGE)
		surfaceTexture = SurfaceTexture(glTextureId)
		surfaceTexture?.setDefaultBufferSize(width, height)
		surface = Surface(surfaceTexture)
		surfaceTexture?.setOnFrameAvailableListener {
			if (isCapturing.get()) captureFrame()
		}
		Log.i(TAG, "OpenGL texture initialized: $glTextureId")
	}

	override fun startCapture(width: Int, height: Int, framerate: Int) {
		if (isCapturing.getAndSet(true)) return
		Log.i(TAG, "Starting capture ${width}x${height} @${framerate}fps")
	}

	private fun captureFrame() {
		try {
			if (!isCapturing.get()) return
			textureHelper?.handler?.post {
				surfaceTexture?.updateTexImage()
				Log.w(TAG, "Texture method not fully implemented yet; use pixel data method")
				val count = frameCounter.incrementAndGet()
				if (count % 30 == 0L) Log.i(TAG, "Frame $count captured (${width}x${height})")
			}
		} catch (_: Exception) { }
	}

	override fun stopCapture() {
		isCapturing.set(false)
		Log.i(TAG, "Capture stopped, total frames: ${frameCounter.get()}")
	}

	override fun dispose() {
		stopCapture()
		textureHelper?.handler?.post {
			surface?.release(); surface = null
			surfaceTexture?.release(); surfaceTexture = null
			if (glTextureId != 0) {
				GLES20.glDeleteTextures(1, intArrayOf(glTextureId), 0)
				glTextureId = 0
			}
		}
		Log.i(TAG, "Capturer disposed")
	}

	override fun isScreencast() = false

	override fun changeCaptureFormat(width: Int, height: Int, framerate: Int) {
		surfaceTexture?.setDefaultBufferSize(width, height)
		Log.i(TAG, "Capture format changed to ${width}x${height} @${framerate}fps")
	}

	private companion object { const val TAG = "UnityTextureVideoCapturer" }
}


