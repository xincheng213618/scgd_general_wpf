using ColorVision.Themes;
using ColorVision.UI.Menus;
using ColorVision.Update;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Xml.Linq;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class HelpKeyboardNavigationTests
{
    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en")]
    [InlineData("zh-TW")]
    public void CommonHelpEntriesHaveDistinctAccessKeysAndKeepDescriptiveWindowTitles(string cultureName)
    {
        WpfTestHost.Invoke(() =>
        {
            var previous = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
                IMenuItem[] entries = [new MenuCheckAndUpdateV1(), new AboutMsgExport(),
                    new ColorVision.UI.LogImp.MenuLogWindow(), new NativeLogging.MenuNativeLog(),
                    new ColorVision.ServiceHost.MenuServiceHostManager(), new ColorVision.UI.Desktop.Feedback.MenuSendFeedback(),
                    new ColorVision.UI.Desktop.Marketplace.MenuPluginManager(), new ColorVision.SocketProtocol.MenuProjectManager()];
                char[] keys = entries.Select(entry => char.ToUpperInvariant(new AccessText { Text = entry.Header }.AccessKey)).ToArray();
                Assert.DoesNotContain('\0', keys);
                Assert.Equal(keys.Length, keys.Distinct().Count());
                Assert.Equal('U', keys[0]);
                Assert.Equal('C', keys[^1]);
                Assert.Contains("Socket", ColorVision.SocketProtocol.Properties.Resources.SocketManagementWindow);
                if (cultureName == "zh-CN")
                    Assert.Equal("网络通信(_C)", entries[^1].Header);
            }
            finally { CultureInfo.CurrentUICulture = previous; }
        });
    }

    [Theory]
    [InlineData(true, false, false, false, "none")]
    [InlineData(false, false, false, false, "close")]
    [InlineData(false, true, true, false, "update")]
    [InlineData(false, true, false, false, "none")]
    [InlineData(false, true, true, true, "none")]
    public void ActualUpdateButtonsRouteEnterAndEscapeAccordingToCurrentState(bool checking, bool hasItem, bool selected, bool busy, string expectedDefault)
    {
        WpfTestHost.Invoke(() =>
        {
            var context = new UpdatePreviewDialogContext { IsChecking = checking, IsUpdating = busy };
            if (hasItem)
                context.Items.Add(new UpdatePreviewItem { Kind = UpdatePreviewItemKind.Plugin, IsSelectable = true, IsSelected = selected });
            Window window = CreateUpdateButtonHost(context);
            var confirm = (Button)window.FindName("ConfirmButton");
            var cancel = (Button)window.FindName("CancelButton");
            int confirms = 0, cancels = 0;
            confirm.Click += (_, e) => { confirms++; e.Handled = true; };
            cancel.Click += (_, e) => { cancels++; e.Handled = true; };
            try
            {
                window.Show();
                Drain();
                Assert.Equal(expectedDefault == "update", confirm.IsDefault);
                Assert.Equal(expectedDefault == "close", cancel.IsDefault);
                AccessKeyManager.ProcessKey(PresentationSource.FromVisual(window), "\r", false);
                Assert.Equal(expectedDefault == "update" ? 1 : 0, confirms);
                Assert.Equal(expectedDefault == "close" ? 1 : 0, cancels);
                int beforeEscape = cancels;
                AccessKeyManager.ProcessKey(PresentationSource.FromVisual(window), "\x1b", false);
                Assert.Equal(beforeEscape + (busy ? 0 : 1), cancels);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ExistingButtonsFollowCheckCompletionSelectionAndBusyTransitions()
    {
        WpfTestHost.Invoke(() =>
        {
            var context = new UpdatePreviewDialogContext { IsChecking = true };
            Window window = CreateUpdateButtonHost(context);
            var confirm = (Button)window.FindName("ConfirmButton");
            var cancel = (Button)window.FindName("CancelButton");
            try
            {
                window.Show();
                var result = new UpdatePreviewDialogContext();
                result.Items.Add(new UpdatePreviewItem { Kind = UpdatePreviewItemKind.Plugin, IsSelectable = true, IsSelected = true });
                context.CopyFrom(result);
                Drain();
                Assert.True(confirm.IsDefault);
                Assert.False(cancel.IsDefault);
                context.Items[0].IsSelected = false;
                Drain();
                Assert.False(confirm.IsDefault);
                Assert.False(cancel.IsDefault);
                context.Items.Clear();
                Drain();
                Assert.True(cancel.IsDefault);
                context.IsUpdating = true;
                Drain();
                Assert.False(cancel.IsDefault);
                Assert.False(cancel.IsEnabled);
                context.IsUpdating = false;
                Drain();
                Assert.True(cancel.IsDefault);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void EscapeHonorsLocalControlsBeforeClosingOrRunningTheWindowAction()
    {
        WpfTestHost.Invoke(() =>
        {
            var input = new TextBox();
            var window = new Window { Content = input, ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
            int escapes = 0;
            WindowKeyboardNavigation.Attach(window, input, () => escapes++);
            KeyEventHandler localHandler = (_, e) => e.Handled = true;
            try
            {
                window.Show();
                input.KeyDown += localHandler;
                RaiseEscape(input, window);
                Assert.Equal(0, escapes);
                input.KeyDown -= localHandler;
                RaiseEscape(input, window);
                Assert.Equal(1, escapes);
                Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(window));
            }
            finally { window.Close(); }
        });
    }

    private static void RaiseEscape(UIElement input, Window window) => input.RaiseEvent(new KeyEventArgs(
        Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), Environment.TickCount, Key.Escape)
        { RoutedEvent = Keyboard.KeyDownEvent });

    private static void Drain() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    private static Window CreateUpdateButtonHost(UpdatePreviewDialogContext context, [CallerFilePath] string testPath = "")
    {
        string repo = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testPath)!, "..", ".."));
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement original = XDocument.Load(Path.Combine(repo, "ColorVision/Update/UpdatePreviewWindow.xaml")).Root!;
        var resources = new XElement(original.Element(ns + "Window.Resources")!);
        var buttons = original.Descendants(ns + "Button")
            .Where(button => (string?)button.Attribute(x + "Name") is "ConfirmButton" or "CancelButton")
            .Select(button => new XElement(button)).ToArray();
        foreach (XElement button in buttons)
            button.Attribute("Click")!.Remove();
        var root = new XElement(ns + "Window", new XAttribute(XNamespace.Xmlns + "x", x), resources, new XElement(ns + "StackPanel", buttons));
        var parserContext = new ParserContext { BaseUri = new Uri("pack://application:,,,/ColorVision;component/Update/UpdatePreviewWindow.xaml") };
        var window = (Window)XamlReader.Parse(root.ToString(), parserContext);
        window.DataContext = context;
        window.Width = 400;
        window.Height = 200;
        window.Left = -10000;
        window.Top = -10000;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        return window;
    }
}
