using ColorVision.Copilot;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class CopilotCompactMessageLayoutTests
{
    [Fact]
    public void CompactModeCommandOffersExplicitStatesAndRunsDuringAgentWork()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/compact-mode on");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.CompactMode, invocation.Command.Kind);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Equal(["on", "off"], invocation.Command.Arguments!.Select(item => item.Value));
        Assert.Contains("不压缩会话上下文", invocation.Command.Description);
    }

    [Theory]
    [InlineData("", false, true)]
    [InlineData("", true, false)]
    [InlineData("on", false, true)]
    [InlineData("OFF", true, false)]
    public void PreferenceResolverSupportsToggleAndExplicitStates(
        string arguments,
        bool currentlyCompact,
        bool expectedCompact)
    {
        Assert.True(CopilotCompactMessageLayout.TryResolvePreference(
            arguments,
            currentlyCompact,
            out var compact));
        Assert.Equal(expectedCompact, compact);
    }

    [Fact]
    public void CompactMetricsReduceOnlyExistingSpacing()
    {
        var standard = CopilotCompactMessageLayout.Resolve(useCompactLayout: false);
        var compact = CopilotCompactMessageLayout.Resolve(useCompactLayout: true);

        Assert.Equal(new Thickness(16, 12, 16, 12), standard.MessageListPadding);
        Assert.Equal(new Thickness(0, 0, 0, 12), standard.MessageItemMargin);
        Assert.Equal(new Thickness(10, 5, 10, 5), standard.UserMessagePadding);
        Assert.Equal(new Thickness(0, 10, 0, 0), standard.AssistantActionsMargin);
        Assert.True(compact.MessageListPadding.Left < standard.MessageListPadding.Left);
        Assert.True(compact.MessageListPadding.Top < standard.MessageListPadding.Top);
        Assert.True(compact.MessageItemMargin.Bottom < standard.MessageItemMargin.Bottom);
        Assert.True(compact.UserMessagePadding.Left < standard.UserMessagePadding.Left);
        Assert.True(compact.AssistantActionsMargin.Top < standard.AssistantActionsMargin.Top);
    }

    [Fact]
    public void InvalidArgumentKeepsTheCurrentDensity()
    {
        Assert.False(CopilotCompactMessageLayout.TryResolvePreference(
            "status",
            currentlyCompact: true,
            out var compact));
        Assert.True(compact);
        Assert.Contains("/compact-mode [on|off]", CopilotCompactMessageLayout.Usage);
    }

    [Fact]
    public void PanelBindingsApplyCompactMetricsToRealizedMessages()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var metrics = CopilotCompactMessageLayout.Resolve(useCompactLayout: true);
                var messages = new ObservableCollection<CopilotChatMessage>
                {
                    new(CopilotChatRole.User, "User request"),
                    new(CopilotChatRole.Assistant, "Assistant response"),
                };
                var panel = new CopilotChatPanel
                {
                    DataContext = new CompactLayoutContext(messages, metrics),
                };

                panel.Measure(new Size(900, 640));
                panel.Arrange(new Rect(0, 0, 900, 640));
                panel.UpdateLayout();

                var messageList = Assert.IsType<ListBox>(panel.FindName("MessagesListBox"));
                Assert.Equal(metrics.MessageListPadding, messageList.Padding);
                var userContainer = Assert.IsType<ListBoxItem>(
                    messageList.ItemContainerGenerator.ContainerFromIndex(0));
                Assert.Equal(
                    metrics.UserMessagePadding,
                    Assert.IsType<Border>(FindNamedVisual(userContainer, "UserBubble")).Padding);

                messageList.ScrollIntoView(messages[1]);
                messageList.UpdateLayout();
                var assistantContainer = Assert.IsType<ListBoxItem>(
                    messageList.ItemContainerGenerator.ContainerFromIndex(1));
                Assert.Equal(
                    metrics.AssistantActionsMargin,
                    Assert.IsType<StackPanel>(FindNamedVisual(assistantContainer, "AssistantActionsPanel")).Margin);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void CompactPreferenceSurvivesRestartWhileDefaultIsOmitted()
    {
        var defaultState = CreateState();
        Assert.False(defaultState.UseCompactMessageLayout);
        Assert.Null(JObject.FromObject(defaultState)[nameof(CopilotChatState.UseCompactMessageLayout)]);

        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var state = CreateState();
            Assert.True(state.SetUseCompactMessageLayout(true));
            var store = new CopilotChatStateStore(root);

            store.Save(state);
            var document = JObject.Parse(File.ReadAllText(store.StateFilePath));
            var restored = new CopilotChatStateStore(root).Load();

            Assert.True(document[nameof(CopilotChatState.UseCompactMessageLayout)]!.Value<bool>());
            Assert.True(restored.UseCompactMessageLayout);
            Assert.Equal(CopilotChatState.CurrentSchemaVersion, restored.SchemaVersion);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static CopilotChatState CreateState()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        return new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = "profile",
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
    }

    private static FrameworkElement? FindNamedVisual(DependencyObject parent, string name)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is FrameworkElement element
                && string.Equals(element.Name, name, StringComparison.Ordinal))
            {
                return element;
            }

            var nested = FindNamedVisual(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private sealed class CompactLayoutContext(
        ObservableCollection<CopilotChatMessage> messages,
        CopilotCompactMessageLayoutMetrics metrics)
    {
        public ObservableCollection<CopilotChatMessage> Messages { get; } = messages;

        public Thickness MessageListPadding { get; } = metrics.MessageListPadding;

        public Thickness MessageItemMargin { get; } = metrics.MessageItemMargin;

        public Thickness UserMessagePadding { get; } = metrics.UserMessagePadding;

        public Thickness AssistantActionsMargin { get; } = metrics.AssistantActionsMargin;

        public bool ShowMessageTimestamps => metrics.MessageListPadding != default;
    }
}
