using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StackPanelDragAndDrop
{
    ///++++++++++++++++++++++++++++++++++++++++++++++++++++
    /// <summary>
    /// StackPanel拡張メソッドクラス
    /// </summary>
    ///++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static class StackPanelExtension
    {
        /// <summary>スタックパネル子要素の取得</summary>
        /// <param name="stackpanel">拡張対象のオブジェクト</param>
        /// <param name="pt">位置</param>
        /// <returns></returns>
        public static UIElement GetChildElement(this StackPanel stackpanel, Point pt)
        {
            UIElement hit = null;
            VisualTreeHelper.HitTest(
                stackpanel,
                (o) =>
                {
                    if (o.GetType() == typeof(UserControl))
                    {
                        hit = o as UIElement;
                        return HitTestFilterBehavior.Stop;
                    }
                    return HitTestFilterBehavior.Continue;
                },
                (o) =>
                {
                    return HitTestResultBehavior.Stop;
                },
                new PointHitTestParameters(pt));

            // ヒットした子要素を返す
            return hit;
        }
    }
}
