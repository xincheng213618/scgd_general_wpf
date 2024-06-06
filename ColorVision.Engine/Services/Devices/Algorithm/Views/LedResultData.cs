using ColorVision.Common.MVVM;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    public class LedResultData : ViewModelBase
    {
        public LedResultData(Point point, double radius)
        {
            Point = point;
            Radius = radius;
        }
        public Point Point { get; set; }
        public double Radius { get; set; }
    }
}
