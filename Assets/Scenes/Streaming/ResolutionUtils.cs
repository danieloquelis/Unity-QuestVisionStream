using UnityEngine;

namespace Streaming
{
    public static class ResolutionUtils
    {
        public static Vector2Int ComputeStreamResolution(int sourceWidth, int sourceHeight, int maxWidth = 640, int maxHeight = 480)
        {
            int width = Mathf.Min(maxWidth, sourceWidth);
            int height = Mathf.Min(maxHeight, sourceHeight);

            float aspect = sourceHeight > 0 ? (float)sourceWidth / sourceHeight : 1f;
            if (height > 0 && width / (float)height > aspect)
            {
                width = Mathf.RoundToInt(height * aspect);
            }
            else
            {
                height = Mathf.RoundToInt(width / aspect);
            }
            return new Vector2Int(Mathf.Max(1, width), Mathf.Max(1, height));
        }
    }
}


