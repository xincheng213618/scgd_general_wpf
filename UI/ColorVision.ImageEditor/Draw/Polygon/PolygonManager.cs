using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class PolygonManager : MultiPointDrawingToolBase<DVPolygon>
    {
        public PolygonManager(EditorContext context) : base(context)
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
            visual.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox.ContentMatrix.M11);
        }
    }
}
