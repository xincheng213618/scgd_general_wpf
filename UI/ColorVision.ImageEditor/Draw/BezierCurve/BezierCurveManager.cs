#pragma warning disable CS0414,CS8625
using System;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class BezierCurveManager : MultiPointDrawingToolBase<DVBezierCurve>
    {
        public BezierCurveManager(EditorContext context) : base(context)
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
            visual.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox.ContentMatrix.M11);
        }
    }
}
