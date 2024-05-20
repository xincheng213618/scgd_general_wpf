using ColorVision.Common.MVVM;
using ColorVision.UI;
using FlowEngineLib;
using Org.BouncyCastle.Utilities;
using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Text;
using System.Text.Unicode;
using System.Threading;
using System.Windows;

namespace ColorVision.Projects
{
    public class HYMesConfig: ViewModelBase, IConfig
    {
        public static HYMesConfig Instance => ConfigHandler.GetInstance().GetRequiredService<HYMesConfig>();

        public bool IsOpenConnect { get => _IsOpenConnect;set { _IsOpenConnect = value; NotifyPropertyChanged(); } }
        private bool _IsOpenConnect;



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

        public HYMesManager()
        {
            serialPort = new SerialPort { };
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
                catch (Exception ex)
                {

                }
            }
        }

        public int DeviceId { get=> _DeviceId; set{ _DeviceId = value; NotifyPropertyChanged();  } }
        private int _DeviceId;
        public string SN { get => _SN; set {
                if (_SN == value) return;
                _SN = value; 
                NotifyPropertyChanged();
                SendSn();
            }
        }
        private string _SN;

        public void SendSn()
        {
            string SendMsg = $"CSN,C,{DeviceId},{SN}";
            Send(System.Text.Encoding.UTF8.GetBytes(SendMsg));
        }

        public void SendPost()
        {
            string SendMsg = $"CPT,C,{DeviceId}";
            Send(System.Text.Encoding.UTF8.GetBytes(SendMsg));
        }

        public string TestName { get => _TestName; set { _TestName = value;NotifyPropertyChanged(); } }
        private string _TestName = "WBROtest";

        public void UploadMes()
        {
            string SendMsg = $"CMI,C,{DeviceId},WBROtest,White,0.00/0.00/0.00/result,Blue,0.00/0.00/0.00/result,Red,0.00/0.00/0.00/result,Orange,0.00/0.00/0.00/result";
            Send(System.Text.Encoding.UTF8.GetBytes(SendMsg));
        }
        public void UploadNG() 
        {
            string SendMsg = $"CGI,C,{DeviceId},Default,errorW";
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
