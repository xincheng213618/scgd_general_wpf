using ColorVision.SocketProtocol;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using ProjectARVRPro.Services;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro
{
    public partial class SocketRelayWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SocketRelayWindow));
        private readonly SocketRelayManager _relayManager = SocketRelayManager.GetInstance();

        public SocketRelayWindow()
        {
            InitializeComponent();
            LoadConfig();
            MessageListView.ItemsSource = _relayManager.Messages;
            SendToFlowTextBox.Text = "{\"EventName\":\"AOITestSwitchImageComplete\",\"MsgID\":\"1\",\"Code\":0,\"Msg\":\"OK\"}";
            SendToClientTextBox.Text = "{\"EventName\":\"AOITestSwitchImage\",\"MsgID\":\"1\",\"Params\":\"\"}";
            _relayManager.MessageReceived += OnMessageReceived;
            _relayManager.PropertyChanged += OnRelayManagerPropertyChanged;
            SyncUI();
        }

        private void LoadConfig()
        {
            var config = _relayManager.Config;
            ListenIPTextBox.Text = config.ListenIP;
            ListenPortTextBox.Text = config.ListenPort.ToString();
        }

        private void SyncUI()
        {
            // 服务器状态
            if (_relayManager.IsListening)
            {
                ServerStatusText.Text = $"● 服务器已启动 {_relayManager.Config.ListenIP}:{_relayManager.Config.ListenPort}";
                ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                ListenIPTextBox.IsEnabled = false;
                ListenPortTextBox.IsEnabled = false;
            }
            else
            {
                ServerStatusText.Text = "● 服务器未启动";
                ServerStatusText.Foreground = new SolidColorBrush(Colors.Gray);
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                ListenIPTextBox.IsEnabled = true;
                ListenPortTextBox.IsEnabled = true;
            }

            // Flow连接状态
            if (_relayManager.IsFlowConnected)
            {
                FlowStatusText.Text = "● Flow已连接";
                FlowStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                SendToFlowButton.IsEnabled = true;
            }
            else
            {
                FlowStatusText.Text = "● Flow未连接";
                FlowStatusText.Foreground = new SolidColorBrush(Colors.Gray);
                SendToFlowButton.IsEnabled = false;
            }

            // 外部Client连接状态
            if (SocketControl.Current.Stream != null && SocketManager.GetInstance().TcpClients.Count > 0)
            {
                ClientStatusText.Text = "● 外部Client已连接";
                ClientStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                SendToClientButton.IsEnabled = true;
            }
            else
            {
                ClientStatusText.Text = "● 外部Client未连接";
                ClientStatusText.Foreground = new SolidColorBrush(Colors.Gray);
                SendToClientButton.IsEnabled = false;
            }
        }

        private void OnRelayManagerPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(() => SyncUI());
        }

        private void OnMessageReceived(RelayMessage msg)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (MessageListView.Items.Count > 0)
                    MessageListView.ScrollIntoView(MessageListView.Items[MessageListView.Items.Count - 1]);
            });
        }

        private void StartServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string ip = ListenIPTextBox.Text.Trim();
                if (!int.TryParse(ListenPortTextBox.Text.Trim(), out int port) || port <= 0 || port > 65535)
                {
                    UpdateStatus("端口号无效");
                    return;
                }

                _relayManager.StartServer(ip, port);
                SyncUI();
                UpdateStatus($"中转服务器已启动, 监听 {ip}:{port}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"启动失败: {ex.Message}");
                log.Error("启动中转服务器失败", ex);
                SyncUI();
            }
        }

        private void StopServer_Click(object sender, RoutedEventArgs e)
        {
            _relayManager.StopServer();
            SyncUI();
            UpdateStatus("中转服务器已停止");
        }

        private void SendToFlow_Click(object sender, RoutedEventArgs e)
        {
            string text = SendToFlowTextBox.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                UpdateStatus("发送内容为空");
                return;
            }

            _relayManager.SendToFlow(text);
            UpdateStatus("已发送到Flow");
        }

        private void SendToClient_Click(object sender, RoutedEventArgs e)
        {
            string text = SendToClientTextBox.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                UpdateStatus("发送内容为空");
                return;
            }

            _relayManager.SendToClient(text);
            UpdateStatus("已发送到外部Client");
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            _relayManager.ClearMessages();
        }

        private void UpdateStatus(string message)
        {
            if (Dispatcher.CheckAccess())
                StatusText.Text = message;
            else
                Dispatcher.Invoke(() => StatusText.Text = message);
        }

        protected override void OnClosed(EventArgs e)
        {
            _relayManager.MessageReceived -= OnMessageReceived;
            _relayManager.PropertyChanged -= OnRelayManagerPropertyChanged;
            base.OnClosed(e);
        }
    }
}
