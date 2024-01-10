#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.MVVM;
using System.Windows;

namespace ColorVision.Services.Device.Algorithm.Views
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
