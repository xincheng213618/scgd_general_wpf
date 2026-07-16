using ColorVision.Copilot;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace ColorVision.UI.Tests;

[Collection(CopilotSharedStateTestGroup.Name)]
public sealed class CopilotPanelLifecycleTests
{
    [Fact]
    public void Panel_RebindsAfterReloadAndDropsOldConversationMessageSubscriptions()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            CopilotChatViewModel? viewModel = null;
            CopilotChatPanel? panel = null;
            try
            {
                viewModel = new CopilotChatViewModel(new CopilotChatService(), new InMemoryChatStateStore());
                var firstConversation = Assert.IsType<CopilotConversationRecord>(viewModel.SelectedConversation);
                var firstMessage = new CopilotChatMessage(CopilotChatRole.User, "first");
                firstConversation.Messages.Add(firstMessage);

                panel = new CopilotChatPanel { DataContext = viewModel };
                Assert.Same(viewModel, ReadPrivateField(panel, "_attachedViewModel"));
                Assert.Contains(firstMessage, ReadTrackedMessages(panel));

                var secondConversation = CopilotConversationRecord.CreateEmpty(firstConversation.ProfileId, firstConversation.ProfileDisplayName);
                var secondMessage = new CopilotChatMessage(CopilotChatRole.User, "second");
                secondConversation.Messages.Add(secondMessage);
                viewModel.Conversations.Add(secondConversation);
                viewModel.SelectedConversation = secondConversation;

                Assert.DoesNotContain(firstMessage, ReadTrackedMessages(panel));
                Assert.Contains(secondMessage, ReadTrackedMessages(panel));

                InvokeLifecycleHandler(panel, "CopilotChatPanel_Unloaded");
                Assert.Null(ReadPrivateField(panel, "_attachedViewModel"));
                Assert.Empty(ReadTrackedMessages(panel));

                InvokeLifecycleHandler(panel, "CopilotChatPanel_Loaded");
                Assert.Same(viewModel, ReadPrivateField(panel, "_attachedViewModel"));
                Assert.Contains(secondMessage, ReadTrackedMessages(panel));
            }
            catch (Exception ex)
            {
                failure = ex is TargetInvocationException { InnerException: not null } invocationException
                    ? invocationException.InnerException
                    : ex;
            }
            finally
            {
                if (panel != null)
                    InvokeLifecycleHandler(panel, "CopilotChatPanel_Unloaded");
                viewModel?.Dispose();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    private static object? ReadPrivateField(CopilotChatPanel panel, string name) =>
        typeof(CopilotChatPanel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(panel);

    private static IReadOnlyList<CopilotChatMessage> ReadTrackedMessages(CopilotChatPanel panel) =>
        Assert.IsAssignableFrom<IEnumerable>(ReadPrivateField(panel, "_attachedMessageItems"))
            .Cast<CopilotChatMessage>()
            .ToArray();

    private static void InvokeLifecycleHandler(CopilotChatPanel panel, string methodName)
    {
        var method = typeof(CopilotChatPanel).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(panel, [panel, new RoutedEventArgs()]);
    }

    private sealed class InMemoryChatStateStore : ICopilotChatStateStore
    {
        private readonly CopilotChatState _state = new();

        public string AttachmentDirectoryPath => Path.GetTempPath();

        public CopilotChatState Load() => _state;

        public void Save(CopilotChatState state)
        {
        }

        public string Serialize(CopilotChatState state) => "{}";

        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public int CleanupOrphanedAttachments(CopilotChatState state) => 0;
    }
}
