using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.SFR2
{
    public partial class Sfr2PlotWindow : Window
    {

        public Sfr2PlotWindow(string path)
        {
            InitializeComponent();
            LoadFromFile(path);
        }

        public void LoadFromFile(string path)
        {
            if (!File.Exists(path))
            {
                MessageBox.Show($"文件不存在: {path}");
                return;
            }

            var json = File.ReadAllText(path);
            var data = JsonConvert.DeserializeObject<SFR2ResultFile>(json);
            if (data?.result == null || data.result.Count == 0)
            {
                MessageBox.Show("结果为空或格式不匹配");
                return;
            }

            Plot.SetData(data);
            PoiList.ItemsSource = Plot.GetPoiNames().ToList();
            if (PoiList.Items.Count > 0)
                PoiList.SelectedIndex = 0;
        }

        private void PoiList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PoiList.SelectedItem is string name)
                Plot.SelectPoi(name);
        }

        private void ChkAll_Checked(object sender, RoutedEventArgs e) => Plot.SetShowAll(true);
        private void ChkAll_Unchecked(object sender, RoutedEventArgs e) => Plot.SetShowAll(false);

        private void CmbShowType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Plot == null) return;
            if (!(CmbShowType.SelectedItem is ComboBoxItem item)) return;
            string text = item.Content?.ToString();
            var t = text switch
            {
                "All" => Sfr2ShowType.All,
                "Left" => Sfr2ShowType.Left,
                "Top" => Sfr2ShowType.Top,
                "Right" => Sfr2ShowType.Right,
                "Bottom" => Sfr2ShowType.Bottom,
                "HorAverage" => Sfr2ShowType.HorAverage,
                "VerAverage" => Sfr2ShowType.VerAverage,
                _ => Sfr2ShowType.All
            };
            Plot.SetShowType(t);
        }

        private void BtnMtfAtFreq_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtFreq.Text, out var f))
            {
                TxtResult.Text = "频率输入错误";
                return;
            }
            var series = TxtSeries.Text?.Trim();
            if (string.IsNullOrEmpty(series))
            {
                TxtResult.Text = "请填写曲线名（如：Point_1-Left 或 Point_1-horAverage）";
                return;
            }

            if (Plot.TryEvaluateMtfAtFrequency(series, f, out var mtf))
                TxtResult.Text = $"{series}: MTF({f:F4}) = {mtf:F5}";
            else
                TxtResult.Text = "未找到曲线或计算失败";
        }

        private void BtnFreqAtMtf_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtMtf.Text, out var m))
            {
                TxtResult.Text = "MTF输入错误";
                return;
            }
            var series = TxtSeries.Text?.Trim();
            if (string.IsNullOrEmpty(series))
            {
                TxtResult.Text = "请填写曲线名（如：Point_1-Left 或 Point_1-horAverage）";
                return;
            }

            if (Plot.TryEvaluateFrequencyAtMtf(series, m, out var freq))
                TxtResult.Text = $"{series}: Freq(MTF={m:F4}) = {freq:F5}";
            else
                TxtResult.Text = "未找到曲线或计算失败";
        }
    }
}