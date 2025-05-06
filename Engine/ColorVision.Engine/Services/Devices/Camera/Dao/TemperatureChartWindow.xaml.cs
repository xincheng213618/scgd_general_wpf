using ColorVision.Engine.Services.Dao;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Engine.Services.Devices.Camera.Dao
{
    /// <summary>
    /// TemperatureChart.xaml 的交互逻辑
    /// </summary>
    public partial class TemperatureChartWindow : Window
    {
        public TemperatureChartWindow(List<CameraTempModel> data)
        {
            this.data = data;
            InitializeComponent();
        }
        public List<CameraTempModel> data { get; set; } = new List<CameraTempModel>();

        private void Window_Initialized(object sender, EventArgs e)
        {
            if (data!=null && data.Count > 0)
            {
                // 设置窗口标题
                this.Title = $"温度曲线图 - {data.First().CreateDate?.ToString("yyyy-MM-dd")}";
                TempatureText.Text = $"{data.Last()?.CreateDate}  {data.Last()?.TempValue} °C";
            }
            else
            {
                this.Title = "温度曲线图";
            }
            // 创建数据序列
            var temperatureSeries = new LineSeries<float>
            {
                Values = data.Select(d => d.TempValue ?? 0).ToArray(),
                Name = "Temperature"
                
            };
            temperatureSeries.GeometrySize = 1;
            // 设置图表数据
            TemperatureChart.Series = new ISeries[] { temperatureSeries };

            // 配置轴
            TemperatureChart.XAxes = new[]
            {
                new Axis
                {
                    Labels = data.Select(d => d.CreateDate?.ToString("HH:mm") ?? "").ToArray(),
                    Name = "时间"
                }
            };

            TemperatureChart.YAxes = new[]
            {
                new Axis
                {
                    Name = "温度 (°C)",
                    MaxLimit =70,
                    MinLimit =10   
                }
            };
        }

        private void ButtonExport_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
