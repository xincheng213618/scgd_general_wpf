using ColorVision.UI.Menus;
using ScottPlot;
using Spectrum.Menus;
using System.Windows;

namespace Spectrum.Calibration
{
    public class MenuGenerateAmplitudeWindow : SpectrumMenuIBase
    {

        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => "生成幅值标定文件";
        public override int Order => 1;
        public override void Execute()
        {
            new GenerateAmplitudeWindow().ShowDialog();
        }
    }


    public partial class GenerateAmplitudeWindow : Window
    {
        private SpectrometerManager Manager => SpectrometerManager.Instance;

        public GenerateAmplitudeWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Manager;
            InitializeChart();
            RefreshChart();

            Manager.DataAcquired += OnDataAcquired;
        }

        private void InitializeChart()
        {
            string title = "暗数据 / 亮数据 预览";
            AmplitudePlot.Plot.Title(title);
            AmplitudePlot.Plot.XLabel("像素点");
            AmplitudePlot.Plot.YLabel("强度");

            string fontSample = "暗数据 / 亮数据 预览";
            AmplitudePlot.Plot.Axes.Title.Label.FontName = Fonts.Detect(fontSample);
            AmplitudePlot.Plot.Axes.Left.Label.FontName = Fonts.Detect(fontSample);
            AmplitudePlot.Plot.Axes.Bottom.Label.FontName = Fonts.Detect(fontSample);
        }

        private void RefreshChart()
        {
            AmplitudePlot.Plot.Clear();

            double[] xs = new double[Manager.fDarkData.Length];
            for (int i = 0; i < xs.Length; i++)
                xs[i] = i;

            // Plot dark data
            double[] darkYs = new double[Manager.fDarkData.Length];
            bool hasDark = false;
            for (int i = 0; i < Manager.fDarkData.Length; i++)
            {
                darkYs[i] = Manager.fDarkData[i];
                if (Manager.fDarkData[i] != 0) hasDark = true;
            }
            if (hasDark)
            {
                var darkPlot = AmplitudePlot.Plot.Add.Scatter(xs, darkYs);
                darkPlot.Label = "暗数据";
                darkPlot.Color = ScottPlot.Color.FromColor(System.Drawing.Color.DodgerBlue);
                darkPlot.LineWidth = 1;
                darkPlot.MarkerSize = 0;
            }

            // Plot light data
            double[] lightYs = new double[Manager.fLightData.Length];
            bool hasLight = false;
            for (int i = 0; i < Manager.fLightData.Length; i++)
            {
                lightYs[i] = Manager.fLightData[i];
                if (Manager.fLightData[i] != 0) hasLight = true;
            }
            if (hasLight)
            {
                var lightPlot = AmplitudePlot.Plot.Add.Scatter(xs, lightYs);
                lightPlot.Label = "亮数据";
                lightPlot.Color = ScottPlot.Color.FromColor(System.Drawing.Color.OrangeRed);
                lightPlot.LineWidth = 1;
                lightPlot.MarkerSize = 0;
            }

            AmplitudePlot.Plot.ShowLegend();
            AmplitudePlot.Plot.Axes.AutoScale();
            AmplitudePlot.Refresh();

            // Update status
            string darkStatus = hasDark ? "✓" : "✗";
            string lightStatus = hasLight ? "✓" : "✗";
            StatusText.Text = $"暗数据: {darkStatus}  |  亮数据: {lightStatus}";
        }

        private void OnDataAcquired(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() => RefreshChart());
        }

        protected override void OnClosed(EventArgs e)
        {
            Manager.DataAcquired -= OnDataAcquired;
            base.OnClosed(e);
        }
    }
}
