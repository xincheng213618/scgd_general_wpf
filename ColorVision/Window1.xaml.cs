using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LiveChartsCore.Drawing;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using System.Collections.ObjectModel;
using NPOI.SS.Formula.Functions;
using static cvColorVision.GCSDLL;

namespace ColorVision
{
    /// <summary>
    /// Window1.xaml 的交互逻辑
    /// </summary>
    public partial class Window1 : Window
    {
        public Window1()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            _lineSeries = new LineSeries<ObservablePoint>
            {
                LineSmoothness = 0.5
            };
            List<ObservablePoint> observablePoint = new List<ObservablePoint>();
            _lineSeries.Values = observablePoint;
            _lineSeries.DataLabelsMaxWidth = 200;
            Random random = new Random();

            for (int i = 350; i <= 750; i++)
                observablePoint.Add(new ObservablePoint(i * 1, 10+i));
            _lineSeries.ScalesXAt = 0;
            _lineSeries.ScalesYAt = 0;

            _series = new ISeries[] { _lineSeries };
        }
        private LineSeries<ObservablePoint> _lineSeries;

        private ISeries[] _series;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Chart1.Series = _series;
            Chart1.ZoomingSpeed = 0;
        }
    }
}
