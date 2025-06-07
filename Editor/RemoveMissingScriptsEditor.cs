using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace EditorExtension
{
    public class RemoveMissingScriptsEditor : EditorWindow
    {
        private string folderPath = "Assets";
        private int totalPrefabs;
        private int totalRemoved;
        private int processedCount;
        private bool isProcessing;

        [MenuItem("Extension/Remove Missing Scripts in Prefabs")]
        public static void ShowWindow()
        {
            GetWindow<RemoveMissingScriptsEditor>("Remove Missing Scripts");
        }

        private void OnGUI()
        {
            GUILayout.Label("Remove Missing Scripts in Prefabs", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(isProcessing);
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.TextField("Prefab Folder", folderPath);
                if (GUILayout.Button("Browse", GUILayout.Width(80)))
                {
                    string selectedPath = EditorUtility.OpenFolderPanel("Select Prefab Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        if (selectedPath.StartsWith(Application.dataPath))
                        {
                            folderPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                        }
                        else
                        {
                            Debug.LogWarning("Selected folder must be inside Assets folder!");
                        }
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(10);
                if (GUILayout.Button("Start Cleanup"))
                {
                    if (EditorUtility.DisplayDialog("Confirm Cleanup",
                        $"This will modify all prefabs in '{folderPath}'. Make sure you have version control!",
                        "Proceed", "Cancel"))
                    {
                        StartCleanup();
                    }
                }
            }
            EditorGUI.EndDisabledGroup();

            if (isProcessing)
            {
                Rect rect = EditorGUILayout.GetControlRect();
                EditorGUI.ProgressBar(rect, (float)processedCount / totalPrefabs,
                    $"Processing: {processedCount}/{totalPrefabs} prefabs");

                if (GUILayout.Button("Cancel"))
                {
                    isProcessing = false;
                }
            }
        }

        private void StartCleanup()
        {
            totalRemoved = 0;
            processedCount = 0;
            isProcessing = true;

            string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            totalPrefabs = prefabGUIDs.Length;

            if (totalPrefabs == 0)
            {
                isProcessing = false;
                EditorUtility.DisplayDialog("No Prefabs Found",
                    $"No prefabs found in folder: {folderPath}", "OK");
                return;
            }

            // 使用延迟调用避免阻塞UI
            EditorApplication.delayCall += ProcessNextPrefab;
        }

        private void ProcessNextPrefab()
        {
            if (!isProcessing) return;

            string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            if (processedCount >= prefabGUIDs.Length)
            {
                FinalizeCleanup();
                return;
            }

            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGUIDs[processedCount]);
            processedCount++;

            try
            {
                GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
                if (instance != null)
                {
                    int removedCount = RemoveMissingScriptsRecursive(instance);
                    if (removedCount > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                        totalRemoved += removedCount;
                    }
                    PrefabUtility.UnloadPrefabContents(instance);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Failed to process prefab: {prefabPath}\nError: {e.Message}");
            }

            // 处理下一个预制体
            EditorApplication.delayCall += ProcessNextPrefab;
        }

        private int RemoveMissingScriptsRecursive(GameObject gameObject)
        {
            int count = 0;
            // 递归处理所有子对象
            foreach (Transform child in gameObject.transform)
            {
                count += RemoveMissingScriptsRecursive(child.gameObject);
            }

            // 移除当前对象上的丢失脚本
            count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
            return count;
        }

        private void FinalizeCleanup()
        {
            isProcessing = false;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Cleanup Complete",
                $"Processed {processedCount} prefabs.\n" +
                $"Total missing scripts removed: {totalRemoved}.",
                "OK"
            );

            Debug.Log($"✅ Cleanup Complete! " +
                      $"Processed {processedCount} prefabs, " +
                      $"Removed {totalRemoved} missing scripts.");
        }
    }
}