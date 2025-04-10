using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Adorners
{
    public class StackPanelAdorners
    {
        public event EventHandler Changed;

        public void Invoke()
        {
            Changed?.Invoke(this, new EventArgs());
        }
    }

    ///++++++++++++++++++++++++++++++++++++++++++++++++++++
    /// <summary>
    /// StackPanel拡張メソッドクラス
    /// </summary>
    ///++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static class StackPanelExtension
    {

        public static StackPanelAdorners AddAdorners(this Panel panel, FrameworkElement window)
        {

            StackPanelAdorners stackPanelAdorners = new StackPanelAdorners();
            FrameworkElement? _draggedItem = null;

            int? _draggedItemIndex = null;
            Point? _initialPosition = null;
            Point _mouseOffsetFromItem = new();

            DragUserControlAdorner? _dragContentAdorner = null;
            InsertionAdorner? _insertionAdorner = null;


            void OnMainPanelMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
            {
                if (args.Source is not FrameworkElement draggedItem) return;

                if (panel.CaptureMouse())
                {
                    _draggedItem = draggedItem;
                    _draggedItemIndex = panel.Children.IndexOf(draggedItem);

                    _initialPosition = window.PointToScreen(args.GetPosition(window));
                    _mouseOffsetFromItem = draggedItem.PointFromScreen(_initialPosition.Value);
                }
            }

            void OnMainPanelMouseMove(object sender, MouseEventArgs args)
            {
                if (_draggedItem == null || _initialPosition == null) return;

                _dragContentAdorner ??= new DragUserControlAdorner(panel, _draggedItem, _mouseOffsetFromItem);
                _draggedItem.Opacity = 0;
                Point ptMovePanel = args.GetPosition(panel);
                Point ptMoveScreen = window.PointToScreen(args.GetPosition(window));

                _dragContentAdorner.SetScreenPosition(ptMoveScreen);

                if (panel.GetChildElement(ptMovePanel) is not FrameworkElement dropTargetItem)
                {
                    _insertionAdorner?.Detach();
                    _insertionAdorner = null;
                }
                else if (!dropTargetItem.Equals(_insertionAdorner?.AdornedElement))
                {
                    _insertionAdorner?.Detach();
                    int indexDraggedItem = panel.Children.IndexOf(_draggedItem);
                    int indexDropTargetItem = panel.Children.IndexOf(dropTargetItem);
                    _insertionAdorner =
                        (indexDraggedItem == indexDropTargetItem) ? null :
                            (indexDraggedItem < indexDropTargetItem) ?
                                new InsertionAdorner(dropTargetItem, true) :
                                new InsertionAdorner(dropTargetItem, false);
                }
                else
                {
                }

                //Point ptUp = args.GetPosition(panel);
                //HitTestResult result = VisualTreeHelper.HitTest(panel, ptUp);
                //if (result != null)
                //{
                //    UserControl dropTargetItem1 = ViewHelper.FindVisualParent<UserControl>(result.VisualHit);

                //    if (_draggedItem != null && dropTargetItem1 != null && _draggedItemIndex != null)
                //    {
                //        int dropTargetItemIndex = panel.Children.IndexOf(dropTargetItem1);
                //        UserControl draggedItem = panel.Children[_draggedItemIndex.Value] as UserControl;
                //        panel.Children.RemoveAt(_draggedItemIndex.Value);
                //        panel.Children.Insert(dropTargetItemIndex, draggedItem);

                //        _draggedItemIndex = dropTargetItemIndex;
                //    }
                //}

            }
            void OnMainPanelMouseLeftButtonUp(object sender, MouseButtonEventArgs args)
            {
                Point ptUp = args.GetPosition(panel);
                HitTestResult result = VisualTreeHelper.HitTest(panel, ptUp);
                if (result != null)
                {
                    FrameworkElement dropTargetItem = ViewHelper.FindVisualParent<UserControl>(result.VisualHit);

                    while (dropTargetItem != null && panel.Children.IndexOf(dropTargetItem) == -1)
                    {
                        dropTargetItem = VisualTreeHelper.GetParent(dropTargetItem) as FrameworkElement;
                    }

                    if (_draggedItem != null && dropTargetItem != null && _draggedItemIndex != null)
                    {
                        _draggedItem.Opacity = 1;
                        int dropTargetItemIndex = panel.Children.IndexOf(dropTargetItem);
                        FrameworkElement draggedItem = panel.Children[_draggedItemIndex.Value] as FrameworkElement;
                        panel.Children.RemoveAt(_draggedItemIndex.Value);
                        panel.Children.Insert(dropTargetItemIndex, draggedItem);
                        stackPanelAdorners.Invoke();
                    }
                }
                panel.ReleaseMouseCapture();
            }

            void OnMainPanelLostMouseCapture(object sender, MouseEventArgs args)
            {
                _dragContentAdorner?.Detach();
                _dragContentAdorner = null;
                _insertionAdorner?.Detach();
                _insertionAdorner = null;

                // フィールドの初期化
                if (_draggedItem!=null)
                    _draggedItem.Opacity = 1;
                _draggedItem = null;
                _draggedItemIndex = null;
                _initialPosition = null;
            }


            panel.MouseLeftButtonDown += OnMainPanelMouseLeftButtonDown;
            panel.MouseMove += OnMainPanelMouseMove;
            panel.MouseLeftButtonUp += OnMainPanelMouseLeftButtonUp;
            panel.LostMouseCapture += OnMainPanelLostMouseCapture;

            return stackPanelAdorners;
        }





        /// <summary>スタックパネル子要素の取得</summary>
        /// <param name="stackpanel">拡張対象のオブジェクト</param>
        /// <param name="pt">位置</param>
        /// <returns></returns>
        public static UIElement? GetChildElement(this UIElement stackpanel, Point pt)
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

            return hit;
        }
    }
}
