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

    private WebCamTexture webcamTexture;
    private RenderTexture blitTexture;
    private Texture2D readbackTexture;
    private byte[] pixelData;
    private bool isStreaming = false;
    private int frameCount = 0;

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

        blitTexture = new RenderTexture(webcamTexture.width, webcamTexture.height, 0, RenderTextureFormat.ARGB32);
        blitTexture.Create();

        if (usePixelDataMethod)
        {
            // Create readback texture for CPU-based method
            readbackTexture = new Texture2D(webcamTexture.width, webcamTexture.height, TextureFormat.RGB24, false);
            pixelData = new byte[webcamTexture.width * webcamTexture.height * 3]; // RGB24 = 3 bytes per pixel
            Debug.Log($"Created readback texture: {readbackTexture.width}x{readbackTexture.height}");
        }

        QuestVisionStreamBridge.SetIceServers(iceServers);
        QuestVisionStreamBridge.ConnectToSignalingServer(signalingServerUrl);

        if (usePixelDataMethod)
        {
            // Use pixel data method
            QuestVisionStreamBridge.SetExternalTexture(IntPtr.Zero, webcamTexture.width, webcamTexture.height);
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
                // Send pixel data every few frames to avoid overwhelming the pipeline
                if (frameCount % 2 == 0) // Send every 2nd frame for ~15 FPS
                {
                    StartCoroutine(ReadPixelsAndSend());
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

    private void OnDestroy()
    {
        isStreaming = false;
        if (readbackTexture != null)
        {
            Destroy(readbackTexture);
        }
    }
}