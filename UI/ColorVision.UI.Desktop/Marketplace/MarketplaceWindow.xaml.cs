#pragma warning disable CA1001,CA1822,CA1863
using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI.Desktop.NativeMethods;
using ColorVision.UI.Marketplace;
using ColorVision.UI.Menus;
using log4net;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopResources = ColorVision.UI.Desktop.Properties.Resources;

namespace ColorVision.UI.Desktop.Marketplace
{

    /// <summary>
    /// MarketplaceWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MarketplaceWindow : Window
    {
        private enum DetailPanelMode
        {
            None,
            Installed,
            Marketplace,
        }

        private static readonly ILog log = LogManager.GetLogger(typeof(MarketplaceWindow));
        private MarketplaceManager? _manager;
        private bool _isRefreshingVersions;
        private CancellationTokenSource? _windowCancellation = new();
        private CancellationTokenSource? _selectionCancellation;
        private CancellationTokenSource? _refreshCancellation;
        private CancellationTokenSource? _detailRefreshCancellation;
        private PluginInfoVM? _installedDetailPlugin;
        private Rect _compactBounds = Rect.Empty;
        private double _catalogColumnWidth;
        private double _catalogWindowChrome = 48;
        private readonly double _compactMinWidth;
        private double _expandedLeft;
        private bool _normalWindowExpanded;
        private bool _isChangingWindowBounds;

        public MarketplaceWindow()
        {
            InitializeComponent();
            _compactMinWidth = MinWidth;
            this.ApplyCaption();
            SizeChanged += Window_BoundsChanged;
            LocationChanged += Window_BoundsChanged;
            Loaded += (_, _) =>
            {
                RememberCompactBounds();
                UpdateDetailPane();
            };
            StateChanged += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateDetailPane));
            Closed += (_, _) =>
            {
                SaveCompactWindowConfig();
                if (_manager != null)
                {
                    _manager.PropertyChanged -= Manager_PropertyChanged;
                }

                _manager?.CancelActiveOperations();
                CancelAndDispose(ref _selectionCancellation);
                CancelAndDispose(ref _refreshCancellation);
                CancelAndDispose(ref _detailRefreshCancellation);
                CancelAndDispose(ref _windowCancellation);
            };
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            _manager = MarketplaceManager.GetInstance();
            _manager.SelectedInstalledPlugin = null;
            _manager.Catalog.SelectedPlugin = null;
            _manager.IsMarketplaceTabActive = false;
            DataContext = _manager;
            _manager.PropertyChanged += Manager_PropertyChanged;

            _ = RefreshInstalledVersionsOnOpenAsync(_windowCancellation!.Token);

