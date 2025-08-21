#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;

namespace ColorVision.SocketProtocol
{
    public class SocketMessageBase
    {
        public string Version { get; set; }
        public string MsgID { get; set; }
        public string EventName { get; set; }
        public string SerialNumber { get; set; }

        public override string ToString() => JsonConvert.SerializeObject(this);
    }

    public class SocketRequest : SocketMessageBase
    {
        public string Params { get; set; }
    }

    public class SocketResponse : SocketMessageBase
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public dynamic Data { get; set; }
    }


    public class SocketEventDispatcher
    {
        private readonly Dictionary<string, ISocketEventHandler> _handlers = new();

        public SocketEventDispatcher()
        {
            foreach (var assembly in AssemblyService.Instance.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(ISocketEventHandler).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is ISocketEventHandler handler)
                    {
                        if (!_handlers.ContainsKey(handler.EventName))
                            _handlers[handler.EventName] = handler;
                    }
                }
            }
        }

        public SocketResponse Dispatch(NetworkStream stream, SocketRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.EventName))
                return new SocketResponse { Code = 400, Msg = "Invalid request" };

            if (_handlers.TryGetValue(request.EventName, out var handler))
                return handler.Handle(stream, request);

            return new SocketResponse { Code = 404, Msg = "Handler not found for event: " + request.EventName };
        }
    }


    public class SocketManager:ViewModelBase
    {
        private static ILog log = LogManager.GetLogger(typeof(SocketManager));
        private static SocketManager _instance;
        private static readonly object _locker = new();
        public static SocketManager GetInstance() { lock (_locker) { return _instance ??= new SocketManager(); } }

        private static TcpListener tcpListener;
        public SocketConfig Config { get; set; } = SocketConfig.Instance;

        public RelayCommand EditCommand { get; set; } 

        public SocketEventDispatcher Dispatcher { get; set; }

        public SocketManager()
        {
            Dispatcher = new SocketEventDispatcher();
            EditCommand = new RelayCommand(a =>  new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
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
        public ObservableCollection<SocketMessageBase> SocketMessageBases { get; set; } = new ObservableCollection<SocketMessageBase>();

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
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TcpClients.Add(client);
                    });
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
                    SocketRequest? request = null;
                    try
                    {
                        request = JsonConvert.DeserializeObject<SocketRequest>(message);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            SocketMessageBases.Add(request);
                        });

                        var response = Dispatcher.Dispatch(stream, request);

                        if(response !=null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                SocketMessageBases.Add(response);
                            });
                            string respString = JsonConvert.SerializeObject(response);
                            stream.Write(Encoding.UTF8.GetBytes(respString));
                        }
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
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            SocketMessageBases.Add(response);
                        });

                        string respString = JsonConvert.SerializeObject(response);
                        byte[] respBytes = Encoding.UTF8.GetBytes(respString);
                        stream.Write(respBytes, 0, respBytes.Length);
                        continue;
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
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TcpClients.Remove(client);
                });

                client?.Dispose();
            }
        }
    }
}
