using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ColorVision.Solution;
using Newtonsoft.Json.Linq;

namespace ColorVision.Copilot.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CopilotChatViewModelProfileIsolationFixture
{
    public const string Name = "CopilotChatViewModel profile isolation";
}

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotChatViewModelProfileIsolationTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CompactionKeepsNewerLocalFeedbackWhileApplyingItsOwnResultAndUsage(bool newerHelp, bool replySucceeds)
    {
        const string summary = "Earlier context remains relevant.";
        var profile = CreateProfile("compaction-feedback-profile", "Compaction feedback", "compaction-feedback-model");
        var conversation = CreateConversation(profile, "compaction-feedback-conversation", string.Empty);
        conversation.SetCustomTitle("Compaction feedback fixture");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, new string('u', 3_000)));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, new string('a', 3_000)));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Keep this recent request verbatim."));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Keep this recent answer verbatim."));
        var originalMessages = conversation.Messages.ToArray();
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id, ActiveProfileId = profile.Id,
            Conversations = [conversation],
        };
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var handler = new GatedCompactionHandler();
        using var client = new HttpClient(handler);
        var service = new CopilotChatService(client);
        using var viewModel = new CopilotChatViewModel(service, new InMemoryStateStore(state),
            CreateConfig(profile, "compaction-feedback-test-token"), new GatedFailingTurnRuntime(), new CopilotAgentTaskHost());
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        System.ComponentModel.PropertyChangedEventHandler observeCompletion = (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.IsBusy) && !viewModel.IsBusy)
                finished.TrySetResult();
        };
        viewModel.PropertyChanged += observeCompletion;
        var responseReleased = false;
        var response = JObject.FromObject(new
        {
            choices = new[] { new { message = new { role = "assistant", content = summary }, finish_reason = replySucceeds ? "stop" : "length" } },
            usage = new { prompt_tokens = 12, completion_tokens = 8, total_tokens = 20, prompt_tokens_details = new { cached_tokens = 3 } },
        }).ToString(Newtonsoft.Json.Formatting.None);
        try
        {
            viewModel.InputText = "/compact";
            Assert.True(viewModel.SendCommand.CanExecute(null));
            viewModel.SendCommand.Execute(null);
            await handler.Entered.Task.WaitAsync(TestTimeout);
            Assert.True(viewModel.IsBusy);
            var expectedTitle = string.Empty;
            var expectedText = string.Empty;
            if (newerHelp)
            {
                viewModel.InputText = "/help";
                Assert.True(viewModel.SendCommand.CanExecute(null));
                viewModel.SendCommand.Execute(null);
                expectedTitle = viewModel.LocalCommandResultTitle;
                expectedText = viewModel.LocalCommandResultText;
                Assert.StartsWith("/help", expectedTitle, StringComparison.Ordinal);
                Assert.NotEmpty(expectedText);
            }

            handler.Respond(response);
            responseReleased = true;
            await finished.Task.WaitAsync(TestTimeout);

            Assert.Equal(originalMessages, conversation.Messages.ToArray());
            var usage = Assert.IsType<CopilotConversationAuxiliaryUsage>(conversation.CompactionUsage);
            Assert.Equal(1, usage.RequestCount);
            Assert.Equal(new CopilotTokenUsage(12, 8, 20, 3), usage.Usage);
            if (replySucceeds)
                Assert.Equal(summary, Assert.IsType<CopilotConversationCompaction>(conversation.Compaction).Summary);
            else
                Assert.Null(conversation.Compaction);
            if (newerHelp)
            {
                Assert.Equal(expectedTitle, viewModel.LocalCommandResultTitle);
                Assert.Equal(expectedText, viewModel.LocalCommandResultText);
            }
            else
            {
                Assert.StartsWith("/compact", viewModel.LocalCommandResultTitle, StringComparison.Ordinal);
                Assert.Contains("延续摘要", viewModel.LocalCommandResultText, StringComparison.Ordinal);
            }
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            if (!responseReleased)
                handler.Respond(response);
            if (viewModel.IsBusy)
                await finished.Task.WaitAsync(TestTimeout);
            viewModel.PropertyChanged -= observeCompletion;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChatSendCommitsStateBeforeTheProviderAndRejectsPersistenceFailure(bool persistFails)
    {
        const string Prompt = "Answer the captured Chat request.";
        const string SaveError = "Expected Chat state persistence failure.";
        var profile = CreateProfile("chat-barrier-profile", "Chat barrier", "chat-barrier-model");
        var config = CreateConfig(profile, "chat-barrier-test-token");
        var conversation = CreateConversation(profile, "chat-barrier-conversation", Prompt);
        conversation.DraftRequestMode = CopilotAgentMode.Chat;
        conversation.SetCustomTitle("Chat persistence barrier");
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id, ActiveProfileId = profile.Id,
            Conversations = [conversation],
        };
        var saveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failSaves = persistFails ? 1 : 0;
        var stateStore = new InMemoryStateStore(state, saveSerializedAsync: async cancellationToken =>
        {
            if (conversation.Messages.Count == 0)
                return;
            saveEntered.TrySetResult();
            await releaseSave.Task.WaitAsync(cancellationToken);
            if (Volatile.Read(ref failSaves) != 0)
                throw new IOException(SaveError);
        });
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var handler = new GatedCompactionHandler();
        handler.Respond("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"Captured Chat answer.\"},\"finish_reason\":\"stop\"}]}");
        using var client = new HttpClient(handler);
        var service = new CopilotChatService(client);
        var taskHost = new CopilotAgentTaskHost();
        var viewModel = new CopilotChatViewModel(service, stateStore, config, new CopilotTurnRuntime(service), taskHost);
        var started = new TaskCompletionSource<CopilotHostedAgentRun>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<CopilotAgentTaskHostChangedEventArgs> observeRun = (_, args) =>
        {
            if (args.Run.ConversationId != conversation.Id)
                return;
            if (args.Kind == CopilotAgentTaskHostChangeKind.Started)
                started.TrySetResult(args.Run);
            if (args.Kind == CopilotAgentTaskHostChangeKind.Completed)
                completed.TrySetResult();
        };
        taskHost.Changed += observeRun;
        CopilotHostedAgentRun? hostedRun = null;
        try
        {
            Assert.True(viewModel.SendCommand.CanExecute(null));
            viewModel.SendCommand.Execute(null);
            hostedRun = await started.Task.WaitAsync(TestTimeout);
            Assert.Equal(CopilotAgentMode.Chat, hostedRun.Mode);
            await saveEntered.Task.WaitAsync(TestTimeout);
            Assert.Equal(0, handler.RequestCount);
            Assert.False(hostedRun.Completion.IsCompleted);
            releaseSave.TrySetResult();
            await hostedRun.Completion.WaitAsync(TestTimeout);
            await completed.Task.WaitAsync(TestTimeout);

            var userMessage = Assert.Single(conversation.Messages, message => message.IsUser);
            var assistantMessage = Assert.Single(conversation.Messages, message => message.Role == CopilotChatRole.Assistant);
            Assert.Equal(Prompt, userMessage.Content);
            Assert.False(assistantMessage.IsThinkingInProgress);
            Assert.Null(taskHost.ActiveRun);
            if (persistFails)
            {
                Assert.Equal(0, handler.RequestCount);
                Assert.True(viewModel.HasStatePersistenceNotice);
                Assert.Contains(SaveError, viewModel.StatePersistenceNoticeToolTip, StringComparison.Ordinal);
                Assert.Contains(SaveError, assistantMessage.Content, StringComparison.Ordinal);
                Assert.True(assistantMessage.WasResponseInterrupted);
                Assert.Contains(SaveError, assistantMessage.ResponseInterruptionDetail, StringComparison.Ordinal);
                Assert.Empty(userMessage.RequestContent);
            }
            else
            {
                Assert.Equal(1, handler.RequestCount);
                Assert.StartsWith(Prompt, userMessage.RequestContent, StringComparison.Ordinal);
                Assert.Equal("Captured Chat answer.", assistantMessage.Content);
                Assert.False(assistantMessage.WasResponseInterrupted);
                Assert.False(viewModel.HasStatePersistenceNotice);
            }
        }
        finally
        {
            Volatile.Write(ref failSaves, 0);
            releaseSave.TrySetResult();
            taskHost.Shutdown();
            try
            {
                var pendingRun = hostedRun ?? (started.Task.IsCompletedSuccessfully ? await started.Task : null);
                if (pendingRun != null)
                {
                    try
                    {
                        await pendingRun.Completion.WaitAsync(TestTimeout);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }
            finally
            {
                taskHost.Changed -= observeRun;
                viewModel.Dispose();
            }
        }
    }

    [Theory]
    [InlineData(CopilotAgentMode.Chat, "reported-payload-failure")]
    [InlineData(CopilotAgentMode.Auto, "reported-payload-failure")]
    [InlineData(CopilotAgentMode.Chat, "unreported-payload-failure")]
    [InlineData(CopilotAgentMode.Chat, "reasoning-only-analysis")]
    [InlineData(CopilotAgentMode.Chat, "billed-backoff-cancel")]
    [InlineData(CopilotAgentMode.Chat, "image-success-main-success")]
    [InlineData(CopilotAgentMode.Auto, "image-success-context-cancel")]
    public async Task ImageAnalysisUsageSurvivesHostedFailureAndCancellationWithoutDoubleBilling(
        CopilotAgentMode mode, string scenario)
    {
        var root = Directory.CreateTempSubdirectory("CopilotImageUsage-").FullName;
        var attachmentRoot = Path.Combine(root, "managed");
        var sourcePath = Path.Combine(root, "source.png");
        await File.WriteAllBytesAsync(sourcePath, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        var profile = CreateProfile("image-usage-profile", "Image usage", "image-usage-model");
        var reasoningOnly = scenario == "reasoning-only-analysis";
        profile.BaseUrl = reasoningOnly ? "https://unit.test/v1/chat/completions" : "https://unit.test/v1/responses";
        profile.SupportsImageInput = true;
        Assert.Equal(!reasoningOnly, CopilotOpenAiRequestPolicy.UsesResponsesApi(profile));
        var config = CreateConfig(profile, "image-usage-test-token");
        var conversation = CreateConversation(profile, "image-usage-conversation", "Describe the attached image.");
        conversation.DraftRequestMode = mode;
        conversation.SetCustomTitle("Image analysis billing");
        conversation.Attachments.Add(CopilotAttachmentItem.CreateImage(sourcePath, "Image evidence"));
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id, ActiveProfileId = profile.Id, Conversations = [conversation],
        };
        var cancelDuringContextCapture = scenario == "image-success-context-cancel";
        var cancelled = scenario is "billed-backoff-cancel" or "image-success-context-cancel";
        var succeeds = scenario == "image-success-main-success";
        var imageResponse = JObject.Parse("""
            {"id":"resp_image_usage","object":"response","status":"failed","output":[],
             "error":{"code":"insufficient_quota","type":"insufficient_quota","message":"Controlled image analysis failure."},
             "usage":{"input_tokens":12,"output_tokens":8,"total_tokens":20,"input_tokens_details":{"cached_tokens":3}}}
            """);
        if (scenario == "unreported-payload-failure")
            imageResponse.Remove("usage");
        if (scenario == "billed-backoff-cancel")
        {
            imageResponse["error"]!["code"] = "server_error";
            imageResponse["error"]!["type"] = "server_error";
        }
        if (succeeds || cancelDuringContextCapture)
        {
            imageResponse["status"] = "completed";
            imageResponse.Remove("error");
            imageResponse["output"] = JArray.Parse("""
                [{"type":"message","role":"assistant","content":[{"type":"output_text","text":"The image contains one visible pixel."}]}]
                """);
        }
        var imageBody = reasoningOnly ? """
            {"choices":[{"message":{"role":"assistant","reasoning_content":"Only private visual reasoning."},"finish_reason":"stop"}],
             "usage":{"prompt_tokens":12,"completion_tokens":8,"total_tokens":20,"prompt_tokens_details":{"cached_tokens":3}}}
            """ : imageResponse.ToString(Newtonsoft.Json.Formatting.None);
        const string MainResponse = """
            {"id":"resp_main_usage","object":"response","status":"completed",
             "output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"The completed image answer."}]}],
             "usage":{"input_tokens":20,"output_tokens":10,"total_tokens":30,"input_tokens_details":{"cached_tokens":5}}}
            """;
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var contextProvider = cancelDuringContextCapture
            ? new ImageUsageContextProvider(new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)) : null;
        using var contextRegistration = contextProvider == null ? null : CopilotAgentExtensionRegistry.Shared.Register(
            new CopilotAgentExtensionRegistration
            {
                SourceId = "test.image-usage." + Guid.NewGuid().ToString("N"),
                SourceName = "Image usage cancellation context",
                ContextProviders = [contextProvider],
            });
        var imageResponseDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new ImageUsageHandler(succeeds ? [imageBody, MainResponse] : [imageBody],
            cancelDuringContextCapture ? imageResponseDisposed : null);
        using var client = new HttpClient(handler);
        var taskHost = new CopilotAgentTaskHost();
        var service = scenario == "billed-backoff-cancel"
            ? new CopilotChatService(client, 3, _ => TimeSpan.Zero, (_, token) =>
            {
                var active = Assert.IsType<CopilotHostedAgentRun>(taskHost.ActiveRun);
                Assert.Equal(conversation.Id, active.ConversationId);
                Assert.True(taskHost.RequestCancel(active.Id));
                return token.IsCancellationRequested ? Task.FromCanceled(token) : Task.Delay(Timeout.InfiniteTimeSpan, token);
            })
            : new CopilotChatService(client);
        var viewModel = new CopilotChatViewModel(service, new InMemoryStateStore(state, attachmentRoot),
            config, new CopilotTurnRuntime(service), taskHost);
        var started = new TaskCompletionSource<CopilotHostedAgentRun>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<CopilotAgentTaskHostChangedEventArgs> observeRun = (_, args) =>
        {
            if (args.Run.ConversationId != conversation.Id)
                return;
            if (args.Kind == CopilotAgentTaskHostChangeKind.Started)
                started.TrySetResult(args.Run);
            if (args.Kind == CopilotAgentTaskHostChangeKind.Completed)
                completed.TrySetResult();
        };
        taskHost.Changed += observeRun;
        CopilotHostedAgentRun? hostedRun = null;
        try
        {
            Assert.True(viewModel.SelectedProfile!.SupportsImageInput);
            Assert.True(viewModel.SendCommand.CanExecute(null));
            viewModel.SendCommand.Execute(null);
            hostedRun = await started.Task.WaitAsync(TestTimeout);
            Assert.Equal(mode, hostedRun.Mode);
            if (cancelDuringContextCapture)
            {
                await contextProvider!.Entered.Task.WaitAsync(TestTimeout);
                await imageResponseDisposed.Task.WaitAsync(TestTimeout);
                Assert.Single(handler.Requests);
                Assert.True(taskHost.RequestCancel(hostedRun.Id));
            }
            if (cancelled)
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hostedRun.Completion.WaitAsync(TestTimeout));
            else
                await hostedRun.Completion.WaitAsync(TestTimeout);
            await completed.Task.WaitAsync(TestTimeout);

            var user = Assert.Single(conversation.Messages, message => message.IsUser);
            var assistant = Assert.Single(conversation.Messages, message => message.Role == CopilotChatRole.Assistant);
            Assert.Equal(attachmentRoot, Path.GetDirectoryName(Assert.Single(user.Attachments).Value));
            Assert.False(assistant.IsThinkingInProgress);
            Assert.Null(taskHost.ActiveRun);
            var requests = handler.Requests.ToArray();
            Assert.Equal(succeeds ? 2 : 1, requests.Length);
            Assert.Contains(reasoningOnly ? "image_url" : "input_image", requests[0].Body, StringComparison.Ordinal);
            Assert.Equal(reasoningOnly ? "/v1/chat/completions" : "/v1/responses", requests[0].Path);
            Assert.Equal(!succeeds, assistant.WasResponseInterrupted);
            Assert.Equal(cancelled, hostedRun.CancellationToken.IsCancellationRequested);
            if (reasoningOnly)
                Assert.Contains("模型没有返回可用的图片解析结果", assistant.ResponseInterruptionDetail, StringComparison.Ordinal);
            if (succeeds)
            {
                Assert.DoesNotContain("input_image", requests[1].Body, StringComparison.Ordinal);
                Assert.Contains("[Attached Image Analysis]", requests[1].Body, StringComparison.Ordinal);
                Assert.Equal("The completed image answer.", assistant.Content);
            }
            var expectedUsage = scenario == "unreported-payload-failure" ? CopilotTokenUsage.Empty
                : succeeds ? new CopilotTokenUsage(32, 18, 50, 8) : new CopilotTokenUsage(12, 8, 20, 3);
            Assert.Equal(expectedUsage, assistant.ReportedUsage);
            Assert.Equal(expectedUsage, conversation.LastUsage);
        }
        finally
        {
            contextProvider?.Release();
            taskHost.Shutdown();
            try
            {
                if (contextProvider?.Entered.Task.IsCompletedSuccessfully == true)
                    await contextProvider.Exited.Task.WaitAsync(TestTimeout);
                var pendingRun = hostedRun ?? (started.Task.IsCompletedSuccessfully ? await started.Task : null);
                if (pendingRun != null)
                {
                    try { await pendingRun.Completion.WaitAsync(TestTimeout); }
                    catch (OperationCanceledException) { }
                }
            }
            finally
            {
                taskHost.Changed -= observeRun;
                viewModel.Dispose();
                var fullRoot = Path.GetFullPath(root);
                var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Assert.StartsWith(tempRoot, fullRoot, StringComparison.OrdinalIgnoreCase);
                Assert.StartsWith("CopilotImageUsage-", Path.GetFileName(fullRoot), StringComparison.Ordinal);
                Directory.Delete(fullRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void DisposingChatViewModelSynchronouslyCommitsItsAcceptedPendingAnswer()
    {
        StaTest.Run(() =>
        {
            var profile = CreateProfile("pending-chat-profile", "Pending Chat", "pending-chat-model");
            var config = CreateConfig(profile, "pending-chat-test-token");
            var conversation = CreateConversation(profile, "pending-chat-conversation", "Reply before this view closes.");
            conversation.DraftRequestMode = CopilotAgentMode.Chat;
            var runtime = new GatedFailingTurnRuntime(emitPendingChatDelta: true);
            var taskHost = new CopilotAgentTaskHost();
            using var solutionManagerScope = new IsolatedSolutionManagerScope();
            var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
            var context = new HeldChatSynchronizationContext();
            var previousContext = SynchronizationContext.Current;
            CopilotHostedAgentRun? hostedRun = null;
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                Assert.True(viewModel.SendCommand.CanExecute(null));
                viewModel.SendCommand.Execute(null);
                Assert.True(runtime.PendingChatDeltaAccepted.IsCompletedSuccessfully);
                hostedRun = Assert.IsType<CopilotHostedAgentRun>(taskHost.ActiveRun);
                Assert.False(hostedRun.Completion.IsCompleted);
                var assistant = Assert.Single(conversation.Messages, message => message.Role == CopilotChatRole.Assistant);
                Assert.Empty(assistant.Content);

                viewModel.Dispose();

                Assert.Equal(GatedFailingTurnRuntime.PendingChatTail, assistant.Content);
                Assert.False(hostedRun.Completion.IsCompleted);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
                context.Release();
                hostedRun ??= taskHost.ActiveRun;
                taskHost.Shutdown();
                runtime.Release();
                try
                {
                    if (hostedRun != null)
                    {
                        try
                        {
                            hostedRun.Completion.WaitAsync(TestTimeout).GetAwaiter().GetResult();
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }
                }
                finally
                {
                    viewModel.Dispose();
                }
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClipboardImageCannotBeStoredThroughLinkedDirectory(bool linkedAncestor)
    {
        StaTest.Run(() =>
        {
            var root = Directory.CreateTempSubdirectory("CopilotClipboardStorage-").FullName;
            var outside = Directory.CreateDirectory(Path.Combine(root, "outside")).FullName;
            var linkedPath = Path.Combine(root, "linked");
            var storePath = linkedAncestor ? Path.Combine(linkedPath, "new-store") : linkedPath;
            Directory.CreateSymbolicLink(linkedPath, outside);
            var profile = CreateProfile("clipboard", "Clipboard", "test-model");
            var conversation = CreateConversation(profile, "clipboard-conversation", "preserved draft");
            var state = new CopilotChatState { ActiveConversationId = conversation.Id, ActiveProfileId = profile.Id, Conversations = [conversation] };
            var config = new CopilotConfig { SchemaVersion = CopilotConfig.CurrentSchemaVersion, McpBearerToken = "test-token", Profiles = [profile] };
            using var solutionManagerScope = new IsolatedSolutionManagerScope();
            using var viewModel = new CopilotChatViewModel(new CopilotChatService(), new InMemoryStateStore(state, storePath), config, new GatedFailingTurnRuntime(), new CopilotAgentTaskHost());
            try
            {
                var image = BitmapSource.Create(4, 3, 96, 96, PixelFormats.Bgra32, null, new byte[4 * 3 * 4], 4 * 4);
                image.Freeze();
                var saveMethod = typeof(CopilotChatViewModel).GetMethod("SaveClipboardImage", BindingFlags.Instance | BindingFlags.NonPublic)!;

                var error = Assert.Throws<TargetInvocationException>(() =>
                    saveMethod.Invoke(viewModel, [image, CancellationToken.None]));

                Assert.IsType<InvalidOperationException>(error.InnerException);
                Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
                Assert.Empty(conversation.Attachments);
                Assert.Equal("preserved draft", conversation.DraftText);
            }
            finally
            {
                viewModel.Dispose();
                Directory.Delete(linkedPath, recursive: false);
                Directory.Delete(root, recursive: true);
            }
        }, TimeSpan.FromSeconds(20), "The STA clipboard lifecycle test did not finish.");
    }

    [Fact]
    public async Task BackgroundTurnCompletionDoesNotApplyProfileFromNewlySelectedConversation()
    {
        var profileA = CreateProfile("profile-a", "Profile A", "model-a");
        var profileB = CreateProfile("profile-b", "Profile B", "model-b");
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "profile-isolation-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig>
            {
                profileA,
                profileB,
            },
        };
        var conversationA = CopilotConversationRecord.CreateEmpty(profileA.Id, profileA.DisplayLabel);
        conversationA.Id = "conversation-a";
        conversationA.DraftRequestMode = CopilotAgentMode.Explain;
        var conversationB = CopilotConversationRecord.CreateEmpty(profileB.Id, profileB.DisplayLabel);
        conversationB.Id = "conversation-b";
        var state = new CopilotChatState
        {
            ActiveConversationId = conversationA.Id,
            ActiveProfileId = profileA.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord>
            {
                conversationA,
                conversationB,
            },
        };
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            runtime,
            taskHost);
        CopilotHostedAgentRun? activeRun = null;
        try
        {
            Assert.Same(conversationA, viewModel.SelectedConversation);
            Assert.Same(profileA, viewModel.SelectedProfile);

            viewModel.InputText = "Explain conversation A.";
            viewModel.SendCommand.Execute(null);

            var request = await runtime.Entered.WaitAsync(TestTimeout);
            activeRun = taskHost.ActiveRun;
            Assert.NotNull(activeRun);
            Assert.Equal(conversationA.Id, request.ConversationId);
            Assert.Equal(profileA.Id, request.Profile.Id);
            Assert.True(activeRun.IsAgent);
            Assert.True(viewModel.CanSwitchConversation);
            Assert.True(viewModel.SelectConversationCommand.CanExecute(conversationB));

            viewModel.SelectConversationCommand.Execute(conversationB);

            Assert.Same(conversationB, viewModel.SelectedConversation);
            Assert.Same(profileB, viewModel.SelectedProfile);

            runtime.Release();
            await activeRun.Completion.WaitAsync(TestTimeout);

            Assert.Same(conversationB, viewModel.SelectedConversation);
            Assert.Same(profileB, viewModel.SelectedProfile);
            Assert.Equal(profileA.Id, conversationA.ProfileId);
            Assert.Equal(profileA.DisplayLabel, conversationA.ProfileDisplayName);
            Assert.Equal(profileB.Id, conversationB.ProfileId);
            Assert.Equal(profileB.DisplayLabel, conversationB.ProfileDisplayName);
        }
        finally
        {
            runtime.Release();
            var pendingRun = activeRun ?? taskHost.ActiveRun;
            if (pendingRun != null && !pendingRun.Completion.IsCompleted)
            {
                try
                {
                    await pendingRun.Completion.WaitAsync(TestTimeout);
                }
                catch
                {
                }
            }
            viewModel.Dispose();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BufferedSteeringRecoveryIsNotDuplicatedAfterShutdownDraftRecovery(bool recoverBeforeFlush)
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "buffered-steering-recovery-test-token");
        var conversation = CreateConversation(profile, "conversation-a", "A newer draft.");
        var otherConversation = CreateConversation(profile, "conversation-b", "Another conversation's draft.");
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        conversation.Messages.Add(assistantMessage);
        var attachment = CopilotAttachmentItem.CreateContext("newer draft attachment");
        conversation.Attachments.Add(attachment);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = [conversation, otherConversation],
        };
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(new CopilotChatService(), new InMemoryStateStore(state), config,
            new GatedFailingTurnRuntime(), new CopilotAgentTaskHost());
        using var hostedRun = new CopilotHostedAgentRun(conversation.Id, CopilotAgentMode.Auto, "run:buffered-recovery");
        var steering = new CopilotSteeringMessageSnapshot("steering:buffered-recovery", "Keep the remaining constraint.");
        var context = new HeldChatSynchronizationContext();
        var buffer = new CopilotAgentEventBuffer(context,
            events => ApplyAgentEvents(viewModel, hostedRun, conversation, assistantMessage, events.ToArray()));
        try
        {
            Assert.True(CopilotSteeringRecovery.TrackPending(conversation, hostedRun.Id, steering, DateTimeOffset.UtcNow));
            buffer.Enqueue(CopilotAgentEvent.SteeringRecovery([steering]));
            Assert.Single(conversation.PendingSteeringRecoveries);
            Assert.Equal("A newer draft.", conversation.DraftText);

            // Application_Exit restores pending input before flushing accepted UI events.
            if (recoverBeforeFlush)
                Assert.True(CopilotSteeringRecovery.RestorePendingToDrafts(state));
            Assert.True(buffer.FlushAsync().IsCompletedSuccessfully);

            Assert.Empty(conversation.PendingSteeringRecoveries);
            Assert.StartsWith("A newer draft.", conversation.DraftText, StringComparison.Ordinal);
            Assert.Contains(steering.Text, conversation.DraftText, StringComparison.Ordinal);
            Assert.Equal(conversation.DraftText.IndexOf(steering.Text, StringComparison.Ordinal),
                conversation.DraftText.LastIndexOf(steering.Text, StringComparison.Ordinal));
            Assert.Same(attachment, Assert.Single(conversation.Attachments));
            Assert.Equal("Another conversation's draft.", otherConversation.DraftText);
        }
        finally
        {
            await buffer.CompleteAsync();
            context.Release();
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task AcceptedAutomaticApprovalRetryKeepsDraftWhenRecoveryCapacityIsFull()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "approval-retry-recovery-test-token");
        var conversation = CreateConversation(profile, "conversation-a", "A newer draft.");
        conversation.DraftRequestMode = CopilotAgentMode.Chat;
        var attachment = CopilotAttachmentItem.CreateContext("newer draft attachment");
        conversation.Attachments.Add(attachment);
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        conversation.Messages.Add(assistantMessage);
        CopilotSteeringMessageSnapshot? acceptedSteering = null;
        var acceptedTaskId = string.Empty;
        var runtime = new GatedFailingTurnRuntime(steeringAdmission: (taskId, message) =>
        {
            acceptedTaskId = taskId;
            acceptedSteering = new CopilotSteeringMessageSnapshot("steering:approval-retry", message);
            return new CopilotSteeringAdmissionResult(CopilotSteeringAdmissionReason.Accepted, acceptedSteering.MessageId);
        });
        var taskHost = new CopilotAgentTaskHost();
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var activeRun = taskHost.Start(conversation.Id, CopilotAgentMode.Auto, _ => releaseActive.Task);
        try
        {
            for (var index = 0; index < CopilotSteeringRecovery.MaximumPendingRecoveryRecords; index++)
            {
                Assert.True(CopilotSteeringRecovery.TrackPending(conversation, activeRun.Id,
                    new CopilotSteeringMessageSnapshot($"steering:existing:{index}", $"Existing constraint {index}."),
                    DateTimeOffset.UtcNow));
            }
            var existingRecoveries = conversation.PendingSteeringRecoveries.ToArray();
            var denial = new CopilotAutomaticApprovalDenialSnapshot("denial:retry", conversation.Id,
                activeRun.Id, string.Empty, "WriteProjectFile", "arguments-digest", DateTimeOffset.UtcNow);
            var retryMethod = typeof(CopilotChatViewModel).GetMethod("RetryAutomaticallyDeniedActionAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("RetryAutomaticallyDeniedActionAsync was not found.");

            await Assert.IsAssignableFrom<Task>(retryMethod.Invoke(viewModel, [denial]));

            Assert.NotNull(acceptedSteering);
            Assert.Equal(activeRun.Id, acceptedTaskId);
            Assert.Contains("WriteProjectFile", acceptedSteering.Text, StringComparison.Ordinal);
            Assert.StartsWith("A newer draft.", conversation.DraftText, StringComparison.Ordinal);
            Assert.Contains(acceptedSteering.Text, conversation.DraftText, StringComparison.Ordinal);
            Assert.Equal(conversation.DraftText, viewModel.InputText);
            Assert.Equal(CopilotAgentMode.Chat, conversation.DraftRequestMode);
            Assert.Same(attachment, Assert.Single(conversation.Attachments));
            Assert.Equal(existingRecoveries, conversation.PendingSteeringRecoveries.ToArray());
            Assert.Contains("恢复记录未能保存", viewModel.PendingActionFeedbackText, StringComparison.Ordinal);
            Assert.Contains("草稿", viewModel.PendingActionFeedbackText, StringComparison.Ordinal);
            var recoveredDraft = conversation.DraftText;

            ApplyAgentEvents(viewModel, activeRun, conversation, assistantMessage,
                CopilotAgentEvent.SteeringRecovery([acceptedSteering]));

            Assert.Equal(recoveredDraft, conversation.DraftText);
            Assert.Equal(recoveredDraft, viewModel.InputText);
            Assert.Equal(existingRecoveries, conversation.PendingSteeringRecoveries.ToArray());
        }
        finally
        {
            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task ScheduledTurnConsumesCapturedAttachmentsButPreservesAttachmentsAddedDuringScheduling()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "composer-attachment-race-test-token");
        var conversation = CreateConversation(profile, "conversation-a", "original draft");
        var capturedAttachment = CopilotAttachmentItem.CreateContext("captured context");
        var lateAttachment = CopilotAttachmentItem.CreateContext("late context");
        conversation.Attachments.Add(capturedAttachment);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        CopilotHostedAgentRun? activeRun = null;
        EventHandler<CopilotAgentTaskHostChangedEventArgs>? onHostChanged = null;
        try
        {
            onHostChanged = (_, args) =>
            {
                if (args.Kind == CopilotAgentTaskHostChangeKind.Started)
                    conversation.Attachments.Add(lateAttachment);
            };
            taskHost.Changed += onHostChanged;

            viewModel.SendCommand.Execute(null);

            await runtime.Entered.WaitAsync(TestTimeout);
            activeRun = taskHost.ActiveRun;
            Assert.NotNull(activeRun);
            Assert.Equal(string.Empty, viewModel.InputText);
            Assert.DoesNotContain(capturedAttachment, conversation.Attachments);
            Assert.Contains(lateAttachment, conversation.Attachments);

            runtime.Release();
            await activeRun.Completion.WaitAsync(TestTimeout);
        }
        finally
        {
            if (onHostChanged != null)
                taskHost.Changed -= onHostChanged;
            await CompleteAndDisposeAsync(viewModel, runtime, taskHost, activeRun);
        }
    }

    [Fact]
    public async Task NewerComposerEditDuringSchedulingIsNotClearedByTheCapturedTurn()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "composer-stale-token-test-token");
        var conversation = CreateConversation(profile, "conversation-a", "original draft");
        conversation.DraftRequestMode = CopilotAgentMode.Review;
        conversation.DraftWorkspaceReviewTarget = CopilotWorkspaceReviewTargetContext.WorkingTree();
        var capturedAttachment = CopilotAttachmentItem.CreateContext("captured context");
        conversation.Attachments.Add(capturedAttachment);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        CopilotHostedAgentRun? activeRun = null;
        EventHandler<CopilotAgentTaskHostChangedEventArgs>? onHostChanged = null;
        try
        {
            onHostChanged = (_, args) =>
            {
                if (args.Kind == CopilotAgentTaskHostChangeKind.Started)
                    viewModel.InputText = "newer draft";
            };
            taskHost.Changed += onHostChanged;

            viewModel.SendCommand.Execute(null);

            await runtime.Entered.WaitAsync(TestTimeout);
            activeRun = taskHost.ActiveRun;
            Assert.NotNull(activeRun);
            Assert.Equal("newer draft", viewModel.InputText);
            Assert.Equal("newer draft", conversation.DraftText);
            Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
            Assert.NotNull(conversation.DraftWorkspaceReviewTarget);
            Assert.Contains(capturedAttachment, conversation.Attachments);

            runtime.Release();
            await activeRun.Completion.WaitAsync(TestTimeout);
        }
        finally
        {
            if (onHostChanged != null)
                taskHost.Changed -= onHostChanged;
            await CompleteAndDisposeAsync(viewModel, runtime, taskHost, activeRun);
        }
    }

    [Fact]
    public async Task QueuedSlashCommandRunsInItsOriginConversationAndRestoresLaterSelection()
    {
        var profileA = CreateProfile("profile-a", "Profile A", "model-a");
        var profileB = CreateProfile("profile-b", "Profile B", "model-b");
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "queued-command-conversation-binding-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig> { profileA, profileB },
        };
        var conversationA = CreateConversation(profileA, "conversation-a", string.Empty);
        var conversationB = CreateConversation(profileB, "conversation-b", "conversation B draft");
        var state = new CopilotChatState
        {
            ActiveConversationId = conversationA.Id,
            ActiveProfileId = profileA.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord>
            {
                conversationA,
                conversationB,
            },
        };
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        var activeRun = taskHost.Start(
            conversationA.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/status";
            viewModel.QueueFollowUpCommand.Execute(null);
            var queuedRun = Assert.Single(taskHost.QueuedRuns);

            Assert.True(viewModel.SelectConversationCommand.CanExecute(conversationB));
            viewModel.SelectConversationCommand.Execute(conversationB);
            Assert.Same(conversationB, viewModel.SelectedConversation);

            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            await queuedRun.Completion.WaitAsync(TestTimeout);

            Assert.Contains("/status", viewModel.LocalCommandResultTitle, StringComparison.Ordinal);
            Assert.Same(conversationB, viewModel.SelectedConversation);
            Assert.Same(profileB, viewModel.SelectedProfile);
            Assert.Equal("conversation B draft", viewModel.InputText);
            Assert.Empty(conversationA.Messages);
            Assert.Empty(conversationB.Messages);
        }
        finally
        {
            releaseActive.TrySetResult();
            if (!activeRun.Completion.IsCompleted)
                await activeRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public void ExpandedComposerChangesOnlyTheCapturedDraftText()
    {
        var profile = CreateProfile("editor-profile", "Editor", "editor-model");
        var conversation = CreateConversation(profile, "editor-conversation", "Original draft");
        conversation.DraftRequestMode = CopilotAgentMode.Review;
        conversation.DraftWorkspaceReviewTarget = CopilotWorkspaceReviewTargetContext.WorkingTree();
        var attachment = CopilotAttachmentItem.CreateContext("Keep this attachment");
        conversation.Attachments.Add(attachment);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(conversation, CreateConfig(profile, "editor-test-token"),
            new GatedFailingTurnRuntime(), new CopilotAgentTaskHost());
        conversation.ComposerStash = CopilotComposerStash.Capture("Existing stash", 4, CopilotAgentMode.Auto, []);
        var stash = conversation.ComposerStash;
        var capture = viewModel.CaptureComposerEditorSession();
        const string editedText = "  Edited draft\nwith preserved whitespace  ";

        Assert.True(viewModel.TryApplyComposerEditor(capture, editedText));

        Assert.Equal(editedText, viewModel.InputText);
        Assert.Equal(editedText, conversation.DraftText);
        Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
        Assert.Equal(CopilotWorkspaceReviewTarget.WorkingTree, conversation.DraftWorkspaceReviewTarget?.Target);
        Assert.Same(attachment, Assert.Single(conversation.Attachments));
        Assert.Same(stash, conversation.ComposerStash);
        Assert.Equal("Original draft", capture.Text);
    }

    [Fact]
    public void ExpandedComposerRejectsQueuedRecoveryUntilTheRecoveredDraftIsReviewed()
    {
        var profile = CreateProfile("editor-recovery-profile", "Editor recovery", "editor-model");
        var conversation = CreateConversation(profile, "editor-recovery-conversation", "Draft before opening the editor");
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id, ActiveProfileId = profile.Id,
            Conversations = [conversation],
        };
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = new CopilotChatViewModel(new CopilotChatService(), new InMemoryStateStore(state),
            CreateConfig(profile, "editor-recovery-test-token"), new GatedFailingTurnRuntime(), new CopilotAgentTaskHost());
        var capture = viewModel.CaptureComposerEditorSession();
        state.QueuedFollowUpRecoveries.Add(new CopilotQueuedFollowUpRecoveryRecord
        {
            RunId = "editor-recovery-run", ConversationId = conversation.Id,
            Prompt = "Recover the queued request without losing it",
        });
        Assert.True(CopilotQueuedFollowUpRecovery.RestoreRecordToDraft(state, "editor-recovery-run"));
        typeof(CopilotChatViewModel).GetMethod("SynchronizeSelectedDraftAfterQueuedRecovery", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, null);
        var recoveredText = viewModel.InputText;
        Assert.Contains("Recover the queued request without losing it", recoveredText, StringComparison.Ordinal);
        Assert.Empty(state.QueuedFollowUpRecoveries);

        Assert.False(viewModel.TryApplyComposerEditor(capture, "Edits made in the open editor"));
        Assert.Equal(recoveredText, viewModel.InputText);
        Assert.Equal(recoveredText, conversation.DraftText);

        var reviewedCapture = viewModel.CaptureComposerEditorSession();
        var mergedText = "Edits made in the open editor\n\n" + reviewedCapture.Text;
        Assert.True(viewModel.TryApplyComposerEditor(reviewedCapture, mergedText));
        Assert.Equal(mergedText, conversation.DraftText);
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void ExpandedComposerCannotOverwriteAnotherConversationOrReuseASwitchedBackCapture()
    {
        var profile = CreateProfile("editor-switch-profile", "Editor switch", "editor-model");
        var conversation = CreateConversation(profile, "editor-origin", "Origin draft");
        var other = CreateConversation(profile, "editor-other", "Other draft");
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(conversation, CreateConfig(profile, "editor-switch-test-token"),
            new GatedFailingTurnRuntime(), new CopilotAgentTaskHost());
        viewModel.Conversations.Add(other);
        var capture = viewModel.CaptureComposerEditorSession();

        Assert.True(viewModel.TrySelectConversation(other.Id));
        Assert.False(viewModel.TryApplyComposerEditor(capture, "Stale editor text"));
        Assert.Equal("Other draft", viewModel.InputText);
        Assert.True(viewModel.TrySelectConversation(conversation.Id));
        Assert.False(viewModel.TryApplyComposerEditor(capture, "Stale editor text"));
        Assert.Equal("Origin draft", conversation.DraftText);
        Assert.Equal("Other draft", other.DraftText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpandedComposerDoesNotWriteIntoHistorySearchOrADisposedViewModel(bool dispose)
    {
        var profile = CreateProfile("editor-lifetime-profile", "Editor lifetime", "editor-model");
        var conversation = CreateConversation(profile, "editor-lifetime-conversation", "Preserved draft");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Visible request to search"));
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(conversation, CreateConfig(profile, "editor-lifetime-test-token"),
            new GatedFailingTurnRuntime(), new CopilotAgentTaskHost());
        var capture = viewModel.CaptureComposerEditorSession();
        if (dispose)
            viewModel.Dispose();
        else
        {
            Assert.True(viewModel.TryOpenPromptHistorySearch());
            viewModel.InputText = "Visible request";
        }

        Assert.False(viewModel.TryApplyComposerEditor(capture, "Stale editor text"));
        Assert.Equal("Preserved draft", conversation.DraftText);
        if (!dispose)
            Assert.Equal("Visible request", viewModel.InputText);
    }

    [Fact]
    public void ExpandedComposerDoesNotReuseThePreviousAttachmentOnlyMessageEditLifetime()
    {
        var profile = CreateProfile("editor-message-profile", "Editor message", "editor-model");
        var conversation = CreateConversation(profile, "editor-message-conversation", string.Empty);
        var user = new CopilotChatMessage(CopilotChatRole.User, string.Empty)
        {
            RequestMode = CopilotAgentMode.Auto,
            AttachmentSnapshotCaptured = true,
        };
        user.Attachments.Add(CopilotAttachmentItem.CreateContext("Attachment-only request"));
        conversation.Messages.Add(user);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Completed answer"));
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(conversation, CreateConfig(profile, "editor-message-test-token"),
            new GatedFailingTurnRuntime(), new CopilotAgentTaskHost());
        Assert.True(viewModel.EditMessageCommand.CanExecute(user));
        viewModel.EditMessageCommand.Execute(user);
        var capture = viewModel.CaptureComposerEditorSession();

        viewModel.CancelMessageEditCommand.Execute(null);
        viewModel.EditMessageCommand.Execute(user);
        var reopenedCapture = viewModel.CaptureComposerEditorSession();
        Assert.Equal(capture.Composer.Version, reopenedCapture.Composer.Version);
        Assert.Equal(capture.EditingUserMessageId, reopenedCapture.EditingUserMessageId);
        Assert.True(viewModel.IsEditingMessage);

        Assert.False(viewModel.TryApplyComposerEditor(capture, "Editor text from the previous edit"));
        Assert.Empty(viewModel.InputText);
        Assert.Single(conversation.Attachments);
        Assert.True(viewModel.TryApplyComposerEditor(reopenedCapture, "Text for the current edit"));
        Assert.Equal("Text for the current edit", conversation.DraftText);
    }

    [Theory]
    [InlineData(ModifierKeys.None, true)]
    [InlineData(ModifierKeys.Shift, false)]
    [InlineData(ModifierKeys.Control, false)]
    public void OnlyPlainTabCompletesTheComposerCommandWithoutSubmittingIt(ModifierKeys modifiers, bool completes)
    {
        StaTest.Run(() =>
        {
            using var solutionManagerScope = new IsolatedSolutionManagerScope();
            var profile = CreateProfile("tab-profile", "Tab completion", "tab-model");
            var conversation = CreateConversation(profile, "tab-conversation", "/mod");
            var host = new CopilotAgentTaskHost();
            var viewModel = CreateViewModel(conversation, CreateConfig(profile, "tab-test-token"),
                new GatedFailingTurnRuntime(), host);
            var panel = new CopilotChatPanel { DataContext = viewModel };
            var window = new Window { Content = panel, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                var source = Assert.IsType<HwndSource>(HwndSource.FromHwnd(new WindowInteropHelper(window).EnsureHandle()));
                Assert.False(window.IsVisible);
                var prompt = Assert.IsType<TextBox>(panel.FindName("PromptTextBox"));
                prompt.GetBindingExpression(TextBox.TextProperty)!.UpdateTarget();
                var completion = Assert.Single(viewModel.LocalCommandSuggestions).CompletionText;
                Assert.Equal("/model ", completion);
                var keyboard = new ComposerTestKeyboardDevice(modifiers);
                Assert.Equal(modifiers, keyboard.Modifiers);
                var keyEvent = new KeyEventArgs(keyboard, source, Environment.TickCount, Key.Tab)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                };

                prompt.RaiseEvent(keyEvent);

                Assert.Equal(completes, keyEvent.Handled);
                Assert.Equal(completes ? completion : "/mod", viewModel.InputText);
                Assert.Equal(viewModel.InputText, conversation.DraftText);
                Assert.Empty(conversation.Messages);
                Assert.Equal(profile.Id, viewModel.SelectedProfile?.Id);
            }
            finally
            {
                panel.DataContext = null;
                window.Content = null;
                window.Close();
                host.Shutdown();
                viewModel.Dispose();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
    }

    private sealed class ComposerTestKeyboardDevice(ModifierKeys modifiers) : KeyboardDevice(InputManager.Current)
    {
        protected override KeyStates GetKeyStatesFromSystem(Key key) =>
            key == Key.Tab
                || key == Key.LeftShift && modifiers.HasFlag(ModifierKeys.Shift)
                || key == Key.LeftCtrl && modifiers.HasFlag(ModifierKeys.Control)
                || key == Key.LeftAlt && modifiers.HasFlag(ModifierKeys.Alt)
                    ? KeyStates.Down : KeyStates.None;
    }

    private static CopilotConfig CreateConfig(CopilotProfileConfig profile, string bearerToken) => new()
    {
        SchemaVersion = CopilotConfig.CurrentSchemaVersion,
        McpBearerToken = bearerToken,
        Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
    };

    private static CopilotConversationRecord CreateConversation(
        CopilotProfileConfig profile,
        string id,
        string draftText)
    {
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        conversation.Id = id;
        conversation.DraftText = draftText;
        return conversation;
    }

    private static CopilotChatViewModel CreateViewModel(
        CopilotConversationRecord conversation,
        CopilotConfig config,
        ICopilotTurnRuntime runtime,
        CopilotAgentTaskHost taskHost,
        string attachmentDirectoryPath = "")
    {
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = conversation.ProfileId,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
        return new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state, attachmentDirectoryPath),
            config,
            runtime,
            taskHost);
    }

    private static void ApplyAgentEvents(
        CopilotChatViewModel viewModel,
        CopilotHostedAgentRun hostedRun,
        CopilotConversationRecord conversation,
        CopilotChatMessage assistantMessage,
        params CopilotAgentEvent[] agentEvents)
    {
        var method = typeof(CopilotChatViewModel).GetMethod(
            "ApplyAgentEvents",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ApplyAgentEvents was not found.");
        method.Invoke(
            viewModel,
            [hostedRun, conversation, assistantMessage, agentEvents]);
    }

    private static async Task CompleteAndDisposeAsync(
        CopilotChatViewModel viewModel,
        GatedFailingTurnRuntime runtime,
        CopilotAgentTaskHost taskHost,
        CopilotHostedAgentRun? activeRun)
    {
        runtime.Release();
        var pendingRun = activeRun ?? taskHost.ActiveRun;
        if (pendingRun != null && !pendingRun.Completion.IsCompleted)
        {
            try
            {
                await pendingRun.Completion.WaitAsync(TestTimeout);
            }
            catch
            {
            }
        }
        viewModel.Dispose();
    }

    private static CopilotProfileConfig CreateProfile(string id, string name, string model) => new()
    {
        Id = id,
        Name = name,
        VendorType = CopilotVendorType.Custom,
        ProviderType = CopilotProviderType.OpenAICompatible,
        ApiKey = "profile-isolation-test-key",
        BaseUrl = "https://unit.test/v1",
        Model = model,
    };

    private sealed class GatedFailingTurnRuntime(
        bool emitPendingChatDelta = false,
        Func<string, string, CopilotSteeringAdmissionResult>? steeringAdmission = null) : ICopilotTurnRuntime
    {
        public const string PendingChatTail = "Accepted Chat tail before the view closes.";
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _pendingChatDeltaAccepted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task PendingChatDeltaAccepted => _pendingChatDeltaAccepted.Task;

        public Task<CopilotTurnRequest> Entered => _entered.Task;

        private readonly TaskCompletionSource<CopilotTurnRequest> _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _entered.TrySetResult(request);
            if (emitPendingChatDelta)
            {
                yield return new CopilotTurnStartedEvent(request.TaskId, request.Mode);
                yield return new CopilotTurnRequestPreparedEvent(new CopilotPreparedTurnRequest(request.UserText, false));
                yield return new CopilotTurnChatDeltaEvent(new CopilotStreamDelta(string.Empty, PendingChatTail));
                _pendingChatDeltaAccepted.TrySetResult();
            }
            await _release.Task.WaitAsync(cancellationToken);
            if (!emitPendingChatDelta)
                yield return new CopilotTurnStartedEvent("profile-isolation-turn", request.Mode);
            throw new InvalidOperationException("Expected profile-isolation test failure.");
        }

        public void Release() => _release.TrySetResult();

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) =>
            steeringAdmission?.Invoke(taskId, message) ?? new(CopilotSteeringAdmissionReason.RuntimeUnavailable);

        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;

        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;

        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;

        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(
            CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken) =>
            Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }

    private sealed class ImageUsageContextProvider(TaskCompletionSource release) : ICopilotContextProvider
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Order => int.MinValue;
        public bool CanProvide(CopilotContextScope scope) => scope == CopilotContextScope.Agent;
        public void Release() => release.TrySetResult();

        public async Task<CopilotContextItem?> CaptureAsync(CopilotContextRequest request, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            try
            {
                await release.Task.WaitAsync(cancellationToken);
                return null;
            }
            finally { Exited.TrySetResult(); }
        }
    }

    private sealed class ImageUsageHandler(string[] responses, TaskCompletionSource? responseDisposed = null) : HttpMessageHandler
    {
        private int _requestCount;
        public ConcurrentQueue<(string Path, string Body)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Enqueue((request.RequestUri!.AbsolutePath,
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            var index = Interlocked.Increment(ref _requestCount) - 1;
            Assert.True(index < responses.Length, "The provider received an unexpected additional image or main request.");
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = responseDisposed == null
                    ? new StringContent(responses[index], System.Text.Encoding.UTF8, "application/json")
                    : new ImageUsageContent(responses[index], responseDisposed),
            };
        }
    }

    private sealed class ImageUsageContent(string body, TaskCompletionSource disposed)
        : StringContent(body, System.Text.Encoding.UTF8, "application/json")
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                disposed.TrySetResult();
        }
    }

    private sealed class GatedCompactionHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<HttpResponseMessage> _response =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<CancellationToken> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RequestCount { get; private set; }

        public void Respond(string body, System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK,
            string contentType = "application/json")
        {
            Assert.True(_response.TrySetResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, contentType),
            }));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Entered.TrySetResult(cancellationToken);
            try
            {
                return await _response.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Canceled.TrySetResult();
                throw;
            }
        }
    }

    private sealed class InMemoryStateStore(
        CopilotChatState state,
        string attachmentDirectoryPath = "",
        Func<CancellationToken, Task>? saveSerializedAsync = null) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath { get; } = attachmentDirectoryPath;

        public CopilotChatState Load() => state;

        public void Save(CopilotChatState value)
        {
        }

        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) =>
            new(new JObject());

        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";

        public string Serialize(CopilotChatState value) => "{}";

        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return saveSerializedAsync?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }

        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private sealed class IsolatedSolutionManagerScope : IDisposable
    {
        private static readonly FieldInfo InstanceField = typeof(SolutionManager).GetField(
            "_instance",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SolutionManager singleton field was not found.");

        private readonly object? _previousInstance = InstanceField.GetValue(null);
        private readonly SolutionManager _testInstance =
            (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));

        public IsolatedSolutionManagerScope()
        {
            InstanceField.SetValue(null, _testInstance);
        }

        public void Dispose()
        {
            if (ReferenceEquals(InstanceField.GetValue(null), _testInstance))
                InstanceField.SetValue(null, _previousInstance);
        }
    }

    private sealed class HeldChatSynchronizationContext : SynchronizationContext
    {
        private readonly object _syncRoot = new();
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        private bool _released;

        public override void Post(SendOrPostCallback callback, object? state)
        {
            lock (_syncRoot)
            {
                if (!_released)
                {
                    _callbacks.Enqueue((callback, state));
                    return;
                }
            }
            base.Post(callback, state);
        }

        public void Release()
        {
            (SendOrPostCallback Callback, object? State)[] callbacks;
            lock (_syncRoot)
            {
                _released = true;
                callbacks = _callbacks.ToArray();
                _callbacks.Clear();
            }
            foreach (var callback in callbacks)
                base.Post(callback.Callback, callback.State);
        }
    }
}
