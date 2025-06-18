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

            // 简单的撤销记录 - 记录整个对象的完整状态
            Undo.RegisterFullObjectHierarchyUndo(selected, "UI图文分离");

            try
            {
                // 获取所有子物体
                Transform[] children = GetAllChildren(selected.transform);

                // 创建ImageLayer层
                GameObject imageLayer = new GameObject("ImageLayer");
                imageLayer.transform.SetParent(selected.transform);
                imageLayer.AddComponent<RectTransform>();
                StretchToFill(imageLayer.GetComponent<RectTransform>());
                MoveAllChildrenToTarget(children, imageLayer.transform);

                // 克隆为 TextLayer
                GameObject textLayer = UnityEngine.Object.Instantiate(imageLayer);
                textLayer.transform.SetParent(selected.transform);
                textLayer.name = "TextLayer";

                // 执行分离操作
                TransferTextReferences(imageLayer, textLayer);
                RemoveTextComponents(imageLayer);
                RemoveImageComponents(textLayer);
                RemoveNonRenderAndLayoutComponents(textLayer);
                DisableInteraction(textLayer);

                // 设置层级顺序：ImageLayer在下，TextLayer在上
                imageLayer.transform.SetSiblingIndex(0);
                textLayer.transform.SetSiblingIndex(1);

                TryDeleteEmptyGameObject(imageLayer.transform);
                TryDeleteEmptyGameObject(textLayer.transform);

                Debug.Log($"图文分离完成：{selected.name} -> ImageLayer + TextLayer");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"图文分离失败：{e.Message}");
                // 如果出错，用户可以手动按 Ctrl+Z 撤销
            }
        }

        static Transform[] GetAllChildren(Transform selected)
        {
            Transform[] children = new Transform[selected.childCount];
            for (int i = 0; i < selected.childCount; i++)
            {
                children[i] = selected.GetChild(i);
            }
            return children;
        }

        static void MoveAllChildrenToTarget(Transform[] children, Transform target)
        {
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null)
                {
                    children[i].SetParent(target);
                }
            }
        }

        static void StretchToFill(RectTransform rt)
        {
            if (rt == null) return;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        static void RemoveTextComponents(GameObject go)
        {
            var texts = go.GetComponentsInChildren<Text>(true).ToList();
            var tmps = go.GetComponentsInChildren<TextMeshProUGUI>(true).ToList();

            foreach (var text in texts)
            {
                if (text != null)
                    RemoveComponentAndMaybeGameObject(text);
            }

            foreach (var tmp in tmps)
            {
                if (tmp != null)
                    RemoveComponentAndMaybeGameObject(tmp);
            }
        }

        static void RemoveImageComponents(GameObject go)
        {
            var images = go.GetComponentsInChildren<Image>(true).ToList();
            var raws = go.GetComponentsInChildren<RawImage>(true).ToList();

            foreach (var img in images)
            {
                if (img != null)
                    RemoveComponentAndMaybeGameObject(img);
            }

            foreach (var raw in raws)
            {
                if (raw != null)
                    RemoveComponentAndMaybeGameObject(raw);
            }
        }

        static void RemoveComponentAndMaybeGameObject(Component comp)
        {
            if (comp == null) return;

            // 先移除组件
            UnityEngine.Object.DestroyImmediate(comp);
        }

        static void TryDeleteEmptyGameObject(Transform root)
        {
            if (root == null) return;

            bool hasEmpty = true;

            while (hasEmpty)
            {
                hasEmpty = false;

                // 倒序遍历，避免删除过程中破坏结构
                Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = allTransforms.Length - 1; i >= 0; i--)
                {
                    var t = allTransforms[i];
                    if (t == null) continue;

                    if (IsEmpty(t))
                    {
                        Undo.DestroyObjectImmediate(t.gameObject);
                        hasEmpty = true;
                        break; // 当前结构已改变，重新获取 Transform 列表
                    }
                }
            }
        }



        static bool IsEmpty(Transform tsf)
        {
            if (tsf == null) return true;

            var components = tsf.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null) continue;

                var type = comp.GetType();
                if (type != typeof(Transform) &&
                    type != typeof(RectTransform) &&
                    type != typeof(CanvasRenderer))
                {
                    return false; // 有非基础组件
                }
            }

            // 没有子节点 & 只有基础组件 → 视为空对象
            return tsf.childCount == 0;
        }

        static void RemoveNonRenderAndLayoutComponents(GameObject go)
        {
            var allComponents = go.GetComponentsInChildren<Transform>(true)
                .SelectMany(t => t.GetComponents<Component>())
                .Where(c => c != null)
                .ToList();

            var componentsToRemove = new List<Component>();

            foreach (var comp in allComponents)
            {
                var type = comp.GetType();
                bool isWhitelisted = RenderAndLayoutWhitelist.Any(whitelisted =>
                    whitelisted.IsAssignableFrom(type));

                if (!isWhitelisted)
                {

                    componentsToRemove.Add(comp);
                }
            }

            // 批量移除组件
            foreach (var comp in componentsToRemove)
            {
                if (comp != null)
                {
                    try
                    {
                        UnityEngine.Object.DestroyImmediate(comp);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"无法移除组件 {comp.GetType().Name}: {e.Message}");
                    }
                }
            }
        }

        static void DisableInteraction(GameObject go)
        {
            if (go == null) return;

            var canvasGroup = go.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = go.AddComponent<CanvasGroup>();
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        // ========= 引用转移功能 =========

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

                TransferComponentReferences(comp, imageTexts, textTexts, imageLayer.transform);
            }
        }

        static void TransferComponentReferences(Component comp,
            Dictionary<string, Component> imageTexts,
            Dictionary<string, Component> textTexts,
            Transform imageLayerRoot)
        {
            var type = comp.GetType();

            // 处理字段
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                TransferFieldReference(comp, field, imageTexts, textTexts, imageLayerRoot);
            }

            // 处理属性
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (property.CanRead && property.CanWrite)
                {
                    TransferPropertyReference(comp, property, imageTexts, textTexts, imageLayerRoot);
                }
            }
        }

        static void TransferFieldReference(Component comp, FieldInfo field,
            Dictionary<string, Component> imageTexts,
            Dictionary<string, Component> textTexts,
            Transform imageLayerRoot)
        {
            if (!typeof(Component).IsAssignableFrom(field.FieldType))
                return;

            var value = field.GetValue(comp) as Component;
            if (value == null) return;

            string path = GetHierarchyPath(value.transform, imageLayerRoot);
            if (path == null) return;

            if (imageTexts.TryGetValue(path, out Component imageTextComp) && imageTextComp == value)
            {
                if (textTexts.TryGetValue(path, out Component textTextComp))
                {
                    field.SetValue(comp, textTextComp);
                }
                else
                {
                    field.SetValue(comp, null);
                    Debug.LogWarning($"未找到对应的TextLayer文本组件：{path}");
                }

                EditorUtility.SetDirty(comp);
            }
        }

        static void TransferPropertyReference(Component comp, PropertyInfo property,
            Dictionary<string, Component> imageTexts,
            Dictionary<string, Component> textTexts,
            Transform imageLayerRoot)
        {
            if (!typeof(Component).IsAssignableFrom(property.PropertyType))
                return;

            try
            {
                var value = property.GetValue(comp) as Component;
                if (value == null) return;

                string path = GetHierarchyPath(value.transform, imageLayerRoot);
                if (path == null) return;

                if (imageTexts.TryGetValue(path, out Component imageTextComp) && imageTextComp == value)
                {
                    if (textTexts.TryGetValue(path, out Component textTextComp))
                    {
                        property.SetValue(comp, textTextComp);
                    }
                    else
                    {
                        property.SetValue(comp, null);
                        Debug.LogWarning($"未找到对应的TextLayer文本组件：{path}");
                    }

                    EditorUtility.SetDirty(comp);
                }
            }
            catch
            {
                // 忽略无法访问的属性
            }
        }

        static Dictionary<string, Component> CollectTextComponentsByPath(GameObject root)
        {
            var dict = new Dictionary<string, Component>();

            var texts = root.GetComponentsInChildren<Text>(true);
            foreach (var t in texts)
            {
                string path = GetHierarchyPath(t.transform, root.transform);
                if (path != null && !dict.ContainsKey(path))
                    dict[path] = t;
            }

            var tmps = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                string path = GetHierarchyPath(t.transform, root.transform);
                if (path != null && !dict.ContainsKey(path))
                    dict[path] = t;
            }

            return dict;
        }

        // 获取从root开始到target的完整层级路径，比如 "Panel/Button/Text"
        static string GetHierarchyPath(Transform target, Transform root)
        {
            if (target == null || root == null) return null;

            if (target == root) return "";

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