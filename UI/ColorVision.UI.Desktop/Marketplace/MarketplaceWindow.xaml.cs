#pragma warning disable CA1001,CA1822,CA1863
using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI.Desktop.NativeMethods;
using ColorVision.UI.Extension;
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

        public MarketplaceWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            this.SizeChanged += (s, e) => MarketplaceWindowConfig.Instance.SetConfig(this);
            Closed += (_, _) =>
            {
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
            DataContext = _manager;
            _manager.PropertyChanged += Manager_PropertyChanged;

            if (_manager.SelectedInstalledPlugin == null)
            {
                _manager.SelectedInstalledPlugin = _manager.Plugins.FirstOrDefault();
            }

            this.CommandBindings.Add(new CommandBinding(
                ApplicationCommands.Delete,
                (s, args) => _manager.SelectedInstalledPlugin?.Delete(),
                (s, args) => args.CanExecute = _manager.SelectedInstalledPlugin != null));
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

                cancellationToken.ThrowIfCancellationRequested();
                await RefreshCurrentDetailAsync(cancellationToken);
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
                await MarketplaceManager.GetInstance().RefreshVersionsAsync(cancellationToken);

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
                    DependentsListView.ItemsSource = null;
                    break;
            }
        }

        private async Task RefreshMarketplaceDetailAsync(MarketplaceDetailContext context, CancellationToken cancellationToken)
        {
            if (TabControl1.SelectedIndex == 0)
            {
                await RenderMarkdownAsync(webViewReadMe, context.Readme, DesktopResources.MarketplaceReadmeEmpty, cancellationToken);
            }

            if (TabControl1.SelectedIndex == 1)
            {
                await RenderMarkdownAsync(webViewChangeLog, context.ChangeLog, DesktopResources.MarketplaceChangelogEmpty, cancellationToken);
            }

            if (TabControl1.SelectedIndex == 3)
            {
                DependentsListView.ItemsSource = null;
            }
        }

        private async Task RefreshInstalledPluginDetailAsync(PluginInfoVM pluginInfoVM, CancellationToken cancellationToken)
        {
            if (TabControl1.SelectedIndex == 0)
            {
                await RenderMarkdownAsync(webViewReadMe, pluginInfoVM.PluginInfo?.README, DesktopResources.MarketplaceReadmeEmpty, cancellationToken);
            }

            if (TabControl1.SelectedIndex == 1)
            {
                await RenderMarkdownAsync(webViewChangeLog, pluginInfoVM.PluginInfo?.ChangeLog, DesktopResources.MarketplaceChangelogEmpty, cancellationToken);
            }

            if (TabControl1.SelectedIndex == 2)
            {
                InitDetailInfo(pluginInfoVM);
            }

            if (TabControl1.SelectedIndex == 3)
            {
                DependentsListView.ItemsSource = null;
                if (pluginInfoVM.PluginInfo?.DepsJson != null)
                {
                    var target = pluginInfoVM.PluginInfo.DepsJson.Targets.Values.First();
                    if (target != null)
                    {
                        var mainPackage = target.Values.FirstOrDefault();
                        var dependencies = mainPackage?.Dependencies;
                        DependentsListView.ItemsSource = dependencies;
                    }
                }
            }
        }

        private void InitDetailInfo(PluginInfoVM pluginInfoVM)
        {
            DetailInfo.Children.Clear();

            if (pluginInfoVM.PluginInfo.Assembly != null)
            {
                void GenIMenuItem(StackPanel stackPanel,Assembly assembly)
                {
                    UniformGrid uniformGrid = new UniformGrid() { Margin = new Thickness(5) };
                    uniformGrid.SizeChanged += (_, __) => uniformGrid.AutoUpdateLayout();
                    foreach (Type type in assembly.GetTypes().Where(t => typeof(IMenuItem).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null))
                    {
                        try
                        {
                            if (Activator.CreateInstance(type) is IMenuItem menuItems)
                            {
                                var button = new Button
                                {
                                    Style = PropertyEditorHelper.ButtonCommandStyle,
                                    Content = menuItems.Header,
                                    Command = menuItems.Command
                                };
                                uniformGrid.Children.Add(button);
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Warn($"Create plugin IMenuItem failed: {type.FullName}: {ex.Message}");
                        }
                    }
                    stackPanel.Children.Add(uniformGrid);
                }
                void GenIConfig(StackPanel stackPanel, Assembly assembly)
                {
                    UniformGrid uniformGrid = new UniformGrid() { Margin = new Thickness(5) };
                    uniformGrid.SizeChanged += (_, __) => uniformGrid.AutoUpdateLayout();
                    foreach (Type type in assembly.GetTypes().Where(t => typeof(IConfig).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        IConfig config = ConfigHandler.GetInstance().GetRequiredService(type);

                        RelayCommand relayCommand = new RelayCommand(a =>
                        {
                            new PropertyEditorWindow(config).Show();
                        });

                        var textBox = new TextBlock
                        {
                            Text = string.Join("\u200B", type.Name.ToCharArray()) ,
                            TextWrapping = TextWrapping.WrapWithOverflow,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        };
                        var button = new Button
                        {
                            Style = PropertyEditorHelper.ButtonCommandStyle,
                            Content = textBox,
                            Command = relayCommand,
                            ToolTip = string.Format(Properties.Resources.Marketplace_OpenConfig, type.Name)
                        };
                        uniformGrid.Children.Add(button);
                    }
                    stackPanel.Children.Add(uniformGrid);
                }

                void GenIFeatureLauncher(StackPanel stackPanel, Assembly assembly)
                {
                    UniformGrid uniformGrid = new UniformGrid() { Margin = new Thickness(5) };
                    uniformGrid.SizeChanged += (_, __) => uniformGrid.AutoUpdateLayout();
                    foreach (Type type in assembly.GetTypes().Where(t => typeof(IFeatureLauncher).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        if (Activator.CreateInstance(type) is IFeatureLauncher menuItems)
                        {
                            RelayCommand relayCommand = new RelayCommand(a =>
                            {
                                string GetExecutablePath = Environments.GetExecutablePath();
                                string shortcutName = menuItems.Header;
                                string shortcutPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                                string arguments = $"-feature {shortcutName}";
                                if (shortcutName != null)
                                   ShortcutCreator.CreateShortcut(shortcutName, shortcutPath, GetExecutablePath, arguments);
                            });

                            var button = new Button
                            {
                                Style = PropertyEditorHelper.ButtonCommandStyle,
                                Content = Properties.Resources.Marketplace_CreateShortcut,
                                Command = relayCommand
                            };
                            uniformGrid.Children.Add(button);
                        }
                    }
                    stackPanel.Children.Add(uniformGrid);
                }


                GenIMenuItem(DetailInfo, pluginInfoVM.PluginInfo.Assembly);
                GenIFeatureLauncher(DetailInfo, pluginInfoVM.PluginInfo.Assembly);
                GenIConfig(DetailInfo, pluginInfoVM.PluginInfo.Assembly);

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
            InstalledDetailScrollViewer.Visibility = mode == DetailPanelMode.Installed ? Visibility.Visible : Visibility.Collapsed;
            MarketplaceDetailScrollViewer.Visibility = mode == DetailPanelMode.Marketplace ? Visibility.Visible : Visibility.Collapsed;
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
