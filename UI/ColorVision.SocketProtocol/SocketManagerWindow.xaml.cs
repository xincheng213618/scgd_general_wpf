using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using Newtonsoft.Json;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.SocketProtocol
{
    /// <summary>
    /// Socket管理窗口菜单项
    /// </summary>
    public class MenuProjectManager : GlobalMenuBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 9000;
        public override string Header => Properties.Resources.SocketManagementWindow;

        public override void Execute()
        {
            new SocketManagerWindow()
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }
    }

    /// <summary>
    /// SocketManagerWindow.xaml 的交互逻辑
    /// 用于管理和监控Socket连接及消息传输
    /// </summary>
    public partial class SocketManagerWindow : Window
    {
        private readonly SocketManager _socketManager;
        private ICollectionView? _messagesView;
        private bool _isWindowInitialized;

        public SocketManagerWindow()
        {
            _socketManager = SocketManager.GetInstance();
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = _socketManager;
            // 加载最近的消息记录
            _socketManager.MessageManager.LoadAll();
            _messagesView = CollectionViewSource.GetDefaultView(_socketManager.MessageManager.Messages);
            _messagesView.Filter = FilterMessage;
            _socketManager.MessageManager.Messages.CollectionChanged += Messages_CollectionChanged;
            RefreshMessageView();
            _isWindowInitialized = true;
            UpdateDetailContent(MessagesListView.SelectedItem as SocketMessage);
        }

        /// <summary>
        /// 消息列表选择改变事件
        /// </summary>
        private void MessagesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MessagesListView.SelectedItem is SocketMessage message)
            {
                DetailPanel.DataContext = message;
                UpdateDetailContent(message);
            }
            else
            {
                DetailPanel.DataContext = null;
                UpdateDetailContent(null);
            }
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isWindowInitialized)
                return;

            RefreshMessageView();
        }

        private void PrettyPrintCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isWindowInitialized || DetailPanel == null)
                return;

            UpdateDetailContent(DetailPanel.DataContext as SocketMessage);
        }

        private void ServerEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isWindowInitialized)
                return;

            ConfigService.Instance.SaveConfigs();
        }

        private bool FilterMessage(object item)
        {
            if (item is not SocketMessage message)
                return false;

            var directionFilter = (DirectionFilterCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (!string.IsNullOrEmpty(directionFilter) &&
                directionFilter != "All" &&
                !string.Equals(message.Direction.ToString(), directionFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var keyword = SearchTextBox?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(keyword))
                return true;

            return Contains(message.ClientEndPoint, keyword)
                   || Contains(message.EventName, keyword)
                   || Contains(message.MsgID, keyword)
                   || Contains(message.Content, keyword)
                   || Contains(message.ResponseCode?.ToString(), keyword);
        }

        private static bool Contains(string? source, string keyword) =>
            source?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;

        private void RefreshMessageView()
        {
            _messagesView?.Refresh();
            UpdateFilteredCount();
        }

        private void UpdateFilteredCount()
        {
            if (FilteredCountTextBlock == null || TotalCountTextBlock == null)
                return;

            var total = _socketManager.MessageManager.Messages.Count;
            var filtered = _messagesView?.Cast<object>().Count() ?? total;
            FilteredCountTextBlock.Text = $"{filtered} / {total}";
            TotalCountTextBlock.Text = FormatResource(Properties.Resources.MessageCountFormat, total);
        }

        private static string FormatResource(string format, params object?[] args)
        {
#pragma warning disable CA1863
            return string.Format(CultureInfo.CurrentUICulture, format, args);
#pragma warning restore CA1863
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateFilteredCount();

            if (AutoScrollCheckBox?.IsChecked != true || e.NewItems == null)
                return;

            foreach (var item in e.NewItems)
            {
                if (item is SocketMessage message && FilterMessage(message))
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        MessagesListView.ScrollIntoView(message);
                    });
                    break;
                }
            }
        }

        private void UpdateDetailContent(SocketMessage? message)
        {
            if (DetailContentTextBox == null)
                return;

            DetailContentTextBox.Text = FormatContent(message?.Content, PrettyPrintCheckBox?.IsChecked == true);
            DetailContentTextBox.ScrollToHome();
        }

        private static string FormatContent(string? content, bool prettyPrint)
        {
            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            if (!prettyPrint)
                return content;

            try
            {
                var obj = JsonConvert.DeserializeObject(content);
                return obj == null ? content : JsonConvert.SerializeObject(obj, Formatting.Indented);
            }
            catch
            {
                return content;
            }
        }

        /// <summary>
        /// 复制消息内容(右键菜单)
        /// </summary>
        private void CopyMessage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is SocketMessage message)
            {
                Common.Clipboard.SetText(message.Content ?? string.Empty);
            }
        }

        /// <summary>
        /// 重发消息(右键菜单)
        /// </summary>
        private void ResendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is SocketMessage message)
            {
                ResendMessageToClient(message);
            }
        }

        /// <summary>
        /// 删除消息(右键菜单)
        /// </summary>
        private void DeleteMessage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is SocketMessage message)
            {
                DeleteMessage(message);
            }
        }

        private void DeleteMessage(SocketMessage message)
        {
            _socketManager.MessageManager.DeleteMessage(message);
            if (ReferenceEquals(DetailPanel.DataContext, message))
            {
                DetailPanel.DataContext = null;
                UpdateDetailContent(null);
            }
        }

        /// <summary>
        /// 重发消息到客户端
        /// </summary>
        private void ResendMessageToClient(SocketMessage message)
        {
            if (string.IsNullOrEmpty(message.Content)) return;

            TcpClient? targetClient = FindTargetClient(message);
            if (targetClient != null && IsClientWritable(targetClient))
            {
                try
                {
                    var stream = targetClient.GetStream();
                    byte[] data = Encoding.UTF8.GetBytes(message.Content);
                    stream.Write(data, 0, data.Length);
                    var clientEndPoint = GetEndPointText(targetClient) ?? message.ClientEndPoint;

                    // 记录重发消息
                    var resendMsg = new SocketMessage
                    {
                        ClientEndPoint = clientEndPoint,
                        Direction = SocketMessageDirection.Sent,
                        Content = message.Content,
                        MessageTime = DateTime.Now,
                        EventName = message.EventName,
                        MsgID = message.MsgID
                    };
                    _socketManager.MessageManager.AddMessage(resendMsg);

                    MessageBox.Show(Properties.Resources.ResendSuccess, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(FormatResource(Properties.Resources.ResendFailed, ex.Message), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show(Properties.Resources.ClientNotConnected, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private TcpClient? FindTargetClient(SocketMessage message)
        {
            foreach (var client in _socketManager.TcpClients)
            {
                var remoteEndPoint = SafeGetRemoteEndPoint(client);
                if (!string.IsNullOrEmpty(remoteEndPoint) &&
                    Contains(message.ClientEndPoint, remoteEndPoint) &&
                    IsClientWritable(client))
                {
                    return client;
                }
            }

            return _socketManager.TcpClients.FirstOrDefault(IsClientWritable);
        }

        private static bool IsClientWritable(TcpClient client)
        {
            try
            {
                return client.Connected && client.GetStream().CanWrite;
            }
            catch
            {
                return false;
            }
        }

        private static string? SafeGetRemoteEndPoint(TcpClient client)
        {
            try
            {
                return client.Client.RemoteEndPoint?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string? GetEndPointText(TcpClient client)
        {
            try
            {
                return client.Client.RemoteEndPoint?.ToString() ?? client.Client.LocalEndPoint?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                SearchTextBox.Focus();
                SearchTextBox.SelectAll();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape && !string.IsNullOrEmpty(SearchTextBox.Text))
            {
                SearchTextBox.Clear();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F5)
            {
                _socketManager.MessageManager.LoadAll(_socketManager.MessageManager.Config.Count);
                RefreshMessageView();
                e.Handled = true;
                return;
            }

            if (Keyboard.FocusedElement is TextBox)
                return;

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C && GetCurrentMessage() is SocketMessage copyMessage)
            {
                Common.Clipboard.SetText(copyMessage.Content ?? string.Empty);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete && GetCurrentMessage() is SocketMessage deleteMessage)
            {
                DeleteMessage(deleteMessage);
                e.Handled = true;
            }
        }

        private SocketMessage? GetCurrentMessage() =>
            DetailPanel.DataContext as SocketMessage ?? MessagesListView.SelectedItem as SocketMessage;

        /// <summary>
        /// 窗口关闭时的清理逻辑
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _socketManager.MessageManager.Messages.CollectionChanged -= Messages_CollectionChanged;
            if (_messagesView != null)
                _messagesView.Filter = null;
            base.OnClosed(e);
        }
    }
}
