using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI.Extension;
using ColorVision.UI.Marketplace;
using ColorVision.UI.Menus;
using log4net;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ColorVision.UI.Desktop.NativeMethods;

namespace ColorVision.UI.Desktop.Plugins
{

    /// <summary>
    /// PluginManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PluginManagerWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginManagerWindow));

        private List<MarketplacePluginSummary> _marketplacePlugins = new();
        private bool _marketplaceLoaded;
        private int _marketplaceDetailRequestId;

        public PluginManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            this.SizeChanged += (s, e) => PluginWindowConfig.Instance.SetConfig(this);
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            this.DataContext = PluginManager.GetInstance(); ;
            DefalutSearchComboBox.ItemsSource = new List<string>() { "ImageProjector", "Pattern", "EventVWR", "ScreenRecorder", "SystemMonitor", "WindowsServicePlugin", "Spectrum" };
            ListViewPlugins.SelectedIndex = 0;
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => PluginManager.GetInstance().Plugins[ListViewPlugins.SelectedIndex].Delete(), (s, e) => e.CanExecute = ListViewPlugins.SelectedIndex > -1));
        }

        private void SearchComboBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // When the marketplace tab is active and loaded, filter the marketplace list
            if (MainTabControl.SelectedIndex == 1 && _marketplaceLoaded)
            {
                FilterMarketplacePlugins(DefalutSearchComboBox.Text);
            }
        }

        private bool IsRefreshChangedX;
        private bool IsRefreshChangedY;

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != MainTabControl) return;
            _marketplaceDetailRequestId++;
            if (MainTabControl.SelectedIndex == 1 && !_marketplaceLoaded)
            {
                LoadMarketplacePlugins();
            }
        }

        private async void LoadMarketplacePlugins()
        {
            _marketplaceLoaded = true;
            MarketplaceStatus.Text = Properties.Resources.Loading + "...";
            try
            {
                var client = Marketplace.MarketplaceClient.GetInstance();
                var result = await client.SearchPluginsAsync(new MarketplaceSearchRequest { PageSize = 100 });
                _marketplacePlugins = result.Items;
                ListViewMarketplace.ItemsSource = _marketplacePlugins;
                MarketplaceStatus.Text = string.Format(Properties.Resources.MarketplacePluginCount, _marketplacePlugins.Count);

                // Update search ComboBox with marketplace plugin IDs
                if (_marketplacePlugins.Count > 0)
                {
                    DefalutSearchComboBox.ItemsSource = _marketplacePlugins.Select(p => p.PluginId).ToList();
                }
            }
            catch (Exception ex)
            {
                log.Debug($"LoadMarketplacePlugins failed: {ex.Message}");
                MarketplaceStatus.Text = Properties.Resources.MarketplaceLoadFailed;
            }
        }

        /// <summary>
        /// Filter the marketplace list based on the current search text.
        /// Called when the search box text changes while the Marketplace tab is active.
        /// </summary>
        private void FilterMarketplacePlugins(string keyword)
        {
            if (_marketplacePlugins == null || _marketplacePlugins.Count == 0) return;

            if (string.IsNullOrWhiteSpace(keyword))
            {
                ListViewMarketplace.ItemsSource = _marketplacePlugins;
                MarketplaceStatus.Text = string.Format(Properties.Resources.MarketplacePluginCount, _marketplacePlugins.Count);
            }
            else
            {
                var kw = keyword.Trim().ToLowerInvariant();
                var filtered = _marketplacePlugins.Where(p =>
                    (p.Name?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.PluginId?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Description?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Author?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Category?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
                ListViewMarketplace.ItemsSource = filtered;
                MarketplaceStatus.Text = string.Format(Properties.Resources.MarketplacePluginCount, filtered.Count);
            }
        }

        private void ListViewMarketplace_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewMarketplace.SelectedIndex > -1 && ListViewMarketplace.SelectedItem is MarketplacePluginSummary summary)
            {
                IsRefreshChangedX = false;
                IsRefreshChangedY = false;
                // Show marketplace detail in the right panel using a temporary PluginInfoVM-like data context
                ShowMarketplaceDetail(summary);
            }
        }

        private async void ShowMarketplaceDetail(MarketplacePluginSummary summary)
        {
            int requestId = ++_marketplaceDetailRequestId;
            try
            {
                var client = Marketplace.MarketplaceClient.GetInstance();
                var detail = await client.GetPluginDetailAsync(summary.PluginId);
                if (detail == null) return;

                if (requestId != _marketplaceDetailRequestId)
                    return;

                // Check if this plugin is installed locally
                var installed = PluginManager.GetInstance().Plugins.FirstOrDefault(p => string.Equals(p.PackageName, summary.PluginId, StringComparison.OrdinalIgnoreCase));
                var ctx = new MarketplaceDetailContext(detail, installed);
                BorderContent.DataContext = ctx;

                await ctx.InitializeAsync();
                if (requestId != _marketplaceDetailRequestId)
                    return;

                await RefreshSelectedDetailAsync();
            }
            catch (Exception ex)
            {
                log.Debug($"ShowMarketplaceDetail failed: {ex.Message}");
            }
        }

        private async Task RenderMarkdownAsync(Microsoft.Web.WebView2.Wpf.WebView2 webView, string markdown)
        {
            string html = Markdig.Markdown.ToHtml(markdown ?? string.Empty);
            await WebViewService.EnsureWebViewInitializedAsync(webView);
            WebViewService.RenderMarkdown(webView, html);
        }

        private async Task RefreshSelectedDetailAsync()
        {
            switch (BorderContent.DataContext)
            {
                case PluginInfoVM pluginInfoVM:
                    await RefreshInstalledPluginDetailAsync(pluginInfoVM);
                    break;
                case MarketplaceDetailContext marketplaceDetail:
                    await RefreshMarketplaceDetailAsync(marketplaceDetail);
                    break;
                default:
                    DetailInfo.Children.Clear();
                    DependentsListView.ItemsSource = null;
                    break;
            }
        }

        private async Task RefreshMarketplaceDetailAsync(MarketplaceDetailContext context)
        {
            if (TabControl1.SelectedIndex == 0 && !IsRefreshChangedX)
            {
                IsRefreshChangedX = true;
                await RenderMarkdownAsync(webViewReadMe, context.Readme ?? string.Empty);
            }

            if (TabControl1.SelectedIndex == 1 && !IsRefreshChangedY)
            {
                IsRefreshChangedY = true;
                await RenderMarkdownAsync(webViewChangeLog, context.ChangeLog ?? string.Empty);
            }

            if (TabControl1.SelectedIndex == 2)
            {
                context.PopulateDetailInfo(DetailInfo, DependentsListView);
            }

            if (TabControl1.SelectedIndex == 3)
            {
                DependentsListView.ItemsSource = null;
            }
        }

        private async Task RefreshInstalledPluginDetailAsync(PluginInfoVM pluginInfoVM)
        {
            if (TabControl1.SelectedIndex == 0 && !IsRefreshChangedX)
            {
                IsRefreshChangedX = true;
                await RenderMarkdownAsync(webViewReadMe, pluginInfoVM.PluginInfo?.README ?? string.Empty);
            }

            if (TabControl1.SelectedIndex == 1 && !IsRefreshChangedY)
            {
                IsRefreshChangedY = true;
                await RenderMarkdownAsync(webViewChangeLog, pluginInfoVM.PluginInfo?.ChangeLog ?? string.Empty);
            }

            if (TabControl1.SelectedIndex == 2)
            {
                InitDetailInfo(pluginInfoVM);
            }

            if (TabControl1.SelectedIndex == 3)
            {
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

        private void ListViewPlugins_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ListViewPlugins.SelectedIndex > -1)
            {
                _marketplaceDetailRequestId++;
                IsRefreshChangedX = false;
                IsRefreshChangedY = false;
                PluginInfoVM pluginInfoVM = PluginManager.GetInstance().Plugins[ListViewPlugins.SelectedIndex];
                BorderContent.DataContext = pluginInfoVM;
                Application.Current.Dispatcher.Invoke(async () => await RefreshSelectedDetailAsync());
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
                    foreach (Type type in assembly.GetTypes().Where(t => typeof(IMenuItem).IsAssignableFrom(t) && !t.IsAbstract))
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
                            ToolTip = $"点击打开 {type.Name} 配置界面"
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
                                Content = "创建快捷方式",
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

        private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.Source != TabControl1) return;

            Application.Current.Dispatcher.Invoke(async () => await RefreshSelectedDetailAsync());
        }
    }
}
