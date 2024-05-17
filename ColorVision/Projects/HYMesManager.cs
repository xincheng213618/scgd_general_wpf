using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Threading;

namespace ColorVision.Projects
{
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

        public bool IsConnect { get=> _IsConnect set { _IsConnect = value; NotifyPropertyChanged(); } }
        private bool _IsConnect;

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

              SerialMsgs.Add(new SerialMsg() { Bytes = framedMsg });
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
}
