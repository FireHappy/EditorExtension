using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;
using System.Linq;

public class PackageGeneratorWindow : EditorWindow
{
    private string packageName = "com.company.module";
    private string displayName = "My Module";
    private string description = "A Unity package.";
    private string authorName = "Your Name";
    private string authorEmail = "your@email.com";
    private string companyName = "Your Company";
    private string version = "1.0.0";
    private string namespacePrefix = "Company.Module";

    private bool includeRuntime = true;
    private bool includeEditor = true;
    private bool includeTests = true;
    private bool generateSampleCode = true;

    [MenuItem("Extension/Package Generator")]
    public static void ShowWindow() => GetWindow<PackageGeneratorWindow>("Package Generator");

    private void OnGUI()
    {
        GUILayout.Label("Package Info", EditorStyles.boldLabel);
        packageName = EditorGUILayout.TextField("Package Name", packageName);
        displayName = EditorGUILayout.TextField("Display Name", displayName);
        description = EditorGUILayout.TextField("Description", description);
        version = EditorGUILayout.TextField("Version", version);
        namespacePrefix = EditorGUILayout.TextField("Namespace", namespacePrefix);

        GUILayout.Space(10);
        GUILayout.Label("Author Info", EditorStyles.boldLabel);
        authorName = EditorGUILayout.TextField("Author", authorName);
        authorEmail = EditorGUILayout.TextField("Email", authorEmail);
        companyName = EditorGUILayout.TextField("Company", companyName);

        GUILayout.Space(10);
        GUILayout.Label("Content Options", EditorStyles.boldLabel);
        includeRuntime = EditorGUILayout.Toggle("Include Runtime", includeRuntime);
        includeEditor = EditorGUILayout.Toggle("Include Editor", includeEditor);
        includeTests = EditorGUILayout.Toggle("Include Tests", includeTests);
        generateSampleCode = EditorGUILayout.Toggle("Generate Sample.cs", generateSampleCode);

        GUILayout.Space(20);
        if (GUILayout.Button("Generate Package"))
        {
            GeneratePackage();
        }
    }

