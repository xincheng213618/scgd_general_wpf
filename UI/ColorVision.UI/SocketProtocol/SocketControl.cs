using ColorVision.Common.MVVM;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace ColorVision.UI.SocketProtocol
{
    public class SocketMessageBase
    {
        public string Version { get; set; }
        public string MsgID { get; set; }
        public string EventName { get; set; }
        public string SerialNumber { get; set; }
    }

    public class SocketRequest : SocketMessageBase
    {
        public dynamic Params { get; set; }
    }

    public class SocketResponse : SocketMessageBase
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public dynamic Data { get; set; }
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
            tcpListener = new TcpListener(IPAddress.Parse(Config.IPAddress), Config.ServerPort);
            try
            {
                tcpListener.Start();
                log.Info("Server started. Listening on port: " + Config.ServerPort);
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
                Config.IsServerEnabled = false;
            }
        }


        private void HandleClient(object? obj)
        {
            if (obj is not TcpClient client) return;

            NetworkStream stream = client.GetStream();

            byte[] buffer = Config.SocketBufferSize > 1024 ? new byte[Config.SocketBufferSize] : new byte[1024];
            int bytesRead;
            try
            {
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    log.Info("Received raw message: " + message);
                    // 尝试作为SocketRequest解析
                    SocketRequest? request = null;
                    try
                    {
                        request = JsonConvert.DeserializeObject<SocketRequest>(message);
                    }
                    catch (Exception ex)
                    {
                        var response = new SocketResponse
                        {
                            Version = request?.Version ?? "1.0",
                            MsgID = request?.MsgID ?? "",
                            EventName = request?.EventName ?? "",
                            SerialNumber = request?.SerialNumber ?? "",
                            Code = -1,
                            Msg = ex.Message,
                            Data = null
                        };
                        string respString = JsonConvert.SerializeObject(response);
                        byte[] respBytes = Encoding.UTF8.GetBytes(respString);
                        stream.Write(respBytes, 0, respBytes.Length);
                        continue;
                    }

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
                        // 构造标准响应
                        var response = new SocketResponse
                        {
                            Version = request?.Version ?? "1.0",
                            MsgID = request?.MsgID ?? "",
                            EventName = request?.EventName ?? "",
                            SerialNumber = request?.SerialNumber ?? "",
                            Code = 404,
                            Msg = "Unhandled Function Call",
                            Data = null
                        };
                        string respString = JsonConvert.SerializeObject(response);
                        byte[] respBytes = Encoding.UTF8.GetBytes(respString);
                        stream.Write(respBytes, 0, respBytes.Length);
                        log.Info("Sent unhandled response: " + respString);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Client handling error: " + ex);
                client?.Close();
            }
            finally
            {
                client?.Dispose();
            }
        }
    }
}
