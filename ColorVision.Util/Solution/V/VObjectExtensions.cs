using System.Collections.Generic;

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
