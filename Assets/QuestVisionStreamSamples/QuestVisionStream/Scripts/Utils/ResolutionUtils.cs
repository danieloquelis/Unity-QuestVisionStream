using UnityEngine;

namespace QuestVisionStream.Utils
{
    public static class ResolutionUtils
    {
        public static Vector2Int ComputeStreamResolution(int sourceWidth, int sourceHeight, int maxWidth = 640, int maxHeight = 480)
        {
            var width = Mathf.Min(maxWidth, sourceWidth);
            var height = Mathf.Min(maxHeight, sourceHeight);

            var aspect = sourceHeight > 0 ? (float)sourceWidth / sourceHeight : 1f;
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


