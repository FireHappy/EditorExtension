using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;


namespace Editor.Extension
{
    public class NamespaceBatchEditor : EditorWindow
    {
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

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("选择文件夹：", GUILayout.Width(70));
            folderPath = EditorGUILayout.TextField(folderPath);
            if (GUILayout.Button("选择", GUILayout.Width(60)))
            {
                string selectPath = EditorUtility.OpenFolderPanel("选择脚本文件夹", Application.dataPath, "");
                if (!string.IsNullOrEmpty(selectPath))
                {
                    string projectPath = Directory.GetParent(Application.dataPath).FullName;

                    if (selectPath.StartsWith(Application.dataPath))
                    {
                        // Assets文件夹下的路径
                        folderPath = "Assets" + selectPath.Substring(Application.dataPath.Length);
                    }
                    else if (selectPath.StartsWith(Path.Combine(projectPath, "Packages")))
                    {
                        // Packages文件夹下的路径
                        string packagesPath = Path.Combine(projectPath, "Packages");
                        folderPath = "Packages" + selectPath.Substring(packagesPath.Length);
                    }
                    else
                    {
                        Debug.LogError("请选择Assets或Packages文件夹下的路径！");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 添加快捷按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Assets", GUILayout.Width(60)))
            {
                folderPath = "Assets";
            }
            if (GUILayout.Button("Packages", GUILayout.Width(80)))
            {
                folderPath = "Packages";
            }
            EditorGUILayout.EndHorizontal();

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
                // 检查文件是否为只读（Packages中的文件通常是只读的）
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
                        // 备份
                        string backupFile = file + ".bak";
                        if (!File.Exists(backupFile))
                        {
                            File.Copy(file, backupFile);
                        }

                        // 如果是只读文件，临时移除只读属性
                        if (isReadOnly)
                        {
                            File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                        }

                        File.WriteAllText(file, newCode);

                        // 恢复只读属性
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

            // 删除所有 .bak 备份文件
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

            // 只有Assets文件夹需要刷新AssetDatabase
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

            // 找using结束的索引(最后一个using语句所在行)
            int lastUsingIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("using ") || line == "" || line.StartsWith("//") || line.StartsWith("/*") || line.StartsWith("*"))
                {
                    // 允许using后有注释或空行
                    lastUsingIndex = i;
                }
                else
                {
                    // 遇到第一条非using且非注释的语句就停止
                    break;
                }
            }

            // 分割文件：using部分 + 其他代码部分
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

            // 判断 restLines 是否已经有 namespace
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
                // 已经有namespace，替换命名空间名称
                string oldNamespaceLine = restLines[namespaceLineIndex];
                string indent = oldNamespaceLine.Substring(0, oldNamespaceLine.IndexOf("namespace"));
                restLines[namespaceLineIndex] = indent + "namespace " + newNamespace;

                // 直接把using + 修改后的restLines合并
                var finalLines = new List<string>();
                finalLines.AddRange(usingLines);
                finalLines.AddRange(restLines);
                return string.Join("\n", finalLines);
            }
            else
            {
                // 没有namespace，给restLines包裹namespace
                var finalLines = new List<string>();
                finalLines.AddRange(usingLines);
                finalLines.Add(""); // 空行

                finalLines.Add("namespace " + newNamespace);
                finalLines.Add("{");

                // 给restLines每行前加4空格缩进
                foreach (var line in restLines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        finalLines.Add("");
                    else
                        finalLines.Add("    " + line);
                }

                finalLines.Add("}");

                return string.Join("\n", finalLines);
            }
        }
    }
}