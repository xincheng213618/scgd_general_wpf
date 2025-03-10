#pragma warning disable CS8603
using System.Windows;
using System.Windows.Media;

namespace System.Windows.Media
{
    public static partial class ViewHelper
    {
        public static T GetParentOfType<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);

            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }
    }

    public static partial class ViewHelper
    {
        public static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj is FrameworkElement framework)
            {
                framework.ApplyTemplate();
            }
            if (obj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                    if (child != null)
                    {
                        if (child is T t)
                        {
                            return t;
                        }
                        else
                        {
                            T childItem = FindVisualChild<T>(child);
                            if (childItem != null)
                                return childItem;
                        }
                    }


                }
            }
            return null;
        }

        /// <summary>
        /// 通过给定的依赖对象，在控件树中向上查找指定类型的控件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T FindVisualParent<T>(DependencyObject obj) where T : class
        {
            while (obj != null)
            {
                if (obj is T t)
                    return t;

                obj = VisualTreeHelper.GetParent(obj);
            }

            return null;
        }

        /// <summary>
        /// 在给定的空间中，查找给定坐标点的给定类型的控件
        /// </summary>
        public static T HitTestView<T>(Visual visual, Point pos) where T : class
        {
            HitTestResult result = VisualTreeHelper.HitTest(visual, pos);
            if (result == null)
                return null;
            DependencyObject obj = result.VisualHit;

            return FindVisualParent<T>(obj);
        }
    }
}
