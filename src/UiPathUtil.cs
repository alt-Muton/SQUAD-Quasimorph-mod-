using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Squad_Leader
{
    internal static class UiPathUtil
    {
        public static Transform FindMercenaryBlock(Transform screenRoot, Transform selectMercButton)
        {
            if (selectMercButton != null)
            {
                var t = selectMercButton;
                while (t != null)
                {
                    if (t.name == "MercenaryBlock")
                        return t;
                    t = t.parent;
                }
            }

            if (screenRoot == null)
                return null;

            var byPath = screenRoot.Find("Background/Scroll View/Viewport/Content/MercenaryBlock");
            if (byPath != null)
                return byPath;

            return FindDeepChild(screenRoot, "MercenaryBlock");
        }

        public static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeepChild(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        public static string RelativePath(Transform root, Transform child)
        {
            if (root == null || child == null)
                return null;
            var parts = new List<string>();
            var t = child;
            while (t != null && t != root)
            {
                parts.Insert(0, t.name);
                t = t.parent;
            }
            if (t != root)
                return null;
            return string.Join("/", parts.ToArray());
        }

        public static Transform FindByRelativePath(Transform root, string relativePath)
        {
            if (root == null || string.IsNullOrEmpty(relativePath))
                return null;
            return root.Find(relativePath);
        }

        public static string DescribeHierarchy(Transform root, int maxDepth = 4)
        {
            var sb = new StringBuilder();
            Append(root, 0, maxDepth, sb);
            return sb.ToString();
        }

        private static void Append(Transform t, int depth, int maxDepth, StringBuilder sb)
        {
            if (t == null || depth > maxDepth)
                return;
            sb.Append(' ', depth * 2).AppendLine(t.name);
            for (var i = 0; i < t.childCount; i++)
                Append(t.GetChild(i), depth + 1, maxDepth, sb);
        }
    }
}
