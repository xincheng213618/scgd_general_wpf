using ColorVision.Themes;
using ColorVision.UI;
using Markdig;
using System.Collections.Generic;
using System.Windows;
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
            DefalutSearchComboBox.ItemsSource = new List<string>() { "ColorVisonChat", "EventVWR", "ScreenRecorder", "SystemMonitor", "WindowsServicePlugin" };
            ListViewPlugins.SelectedIndex = 0;
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => PluginManager.GetInstance().Plugins[ListViewPlugins.SelectedIndex].Delete(), (s, e) => e.CanExecute = ListViewPlugins.SelectedIndex > -1));
        }

        private void ListViewPlugins_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ListViewPlugins.SelectedIndex > -1)
            {
                PluginInfoVM pluginInfoVM = PluginManager.GetInstance().Plugins[ListViewPlugins.SelectedIndex];
                BorderContent.DataContext = pluginInfoVM;
                Application.Current.Dispatcher.Invoke(async () =>
                {
                    string html = Markdig.Markdown.ToHtml(pluginInfoVM.PluginInfo?.README ??string.Empty);
                    await WebViewService.EnsureWebViewInitializedAsync(webViewReadMe);
                    WebViewService.RenderMarkdown(webViewReadMe, html);

                    string htm2 = Markdig.Markdown.ToHtml(pluginInfoVM.PluginInfo?.ChangeLog ?? string.Empty);
                    await WebViewService.EnsureWebViewInitializedAsync(webViewChangeLog);
                    WebViewService.RenderMarkdown(webViewChangeLog, htm2);
                });
            }
        }
    }
}
