using cvColorVision;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static cvColorVision.GCSDLL;

namespace ColorVision.Device.Spectrum
{
    /// <summary>
    /// SpectrumDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class SpectrumDisplayLocal : UserControl
    {
        public DeviceSpectrum DeviceSpectrum { get; set; }
        public SpectrumService SpectrumService { get => DeviceSpectrum.SpectrumService; }

        public SpectrumView View { get => DeviceSpectrum.ChartView;}
        public SpectrumDisplayLocal(DeviceSpectrum DeviceSpectrum)
        {
            this.DeviceSpectrum = DeviceSpectrum;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = SpectrumService;
            ViewGridManager.GetInstance().AddView(View);

            ViewGridManager.GetInstance().ViewMaxChangedEvent += (e) =>
            {
                List<KeyValuePair<string, int>> KeyValues = new List<KeyValuePair<string, int>>();
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowSingle, -2));
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowHidden, -1));
                for (int i = 0; i < e; i++)
                {
                    KeyValues.Add(new KeyValuePair<string, int>((i + 1).ToString(), i));
                }
                ComboxView.ItemsSource = KeyValues;
                ComboxView.SelectedValue = View.View.ViewIndex;
            };
            View.View.ViewIndexChangedEvent += (e1, e2) =>
            {
                ComboxView.SelectedIndex = e2 + 2;
            };
            ComboxView.SelectionChanged += (s, e) =>
            {
                if (ComboxView.SelectedItem is KeyValuePair<string, int> KeyValue)
                {
                    View.View.ViewIndex = KeyValue.Value;
                    ViewGridManager.GetInstance().SetViewIndex(View, KeyValue.Value);
                }
            };
        }


        private ColorParamReturn SpectrumData;

        private bool ConnectSpectrum()
        {
            if (GCSDLL.JKStartServer() != 0)
            {
                MessageBox.Show("启动光谱仪软件的服务失败");
                return false;
            }
            if (GCSDLL.CVInit() != 0)
            {
                MessageBox.Show("连接光谱仪失败");
                return false;
            }
            SpectrumData += TestResult;
            return true;
        }
        private static bool DisconnectSpectrum()
        {
            if (GCSDLL.JKEmissionClose() != 0)
            {
                MessageBox.Show("断开光谱仪失败");
                return false;
            }
            if (GCSDLL.JKCloseServer() != 0)
            {
                MessageBox.Show("断开光谱仪软件的服务失败");
                return false;
            }
            return true;
        }



        private async void SpectrumIni(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.Content.ToString() == "打开")
                {
                    if (await Task.Run(() => ConnectSpectrum()))
                        button.Content = "断开光谱仪";
                }
                else
                {
                    if (await Task.Run(() => DisconnectSpectrum()))
                        button.Content = "打开";
                }
            }
        }
        private void Spectrum0(object sender, RoutedEventArgs e)
        {
            float fIntTime = 100;
            int iAveNum = 1;
            if (GCSDLL.CVInitDark(fIntTime, iAveNum) == 0)
                MessageBox.Show("校零成功");
            else
                MessageBox.Show("校零失败");
        }
        private void SpectrumSingleTest(object sender, RoutedEventArgs e)
        {
            GCSDLL.CVOneTest(SpectrumData, (float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, false, false);
        }

        public void TestResult(ref ColorParam data, float intTime, int resultCode)
        {
            if (resultCode == 0)
            {
                View.SpectrumDrawPlot(new SpectumData(-1,data));
            }
            else
            {
                MessageBox.Show("测量失败");
            }
        }

        private void Button_Click_AutoTest(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_Init_Dark(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_GetParam(object sender, RoutedEventArgs e)
        {

        }
    }
}