    private void GeneratePackage()
    {
        string root = Path.Combine("Packages", packageName);
        if (Directory.Exists(root))
        {
            EditorUtility.DisplayDialog("Error", "Package already exists!", "OK");
            return;
        }

        Directory.CreateDirectory(root);
        CreateReadme(root);
        CreateGitIgnore(root);
        CreatePackageJson(root);
        InitGit(root);

        if (includeRuntime)
        {
            string runtimePath = Path.Combine(root, "Runtime");
            Directory.CreateDirectory(runtimePath);
            string asmName = $"{namespacePrefix}.Runtime";
            CreateAsmdef(runtimePath, asmName, false);
            CreateAssemblyInfo(runtimePath, asmName);
            if (generateSampleCode) CreateSample(runtimePath, asmName);
        }

        if (includeEditor)
        {
            string editorPath = Path.Combine(root, "Editor");
            Directory.CreateDirectory(editorPath);
            string asmName = $"{namespacePrefix}.Editor";
            CreateAsmdef(editorPath, asmName, true);
            CreateAssemblyInfo(editorPath, asmName);
            if (generateSampleCode) CreateSample(editorPath, asmName);
        }

        if (includeTests)
        {
            string testPath = Path.Combine(root, "Tests");
            Directory.CreateDirectory(testPath);
            string asmName = $"{namespacePrefix}.Tests";
            CreateAsmdef(testPath, asmName, false, new[] { "UnityEngine.TestRunner", "UnityEditor.TestRunner" });
            CreateAssemblyInfo(testPath, asmName);
            if (generateSampleCode) CreateSample(testPath, asmName);
        }

        AddPackageToManifest(packageName);

        // 强制资源刷新（新增）
        AssetDatabase.ImportAsset(root, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success", "Package created and refreshed!", "OK");
    }

    private void CreateAsmdef(string path, string asmName, bool isEditor, string[] references = null)
    {
        string refs = references != null && references.Length > 0
            ? $"  \"references\": [{string.Join(", ", references.Select(r => $"\"{r}\""))}],\n"
            : "";

        string include = isEditor ? "  \"includePlatforms\": [\"Editor\"],\n" : "";
        string exclude = !isEditor ? "  \"excludePlatforms\": [\"Editor\"],\n" : "";

        string content = "{\n" +
                         $"  \"name\": \"{asmName}\",\n" +
                         refs +
                         include +
                         exclude +
                         "  \"autoReferenced\": true\n" +
                         "}";
        File.WriteAllText(Path.Combine(path, $"{asmName}.asmdef"), content);
    }

    private void CreateAssemblyInfo(string folderPath, string asmName)
    {
        string content = $@"using System.Reflection;

[assembly: AssemblyTitle(""{asmName}"")]
[assembly: AssemblyDescription(""Generated by PackageGenerator."")]
[assembly: AssemblyCompany(""{companyName}"")]
[assembly: AssemblyVersion(""{version}"")]
[assembly: AssemblyFileVersion(""{version}"")]
";
        File.WriteAllText(Path.Combine(folderPath, "AssemblyInfo.cs"), content);
    }

    private void CreateReadme(string path)
    {
        string content = $"# {displayName}\n\n{description}\n";
        File.WriteAllText(Path.Combine(path, "README.md"), content);
    }

    private void CreateGitIgnore(string path)
    {
        string content = @"/Library/
/Temp/
/Obj/
/Build/
/Builds/
/Logs/
/UserSettings/
*.csproj
*.unityproj
*.sln
*.user
*.pidb
*.booproj
*.svd
*.pdb
*.mdb
*.opendb
*.VC.db
*.DS_Store
*.apk
*.aab
";
        File.WriteAllText(Path.Combine(path, ".gitignore"), content);
    }

    private void CreatePackageJson(string path)
    {
        string content = $@"{{
  ""name"": ""{packageName}"",
  ""displayName"": ""{displayName}"",
  ""version"": ""{version}"",
  ""description"": ""{description}"",
  ""unity"": ""2020.3"",
  ""author"": {{
    ""name"": ""{authorName}"",
    ""email"": ""{authorEmail}""
  }}
}}";
        File.WriteAllText(Path.Combine(path, "package.json"), content);
    }

    private void InitGit(string path)
    {
        try
        {
            ProcessStartInfo info = new ProcessStartInfo("git", "init")
            {
                WorkingDirectory = Path.GetFullPath(path),
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(info);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("Git init failed: " + e.Message);
        }
    }

    private void CreateSample(string path, string asmName)
    {
        string content = $@"using UnityEngine;

namespace Editor.Extension
{{
    public class Sample : MonoBehaviour
    {{
        private void Start()
        {{
            Debug.Log(""Hello from {asmName}"");
        }}
    }}
}}";
        File.WriteAllText(Path.Combine(path, "Sample.cs"), content);
    }

    private void AddPackageToManifest(string packageName)
    {
        string manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
        if (!File.Exists(manifestPath))
        {
            UnityEngine.Debug.LogError("manifest.json not found!");
            return;
        }

        string manifestJson = File.ReadAllText(manifestPath);

        if (manifestJson.Contains($"\"{packageName}\""))
        {
            UnityEngine.Debug.Log($"{packageName} already in manifest.json");
            return;
        }

        int depIndex = manifestJson.IndexOf("\"dependencies\":");
        if (depIndex < 0)
        {
            UnityEngine.Debug.LogError("dependencies not found in manifest.json!");
            return;
        }
        int startIndex = manifestJson.IndexOf("{", depIndex);
        int endIndex = manifestJson.IndexOf("}", startIndex);

        string before = manifestJson.Substring(0, endIndex);
        string after = manifestJson.Substring(endIndex);

        string newEntry = $",\n    \"{packageName}\": \"file:Packages/{packageName}\"";

        string newManifest = before + newEntry + after;

        File.WriteAllText(manifestPath, newManifest);

        UnityEngine.Debug.Log($"Added {packageName} to manifest.json");
    }


}
