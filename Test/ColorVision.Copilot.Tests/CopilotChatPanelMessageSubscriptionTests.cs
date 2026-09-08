using ColorVision.Common.MVVM;
using ColorVision.Copilot;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotChatPanelMessageSubscriptionTests
{
    private static readonly MethodInfo ResetSubscriptions = typeof(CopilotChatPanel).GetMethod(
        "ResetMessageSubscriptions", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo PropertyChangedHandlers = typeof(ViewModelBase).GetField(
        "PropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Fact]
    public void MessageSubscriptionsFollowReplacementDuplicatesResetAndConversationSwitch()
    {
        StaTest.Run(() =>
        {
            var panel = new CopilotChatPanel();
            var first = new CopilotChatMessage(CopilotChatRole.User, "First");
            var second = new CopilotChatMessage(CopilotChatRole.Assistant, "Second");
            var replacement = new CopilotChatMessage(CopilotChatRole.Assistant, "Replacement");
            var messages = new ObservableCollection<CopilotChatMessage> { first, first, second };
            try
            {
                ResetSubscriptions.Invoke(panel, [messages]);
                Assert.Equal(1, HandlerCount(first, panel));
                Assert.Equal(1, HandlerCount(second, panel));

                messages.Move(0, 2);
                messages.RemoveAt(0);
                Assert.Equal(1, HandlerCount(first, panel));
                Assert.Equal(1, HandlerCount(second, panel));

                messages[1] = replacement;
                Assert.Equal(0, HandlerCount(first, panel));
                Assert.Equal(1, HandlerCount(replacement, panel));
                messages[1] = replacement;
                Assert.Equal(1, HandlerCount(replacement, panel));

                messages.Clear();
                Assert.Equal(0, HandlerCount(second, panel));
                Assert.Equal(0, HandlerCount(replacement, panel));
                messages.Add(first);
                Assert.Equal(1, HandlerCount(first, panel));

                var nextConversation = new ObservableCollection<CopilotChatMessage> { second };
                ResetSubscriptions.Invoke(panel, [nextConversation]);
                messages.Add(replacement);
                Assert.Equal(0, HandlerCount(first, panel));
                Assert.Equal(0, HandlerCount(replacement, panel));
                Assert.Equal(1, HandlerCount(second, panel));
                ResetSubscriptions.Invoke(panel, [null]);
                Assert.Equal(0, HandlerCount(second, panel));
            }
            finally
            {
                ResetSubscriptions.Invoke(panel, [null]);
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
    }

    [Fact]
    public void AppendingToALongConversationDoesNotAllocateFullHistorySubscriptionSets()
    {
        StaTest.Run(() =>
        {
            var panel = new CopilotChatPanel();
            var messages = new ObservableCollection<CopilotChatMessage>(Enumerable.Range(0, 10_000)
                .Select(index => new CopilotChatMessage(CopilotChatRole.Assistant, $"Existing {index}")));
            var additions = Enumerable.Range(0, 32)
                .Select(index => new CopilotChatMessage(CopilotChatRole.Assistant, $"New {index}"))
                .ToArray();
            try
            {
                ResetSubscriptions.Invoke(panel, [messages]);
                // Warm event dispatch and grow the source collection before measuring subscriptions.
                for (var index = 0; index < 8; index++)
                    messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Warmup"));
                long before = GC.GetAllocatedBytesForCurrentThread();
                foreach (var message in additions)
                    messages.Add(message);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.True(allocated < 128 * 1024, $"Appending 32 messages allocated {allocated:N0} bytes.");
                Assert.All(additions, message => Assert.Equal(1, HandlerCount(message, panel)));
            }
            finally
            {
                ResetSubscriptions.Invoke(panel, [null]);
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
    }

    private static int HandlerCount(CopilotChatMessage message, CopilotChatPanel panel) =>
        (PropertyChangedHandlers.GetValue(message) as PropertyChangedEventHandler)?.GetInvocationList()
            .Count(handler => ReferenceEquals(handler.Target, panel)) ?? 0;
}
