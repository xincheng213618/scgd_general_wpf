using ColorVision.Common.MVVM;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck
{
    public class ViewResultLedCheck : ViewModelBase
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
