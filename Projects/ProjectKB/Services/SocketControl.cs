using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ProjectKB.Services
{

    public class SocketConfig : ViewModelBase,IConfig
    {

        public static SocketConfig Instance => ConfigService.Instance.GetRequiredService<SocketConfig>();

        public bool IsUseSocket { get => _IsUseSocket; set { _IsUseSocket = value; NotifyPropertyChanged(); } }
        private bool _IsUseSocket;


        /// <summary>
        /// IP地址
        /// </summary>
        [DisplayName("IP地址")]
        public string Host { get => _Host; set { _Host = value; NotifyPropertyChanged(); } }
        private string _Host = "127.0.0.1";

        /// <summary>
        /// 端口地址
        /// </summary>
        [DisplayName("端口")]
        public int Port
        {
            get => _Port; set
            {
                _Port = value <= 0 ? 0 : value >= 65535 ? 65535 : value;
                NotifyPropertyChanged();
            }
        }
        private int _Port = 6666;
    }


    public class SocketControl
    {
        private static ILog log = LogManager.GetLogger(typeof(SocketControl));
        private static SocketControl _instance;
        private static readonly object _locker = new();
        public static SocketControl GetInstance() { lock (_locker) { return _instance ??= new SocketControl(); } }

        private static TcpListener tcpListener;
        public static SocketConfig Config => SocketConfig.Instance;

        public SocketControl()
        {
            StartServer();
        }

        public void StartServer()
        {
            Task.Run(() => CheckUpdate());
        }
        public void StopServer()
        {
            if (tcpListener != null)
            {
                try
                {
                    tcpListener.Stop();
                    Console.WriteLine("Server stopped.");
                }
                catch (Exception e)
                {
                    log.Error("Error stopping server: " + e.Message);
                }
            }
        }
        public void CheckUpdate()
        {
            if (Config.IsUseSocket)
            {
                tcpListener = new TcpListener(IPAddress.Parse(Config.Host), Config.Port);
                try
                {
                    tcpListener.Start();

                    Console.WriteLine("Server started. Listening on port: " + Config.Port);

                    while (true)
                    {
                        TcpClient client = tcpListener.AcceptTcpClient();
                        Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClient));
                        clientThread.Start(client);
                    }
                }
                catch (SocketException e)
                {
                    log.Error(e);
                }
                finally
                {
                    tcpListener.Stop();
                }
            }
        }
        public event EventHandler StatusChanged;


        private  void HandleClient(object obj)
        {
            TcpClient client = (TcpClient)obj;

            NetworkStream stream = client.GetStream();

            byte[] buffer = new byte[1024];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                log.Info("Received message: " + message);
                if (message.Contains('*') == false ||message.Contains('#') == false)
                {
                    string[] resultData = new string[3];
                    resultData[0] = "A";
                    resultData[1] = "Error";
                    resultData[2] = "2";
                    string XorData = CalXor(resultData);
                    string hhData = CalXor(XorData);
                    string returnmsg = "#" + resultData[0] + "," + resultData[1] + "," + resultData[2] + ",*" + hhData;
                    byte[] response = Encoding.ASCII.GetBytes(returnmsg);
                    stream.Write(response, 0, response.Length);
                    continue;
                }


                StatusChanged?.Invoke(this, new EventArgs());
            }

            client.Close();
        }

        public string CalXor(string[] srcData)
        {
            StringBuilder strText = new StringBuilder();
            foreach (string s in srcData)
            {
                // 获取s对应的字节数组
                byte[] b = Encoding.ASCII.GetBytes(s);
                // xorResult 存放校验结果。注意：初值去首元素值！
                byte xorResult = b[0];
                // 求xor校验和。注意：XOR运算从第二元素开始
                for (int i = 1; i < b.Length; i++)
                {
                    xorResult ^= b[i];
                }
                strText.Append(xorResult.ToString("x2")); // 使用16进制格式
            }
            return strText.ToString();
        }
        public string CalXor(string srcData)
        {
            // 获取s对应的字节数组
            byte[] b = Encoding.ASCII.GetBytes(srcData);
            // xorResult 存放校验结果。注意：初值去首元素值！
            byte xorResult = b[0];
            // 求xor校验和。注意：XOR运算从第二元素开始
            for (int i = 1; i < b.Length; i++)
            {
                xorResult ^= b[i];
            }
            return xorResult.ToString("x2"); // 直接返回16进制格式的字符串
        }


    }
}
