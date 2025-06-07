using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


namespace EditorExtension
{
    public class UISeparationEditor : EditorWindow
    {
        private GameObject selectedRoot;

        [MenuItem("Extension/UI 图文分离工具")]
        public static void ShowWindow()
        {
            GetWindow<UISeparationEditor>("UI 图文分离");
        }

        private void OnGUI()
        {
            GUILayout.Label("图文分离工具", EditorStyles.boldLabel);
            selectedRoot = (GameObject)EditorGUILayout.ObjectField("UI 根节点", selectedRoot, typeof(GameObject), true);

            if (selectedRoot == null)
            {
                EditorGUILayout.HelpBox("请指定一个 UI 根节点（含 Image 和 Text 的节点）", MessageType.Info);
                return;
            }

            if (GUILayout.Button("执行图文分离"))
            {
                SeparateImagesAndTexts(selectedRoot);
            }
        }

        private void SeparateImagesAndTexts(GameObject root)
        {
            if (root == null) return;

            GameObject imageLayer = new GameObject("ImageLayer");
            GameObject textLayer = new GameObject("TextLayer");
            imageLayer.transform.SetParent(root.transform, false);
            textLayer.transform.SetParent(root.transform, false);

            imageLayer.AddComponent<RectTransform>().CopyFrom(root.GetComponent<RectTransform>());
            textLayer.AddComponent<RectTransform>().CopyFrom(root.GetComponent<RectTransform>());

            var components = root.GetComponentsInChildren<Graphic>(true);

            foreach (var graphic in components)
            {
                if (graphic == null || graphic.gameObject == imageLayer || graphic.gameObject == textLayer)
                    continue;

                if (graphic is Image || graphic is RawImage)
                {
                    CloneToLayer(graphic.gameObject, imageLayer.transform);
                }
                else if (graphic is Text || graphic is TextMeshProUGUI)
                {
                    CloneToLayer(graphic.gameObject, textLayer.transform);
                }
            }

            Debug.Log($"图文分离完成：共处理 {components.Length} 个图文组件。");
        }

        private void CloneToLayer(GameObject source, Transform targetParent)
        {
            GameObject copy = Instantiate(source, targetParent);
            copy.name = source.name;
            // 可选：复制原始组件的层级位置（暂略）
        }
    }

    public static class RectTransformExtensions
    {
        public static void CopyFrom(this RectTransform target, RectTransform source)
        {
            if (target == null || source == null) return;
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.pivot = source.pivot;
            target.localScale = source.localScale;
            target.localRotation = source.localRotation;
        }
    }

}