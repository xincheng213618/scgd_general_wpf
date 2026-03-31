namespace ColorVision.Solution.V
{
    public static class VObjectExtensions
    {
        /// <summary>
        /// 得到指定数据类型的祖先节点。
        /// </summary>
        public static T? GetAncestor<T>(this VObject This) where T : VObject
        {
            if (This is T t)
                return t;

            if (This.Parent == null)
                return null;

            return This.Parent.GetAncestor<T>();
        }

        public static bool HasFile(this VObject vObject)
        {
            for (int i = 0; i < vObject.VisualChildren.Count; i++)
            {
                if (vObject.VisualChildren[i] is VFile file1)
                {
                    return true;
                }
                if (vObject.VisualChildren[i] is VFolder vFolder)
                {
                    bool result = vFolder.HasFile();
                    if (result) return true;
                }
            }
            return false;
        }

        public static void SortByName(this VObject vObject)
        {
            var sorted = vObject.VisualChildren.OrderBy(v => v.Name).ToList();

            // 比较是否需要排序，避免不必要的UI更新
            bool needsSort = false;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (!ReferenceEquals(vObject.VisualChildren[i], sorted[i]))
                {
                    needsSort = true;
                    break;
                }
            }

            if (!needsSort) return;

            // 使用Move而非Clear+Add，减少CollectionChanged事件
            for (int i = 0; i < sorted.Count; i++)
            {
                int currentIndex = vObject.VisualChildren.IndexOf(sorted[i]);
                if (currentIndex != i)
                {
                    vObject.VisualChildren.Move(currentIndex, i);
                }
            }
        }

        public static bool SetSelected(this VObject vObject, string fullpath)
        {
            // 1. 当前节点匹配
            if (vObject.FullPath == fullpath)
            {
                vObject.IsSelected = true;
                // 展开所有父节点
                var parent = vObject.Parent;
                while (parent != null)
                {
                    parent.IsExpanded = true;
                    parent = parent.Parent;
                }
                // 目标已找到
                return true;
            }
            // 2. 递归查找子节点
            foreach (var child in vObject.VisualChildren)
            {
                if (SetSelected(child, fullpath))
                {
                    // 只要子节点中有一个命中，则本节点也要展开
                    vObject.IsExpanded = true;
                    return true;
                }
            }
            // 没找到
            return false;
        }

        public static IEnumerable<VObject> GetAllVisualChildren(this IEnumerable<VObject> visualChildren)
        {
            foreach (var child in visualChildren)
            {
                yield return child;

                foreach (var grandChild in GetAllVisualChildren(child.VisualChildren))
                {
                    yield return grandChild;
                }
            }
        }
    }
}
