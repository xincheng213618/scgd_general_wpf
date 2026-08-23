#pragma warning disable CA1852,CS0067,CS0103,CS8602,CS8607,CS8625
using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw.Rasterized;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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
        private static readonly SolidColorBrush SelectionAreaBrush = new(Color.FromArgb(0x77, 0xF3, 0xF3, 0xF3));
        private static readonly SolidColorBrush SelectionHitTestBrush = new(Color.FromArgb(1, 255, 255, 255));
        private static readonly Pen SelectionAreaPen = new(Brushes.Blue, 1);

        static SelectEditorVisual()
        {
            SelectionAreaBrush.Freeze();
            SelectionHitTestBrush.Freeze();
            SelectionAreaPen.Freeze();
        }

        public DrawCanvas DrawCanvas { get; set; }

        public Zoombox ZoomboxSub { get; set; }

        private DrawingVisual SelectRect = new DrawingVisual();

        public DrawEditorContext EditorContext { get; set; }

        public TextEditingContext? TextEditingContext { get; set; }

        private readonly DispatcherTimer _layoutRenderTimer;

        public SelectEditorVisual(DrawEditorContext editorContext)
        {
            EditorContext = editorContext;
            DrawCanvas = EditorContext.DrawCanvas;
            ZoomboxSub = EditorContext.Zoombox;
            _layoutRenderTimer = new DispatcherTimer(DispatcherPriority.Render, DrawCanvas.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(20),
            };
            _layoutRenderTimer.Tick += LayoutRenderTimer_Tick;
            DrawCanvas.AddVisual(this);
            DrawCanvas.PreviewMouseLeftButtonDown += DrawCanvas_PreviewMouseLeftButtonDown;
            DrawCanvas.MouseMove += DrawCanvas_MouseMove;
            DrawCanvas.PreviewMouseUp += DrawCanvas_PreviewMouseUp;
            DrawCanvas.VisualsRemove += DrawCanvas_VisualsRemove;
        }


        private void ZoomboxSub_LayoutUpdated(object? sender, System.EventArgs e)
        {
            if (SelectVisuals.Count == 0)
                return;

            _layoutRenderTimer.Stop();
            _layoutRenderTimer.Start();
        }

        private void LayoutRenderTimer_Tick(object? sender, EventArgs e)
        {
            _layoutRenderTimer.Stop();
            if (SelectVisuals.Count != 0)
                Render();
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
                if (selectVisual.ISelectVisual == null)
                    return false;

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
            _layoutRenderTimer.Stop();
            SelectVisuals.Clear();
            ClearSelectionHandles();
            DrawCanvas.PreviewKeyDown -= PreviewKeyDown;
            ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;
        }

        private void ClearSelectionHandles(bool releaseInteractionTarget = true)
        {
            foreach (SelectionHandleRect selectRect in selectRects)
            {
                selectRect.Dispose();
            }

            selectRects.Clear();
            if (releaseInteractionTarget)
            {
                ISelectVisual = null;
            }
        }

        private void DrawCanvas_VisualsRemove(object? sender, VisualChangedEventArgs e)
        {
            if (e.Visual is not ISelectVisual removedVisual || !SelectVisuals.Remove(removedVisual))
            {
                return;
            }

            ClearSelectionHandles();
            if (SelectVisuals.Count == 0)
            {
                DrawCanvas.PreviewKeyDown -= PreviewKeyDown;
                ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;
            }
            else if (SelectVisuals.Count == 1)
            {
                SelectVisualChanged?.Invoke(this, SelectVisuals[0]);
            }

            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Render();
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
                if (SelectVisuals.Count != 0)
                {
                    DrawCanvas.Focus();
                    DrawCanvas.PreviewKeyDown += PreviewKeyDown;
                    ZoomboxSub.LayoutUpdated += ZoomboxSub_LayoutUpdated;
                }
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
                if (SelectVisuals.Count != 0)
                {
                    DrawCanvas.Focus();
                    DrawCanvas.PreviewKeyDown += PreviewKeyDown;
                    ZoomboxSub.LayoutUpdated += ZoomboxSub_LayoutUpdated;
                }

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
            internal ISelectVisual? ISelectVisual;
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

        public void Render()
        {
            using DrawingContext dc = this.RenderOpen();
            ClearSelectionHandles(releaseInteractionTarget: !IsMouseDown);
            if (SelectVisuals.Count == 0)
                return;
            double zoomRatio = ZoomboxSub.ContentMatrix.M11;
            if (!double.IsFinite(zoomRatio) || zoomRatio <= 0)
                zoomRatio = 1;
            double thickness = 1 / zoomRatio;
            Pen blackPen = new(Brushes.Black, thickness * 1.5);
            Pen whitePen = new(Brushes.White, thickness);
            blackPen.Freeze();
            whitePen.Freeze();
            Rect[]? detailedBounds = SelectVisuals.Count < DetailedSelectionLimit ? new Rect[SelectVisuals.Count] : null;
            Rect unionRect = Rect.Empty;
            for (int index = 0; index < SelectVisuals.Count; index++)
            {
                Rect bounds = SelectVisuals[index].GetRect();
                unionRect = unionRect.IsEmpty ? bounds : Rect.Union(unionRect, bounds);
                if (detailedBounds != null)
                {
                    detailedBounds[index] = bounds;
                }
            }
            dc.DrawRectangle(SelectionHitTestBrush, null, unionRect);

            if (detailedBounds != null)
            {
                for (int index = 0; index < SelectVisuals.Count; index++)
                {
                    RenderRect(detailedBounds[index], SelectVisuals[index]);
                }
            }
            else
            {
                RenderRect(unionRect);
            }

            void RenderRect(Rect rect, ISelectVisual? selectVisual = null)
            {
                dc.DrawRectangle(Brushes.Transparent, blackPen, rect);
                dc.DrawRectangle(Brushes.Transparent, whitePen, rect);

                if (selectVisual == null || selectVisual is IEditableDrawingVisual)
                {
                    selectRects.Add(new SelectionHandleRect
                    {
                        rect = rect,
                        ISelectVisual = selectVisual,
                    });
                    return;
                }

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
            // 2. 获取全局画布尺寸；未完成布局时回退到显式尺寸。
            double width = DrawCanvas.ActualWidth > 0 ? DrawCanvas.ActualWidth : DrawCanvas.Width;
            double height = DrawCanvas.ActualHeight > 0 ? DrawCanvas.ActualHeight : DrawCanvas.Height;
            if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
                return;
            int canvasWidth = (int)Math.Ceiling(width);
            int canvasHeight = (int)Math.Ceiling(height);

            var cropRect = new Int32Rect(
                (int)Math.Floor(unionRect.X),
                (int)Math.Floor(unionRect.Y),
                (int)Math.Ceiling(unionRect.Width),
                (int)Math.Ceiling(unionRect.Height)
            );
            BitmapSource cropped = RasterizeSelection(SelectVisuals, canvasWidth, canvasHeight, cropRect);

            DrawCanvas canvas = DrawCanvas;
            List<Visual> canvasVisuals = canvas.Visuals.ToList();
            List<(DrawingVisual Visual, int Index)> originalVisuals = SelectVisuals
                .OfType<DrawingVisual>()
                .Select(visual => (Visual: visual, Index: canvasVisuals.IndexOf(visual)))
                .Where(item => item.Index >= 0)
                .OrderBy(item => item.Index)
                .ToList();
            if (originalVisuals.Count == 0)
            {
                return;
            }

            foreach (var (visual, _) in originalVisuals)
                canvas.RemoveVisual(visual);

            RasterizedSelectVisual rasterVisual = new(cropped, unionRect);
            canvas.AddVisual(rasterVisual);
            canvas.AddActionCommand(new ActionCommand(
                () =>
                {
                    canvas.RemoveVisual(rasterVisual);
                    foreach (var (visual, index) in originalVisuals)
                        canvas.InsertVisual(index, visual);
                },
                () =>
                {
                    foreach (var (visual, _) in originalVisuals)
                        canvas.RemoveVisual(visual);
                    canvas.AddVisual(rasterVisual);
                })
            {
                Header = ColorVision.ImageEditor.Properties.Resources.Draw_Rasterize,
            });

            SetRender(rasterVisual);
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
            if (SelectVisuals.Count == 0 || !EditorContext.IsImageEditMode)
            {
                return;
            }
            Key realKey = e.Key;
            if (realKey == Key.ImeProcessed)
            {
                realKey = e.ImeProcessedKey;
            }

            if (realKey == Key.F2 && TextEditingContext != null && SelectVisuals.Count == 1 &&
                SelectVisuals[0] is IEditableDrawingVisual editableVisual && editableVisual.SupportsDoubleClickEditing)
            {
                editableVisual.BeginEdit(TextEditingContext);
                e.Handled = true;
            }
            else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && (realKey == Key.Left || realKey == Key.A))
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
            else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && SelectVisuals.All(visual => visual is not IEditableDrawingVisual) && (realKey == Key.Add || realKey == Key.I))
            {
                TransformSelection(-1, -1, 2, 2);
                e.Handled = true;
            }
            else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && SelectVisuals.All(visual => visual is not IEditableDrawingVisual) && (realKey == Key.Subtract || realKey == Key.O))
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

        private void DrawCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!EditorContext.IsImageEditMode || EditorContext.DrawEditorManager.Current !=null)
                return;

            Point mousePosition = e.GetPosition(DrawCanvas);
            if (e.ClickCount == 2)
            {
                HandleDoubleClick(mousePosition);
                e.Handled = true;
                return;
            }

            DrawCanvas.CaptureMouse();
            MouseDownP = mousePosition;
            LastMouseMove = MouseDownP;
            IsMouseDown = true;

            var MouseVisual = DrawCanvas.GetVisual<Visual>(MouseDownP);
            if (MouseVisual == this)
            {
                GetContainingRect(MouseDownP);
                return;
            }
            if (MouseVisual is IDrawingVisual drawingVisual)
            {
                if (EditorContext.IsImageEditMode == true)
                {
                    if (drawingVisual is ISelectVisual visual)
                    {
                        if (SelectVisuals.Contains(visual))
                        {
                            GetContainingRect(MouseDownP);
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
            dc.DrawRectangle(SelectionAreaBrush, SelectionAreaPen, new Rect(MouseDownP, MouseDownP));
            DrawCanvas.AddOverlayVisual(SelectRect);

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
                if (!IsMouseDown && (!EditorContext.IsImageEditMode || EditorContext.DrawEditorManager.Current != null))
                    return;

                var point = e.GetPosition(drawCanvas);
                if (IsMouseDown && point == LastMouseMove)
                    return;

                if (IsMouseDown)
                {
                    if (drawCanvas.ContainsVisual(SelectRect))
                    {
                        using DrawingContext dc = SelectRect.RenderOpen();
                        dc.DrawRectangle(SelectionAreaBrush, SelectionAreaPen, new Rect(MouseDownP, point));
                    }

                    if (SelectVisuals.Count != 0)
                    {
                        if (ZoomboxSub.Cursor == Cursors.SizeAll)
                        {
                            Vector delta = point - LastMouseMove;
                            foreach (var selectVisual in SelectVisuals)
                            {
                                Rect oldRect = selectVisual.GetRect();

                                // 移动选择的区域
                                Rect rect = new Rect(
                                   oldRect.X + delta.X,
                                   oldRect.Y + delta.Y,
                                   oldRect.Width,
                                   oldRect.Height
                               );
                                selectVisual.SetRect(rect);
                            }
                            Render();
                            LastMouseMove = point;
                            return;
                        }
                        if (ISelectVisual == null) return;
                        if (ZoomboxSub.Cursor == Cursors.SizeNWSE || ZoomboxSub.Cursor == Cursors.SizeNESW)
                        {
                            Rect rect = new Rect(FixedPoint, point);
                            ISelectVisual.SetRect(rect);
                            Render();
                        }
                        else if (ZoomboxSub.Cursor == Cursors.SizeNS)
                        {
                            Point point1 = FixedPoint1;
                            point1.Y = point.Y;

                            Rect rect = new Rect(FixedPoint, point1);
                            ISelectVisual.SetRect(rect);
                            Render();
                        }
                        else if (ZoomboxSub.Cursor == Cursors.SizeWE)
                        {
                            Point point1 = FixedPoint1;
                            point1.X = point.X;
                            Rect rect = new Rect(FixedPoint, point1);
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
            if (sender is not DrawCanvas drawCanvas || !IsMouseDown)
            {
                return;
            }

            try
            {
                bool isZoomModifierActive = ZoomboxSub.ActivateOn != ModifierKeys.None &&
                    Keyboard.Modifiers.HasFlag((Enum)ZoomboxSub.ActivateOn);
                if (isZoomModifierActive)
                {
                    return;
                }

                var MouseUpP = e.GetPosition(drawCanvas);

                if (!Contains(MouseUpP))
                    ClearRender();

                if (drawCanvas.ContainsVisual(SelectRect))
                {
                    var List = drawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP)));
                    SetRenders(List.OfType<ISelectVisual>());
                }
            }
            finally
            {
                if (drawCanvas.ContainsVisual(SelectRect))
                {
                    drawCanvas.RemoveOverlayVisual(SelectRect);
                }

                IsMouseDown = false;
                ISelectVisual = null;
                drawCanvas.ReleaseMouseCapture();
            }
        }



        public void Dispose()
        {
            Clear();
            _layoutRenderTimer.Tick -= LayoutRenderTimer_Tick;
            IsMouseDown = false;
            if (DrawCanvas != null)
            {
                if (DrawCanvas.ContainsVisual(SelectRect))
                {
                    DrawCanvas.RemoveOverlayVisual(SelectRect);
                }
                DrawCanvas.ReleaseMouseCapture();
                DrawCanvas.PreviewMouseLeftButtonDown -= DrawCanvas_PreviewMouseLeftButtonDown;
                DrawCanvas.MouseMove -= DrawCanvas_MouseMove;
                DrawCanvas.PreviewMouseUp -= DrawCanvas_PreviewMouseUp;
                DrawCanvas.VisualsRemove -= DrawCanvas_VisualsRemove;
                DrawCanvas.PreviewKeyDown -= PreviewKeyDown;
                DrawCanvas.RemoveVisual(this);
            }
            if (ZoomboxSub != null)
                ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;
            SelectVisualChanged = null;
            SelectionChanged = null;
            GC.SuppressFinalize(this);
        }
    }
}
