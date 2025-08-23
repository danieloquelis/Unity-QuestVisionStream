using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuestVisionStream.Utils
{
    public class FrameSender
    {
        private readonly ComputeShader _rgbToYuvShader;
        private readonly int _computeKernel;
        private readonly bool _verbose;

        public FrameSender(ComputeShader shader, bool verboseLogging = false)
        {
            _rgbToYuvShader = shader;
            _verbose = verboseLogging;
            if (_rgbToYuvShader != null)
            {
                _computeKernel = _rgbToYuvShader.FindKernel("CSMain");
            }
        }

        public void SetupYuvTargets(RenderTexture y, RenderTexture u, RenderTexture v)
        {
            if (_rgbToYuvShader == null) return;
            y.enableRandomWrite = true; u.enableRandomWrite = true; v.enableRandomWrite = true;
            if (!y.IsCreated()) y.Create();
            if (!u.IsCreated()) u.Create();
            if (!v.IsCreated()) v.Create();
        }

        public void DispatchYuv(RenderTexture input, RenderTexture y, RenderTexture u, RenderTexture v)
        {
            if (!_rgbToYuvShader) return;
            _rgbToYuvShader.SetTexture(_computeKernel, "InputTexture", input);
            _rgbToYuvShader.SetTexture(_computeKernel, "OutputY", y);
            _rgbToYuvShader.SetTexture(_computeKernel, "OutputU", u);
            _rgbToYuvShader.SetTexture(_computeKernel, "OutputV", v);
            var groupsX = (input.width + 7) / 8;
            var groupsY = (input.height + 7) / 8;
            _rgbToYuvShader.Dispatch(_computeKernel, groupsX, groupsY, 1);
        }

        public IEnumerator ReadYuvAndSend(RenderTexture y, RenderTexture u, RenderTexture v, System.Action<byte[], byte[], byte[], int, int> sender, int frameCount)
        {
            var yReq = AsyncGPUReadback.Request(y);
            var uReq = AsyncGPUReadback.Request(u);
            var vReq = AsyncGPUReadback.Request(v);
            yield return new WaitUntil(() => yReq.done && uReq.done && vReq.done);
            if (yReq.hasError || uReq.hasError || vReq.hasError) yield break;
            var yData = yReq.GetData<byte>().ToArray();
            var uData = uReq.GetData<byte>().ToArray();
            var vData = vReq.GetData<byte>().ToArray();
            sender?.Invoke(yData, uData, vData, y.width, y.height);
            if (_verbose && frameCount % 60 == 0)
            {
                Debug.Log($"GPU YUV frame sent ({y.width}x{y.height})");
            }
        }
    }
}


