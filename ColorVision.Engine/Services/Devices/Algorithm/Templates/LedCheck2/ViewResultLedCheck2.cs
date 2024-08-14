using ColorVision.Common.MVVM;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck2
{
    public class ViewResultLedCheck2 : ViewModelBase, IViewResult
    {
        public Point Point { get; set; }
        public double Radius { get; set; }
    }
}
