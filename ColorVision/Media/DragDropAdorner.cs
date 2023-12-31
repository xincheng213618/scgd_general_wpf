﻿using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;


namespace ColorVision.Media
{
    public class DragDropAdorner : Adorner
    {
        public DragDropAdorner(UIElement parent): base(parent)
        {
            IsHitTestVisible = false; // Seems Adorner is hit test visible?
            if (parent is FrameworkElement framework)
                mDraggedElement = framework;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (mDraggedElement != null)
            {
                POINT screenPos = new POINT();
                if (GetCursorPos(ref screenPos))
                {
                    Point pos = this.PointFromScreen(new System.Windows.Point(screenPos.X, screenPos.Y));
                    Rect rect = new Rect(pos.X, pos.Y, mDraggedElement.ActualWidth, mDraggedElement.ActualHeight);
                    drawingContext.PushOpacity(1.0);

                    if (mDraggedElement.TryFindResource(SystemColors.HighlightBrushKey) is Brush highlight)
                        drawingContext.DrawRectangle(highlight, new Pen(Brushes.Transparent, 0), rect);
                    drawingContext.DrawRectangle(new VisualBrush(mDraggedElement), new Pen(Brushes.Transparent, 0), rect);
                }
            }
        }
        public struct POINT { public int X { get; set; } public int Y { get; set; } }
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(ref POINT point);

        FrameworkElement? mDraggedElement;
    }
}
