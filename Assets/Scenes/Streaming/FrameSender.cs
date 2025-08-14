using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Streaming
{
    public class FrameSender
    {
        private readonly ComputeShader rgbToYuvShader;
        private readonly int computeKernel;
        private readonly bool verbose;

        public FrameSender(ComputeShader shader, bool verboseLogging = false)
        {
            rgbToYuvShader = shader;
            verbose = verboseLogging;
            if (rgbToYuvShader != null)
            {
                computeKernel = rgbToYuvShader.FindKernel("CSMain");
            }
        }

        public void SetupYuvTargets(RenderTexture y, RenderTexture u, RenderTexture v)
        {
            if (rgbToYuvShader == null) return;
            y.enableRandomWrite = true; u.enableRandomWrite = true; v.enableRandomWrite = true;
            if (!y.IsCreated()) y.Create();
            if (!u.IsCreated()) u.Create();
            if (!v.IsCreated()) v.Create();
        }

        public void DispatchYuv(RenderTexture input, RenderTexture y, RenderTexture u, RenderTexture v)
        {
            if (rgbToYuvShader == null) return;
            rgbToYuvShader.SetTexture(computeKernel, "InputTexture", input);
            rgbToYuvShader.SetTexture(computeKernel, "OutputY", y);
            rgbToYuvShader.SetTexture(computeKernel, "OutputU", u);
            rgbToYuvShader.SetTexture(computeKernel, "OutputV", v);
            int groupsX = (input.width + 7) / 8;
            int groupsY = (input.height + 7) / 8;
            rgbToYuvShader.Dispatch(computeKernel, groupsX, groupsY, 1);
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
            if (verbose && frameCount % 60 == 0)
            {
                Debug.Log($"GPU YUV frame sent ({y.width}x{y.height})");
            }
        }
    }
}


