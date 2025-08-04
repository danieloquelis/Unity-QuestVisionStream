using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using PassthroughCameraSamples;
using UnityEngine.Rendering;

public class PCAVideoStreamer : MonoBehaviour
{
    [SerializeField] private string signalingServerUrl = "ws://192.168.178.36:3000";
    [SerializeField] private string[] iceServers = {
        "stun:stun.l.google.com:19302",
        "stun:stun1.l.google.com:19302"
    };
    [SerializeField] private WebCamTextureManager passthroughCameraManager;
    [SerializeField] private RawImage previewRawImage;
    [SerializeField] private bool usePixelDataMethod = true; // Toggle between methods
    [SerializeField] private bool useGPUCompute = true; // 🚀 NEW: Use GPU compute shader
    [SerializeField] private ComputeShader rgbToYuvShader;

    private WebCamTexture webcamTexture;
    private RenderTexture blitTexture;
    private Texture2D readbackTexture;
    private byte[] pixelData;
    private bool isStreaming = false;
    private int frameCount = 0;
    
    // 🚀 GPU Compute Shader variables
    private RenderTexture yTexture, uTexture, vTexture;
    private ComputeBuffer yuvDataBuffer;
    private int computeKernel;

    private IEnumerator Start()
    {
        yield return new WaitUntil(() =>
            passthroughCameraManager != null &&
            passthroughCameraManager.WebCamTexture != null &&
            passthroughCameraManager.WebCamTexture.isPlaying);

        webcamTexture = passthroughCameraManager.WebCamTexture;
        Debug.Log($"Passthrough initialized: {webcamTexture.width}x{webcamTexture.height}");

        if (previewRawImage != null)
            previewRawImage.texture = webcamTexture;

        // 🚀 PERFORMANCE: Use smaller resolution for streaming (4-16x speed boost!)
        int streamWidth = Mathf.Min(640, webcamTexture.width);   // Max 640 width
        int streamHeight = Mathf.Min(480, webcamTexture.height); // Max 480 height
        
        // Keep aspect ratio
        float aspectRatio = (float)webcamTexture.width / webcamTexture.height;
        if (streamWidth / (float)streamHeight > aspectRatio)
        {
            streamWidth = Mathf.RoundToInt(streamHeight * aspectRatio);
        }
        else
        {
            streamHeight = Mathf.RoundToInt(streamWidth / aspectRatio);
        }
        
        Debug.Log($"📹 Streaming resolution: {streamWidth}x{streamHeight} (original: {webcamTexture.width}x{webcamTexture.height})");
        
        blitTexture = new RenderTexture(streamWidth, streamHeight, 0, RenderTextureFormat.ARGB32);
        blitTexture.Create();

        if (usePixelDataMethod)
        {
            if (useGPUCompute && rgbToYuvShader != null)
            {
                // 🚀 GPU Compute Shader setup
                SetupGPUComputeShader(streamWidth, streamHeight);
            }
            else
            {
                // CPU fallback method
                readbackTexture = new Texture2D(streamWidth, streamHeight, TextureFormat.RGB24, false);
                pixelData = new byte[streamWidth * streamHeight * 3]; // RGB24 = 3 bytes per pixel
                Debug.Log($"Created readback texture: {readbackTexture.width}x{readbackTexture.height}");
            }
        }

        QuestVisionStreamBridge.SetIceServers(iceServers);
        QuestVisionStreamBridge.ConnectToSignalingServer(signalingServerUrl);

        if (usePixelDataMethod)
        {
            // Use pixel data method
            QuestVisionStreamBridge.SetExternalTexture(IntPtr.Zero, streamWidth, streamHeight);
        }
        else
        {
            // Use texture pointer method (original)
            IntPtr texPtr = blitTexture.GetNativeTexturePtr();
            Debug.Log($"Sending blit RenderTexture ptr: {texPtr}");
            QuestVisionStreamBridge.SetExternalTexture(texPtr, blitTexture.width, blitTexture.height);
        }

        QuestVisionStreamBridge.CreateOffer();
        isStreaming = true;
    }

    private void Update()
    {
        if (webcamTexture != null && webcamTexture.isPlaying && isStreaming)
        {
            Graphics.Blit(webcamTexture, blitTexture);
            
            if (usePixelDataMethod)
            {
                // 🚀 PERFORMANCE: Send frames
                if (frameCount % 4 == 0) // Send every 4th frame for ~7.5 FPS (for testing)
                {
                    if (useGPUCompute && rgbToYuvShader != null)
                    {
                        // GPU-accelerated processing
                        ProcessFrameGPU();
                    }
                    else
                    {
                        // CPU fallback
                        StartCoroutine(ReadPixelsAndSend());
                    }
                }
            }
            
            frameCount++;
            if (frameCount % 30 == 0)
            {
                Debug.Log($"Unity frame {frameCount}, method: {(usePixelDataMethod ? "pixel data" : "texture pointer")}");
            }
        }
    }

