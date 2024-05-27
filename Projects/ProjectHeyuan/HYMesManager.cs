using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Text;
using System.Windows;

namespace ColorVision.Projects
{
    public class HYMesConfig: ViewModelBase, IConfig
    {
        public static HYMesConfig Instance => ConfigHandler.GetInstance().GetRequiredService<HYMesConfig>();

        public bool IsOpenConnect { get => _IsOpenConnect;set { _IsOpenConnect = value; NotifyPropertyChanged(); } }
        private bool _IsOpenConnect;


        public int DeviceId { get => _DeviceId; set { _DeviceId = value; NotifyPropertyChanged(); } }
        private int _DeviceId;

        public string PortName { get => _PortName; set { _PortName = value; NotifyPropertyChanged(); } }
        private string _PortName;

        public string TestName { get => _TestName; set { _TestName = value; NotifyPropertyChanged(); } }
        private string _TestName = "WBROtest";

        public string DataPath { get => _DataPath; set { _DataPath = value; NotifyPropertyChanged(); } }
        private string _DataPath;

        public bool IsAutoUploadSn { get => _IsAutoUploadSn; set { _IsAutoUploadSn = value; NotifyPropertyChanged(); } }
        private bool _IsAutoUploadSn;

    }

    public class HYMesManager:ViewModelBase
    {
        private static HYMesManager _instance;
        private static readonly object _locker = new();
        public static HYMesManager GetInstance() 
        {
            
            lock (_locker) {
                if (_instance ==null)
                    _instance = new HYMesManager();
                return _instance;
            } 
        }

        public ObservableCollection<SerialMsg> SerialMsgs { get; set; } = new ObservableCollection<SerialMsg>();

        private SerialPort serialPort { get; set; }

        public static HYMesConfig Config => HYMesConfig.Instance;

        public HYMesManager()
        {
            serialPort = new SerialPort { };

            if (Config.IsOpenConnect)
            {
                OpenPort(Config.PortName);
            }
        }

        public bool IsConnect { get => _IsConnect; set { _IsConnect = value; NotifyPropertyChanged(); } }
        private bool _IsConnect;

        public int OpenPort(string portName)
        {
            try
            {
                if (!serialPort.IsOpen)
                {
                    serialPort = new SerialPort { PortName = portName, BaudRate = 38400 };
                    serialPort.Open();
                    string SetMsg = $"CSN,C,0,TEST202405140001";
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(SetMsg);
                    byte[] framedMsg = new byte[buffer.Length + 2];
                    framedMsg[0] = 0x02; // STX (Start of Text)
                    buffer.CopyTo(framedMsg, 1); // Copy original message into the new array starting at index 1
                    framedMsg[framedMsg.Length - 1] = 0x03; // ETX (End of Text)

                    serialPort.Write(framedMsg, 0, framedMsg.Length);

                    for (int i = 0; i < 10; i++)
                    {
                        Thread.Sleep(16);
                        int bytesread = serialPort.BytesToRead;
                        if (bytesread > 0)
                        {
                            byte[] buff = new byte[bytesread];
                            serialPort.Read(buff, 0, bytesread);
                            if (buff.Length > 3 && buff[0] == 0x02)
                            {
                                IsConnect = true;
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
            if (sender is SerialPort serialPort)
            {
                try
                {
                    int bytesRead = serialPort.BytesToRead;
                    if (bytesRead > 0)
                    {
                        byte[] buffer = new byte[bytesRead];
                        serialPort.Read(buffer, 0, bytesRead);

                        string Msg = Encoding.UTF8.GetString(buffer);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            SerialMsgs.Add(new SerialMsg() { SerialStatus = SerialStatus.Receive, Bytes = buffer });
                        });
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Msg.Contains("CSN,S"))
                            {
                                UploadNG();
                            }
                            if (Msg.Contains("CMI,S"))
                            {
                                SendPost();
                            }
                        });
                    }
                }
                catch 
                {

                }
            }
        }

        public string SN { get => _SN; set {
                if (_SN == value) return;
                _SN = value; 
                NotifyPropertyChanged();
                if (Config.IsAutoUploadSn)
                    UploadSN();
            }
        }
        private string _SN;

        public void UploadSN()
        {
            string SendMsg = $"CSN,C,{Config.DeviceId},{SN}";
            Send(System.Text.Encoding.UTF8.GetBytes(SendMsg));
        }

        public void SendPost()
        {
            string SendMsg = $"CPT,C,{Config.DeviceId}";
            Send(System.Text.Encoding.UTF8.GetBytes(SendMsg));
        }
        public void UploadMes()
        {
            string SendMsg = $"CMI,C,{Config.DeviceId},{Config.TestName},White,0.00/0.00/0.00/result,Blue,0.00/0.00/0.00/result,Red,0.00/0.00/0.00/result,Orange,0.00/0.00/0.00/result";
            Send(System.Text.Encoding.UTF8.GetBytes(SendMsg));
        }
        public void UploadNG() 
        {
            string SendMsg = $"CGI,C,{Config.DeviceId},Default,errorW";
            Send(System.Text.Encoding.UTF8.GetBytes(SendMsg));
        }

        public void Send(byte[] msg)
        {
            byte[] framedMsg = new byte[msg.Length + 2];
            framedMsg[0] = 0x02; // STX (Start of Text)
            msg.CopyTo(framedMsg, 1); // Copy original message into the new array starting at index 1
            framedMsg[framedMsg.Length - 1] = 0x03; // ETX (End of Text)

              SerialMsgs.Add(new SerialMsg() { SerialStatus = SerialStatus.Send, Bytes = framedMsg });
            if (serialPort.IsOpen)
                serialPort.Write(framedMsg, 0, framedMsg.Length);
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
}
