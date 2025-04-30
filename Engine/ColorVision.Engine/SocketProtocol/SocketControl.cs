using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using log4net;
using MySqlX.XDevAPI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
                    foreach (var item in TcpClients)
                    {
                        if (item.Connected)
                        {
                            item.Client.Shutdown(SocketShutdown.Both);
                        }
                        item?.Close();
                        item?.Dispose();
                    }
                }
                catch (Exception e)
                {
                    log.Error("Error stopping server: " + e.Message);
                }
            }
        }

        public ObservableCollection<TcpClient> TcpClients { get; set; } = new ObservableCollection<TcpClient>();

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
                    TcpClients.Add(client);
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

            byte[] buffer = Config.BufferLength > 1024 ? new byte[Config.BufferLength] : new byte[1024];
            int bytesRead;
            try
            {
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
            }
            catch
            {
                client?.Close();
            }
            client?.Dispose();
        }
    }
}
