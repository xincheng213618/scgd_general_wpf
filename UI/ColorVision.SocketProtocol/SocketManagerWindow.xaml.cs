using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using Newtonsoft.Json;
using System.Collections.Specialized;
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
        private readonly ISocketDatabaseCleanupWindowLauncher? _cleanupWindowLauncher;
        private ListCollectionView? _messagesView;
        private bool _isWindowInitialized;

        public SocketManagerWindow() : this(SocketManager.GetInstance(), loadMessages: true)
        {
        }

        internal SocketManagerWindow(SocketManager socketManager, bool loadMessages = false, ISocketDatabaseCleanupWindowLauncher? cleanupWindowLauncher = null)
        {
            ArgumentNullException.ThrowIfNull(socketManager);
            _socketManager = socketManager;
            _cleanupWindowLauncher = cleanupWindowLauncher;
            // 绑定视图之前加载历史记录，避免初始查询触发实时消息的自动滚动。
            if (loadMessages)
                _socketManager.MessageManager.LoadAll(_socketManager.MessageManager.Config.Count);
            InitializeComponent();
            this.ApplyCaption();
        }

        private void DatabaseCleanupButton_Click(object sender, RoutedEventArgs e)
        {
            ISocketDatabaseCleanupWindowLauncher? launcher = _cleanupWindowLauncher;
            if (launcher == null)
            {
                AssemblyHandler assemblies = AssemblyHandler.GetInstance();
                assemblies.RefreshAssemblies();
                launcher = assemblies.LoadImplementations<ISocketDatabaseCleanupWindowLauncher>().FirstOrDefault();
            }

            if (launcher == null)
            {
                MessageBox.Show(this, Properties.Resources.DatabaseCleanupUnavailable, Properties.Resources.SocketManagementWindow, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            launcher.OpenWindow(this);
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = _socketManager;
            // 每个窗口独立筛选，避免改变其他窗口或数据库浏览器的默认视图。
            _messagesView = new ListCollectionView(_socketManager.MessageManager.Messages);
            _messagesView.Filter = FilterMessage;
            MessagesListView.ItemsSource = _messagesView;
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
            UpdateDetailContent(MessagesListView.SelectedItem as SocketMessage);
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isWindowInitialized)
                return;

            RefreshMessageView();
        }

        private void ClearFilterButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Clear();
            DirectionFilterCombo.SelectedIndex = 0;
            SearchTextBox.Focus();
        }

        private void PrettyPrintCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isWindowInitialized || DetailPanel == null)
                return;

            UpdateDetailContent(DetailPanel.DataContext as SocketMessage);
        }

        private void MessagesListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateContentColumnWidth();
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
                   || Contains(message.ContentPreview, keyword)
                   || Contains(message.ResponseCode?.ToString(), keyword);
        }

        private static bool Contains(string? source, string keyword) =>
            source?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;

        private void RefreshMessageView()
        {
            _messagesView?.Refresh();
            UpdateFilteredCount();
            UpdateContentColumnWidth();
        }

        private void UpdateFilteredCount()
        {
            if (MessageCountTextBlock == null)
                return;

            var total = _socketManager.MessageManager.Messages.Count;
            var filtered = _messagesView?.Count ?? total;
            bool hasFilter = !string.IsNullOrWhiteSpace(SearchTextBox.Text) || DirectionFilterCombo.SelectedIndex > 0;
            MessageCountTextBlock.Text = hasFilter
                ? FormatResource(Properties.Resources.FilteredMessageCountFormat, filtered, total)
                : FormatResource(Properties.Resources.MessageCountFormat, total);
            ClearFilterButton.IsEnabled = !string.IsNullOrEmpty(SearchTextBox.Text) || DirectionFilterCombo.SelectedIndex > 0;
            EmptyMessagesPanel.Visibility = filtered == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyMessagesTitleTextBlock.Text = total == 0 ? Properties.Resources.NoMessages : Properties.Resources.NoMatchingMessages;
            EmptyMessagesHintTextBlock.Text = total == 0 ? Properties.Resources.NoMessagesHint : Properties.Resources.NoMatchingMessagesHint;
        }

        private void UpdateContentColumnWidth()
        {
            if (ContentColumn == null || MessagesListView?.View is not GridView gridView || MessagesListView.ActualWidth <= 0)
                return;

            double fixedColumnsWidth = gridView.Columns.Where(column => column != ContentColumn)
                .Sum(column => double.IsNaN(column.Width) ? column.ActualWidth : column.Width);
            double availableWidth = MessagesListView.ActualWidth
                - fixedColumnsWidth
                - MessagesListView.Padding.Left
                - MessagesListView.Padding.Right
                - SystemParameters.VerticalScrollBarWidth
                - 12; // row chrome and scrollbar breathing room
            ContentColumn.Width = Math.Max(140, availableWidth);
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
                        if (_isWindowInitialized && _messagesView?.Contains(message) == true)
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

            DetailPanel.DataContext = message;
            MessageMetadataPanel.Visibility = message == null ? Visibility.Collapsed : Visibility.Visible;
            NoSelectionPanel.Visibility = message == null ? Visibility.Visible : Visibility.Collapsed;
            EmptyContentPanel.Visibility = Visibility.Collapsed;
            CopyDetailButton.IsEnabled = false;
            CopyFormattedDetailButton.IsEnabled = false;
            PrettyPrintCheckBox.IsEnabled = false;
            DetailClientTextBlock.Text = DisplayMetadata(message?.ClientEndPoint);
            DetailEventTextBlock.Text = DisplayMetadata(message?.EventName);
            DetailMsgIdTextBlock.Text = DisplayMetadata(message?.MsgID);
            DetailResponseCodeTextBlock.Text = DisplayMetadata(message?.ResponseCode?.ToString(CultureInfo.CurrentUICulture));

            if (message == null)
            {
                DetailContentTextBox.Text = string.Empty;
                return;
            }

            try
            {
                string? content = _socketManager.MessageManager.LoadContent(message);
                bool hasContent = !string.IsNullOrWhiteSpace(content);
                DetailContentTextBox.Text = FormatContent(content, PrettyPrintCheckBox.IsChecked == true);
                EmptyContentPanel.Visibility = hasContent ? Visibility.Collapsed : Visibility.Visible;
                CopyDetailButton.IsEnabled = hasContent;
                CopyFormattedDetailButton.IsEnabled = hasContent;
                PrettyPrintCheckBox.IsEnabled = hasContent;
            }
            catch (Exception ex)
            {
                DetailContentTextBox.Text = FormatResource(Properties.Resources.ContentLoadFailedFormat, ex.Message);
            }
            DetailContentTextBox.ScrollToHome();
        }

        private static string DisplayMetadata(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

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
            if (sender is FrameworkElement { Tag: SocketMessage message })
            {
                if (TryLoadMessageContent(message, out string? content))
                    Common.Clipboard.SetText(content ?? string.Empty);
            }
        }

        private void CopyFormattedMessage_Click(object sender, RoutedEventArgs e)
        {
            if (GetCurrentMessage() is SocketMessage message && TryLoadMessageContent(message, out string? content))
                Common.Clipboard.SetText(FormatContent(content, prettyPrint: true));
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
            if (!TryLoadMessageContent(message, out string? content) || string.IsNullOrEmpty(content))
                return;

            TcpClient? targetClient = FindTargetClient(message);
            if (targetClient != null && IsClientWritable(targetClient))
            {
                try
                {
                    var stream = targetClient.GetStream();
                    byte[] data = Encoding.UTF8.GetBytes(content);
                    stream.Write(data, 0, data.Length);
                    var clientEndPoint = GetEndPointText(targetClient) ?? message.ClientEndPoint;

                    // 记录重发消息
                    var resendMsg = new SocketMessage
                    {
                        ClientEndPoint = clientEndPoint,
                        Direction = SocketMessageDirection.Sent,
                        Content = content,
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

        private bool TryLoadMessageContent(SocketMessage message, out string? content)
        {
            try
            {
                content = _socketManager.MessageManager.LoadContent(message);
                return true;
            }
            catch (Exception ex)
            {
                content = null;
                MessageBox.Show(
                    FormatResource(Properties.Resources.ContentLoadFailedFormat, ex.Message),
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
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

            if (e.Key == Key.Escape && ClearFilterButton.IsEnabled)
            {
                ClearFilterButton_Click(sender, e);
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
                if (TryLoadMessageContent(copyMessage, out string? content))
                    Common.Clipboard.SetText(content ?? string.Empty);
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
            _isWindowInitialized = false;
            _socketManager.MessageManager.Messages.CollectionChanged -= Messages_CollectionChanged;
            MessagesListView.ItemsSource = null;
            if (_messagesView != null)
            {
                _messagesView.Filter = null;
                _messagesView.DetachFromSourceCollection();
            }
            _messagesView = null;
            base.OnClosed(e);
        }
    }
}
