#pragma warning disable CA1852,CS0067,CS0103,CS8602,CS8607,CS8625
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

        public IEnumerable<MenuItem> GetContextMenuItems(object obj)
        {
            List<MenuItem> MenuItems = new List<MenuItem>();
            if (obj is SelectEditorVisual selectEditorVisual)
            {
                MenuItem menuIte2 = new() { Header = ColorVision.ImageEditor.Properties.Resources.Draw_Rasterize };
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
        private const int DetailedSelectionLimit = 32;

        public DrawCanvas DrawCanvas { get; set; }

        public Zoombox ZoomboxSub { get; set; }

        private DrawingVisual SelectRect = new DrawingVisual();

        public DrawEditorContext EditorContext { get; set; }

        public TextEditingContext? TextEditingContext { get; set; }

        public SelectEditorVisual(DrawEditorContext editorContext)
        {
            EditorContext = editorContext;
            DrawCanvas = EditorContext.DrawCanvas;
            ZoomboxSub = EditorContext.Zoombox;
            DrawCanvas.AddVisual(this);
            DrawCanvas.PreviewMouseLeftButtonDown += DrawCanvas_PreviewMouseLeftButtonDown;
            DrawCanvas.MouseMove += DrawCanvas_MouseMove;
            DrawCanvas.PreviewMouseUp += DrawCanvas_PreviewMouseUp;
        }


        private void ZoomboxSub_LayoutUpdated(object? sender, System.EventArgs e)
        {
            DebounceTimer.AddOrResetTimerDispatcher("SelectEditorVisualRender" + EditorContext.Id.ToString(), 20, () => Render());
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

            bool Check(SelectionHandleRect selectVisual)
            {
                ISelectVisual = selectVisual.ISelectVisual;

                Rect Rect = selectVisual.rect;

                OldRect = new Rect(Rect.X, Rect.Y, Rect.Width, Rect.Height);

                // 检查点在哪个小矩形内
                if (selectVisual.topLeft.Contains(point))
                {
                    SetCursor(Cursors.SizeNWSE);
                    FixedPoint = OldRect.BottomRight;
                    return true;
                }
                else if (selectVisual.topRight.Contains(point))
                {
                    FixedPoint = OldRect.BottomLeft;
                    SetCursor(Cursors.SizeNESW);
                    return true;
                }
                else if (selectVisual.bottomLeft.Contains(point))
                {
                    FixedPoint = OldRect.TopRight;
                    SetCursor(Cursors.SizeNESW);
                    return true;
                }
                else if (selectVisual.bottomRight.Contains(point))
                {
                    FixedPoint = OldRect.TopLeft;
                    SetCursor(Cursors.SizeNWSE);
                    return true;
                }
                else if (selectVisual.middleTop.Contains(point))
                {
                    FixedPoint = OldRect.BottomLeft;
                    FixedPoint1 = OldRect.BottomRight;
                    SetCursor(Cursors.SizeNS);

                    return true;
                }
                else if (selectVisual.middleBottom.Contains(point))
                {
                    FixedPoint = OldRect.TopLeft;
                    FixedPoint1 = OldRect.TopRight;
                    SetCursor(Cursors.SizeNS);
                    return true;
                }
                else if (selectVisual.middleLeft.Contains(point))
                {
                    FixedPoint = OldRect.TopRight;
                    FixedPoint1 = OldRect.BottomRight;
                    SetCursor(Cursors.SizeWE);
                    return true;
                }
                else if (selectVisual.middleRight.Contains(point))
                {
                    FixedPoint = OldRect.TopLeft;
                    FixedPoint1 = OldRect.BottomLeft;
                    SetCursor(Cursors.SizeWE);
                    return true;
                }
                return false;
            }


            foreach (var item in selectRects)
            {
                if (Check(item))
                {
                    return true;
                }
            }

            foreach (var item in selectRects)
            {
                if (item.rect.Contains(point))
                {
                    SetCursor(Cursors.SizeAll);
                    return true;
                }
            }
            return false;
        }

        private void SetCursor(Cursor cursor)
        {
            if (ZoomboxSub.Cursor != cursor)
                ZoomboxSub.Cursor = cursor;
        }

        public void ClearRender()
        {
            Clear();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Render();
        }
        private void Clear()
        {
            SelectVisuals.Clear();
            DrawCanvas.PreviewKeyDown -= PreviewKeyDown;
            ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;
        }

        public ISelectVisual? PrimarySelectedVisual => SelectVisuals.Count == 1 ? SelectVisuals[0] : null;


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
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            if (!DrawCanvas.ContainsVisual(this))
            {
                DrawCanvas.AddVisual(this);
            }
            DrawCanvas.TopVisual(this);
            Render();
        }



        public event EventHandler<ISelectVisual> SelectVisualChanged;
        public event EventHandler? SelectionChanged;


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
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            if (!DrawCanvas.ContainsVisual(this))
            {
                DrawCanvas.AddVisual(this);
            }
            DrawCanvas.TopVisual(this);
            Render();
        }

        internal class SelectionHandleRect:IDisposable
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

        private List<SelectionHandleRect> selectRects = new List<SelectionHandleRect>();

        SolidColorBrush SolidColorBrush = new SolidColorBrush(Color.FromArgb(1, 255, 255, 255));
        public void Render()
        {
            using DrawingContext dc = this.RenderOpen();
            if (SelectVisuals.Count == 0)
                return;
            double thickness = 1 / ZoomboxSub.ContentMatrix.M11;
            Pen blackPen = new(Brushes.Black, thickness * 1.5);
            Pen whitePen = new(Brushes.White, thickness);
            Rect unionRect = SelectVisuals.Select(v => v.GetRect()).Aggregate((a, b) => Rect.Union(a, b));
            dc.DrawRectangle(SolidColorBrush, null, unionRect);

            selectRects.Clear();

            if (SelectVisuals.Count < DetailedSelectionLimit)
            {
                foreach (var item in SelectVisuals)
                {
                    RenderRect(item.GetRect(), item);
                }
            }
            else
            {
                RenderRect(unionRect);
            }

            void RenderRect(Rect rect, ISelectVisual selectVisual =null)
            {
                dc.DrawRectangle(Brushes.Transparent, blackPen, rect);
                dc.DrawRectangle(Brushes.Transparent, whitePen, rect);

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

                SelectionHandleRect selectRect = new SelectionHandleRect();
                selectRect.rect = rect;
                selectRect.ISelectVisual = selectVisual;
                selectRect.topLeft = topLeft;
                selectRect.topRight = topRight;
                selectRect.bottomLeft = bottomLeft;
                selectRect.bottomRight = bottomRight;
                selectRect.middleTop = middleTop;
                selectRect.middleBottom = middleBottom;
                selectRect.middleLeft = middleLeft;
                selectRect.middleRight = middleRight;
                selectRects.Add(selectRect);

                // 绘制小矩形

                dc.DrawRectangle(Brushes.Transparent, blackPen, topLeft);
                dc.DrawRectangle(Brushes.Transparent, blackPen, topRight);
                dc.DrawRectangle(Brushes.Transparent, blackPen, bottomLeft);
                dc.DrawRectangle(Brushes.Transparent, blackPen, bottomRight);

                dc.DrawRectangle(Brushes.Transparent, blackPen, middleTop);
                dc.DrawRectangle(Brushes.Transparent, blackPen, middleBottom);
                dc.DrawRectangle(Brushes.Transparent, blackPen, middleLeft);
                dc.DrawRectangle(Brushes.Transparent, blackPen, middleRight);

                dc.DrawRectangle(Brushes.Transparent, whitePen, topLeft);
                dc.DrawRectangle(Brushes.Transparent, whitePen, topRight);
                dc.DrawRectangle(Brushes.Transparent, whitePen, bottomLeft);
                dc.DrawRectangle(Brushes.Transparent, whitePen, bottomRight);

                dc.DrawRectangle(Brushes.Transparent, whitePen, middleTop);
                dc.DrawRectangle(Brushes.Transparent, whitePen, middleBottom);
                dc.DrawRectangle(Brushes.Transparent, whitePen, middleLeft);
                dc.DrawRectangle(Brushes.Transparent, whitePen, middleRight);




                Point start = new Point(middleTop.Left + middleTop.Width / 2, middleTop.Top + middleTop.Height / 2);
                Point end = start + new Vector(0, -40 * thickness);

                // Draw line
                dc.DrawLine(blackPen, start, end);
                dc.DrawLine(whitePen, start, end);

                // Draw rotation icon (simple circle for demonstration)
                double iconSize = 10 * thickness;
                dc.DrawEllipse(Brushes.Transparent, blackPen, end, iconSize / 2, iconSize / 2);
                dc.DrawEllipse(Brushes.Transparent, whitePen, end, iconSize / 2, iconSize / 2);
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

            var cropRect = new Int32Rect(
                (int)Math.Floor(unionRect.X),
                (int)Math.Floor(unionRect.Y),
                (int)Math.Ceiling(unionRect.Width),
                (int)Math.Ceiling(unionRect.Height)
            );
            BitmapSource cropped = RasterizeSelection(SelectVisuals, canvasWidth, canvasHeight, cropRect);

            foreach (var visual in SelectVisuals.OfType<DrawingVisual>())
            {
                DrawCanvas.RemoveVisual(visual);
            }
            SelectVisuals.Clear();
            var rasterVisual = new RasterizedSelectVisual(cropped, unionRect);

            DrawCanvas.AddVisualCommand(rasterVisual);
            SelectVisuals.Add(rasterVisual);
            Render();
        }

        private static RenderTargetBitmap RasterizeSelection(IEnumerable<ISelectVisual> visuals, int canvasWidth, int canvasHeight, Int32Rect cropRect)
        {
            Int32Rect renderRect = cropRect.IsEmpty ? new Int32Rect(0, 0, canvasWidth, canvasHeight) : cropRect;
            if (!cropRect.IsEmpty && (renderRect.X < 0 || renderRect.Y < 0 || renderRect.Width <= 0 || renderRect.Height <= 0 ||
                (long)renderRect.X + renderRect.Width > canvasWidth || (long)renderRect.Y + renderRect.Height > canvasHeight))
                throw new ArgumentException("The crop rectangle must be within the canvas.", nameof(cropRect));

            RenderTargetBitmap bitmap = new(renderRect.Width, renderRect.Height, 96, 96, PixelFormats.Pbgra32);
            DrawingVisual composite = new();
            using (DrawingContext context = composite.RenderOpen())
            {
                context.PushTransform(new TranslateTransform(-renderRect.X, -renderRect.Y));
                foreach (DrawingVisual visual in visuals.OfType<DrawingVisual>())
                    context.DrawDrawing(visual.Drawing);
                context.Pop();
            }
            bitmap.Render(composite);
            return bitmap;
        }

        private void PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (SelectVisuals.Count == 0 || !EditorContext.IsImageEditMode )
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
                TransformSelection(-2, 0, 0, 0);
                e.Handled = true;
            }
            else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && (realKey == Key.Right || realKey == Key.D))
            {
                TransformSelection(2, 0, 0, 0);
                e.Handled = true;
            }
            else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && (realKey == Key.Up || realKey == Key.W))
            {
                TransformSelection(0, -2, 0, 0);
                e.Handled = true;
            }
            else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && (realKey == Key.Down || realKey == Key.S))
            {
                TransformSelection(0, 2, 0, 0);
                e.Handled = true;
            }
            else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && (realKey == Key.Add || realKey == Key.I))
            {
                TransformSelection(-1, -1, 2, 2);
                e.Handled = true;
            }
            else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && (realKey == Key.Subtract || realKey == Key.O))
            {
                TransformSelection(1, 1, -2, -2);
                e.Handled = true;
            }
            else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && (realKey == Key.Delete))
            {
                foreach (var selectVisual in SelectVisuals.Cast<DrawingVisual>().ToList())
                {
                    DrawCanvas.RemoveVisualCommand(selectVisual);
                }
                ClearRender();
                e.Handled = true;

            }
        }

        private void TransformSelection(double xOffset, double yOffset, double widthOffset, double heightOffset)
        {
            foreach (var selectVisual in SelectVisuals)
            {
                Rect oldRect = selectVisual.GetRect();
                selectVisual.SetRect(new Rect(
                    oldRect.X + xOffset,
                    oldRect.Y + yOffset,
                    oldRect.Width + widthOffset,
                    oldRect.Height + heightOffset));
            }
            Render();
        }


        private bool IsMouseDown;
        private Point MouseDownP;
        Point LastMouseMove;

        // 双击检测
        private DateTime _lastClickTime;
        private Point _lastClickPosition;
        private const int DoubleClickTime = 300; // ms
        private const double DoubleClickDistance = 5; // 像素

        private void DrawCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!EditorContext.IsImageEditMode || EditorContext.DrawEditorManager.Current !=null)
                return;

            DrawCanvas.CaptureMouse();
            MouseDownP = e.GetPosition(DrawCanvas);
            IsMouseDown = true;

            // 双击检测
            DateTime now = DateTime.Now;
            double distance = Math.Sqrt(Math.Pow(MouseDownP.X - _lastClickPosition.X, 2) +
                                       Math.Pow(MouseDownP.Y - _lastClickPosition.Y, 2));

            if ((now - _lastClickTime).TotalMilliseconds <= DoubleClickTime && distance <= DoubleClickDistance)
            {
                // 处理双击
                HandleDoubleClick(MouseDownP);
                _lastClickTime = DateTime.MinValue; // 重置，防止连续触发
                e.Handled = true;
                return;
            }

            _lastClickTime = now;
            _lastClickPosition = MouseDownP;

            var MouseVisual = DrawCanvas.GetVisual<Visual>(MouseDownP);
            if (MouseVisual == this)
                return;
            if (MouseVisual is IDrawingVisual drawingVisual)
            {
                if (EditorContext.IsImageEditMode == true)
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
                            if (!GetContainingRect(MouseDownP))
                                SetCursor(Cursors.Cross);
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

        /// <summary>
        /// 处理双击事件
        /// </summary>
        private void HandleDoubleClick(Point point)
        {
            if (TextEditingContext == null)
            {
                return;
            }

            // 检查当前选中的视觉元素
            foreach (var visual in SelectVisuals)
            {
                if (visual is IEditableDrawingVisual editableVisual && visual.GetRect().Contains(point))
                {
                    editableVisual.HandleDoubleClick(TextEditingContext, point);
                    return;
                }
            }

            // 如果没有选中，检查点击位置的视觉元素
            var clickedVisual = DrawCanvas.GetVisual<Visual>(point);
            if (clickedVisual is IEditableDrawingVisual editable && clickedVisual is ISelectVisual selectVisual)
            {
                SetRender(selectVisual);
                editable.HandleDoubleClick(TextEditingContext, point);
            }
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
                    if (!GetContainingRect(point))
                        SetCursor(Cursors.Cross);
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

                    if (!Contains(MouseUpP))
                        ClearRender();

                    if (drawCanvas.ContainsVisual(SelectRect))
                    {
                        var List = drawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP)));
                        SetRenders(List.Cast<ISelectVisual>());
                        drawCanvas.RemoveVisual(SelectRect);
                    }

                    drawCanvas.ReleaseMouseCapture();
                }
            }
        }



        public void Dispose()
        {
            if (DrawCanvas != null)
            {
                DrawCanvas.PreviewMouseLeftButtonDown -= DrawCanvas_PreviewMouseLeftButtonDown;
                DrawCanvas.MouseMove -= DrawCanvas_MouseMove;
                DrawCanvas.PreviewMouseUp -= DrawCanvas_PreviewMouseUp;
                DrawCanvas.PreviewKeyDown -= PreviewKeyDown;
                DrawCanvas.RemoveVisual(this);
            }
            if (ZoomboxSub != null)
                ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;
            SelectVisualChanged = null;
            GC.SuppressFinalize(this);
        }
    }
}
