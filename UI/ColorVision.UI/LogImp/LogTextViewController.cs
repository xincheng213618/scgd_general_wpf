using log4net.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.LogImp
{
    internal sealed class LogTextViewController
    {
        private readonly UIElement _keyboardTarget;
        private readonly FrameworkElement _contextMenuRoot;
        private readonly FrameworkElement _searchPanel;
        private readonly Control _searchInput;
        private readonly TextBox _sourceTextBox;
        private readonly TextBox _searchResultTextBox;
        private readonly ButtonBase? _closeSearchButton;
        private readonly Brush? _defaultSearchBorderBrush;
        private readonly DispatcherTimer _searchDebounceTimer;

        private string _pendingSearchText = string.Empty;
        private bool _isDetached;
        private const int SearchDebounceMilliseconds = 200;

        public LogTextViewController(
            UIElement keyboardTarget,
            FrameworkElement contextMenuRoot,
            FrameworkElement searchPanel,
            Control searchInput,
            TextBox sourceTextBox,
            TextBox searchResultTextBox,
            ButtonBase? closeSearchButton = null)
        {
            _keyboardTarget = keyboardTarget;
            _contextMenuRoot = contextMenuRoot;
            _searchPanel = searchPanel;
            _searchInput = searchInput;
            _sourceTextBox = sourceTextBox;
            _searchResultTextBox = searchResultTextBox;
            _closeSearchButton = closeSearchButton;
            _defaultSearchBorderBrush = searchInput.BorderBrush;
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SearchDebounceMilliseconds)
            };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;

            _keyboardTarget.PreviewKeyDown += KeyboardTarget_PreviewKeyDown;
            if (_closeSearchButton != null)
            {
                _closeSearchButton.Click += CloseSearchButton_Click;
            }
        }

        public void ApplySearchFilter(string searchText)
        {
            _searchDebounceTimer.Stop();
            LogViewUiHelper.ApplySearchFilter(searchText, _sourceTextBox, _searchResultTextBox, _searchInput, _defaultSearchBorderBrush);
        }

        public void QueueSearchFilter(string searchText)
        {
            _pendingSearchText = searchText;

            if (string.IsNullOrEmpty(searchText))
            {
                ApplySearchFilter(searchText);
                return;
            }

            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        public void ConfigureContextMenus(Action<ContextMenu, TextBox> appendLogItems)
        {
            _sourceTextBox.ContextMenu = CreateContextMenu(_sourceTextBox, appendLogItems);
            _searchResultTextBox.ContextMenu = CreateContextMenu(_searchResultTextBox, appendLogItems);
            _contextMenuRoot.ContextMenu = CreateContextMenu(_sourceTextBox, appendLogItems);
        }

        public void ShowSearchPanel()
        {
            _searchPanel.Visibility = Visibility.Visible;
            _searchInput.Dispatcher.BeginInvoke(() => LogViewUiHelper.FocusSearchInput(_searchInput));
        }

        public void HideSearchPanel(bool clearSearch)
        {
            if (clearSearch && _searchInput is TextBox textBox)
            {
                textBox.Text = string.Empty;
            }

            _searchPanel.Visibility = Visibility.Collapsed;
            _sourceTextBox.Focus();
        }

        private ContextMenu CreateContextMenu(TextBox commandTarget, Action<ContextMenu, TextBox> appendLogItems)
        {
            var contextMenu = new ContextMenu();

            var searchMenuItem = new MenuItem
            {
                Header = Properties.Resources.Search,
                InputGestureText = LogViewUiHelper.CtrlFInputGestureText
            };
            searchMenuItem.Click += (_, _) => ShowSearchPanel();
            contextMenu.Items.Add(searchMenuItem);

            contextMenu.Items.Add(new MenuItem
            {
                Header = Properties.Resources.MenuCopy,
                Command = ApplicationCommands.Copy,
                CommandTarget = commandTarget,
                InputGestureText = "Ctrl+C"
            });
            contextMenu.Items.Add(new MenuItem
            {
                Header = Properties.Resources.MenuSelectAll,
                Command = ApplicationCommands.SelectAll,
                CommandTarget = commandTarget,
                InputGestureText = "Ctrl+A"
            });
            contextMenu.Items.Add(new Separator());

            appendLogItems(contextMenu, commandTarget);

            return contextMenu;
        }

        private void KeyboardTarget_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (LogViewUiHelper.IsCtrlF(e))
            {
                ShowSearchPanel();
                e.Handled = true;
                return;
            }

            if (LogViewUiHelper.IsEscape(e) && _searchPanel.Visibility == Visibility.Visible)
            {
                HideSearchPanel(clearSearch: true);
                e.Handled = true;
            }
        }

        private void CloseSearchButton_Click(object sender, RoutedEventArgs e)
        {
            HideSearchPanel(clearSearch: true);
        }

        private void SearchDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _searchDebounceTimer.Stop();
            ApplySearchFilter(_pendingSearchText);
        }

        public void Detach()
        {
            if (_isDetached)
            {
                return;
            }

            _isDetached = true;
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Tick -= SearchDebounceTimer_Tick;
            _keyboardTarget.PreviewKeyDown -= KeyboardTarget_PreviewKeyDown;
            if (_closeSearchButton != null)
            {
                _closeSearchButton.Click -= CloseSearchButton_Click;
            }
        }
    }

    internal static class LogTextViewMenuFactory
    {
        public static void AppendRealtimeLogMenuItems(ContextMenu contextMenu, Action clear, Action<Level> setLogLevel)
        {
            var clearMenuItem = new MenuItem { Header = Properties.Resources.Clear };
            clearMenuItem.Click += (_, _) => clear();
            contextMenu.Items.Add(clearMenuItem);

            var logLevelMenuItem = new MenuItem { Header = Properties.Resources.LogLevel };
            contextMenu.Items.Add(logLevelMenuItem);

            var autoScrollMenuItem = CreateCheckableItem(
                Properties.Resources.AutoScrollToEnd,
                () => LogConfig.Instance.AutoScrollToEnd,
                value => LogConfig.Instance.AutoScrollToEnd = value);
            contextMenu.Items.Add(autoScrollMenuItem);

            var autoRefreshMenuItem = CreateCheckableItem(
                Properties.Resources.AutoRefresh,
                () => LogConfig.Instance.AutoRefresh,
                value => LogConfig.Instance.AutoRefresh = value);
            contextMenu.Items.Add(autoRefreshMenuItem);

            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem
            {
                Header = Properties.Resources.GeneralSettings,
                Command = LogConfig.Instance.EditCommand
            });

            contextMenu.Opened += (_, _) =>
            {
                LogViewUiHelper.PopulateLogLevelMenu(logLevelMenuItem, LogConfig.GetAllLevels(), LogConfig.Instance.LogLevel, setLogLevel);
                RefreshCheckableItem(autoScrollMenuItem, () => LogConfig.Instance.AutoScrollToEnd);
                RefreshCheckableItem(autoRefreshMenuItem, () => LogConfig.Instance.AutoRefresh);
            };
        }

        public static void AppendLocalLogMenuItems(
            ContextMenu contextMenu,
            WindowLogLocalConfig config,
            Action refresh,
            Action open,
            Action clear)
        {
            var refreshMenuItem = new MenuItem { Header = NavigationCommands.Refresh.Text };
            refreshMenuItem.Click += (_, _) => refresh();
            contextMenu.Items.Add(refreshMenuItem);

            var openMenuItem = new MenuItem { Header = Properties.Resources.Log_OpenFolder };
            openMenuItem.Click += (_, _) => open();
            contextMenu.Items.Add(openMenuItem);

            var clearMenuItem = new MenuItem { Header = Properties.Resources.Clear };
            clearMenuItem.Click += (_, _) => clear();
            contextMenu.Items.Add(clearMenuItem);

            contextMenu.Items.Add(new Separator());

            var reverseMenuItem = CreateCheckableItem(
                Properties.Resources.Log_ReverseOrder,
                () => config.LogReverse,
                value => config.LogReverse = value);
            contextMenu.Items.Add(reverseMenuItem);

            var autoScrollMenuItem = CreateCheckableItem(
                Properties.Resources.AutoScrollToEnd,
                () => config.AutoScrollToEnd,
                value => config.AutoScrollToEnd = value);
            contextMenu.Items.Add(autoScrollMenuItem);

            var autoRefreshMenuItem = CreateCheckableItem(
                Properties.Resources.AutoRefresh,
                () => config.AutoRefresh,
                value => config.AutoRefresh = value);
            contextMenu.Items.Add(autoRefreshMenuItem);

            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem
            {
                Header = Properties.Resources.GeneralSettings,
                Command = config.EditCommand
            });

            contextMenu.Opened += (_, _) =>
            {
                RefreshCheckableItem(reverseMenuItem, () => config.LogReverse);
                RefreshCheckableItem(autoScrollMenuItem, () => config.AutoScrollToEnd);
                RefreshCheckableItem(autoRefreshMenuItem, () => config.AutoRefresh);
            };
        }

        private static MenuItem CreateCheckableItem(string header, Func<bool> getValue, Action<bool> setValue)
        {
            var menuItem = new MenuItem
            {
                Header = header,
                IsCheckable = true,
                IsChecked = getValue()
            };
            menuItem.Click += (_, _) => setValue(menuItem.IsChecked);
            return menuItem;
        }

        private static void RefreshCheckableItem(MenuItem menuItem, Func<bool> getValue)
        {
            menuItem.IsChecked = getValue();
        }
    }
}