    private IEnumerator ReadPixelsAndSend()
    {
        // Use AsyncGPUReadback for better performance (Unity 2018.2+)
        var request = AsyncGPUReadback.Request(blitTexture, 0, TextureFormat.RGB24);
        yield return new WaitUntil(() => request.done);

        if (request.hasError)
        {
            Debug.LogError("GPU readback failed");
            yield break;
        }

        var data = request.GetData<byte>();
        if (data.Length > 0)
        {
            QuestVisionStreamBridge.UpdateFrameData(data.ToArray(), blitTexture.width, blitTexture.height);
            
            if (frameCount % 60 == 0)
            {
                Debug.Log($"✅ Sent frame data: {data.Length} bytes ({blitTexture.width}x{blitTexture.height})");
            }
        }
    }

    // 🚀 GPU Compute Shader setup
    private void SetupGPUComputeShader(int width, int height)
    {
        if (rgbToYuvShader == null) return;
        
        computeKernel = rgbToYuvShader.FindKernel("CSMain");
        
        // Create output textures for Y, U, V planes
        yTexture = new RenderTexture(width, height, 0, RenderTextureFormat.R8);
        uTexture = new RenderTexture(width / 2, height / 2, 0, RenderTextureFormat.R8);
        vTexture = new RenderTexture(width / 2, height / 2, 0, RenderTextureFormat.R8);
        
        yTexture.enableRandomWrite = true;
        uTexture.enableRandomWrite = true;
        vTexture.enableRandomWrite = true;
        
        yTexture.Create();
        uTexture.Create();
        vTexture.Create();
        
        Debug.Log($"🚀 GPU compute shader initialized: {width}x{height}");
    }
    
    // 🚀 GPU-accelerated frame processing
    private void ProcessFrameGPU()
    {
        if (rgbToYuvShader == null || yTexture == null) return;
        
        // Set compute shader inputs
        rgbToYuvShader.SetTexture(computeKernel, "InputTexture", blitTexture);
        rgbToYuvShader.SetTexture(computeKernel, "OutputY", yTexture);
        rgbToYuvShader.SetTexture(computeKernel, "OutputU", uTexture);
        rgbToYuvShader.SetTexture(computeKernel, "OutputV", vTexture);
        
        // Dispatch compute shader (parallel GPU processing!)
        int threadGroupsX = (blitTexture.width + 7) / 8;
        int threadGroupsY = (blitTexture.height + 7) / 8;
        rgbToYuvShader.Dispatch(computeKernel, threadGroupsX, threadGroupsY, 1);
        
        // Now we need to read YUV data and send to Android
        // This is still a GPU→CPU transfer, but much more efficient
        StartCoroutine(ReadYUVAndSend());
    }
    
    private IEnumerator ReadYUVAndSend()
    {
        // 🚀 Read Y, U, V planes separately (much more efficient!)
        var yRequest = AsyncGPUReadback.Request(yTexture);
        var uRequest = AsyncGPUReadback.Request(uTexture);
        var vRequest = AsyncGPUReadback.Request(vTexture);
        
        yield return new WaitUntil(() => yRequest.done && uRequest.done && vRequest.done);
        
        if (yRequest.hasError || uRequest.hasError || vRequest.hasError)
        {
            Debug.LogError("Failed to read YUV planes");
            yield break;
        }
        
        // 🚀 Send YUV data directly to Android (no conversion needed!)
        var yData = yRequest.GetData<byte>().ToArray();
        var uData = uRequest.GetData<byte>().ToArray();
        var vData = vRequest.GetData<byte>().ToArray();
        
        QuestVisionStreamBridge.UpdateFrameDataYUV(
            yData, uData, vData,
            yTexture.width, yTexture.height
        );
        
        if (frameCount % 60 == 0)
        {
            Debug.Log($"🚀 GPU YUV frame: Y={yData.Length}, U={uData.Length}, V={vData.Length} bytes ({yTexture.width}x{yTexture.height})");
        }
    }

    private void OnDestroy()
    {
        isStreaming = false;
        
        // Clean up CPU resources
        if (readbackTexture != null)
        {
            Destroy(readbackTexture);
        }
        
        // Clean up GPU resources
        if (yTexture != null) yTexture.Release();
        if (uTexture != null) uTexture.Release();
        if (vTexture != null) vTexture.Release();
        if (yuvDataBuffer != null) yuvDataBuffer.Release();
    }
}