using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ColorVision.Plugins
{

    /// <summary>
    /// PluginManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PluginManagerWindow : Window
    {

        public PluginManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            this.SizeChanged += (s, e) => PluginWindowConfig.Instance.SetConfig(this);
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            this.DataContext = PluginManager.GetInstance(); ;
            DefalutSearchComboBox.ItemsSource = new List<string>() { "Pattern", "EventVWR", "ScreenRecorder", "SystemMonitor", "WindowsServicePlugin"};
            ListViewPlugins.SelectedIndex = 0;
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => PluginManager.GetInstance().Plugins[ListViewPlugins.SelectedIndex].Delete(), (s, e) => e.CanExecute = ListViewPlugins.SelectedIndex > -1));
        }

        private bool IsRefreshChangedX;
        private bool IsRefreshChangedY;


        private void ListViewPlugins_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ListViewPlugins.SelectedIndex > -1)
            {
                IsRefreshChangedX = false;
                IsRefreshChangedY = false;
                PluginInfoVM pluginInfoVM = PluginManager.GetInstance().Plugins[ListViewPlugins.SelectedIndex];
                BorderContent.DataContext = pluginInfoVM;
                Application.Current.Dispatcher.Invoke(async () =>
                {
                    if (TabControl1.SelectedIndex == 0 && !IsRefreshChangedX)
                    {
                        IsRefreshChangedX = true;
                        string html = Markdig.Markdown.ToHtml(pluginInfoVM.PluginInfo?.README ?? string.Empty);
                        await WebViewService.EnsureWebViewInitializedAsync(webViewReadMe);
                        WebViewService.RenderMarkdown(webViewReadMe, html);
                    }

                    if (TabControl1.SelectedIndex == 1)
                    {
                        IsRefreshChangedY = true;
                        string htm2 = Markdig.Markdown.ToHtml(pluginInfoVM.PluginInfo?.ChangeLog ?? string.Empty);
                        await WebViewService.EnsureWebViewInitializedAsync(webViewChangeLog);
                        WebViewService.RenderMarkdown(webViewChangeLog, htm2);
                    }
                    if (TabControl1.SelectedIndex == 2)
                    {
                        InitDetailInfo(pluginInfoVM);
                    }
                });
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


                        var button = new Button
                        {
                            Style = PropertyEditorHelper.ButtonCommandStyle,
                            Content = type.Name,
                            Command = relayCommand
                        };
                        uniformGrid.Children.Add(button);
                    }
                    stackPanel.Children.Add(uniformGrid);
                }


                GenIMenuItem(DetailInfo, pluginInfoVM.PluginInfo.Assembly);
                GenIConfig(DetailInfo, pluginInfoVM.PluginInfo.Assembly);

            }
        }

        private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ListViewPlugins.SelectedIndex > -1)
            {
                PluginInfoVM pluginInfoVM = PluginManager.GetInstance().Plugins[ListViewPlugins.SelectedIndex];
                BorderContent.DataContext = pluginInfoVM;
                Application.Current.Dispatcher.Invoke(async () =>
                {
                    if (TabControl1.SelectedIndex == 0 && !IsRefreshChangedX)
                    {
                        IsRefreshChangedX = true;
                        string html = Markdig.Markdown.ToHtml(pluginInfoVM.PluginInfo?.README ?? string.Empty);
                        await WebViewService.EnsureWebViewInitializedAsync(webViewReadMe);
                        WebViewService.RenderMarkdown(webViewReadMe, html);
                    }

                    if (TabControl1.SelectedIndex == 1 && !IsRefreshChangedY)
                    {
                        IsRefreshChangedY = true;
                        string htm2 = Markdig.Markdown.ToHtml(pluginInfoVM.PluginInfo?.ChangeLog ?? string.Empty);
                        await WebViewService.EnsureWebViewInitializedAsync(webViewChangeLog);
                        WebViewService.RenderMarkdown(webViewChangeLog, htm2);
                    }
                    if (TabControl1.SelectedIndex == 2)
                    {
                        InitDetailInfo(pluginInfoVM);
                    }
                });
            }
        }
    }
}
