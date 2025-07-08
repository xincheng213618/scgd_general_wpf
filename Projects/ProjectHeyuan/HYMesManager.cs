using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using log4net;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Text;
using System.Windows;

namespace ColorVision.Projects.ProjectHeyuan
{
    public class HYMesConfig: ViewModelBase, IConfig
    {
        public static HYMesConfig Instance => ConfigService.Instance.GetRequiredService<HYMesConfig>();

        public RelayCommand OpenTemplateCommand { get; set; }
        public RelayCommand OpenFlowEngineToolCommand { get; set; }

        public HYMesConfig()
        {
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenFlowEngineToolCommand = new RelayCommand(a => OpenFlowEngineTool());
        }

        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;
        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateFlow(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public void OpenFlowEngineTool()
        {
            new FlowEngineToolWindow(TemplateFlow.Params[TemplateSelectedIndex].Value) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
        public bool IsOpenConnect { get => _IsOpenConnect;set { _IsOpenConnect = value; NotifyPropertyChanged(); } }
        private bool _IsOpenConnect;

        public string PortName { get => _PortName; set { _PortName = value; NotifyPropertyChanged(); } }
        private string _PortName;

        public string FlowName { get => _FlowName; set { _FlowName = value; NotifyPropertyChanged(); } }
        private string _FlowName;

        public int DeviceId { get => _DeviceId; set { _DeviceId = value; NotifyPropertyChanged(); } }
        private int _DeviceId;


        public string TestName { get => _TestName; set { _TestName = value; NotifyPropertyChanged(); } }
        private string _TestName = "WBROtest";

        public string DataPath { get => _DataPath; set { _DataPath = value; NotifyPropertyChanged(); } }
        private string _DataPath;

        public bool IsAutoUploadSn { get => _IsAutoUploadSn; set { _IsAutoUploadSn = value; NotifyPropertyChanged(); } }
        private bool _IsAutoUploadSn;

    }

    public class HYMesManager:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(HYMesManager));

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
            IsConnect = false;
            try
            {
                if (!serialPort.IsOpen)
                {
                    serialPort = new SerialPort { PortName = portName, BaudRate = 38400 };
                    serialPort.Open();
                    string SetMsg = $"CSN,C,0,TEST202405140001";
                    byte[] buffer = Encoding.UTF8.GetBytes(SetMsg);
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
                                string[] parts = Msg.Split(',');
                                CSNResult = parts[^1].Contains('0') || parts[^2].Contains('0');
                                CPTResult = null;
                                CGIResult = null;
                                CMIResult = null;
                            }
                            if (Msg.Contains("CPT,S"))
                            {
                                string[] parts = Msg.Split(',');
                                CPTResult = parts[^1].Contains('0'); 
                            }
                            if (Msg.Contains("CGI,S"))
                            {
                                string[] parts = Msg.Split(',');
                                CGIResult = parts[^1].Contains('0');
                                if (CGIResult == true)
                                {
                                    UploadMes(Results);
                                }
                            }
                            if (Msg.Contains("CMI,S"))
                            {
                                string[] parts = Msg.Split(',');
                                CMIResult = parts[^1].Contains('0');    
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

        public bool? CSNResult { get => _CSNResult; set { _CSNResult = value; NotifyPropertyChanged(); } }
        private bool? _CSNResult;

        public bool? CPTResult { get => _CPTResult; set { _CPTResult = value; NotifyPropertyChanged(); } }
        private bool? _CPTResult;
        public bool? CGIResult { get => _CGIResult; set { _CGIResult = value; NotifyPropertyChanged(); } }
        private bool? _CGIResult;

        public bool? CMIResult { get => _CMIResult; set { _CMIResult = value; NotifyPropertyChanged(); } }
        private bool? _CMIResult;
        public long LastFlowTime { get => _LastFlowTime; set { _LastFlowTime = value; NotifyPropertyChanged(); } }
        private long _LastFlowTime;

        public void UploadSN()
        {
            if (string.IsNullOrWhiteSpace(SN))
            {
                MessageBox.Show("产品编号为空");
                return;
            }
            string SendMsg = $"CSN,C,{Config.DeviceId},{SN}";
            log.Info("UploadSN" + SendMsg);
            Send(Encoding.UTF8.GetBytes(SendMsg));
        }

        public void SendPost()
        {
            string SendMsg = $"CPT,C,{Config.DeviceId}";
            log.Info("SendPost" + SendMsg);
            Send(Encoding.UTF8.GetBytes(SendMsg));
        }

        public ObservableCollection<TempResult> Results { get; set; } =new ObservableCollection<TempResult>();

        public void UploadMes(ObservableCollection<TempResult> Results)
        {
            string SendMsg = $"CMI,C,{Config.DeviceId},{Config.TestName},White,{Results[0].X.Value:F3}/{Results[0].Y.Value:F3}/{Results[0].Lv.Value:F3}/{Results[0].Dw.Value:F3}/{(Results[0].Result?"Pass":"Fail")},Blue,{Results[1].X.Value:F3}/{Results[1].Y.Value:F3}/{Results[1].Lv.Value:F3}/{Results[1].Dw.Value:F3}/{(Results[1].Result ? "Pass" : "Fail")},Red,{Results[2].X.Value:F3}/{Results[2].Y.Value:F3}/{Results[2].Lv.Value:F3}/{Results[2].Dw.Value:F3}/{(Results[2].Result ? "Pass" : "Fail")},Orange,{Results[3].X.Value:F3}/{Results[3].Y.Value:F3}/{Results[3].Lv.Value:F3}/{Results[3].Dw.Value:F3}/{(Results[3].Result ? "Pass" : "Fail")}";
            log.Info("UploadMes" + SendMsg);
            Send(Encoding.UTF8.GetBytes(SendMsg));
        }
        public void UploadNG(string Msg = "errorW") 
        {
            string SendMsg = $"CGI,C,{Config.DeviceId},Default,{Msg}";
            log.Info("UploadNG"+ SendMsg);
            Send(Encoding.UTF8.GetBytes(SendMsg));
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

        public void Close()
        {
            if (serialPort.IsOpen)
                serialPort.Close();
            serialPort.Dispose();
            IsConnect = false;
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
