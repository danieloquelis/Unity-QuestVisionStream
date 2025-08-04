package com.xreducation.questvisionstreamplugin

import android.content.Context
import android.util.Log
import org.webrtc.*
import java.nio.ByteBuffer
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicLong

class PixelDataVideoCapturer(
    private val width: Int,
    private val height: Int
) : VideoCapturer {

    private var observer: CapturerObserver? = null
    private val isCapturing = AtomicBoolean(false)
    private val frameCounter = AtomicLong(0)
    private var lastFrameTime = 0L
    
    override fun initialize(helper: SurfaceTextureHelper?, context: Context?, obs: CapturerObserver?) {
        observer = obs
        Log.i("PixelDataVideoCapturer", "Initialized pixel data capturer ${width}x${height}")
    }
    
    // Alternative initialize method for pixel data method
    fun initializePixelCapture(context: Context, obs: CapturerObserver) {
        observer = obs
        Log.i("PixelDataVideoCapturer", "Initialized pixel data capturer ${width}x${height}")
    }

    override fun startCapture(width: Int, height: Int, framerate: Int) {
        if (isCapturing.getAndSet(true)) {
            Log.w("PixelDataVideoCapturer", "Already capturing")
            return
        }
        Log.i("PixelDataVideoCapturer", "Started pixel data capture ${width}x${height} @${framerate}fps")
    }

    override fun stopCapture() {
        isCapturing.set(false)
        Log.i("PixelDataVideoCapturer", "Stopped pixel data capture, total frames: ${frameCounter.get()}")
    }

    override fun dispose() {
        stopCapture()
        Log.i("PixelDataVideoCapturer", "Disposed pixel data capturer")
    }

    override fun isScreencast() = false

    override fun changeCaptureFormat(width: Int, height: Int, framerate: Int) {
        Log.i("PixelDataVideoCapturer", "Capture format changed to ${width}x${height} @${framerate}fps")
    }

    fun updateFrame(pixelData: ByteArray, frameWidth: Int, frameHeight: Int) {
        if (!isCapturing.get()) {
            Log.d("PixelDataVideoCapturer", "Not capturing, ignoring frame")
            return
        }

        try {
            val currentTime = System.currentTimeMillis()
            
            // Throttle frames to avoid overwhelming WebRTC
            if (currentTime - lastFrameTime < 66) { // ~15 FPS max
                return
            }
            lastFrameTime = currentTime

            // Convert RGB24 pixel data to I420 format for WebRTC
            val i420Buffer = convertRgbToI420(pixelData, frameWidth, frameHeight)
            
            val videoFrame = VideoFrame(i420Buffer, 0, System.nanoTime())
            observer?.onFrameCaptured(videoFrame)
            
            val count = frameCounter.incrementAndGet()
            if (count % 30 == 0L) {
                Log.i("PixelDataVideoCapturer", "✅ Frame $count processed and sent to WebRTC (${frameWidth}x${frameHeight})")
            }
            
        } catch (e: Exception) {
            Log.e("PixelDataVideoCapturer", "❌ Error processing pixel data", e)
        }
    }

    private fun convertRgbToI420(rgbData: ByteArray, width: Int, height: Int): VideoFrame.I420Buffer {
        // Create I420 buffer
        val i420Buffer = JavaI420Buffer.allocate(width, height)
        
        // Get Y, U, V planes
        val yPlane = i420Buffer.dataY
        val uPlane = i420Buffer.dataU
        val vPlane = i420Buffer.dataV
        
        val yStride = i420Buffer.strideY
        val uStride = i420Buffer.strideU
        val vStride = i420Buffer.strideV
        
        // Convert RGB to YUV420
        convertRgbToYuv420(
            rgbData, width, height,
            yPlane, yStride,
            uPlane, uStride,
            vPlane, vStride
        )
        
        return i420Buffer
    }
    
    private fun convertRgbToYuv420(
        rgb: ByteArray, width: Int, height: Int,
        yPlane: ByteBuffer, yStride: Int,
        uPlane: ByteBuffer, uStride: Int,
        vPlane: ByteBuffer, vStride: Int
    ) {
        val frameSize = width * height
        
        for (y in 0 until height) {
            for (x in 0 until width) {
                val index = (y * width + x) * 3 // RGB24 = 3 bytes per pixel
                
                val r = (rgb[index].toInt() and 0xFF)
                val g = (rgb[index + 1].toInt() and 0xFF) 
                val b = (rgb[index + 2].toInt() and 0xFF)
                
                // Convert RGB to YUV using ITU-R BT.601 conversion matrix
                val yValue = ((66 * r + 129 * g + 25 * b + 128) shr 8) + 16
                val uValue = ((-38 * r - 74 * g + 112 * b + 128) shr 8) + 128
                val vValue = ((112 * r - 94 * g - 18 * b + 128) shr 8) + 128
                
                // Set Y value
                yPlane.put(y * yStride + x, yValue.toByte())
                
                // Set U and V values (subsampled 2x2)
                if (y % 2 == 0 && x % 2 == 0) {
                    val uvIndex = (y / 2) * uStride + (x / 2)
                    uPlane.put(uvIndex, uValue.toByte())
                    vPlane.put(uvIndex, vValue.toByte())
                }
            }
        }
    }
}