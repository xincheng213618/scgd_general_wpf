#pragma warning disable CS0414,CS8625
using System;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class BezierCurveManager : MultiPointDrawingToolBase<DVBezierCurve>
    {

        public BezierCurveManager(DrawEditorContext context) : base(context)
        {
            Order = 9;
            Icon = IEditorToolFactory.TryFindResource("DrawingImagePolygon");
        }

        protected override bool SupportsKeyboardCompletion => true;

        protected override DVBezierCurve CreateVisual()
        {
            return new DVBezierCurve() { AutoAttributeChanged = false };
        }

        protected override System.Collections.Generic.IList<Point> GetPoints(DVBezierCurve visual)
        {
            return visual.Points;
        }

        protected override void RenderVisual(DVBezierCurve visual)
        {
            visual.Render();
        }

        protected override void OnVisualCreated(DVBezierCurve visual)
        {
            double zoomRatio = Math.Max(Zoombox.ContentMatrix.M11, 0.0001);
            visual.Attribute.Brush = StyleConfig.StrokeBrush;
            visual.Attribute.Pen = new Pen(StyleConfig.StrokeBrush, StyleConfig.StrokeThickness / zoomRatio);
        }

        protected override void OnVisualCompleted(DVBezierCurve visual)
        {
            visual.AutoAttributeChanged = true;
            base.OnVisualCompleted(visual);
        }
    }
}
