using System;
using System.Collections;
using PassthroughCameraSamples;
using QuestVisionStream.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace QuestVisionStream
{
    public class PCAVideoStreamer : MonoBehaviour
    {
        [Header("WebRTC Config")]
        [SerializeField] private string signalingServerUrl = "ws://192.168.178.36:3000";
        [SerializeField] private string[] iceServers = {
            "stun:stun.l.google.com:19302",
            "stun:stun1.l.google.com:19302"
        };
        [SerializeField] private bool enableTurnServer = false;
        [Tooltip("Optional TURN server URL, e.g., turn:openrelay.metered.ca:443?transport=tcp")] 
        [SerializeField] private string turnServerUrl = "turn:openrelay.metered.ca:443?transport=tcp";
        [Tooltip("TURN username (if required)")]
        [SerializeField] private string turnUsername = "openrelayproject";
        [Tooltip("TURN credential (if required)")]
        [SerializeField] private string turnCredential = "openrelayproject";
        [SerializeField] private QuestVisionStreamEvents eventsProxy;
        
        [Header("Passthrough Camera")]
        [SerializeField] private WebCamTextureManager passthroughCameraManager;
        [SerializeField] private RawImage previewRawImage;

        [Header("Advanced Options")]
        [SerializeField] private bool useGPUCompute = true;
        [SerializeField] private bool usePixelDataMethod = true;
        [Range(1,30)][SerializeField] private int targetFps = 30;
        [Range(2,4)][SerializeField] private int sendEveryNFrame = 2;
        [SerializeField] private ComputeShader rgbToYuvShader;

        private WebCamTexture _webcamTexture;
        private RenderTexture _blitTexture;
        private Texture2D _readbackTexture;
        private byte[] _pixelData;
        private bool _isStreaming = false;
        private int _frameCount = 0;
    
        // GPU Compute Shader variables
        private RenderTexture _yTexture, _uTexture, _vTexture;
        private ComputeBuffer _yuvDataBuffer;
        private int _computeKernel;
        private FrameSender _frameSender;

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
            _webcamTexture = passthroughCameraManager.WebCamTexture;
            if (previewRawImage != null) previewRawImage.texture = _webcamTexture;
            var res = ResolutionUtils.ComputeStreamResolution(_webcamTexture.width, _webcamTexture.height);
            
            Debug.Log($"Passthrough: {_webcamTexture.width}x{_webcamTexture.height} | Stream: {res.x}x{res.y}");
            
            _blitTexture = new RenderTexture(res.x, res.y, 0, RenderTextureFormat.ARGB32);
            _blitTexture.Create();
            return res;
        }

        private void SetupCapture(int streamWidth, int streamHeight)
        {
            if (!usePixelDataMethod) return;
            if (useGPUCompute && rgbToYuvShader != null)
            {
                SetupGPUComputeShader(streamWidth, streamHeight);
                _frameSender = new FrameSender(rgbToYuvShader);
                _frameSender.SetupYuvTargets(_yTexture, _uTexture, _vTexture);
            }
            else
            {
                _readbackTexture = new Texture2D(streamWidth, streamHeight, TextureFormat.RGB24, false);
                _pixelData = new byte[streamWidth * streamHeight * 3];
            }
        }

        private void ConfigureSignalingAndTargets(int streamWidth, int streamHeight)
        {
            QuestVisionStreamBridge.SetIceServers(iceServers);
            if (enableTurnServer && !string.IsNullOrEmpty(turnServerUrl))
            {
                QuestVisionStreamBridge.AddTurnServer(turnServerUrl, turnUsername, turnCredential);
            }
            QuestVisionStreamBridge.SetTargetFps(targetFps);
            QuestVisionStreamBridge.SetDesiredResolution(streamWidth, streamHeight);
            QuestVisionStreamBridge.ConnectToSignalingServer(signalingServerUrl);

            if (eventsProxy == null) eventsProxy = FindFirstObjectByType<QuestVisionStreamEvents>();
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
                var texPtr = _blitTexture.GetNativeTexturePtr();
                QuestVisionStreamBridge.SetExternalTexture(texPtr, _blitTexture.width, _blitTexture.height);
            }

            QuestVisionStreamBridge.CreateOffer();
            _isStreaming = true;
            eventsProxy?.OnVideoStarted("");
        }

        private void Update()
        {
            if (!_webcamTexture || !_webcamTexture.isPlaying || !_isStreaming) return;
            Graphics.Blit(_webcamTexture, _blitTexture);
            
            if (usePixelDataMethod)
            {
                var divisor = Mathf.Clamp(sendEveryNFrame, 2, 4);
                if (_frameCount % divisor == 0)
                {
                    if (useGPUCompute && rgbToYuvShader)
                    {
                        ProcessFrameGPU();
                    }
                    else
                    {
                        StartCoroutine(ReadPixelsAndSend());
                    }
                }
            }
            
            _frameCount++;
            if (_frameCount % 30 == 0)
            {
                Debug.Log($"Frame {_frameCount} [{(usePixelDataMethod ? "pixel" : "texture")}] ");
            }
        }

        private IEnumerator ReadPixelsAndSend()
        {
            // AsyncGPUReadback for better performance (Unity 2018.2+)
            var request = AsyncGPUReadback.Request(_blitTexture, 0, TextureFormat.RGB24);
            yield return new WaitUntil(() => request.done);

            if (request.hasError)
            {
                Debug.LogError("GPU readback failed");
                yield break;
            }

            var data = request.GetData<byte>();
            if (data.Length <= 0) yield break;
            
            QuestVisionStreamBridge.UpdateFrameData(data.ToArray(), _blitTexture.width, _blitTexture.height);
            if (_frameCount % 60 == 0)
            {
                Debug.Log($"Sent frame: {data.Length} bytes ({_blitTexture.width}x{_blitTexture.height})");
            }
        }

        private void SetupGPUComputeShader(int width, int height)
        {
            if (rgbToYuvShader == null) return;
        
            _computeKernel = rgbToYuvShader.FindKernel("CSMain");
        
            _yTexture = new RenderTexture(width, height, 0, RenderTextureFormat.R8);
            _uTexture = new RenderTexture(width / 2, height / 2, 0, RenderTextureFormat.R8);
            _vTexture = new RenderTexture(width / 2, height / 2, 0, RenderTextureFormat.R8);
        
            _yTexture.enableRandomWrite = true;
            _uTexture.enableRandomWrite = true;
            _vTexture.enableRandomWrite = true;
        
            _yTexture.Create();
            _uTexture.Create();
            _vTexture.Create();
        }
    
        private void ProcessFrameGPU()
        {
            if (!rgbToYuvShader || !_yTexture) return;
            _frameSender.DispatchYuv(_blitTexture, _yTexture, _uTexture, _vTexture);
            StartCoroutine(_frameSender.ReadYuvAndSend(_yTexture, _uTexture, _vTexture, QuestVisionStreamBridge.UpdateFrameDataYuv, _frameCount));
        }
    
        private void OnDestroy()
        {
            _isStreaming = false;
        
            if (_readbackTexture != null)
            {
                Destroy(_readbackTexture);
            }
        
            if (_yTexture != null) _yTexture.Release();
            if (_uTexture != null) _uTexture.Release();
            if (_vTexture != null) _vTexture.Release();
            _yuvDataBuffer?.Release();
        }
    }
}