using ColorVision.Common.MVVM;
using System.Windows;

namespace ColorVision.Engine.Templates.LedCheck
{
    public class ViewResultLedCheck : ViewModelBase, IViewResult
    {
        public ViewResultLedCheck(Point point, double radius)
        {
            Point = point;
            Radius = radius;
        }
        public Point Point { get; set; }
        public double Radius { get; set; }
    }
}
