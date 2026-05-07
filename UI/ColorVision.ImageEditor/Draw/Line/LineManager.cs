using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.Draw
{
    public class LineManager : MultiPointDrawingToolBase<DVLine>
    {
        public LineManager(EditorContext context) : base(context)
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
            double zoomRatio = Math.Max(Zoombox.ContentMatrix.M11, 0.0001);
            visual.Attribute.Brush = StyleConfig.StrokeBrush;
            visual.Attribute.Pen = new Pen(StyleConfig.StrokeBrush, StyleConfig.StrokeThickness / zoomRatio);
        }
    }
}
