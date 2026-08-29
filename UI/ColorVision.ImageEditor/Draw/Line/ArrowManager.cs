using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.Draw
{
    internal sealed class ArrowManager : LineManager
    {
        public ArrowManager(DrawEditorContext context)
            : base(context)
        {
            Order = 6;
            Icon = new TextBlock { Text = "➜" };
        }

        protected override void OnVisualMouseUp(DVLine visual, Point point)
        {
            if (visual.Points.Count < 2)
            {
                return;
            }

            Point tail = visual.Points[0];
            Point tip = visual.Points[^1];
            Vector direction = tip - tail;
            double length = direction.Length;
            if (!double.IsFinite(length) || length <= 0.001)
            {
                CancelActiveVisual();
                return;
            }

            direction.Normalize();
            double zoomRatio = GetSafeZoomRatio();

            double headLength = Math.Min(length * 0.35, 14 / zoomRatio);
            double halfHeadWidth = headLength * 0.55;
            Point headBase = tip - direction * headLength;
            Vector perpendicular = new(-direction.Y, direction.X);
            Point headLeft = headBase + perpendicular * halfHeadWidth;
            Point headRight = headBase - perpendicular * halfHeadWidth;

            visual.Points.Clear();
            visual.Points.Add(tail);
            visual.Points.Add(tip);
            visual.Points.Add(headLeft);
            visual.Points.Add(headRight);
            visual.Points.Add(tip);
        }
    }
}
