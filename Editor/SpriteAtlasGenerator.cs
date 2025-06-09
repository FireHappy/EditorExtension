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

        [MenuItem("Tools/Generate Sprite Atlas")]
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
            if (GUILayout.Button("📂", GUILayout.Width(30)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Sprite Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                    spriteFolderPath = ToRelativeAssetPath(selected);
            }
            GUILayout.EndHorizontal();

            // Save Path
            GUILayout.Label("2. Atlas Save Folder", EditorStyles.label);
            GUILayout.BeginHorizontal();
            atlasSaveFolder = EditorGUILayout.TextField(atlasSaveFolder);
            if (GUILayout.Button("📂", GUILayout.Width(30)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Save Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                    atlasSaveFolder = ToRelativeAssetPath(selected);
            }
            GUILayout.EndHorizontal();

            // Atlas Name
            atlasName = EditorGUILayout.TextField("3. Atlas Name", atlasName);

            // Button
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
                Debug.LogError($"Sprite folder not found: {spriteFolderPath}");
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

            // Collect all Sprites
            List<Object> spritesToAdd = new List<Object>();
            string[] files = Directory.GetFiles(spriteFolderPath, "*.*", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                if (file.EndsWith(".png") || file.EndsWith(".jpg"))
                {
                    string assetPath = ToRelativeAssetPath(file);
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (sprite != null)
                    {
                        spritesToAdd.Add(sprite);
                    }
                }
            }

            if (spritesToAdd.Count == 0)
            {
                Debug.LogWarning("No sprites found to add.");
                return;
            }

            atlas.Add(spritesToAdd.ToArray());

            AssetDatabase.CreateAsset(atlas, atlasPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"✅ Sprite Atlas '{atlasName}' created with {spritesToAdd.Count} sprites at: {atlasPath}");
        }

        /// <summary>
        /// 转换为相对路径（例如 Assets/Sprites/XXX）
        /// </summary>
        private static string ToRelativeAssetPath(string absolutePath)
        {
            string projectPath = Application.dataPath;
            if (absolutePath.StartsWith(projectPath))
            {
                return "Assets" + absolutePath.Substring(projectPath.Length);
            }
            Debug.LogError("Selected folder is not inside the Assets folder!");
            return absolutePath;
        }
    }
#endif

}

