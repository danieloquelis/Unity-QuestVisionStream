using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using PassthroughCameraSamples;
using UnityEngine.Rendering;
using Unity.Collections;
using Streaming;

public class PCAVideoStreamer : MonoBehaviour
{
    [SerializeField] private string signalingServerUrl = "ws://192.168.178.36:3000";
    [SerializeField] private string[] iceServers = {
        "stun:stun.l.google.com:19302",
        "stun:stun1.l.google.com:19302"
    };

    [SerializeField] private WebCamTextureManager passthroughCameraManager;
    [SerializeField] private QuestVisionStreamEvents eventsProxy;
    [SerializeField] private RawImage previewRawImage;

    [Header("Advanced Options")]
    [SerializeField] private bool useGPUCompute = true;
    [SerializeField] private bool usePixelDataMethod = true;
    [Range(1,30)][SerializeField] private int targetFps = 30;
    [Range(2,4)][SerializeField] private int sendEveryNFrame = 2;
    [SerializeField] private ComputeShader rgbToYuvShader;

    private WebCamTexture webcamTexture;
    private RenderTexture blitTexture;
    private Texture2D readbackTexture;
    private byte[] pixelData;
    private bool isStreaming = false;
    private int frameCount = 0;
    
    // GPU Compute Shader variables
    private RenderTexture yTexture, uTexture, vTexture;
    private ComputeBuffer yuvDataBuffer;
    private int computeKernel;
    private FrameSender frameSender;

    private IEnumerator Start()
    {
        yield return new WaitUntil(IsPassthroughReady);
        var res = InitializeSourceAndPreview();
        SetupCapture(res.x, res.y);
        ConfigureSignalingAndTargets(res.x, res.y);
    }

    private bool IsPassthroughReady()
    {
        return passthroughCameraManager != null &&
               passthroughCameraManager.WebCamTexture != null &&
               passthroughCameraManager.WebCamTexture.isPlaying;
    }

    private Vector2Int InitializeSourceAndPreview()
    {
        webcamTexture = passthroughCameraManager.WebCamTexture;
        if (previewRawImage != null) previewRawImage.texture = webcamTexture;
        var res = ResolutionUtils.ComputeStreamResolution(webcamTexture.width, webcamTexture.height, 640, 480);
        Debug.Log($"Passthrough: {webcamTexture.width}x{webcamTexture.height} | Stream: {res.x}x{res.y}");
        blitTexture = new RenderTexture(res.x, res.y, 0, RenderTextureFormat.ARGB32);
        blitTexture.Create();
        return res;
    }

    private void SetupCapture(int streamWidth, int streamHeight)
    {
        if (!usePixelDataMethod) return;
        if (useGPUCompute && rgbToYuvShader != null)
        {
            SetupGPUComputeShader(streamWidth, streamHeight);
            frameSender = new FrameSender(rgbToYuvShader);
            frameSender.SetupYuvTargets(yTexture, uTexture, vTexture);
        }
        else
        {
            readbackTexture = new Texture2D(streamWidth, streamHeight, TextureFormat.RGB24, false);
            pixelData = new byte[streamWidth * streamHeight * 3];
        }
    }

    private void ConfigureSignalingAndTargets(int streamWidth, int streamHeight)
    {
        QuestVisionStreamBridge.SetIceServers(iceServers);
        QuestVisionStreamBridge.SetTargetFps(targetFps);
        QuestVisionStreamBridge.SetDesiredResolution(streamWidth, streamHeight);
        QuestVisionStreamBridge.ConnectToSignalingServer(signalingServerUrl);

        if (eventsProxy == null) eventsProxy = FindObjectOfType<QuestVisionStreamEvents>();
        if (eventsProxy != null)
        {
            QuestVisionStreamBridge.SetUnityMessageTarget(eventsProxy.gameObject.name, "OnDetections");
        }
        else
        {
            Debug.LogWarning("No events proxy or receiver in scene; detections will be dropped.");
        }

        if (usePixelDataMethod)
        {
            QuestVisionStreamBridge.SetExternalTexture(IntPtr.Zero, streamWidth, streamHeight);
        }
        else
        {
            var texPtr = blitTexture.GetNativeTexturePtr();
            QuestVisionStreamBridge.SetExternalTexture(texPtr, blitTexture.width, blitTexture.height);
        }

        QuestVisionStreamBridge.CreateOffer();
        isStreaming = true;
        eventsProxy?.OnVideoStarted("");
    }

    private void Update()
    {
        if (webcamTexture != null && webcamTexture.isPlaying && isStreaming)
        {
            Graphics.Blit(webcamTexture, blitTexture);
            
            if (usePixelDataMethod)
            {
                int divisor = Mathf.Clamp(sendEveryNFrame, 2, 4);
                if (frameCount % divisor == 0)
                {
                    if (useGPUCompute && rgbToYuvShader != null)
                    {
                        ProcessFrameGPU();
                    }
                    else
                    {
                        StartCoroutine(ReadPixelsAndSend());
                    }
                }
            }
            
            frameCount++;
            if (frameCount % 30 == 0)
            {
                Debug.Log($"Frame {frameCount} [{(usePixelDataMethod ? "pixel" : "texture")}] ");
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
                Debug.Log($"Sent frame: {data.Length} bytes ({blitTexture.width}x{blitTexture.height})");
            }
        }
    }

    private void SetupGPUComputeShader(int width, int height)
    {
        if (rgbToYuvShader == null) return;
        
        computeKernel = rgbToYuvShader.FindKernel("CSMain");
        
        yTexture = new RenderTexture(width, height, 0, RenderTextureFormat.R8);
        uTexture = new RenderTexture(width / 2, height / 2, 0, RenderTextureFormat.R8);
        vTexture = new RenderTexture(width / 2, height / 2, 0, RenderTextureFormat.R8);
        
        yTexture.enableRandomWrite = true;
        uTexture.enableRandomWrite = true;
        vTexture.enableRandomWrite = true;
        
        yTexture.Create();
        uTexture.Create();
        vTexture.Create();
    }
    
    private void ProcessFrameGPU()
    {
        if (rgbToYuvShader == null || yTexture == null) return;
            frameSender.DispatchYuv(blitTexture, yTexture, uTexture, vTexture);
            StartCoroutine(frameSender.ReadYuvAndSend(yTexture, uTexture, vTexture, (y,u,v,w,h)=>{
                QuestVisionStreamBridge.UpdateFrameDataYUV(y,u,v,w,h);
            }, frameCount));
    }
    
    private void OnDestroy()
    {
        isStreaming = false;
        
        if (readbackTexture != null)
        {
            Destroy(readbackTexture);
        }
        
        if (yTexture != null) yTexture.Release();
        if (uTexture != null) uTexture.Release();
        if (vTexture != null) vTexture.Release();
        if (yuvDataBuffer != null) yuvDataBuffer.Release();
    }
}