            this.CommandBindings.Add(new CommandBinding(
                ApplicationCommands.Delete,
                (s, args) => _manager.SelectedInstalledPlugin?.Delete(),
                (s, args) => args.CanExecute = !_manager.IsMarketplaceTabActive && _manager.SelectedInstalledPlugin != null));
        }

        private void Window_BoundsChanged(object? sender, EventArgs e)
        {
            if (!IsLoaded || _isChangingWindowBounds)
                return;

            if (WindowState == WindowState.Normal && !_normalWindowExpanded)
                RememberCompactBounds();

            if (WindowState == WindowState.Normal && _normalWindowExpanded)
                MinWidth = GetExpandedMinWidth(GetCurrentWorkingArea());
            UpdateDetailColumns(_manager?.HasCurrentSelection == true);
            SaveCompactWindowConfig();
        }

        private void RememberCompactBounds()
        {
            if (WindowState != WindowState.Normal)
            {
                if (_compactBounds.IsEmpty && !RestoreBounds.IsEmpty)
                    _compactBounds = RestoreBounds;
                return;
            }

            _compactBounds = new Rect(Left, Top, Width, Height);
            if (DetailColumn.Width.IsAbsolute && DetailColumn.Width.Value == 0)
            {
                _catalogColumnWidth = CatalogColumn.ActualWidth > 0 ? CatalogColumn.ActualWidth : Math.Max(0, Width - 48);
                _catalogWindowChrome = Math.Max(0, Width - _catalogColumnWidth);
            }
        }

        private Rect GetCurrentWorkingArea()
        {
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
            Rect pixelBounds = this.GetWindowRectInPixel();
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            // Convert the monitor's pixel offsets relative to this window, keeping mixed-DPI desktop origins intact.
            return new Rect(
                Left + (screen.WorkingArea.Left - pixelBounds.Left) / dpi.DpiScaleX,
                Top + (screen.WorkingArea.Top - pixelBounds.Top) / dpi.DpiScaleY,
                screen.WorkingArea.Width / dpi.DpiScaleX,
                screen.WorkingArea.Height / dpi.DpiScaleY);
        }

        private static Rect CalculateExpandedBounds(Rect compactBounds, Rect workingArea)
        {
            double width = Math.Min(compactBounds.Width + 660, workingArea.Width);
            double left = Math.Clamp(compactBounds.Left, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - width));
            return new Rect(left, compactBounds.Top, width, compactBounds.Height);
        }

        private double GetExpandedMinWidth(Rect workingArea)
        {
            return Math.Min(workingArea.Width, _catalogWindowChrome + Math.Min(_catalogColumnWidth, 380) + 400);
        }

        private void UpdateDetailColumns(bool showDetails)
        {
            double availableWidth = Math.Max(0, ActualWidth - _catalogWindowChrome);
            double preferredCatalogWidth = _catalogColumnWidth > 0 ? _catalogColumnWidth : Math.Max(0, Width - 48);
            double catalogWidth = Math.Min(preferredCatalogWidth, Math.Max(320, availableWidth - 400));
            catalogWidth = Math.Min(catalogWidth, availableWidth * 0.55);
            CatalogColumn.Width = showDetails ? new GridLength(catalogWidth) : new GridLength(1, GridUnitType.Star);
            DetailColumn.Width = showDetails ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            BorderContent.Visibility = showDetails ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateDetailPane()
        {
            if (!IsLoaded || _windowCancellation == null)
                return;

            bool showDetails = _manager?.HasCurrentSelection == true;
            if (WindowState == WindowState.Normal && showDetails != _normalWindowExpanded)
            {
                _isChangingWindowBounds = true;
                try
                {
                    if (showDetails)
                    {
                        RememberCompactBounds();
                        Rect workingArea = GetCurrentWorkingArea();
                        Rect expandedBounds = CalculateExpandedBounds(_compactBounds, workingArea);
                        _normalWindowExpanded = true;
                        _expandedLeft = expandedBounds.Left;
                        MinWidth = GetExpandedMinWidth(workingArea);
                        Width = expandedBounds.Width;
                        Left = expandedBounds.Left;
                    }
                    else
                    {
                        Rect workingArea = GetCurrentWorkingArea();
                        double width = Math.Min(_compactBounds.Width, workingArea.Width);
                        double left = _compactBounds.Left + (Left - _expandedLeft);
                        left = Math.Clamp(left, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - width));
                        MinWidth = Math.Min(_compactMinWidth, workingArea.Width);
                        Width = width;
                        Left = left;
                        _normalWindowExpanded = false;
                        _compactBounds = new Rect(left, Top, width, Height);
                    }
                }
                finally
                {
                    _isChangingWindowBounds = false;
                }
            }

            UpdateDetailColumns(showDetails);
            SaveCompactWindowConfig();
        }

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { ContextMenu: { } menu } button)
            {
                menu.DataContext = button.DataContext;
                menu.PlacementTarget = button;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private void SaveCompactWindowConfig()
        {
            if (!IsLoaded || _compactBounds.IsEmpty)
                return;

            MarketplaceWindowConfig config = MarketplaceWindowConfig.Instance;
            config.SetConfig(this);
            // SizeChanged can arrive after a programmed resize; always persist the logical list-only width.
            config.Width = _compactBounds.Width;
            double normalLeft = WindowState == WindowState.Normal ? Left : RestoreBounds.Left;
            config.Left = _normalWindowExpanded ? _compactBounds.Left + (normalLeft - _expandedLeft) : _compactBounds.Left;
        }

        private void CloseDetailButton_Click(object sender, RoutedEventArgs e)
        {
            if (_manager == null)
                return;

            if (_manager.IsMarketplaceTabActive)
            {
                _manager.Catalog.SelectedPlugin = null;
                ListViewMarketplace.Focus();
            }
            else
            {
                _manager.SelectedInstalledPlugin = null;
                ListViewPlugins.Focus();
            }
        }

        private async Task RefreshInstalledVersionsOnOpenAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _manager!.RefreshVersionsAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                log.Debug("Initial marketplace version refresh canceled.");
            }
            catch (Exception ex)
            {
                log.Error("Initial marketplace version refresh failed.", ex);
            }
        }

        private async void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != MainTabControl) return;

            if (_manager == null)
                return;

            CancellationTokenSource operationCancellation = CreateLinkedOperationCancellation(ref _selectionCancellation);
            CancellationToken cancellationToken = operationCancellation.Token;

            try
            {
                _manager.IsMarketplaceTabActive = MainTabControl.SelectedIndex == 1;

                if (_manager.IsMarketplaceTabActive)
                {
                    await _manager.EnsureMarketplaceCatalogLoadedAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                log.Debug("Marketplace tab selection refresh canceled.");
            }
            catch (Exception ex)
            {
                log.Error("Marketplace tab selection refresh failed.", ex);
            }
            finally
            {
                ClearOperationCancellation(ref _selectionCancellation, operationCancellation);
            }
        }

        private async void RefreshVersionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRefreshingVersions)
                return;

            CancellationTokenSource operationCancellation = CreateLinkedOperationCancellation(ref _refreshCancellation);
            CancellationToken cancellationToken = operationCancellation.Token;

            _isRefreshingVersions = true;
            if (sender is Button button)
                button.IsEnabled = false;

            try
            {
                await MarketplaceManager.GetInstance().RefreshVersionsAsync(cancellationToken, forceRefresh: true);

                if (_manager?.IsMarketplaceTabActive == true)
                {
                    await _manager.RefreshMarketplaceCatalogAsync(cancellationToken);
                }

                await RefreshCurrentDetailAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                log.Debug("Marketplace version refresh canceled.");
            }
            catch (Exception ex)
            {
                log.Error("RefreshVersionsButton_Click failed.", ex);
            }
            finally
            {
                if (sender is Button button2)
                    button2.IsEnabled = true;
                _isRefreshingVersions = false;
                ClearOperationCancellation(ref _refreshCancellation, operationCancellation);
            }
        }

        private Task RenderMarkdownAsync(Microsoft.Web.WebView2.Wpf.WebView2 webView, string? markdown, string emptyMessage, CancellationToken cancellationToken)
        {
            return MarketplaceMarkdownPresenter.RenderAsync(webView, markdown, emptyMessage, cancellationToken);
        }

        private async Task RefreshCurrentDetailAsync(CancellationToken cancellationToken = default)
        {
            CancellationTokenSource operationCancellation = CreateLinkedOperationCancellation(ref _detailRefreshCancellation, cancellationToken);
            try
            {
                UpdateDetailPane();
                object? detailContext = _manager?.CurrentDetailContext;
                await RefreshSelectedDetailAsync(detailContext, operationCancellation.Token);
            }
            finally
            {
                ClearOperationCancellation(ref _detailRefreshCancellation, operationCancellation);
            }
        }

        private async Task RefreshSelectedDetailAsync(object? detailContext, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (detailContext)
            {
                case PluginInfoVM pluginInfoVM:
                    SetDetailPanelMode(DetailPanelMode.Installed);
                    MarketplaceDetailScrollViewer.DataContext = null;
                    await RefreshInstalledPluginDetailAsync(pluginInfoVM, cancellationToken);
                    break;
                case MarketplaceDetailContext marketplaceDetail:
                    SetDetailPanelMode(DetailPanelMode.Marketplace);
                    MarketplaceDetailScrollViewer.DataContext = marketplaceDetail;
                    await RefreshMarketplaceDetailAsync(marketplaceDetail, cancellationToken);
                    break;
                default:
                    SetDetailPanelMode(DetailPanelMode.None);
                    MarketplaceDetailScrollViewer.DataContext = null;
                    DetailInfo.Children.Clear();
                    _installedDetailPlugin = null;
                    break;
            }
        }

        private async Task RefreshMarketplaceDetailAsync(MarketplaceDetailContext context, CancellationToken cancellationToken)
        {
            if (TabControl1.SelectedIndex == 0)
            {
                await RenderMarkdownAsync(webViewMarkdown, context.Readme, DesktopResources.MarketplaceReadmeEmpty, cancellationToken);
            }

            if (TabControl1.SelectedIndex == 1)
            {
                await RenderMarkdownAsync(webViewMarkdown, context.ChangeLog, DesktopResources.MarketplaceChangelogEmpty, cancellationToken);
            }
        }

        private async Task RefreshInstalledPluginDetailAsync(PluginInfoVM pluginInfoVM, CancellationToken cancellationToken)
        {
            if (TabControl1.SelectedIndex == 0)
            {
                await RenderMarkdownAsync(webViewMarkdown, pluginInfoVM.PluginInfo?.README, DesktopResources.MarketplaceReadmeEmpty, cancellationToken);
            }

            if (TabControl1.SelectedIndex == 1)
            {
                await RenderMarkdownAsync(webViewMarkdown, pluginInfoVM.PluginInfo?.ChangeLog, DesktopResources.MarketplaceChangelogEmpty, cancellationToken);
            }

            if (TabControl1.SelectedIndex == 2 && !ReferenceEquals(_installedDetailPlugin, pluginInfoVM))
            {
                InitDetailInfo(pluginInfoVM);
                _installedDetailPlugin = pluginInfoVM;
            }

        }

        private void InitDetailInfo(PluginInfoVM pluginInfoVM)
        {
            DetailInfo.Children.Clear();

            Button CreateActionButton(string icon, string title, string? description, ICommand? command, string? toolTip = null)
            {
                var content = new Grid();
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });

                var iconText = new TextBlock
                {
                    Text = icon,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                    FontSize = 18,
                    VerticalAlignment = VerticalAlignment.Center
                };
                iconText.SetResourceReference(TextBlock.ForegroundProperty, "Marketplace.Accent");
                content.Children.Add(iconText);

                var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
                text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
                if (!string.IsNullOrWhiteSpace(description))
                {
                    var subtitle = new TextBlock { Text = description, FontSize = 12, Margin = new Thickness(0, 4, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
                    subtitle.SetResourceReference(TextBlock.ForegroundProperty, "Marketplace.TextSecondary");
                    text.Children.Add(subtitle);
                }
                Grid.SetColumn(text, 1);
                content.Children.Add(text);

                var chevron = new TextBlock
                {
                    Text = "\uE76C",
                    FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                chevron.SetResourceReference(TextBlock.ForegroundProperty, "Marketplace.TextSecondary");
                Grid.SetColumn(chevron, 2);
                content.Children.Add(chevron);

                var button = new Button
                {
                    Content = content,
                    Command = command,
                    ToolTip = toolTip ?? title,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                System.Windows.Automation.AutomationProperties.SetName(button, title);
                button.SetResourceReference(StyleProperty, "MarketplaceDetailActionButtonStyle");
                return button;
            }

            void AddSection(string title, StackPanel actions)
            {
                if (actions.Children.Count == 0)
                    return;

                DetailInfo.Children.Add(new TextBlock
                {
                    Text = title,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, DetailInfo.Children.Count == 0 ? 0 : 16, 0, 12)
                });
                DetailInfo.Children.Add(actions);
            }

            if (pluginInfoVM.PluginInfo.Assembly is Assembly assembly)
            {
                Type[] types = assembly.GetTypes();
                var features = new StackPanel();
                foreach (Type type in types.Where(t => typeof(IMenuItem).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null))
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is IMenuItem menuItem)
                        {
                            string title = string.IsNullOrWhiteSpace(menuItem.Header) ? type.Name : menuItem.Header;
                            string? description = type.GetCustomAttribute<DescriptionAttribute>()?.Description ?? menuItem.InputGestureText;
                            features.Children.Add(CreateActionButton("\uE8A5", title, description, menuItem.Command));
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"Create plugin IMenuItem failed: {type.FullName}: {ex.Message}");
                    }
                }
                AddSection(DesktopResources.MarketplaceFeatureActions, features);

                var shortcuts = new StackPanel();
                foreach (Type type in types.Where(t => typeof(IFeatureLauncher).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is IFeatureLauncher feature)
                        {
                            var command = new RelayCommand(_ =>
                            {
                                string executablePath = Environments.GetExecutablePath();
                                string? shortcutName = feature.Header;
                                string shortcutPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                                string arguments = $"-feature {shortcutName}";
                                if (shortcutName != null)
                                    ShortcutCreator.CreateShortcut(shortcutName, shortcutPath, executablePath, arguments);
                            });
                            string title = string.IsNullOrWhiteSpace(feature.Header) ? type.Name : feature.Header;
                            shortcuts.Children.Add(CreateActionButton("\uE8A7", title, DesktopResources.Marketplace_CreateShortcut, command, feature.Description));
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"Create plugin IFeatureLauncher failed: {type.FullName}: {ex.Message}");
                    }
                }
                AddSection(DesktopResources.MarketplaceShortcuts, shortcuts);

                var settings = new StackPanel();
                foreach (Type type in types.Where(t => typeof(IConfig).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    try
                    {
                        IConfig config = ConfigHandler.GetInstance().GetRequiredService(type);
                        var command = new RelayCommand(_ => new PropertyEditorWindow(config).Show());
                        string? displayName = type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
                        string title = string.IsNullOrWhiteSpace(displayName) ? type.Name : displayName;
                        string? description = title != type.Name ? type.Name : type.GetCustomAttribute<DescriptionAttribute>()?.Description;
                        settings.Children.Add(CreateActionButton("\uE713", title, description, command, string.Format(DesktopResources.Marketplace_OpenConfig, type.Name)));
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"Create plugin IConfig failed: {type.FullName}: {ex.Message}");
                    }
                }
                AddSection(DesktopResources.MarketplacePluginSettings, settings);
            }

            if (DetailInfo.Children.Count == 0)
            {
                var emptyState = new TextBlock
                {
                    Text = DesktopResources.MarketplaceNoPluginActions,
                    Margin = new Thickness(24, 40, 24, 40),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };
                emptyState.SetResourceReference(TextBlock.ForegroundProperty, "Marketplace.TextSecondary");
                DetailInfo.Children.Add(emptyState);
            }
        }

        private async void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.Source != TabControl1) return;

            try
            {
                await RefreshCurrentDetailAsync();
            }
            catch (OperationCanceledException)
            {
                log.Debug("Marketplace detail tab refresh canceled.");
            }
            catch (Exception ex)
            {
                log.Error("Marketplace detail tab refresh failed.", ex);
            }
        }

        private async void Manager_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MarketplaceManager.HasCurrentSelection))
            {
                UpdateDetailPane();
                return;
            }

            if (e.PropertyName != nameof(MarketplaceManager.CurrentDetailContext))
                return;

            try
            {
                await RefreshCurrentDetailAsync();
            }
            catch (OperationCanceledException)
            {
                log.Debug("Marketplace manager detail refresh canceled.");
            }
            catch (Exception ex)
            {
                log.Error("Marketplace manager detail refresh failed.", ex);
            }
        }

        private void SetDetailPanelMode(DetailPanelMode mode)
        {
            bool showActions = TabControl1.SelectedIndex == 2;
            webViewMarkdown.Visibility = mode != DetailPanelMode.None && !showActions ? Visibility.Visible : Visibility.Hidden;
            InstalledDetailScrollViewer.Visibility = mode == DetailPanelMode.Installed && showActions ? Visibility.Visible : Visibility.Collapsed;
            MarketplaceDetailScrollViewer.Visibility = mode == DetailPanelMode.Marketplace && showActions ? Visibility.Visible : Visibility.Collapsed;
        }

        private CancellationTokenSource CreateLinkedOperationCancellation(ref CancellationTokenSource? operationCancellation, CancellationToken cancellationToken = default)
        {
            CancelAndDispose(ref operationCancellation);
            CancellationToken windowToken = _windowCancellation?.Token ?? CancellationToken.None;
            operationCancellation = cancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(windowToken, cancellationToken)
                : CancellationTokenSource.CreateLinkedTokenSource(windowToken);
            return operationCancellation;
        }

        private static void ClearOperationCancellation(ref CancellationTokenSource? currentCancellation, CancellationTokenSource operationCancellation)
        {
            if (!ReferenceEquals(currentCancellation, operationCancellation))
                return;

            operationCancellation.Dispose();
            currentCancellation = null;
        }

        private static void CancelAndDispose(ref CancellationTokenSource? cancellationTokenSource)
        {
            if (cancellationTokenSource == null)
                return;

            try
            {
                cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }
    }
}
