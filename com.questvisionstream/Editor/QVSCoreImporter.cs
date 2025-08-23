#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class QVSCoreImporter
{
    const string kPackageName    = "com.questvisionstream";
    const string kSampleRelative = "Samples~/QuestVisionStream";
    const string kDest           = "Assets/QuestVisionStream";

    static QVSCoreImporter()
    {
        if (Directory.Exists(kDest)) return;

        if (EditorUtility.DisplayDialog(
            "Quest Vision Stream",
            "Core prefabs and scripts are required. Do you want to import them now?",
            "Import", "Skip"))
        {
            var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{kPackageName}");
            if (pkgInfo == null)
            {
                Debug.LogWarning("[QVS] Package not found.");
                return;
            }

            var src = Path.Combine(pkgInfo.resolvedPath, kSampleRelative);
            if (!Directory.Exists(src))
            {
                Debug.LogWarning($"[QVS] Sample source not found: {src}");
                return;
            }

            // Ensure parent directory exists
            var parentDir = Path.GetDirectoryName(kDest);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                Directory.CreateDirectory(parentDir);

            // Copy the sample
            FileUtil.CopyFileOrDirectory(src, kDest);
            AssetDatabase.Refresh();

            Debug.Log("[QVS] Core sample imported to " + kDest);
        }
    }
}
#endif
