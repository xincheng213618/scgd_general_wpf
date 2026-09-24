using ColorVision.Copilot;
using ColorVision.Solution;
using ColorVision.Themes;
using ColorVision.UI.Docking;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotSettingsPresentationTests
{
    [Theory]
    [InlineData(false, 1180)]
    [InlineData(true, 960)]
    public void SettingsPagesAndInlineCreationRenderWithTheme(bool dark, int width)
    {
        RunUi(dark, () =>
        {
            using var fixture = new CopilotSettingsQuickAddTests.Fixture();
            var window = new CopilotSettingsWindow(fixture.ViewModel, CopilotSettingsPage.Models)
            {
                Width = width, Height = 780, ShowInTaskbar = false, ShowActivated = false, Left = -16000, Top = -16000,
            };
            try
            {
                ApplyTheme(window, dark);
                window.Show();
                Pump();
                Capture((FrameworkElement)window.Content, $"settings-{width}-{dark}.png");
                var tabs = (TabControl)window.FindName("SettingsTabs");
                foreach (TabItem item in tabs.Items)
                {
                    tabs.SelectedItem = item;
                    Pump();
                    Assert.True(((FrameworkElement)item.Content).ActualWidth > 0);
                }
                tabs.SelectedIndex = 0;
                fixture.ViewModel.PrepareAddModelDialog();
                fixture.ViewModel.IsAddingModel = true;
                Pump();
                Capture((FrameworkElement)window.Content, $"providers-{width}-{dark}.png");
                fixture.ViewModel.SelectConnectProviderCommand.Execute(fixture.ViewModel.ConnectProviderOptions.Single(x => x.VendorType == CopilotVendorType.Custom));
                Pump();
                Capture((FrameworkElement)window.Content, $"add-model-{width}-{dark}.png");
                fixture.ViewModel.CancelAddModel();
                Pump();
                Assert.False(fixture.ViewModel.IsAddingModel);
                Assert.Single(fixture.ViewModel.Profiles);
                Assert.False(File.Exists(fixture.Path));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(420)]
    [InlineData(840)]
    public void ComposerKeepsModelActionsVisibleAndContributesSettingsToDockTitle(int width)
    {
        RunUi(true, () =>
        {
            var instanceField = typeof(SolutionManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
            var previous = instanceField.GetValue(null);
            instanceField.SetValue(null, RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager)));
            using var fixture = new CopilotSettingsQuickAddTests.Fixture();
            var profile = fixture.Config.Profiles[0];
            var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.Name);
            conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "已整理当前检查结果，可以继续提问。"));
            var state = new CopilotChatState { ActiveProfileId = profile.Id, ActiveConversationId = conversation.Id, Conversations = [conversation] };
            var host = new CopilotAgentTaskHost();
            using var vm = new CopilotChatViewModel(new CopilotChatService(), new MemoryStateStore(state), fixture.Config, new UnusedRuntime(), host);
            var panel = new CopilotChatPanel { DataContext = vm };
            var window = new Window { Content = panel, Width = width, Height = 640, ShowInTaskbar = false, ShowActivated = false, Left = -16000, Top = -16000 };
            try
            {
                ApplyTheme(window, true);
                window.Show();
                Pump();
                var selector = (FrameworkElement)panel.FindName("ComposerSelectorGrid");
                var actions = (FrameworkElement)panel.FindName("ComposerActionStack");
                var footer = (FrameworkElement)panel.FindName("ComposerFooterGrid");
                var selectorBounds = selector.TransformToAncestor(footer).TransformBounds(new Rect(selector.RenderSize));
                var actionBounds = actions.TransformToAncestor(footer).TransformBounds(new Rect(actions.RenderSize));
                Assert.False(selectorBounds.IntersectsWith(actionBounds));
                Assert.True(actionBounds.Right <= footer.ActualWidth + 1);
                var accessLabel = (TextBlock)panel.FindName("AccessModeLabelTextBlock");
                Assert.Equal(Visibility.Visible, accessLabel.Visibility);
                var labelMeasure = new TextBlock { Text = accessLabel.Text, FontFamily = accessLabel.FontFamily, FontSize = accessLabel.FontSize, FontWeight = accessLabel.FontWeight };
                labelMeasure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.True(accessLabel.ActualWidth >= labelMeasure.DesiredSize.Width);
                var settings = Assert.Single(((IDockPanelTitleActionProvider)panel).TitleActions);
                Assert.Equal("\uE713", settings.Glyph);
                Assert.True(settings.Command.CanExecute(null));
                Capture(panel, $"composer-{width}.png");
                var toggle = (System.Windows.Controls.Primitives.ToggleButton)panel.FindName("ProfileSelectorButton");
                toggle.IsChecked = true;
                Pump();
                var popup = (System.Windows.Controls.Primitives.Popup)panel.FindName("ProfileSelectorPopup");
                Assert.True(popup.IsOpen);
                Capture((FrameworkElement)popup.Child, $"model-picker-{width}.png");
                toggle.IsChecked = false;
            }
            finally
            {
                panel.DataContext = null;
                window.Close();
                host.Shutdown();
                instanceField.SetValue(null, previous);
            }
        });
    }

    private static readonly Lazy<Dispatcher> UiDispatcher = new(() =>
    {
        Dispatcher? dispatcher = null;
        Exception? failure = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                var application = Application.Current ?? new Application();
                application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                dispatcher = application.Dispatcher;
            }
            catch (Exception ex) { failure = ex; }
            finally { ready.Set(); }
            if (dispatcher != null) Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        if (failure != null) throw new InvalidOperationException("Could not initialize the WPF presentation host.", failure);
        return dispatcher!;
    });

    private static void RunUi(bool dark, Action action)
    {
        // Only explicit preview runs create an Application; normal suite runs retain
        // the existing per-test STA isolation and do not change Application.Current.
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COLORVISION_COPILOT_PREVIEW_OUTPUT")))
        {
            StaTest.Run(() =>
            {
                try { action(); }
                finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
            });
            return;
        }
        UiDispatcher.Value.Invoke(() =>
        {
            var resources = Application.Current.Resources;
            try
            {
                var theme = new ResourceDictionary();
                foreach (var source in (dark ? ThemeManager.ResourceDictionaryDark : ThemeManager.ResourceDictionaryWhite).Concat(ThemeManager.ResourceDictionaryBase))
                    theme.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.RelativeOrAbsolute) });
                Application.Current.Resources = theme;
                action();
            }
            finally { Application.Current.Resources = resources; }
        });
    }

    private static void ApplyTheme(Window window, bool dark)
    {
        foreach (var source in (dark ? ThemeManager.ResourceDictionaryDark : ThemeManager.ResourceDictionaryWhite).Concat(ThemeManager.ResourceDictionaryBase))
            window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.RelativeOrAbsolute) });
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static void Capture(FrameworkElement element, string name)
    {
        var directory = Environment.GetEnvironmentVariable("COLORVISION_COPILOT_PREVIEW_OUTPUT");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            var bounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
            context.DrawRectangle((Brush)element.FindResource("GlobalBackground"), null, bounds);
            context.DrawRectangle(new VisualBrush(element) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds, Stretch = Stretch.Fill }, null, bounds);
        }
        bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name));
        encoder.Save(stream);
    }

    private sealed class MemoryStateStore(CopilotChatState state) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => string.Empty;
        public CopilotChatState Load() => state;
        public void Save(CopilotChatState value) { }
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());
        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";
        public string Serialize(CopilotChatState value) => "{}";
        public Task SaveSerializedAsync(string value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private sealed class UnusedRuntime : ICopilotTurnRuntime
    {
        public IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("UI checks must not call a model.");
        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs args) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request, Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
