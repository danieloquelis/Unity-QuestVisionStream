package com.xreducation.questvisionstreamplugin.capture

import android.content.Context
import android.util.Log
import org.webrtc.CapturerObserver
import org.webrtc.JavaI420Buffer
import org.webrtc.SurfaceTextureHelper
import org.webrtc.VideoCapturer
import org.webrtc.VideoFrame
import java.nio.ByteBuffer
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicLong

/**
 * Capturer that accepts raw pixel data from Unity and forwards I420 frames to WebRTC.
 * Supports two inputs: packed RGB24 and planar I420 (Y, U, V) for optimal performance.
 */
class PixelDataVideoCapturer(
	private val width: Int,
	private val height: Int,
	private var targetFps: Int = 30
) : VideoCapturer {

	private var observer: CapturerObserver? = null
	private val isCapturing = AtomicBoolean(false)
	private val frameCounter = AtomicLong(0)
	private var lastFrameTime = 0L
	private var cachedI420Buffer: JavaI420Buffer? = null
	private var cachedWidth = 0
	private var cachedHeight = 0
	private var cachedYArray: ByteArray? = null
	private var cachedUArray: ByteArray? = null
	private var cachedVArray: ByteArray? = null
	private var cachedUvWidth = 0
	private var cachedUvHeight = 0

	override fun initialize(helper: SurfaceTextureHelper?, context: Context?, obs: CapturerObserver?) {
		observer = obs
		Log.i(TAG, "Initialized pixel data capturer ${width}x${height}")
	}

	fun initializePixelCapture(context: Context, obs: CapturerObserver) {
		observer = obs
		Log.i(TAG, "Initialized pixel data capturer ${width}x${height}")
	}

	override fun startCapture(width: Int, height: Int, framerate: Int) {
		if (isCapturing.getAndSet(true)) {
			Log.w(TAG, "Already capturing")
			return
		}
		Log.i(TAG, "Started pixel data capture ${width}x${height} @${framerate}fps")
	}

	override fun stopCapture() {
		isCapturing.set(false)

		cachedI420Buffer?.release()
		cachedI420Buffer = null

		Log.i(TAG, "Stopped pixel data capture, total frames: ${frameCounter.get()}")
	}

	override fun dispose() {
		stopCapture()
		cachedI420Buffer?.release()
		cachedI420Buffer = null
		Log.i(TAG, "Disposed pixel data capturer")
	}

	override fun isScreencast() = false

	override fun changeCaptureFormat(width: Int, height: Int, framerate: Int) {
		Log.i(TAG, "Capture format changed to ${width}x${height} @${framerate}fps")
	}

	fun updateFrame(pixelData: ByteArray, frameWidth: Int, frameHeight: Int) {
		if (!isCapturing.get()) {
			Log.d(TAG, "Not capturing, ignoring frame")
			return
		}

		try {
			val currentTime = System.currentTimeMillis()
			// Throttle frames to target FPS
			val minIntervalMs = if (targetFps <= 0) 0 else (1000 / targetFps)
			if (minIntervalMs > 0 && currentTime - lastFrameTime < minIntervalMs) return
			lastFrameTime = currentTime

			val i420Buffer = convertRgbToI420(pixelData, frameWidth, frameHeight)
			val videoFrame = VideoFrame(i420Buffer, 0, System.nanoTime())
			observer?.onFrameCaptured(videoFrame)

			val count = frameCounter.incrementAndGet()
			if (count % 30 == 0L) Log.i(TAG, "Frame $count processed and sent to WebRTC (${frameWidth}x${frameHeight})")
		} catch (e: Exception) {
			Log.e(TAG, "Error processing pixel data", e)
		}
	}

	fun updateFrameYUV(yData: ByteArray, uData: ByteArray, vData: ByteArray, frameWidth: Int, frameHeight: Int) {
		if (!isCapturing.get()) {
			Log.d(TAG, "Not capturing, ignoring YUV frame")
			return
		}

		try {
			val currentTime = System.currentTimeMillis()
			// Throttle frames to target FPS
			val minIntervalMs = if (targetFps <= 0) 0 else (1000 / targetFps)
			if (minIntervalMs > 0 && currentTime - lastFrameTime < minIntervalMs) return
			lastFrameTime = currentTime

			val i420Buffer = createI420BufferFromYUV(yData, uData, vData, frameWidth, frameHeight)
			val videoFrame = VideoFrame(i420Buffer, 0, System.nanoTime())
			observer?.onFrameCaptured(videoFrame)

			val count = frameCounter.incrementAndGet()
			if (count % 30 == 0L) Log.i(TAG, "YUV Frame $count sent to WebRTC (${frameWidth}x${frameHeight}) - Y:${yData.size}, U:${uData.size}, V:${vData.size}")
		} catch (e: Exception) {
			Log.e(TAG, "Error processing YUV data", e)
		}
	}

	private fun convertRgbToI420(rgbData: ByteArray, width: Int, height: Int): VideoFrame.I420Buffer {
		val i420Buffer = if (cachedI420Buffer != null && cachedWidth == width && cachedHeight == height) {
			cachedI420Buffer!!
		} else {
			cachedI420Buffer?.release()
			val newBuffer = JavaI420Buffer.allocate(width, height)
			cachedI420Buffer = newBuffer
			cachedWidth = width
			cachedHeight = height
			Log.i(TAG, "Allocated new I420 buffer: ${width}x${height}")
			newBuffer
		}

		val yPlane = i420Buffer.dataY
		val uPlane = i420Buffer.dataU
		val vPlane = i420Buffer.dataV
		val yStride = i420Buffer.strideY
		val uStride = i420Buffer.strideU
		val vStride = i420Buffer.strideV

		convertRgbToYuv420(rgbData, width, height, yPlane, yStride, uPlane, uStride, vPlane, vStride)
		return i420Buffer
	}

	private fun createI420BufferFromYUV(yData: ByteArray, uData: ByteArray, vData: ByteArray, width: Int, height: Int): VideoFrame.I420Buffer {
		val i420Buffer = if (cachedI420Buffer != null && cachedWidth == width && cachedHeight == height) {
			cachedI420Buffer!!
		} else {
			cachedI420Buffer?.release()
			val newBuffer = JavaI420Buffer.allocate(width, height)
			cachedI420Buffer = newBuffer
			cachedWidth = width
			cachedHeight = height
			Log.i(TAG, "Allocated new I420 buffer for YUV: ${width}x${height}")
			newBuffer
		}

		val yPlane = i420Buffer.dataY
		val uPlane = i420Buffer.dataU
		val vPlane = i420Buffer.dataV
		yPlane.rewind(); uPlane.rewind(); vPlane.rewind()
		yPlane.put(yData); uPlane.put(uData); vPlane.put(vData)
		return i420Buffer
	}

	private fun convertRgbToYuv420(
		rgb: ByteArray, width: Int, height: Int,
		yPlane: ByteBuffer, yStride: Int,
		uPlane: ByteBuffer, uStride: Int,
		vPlane: ByteBuffer, vStride: Int
	) {
		try {
			convertRgbToYuv420Native(rgb, width, height, yPlane, yStride, uPlane, uStride, vPlane, vStride)
		} catch (e: UnsatisfiedLinkError) {
			convertRgbToYuv420Optimized(rgb, width, height, yPlane, yStride, uPlane, uStride, vPlane, vStride)
		}
	}

	private external fun convertRgbToYuv420Native(
		rgb: ByteArray, width: Int, height: Int,
		yPlane: ByteBuffer, yStride: Int,
		uPlane: ByteBuffer, uStride: Int,
		vPlane: ByteBuffer, vStride: Int
	)

	private fun convertRgbToYuv420Optimized(
		rgb: ByteArray, width: Int, height: Int,
		yPlane: ByteBuffer, yStride: Int,
		uPlane: ByteBuffer, uStride: Int,
		vPlane: ByteBuffer, vStride: Int
	) {
		val ySize = width * height
		val uvWidth = width / 2
		val uvHeight = height / 2
		val uvSize = uvWidth * uvHeight

		if (cachedYArray == null || cachedYArray!!.size != ySize || cachedUvWidth != uvWidth || cachedUvHeight != uvHeight) {
			cachedYArray = ByteArray(ySize)
			cachedUArray = ByteArray(uvSize)
			cachedVArray = ByteArray(uvSize)
			cachedUvWidth = uvWidth
			cachedUvHeight = uvHeight
		}
		val yArr = cachedYArray!!
		val uArr = cachedUArray!!
		val vArr = cachedVArray!!

		var rgbIndex = 0
		var yIndex = 0
		for (y in 0 until height) {
			for (x in 0 until width) {
				val r = rgb[rgbIndex].toInt() and 0xFF
				val g = rgb[rgbIndex + 1].toInt() and 0xFF
				val b = rgb[rgbIndex + 2].toInt() and 0xFF
				rgbIndex += 3

				val yVal = (r * 66 + g * 129 + b * 25 + 4096) shr 8
				yArr[yIndex++] = (yVal + 16).toByte()
			}
		}

		for (y in 0 until height step 2) {
			val uvY = y / 2
			val uvLineOffset = uvY * uvWidth
			for (x in 0 until width step 2) {
				val uvX = x / 2
				val baseIdx = (y * width + x) * 3
				val r = rgb[baseIdx].toInt() and 0xFF
				val g = rgb[baseIdx + 1].toInt() and 0xFF
				val b = rgb[baseIdx + 2].toInt() and 0xFF
				val u = (-r * 38 - g * 74 + b * 112 + 16384) shr 8
				val v = (r * 112 - g * 94 - b * 18 + 16384) shr 8
				val idx = uvLineOffset + uvX
				uArr[idx] = (u + 128).toByte()
				vArr[idx] = (v + 128).toByte()
			}
		}

		// Respect strides by copying row-by-row
		for (row in 0 until height) {
			yPlane.position(row * yStride)
			yPlane.put(yArr, row * width, width)
		}
		for (row in 0 until uvHeight) {
			val offset = row * uvWidth
			uPlane.position(row * uStride)
			uPlane.put(uArr, offset, uvWidth)
			vPlane.position(row * vStride)
			vPlane.put(vArr, offset, uvWidth)
		}
	}

	private companion object {
		const val TAG = "PixelDataVideoCapturer"
	}

	fun setTargetFps(fps: Int) { targetFps = fps }
}


