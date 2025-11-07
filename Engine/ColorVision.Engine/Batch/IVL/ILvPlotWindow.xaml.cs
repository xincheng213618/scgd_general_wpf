using Microsoft.Win32;
using ScottPlot;
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

namespace ColorVision.Engine.Batch.IVL
{
    /// <summary>
    /// ILvPlotWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ILvPlotWindow : Window
    {
        public ILvPlotWindow()
        {
            InitializeComponent();
            InitPlot();
        }
        private void InitPlot()
        {
            var plt = Plot.Plot;
            plt.Clear();
            plt.Title("I–L");
            plt.Axes.Bottom.Label.Text = "Current (mA)";
            plt.Axes.Left.Label.Text = "Lv (cd/m²)";
            plt.ShowGrid();
            Plot.Refresh();
        }

        private void BtnOpenAndPlot_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = "选择由 IVL 导出的 CSV（Camera_IVL_*.csv 或 SP_IVL_*.csv）"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var (currents, lvs) = ILvCsvParser.ParseCurrentLvFromCsv(dlg.FileName);

                if (currents.Count == 0 || lvs.Count == 0 || currents.Count != lvs.Count)
                {
                    TxtStatus.Text = "未解析到有效的 Current 与 Lv。";
                    return;
                }

                PlotIL(currents, lvs);
                TxtStatus.Text = $"已绘图：{currents.Count} 点 —— {System.IO.Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"解析/绘图失败：{ex.Message}";
            }
        }

        private void PlotIL(IReadOnlyList<double> currents, IReadOnlyList<double> lvs)
        {
            var plt = Plot.Plot;
            plt.Clear();

            var zipped = currents.Zip(lvs, (i, lv) => new { i, lv })
                                 .Where(p => !double.IsNaN(p.i) && !double.IsNaN(p.lv)
                                          && !double.IsInfinity(p.i) && !double.IsInfinity(p.lv))
                                 .OrderBy(p => p.i)
                                 .ToList();

            if (zipped.Count == 0)
                throw new InvalidOperationException("全部数据为 NaN 或 Infinity。");

            double[] xsSorted = zipped.Select(p => p.i).ToArray();
            double[] ysSorted = zipped.Select(p => p.lv).ToArray();

            // 折线（趋势），只显示线，不显示点
            var lineScatter = plt.Add.Scatter(xsSorted, ysSorted);
            lineScatter.MarkerSize = 0;     // 不显示标记
            lineScatter.LineWidth = 2;
            lineScatter.Color = ScottPlot.Colors.Blue; // 可自行调整

            // 散点（原始顺序），只显示点不显示线
            var pointScatter = plt.Add.Scatter(currents.ToArray(), lvs.ToArray());
            pointScatter.LineWidth = 0;      // 不显示连接线
            pointScatter.MarkerSize = 6;
            pointScatter.MarkerShape = MarkerShape.FilledCircle;
            pointScatter.Color = ScottPlot.Colors.Black;

            plt.Title("I–L");
            plt.Axes.Bottom.Label.Text = "Current (mA)";
            plt.Axes.Left.Label.Text = "Lv (cd/m²)";
            plt.ShowGrid();
            plt.Axes.AutoScale();

            Plot.Refresh();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            InitPlot();
            TxtStatus.Text = string.Empty;
        }
    }
}
