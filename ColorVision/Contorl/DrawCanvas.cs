using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ColorVision
{
    public class DrawCanvas : Image
    {
        private List<Visual> visuals = new List<Visual>();

        protected override Visual GetVisualChild(int index) => visuals[index];

        protected override int VisualChildrenCount { get => visuals.Count; }

        public bool ContainsVisual(Visual visual) => visuals.Contains(visual);


        public void Clear()
        {
            foreach (var item in visuals)
            {
                RemoveVisualChild(item);
                RemoveLogicalChild(item);
            }
            visuals.Clear();
        }


        public void AddVisual(Visual visual)
        {
            visuals.Add(visual);

            AddVisualChild(visual);
            AddLogicalChild(visual);
        }

        public void RemoveVisual(Visual visual)
        {
            visuals.Remove(visual);

            RemoveVisualChild(visual);
            RemoveLogicalChild(visual);
        }
        public void TopVisual(Visual visual)
        {
            RemoveVisualChild(visual);
            RemoveLogicalChild(visual);

            AddVisualChild(visual);
            AddLogicalChild(visual);
        }


        public DrawingVisual? GetVisual(Point point)
        {
            HitTestResult hitResult = VisualTreeHelper.HitTest(this, point);
            return hitResult.VisualHit as DrawingVisual;
        }

        private List<DrawingVisual> hits = new List<DrawingVisual>();
        public List<DrawingVisual> GetVisuals(Geometry region)
        {
            hits.Clear();
            GeometryHitTestParameters parameters = new GeometryHitTestParameters(region);
            HitTestResultCallback callback = new HitTestResultCallback(HitTestCallback);
            VisualTreeHelper.HitTest(this, null, callback, parameters);
            return hits;
        }

        private HitTestResultBehavior HitTestCallback(HitTestResult result)
        {
            GeometryHitTestResult geometryResult = (GeometryHitTestResult)result;
            DrawingVisual visual = result.VisualHit as DrawingVisual;
            if (visual != null &&
                geometryResult.IntersectionDetail == IntersectionDetail.FullyInside)
            {
                hits.Add(visual);
            }
            return HitTestResultBehavior.Continue;
        }



    }

}
