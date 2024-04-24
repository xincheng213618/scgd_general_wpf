using ColorVision.Common.MVVM;
using ColorVision.Common.Sorts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
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

    public class HYMesManager
    {
        private static HYMesManager _Instance;
        private static readonly object locker = new object();
        public static HYMesManager GetInstance()
        {
            lock (locker) { return _Instance ?? new HYMesManager(); }
        }
        
        private SerialPort serialPort { get; set; }

        public HYMesManager()
        {
            serialPort = new SerialPort { };
        }

        public int OpenPort(string portName)
        {
            try
            {
                if (!serialPort.IsOpen)
                {
                    serialPort = new SerialPort { PortName = portName, BaudRate = 115200 };
                    serialPort.Open();
                    string SetMsg = $"/CSN,C,0,TEST202303030002";

                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(SetMsg);
                    serialPort.Write(buffer, 0, buffer.Length);

                    for (int i = 0; i < 10; i++)
                    {
                        Thread.Sleep(16);
                        int bytesread = serialPort.BytesToRead;
                        if (bytesread > 0)
                        {
                            byte[] buff = new byte[bytesread];
                            serialPort.Read(buff, 0, bytesread);
                            if (buff.Length == 8 && buff[3] == 64)
                            {
                                serialPort.DataReceived += SerialPort_DataReceived;
                                return 0;
                            }
                        }
                    }
                    serialPort.Close();
                    return -1;
                }
                else
                {
                    return 0;
                }
            }
            catch
            {
                return -2;
            }


        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            

        }
        public void SendSn(string ch,string sn)
        {
            string SendMsg = $"CSN,C,{ch},{sn}";

            Send(System.Text.Encoding.UTF8.GetBytes(SendMsg));
        }

        public void Send(byte[] msg)
        {
            byte[] framedMsg = new byte[msg.Length + 2];
            framedMsg[0] = 0x02; // STX (Start of Text)
            msg.CopyTo(framedMsg, 1); // Copy original message into the new array starting at index 1
            framedMsg[framedMsg.Length - 1] = 0x03; // ETX (End of Text)

            string hex = BitConverter.ToString(framedMsg).Replace("-", " ");
            if (serialPort.IsOpen)
                serialPort.Write(msg, 0, msg.Length);
        }



        public int Initialized()
        {
            string[] TempPortNames = SerialPort.GetPortNames();
            //这种写法不允许有多个串口；
            for (int i = 0; i < TempPortNames.Length; i++)
            {
                if (OpenPort(TempPortNames[i]) == 0)
                {
                    return 0;
                }
            }
            return -1;
        }

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


            HYMesManager.GetInstance().SendSn("0","2222");

        }


    }
}
