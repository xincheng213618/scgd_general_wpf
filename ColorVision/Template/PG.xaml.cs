using ColorVision.MVVM;
using Newtonsoft.Json;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ColorVision.Template
{
    /// <summary>
    /// PG.xaml 的交互逻辑
    /// </summary>
    public partial class PG : UserControl
    {
        public PGParam PGParam { get; set; }
        public PG()
        {
            InitializeComponent();
            this.PGParam = new PGParam();
            this.DataContext = PGParam;
        }
        public PG(PGParam pGParam)
        {
            InitializeComponent();
            this.PGParam = pGParam;
            this.DataContext = PGParam;
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            PGParam.PGtype = 1;
        }

        private void RadioButton_Checked1(object sender, RoutedEventArgs e)
        {
            PGParam.PGtype = 2;
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NativeMethods.Keyboard.PressKey(0x09);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }
    }

#pragma warning disable CA1507
    public class PGParam : ParamBase
    {
        //PG的类型-1为默认的TCPIP，2为广林达的dllPG
        [JsonProperty("PGtype")]
        public int PGtype { get => _PGtype; set { _PGtype = value; NotifyPropertyChanged(); } }
        private int _PGtype;

        //PG的画面切换指令
        [JsonProperty("PGPattenChangeComm1")]
        public string PGPattenChangeComm1 { get => _PGPattenChangeComm1; set { _PGPattenChangeComm1 = value; NotifyPropertyChanged(); } }
        private string _PGPattenChangeComm1;

        //PG的画面切换指令
        [JsonProperty("PGPattenChangeComm2")]
        public string PGPattenChangeComm2 { get => _PGPattenChangeComm2; set { _PGPattenChangeComm2 = value; NotifyPropertyChanged(); } }
        private string _PGPattenChangeComm2;

        //PG的画面切换指令
        [JsonProperty("PGPattenChangeComm3")]
        public string PGPattenChangeComm3 { get => _PGPattenChangeComm3; set { _PGPattenChangeComm3 = value; NotifyPropertyChanged(); } }
        private string _PGPattenChangeComm3;
        //PG的画面切换指令
        [JsonProperty("PGPattenChangeComm4")]
        public string PGPattenChangeComm4 { get => _PGPattenChangeComm4; set { _PGPattenChangeComm4 = value; NotifyPropertyChanged(); } }
        private string _PGPattenChangeComm4;

        //PG的画面切换指令
        [JsonProperty("PGPattenChangeComm5")]
        public string PGPattenChangeComm5 { get => _PGPattenChangeComm5; set { _PGPattenChangeComm5 = value; NotifyPropertyChanged(); } }
        private string _PGPattenChangeComm5;
    }
    #pragma warning restore CA1507


}
