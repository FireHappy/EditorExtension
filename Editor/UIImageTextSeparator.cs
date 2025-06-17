using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System;
using System.Reflection;
using System.Collections.Generic;

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
            typeof(CanvasGroup),
            typeof(Mask),
            typeof(RectMask2D)
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

            // 克隆为 TextLayer
            GameObject textLayer = UnityEngine.Object.Instantiate(selected);
            textLayer.name = "TextLayer";
            Undo.RegisterCreatedObjectUndo(textLayer, "Create TextLayer");

            // 转移引用：ImageLayer 内组件字段中对 Text/TextMeshProUGUI 的引用转为指向 TextLayer 中对应的组件
            TransferTextReferences(imageLayer, textLayer);

            // 清除 ImageLayer 中的文本组件
            RemoveTextComponents(imageLayer);

            // 清理 TextLayer 中的图片组件和非白名单组件
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


        // ========= 新增：引用转移功能 =========

        static void TransferTextReferences(GameObject imageLayer, GameObject textLayer)
        {
            // 收集 ImageLayer 中所有 Text/TextMeshProUGUI 组件，按完整路径索引
            var imageTexts = CollectTextComponentsByPath(imageLayer);

            // 收集 TextLayer 中所有 Text/TextMeshProUGUI 组件，按完整路径索引
            var textTexts = CollectTextComponentsByPath(textLayer);

            // 遍历 ImageLayer 所有组件字段，寻找引用ImageLayer文本组件的字段，替换成 TextLayer 中对应路径的文本组件
            var allComponents = imageLayer.GetComponentsInChildren<Component>(true);

            foreach (var comp in allComponents)
            {
                if (comp == null) continue;

                var type = comp.GetType();
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    if (!typeof(Component).IsAssignableFrom(field.FieldType))
                        continue;

                    var value = field.GetValue(comp) as Component;
                    if (value == null) continue;

                    // 判断该引用是否是ImageLayer内的文本组件
                    string path = GetHierarchyPath(value.transform, imageLayer.transform);
                    if (path == null) continue;

                    if (imageTexts.TryGetValue(path, out Component imageTextComp))
                    {
                        if (imageTextComp == value)
                        {
                            // 找 TextLayer 中同路径的文本组件替代
                            if (textTexts.TryGetValue(path, out Component textTextComp))
                            {
                                Undo.RecordObject(comp, "Transfer Text Reference");
                                field.SetValue(comp, textTextComp);
                                EditorUtility.SetDirty(comp);
                            }
                            else
                            {
                                Undo.RecordObject(comp, "Clear Text Reference");
                                field.SetValue(comp, null);
                                EditorUtility.SetDirty(comp);
                            }
                        }
                    }
                }
            }
        }

        static Dictionary<string, Component> CollectTextComponentsByPath(GameObject root)
        {
            Dictionary<string, Component> dict = new Dictionary<string, Component>();

            var texts = root.GetComponentsInChildren<Text>(true);
            foreach (var t in texts)
            {
                string path = GetHierarchyPath(t.transform, root.transform);
                if (!dict.ContainsKey(path))
                    dict[path] = t;
            }

            var tmps = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                string path = GetHierarchyPath(t.transform, root.transform);
                if (!dict.ContainsKey(path))
                    dict[path] = t;
            }

            return dict;
        }

        // 获取从root开始到target的完整层级路径，比如 "Panel/Button/Text"
        static string GetHierarchyPath(Transform target, Transform root)
        {
            if (target == null || root == null) return null;

            var pathStack = new Stack<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                pathStack.Push(current.name);
                current = current.parent;
            }
            if (current != root) return null; // target不在root子层级中

            return string.Join("/", pathStack);
        }
    }
}
