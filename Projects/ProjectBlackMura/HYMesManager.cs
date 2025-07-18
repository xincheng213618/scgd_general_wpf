using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using log4net;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace ProjectBlackMura
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
        public bool IsOpenConnect { get => _IsOpenConnect; set { _IsOpenConnect = value; NotifyPropertyChanged(); } }
        private bool _IsOpenConnect;

        public string PortName { get => _PortName; set { _PortName = value; NotifyPropertyChanged(); } }
        private string _PortName;

        public int DeviceId { get => _DeviceId; set { _DeviceId = value; NotifyPropertyChanged(); } }
        private int _DeviceId;

        [DisplayName("单独发送MES指令")]
        public bool IsSingleMes { get => _IsSingleMes; set { _IsSingleMes = value; NotifyPropertyChanged(); } }
        private bool _IsSingleMes;

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

        private SerialPort serialPort { get; set; }

        public static HYMesConfig Config => HYMesConfig.Instance;

        public HYMesManager()
        {
            serialPort = new SerialPort { };
            if (HYMesConfig.Instance.IsOpenConnect)
            {
                Task.Run(Initialized);
            }
        }

        public bool IsConnect { get => _IsConnect; set { _IsConnect = value; NotifyPropertyChanged(); } }
        private bool _IsConnect;

        public async Task<int> OpenPortAsync(string portName)
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
                        await Task.Delay(16);
                        int bytesread = serialPort.BytesToRead;
                        if (bytesread > 0)
                        {
                            byte[] buff = new byte[bytesread];
                            serialPort.Read(buff, 0, bytesread);
                            if (buff.Length > 3 && buff[0] == 0x02)
                            {
                                IsConnect = true;
                                log.Info("serialPort Connect");
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
        private static readonly object _dbLock = new object();

        private List<byte> receiveBuffer = new List<byte>();
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (sender is SerialPort serialPort)
            {
                int bytesRead = serialPort.BytesToRead;
                if (bytesRead > 0)
                {
                    byte[] buffer = new byte[bytesRead];
                    serialPort.Read(buffer, 0, bytesRead);
                    receiveBuffer.AddRange(buffer);
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        // 查找完整报文（以 0x02 开头，0x03 结尾）
                        int stxIndex = receiveBuffer.IndexOf(0x02);
                        int etxIndex = receiveBuffer.IndexOf(0x03, stxIndex + 1);

                        if (stxIndex >= 0 && etxIndex > stxIndex)
                        {
                            // 提取完整数据（不含 STX/ETX）
                            int dataLen = etxIndex - stxIndex - 1;
                            if (dataLen > 0)
                            {
                                byte[] msgBytes = receiveBuffer.Skip(stxIndex + 1).Take(dataLen).ToArray();
                                string msg = Encoding.UTF8.GetString(msgBytes);
                                log.Info("正在处理：" + msg);
                                ProcessSerialLine(msg);
                            }
                            // 移除已处理数据
                            receiveBuffer.Clear();
                        }
                        else
                        {
                            log.Info("没有完整的数据" + Encoding.UTF8.GetString(buffer));
                        }

                    });
                }
            }

        }

        private void ProcessSerialLine(string Msg)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (Msg.Contains("CCPI,S"))
                {
                    string[] parts = Msg.Split(',');
                    bool result = parts[^1].Contains('0');
                    log.Info($"CCPIResult:{result}");
                    CCPIResult = result;
                }
                if (Msg.Contains("CON,S"))
                {
                    string[] parts = Msg.Split(',');
                    bool result = parts[^1].Contains('0');
                    log.Info($"CONResult:{result}");
                    CONResult = result;
                }
                if (Msg.Contains("COFF,S"))
                {
                    string[] parts = Msg.Split(',');
                    bool result = parts[^1].Contains('0');
                    log.Info($"COFFResult:{result}");
                    COFFResult = result;
                }

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
                        //UploadMes(Results);
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

        public bool? CSNResult { get => _CSNResult; set { _CSNResult = value; NotifyPropertyChanged(); } }
        private bool? _CSNResult;


        /// <summary>
        /// 上电
        /// </summary>
        public event EventHandler<bool> CONCompleted;
        /// <summary>
        /// 上电结果
        /// </summary>
        public bool? CONResult { get => _CONResult; set { _CONResult = value; NotifyPropertyChanged(); CONCompleted?.Invoke(this, value == true); } } 
        private bool? _CONResult;

        /// <summary>
        /// 下电
        /// </summary>
        public event EventHandler<bool> COFFCompleted;
        /// <summary>
        /// 上电结果
        /// </summary>
        public bool? COFFResult { get => _COFFResult; set { _COFFResult = value; NotifyPropertyChanged(); COFFCompleted?.Invoke(this, value == true); } }
        private bool? _COFFResult;


        /// <summary>
        /// 切图
        /// </summary>
        public event EventHandler<bool> CCPICompleted;
        /// <summary>
        /// 切图结果
        /// </summary>
        public bool? CCPIResult { get => _CCPIResult; set { _CCPIResult = value; NotifyPropertyChanged(); CCPICompleted?.Invoke(this,value==true); } }
        private bool? _CCPIResult;
        

        public bool? CPTResult { get => _CPTResult; set { _CPTResult = value; NotifyPropertyChanged(); } }
        private bool? _CPTResult;
        public bool? CGIResult { get => _CGIResult; set { _CGIResult = value; NotifyPropertyChanged(); } }
        private bool? _CGIResult;

        public bool? CMIResult { get => _CMIResult; set { _CMIResult = value; NotifyPropertyChanged(); } }
        private bool? _CMIResult;
        public long LastFlowTime { get => _LastFlowTime; set { _LastFlowTime = value; NotifyPropertyChanged(); } }
        private long _LastFlowTime;

        public async Task PGPowerOn()
        {
            string SendMsg = $"CON,C,{Config.DeviceId}";
            log.Info("PG上电" + SendMsg);
            Send(Encoding.UTF8.GetBytes(SendMsg));
            for (int i = 0; i < 1000; i++)
            {
                await Task.Delay(16);
                int bytesread = serialPort.BytesToRead;
                if (bytesread > 0)
                {
                    byte[] buff = new byte[bytesread];
                    serialPort.Read(buff, 0, bytesread);
                    string Msg = Encoding.UTF8.GetString(buff);
                    if (Msg.Contains("CON,S"))
                    {
                        string[] parts = Msg.Split(',');
                        bool result = parts[^1].Contains('0');
                        log.Info($"CONResult:{result}");
                        CONResult = result;
                        return;
                    }
                    else
                    {
                        log.Info(Msg);
                    }
                }
            }
            log.Info("超时判定");
        }
        public async Task PGPowerOff()
        {
            string SendMsg = $"COFF,C,{Config.DeviceId}";
            log.Info("PG下电" + SendMsg);
            Send(Encoding.UTF8.GetBytes(SendMsg));
            for (int i = 0; i < 50; i++)
            {
                await Task.Delay(16);
                int bytesread = serialPort.BytesToRead;
                if (bytesread > 0)
                {
                    byte[] buff = new byte[bytesread];
                    serialPort.Read(buff, 0, bytesread);
                    string Msg = Encoding.UTF8.GetString(buff);
                    if (Msg.Contains("COFF,S"))
                    {
                        string[] parts = Msg.Split(',');
                        bool result = parts[^1].Contains('0');
                        log.Info($"COFFResult:{result}");
                        COFFResult = result;
                    }
                    break;
                }
            }
        }
        public async Task PGSwitch(int id)
        {
            string SendMsg = $"CCPI,C,{Config.DeviceId},{id}";
            log.Info("PG切图" + SendMsg);
            Send(Encoding.UTF8.GetBytes(SendMsg));
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(16);
                int bytesread = serialPort.BytesToRead;
                if (bytesread > 0)
                {
                    byte[] buff = new byte[bytesread];
                    serialPort.Read(buff, 0, bytesread);
                    string Msg = Encoding.UTF8.GetString(buff);
                    string[] parts = Msg.Split(',');
                    bool result = parts[^1].Contains('0');
                    log.Info($"CCPIResult:{result}");
                    CCPIResult = result;
                    break;
                }
            }
        }


        public void UploadSN(string sn)
        {
            if (string.IsNullOrWhiteSpace(sn))
            {
                MessageBox.Show("产品编号为空");
                return;
            }
            string SendMsg = $"CSN,C,{Config.DeviceId},{sn}";
            log.Info("UploadSN" + SendMsg);
            Send(Encoding.UTF8.GetBytes(SendMsg));
        }

        public void SendPost()
        {
            string SendMsg = $"CPT,C,{Config.DeviceId}";
            log.Info("SendPost" + SendMsg);
            Send(Encoding.UTF8.GetBytes(SendMsg));
        }

        //public ObservableCollection<TempResult> Results { get; set; } =new ObservableCollection<TempResult>();

        //public void UploadMes(ObservableCollection<TempResult> Results)
        //{
        //    string SendMsg = $"CMI,C,{Config.DeviceId},{Config.TestName},White,{Results[0].X.Value:F3}/{Results[0].Y.Value:F3}/{Results[0].Lv.Value:F3}/{Results[0].Dw.Value:F3}/{(Results[0].Result?"Pass":"Fail")},Blue,{Results[1].X.Value:F3}/{Results[1].Y.Value:F3}/{Results[1].Lv.Value:F3}/{Results[1].Dw.Value:F3}/{(Results[1].Result ? "Pass" : "Fail")},Red,{Results[2].X.Value:F3}/{Results[2].Y.Value:F3}/{Results[2].Lv.Value:F3}/{Results[2].Dw.Value:F3}/{(Results[2].Result ? "Pass" : "Fail")},Orange,{Results[3].X.Value:F3}/{Results[3].Y.Value:F3}/{Results[3].Lv.Value:F3}/{Results[3].Dw.Value:F3}/{(Results[3].Result ? "Pass" : "Fail")}";
        //    log.Info("UploadMes" + SendMsg);
        //    Send(Encoding.UTF8.GetBytes(SendMsg));
        //}
        public void UploadNG(string Msg = "errorW") 
        {
            string SendMsg = $"CGI,C,{Config.DeviceId},Default,{Msg}";
            log.Info("UploadNG"+ SendMsg);
            Send(Encoding.UTF8.GetBytes(SendMsg));
        }

        public void Send(byte[] msg)
        {
            if (!serialPort.IsOpen)
            {
                log.Info("serialPort Is Not Open"); 
                return;
            }

            byte[] framedMsg = new byte[msg.Length + 2];
            framedMsg[0] = 0x02; // STX (Start of Text)
            msg.CopyTo(framedMsg, 1); // Copy original message into the new array starting at index 1
            framedMsg[framedMsg.Length - 1] = 0x03; // ETX (End of Text)

            serialPort.Write(framedMsg, 0, framedMsg.Length);
        }

        public void Close()
        {
            if (serialPort.IsOpen)
                serialPort.Close();
            serialPort.DataReceived -= SerialPort_DataReceived;
            serialPort.Dispose();
            IsConnect = false;
        }


        public async Task<int> Initialized()
        {
            string[] TempPortNames = SerialPort.GetPortNames();
            //这种写法不允许有多个串口；
            for (int i = 0; i < TempPortNames.Length; i++)
            {
                if (await OpenPortAsync(TempPortNames[i]) == 0)
                {
                    return 0;
                }
            }
            return -1;
        }

    }
}
