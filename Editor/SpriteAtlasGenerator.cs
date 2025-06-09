#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using System.IO;
using System.Collections.Generic;

namespace EditorExtension
{
    public class SpriteAtlasGenerator : EditorWindow
    {
        private string spriteFolderPath = "Assets/Sprites/UI";
        private string atlasSaveFolder = "Assets/Atlas";
        private string atlasName = "UI_Atlas";

        [MenuItem("Extension/Generate Sprite Atlas")]
        public static void ShowWindow()
        {
            GetWindow<SpriteAtlasGenerator>("Sprite Atlas Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Sprite Atlas Generator", EditorStyles.boldLabel);

            // Sprite Source Folder
            GUILayout.Label("1. Sprite Source Folder", EditorStyles.label);
            GUILayout.BeginHorizontal();
            spriteFolderPath = EditorGUILayout.TextField(spriteFolderPath);
            if (GUILayout.Button("Open", GUILayout.Width(50)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Sprite Folder", Application.dataPath, "");
                string relative = ToRelativeAssetPath(selected);
                if (!string.IsNullOrEmpty(relative))
                    spriteFolderPath = relative;
                else
                    Debug.LogError("❌ Please select a folder inside the Assets directory.");
            }
            GUILayout.EndHorizontal();

            // Save Path
            GUILayout.Label("2. Atlas Save Folder", EditorStyles.label);
            GUILayout.BeginHorizontal();
            atlasSaveFolder = EditorGUILayout.TextField(atlasSaveFolder);
            if (GUILayout.Button("Open", GUILayout.Width(50)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Save Folder", Application.dataPath, "");
                string relative = ToRelativeAssetPath(selected);
                if (!string.IsNullOrEmpty(relative))
                    atlasSaveFolder = relative;
                else
                    Debug.LogError("❌ Please select a folder inside the Assets directory.");
            }
            GUILayout.EndHorizontal();

            // Atlas Name
            atlasName = EditorGUILayout.TextField("3. Atlas Name", atlasName);

            GUILayout.Space(10);
            if (GUILayout.Button("✅ Generate Sprite Atlas"))
            {
                GenerateAtlas();
            }
        }

        private void GenerateAtlas()
        {
            if (!Directory.Exists(spriteFolderPath))
            {
                Debug.LogError($"❌ Sprite folder not found: {spriteFolderPath}");
                return;
            }

            if (!Directory.Exists(atlasSaveFolder))
            {
                Directory.CreateDirectory(atlasSaveFolder);
            }

            string atlasPath = Path.Combine(atlasSaveFolder, atlasName + ".spriteatlas");
            SpriteAtlas atlas = new SpriteAtlas();

            // Optional settings
            atlas.SetPackingSettings(new SpriteAtlasPackingSettings
            {
                blockOffset = 2,
                padding = 4,
                enableRotation = false,
                enableTightPacking = false
            });

            atlas.SetTextureSettings(new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = false,
                sRGB = true,
                filterMode = FilterMode.Bilinear
            });

            atlas.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                maxTextureSize = 2048,
                format = TextureImporterFormat.RGBA32,
                textureCompression = TextureImporterCompression.Uncompressed,
                name = "Default"
            });

            // 添加图片资源
            List<Object> assetsToAdd = new List<Object>();
            string[] imageFiles = Directory.GetFiles(spriteFolderPath, "*.*", SearchOption.AllDirectories);

            foreach (string file in imageFiles)
            {
                if (file.EndsWith(".png") || file.EndsWith(".jpg"))
                {
                    string assetPath = ToRelativeAssetPath(file);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        Object textureAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                        if (textureAsset != null)
                        {
                            assetsToAdd.Add(textureAsset);
                        }
                    }
                }
            }

            if (assetsToAdd.Count == 0)
            {
                Debug.LogWarning("⚠️ No images found to add to the atlas.");
                return;
            }

            atlas.Add(assetsToAdd.ToArray());

            AssetDatabase.CreateAsset(atlas, atlasPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"✅ Sprite Atlas '{atlasName}' created with {assetsToAdd.Count} textures at: {atlasPath}");
        }

        /// <summary>
        /// 将绝对路径转换为 Assets 相对路径
        /// </summary>
        private static string ToRelativeAssetPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return null;

            absolutePath = Path.GetFullPath(absolutePath).Replace("\\", "/");
            string dataPath = Path.GetFullPath(Application.dataPath).Replace("\\", "/");

            if (absolutePath.StartsWith(dataPath))
            {
                return "Assets" + absolutePath.Substring(dataPath.Length);
            }

            Debug.LogError("❌ Selected folder is not inside the Assets folder!");
            return null;
        }
    }
}
#endif
