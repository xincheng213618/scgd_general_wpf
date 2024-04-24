using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
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
using static NPOI.HSSF.Util.HSSFColor;

namespace ColorVision
{
    public class NumSet :ViewModelBase
    {
        public double White { get => _White; set { _White = value; NotifyPropertyChanged(); } }
        private double _White;

        public double Blue { get => _Blue; set { _Blue = value; NotifyPropertyChanged(); } }
        private double _Blue;

        public double Red { get => _Red; set { _Red = value; NotifyPropertyChanged(); } }
        private double _Red;
        public double Orange { get => _Orange; set { _Orange = value; NotifyPropertyChanged(); } }
        private double _Orange;
    }

    public class TempResult : ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        public NumSet NumSet { get; set; } = new NumSet();
    }


    /// <summary>
    /// ProjectHeiyuan.xaml 的交互逻辑
    /// </summary>
    public partial class ProjectHeiyuan : Window
    {
        public ProjectHeiyuan()
        {
            InitializeComponent();
        }

        public ObservableCollection<TempResult> Settings { get; set; } = new ObservableCollection<TempResult>();
        public ObservableCollection<TempResult> Results { get; set; } = new ObservableCollection<TempResult>();

        private void Window_Initialized(object sender, EventArgs e)
        {
            Settings.Add(new TempResult() { Name = "x(上限)"});
            Settings.Add(new TempResult() { Name = "x(下限)" });
            Settings.Add(new TempResult() { Name = "y(上限)" });
            Settings.Add(new TempResult() { Name = "y(下限)" });
            Settings.Add(new TempResult() { Name = "lv(上限)" });
            Settings.Add(new TempResult() { Name = "lv(下限)" });
            ListViewSetting.ItemsSource = Settings;
            Results.Add(new TempResult() { Name = "x" });
            Results.Add(new TempResult() { Name = "y" });
            Results.Add(new TempResult() { Name = "z" });
            ListViewResult.ItemsSource = Results;

            ComboBoxSer.ItemsSource = SerialPort.GetPortNames();
            ComboBoxSer.SelectedIndex = 0;
        }


        bool result =true;
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            result = !result;
            ResultText.Text = result ? "OK" : "不合格";
            ResultText.Foreground = result ? Brushes.Blue : Brushes.Red;
        }


    }
}
