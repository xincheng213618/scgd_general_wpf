using System;
using System.Collections.Generic;
using System.Windows;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Defaults;

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
