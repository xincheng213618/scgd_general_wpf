using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace ColorVision.Engine
{
    public class SocketConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = "通信协议",
                                Description = "通用协议配置",
                                Order =1,
                                Type = ConfigSettingType.Class,
                                Source = SocketConfig.Instance
                            },
            };
        }

    }


    public class SocketConfig : ViewModelBase,IConfig
    {
        public static SocketConfig Instance => ConfigService.Instance.GetRequiredService<SocketConfig>();

        public event EventHandler<bool> IsSocketServiceChanged;
        public bool IsSocketService { get => _IsSocketService; set { _IsSocketService = value; NotifyPropertyChanged(); IsSocketServiceChanged?.Invoke(this, _IsSocketService); } }
        private bool _IsSocketService;

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

    public interface ISocketMsgHandle
    {
        public int Order { get; }
        bool Handle(NetworkStream stream, string message);
    }



    public class SocketControl:ViewModelBase
    {
        private static ILog log = LogManager.GetLogger(typeof(SocketControl));
        private static SocketControl _instance;
        private static readonly object _locker = new();
        public static SocketControl GetInstance() { lock (_locker) { return _instance ??= new SocketControl(); } }

        private static TcpListener tcpListener;
        public static SocketConfig Config => SocketConfig.Instance;

        public List<ISocketMsgHandle> SocketMsgHandles { get; set; } = new List<ISocketMsgHandle>();

        public SocketControl()
        {
            if (Config.IsSocketService)
            {
                Task.Run(() => CheckUpdate());
            }
            Config.IsSocketServiceChanged += (s, e) =>
            {
                if (e)
                {
                    StartServer();
                }
                else
                {
                    StopServer();
                }
            };
            var hanles = new List<ISocketMsgHandle>();

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(ISocketMsgHandle).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is ISocketMsgHandle configSetting)
                    {
                        hanles.Add(configSetting);
                    }
                }
            }
            SocketMsgHandles = hanles.OrderBy(handler => handler.Order).ToList();
        }


        public event EventHandler<bool> SocketConnectChanged;

        public bool IsConnect { get => _IsConnect; private set { if (_IsConnect == value) return;  _IsConnect = value; NotifyPropertyChanged(); SocketConnectChanged?.Invoke(this, _IsConnect); } }
        private bool _IsConnect;


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
                    IsConnect = false;
                    log.Info("Server stopped.");
                }
                catch (Exception e)
                {
                    log.Error("Error stopping server: " + e.Message);
                }
            }
        }
        public void CheckUpdate()
        {
            tcpListener = new TcpListener(IPAddress.Parse(Config.Host), Config.Port);
            try
            {
                tcpListener.Start();
                log.Info("Server started. Listening on port: " + Config.Port);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsConnect = true;
                });
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
                IsConnect = false;
            }
        }


        private void HandleClient(object? obj)
        {
            if (obj is not TcpClient client) return;

            NetworkStream stream = client.GetStream();

            byte[] buffer = new byte[1024];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                log.Info("Received message: " + message);

                bool handled = false;
                foreach (var handler in SocketMsgHandles)
                {
                    if (handler.Handle(stream, message))
                    {
                        handled = true;
                        break;
                    }
                }

                if (!handled)
                {
                    byte[] response = Encoding.ASCII.GetBytes("Unhandled Function Call");
                    stream.Write(response, 0, response.Length);
                }

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
