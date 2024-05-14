using ColorVision.UI.HotKey;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using ColorVision.Update;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static NPOI.HSSF.Util.HSSFColor;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;

namespace ColorVision.Projects
{

    public class ProjectHeyuanExport : IMenuItem
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "HeYuan";

        public int Order => 100;

        public string? Header => "河源精电";

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(A => Execute());

        private void Execute()
        {
            new ProjectHeyuan() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }


    /// <summary>
    /// ProjectHeyuan.xaml 的交互逻辑
    /// </summary>
    public partial class ProjectHeyuan : Window
    {
        public ProjectHeyuan()
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

            ListViewMes.ItemsSource = HYMesManager.GetInstance().SerialMsgs;
        }


        bool result =true;
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            result = !result;
            ResultText.Text = result ? "OK" : "不合格";
            ResultText.Foreground = result ? Brushes.Blue : Brushes.Red;


            HYMesManager.GetInstance().SendSn("0","2222");

        }


    }
}
