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
        private DefaultAsset sourceFolder;
        private string sampleFolderName = "MySample01";
        private string packageName = "com.yourcompany.yourpackage";
        private bool clearTargetBeforeCopy = true;

        private const string PREF_PREFIX = "SampleExporter_";

        [MenuItem("Extension/Export Sample to Package")]
        public static void ShowWindow()
        {
            var window = GetWindow<SampleExporter>("Sample Exporter");
            window.LoadPrefs();
        }

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
            {
                SavePrefs();
                ExportSample();
            }
        }

        private void OnDestroy()
        {
            SavePrefs();
        }

        private void LoadPrefs()
        {
            sampleFolderName = EditorPrefs.GetString(PREF_PREFIX + "SampleFolderName", sampleFolderName);
            packageName = EditorPrefs.GetString(PREF_PREFIX + "PackageName", packageName);
            clearTargetBeforeCopy = EditorPrefs.GetBool(PREF_PREFIX + "ClearTarget", clearTargetBeforeCopy);

            var sourcePath = EditorPrefs.GetString(PREF_PREFIX + "SourceFolderPath", string.Empty);
            if (!string.IsNullOrEmpty(sourcePath))
            {
                sourceFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(sourcePath);
            }
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PREF_PREFIX + "SampleFolderName", sampleFolderName);
            EditorPrefs.SetString(PREF_PREFIX + "PackageName", packageName);
            EditorPrefs.SetBool(PREF_PREFIX + "ClearTarget", clearTargetBeforeCopy);

            if (sourceFolder != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(sourceFolder);
                EditorPrefs.SetString(PREF_PREFIX + "SourceFolderPath", assetPath);
            }
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

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var packagePath = Path.Combine(projectRoot, "Packages", packageName);
            if (!Directory.Exists(packagePath))
            {
                Debug.LogError($"❌ Package path not found: {packagePath}");
                return;
            }

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
                            packageSamplesPath = Path.Combine(packagePath, roots.First());
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"⚠️ Failed to parse package.json: {e.Message}");
                }
            }

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

            var targetSamplePath = Path.Combine(packageSamplesPath, sampleFolderName);
            if (clearTargetBeforeCopy && Directory.Exists(targetSamplePath))
            {
                Directory.Delete(targetSamplePath, true);
                Debug.Log("🧹 Cleared existing target sample folder.");
            }
            Directory.CreateDirectory(targetSamplePath);

            CopyDirectory(sourcePath, targetSamplePath);

            AssetDatabase.Refresh();
            Debug.Log($"✅ Sample exported to: {targetSamplePath}");
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            foreach (var filePath in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(filePath);
                var destFile = Path.Combine(destinationDir, fileName);
                File.Copy(filePath, destFile, true);
            }

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
