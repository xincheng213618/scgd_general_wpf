using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System;
using System.Windows;
using System.Windows.Input;

namespace Conoscope
{
    internal sealed class ConoscopeFocusCircleDrawTool : DragDrawingToolBase
    {
        private readonly ConoscopeImageHost host;
        private DVCircleText? draftCircle;

        public ConoscopeFocusCircleDrawTool(DrawEditorContext editorContext, ConoscopeImageHost host): base(editorContext)
        {
            this.host = host;
            Order = 3;
        }

        protected override bool TryHandleExistingSelection(Point point)
        {
            return false;
        }

        protected override void OnBeginDraw(Point startPoint, MouseButtonEventArgs e)
        {
            if (draftCircle != null)
            {
                e.Handled = true;
                return;
            }

            if (!host.CanCreateFocusCircleAt(startPoint))
            {
                e.Handled = true;
                return;
            }

            ClearCurrentSelection();
            draftCircle = host.CreateFocusCircle(startPoint);
            host.AttachFocusCircle(draftCircle);
            e.Handled = true;
        }

        protected override void OnUpdateDraw(Point currentPoint, MouseEventArgs e)
        {
            if (draftCircle == null)
            {
                return;
            }

            Point center = draftCircle.Attribute.Center;
            double radius = Math.Sqrt(Math.Pow(currentPoint.X - center.X, 2) + Math.Pow(currentPoint.Y - center.Y, 2));
            radius = host.ClampFocusCircleRadius(center, radius);
            draftCircle.Attribute.Radius = radius;
            draftCircle.Attribute.RadiusY = radius;
            draftCircle.Render();
            e.Handled = true;
        }

        protected override void OnEndDraw(Point endPoint, MouseButtonEventArgs e)
        {
            if (draftCircle == null)
            {
                return;
            }

            DVCircleText circle = draftCircle;
            draftCircle = null;

            if (circle.Attribute.Radius < ConoscopeImageHost.MinimumFocusCircleRadius)
            {
                host.RemoveFocusCircle(circle);
            }
            else
            {
                host.ConstrainFocusCircleToBoundary(circle);
                circle.Render();
            }

            e.Handled = true;
        }

        protected override void OnDeactivated()
        {
            if (draftCircle == null)
            {
                return;
            }

            host.RemoveFocusCircle(draftCircle);
            draftCircle = null;
        }
    }
}