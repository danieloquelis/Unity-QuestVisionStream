using System.IO;
using UnityEditor;
using UnityEngine;

public class UpmScaffoldCreator : EditorWindow
{
    string packageFolderName = "com.yourcompany.yourplugin";
    string displayName = "Your Company - Your Plugin";
    string version = "1.0.0";
    string unityMinVersion = "2021.3";
    string rootPath; // where to place the package (usually project root)
    string rootNamespace = "YourCompany.YourPlugin";
    bool includeEditorAsmdef = false;
    bool includeSample = true;

    [MenuItem("Tools/Create UPM Package…")]
    static void ShowWindow()
    {
        var w = GetWindow<UpmScaffoldCreator>("Create UPM Package");
        w.minSize = new Vector2(420, 280);
        w.rootPath = Directory.GetCurrentDirectory().Replace("\\", "/");
        w.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("UPM Package Scaffold", EditorStyles.boldLabel);

        packageFolderName = EditorGUILayout.TextField("Package Name", packageFolderName);
        displayName = EditorGUILayout.TextField("Display Name", displayName);
        version = EditorGUILayout.TextField("Version", version);
        unityMinVersion = EditorGUILayout.TextField("Min Unity", unityMinVersion);
        rootNamespace = EditorGUILayout.TextField("Root Namespace", rootNamespace);

        EditorGUILayout.Space(4);
        includeEditorAsmdef = EditorGUILayout.Toggle("Include Editor asmdef", includeEditorAsmdef);
        includeSample = EditorGUILayout.Toggle("Include Samples~/DemoScene", includeSample);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Destination (repo root recommended)");
        EditorGUILayout.BeginHorizontal();
        rootPath = EditorGUILayout.TextField(rootPath);
        if (GUILayout.Button("Select…", GUILayout.Width(80)))
        {
            var picked = EditorUtility.OpenFolderPanel("Select destination", rootPath, "");
            if (!string.IsNullOrEmpty(picked)) rootPath = picked.Replace("\\", "/");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(12);
        if (GUILayout.Button("Create", GUILayout.Height(32)))
        {
            try
            {
                CreateScaffold();
                EditorUtility.DisplayDialog("UPM Package", "Package scaffold created successfully.", "OK");
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex);
                EditorUtility.DisplayDialog("Error", ex.Message, "OK");
            }
        }
    }

    void CreateScaffold()
    {
        if (string.IsNullOrWhiteSpace(packageFolderName)) throw new System.Exception("Package name is required.");
        if (!packageFolderName.Contains(".")) throw new System.Exception("Package name should look like com.yourcompany.yourplugin.");

        var pkgPath = Path.Combine(rootPath, packageFolderName).Replace("\\", "/");
        if (Directory.Exists(pkgPath)) throw new System.Exception($"Folder already exists:\n{pkgPath}");

        // Directories
        var runtime = Path.Combine(pkgPath, "Runtime");
        var android = Path.Combine(runtime, "Plugins/Android");
        var editor = Path.Combine(pkgPath, "Editor");
        var docs = Path.Combine(pkgPath, "Documentation~");
        var samples = Path.Combine(pkgPath, "Samples~/DemoScene");

        Directory.CreateDirectory(runtime);
        Directory.CreateDirectory(android);
        Directory.CreateDirectory(docs);
        if (includeEditorAsmdef) Directory.CreateDirectory(editor);
        if (includeSample) Directory.CreateDirectory(samples);

        // package.json
        var pkgJson = $@"{{
  ""name"": ""{packageFolderName}"",
  ""displayName"": ""{displayName}"",
  ""version"": ""{version}"",
  ""unity"": ""{unityMinVersion}"",
  ""description"": ""Short one-liner of what your plugin does."",
  ""author"": {{
    ""name"": ""Your Company"",
    ""email"": ""support@yourco.com"",
    ""url"": ""https://yourco.com""
  }}{(includeSample ? "," : "")}
{(includeSample ? @"  ""samples"": [
    {
      ""displayName"": ""Demo Scene"",
      ""description"": ""Basic demo showing the Android plugin working."",
      ""path"": ""Samples~/DemoScene""
    }
  ]" : "")}
}}";
        File.WriteAllText(Path.Combine(pkgPath, "package.json"), pkgJson);

        // Runtime asmdef
        var runtimeAsmdef = $@"{{
  ""name"": ""{rootNamespace}"",
  ""rootNamespace"": ""{rootNamespace}"",
  ""references"": [],
  ""includePlatforms"": [],
  ""excludePlatforms"": [],
  ""allowUnsafeCode"": false,
  ""overrideReferences"": false,
  ""precompiledReferences"": [],
  ""autoReferenced"": true,
  ""defineConstraints"": [],
  ""noEngineReferences"": false
}}";
        File.WriteAllText(Path.Combine(runtime, $"{SanitizeAsmdefName(rootNamespace)}.asmdef"), runtimeAsmdef);

        // Editor asmdef (optional)
        if (includeEditorAsmdef)
        {
            var editorAsmdef = $@"{{
  ""name"": ""{rootNamespace}.Editor"",
  ""rootNamespace"": ""{rootNamespace}.Editor"",
  ""references"": [ ""{rootNamespace}"" ],
  ""includePlatforms"": [ ""Editor"" ],
  ""excludePlatforms"": [],
  ""allowUnsafeCode"": false,
  ""overrideReferences"": false,
  ""precompiledReferences"": [],
  ""autoReferenced"": true,
  ""defineConstraints"": [],
  ""noEngineReferences"": false
}}";
            File.WriteAllText(Path.Combine(editor, $"{SanitizeAsmdefName(rootNamespace)}.Editor.asmdef"), editorAsmdef);
        }

        // Android placeholders
        File.WriteAllText(Path.Combine(android, "READ_ME_PLACEHOLDER.txt"),
            "Place your AAR here (yourlib.aar). Add AndroidManifest.xml or proguard-user.txt only if required.");

        // Docs
        File.WriteAllText(Path.Combine(docs, "README.md"),
            $"# {displayName}\n\nQuickstart, permissions, and Android notes go here.\n");
        File.WriteAllText(Path.Combine(pkgPath, "CHANGELOG.md"),
            $"# Changelog\n\n## {version} - Initial release\n");
        File.WriteAllText(Path.Combine(pkgPath, "LICENSE.md"),
            "Your license text here.");

        // Sample scene placeholder (so folder gets .meta)
        if (includeSample)
        {
            File.WriteAllText(Path.Combine(samples, "README.txt"),
                "Create/drag a demo scene here. It will show in Package Manager > Samples.");
        }
    }

    static string SanitizeAsmdefName(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c.ToString(), "");
        return s;
    }
}
