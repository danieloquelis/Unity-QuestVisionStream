package com.xreducation.questvisionstreamplugin

import android.content.Context
import android.graphics.SurfaceTexture
import android.opengl.GLES20
import android.opengl.GLES11Ext
import android.util.Log
import android.view.Surface
import org.webrtc.*
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicLong

class UnityTextureVideoCapturer(
    private val unityTexturePtr: Long,
    private val width: Int,
    private val height: Int
) : VideoCapturer {

    private var observer: CapturerObserver? = null
    private var textureHelper: SurfaceTextureHelper? = null
    private var surfaceTexture: SurfaceTexture? = null
    private var surface: Surface? = null
    private val isCapturing = AtomicBoolean(false)
    private val frameCounter = AtomicLong(0)
    
    // OpenGL texture for Unity texture binding
    private var glTextureId: Int = 0
    private var frameTimestamp: Long = 0

    override fun initialize(helper: SurfaceTextureHelper, context: Context, obs: CapturerObserver) {
        textureHelper = helper
        observer = obs
        
        Log.i("UnityTextureVideoCapturer", "Initializing with Unity texture ptr: $unityTexturePtr, size: ${width}x${height}")
        
        // Execute on the texture helper thread to have proper OpenGL context
        textureHelper?.handler?.post {
            initializeOpenGL()
        }
    }
    
    private fun initializeOpenGL() {
        // Create OpenGL texture that will bind to Unity's texture
        val textures = IntArray(1)
        GLES20.glGenTextures(1, textures, 0)
        glTextureId = textures[0]
        
        // Bind as external texture (for Unity RenderTexture compatibility)
        GLES20.glBindTexture(GLES11Ext.GL_TEXTURE_EXTERNAL_OES, glTextureId)
        GLES20.glTexParameterf(GLES11Ext.GL_TEXTURE_EXTERNAL_OES, GLES20.GL_TEXTURE_MIN_FILTER, GLES20.GL_LINEAR.toFloat())
        GLES20.glTexParameterf(GLES11Ext.GL_TEXTURE_EXTERNAL_OES, GLES20.GL_TEXTURE_MAG_FILTER, GLES20.GL_LINEAR.toFloat())
        GLES20.glTexParameteri(GLES11Ext.GL_TEXTURE_EXTERNAL_OES, GLES20.GL_TEXTURE_WRAP_S, GLES20.GL_CLAMP_TO_EDGE)
        GLES20.glTexParameteri(GLES11Ext.GL_TEXTURE_EXTERNAL_OES, GLES20.GL_TEXTURE_WRAP_T, GLES20.GL_CLAMP_TO_EDGE)
        
        // Create SurfaceTexture from our OpenGL texture
        surfaceTexture = SurfaceTexture(glTextureId)
        surfaceTexture?.setDefaultBufferSize(width, height)
        surface = Surface(surfaceTexture)
        
        // Set up frame available listener
        surfaceTexture?.setOnFrameAvailableListener { texture ->
            if (isCapturing.get()) {
                captureFrame()
            }
        }
        
        Log.i("UnityTextureVideoCapturer", "OpenGL texture initialized: $glTextureId")
    }

    override fun startCapture(width: Int, height: Int, framerate: Int) {
        if (isCapturing.getAndSet(true)) {
            Log.w("UnityTextureVideoCapturer", "Already capturing")
            return
        }
        
        Log.i("UnityTextureVideoCapturer", "Starting capture ${width}x${height} @${framerate}fps")
        
        // Start unity rendering callback
        startUnityRenderingCallback()
        
        // Start periodic frame capture as fallback
        startPeriodicCapture(framerate)
    }
    
    private fun startUnityRenderingCallback() {
        // For now, use periodic capture - we can add native callbacks later
        Log.i("UnityTextureVideoCapturer", "Using periodic capture method (native callbacks not implemented yet)")
    }
    
    private fun startPeriodicCapture(framerate: Int) {
        val frameInterval = 1000L / framerate
        Log.i("UnityTextureVideoCapturer", "Starting periodic capture every ${frameInterval}ms")
        
        textureHelper?.handler?.post {
            fun captureLoop() {
                if (isCapturing.get()) {
                    captureFrame()
                    textureHelper?.handler?.postDelayed({ captureLoop() }, frameInterval)
                }
            }
            captureLoop()
        }
    }
    
    private fun captureFrame() {
        try {
            if (!isCapturing.get()) return
            
            textureHelper?.handler?.post {
                // Update texture from Unity
                surfaceTexture?.updateTexImage()
                
                // Get transformation matrix
                val transformMatrix = FloatArray(16)
                surfaceTexture?.getTransformMatrix(transformMatrix)
                
                // Create VideoFrame from texture
                frameTimestamp = System.nanoTime()
                
                // TODO: Implement proper texture buffer creation
                // This requires complex integration with Unity's native texture system
                Log.w("UnityTextureVideoCapturer", "Texture method not fully implemented yet")
                Log.w("UnityTextureVideoCapturer", "Please use pixel data method (set usePixelDataMethod = true in Unity)")
                return@post
                
                // NOTE: The correct WebRTC API would be something like:
                // val buffer = TextureBuffer.wrap(Type.OES, glTextureId, width, height, transformMatrix, ...)
                // But this requires proper Unity native plugin integration
                
                // val videoFrame = VideoFrame(buffer, 0, frameTimestamp)
                // observer?.onFrameCaptured(videoFrame)
                
                val count = frameCounter.incrementAndGet()
                if (count % 30 == 0L) {
                    Log.i("UnityTextureVideoCapturer", "Frame $count captured and sent to WebRTC (${width}x${height})")
                }
            }
        } catch (e: Exception) {
            Log.e("UnityTextureVideoCapturer", "Error capturing frame", e)
        }
    }

    override fun stopCapture() {
        isCapturing.set(false)
        // unregisterUnityRenderingCallback() // Not implemented yet
        Log.i("UnityTextureVideoCapturer", "Capture stopped, total frames: ${frameCounter.get()}")
    }

    override fun dispose() {
        stopCapture()
        
        textureHelper?.handler?.post {
            surface?.release()
            surface = null
            surfaceTexture?.release()
            surfaceTexture = null
            
            if (glTextureId != 0) {
                GLES20.glDeleteTextures(1, intArrayOf(glTextureId), 0)
                glTextureId = 0
            }
        }
        
        Log.i("UnityTextureVideoCapturer", "Capturer disposed")
    }

    override fun isScreencast() = false

    override fun changeCaptureFormat(width: Int, height: Int, framerate: Int) {
        surfaceTexture?.setDefaultBufferSize(width, height)
        Log.i("UnityTextureVideoCapturer", "Capture format changed to ${width}x${height} @${framerate}fps")
    }
    
    // TODO: Native methods for Unity integration (for future optimization)
    // private external fun registerUnityRenderingCallback(texturePtr: Long)
    // private external fun unregisterUnityRenderingCallback()
    
    companion object {
        init {
            // Native library loading will be added later for optimization
            Log.i("UnityTextureVideoCapturer", "Using Kotlin-only implementation")
        }
    }
}
