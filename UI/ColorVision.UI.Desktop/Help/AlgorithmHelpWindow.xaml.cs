using ColorVision.Common.Utilities;
using ColorVision.Themes;
using Microsoft.Web.WebView2.Core;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ColorVision.UI.Desktop.Help;

public partial class AlgorithmHelpWindow : Window
{
    private static WeakReference<AlgorithmHelpWindow>? activeWindow;
    private readonly AlgorithmHelpCatalog catalog = new(Path.Combine(AppContext.BaseDirectory, "Assets", "Help"));
    private AlgorithmHelpTopic topic;
    private bool closed;

    public AlgorithmHelpWindow(string topicId)
    {
        topic = catalog.Get(topicId);
        InitializeComponent();
        this.ApplyCaption();
        OnlineButton.Content = Text("AlgorithmHelp_Online");
        CopyLinkButton.Content = Text("AlgorithmHelp_CopyLink");
        PrintButton.Content = Text("AlgorithmHelp_Print");
        StatusText.Text = Text("Loading");
        UpdateTitle();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    public static void ShowTopic(string topicId, Window? owner = null)
    {
        try
        {
            if (activeWindow != null && activeWindow.TryGetTarget(out AlgorithmHelpWindow? existing) && !existing.closed)
            {
                existing.topic = existing.catalog.Get(topicId);
                existing.UpdateTitle();
                existing.Navigate();
                if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                existing.Activate();
                return;
            }
            var window = new AlgorithmHelpWindow(topicId);
            if (owner != null && owner.IsVisible) window.Owner = owner;
            activeWindow = new(window);
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(CultureInfo.CurrentCulture, Text("AlgorithmHelp_Failed"), ex.Message), Text("AlgorithmHelp_Title"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void UpdateTitle()
    {
        Title = topic.Title + " - " + Text("AlgorithmHelp_Title");
        TopicTitle.Text = topic.Title;
        VersionText.Text = string.Format(CultureInfo.CurrentCulture, Text("AlgorithmHelp_Version"), Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? string.Empty);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        try
        {
            Browser.DefaultBackgroundColor = ThemeManager.Current.CurrentUITheme == Theme.Dark ? System.Drawing.Color.FromArgb(24, 24, 27) : System.Drawing.Color.White;
            await WebViewService.EnsureWebViewInitializedAsync(Browser);
            if (closed) return;
            CoreWebView2 core = Browser.CoreWebView2 ?? throw new InvalidOperationException("WebView2 could not be initialized.");
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = false;
            core.SetVirtualHostNameToFolderMapping(AlgorithmHelpCatalog.LocalHost, catalog.RootDirectory, CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting += OnNavigationStarting;
            core.NavigationCompleted += OnNavigationCompleted;
            core.NewWindowRequested += OnNewWindowRequested;
            Navigate();
        }
        catch (Exception ex)
        {
            if (!closed) StatusText.Text = string.Format(CultureInfo.CurrentCulture, Text("AlgorithmHelp_Failed"), ex.Message);
        }
    }

    private void Navigate()
    {
        if (Browser.CoreWebView2 == null || closed) return;
        PrintButton.IsEnabled = false;
        StatusText.Text = Text("Loading");
        StatusText.Visibility = Visibility.Visible;
        Browser.CoreWebView2.Navigate(catalog.GetLocalUri(topic).AbsoluteUri);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps && uri.Host == AlgorithmHelpCatalog.LocalHost)
        {
            try
            {
                AlgorithmHelpTopic destination = catalog.GetByLocalUri(uri);
                if (destination.Id != topic.Id)
                {
                    topic = destination;
                    UpdateTitle();
                }
                return;
            }
            catch (Exception ex)
            {
                e.Cancel = true;
                StatusText.Text = string.Format(CultureInfo.CurrentCulture, Text("AlgorithmHelp_Failed"), ex.Message);
                StatusText.Visibility = Visibility.Visible;
                return;
            }
        }
        e.Cancel = true;
        if (e.IsUserInitiated) RunAction(() => OpenExternal(e.Uri));
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (closed) return;
        PrintButton.IsEnabled = e.IsSuccess;
        StatusText.Visibility = e.IsSuccess ? Visibility.Collapsed : Visibility.Visible;
        if (!e.IsSuccess) StatusText.Text = string.Format(CultureInfo.CurrentCulture, Text("AlgorithmHelp_Failed"), e.WebErrorStatus);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (e.IsUserInitiated) RunAction(() => OpenExternal(e.Uri));
    }

    private static void OpenExternal(string address)
    {
        if (Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && uri.Scheme is "https" or "http") PlatformHelper.Open(uri.AbsoluteUri);
    }

    private void OnlineButton_Click(object sender, RoutedEventArgs e) => RunAction(() => OpenExternal(topic.OnlineUrl));
    private void CopyLinkButton_Click(object sender, RoutedEventArgs e) => RunAction(() => Clipboard.SetText(topic.OnlineUrl));
    private void PrintButton_Click(object sender, RoutedEventArgs e) => RunAction(() => Browser.CoreWebView2?.ShowPrintUI());

    private void RunAction(Action action)
    {
        try { action(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, Text("AlgorithmHelp_Title"), MessageBoxButton.OK, MessageBoxImage.Information); }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        closed = true;
        Browser.Dispose();
    }

    private static string Text(string key) => Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
