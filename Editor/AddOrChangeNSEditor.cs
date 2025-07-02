using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;

namespace EditorExtension
{
    public class NamespaceBatchEditor : EditorWindow
    {
        private DefaultAsset folderAsset; // 拖入的文件夹对象
        private string folderPath = "Assets";
        private string targetNamespace = "";

        [MenuItem("Extension/AddOrChangeNameSpace")]
        public static void ShowWindow()
        {
            GetWindow<NamespaceBatchEditor>("批量命名空间修改");
        }

        private void OnGUI()
        {
            GUILayout.Label("批量修改或创建命名空间", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            GUILayout.Label("拖入 Assets 或 Packages 中的文件夹：", EditorStyles.label);
            folderAsset = (DefaultAsset)EditorGUILayout.ObjectField("目标文件夹", folderAsset, typeof(DefaultAsset), false);
            if (folderAsset != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(folderAsset);
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    folderPath = assetPath;
                }
                else
                {
                    EditorGUILayout.HelpBox("请拖入有效的文件夹（Assets 或 Packages 内）", MessageType.Warning);
                }
            }

            EditorGUILayout.Space();

            targetNamespace = EditorGUILayout.TextField("命名空间：", targetNamespace);

            EditorGUILayout.Space();

            if (GUILayout.Button("执行修改"))
            {
                if (string.IsNullOrEmpty(targetNamespace))
                {
                    EditorUtility.DisplayDialog("错误", "命名空间不能为空", "确定");
                    return;
                }

                if (!IsValidFolderPath(folderPath))
                {
                    EditorUtility.DisplayDialog("错误", "无效的文件夹路径", "确定");
                    return;
                }

                ModifyNamespaceInFolder(folderPath, targetNamespace);
            }
        }

        private bool IsValidFolderPath(string path)
        {
            if (path.StartsWith("Assets"))
            {
                return AssetDatabase.IsValidFolder(path);
            }
            else if (path.StartsWith("Packages"))
            {
                string projectPath = Directory.GetParent(Application.dataPath).FullName;
                string fullPath = Path.Combine(projectPath, path);
                return Directory.Exists(fullPath);
            }
            return false;
        }

        private void ModifyNamespaceInFolder(string folder, string newNamespace)
        {
            string fullPath = GetFullPath(folder);

            if (!Directory.Exists(fullPath))
            {
                Debug.LogError($"文件夹不存在：{fullPath}");
                return;
            }

            string[] files = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories);
            int modifiedCount = 0;
            List<string> readOnlyFiles = new List<string>();

            foreach (string file in files)
            {
                FileAttributes attributes = File.GetAttributes(file);
                bool isReadOnly = (attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;

                if (isReadOnly && folder.StartsWith("Packages"))
                {
                    readOnlyFiles.Add(file);
                    continue;
                }

                try
                {
                    string code = File.ReadAllText(file);
                    string newCode = ModifyNamespaceInCode(code, newNamespace);

                    if (newCode != code)
                    {
                        string backupFile = file + ".bak";
                        if (!File.Exists(backupFile))
                        {
                            File.Copy(file, backupFile);
                        }

                        if (isReadOnly)
                        {
                            File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                        }

                        File.WriteAllText(file, newCode);

                        if (isReadOnly)
                        {
                            File.SetAttributes(file, attributes);
                        }

                        modifiedCount++;
                        Debug.Log($"修改文件：{file}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"处理文件失败：{file}，原因：{e.Message}");
                }
            }

            string[] bakFiles = Directory.GetFiles(fullPath, "*.cs.bak", SearchOption.AllDirectories);
            foreach (string bak in bakFiles)
            {
                try
                {
                    File.Delete(bak);
                    Debug.Log($"删除备份文件：{bak}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"删除备份文件失败：{bak}，原因：{e.Message}");
                }
            }

            if (folder.StartsWith("Assets"))
            {
                AssetDatabase.Refresh();
            }

            string message = $"处理完成，共修改 {modifiedCount} 个文件，已删除备份文件";
            if (readOnlyFiles.Count > 0)
            {
                message += $"\n跳过 {readOnlyFiles.Count} 个只读文件";
            }

            EditorUtility.DisplayDialog("完成", message, "确定");
        }

        private string GetFullPath(string relativePath)
        {
            string projectPath = Directory.GetParent(Application.dataPath).FullName;

            if (relativePath.StartsWith("Assets"))
            {
                string assetsRelativePath = relativePath.Substring("Assets".Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return Path.Combine(Application.dataPath, assetsRelativePath);
            }
            else if (relativePath.StartsWith("Packages"))
            {
                return Path.Combine(projectPath, relativePath);
            }

            return relativePath;
        }

        private string ModifyNamespaceInCode(string code, string newNamespace)
        {
            var lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            int lastUsingIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("using ") || line == "" || line.StartsWith("//") || line.StartsWith("/*") || line.StartsWith("*"))
                {
                    lastUsingIndex = i;
                }
                else
                {
                    break;
                }
            }

            var usingLines = new List<string>();
            var restLines = new List<string>();

            for (int i = 0; i <= lastUsingIndex; i++)
            {
                usingLines.Add(lines[i]);
            }
            for (int i = lastUsingIndex + 1; i < lines.Length; i++)
            {
                restLines.Add(lines[i]);
            }

            int namespaceLineIndex = -1;
            for (int i = 0; i < restLines.Count; i++)
            {
                if (restLines[i].TrimStart().StartsWith("namespace "))
                {
                    namespaceLineIndex = i;
                    break;
                }
            }

            if (namespaceLineIndex >= 0)
            {
                string oldNamespaceLine = restLines[namespaceLineIndex];
                string indent = oldNamespaceLine.Substring(0, oldNamespaceLine.IndexOf("namespace"));
                restLines[namespaceLineIndex] = indent + "namespace " + newNamespace;

                var finalLines = new List<string>();
                finalLines.AddRange(usingLines);
                finalLines.AddRange(restLines);
                return string.Join("\n", finalLines);
            }
            else
            {
                var finalLines = new List<string>();
                finalLines.AddRange(usingLines);
                finalLines.Add("");
                finalLines.Add("namespace " + newNamespace);
                finalLines.Add("{");

                foreach (var line in restLines)
                {
                    finalLines.Add(string.IsNullOrWhiteSpace(line) ? "" : "    " + line);
                }

                finalLines.Add("}");

                return string.Join("\n", finalLines);
            }
        }
    }
}
