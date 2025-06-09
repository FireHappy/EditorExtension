using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System;

namespace EditorExtension
{
    public static class UIImageTextSeparator
    {
        // ✅ 白名单：TextLayer 保留的组件类型
        static readonly Type[] RenderAndLayoutWhitelist =
        {
            typeof(Transform),
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text),
            typeof(TextMeshProUGUI),
            typeof(HorizontalLayoutGroup),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter),
            typeof(LayoutElement),
            typeof(GridLayoutGroup),
            typeof(CanvasGroup)
        };

        [MenuItem("GameObject/UI/图文分离", false, 10)]
        static void SeparateImageAndText(MenuCommand command)
        {
            GameObject selected = Selection.activeGameObject;

            if (selected == null)
            {
                Debug.LogWarning("请先选中一个 UI GameObject。");
                return;
            }

            RectTransform originalRect = selected.GetComponent<RectTransform>();
            if (originalRect == null)
            {
                Debug.LogWarning("请选择一个 UI 类型（带有 RectTransform）的对象。");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            // 创建新的父节点
            GameObject root = new GameObject(selected.name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Create MergedRoot");
            RectTransform rootRect = root.GetComponent<RectTransform>();
            CopyRectTransform(originalRect, rootRect);

            // 保持层级结构
            Transform parent = selected.transform.parent;
            int siblingIndex = selected.transform.GetSiblingIndex();
            root.transform.SetParent(parent, false);
            root.transform.SetSiblingIndex(siblingIndex);

            // 克隆为 ImageLayer
            GameObject imageLayer = UnityEngine.Object.Instantiate(selected);
            imageLayer.name = "ImageLayer";
            Undo.RegisterCreatedObjectUndo(imageLayer, "Create ImageLayer");
            RemoveTextComponents(imageLayer);

            // 克隆为 TextLayer
            GameObject textLayer = UnityEngine.Object.Instantiate(selected);
            textLayer.name = "TextLayer";
            Undo.RegisterCreatedObjectUndo(textLayer, "Create TextLayer");
            RemoveImageComponents(textLayer);
            RemoveNonRenderAndLayoutComponents(textLayer);
            DisableInteraction(textLayer);

            // 设置父子关系
            imageLayer.transform.SetParent(root.transform, false);
            textLayer.transform.SetParent(root.transform, false);
            ResetLocalTransform(imageLayer.transform);
            ResetLocalTransform(textLayer.transform);

            // 删除原始对象
            Undo.DestroyObjectImmediate(selected);

            Undo.CollapseUndoOperations(undoGroup);
        }

        static void CopyRectTransform(RectTransform from, RectTransform to)
        {
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.anchoredPosition = from.anchoredPosition;
            to.sizeDelta = from.sizeDelta;
            to.pivot = from.pivot;
            to.localScale = from.localScale;
            to.localRotation = from.localRotation;
        }

        static void ResetLocalTransform(Transform t)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        static void RemoveTextComponents(GameObject go)
        {
            var texts = go.GetComponentsInChildren<Text>(true).ToList();
            var tmps = go.GetComponentsInChildren<TextMeshProUGUI>(true).ToList();

            foreach (var text in texts)
                RemoveComponentAndMaybeGameObject(text);

            foreach (var tmp in tmps)
                RemoveComponentAndMaybeGameObject(tmp);
        }

        static void RemoveImageComponents(GameObject go)
        {
            var images = go.GetComponentsInChildren<Image>(true).ToList();
            var raws = go.GetComponentsInChildren<RawImage>(true).ToList();

            foreach (var img in images)
                RemoveComponentAndMaybeGameObject(img);

            foreach (var raw in raws)
                RemoveComponentAndMaybeGameObject(raw);
        }

        static void RemoveComponentAndMaybeGameObject(Component comp)
        {
            GameObject obj = comp.gameObject;
            Undo.DestroyObjectImmediate(comp);
            TryDeleteEmptyUpwards(obj.transform);
        }

        static void TryDeleteEmptyUpwards(Transform t)
        {
            if (t == null || t == t.root)
                return;

            if (t.childCount == 0)
            {
                Transform parent = t.parent;
                Undo.DestroyObjectImmediate(t.gameObject);
                TryDeleteEmptyUpwards(parent);
            }
        }

        // ✅ 移除非白名单组件（TextLayer）
        static void RemoveNonRenderAndLayoutComponents(GameObject go)
        {
            var allComponents = go.GetComponentsInChildren<Transform>(true)
                .SelectMany(t => t.GetComponents<Component>())
                .ToList();

            foreach (var comp in allComponents)
            {
                if (comp == null) continue;

                var type = comp.GetType();
                bool isWhitelisted = RenderAndLayoutWhitelist.Any(whitelisted => whitelisted.IsAssignableFrom(type));

                if (!isWhitelisted)
                {
                    Undo.DestroyObjectImmediate(comp);
                }
            }
        }

        // ✅ 禁用所有交互组件（ImageLayer）
        static void DisableInteraction(GameObject go)
        {
            var canvasGroup = go.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = Undo.AddComponent<CanvasGroup>(go.gameObject);
            }
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
}
