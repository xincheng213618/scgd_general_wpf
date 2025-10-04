#pragma warning disable CS8625,CS8602,CS8607,CS0103,CS0067
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Draw.Rasterized;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Draw
{
    public class SelectEditorVisualVContextMenu : IDVContextMenu
    {
        public Type ContextType => typeof(SelectEditorVisual);

        public IEnumerable<MenuItem> GetContextMenuItems(ImageViewModel imageViewModel, object obj)
        {
            List<MenuItem> MenuItems = new List<MenuItem>();
            if (obj is SelectEditorVisual selectEditorVisual)
            {
                MenuItem menuIte2 = new() { Header = "栅格化" };
                menuIte2.Click += (s, e) =>
                {
                    selectEditorVisual.RasterizeSelectionAndReplace();
                };
                MenuItems.Add(menuIte2);
            }
            return MenuItems;
        }
    }


    public class SelectEditorVisual : DrawingVisual,IDisposable
    {
        public DrawCanvas DrawCanvas { get; set; }

        public Zoombox ZoomboxSub { get; set; }
        public ImageViewModel ImageViewModel { get; set; }

        private DrawingVisual SelectRect = new DrawingVisual();

        Guid Guid { get; set; }
        public SelectEditorVisual(ImageViewModel imageViewModel, DrawCanvas drawCanvas, Zoombox zoomboxSub)
        {
            ImageViewModel = imageViewModel;
            DrawCanvas = drawCanvas;
            ZoomboxSub = zoomboxSub;
            DrawCanvas.AddVisual(this);
            DrawCanvas.PreviewMouseLeftButtonDown += DrawCanvas_PreviewMouseLeftButtonDown;
            DrawCanvas.MouseMove += DrawCanvas_MouseMove;
            DrawCanvas.PreviewMouseUp += DrawCanvas_PreviewMouseUp;
        }



        private void ZoomboxSub_LayoutUpdated(object? sender, System.EventArgs e)
        {
            DebounceTimer.AddOrResetTimerDispatcher("SelectEditorVisualRender" + Guid.ToString(), 20, () => Render());
        }


        public List<ISelectVisual> SelectVisuals { get; set; } = new List<ISelectVisual>();

        public bool Contains(Point point)=> SelectVisuals.Any(v => v.GetRect().Contains(point));
        public ISelectVisual? GetVisual(Point point) => SelectVisuals.FirstOrDefault(v => v.GetRect().Contains(point));

        public ISelectVisual ISelectVisual { get; set; }

        public Rect OldRect { get; set; }

        public Point FixedPoint { get; set; }
        public Point FixedPoint1 { get; set; }

        public bool GetContainingRect(Point point)
        {
            if (SelectVisuals.Count == 0) return false;

            double thickness = 1 / ZoomboxSub.ContentMatrix.M11;
            double smallRectSize = 10 * thickness;
            double halfSmallRectSize = smallRectSize / 2;


            bool Check(ISelctRect selectVisual)
            {
                ISelectVisual = selectVisual.ISelectVisual;

                Rect Rect = selectVisual.rect;

                OldRect = new Rect(Rect.X, Rect.Y, Rect.Width, Rect.Height);

                // 计算每个角落的小矩形，使其中心在角落
                Rect topLeft = new Rect(Rect.Left - halfSmallRectSize, Rect.Top - halfSmallRectSize, smallRectSize, smallRectSize);
                Rect topRight = new Rect(Rect.Right - halfSmallRectSize, Rect.Top - halfSmallRectSize, smallRectSize, smallRectSize);
                Rect bottomLeft = new Rect(Rect.Left - halfSmallRectSize, Rect.Bottom - halfSmallRectSize, smallRectSize, smallRectSize);
                Rect bottomRight = new Rect(Rect.Right - halfSmallRectSize, Rect.Bottom - halfSmallRectSize, smallRectSize, smallRectSize);

                // 计算每条边中间的小矩形，使其中心在边的中点
                Rect middleTop = new Rect(Rect.Left + (Rect.Width / 2) - halfSmallRectSize, Rect.Top - halfSmallRectSize, smallRectSize, smallRectSize);
                Rect middleBottom = new Rect(Rect.Left + (Rect.Width / 2) - halfSmallRectSize, Rect.Bottom - halfSmallRectSize, smallRectSize, smallRectSize);
                Rect middleLeft = new Rect(Rect.Left - halfSmallRectSize, Rect.Top + (Rect.Height / 2) - halfSmallRectSize, smallRectSize, smallRectSize);
                Rect middleRight = new Rect(Rect.Right - halfSmallRectSize, Rect.Top + (Rect.Height / 2) - halfSmallRectSize, smallRectSize, smallRectSize);

                // 检查点在哪个小矩形内
                if (selectVisual.topLeft.Contains(point))
                {
                    ZoomboxSub.Cursor = Cursors.SizeNWSE;
                    FixedPoint = OldRect.BottomRight;
                    return true;
                }
                else if (selectVisual.topRight.Contains(point))
                {
                    FixedPoint = OldRect.BottomLeft;
                    ZoomboxSub.Cursor = Cursors.SizeNESW;
                    return true;
                }
                else if (selectVisual.bottomLeft.Contains(point))
                {
                    FixedPoint = OldRect.TopRight;
                    ZoomboxSub.Cursor = Cursors.SizeNESW;
                    return true;
                }
                else if (selectVisual.bottomRight.Contains(point))
                {
                    FixedPoint = OldRect.TopLeft;
                    ZoomboxSub.Cursor = Cursors.SizeNWSE;
                    return true;
                }
                else if (selectVisual.middleTop.Contains(point))
                {
                    FixedPoint = OldRect.BottomLeft;
                    FixedPoint1 = OldRect.BottomRight;
                    ZoomboxSub.Cursor = Cursors.SizeNS;

                    return true;
                }
                else if (selectVisual.middleBottom.Contains(point))
                {
                    FixedPoint = OldRect.TopLeft;
                    FixedPoint1 = OldRect.TopRight;
                    ZoomboxSub.Cursor = Cursors.SizeNS;
                    return true;
                }
                else if (selectVisual.middleLeft.Contains(point))
                {
                    FixedPoint = OldRect.TopRight;
                    FixedPoint1 = OldRect.BottomRight;
                    ZoomboxSub.Cursor = Cursors.SizeWE;
                    return true;
                }
                else if (selectVisual.middleRight.Contains(point))
                {
                    FixedPoint = OldRect.TopLeft;
                    FixedPoint1 = OldRect.BottomLeft;
                    ZoomboxSub.Cursor = Cursors.SizeWE;
                    return true;
                }
                return false;
            }


            foreach (var item in ISelctRects)
            {
                if (Check(item))
                {
                    return true;
                }
            }

            foreach (var item in ISelctRects)
            {
                if (item.rect.Contains(point))
                {
                    ZoomboxSub.Cursor = Cursors.SizeAll;
                    return true;
                }
            }
            return false;
        }
        public void ClearRender()
        {
            Clear();
            Render();
        }
        private void Clear()
        {
            SelectVisuals.Clear();
            DrawCanvas.PreviewKeyDown -= PreviewKeyDown;
            ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;
        }


        public void SetRender<T>(T selectVisual)  where T :ISelectVisual
        {
            Clear();

            if (selectVisual != null)
            {
                SelectVisuals.Add(selectVisual);
                if (SelectVisuals.Count == 1)
                {
                    SelectVisualChanged?.Invoke(this, SelectVisuals[0]);
                }
                DrawCanvas.Focus();
                DrawCanvas.PreviewKeyDown += PreviewKeyDown;
                ZoomboxSub.LayoutUpdated += ZoomboxSub_LayoutUpdated;
            }
            if (!DrawCanvas.ContainsVisual(this))
            {
                DrawCanvas.AddVisual(this);
            }
            DrawCanvas.TopVisual(this);
            Render();
        }



        public event EventHandler<ISelectVisual> SelectVisualChanged;


        public void SetRenders<T>(IEnumerable<T> selectVisuals) where T : ISelectVisual
        {
            Clear();

            if (selectVisuals != null)
            {
                foreach (var item in selectVisuals)
                {
                    SelectVisuals.Add(item);
                }

                if (SelectVisuals.Count == 1)
                {
                    SelectVisualChanged?.Invoke(this, SelectVisuals[0]);
                }
                DrawCanvas.Focus();
                DrawCanvas.PreviewKeyDown += PreviewKeyDown;
                ZoomboxSub.LayoutUpdated += ZoomboxSub_LayoutUpdated;

            }
            if (!DrawCanvas.ContainsVisual(this))
            {
                DrawCanvas.AddVisual(this);
            }
            DrawCanvas.TopVisual(this);
            Render();
        }

        internal class ISelctRect:IDisposable
        {
            internal ISelectVisual ISelectVisual;
            internal Rect rect;
            internal Rect topLeft;
            internal Rect topRight;
            internal Rect bottomLeft;
            internal Rect bottomRight;
            internal Rect middleTop;
            internal Rect middleBottom;
            internal Rect middleLeft;
            internal Rect middleRight;

            public void Dispose()
            {
                ISelectVisual = null;
            }
        }

        private List<ISelctRect> ISelctRects = new List<ISelctRect>();

        SolidColorBrush SolidColorBrush = new SolidColorBrush(Color.FromArgb(1, 255, 255, 255));
        public void Render()
        {

            using DrawingContext dc = this.RenderOpen();
            if (SelectVisuals.Count == 0)
                return;
            Rect unionRect = SelectVisuals.Select(v => v.GetRect()).Aggregate((a, b) => Rect.Union(a, b));
            dc.DrawRectangle(SolidColorBrush, new Pen(Brushes.Transparent, 0), unionRect);

            ISelctRects.Clear();

            //全局选中性能太差
            if (SelectVisuals.Count < 1000)
            {
                foreach (var item in SelectVisuals)
                {
                    RederRecct(item.GetRect(), item);
                }
            }
            else
            {
                RederRecct(unionRect);
            }

            void RederRecct(Rect rect, ISelectVisual selectVisual =null)
            {
                double thickness = 1 / ZoomboxSub.ContentMatrix.M11;
                double thickness1 = thickness * 1.5;
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Black, thickness1), rect);
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), rect);

                // 小矩形的尺寸
                double smallRectSize = 10 * thickness;
                double halfSmallRectSize = smallRectSize / 2;

                // 计算每个角落的小矩形，使其中心在角落
                Rect topLeft = new Rect(rect.Left - halfSmallRectSize, rect.Top - halfSmallRectSize, smallRectSize, smallRectSize);
                Rect topRight = new Rect(rect.Right - halfSmallRectSize, rect.Top - halfSmallRectSize, smallRectSize, smallRectSize);
                Rect bottomLeft = new Rect(rect.Left - halfSmallRectSize, rect.Bottom - halfSmallRectSize, smallRectSize, smallRectSize);
                Rect bottomRight = new Rect(rect.Right - halfSmallRectSize, rect.Bottom - halfSmallRectSize, smallRectSize, smallRectSize);

                // 计算每条边中间的小矩形，使其中心在边的中点
                Rect middleTop = new Rect(rect.Left + (rect.Width / 2) - halfSmallRectSize, rect.Top - halfSmallRectSize, smallRectSize, smallRectSize);
                Rect middleBottom = new Rect(rect.Left + (rect.Width / 2) - halfSmallRectSize, rect.Bottom - halfSmallRectSize, smallRectSize, smallRectSize);
                Rect middleLeft = new Rect(rect.Left - halfSmallRectSize, rect.Top + (rect.Height / 2) - halfSmallRectSize, smallRectSize, smallRectSize);
                Rect middleRight = new Rect(rect.Right - halfSmallRectSize, rect.Top + (rect.Height / 2) - halfSmallRectSize, smallRectSize, smallRectSize);

                ISelctRect selctRect = new ISelctRect();
                selctRect.rect = rect;
                selctRect.ISelectVisual = selectVisual;
                selctRect.topLeft = topLeft;
                selctRect.topRight = topRight;
                selctRect.bottomLeft = bottomLeft;
                selctRect.bottomRight = bottomRight;
                selctRect.middleTop = middleTop;
                selctRect.middleBottom = middleBottom;
                selctRect.middleLeft = middleLeft;
                selctRect.middleRight = middleRight;
                ISelctRects.Add(selctRect);

                // 绘制小矩形

                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Black, thickness1), topLeft);
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Black, thickness1), topRight);
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Black, thickness1), bottomLeft);
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Black, thickness1), bottomRight);

                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Black, thickness1), middleTop);
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Black, thickness1), middleBottom);
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Black, thickness1), middleLeft);
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Black, thickness1), middleRight);

                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), topLeft);
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), topRight);
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), bottomLeft);
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), bottomRight);

                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), middleTop);
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), middleBottom);
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), middleLeft);
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), middleRight);




                Point start = new Point(middleTop.Left + middleTop.Width / 2, middleTop.Top + middleTop.Height / 2);
                Point end = start + new Vector(0, -40 * thickness);

                // Draw line
                dc.DrawLine(new Pen(Brushes.Black, thickness1), start, end);
                dc.DrawLine(new Pen(Brushes.White, thickness), start, end);

                // Draw rotation icon (simple circle for demonstration)
                double iconSize = 10 * thickness;
                Rect iconRect = new Rect(end.X - iconSize / 2, end.Y - iconSize / 2, iconSize, iconSize);
                dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.Black, thickness1), end, iconSize / 2, iconSize / 2);
                dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.White, thickness), end, iconSize / 2, iconSize / 2);
            }
        }
        /// <summary>
        /// 将所有选中区域合成为一个图片，并替换当前选中对象。
        /// </summary>
        public void RasterizeSelectionAndReplace()
        {
            if (SelectVisuals == null || SelectVisuals.Count == 0) return;

            // 1. 计算所有选中区域的外接矩形
            Rect unionRect = SelectVisuals.Select(v => v.GetRect()).Aggregate((a, b) => Rect.Union(a, b));
            // 2. 获取全局画布尺寸（假设 DrawCanvas.ActualWidth/ActualHeight）
            int canvasWidth = (int)Math.Ceiling(DrawCanvas.ActualWidth);
            int canvasHeight = (int)Math.Ceiling(DrawCanvas.ActualHeight);
            if (canvasWidth == 0 || canvasHeight == 0) return;

            // 3. 新建全局大图
            var rtb = new RenderTargetBitmap(canvasWidth, canvasHeight, 96, 96, PixelFormats.Pbgra32);

            // 4. 渲染所有选中的Visual到全局
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                foreach (var visual in SelectVisuals)
                {
                    if (visual is DrawingVisual drawVisual)
                    {
                        // 直接绘制，不偏移
                        dc.DrawDrawing(drawVisual.Drawing);
                    }
                }
            }
            rtb.Render(dv);

            // 5. 用 CroppedBitmap 截取 unionRect 区域
            var cropRect = new Int32Rect(
                (int)Math.Floor(unionRect.X),
                (int)Math.Floor(unionRect.Y),
                (int)Math.Ceiling(unionRect.Width),
                (int)Math.Ceiling(unionRect.Height)
            );
            var cropped = new CroppedBitmap(rtb, cropRect);

            // 4. 清空原选中，添加新的栅格化对象
            foreach (var visual in SelectVisuals.OfType<DrawingVisual>())
            {
                DrawCanvas.RemoveVisual(visual);
            }
            SelectVisuals.Clear();
            var rasterVisual = new RasterizedSelectVisual(cropped, unionRect);

            DrawCanvas.AddVisualCommand(rasterVisual);
            SelectVisuals.Add(rasterVisual);
            // 5. 触发重绘
            Render();
        }
        private void PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (SelectVisuals.Count == 0 || !ImageViewModel.ImageEditMode )
            {
                e.Handled = true;
                return;
            }
            Key realKey = e.Key;
            if (realKey == Key.ImeProcessed)
            {
                realKey = e.ImeProcessedKey;
            }

            if (!Keyboard.IsKeyDown(Key.LeftCtrl) && (realKey == Key.Left || realKey == Key.A))
            {
                foreach (var selectVisual in SelectVisuals)
                {
                    var OldRect = selectVisual.GetRect();
                    Rect rect = new Rect(OldRect.X - 2, OldRect.Y, OldRect.Width, OldRect.Height);
                    selectVisual.SetRect(rect);
                    Render();
                }
                e.Handled = true;
            }
            else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && (realKey == Key.Right || realKey == Key.D))
            {
                foreach (var selectVisual in SelectVisuals)
                {
                    var OldRect = selectVisual.GetRect();
                    Rect rect = new Rect(OldRect.X + 2, OldRect.Y, OldRect.Width, OldRect.Height);
                    selectVisual.SetRect(rect);
                    Render();
                }

                e.Handled = true;
            }
            else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && (realKey == Key.Up || realKey == Key.W))
            {
                foreach (var selectVisual in SelectVisuals)
                {
                    var OldRect = selectVisual.GetRect();
                    Rect rect = new Rect(OldRect.X, OldRect.Y - 2, OldRect.Width, OldRect.Height);
                    selectVisual.SetRect(rect);
                    Render();
                }
                e.Handled = true;
            }
            else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && (realKey == Key.Down || realKey == Key.S))
            {
                foreach (var selectVisual in SelectVisuals)
                {
                    var OldRect = selectVisual.GetRect();
                    Rect rect = new Rect(OldRect.X, OldRect.Y + 2, OldRect.Width, OldRect.Height);
                    selectVisual.SetRect(rect);
                    Render();
                }
                e.Handled = true;
            }
            else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && (realKey == Key.Add || realKey == Key.I))
            {
                foreach (var selectVisual in SelectVisuals)
                {
                    var OldRect = selectVisual.GetRect();
                    Rect rect = new Rect(OldRect.X - 1, OldRect.Y - 1, OldRect.Width + 2, OldRect.Height + 2);
                    selectVisual.SetRect(rect);
                    Render();
                }
                e.Handled = true;
            }
            else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && (realKey == Key.Subtract || realKey == Key.O))
            {
                foreach (var selectVisual in SelectVisuals)
                {
                    var OldRect = selectVisual.GetRect();
                    Rect rect = new Rect(OldRect.X + 1, OldRect.Y + 1, OldRect.Width - 2, OldRect.Height - 2);
                    selectVisual.SetRect(rect);
                    Render();
                }
                e.Handled = true;
            }
            else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && (realKey == Key.Delete))
            {
                foreach (var selectVisual in SelectVisuals.Cast<DrawingVisual>())
                {
                    DrawCanvas.RemoveVisualCommand(selectVisual);
                }
                ClearRender();
                e.Handled = true;

            }
        }


        private bool IsMouseDown;
        private Point MouseDownP;
        Point LastMouseMove;

        private void DrawCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.CaptureMouse();
            MouseDownP = e.GetPosition(DrawCanvas);
            IsMouseDown = true;

            if (!ImageViewModel.ImageEditMode || ImageViewModel.DrawEditorManager.Current !=null)
                return;

            var MouseVisual = DrawCanvas.GetVisual<Visual>(MouseDownP);
            if (MouseVisual == this)
                return;
            if (MouseVisual is IDrawingVisual drawingVisual)
            {
                if (ImageViewModel.ImageEditMode == true)
                {
                    if (drawingVisual is ISelectVisual visual)
                    {
                        if (SelectVisuals.Contains(visual))
                        {
                            return;
                        }
                        else
                        {
                            SetRender(visual);
                            if (!(GetContainingRect(MouseDownP)))
                                ZoomboxSub.Cursor = Cursors.Cross;
                        }
                    }
                    else
                    {
                        ClearRender();
                    }
                }
                return;
            }
            ClearRender();

            using DrawingContext dc = SelectRect.RenderOpen();
            dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#77F3F3F3")), new Pen(Brushes.Blue, 1), new Rect(MouseDownP, MouseDownP));
            DrawCanvas.AddVisual(SelectRect);

        }
        private void DrawCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && (ZoomboxSub.ActivateOn == ModifierKeys.None || !Keyboard.Modifiers.HasFlag((Enum)ZoomboxSub.ActivateOn)))
            {
                var point = e.GetPosition(drawCanvas);
                if (IsMouseDown)
                {
                    using DrawingContext dc = SelectRect.RenderOpen();
                    dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#77F3F3F3")), new Pen(Brushes.Blue, 1), new Rect(MouseDownP, point));

                    if (SelectVisuals.Count != 0)
                    {
                        if (ZoomboxSub.Cursor == Cursors.SizeAll)
                        {
                            foreach (var selectVisual in SelectVisuals)
                            {
                                var oldRect = selectVisual.GetRect(); ;
                                var deltaX = point.X - LastMouseMove.X;
                                var deltaY = point.Y - LastMouseMove.Y;

                                // 移动选择的区域
                                Rect rect = new System.Windows.Rect(
                                   oldRect.X + deltaX,
                                   oldRect.Y + deltaY,
                                   oldRect.Width,
                                   oldRect.Height
                               );
                                selectVisual.SetRect(rect);
                            }
                            Render();

                        }
                        if (ISelectVisual == null) return;
                        if (ZoomboxSub.Cursor == Cursors.SizeNWSE || ZoomboxSub.Cursor == Cursors.SizeNESW)
                        {
                            var oldRect = ISelectVisual.GetRect();
                            Point point1 = oldRect.TopLeft;

                            Rect rect = new System.Windows.Rect(FixedPoint, point);
                            ISelectVisual.SetRect(rect);
                            Render(); ;
                        }
                        else if (ZoomboxSub.Cursor == Cursors.SizeNS)
                        {
                            var oldRect = ISelectVisual.GetRect();
                            Point point1 = FixedPoint1;
                            point1.Y = point.Y;

                            Rect rect = new System.Windows.Rect(FixedPoint, point1);
                            ISelectVisual.SetRect(rect);
                            Render();
                        }
                        else if (ZoomboxSub.Cursor == Cursors.SizeWE)
                        {
                            var oldRect = ISelectVisual.GetRect();
                            Point point1 = FixedPoint1;
                            point1.X = point.X;
                            Rect rect = new System.Windows.Rect(FixedPoint, point1);
                            ISelectVisual.SetRect(rect);
                            Render();
                        }
                    }
                }
                else
                {
                    if (!(drawCanvas.GetVisual<Visual>(point) == ImageViewModel.SelectEditorVisual && GetContainingRect(point)))
                        ZoomboxSub.Cursor = Cursors.Cross;
                }
                LastMouseMove = point;
            }
        }

        private void DrawCanvas_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag((Enum)ZoomboxSub.ActivateOn))
            {
                if (IsMouseDown)
                {
                    IsMouseDown = false;
                    var MouseUpP = e.GetPosition(drawCanvas);

                    if (!ImageViewModel.SelectEditorVisual.Contains(MouseUpP))
                        ImageViewModel.SelectEditorVisual.ClearRender();

                    if (drawCanvas.ContainsVisual(SelectRect))
                    {
                        var List = drawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP)));
                        ImageViewModel.SelectEditorVisual.SetRenders(List.Cast<ISelectVisual>());
                        drawCanvas.RemoveVisual(SelectRect);
                    }

                    drawCanvas.ReleaseMouseCapture();
                }
            }
        }



        public void Dispose()
        {
            DrawCanvas?.RemoveVisual(this);
            if (ZoomboxSub != null)
                ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;
            SelectVisualChanged = null;
            GC.SuppressFinalize(this);
        }
    }
}
