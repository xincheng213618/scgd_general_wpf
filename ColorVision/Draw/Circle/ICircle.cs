#pragma warning disable CA1711,CA2211
using System.Windows;

namespace ColorVision.Draw
{
    public interface ICircle
    {
        public Point Center { get; set; }
        public double Radius { get;set; }
    }



}
