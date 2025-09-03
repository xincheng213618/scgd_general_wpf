#pragma warning disable CS8625,CS8602,CS8607,CS0103,CS0067
using ColorVision.Common.Utilities;
using Gu.Wpf.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class SelectEditorVisual : DrawingVisual,IDisposable
    {
        public DrawCanvas DrawCanvas { get; set; }

        public ZoomboxSub ZoomboxSub { get; set; }
        public ImageViewModel ImageViewModel { get; set; }
        Guid Guid { get; set; }
        public SelectEditorVisual(ImageViewModel imageViewModel, DrawCanvas drawCanvas, ZoomboxSub zoomboxSub)
        {
            ImageViewModel = imageViewModel;
            DrawCanvas = drawCanvas;
            ZoomboxSub = zoomboxSub;
            DrawCanvas.AddVisual(this, false);
        }

        private void ZoomboxSub_LayoutUpdated(object? sender, System.EventArgs e)
        {
            DebounceTimer.AddOrResetTimerDispatcher("SelectEditorVisualRender" + Guid.ToString(), 20, () => Render());
        }
        public System.Windows.Forms.PropertyGrid PropertyGrid { get; set; }

        public Rect Rect { get => _Rect; set {  _Rect = value; }  }
        private Rect _Rect;

        public List<ISelectVisual> SelectVisuals { get; set; } = new List<ISelectVisual>();

        public bool IsEditor { get; set; }
        public class CacheClass
        {
            public Rect OldRect { get; set; }

            public Point FixedPoint { get; set; }
            public Point FixedPoint1 { get; set; }
        }


        public Dictionary<ISelectVisual, CacheClass> Cache { get; set; } = new Dictionary<ISelectVisual, CacheClass>();


        public bool GetContainingRect(Point point)
        {
            if (SelectVisuals.Count == 0) return false;

            double thickness = 1 / ZoomboxSub.ContentMatrix.M11;
            double smallRectSize = 10 * thickness;
            double halfSmallRectSize = smallRectSize / 2;


            bool Check(ISelectVisual selectVisual)
            {
                Rect Rect = selectVisual.GetRect();
                CacheClass cacheClass = new CacheClass();
                Cache.Add(selectVisual,cacheClass);

                cacheClass.OldRect = new Rect(Rect.X, Rect.Y, Rect.Width, Rect.Height);
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
                if (topLeft.Contains(point))
                {
                    ZoomboxSub.Cursor = Cursors.SizeNWSE;
                    cacheClass.FixedPoint = cacheClass.OldRect.BottomRight;
                    return true;
                }
                else if (topRight.Contains(point))
                {
                    cacheClass.FixedPoint = cacheClass.OldRect.BottomLeft;
                    ZoomboxSub.Cursor = Cursors.SizeNESW;
                    return true;
                }
                else if (bottomLeft.Contains(point))
                {
                    cacheClass.FixedPoint = cacheClass.OldRect.TopRight;
                    ZoomboxSub.Cursor = Cursors.SizeNESW;
                    return true;
                }
                else if (bottomRight.Contains(point))
                {
                    cacheClass.FixedPoint = cacheClass.OldRect.TopLeft;
                    ZoomboxSub.Cursor = Cursors.SizeNWSE;
                    return true;
                }
                else if (middleTop.Contains(point))
                {
                    cacheClass.FixedPoint = cacheClass.OldRect.BottomLeft;
                    cacheClass.FixedPoint1 = cacheClass.OldRect.BottomRight;
                    ZoomboxSub.Cursor = Cursors.SizeNS;

                    return true;
                }
                else if (middleBottom.Contains(point))
                {
                    cacheClass.FixedPoint = cacheClass.OldRect.TopLeft;
                    cacheClass.FixedPoint1 = cacheClass.OldRect.TopRight;
                    ZoomboxSub.Cursor = Cursors.SizeNS;
                    return true;
                }
                else if (middleLeft.Contains(point))
                {
                    cacheClass.FixedPoint = cacheClass.OldRect.TopRight;
                    cacheClass.FixedPoint1 = cacheClass.OldRect.BottomRight;
                    ZoomboxSub.Cursor = Cursors.SizeWE;
                    return true;
                }
                else if (middleRight.Contains(point))
                {
                    cacheClass.FixedPoint = cacheClass.OldRect.TopLeft;
                    cacheClass.FixedPoint1 = cacheClass.OldRect.BottomLeft;
                    ZoomboxSub.Cursor = Cursors.SizeWE;
                    return true;
                }
                else if (Rect.Contains(point))
                {
                    ZoomboxSub.Cursor = Cursors.SizeAll;
                    return true;
                }
                return false;

            }
            Cache.Clear();
            foreach (var item in SelectVisuals)
            {
                if (Check(item))
                {
                    return true;
                }
            }
            return false;
        }
        public void ClearRender()
        {
            SelectVisuals.Clear();
            DrawCanvas.PreviewKeyDown -= PreviewKeyDown;
            ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;
            if (PropertyGrid.SelectedObject is IDrawingVisual drawingVisualold)
                drawingVisualold.BaseAttribute.PropertyChanged -= Attribute_PropertyGrid2;

            Render();
        }

        public void SetRender<T>(T selectVisual)  where T :ISelectVisual
        {
            SelectVisuals.Clear();
            DrawCanvas.PreviewKeyDown -= PreviewKeyDown;
            ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;
            if (PropertyGrid.SelectedObject is IDrawingVisual drawingVisualold)
                drawingVisualold.BaseAttribute.PropertyChanged -= Attribute_PropertyGrid2;

            if (selectVisual != null)
            {
                SelectVisuals.Add(selectVisual);
                if (SelectVisuals.Count == 1)
                {
                    if (PropertyGrid != null && SelectVisuals[0] is IDrawingVisual drawingVisualBase)
                    {
                        PropertyGrid.SelectedObject = drawingVisualBase.BaseAttribute;
                        drawingVisualBase.BaseAttribute.PropertyChanged += Attribute_PropertyGrid2;
                    }
                }
                DrawCanvas.Focus();
                DrawCanvas.PreviewKeyDown += PreviewKeyDown;
                ZoomboxSub.LayoutUpdated += ZoomboxSub_LayoutUpdated;
            }
            Render();
        }

        public void SetRenders<T>(IEnumerable<T> selectVisuals) where T : ISelectVisual
        {
            SelectVisuals.Clear();
            DrawCanvas.PreviewKeyDown -= PreviewKeyDown;
            ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;
            if (PropertyGrid.SelectedObject is IDrawingVisual drawingVisualold)
                drawingVisualold.BaseAttribute.PropertyChanged -= Attribute_PropertyGrid2;

            if (selectVisuals != null)
            {
                foreach (var item in selectVisuals)
                {
                    SelectVisuals.Add(item);
                }

                if (SelectVisuals.Count == 1)
                {
                    if (PropertyGrid != null && SelectVisuals[0] is IDrawingVisual drawingVisualBase)
                    {
                        PropertyGrid.SelectedObject = drawingVisualBase.BaseAttribute;
                        drawingVisualBase.BaseAttribute.PropertyChanged += Attribute_PropertyGrid2;
                    }
                }

                DrawCanvas.PreviewKeyDown += PreviewKeyDown;
                DrawCanvas.Focus();
                ZoomboxSub.LayoutUpdated += ZoomboxSub_LayoutUpdated;
            }

            Render();
        }

        private void Attribute_PropertyGrid2(object? sender, PropertyChangedEventArgs e)
        {
            PropertyGrid.Refresh();
        }


        public void Render()
        {
            using DrawingContext dc = this.RenderOpen();
            if (SelectVisuals.Count == 0)
                return;
            foreach (var item in SelectVisuals)
            {
                RederRecct(item.GetRect());
            }
            void RederRecct(Rect rect)
            {
                double thickness = 1 / ZoomboxSub.ContentMatrix.M11;
                double thickness1 = thickness * 1.5;
                ;
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
                    DrawCanvas.RemoveVisual(selectVisual);
                }
                ClearRender();
                e.Handled = true;

            }
        }


        public void Dispose()
        {
            PropertyGrid?.Dispose();
            DrawCanvas?.RemoveVisual(this, false);
            if (ZoomboxSub != null)
                ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;

            GC.SuppressFinalize(this);
        }
    }
}
