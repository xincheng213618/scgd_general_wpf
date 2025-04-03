using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using log4net.Util;
using ProjectKB.Modbus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ProjectKB.Services
{

    public class SocketConfig : ViewModelBase,IConfig
    {

        public static SocketConfig Instance => ConfigService.Instance.GetRequiredService<SocketConfig>();

        public bool IsUseSocket { get => _IsUseSocket; set { _IsUseSocket = value; NotifyPropertyChanged(); } }
        private bool _IsUseSocket = false;


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
                string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                log.Info("Received message: " + message);
                byte[] response = Encoding.ASCII.GetBytes(message);
                stream.Write(response, 0, response.Length);
                StatusChanged?.Invoke(this, new EventArgs());
            }

            client.Close();
        }




    }
}
