using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.Draw
{
    public class LineManager : MultiPointDrawingToolBase<DVLine>
    {
        public LineManager(DrawEditorContext context) : base(context)
        {
            Order = 7;
            Icon =  new TextBlock() { Text = "L"};
        }

        protected override bool CompleteOnMouseUp => true;

        protected override DVLine CreateVisual()
        {
            return new DVLine();
        }

        protected override IList<Point> GetPoints(DVLine visual)
        {
            return visual.Points;
        }

        protected override void RenderVisual(DVLine visual)
        {
            visual.Render();
        }

        protected override void OnVisualCreated(DVLine visual)
        {
            double zoomRatio = GetSafeZoomRatio();
            visual.Attribute.Brush = StyleConfig.StrokeBrush;
            visual.Attribute.Pen = new Pen(StyleConfig.StrokeBrush, StyleConfig.StrokeThickness / zoomRatio);
        }

        protected override void OnVisualMouseUp(DVLine visual, Point point)
        {
            if (visual.Points.Count < 2)
            {
                CancelActiveVisual();
                return;
            }

            Vector direction = visual.Points[^1] - visual.Points[0];
            double lengthSquared = direction.LengthSquared;
            if (!double.IsFinite(lengthSquared) || lengthSquared <= 0)
                CancelActiveVisual();
        }
    }
}
