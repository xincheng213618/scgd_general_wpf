using ColorVision.Engine.Services.Dao;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
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

            LiveCharts.Configure(config => config.HasGlobalSKTypeface(SKFontManager.Default.MatchCharacter('汉')));
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
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                Title = "Save as CSV",
                FileName =  DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv" // 设置默认文件名
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var filePath = saveFileDialog.FileName;
                var csv = new StringBuilder();

                // 添加CSV文件头
                csv.AppendLine("TempValue,PwmValue,CreateDate");

                // 添加数据行
                foreach (var item in data)
                {
                    var line = $"{item.TempValue},{item.PwmValue},{item.CreateDate?.ToString("yyyy-MM-dd HH:mm:ss")},{item.RescourceId}";
                    csv.AppendLine(line);
                }

                // 写入文件
                File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
            }
        }
    }
}
