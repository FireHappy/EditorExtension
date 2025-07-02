using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace EditorExtension
{
    [Serializable]
    internal class SampleItem
    {
        public string displayName;
        public string description;
        public string path;
    }

    [Serializable]
    internal class PackageJson
    {
        public SampleItem[] samples;
    }

    public class SampleExporter : EditorWindow
    {
        private DefaultAsset sourceFolder; // 拖入的文件夹
        private string sampleFolderName = "MySample01"; // 目标 Sample 子文件夹名
        private string packageName = "com.yourcompany.yourpackage"; // 目标包名
        private bool clearTargetBeforeCopy = true;

        [MenuItem("Extension/Export Sample to Package")]
        public static void ShowWindow() => GetWindow<SampleExporter>("Sample Exporter");

        private void OnGUI()
        {
            GUILayout.Label("📦 Sample Export Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField("Sample Folder", sourceFolder, typeof(DefaultAsset), false);
            sampleFolderName = EditorGUILayout.TextField("Sample Folder Name", sampleFolderName);
            packageName = EditorGUILayout.TextField("Target Package Name", packageName);
            clearTargetBeforeCopy = EditorGUILayout.Toggle("Clear Target First", clearTargetBeforeCopy);

            EditorGUILayout.Space();
            if (GUILayout.Button("🚀 Export Sample"))
                ExportSample();
        }

        private void ExportSample()
        {
            if (sourceFolder == null)
            {
                Debug.LogError("❌ Please drag a folder into the Sample Folder field.");
                return;
            }

            var sourcePath = AssetDatabase.GetAssetPath(sourceFolder);
            if (!Directory.Exists(sourcePath))
            {
                Debug.LogError("❌ Source path is not a valid folder: " + sourcePath);
                return;
            }

            // 定位包根目录
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var packagePath = Path.Combine(projectRoot, "Packages", packageName);
            if (!Directory.Exists(packagePath))
            {
                Debug.LogError($"❌ Package path not found: {packagePath}");
                return;
            }

            // 从 package.json 解析 samples 配置
            string packageJsonPath = Path.Combine(packagePath, "package.json");
            string packageSamplesPath = null;
            if (File.Exists(packageJsonPath))
            {
                try
                {
                    var jsonText = File.ReadAllText(packageJsonPath);
                    var pkgData = JsonUtility.FromJson<PackageJson>(jsonText);
                    if (pkgData?.samples != null && pkgData.samples.Length > 0)
                    {
                        // 获取所有样本路径的根目录名称
                        var roots = new HashSet<string>();
                        foreach (var item in pkgData.samples)
                        {
                            if (string.IsNullOrEmpty(item.path)) continue;
                            var parts = item.path.Replace("\\", "/").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0)
                                roots.Add(parts[0]);
                        }
                        if (roots.Count > 0)
                        {
                            // 取第一个作为 Samples 根目录
                            packageSamplesPath = Path.Combine(packagePath, roots.First());
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"⚠️ Failed to parse package.json: {e.Message}");
                }
            }

            // 如果没有解析到，则自动查找或创建以 "Samples" 开头的目录
            if (string.IsNullOrEmpty(packageSamplesPath) || !Directory.Exists(packageSamplesPath))
            {
                var samplesDirs = Directory.GetDirectories(packagePath)
                    .Where(d => Path.GetFileName(d).StartsWith("Samples", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (samplesDirs.Count > 0)
                    packageSamplesPath = samplesDirs[0];
                else
                {
                    packageSamplesPath = Path.Combine(packagePath, "Samples~");
                    Directory.CreateDirectory(packageSamplesPath);
                    Debug.Log("ℹ️ Created Samples~ directory: " + packageSamplesPath);
                }
            }

            // 目标 Sample 文件夹
            var targetSamplePath = Path.Combine(packageSamplesPath, sampleFolderName);
            if (clearTargetBeforeCopy && Directory.Exists(targetSamplePath))
            {
                Directory.Delete(targetSamplePath, true);
                Debug.Log("🧹 Cleared existing target sample folder.");
            }
            Directory.CreateDirectory(targetSamplePath);

            // 拷贝
            CopyDirectory(sourcePath, targetSamplePath);

            AssetDatabase.Refresh();
            Debug.Log($"✅ Sample exported to: {targetSamplePath}");
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            // Copy all files including .meta
            foreach (var filePath in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(filePath);
                var destFile = Path.Combine(destinationDir, fileName);
                File.Copy(filePath, destFile, true);
            }
            // Recursively copy subdirs
            foreach (var dirPath in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dirPath);
                var destSubDir = Path.Combine(destinationDir, dirName);
                Directory.CreateDirectory(destSubDir);
                CopyDirectory(dirPath, destSubDir);
            }
        }
    }
}
