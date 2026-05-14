using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class PolygonManager : MultiPointDrawingToolBase<DVPolygon>
    {
        public PolygonManager(DrawEditorContext context) : base(context)
        {
            Order = 5;

            Icon = IEditorToolFactory.TryFindResource("DrawingImagePolygon");
        }

        protected override bool SupportsKeyboardCompletion => true;
        protected override bool SelectOnMouseUp => true;

        protected override DVPolygon CreateVisual()
        {
            return new DVPolygon();
        }

        protected override IList<Point> GetPoints(DVPolygon visual)
        {
            return visual.Points;
        }

        protected override void RenderVisual(DVPolygon visual)
        {
            visual.Render();
        }

        protected override void OnVisualCreated(DVPolygon visual)
        {
            double zoomRatio = Math.Max(Zoombox.ContentMatrix.M11, 0.0001);
            visual.Attribute.Brush = StyleConfig.StrokeBrush;
            visual.Attribute.Pen = new Pen(StyleConfig.StrokeBrush, StyleConfig.StrokeThickness / zoomRatio);
        }
    }
}
