using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    [Fact]
    public void ClipboardImageCanCreateMultipleMissingStorageDirectories()
    {
        StaTest.Run(() =>
        {
            var root = Directory.CreateTempSubdirectory("CopilotClipboardStorage-").FullName;
            var storePath = Path.Combine(root, "new-parent", "nested", "attachments");
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

                var savedPath = Assert.IsType<string>(saveMethod.Invoke(viewModel, [image, CancellationToken.None]));

                Assert.Equal(storePath, Path.GetDirectoryName(savedPath));
                Assert.Equal(savedPath, Assert.Single(Directory.GetFiles(storePath, "clipboard-*.png")));
                var savedImage = BitmapFrame.Create(new Uri(savedPath), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                Assert.Equal(4, savedImage.PixelWidth);
                Assert.Equal(3, savedImage.PixelHeight);
                Assert.Equal("preserved draft", conversation.DraftText);
            }
            finally
            {
                viewModel.Dispose();
                Directory.Delete(root, recursive: true);
            }
        }, TimeSpan.FromSeconds(20), "The STA clipboard lifecycle test did not finish.");
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

    [Theory]
    [InlineData("cancel")]
    [InlineData("dispose")]
    [InlineData("complete")]
    [InlineData("cancel-edit")]
    [InlineData("complete-edit")]
    public void ClipboardImageCompletedBeforeUiContinuationIsKeptOnlyWhenAttached(string transition)
    {
        StaTest.Run(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "CopilotClipboardLifecycle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var profile = CreateProfile("clipboard", "Clipboard", "test-model");
            var editing = transition.EndsWith("-edit", StringComparison.Ordinal);
            var conversation = CreateConversation(profile, "clipboard-conversation", editing ? string.Empty : "preserved draft");
            var existingFile = Path.Combine(root, "existing.png");
            File.WriteAllBytes(existingFile, [1, 2, 3]);
            var existingAttachment = CopilotAttachmentItem.CreateImage(existingFile);
            var originalUser = new CopilotChatMessage(CopilotChatRole.User, "Original image request")
            {
                RequestMode = CopilotAgentMode.Auto,
                AttachmentSnapshotCaptured = true,
            };
            if (editing)
            {
                originalUser.Attachments.Add(existingAttachment);
                conversation.Messages.Add(originalUser);
                conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Original image answer"));
            }
            else
            {
                conversation.Attachments.Add(existingAttachment);
            }
            var state = new CopilotChatState { ActiveConversationId = conversation.Id, ActiveProfileId = profile.Id, Conversations = [conversation] };
            var config = new CopilotConfig { SchemaVersion = CopilotConfig.CurrentSchemaVersion, McpBearerToken = "test-token", Profiles = [profile] };
            using var solutionManagerScope = new IsolatedSolutionManagerScope();
            using var viewModel = new CopilotChatViewModel(new CopilotChatService(), new InMemoryStateStore(state, root), config, new GatedFailingTurnRuntime(), new CopilotAgentTaskHost());
            using var context = new PausedAttachmentSynchronizationContext();
            var previousContext = SynchronizationContext.Current;
            try
            {
                if (editing)
                {
                    Assert.True(viewModel.EditMessageCommand.CanExecute(originalUser));
                    viewModel.EditMessageCommand.Execute(originalUser);
                    Assert.True(viewModel.IsEditingMessage);
                    existingAttachment = Assert.Single(conversation.Attachments);
                }
                var pixels = new byte[512 * 512 * 4];
                new Random(42).NextBytes(pixels);
                var image = BitmapSource.Create(512, 512, 96, 96, PixelFormats.Bgra32, null, pixels, 512 * 4);
                image.Freeze();
                var saveMethod = typeof(CopilotChatViewModel).GetMethod("SaveClipboardImageAttachmentAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                SynchronizationContext.SetSynchronizationContext(context);
                var operation = Assert.IsType<Task<bool>>(saveMethod.Invoke(viewModel, [image]), exactMatch: false);
                Assert.True(context.CallbackPosted.Wait(TestTimeout), "The clipboard save continuation was not queued.");
                Assert.False(operation.IsCompleted);
                var savedFile = Assert.Single(Directory.GetFiles(root, "clipboard-*.png"));
                Assert.Same(existingAttachment, Assert.Single(conversation.Attachments));

                if (transition == "cancel")
                    viewModel.PrimaryActionCommand.Execute(null);
                else if (transition == "dispose")
                    viewModel.Dispose();
                else if (transition == "cancel-edit")
                {
                    Assert.True(viewModel.CancelMessageEditCommand.CanExecute(null));
                    viewModel.CancelMessageEditCommand.Execute(null);
                    viewModel.InputText = "New draft after cancelling the image request edit";
                }

                context.RunPending();
                Assert.True(operation.IsCompleted);
                var attached = transition is "complete" or "complete-edit";
                Assert.Equal(attached, operation.GetAwaiter().GetResult());
                Assert.Equal(attached, File.Exists(savedFile));
                Assert.True(File.Exists(existingFile));
                Assert.Equal(transition == "cancel-edit"
                    ? "New draft after cancelling the image request edit"
                    : editing ? originalUser.Content : "preserved draft", conversation.DraftText);
                Assert.False(viewModel.IsBusy);
                if (attached)
                {
                    Assert.Equal(2, conversation.Attachments.Count);
                    Assert.Contains(conversation.Attachments, attachment => attachment.Value == savedFile);
                }
                else if (transition == "cancel-edit")
                {
                    Assert.Empty(conversation.Attachments);
                }
                else
                {
                    Assert.Same(existingAttachment, Assert.Single(conversation.Attachments));
                }
                if (editing)
                {
                    Assert.Equal(transition == "complete-edit", viewModel.IsEditingMessage);
                    Assert.Equal(existingFile, Assert.Single(originalUser.Attachments).Value);
                    Assert.Equal(2, conversation.Messages.Count);
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
                viewModel.Dispose();
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

    [Fact]
    public void DeliveredSteeringRecoveryRecordIsCommittedWithTheNextCheckpoint()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "steering-checkpoint-commit-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        conversation.Messages.Add(assistantMessage);
        var steeringMessage = new CopilotSteeringMessageSnapshot(
            "steering:checkpoint-commit",
            "keep the current constraint");

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:checkpoint-commit");
        try
        {
            Assert.True(CopilotSteeringRecovery.TrackPending(
                conversation,
                hostedRun.Id,
                steeringMessage,
                DateTimeOffset.UtcNow));
            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.SteeringDelivered([steeringMessage]));

            Assert.Single(conversation.PendingSteeringRecoveries);

            var taskLedger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                Items =
                [
                    new CopilotAgentTaskItem
                    {
                        Id = 1,
                        Title = "Persist steering",
                        Description = "Commit delivered steering with the checkpoint.",
                    },
                ],
            };
            var staleCheckpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = "profile-a|model-a",
                SerializedSessionJson = "{}",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.CheckpointUpdated(staleCheckpoint, taskLedger));

            Assert.Single(conversation.PendingSteeringRecoveries);

            var checkpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = "profile-a|model-a",
                SerializedSessionJson = "{}",
                ConversationMemory =
                [
                    new CopilotRequestMessage("user", steeringMessage.Text)
                    {
                        IsSteering = true,
                    },
                ],
                UpdatedAtUtc = staleCheckpoint.UpdatedAtUtc.AddSeconds(1),
            };
            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.CheckpointUpdated(checkpoint, taskLedger));

            Assert.Empty(conversation.PendingSteeringRecoveries);
            var committedCheckpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(
                conversation.AgentSessionCheckpoint);
            Assert.NotSame(checkpoint, committedCheckpoint);
            Assert.Equal(checkpoint.UpdatedAtUtc, committedCheckpoint.UpdatedAtUtc);
            Assert.Equal(checkpoint.ConversationMemory, committedCheckpoint.ConversationMemory);
        }
        finally
        {
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public void RejectedCheckpointCannotCommitDeliveredSteeringRecovery()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "rejected-steering-checkpoint-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        conversation.Messages.Add(assistantMessage);
        var steeringMessage = new CopilotSteeringMessageSnapshot(
            "steering:rejected-checkpoint",
            "retain this instruction until durable");
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var regressingJournal = journal.Snapshot();
        journal.RecordStop(CopilotAgentStopReason.Paused);
        var currentCheckpoint = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "profile-a|model-a",
            SerializedSessionJson = "{}",
            TaskEventJournal = journal.Snapshot(),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        Assert.True(conversation.SetAgentSessionCheckpoint(currentCheckpoint));
        var acceptedCheckpoint = conversation.AgentSessionCheckpoint;

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:rejected-checkpoint");
        try
        {
            Assert.True(CopilotSteeringRecovery.TrackPending(
                conversation,
                hostedRun.Id,
                steeringMessage,
                DateTimeOffset.UtcNow));
            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.SteeringDelivered([steeringMessage]));

            var rejectedCheckpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = "profile-a|model-a",
                SerializedSessionJson = "{}",
                ConversationMemory =
                [
                    new CopilotRequestMessage("user", steeringMessage.Text)
                    {
                        IsSteering = true,
                    },
                ],
                TaskEventJournal = regressingJournal,
                UpdatedAtUtc = currentCheckpoint.UpdatedAtUtc.AddSeconds(1),
            };
            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.CheckpointUpdated(
                    rejectedCheckpoint,
                    new CopilotAgentTaskLedgerSnapshot
                    {
                        Mode = "execute",
                        Items =
                        [
                            new CopilotAgentTaskItem
                            {
                                Id = 1,
                                Title = "Retain steering",
                                Description = "Do not commit recovery through rejected evidence.",
                            },
                        ],
                    }));

            Assert.Same(acceptedCheckpoint, conversation.AgentSessionCheckpoint);
            Assert.Single(conversation.PendingSteeringRecoveries);
        }
        finally
        {
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public void RejectedTerminalCheckpointRestoresDeliveredSteeringToDraft()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "rejected-terminal-checkpoint-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        conversation.Messages.Add(assistantMessage);
        var steeringMessage = new CopilotSteeringMessageSnapshot(
            "steering:rejected-terminal-checkpoint",
            "restore this terminal instruction");
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var rejectedJournal = journal.Snapshot();
        journal.RecordStop(CopilotAgentStopReason.Paused);
        var currentCheckpoint = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "profile-a|model-a",
            SerializedSessionJson = "{}",
            TaskEventJournal = journal.Snapshot(),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        conversation.SetAgentSessionCheckpoint(currentCheckpoint);
        var acceptedCheckpoint = conversation.AgentSessionCheckpoint;

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:rejected-terminal-checkpoint");
        try
        {
            Assert.True(CopilotSteeringRecovery.TrackPending(
                conversation,
                hostedRun.Id,
                steeringMessage,
                DateTimeOffset.UtcNow));
            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.SteeringDelivered([steeringMessage]));
            var rejectedCheckpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = "profile-a|model-a",
                SerializedSessionJson = "{}",
                ConversationMemory =
                [
                    new CopilotRequestMessage("user", steeringMessage.Text)
                    {
                        IsSteering = true,
                    },
                ],
                TaskEventJournal = rejectedJournal,
                UpdatedAtUtc = currentCheckpoint.UpdatedAtUtc.AddSeconds(1),
            };

            var accepted = CommitAgentRunStateAndResolveSteering(
                viewModel,
                hostedRun,
                conversation,
                rejectedJournal,
                rejectedCheckpoint,
                CopilotAgentStopReason.Paused);

            Assert.False(accepted);
            Assert.Same(acceptedCheckpoint, conversation.AgentSessionCheckpoint);
            Assert.Empty(conversation.PendingSteeringRecoveries);
            Assert.Contains(steeringMessage.Text, conversation.DraftText, StringComparison.Ordinal);
        }
        finally
        {
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task PausedBoundGoalStillAccountsTheCompletedTurn()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "paused-goal-accounting-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var createdAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var activeGoal = CopilotConversationGoal.Create("Finish the verified runtime slice", createdAt);
        conversation.Goal = activeGoal.WithState(
            CopilotConversationGoalState.Paused,
            createdAt.AddMinutes(1),
            "用户暂停");
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, activeGoal.Objective)
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        conversation.Messages.Add(userMessage);
        conversation.Messages.Add(assistantMessage);
        var turnUsage = new CopilotTokenUsage(10, 5, 15);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:paused-goal-accounting");
        try
        {
            await ProcessGoalAfterTurnAsync(
                viewModel,
                hostedRun,
                conversation,
                profile,
                userMessage,
                assistantMessage,
                activeGoal.Id,
                turnUsage);

            Assert.NotNull(conversation.Goal);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Equal(1, conversation.Goal.TurnCount);
            Assert.Equal(turnUsage.EffectiveTotalTokens, conversation.Goal.TokensUsed);
            var iteration = Assert.Single(conversation.Goal.IterationLog);
            Assert.False(iteration.Evaluated);
            Assert.False(iteration.ContinuationCounted);
        }
        finally
        {
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task PausingDuringGoalEvaluationAccountsEvaluationUsageWithoutApplyingItsVerdict()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "paused-goal-evaluation-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var createdAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var activeGoal = CopilotConversationGoal.Create("Finish the verified runtime slice", createdAt);
        conversation.Goal = activeGoal;
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, activeGoal.Objective)
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        conversation.Messages.Add(userMessage);
        conversation.Messages.Add(assistantMessage);
        var turnUsage = new CopilotTokenUsage(10, 5, 15);
        var evaluationUsage = new CopilotTokenUsage(3, 2, 5);
        var evaluator = new GatedGoalCompletionEvaluator(evaluationUsage);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        var evaluatorField = typeof(CopilotChatViewModel).GetField(
            "_goalCompletionEvaluator",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Goal completion evaluator field was not found.");
        evaluatorField.SetValue(viewModel, evaluator);
        conversation.Goal = activeGoal;
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:paused-goal-evaluation");
        try
        {
            var processing = ProcessGoalAfterTurnAsync(
                viewModel,
                hostedRun,
                conversation,
                profile,
                userMessage,
                assistantMessage,
                activeGoal.Id,
                turnUsage);
            await evaluator.Entered.WaitAsync(TestTimeout);
            conversation.Goal = activeGoal.WithState(
                CopilotConversationGoalState.Paused,
                createdAt.AddMinutes(1),
                "用户暂停");
            evaluator.Release();
            await processing;

            Assert.NotNull(conversation.Goal);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Equal(1, conversation.Goal.TurnCount);
            Assert.Equal(0, conversation.Goal.EvaluationCount);
            Assert.Equal(
                turnUsage.Add(evaluationUsage).EffectiveTotalTokens,
                conversation.Goal.TokensUsed);
            var iteration = Assert.Single(conversation.Goal.IterationLog);
            Assert.False(iteration.Evaluated);
            Assert.False(iteration.ContinuationCounted);
        }
        finally
        {
            evaluator.Release();
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task CancellingGoalEvaluationAfterCompletedTurnPreservesAccountingAndPausesGoal()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "cancelled-goal-evaluation-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var createdAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var activeGoal = CopilotConversationGoal.Create("Finish the verified runtime slice", createdAt);
        conversation.Goal = activeGoal;
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, activeGoal.Objective)
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        conversation.Messages.Add(userMessage);
        conversation.Messages.Add(assistantMessage);
        var turnUsage = new CopilotTokenUsage(10, 5, 15);
        var evaluator = new GatedGoalCompletionEvaluator(CopilotTokenUsage.Empty);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        var evaluatorField = typeof(CopilotChatViewModel).GetField(
            "_goalCompletionEvaluator",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Goal completion evaluator field was not found.");
        evaluatorField.SetValue(viewModel, evaluator);
        conversation.Goal = activeGoal;
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:cancelled-goal-evaluation");
        try
        {
            var processing = ProcessGoalAfterTurnAsync(
                viewModel,
                hostedRun,
                conversation,
                profile,
                userMessage,
                assistantMessage,
                activeGoal.Id,
                turnUsage);
            await evaluator.Entered.WaitAsync(TestTimeout);

            Assert.True(hostedRun.TryRequestCancel());
            await processing;

            Assert.NotNull(conversation.Goal);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Equal(1, conversation.Goal.TurnCount);
            Assert.Equal(0, conversation.Goal.EvaluationCount);
            Assert.Equal(turnUsage.EffectiveTotalTokens, conversation.Goal.TokensUsed);
            var iteration = Assert.Single(conversation.Goal.IterationLog);
            Assert.False(iteration.Evaluated);
            Assert.False(iteration.ContinuationCounted);
            Assert.Equal(CopilotAgentStopReason.Completed, assistantMessage.AgentStopReason);
            Assert.False(assistantMessage.WasResponseInterrupted);
        }
        finally
        {
            evaluator.Release();
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task CancellationObservedWithEvaluationResultRetainsItsUsageWithoutApplyingVerdict()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "late-cancelled-goal-evaluation-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var createdAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var activeGoal = CopilotConversationGoal.Create("Finish the verified runtime slice", createdAt);
        conversation.Goal = activeGoal;
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, activeGoal.Objective)
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        conversation.Messages.Add(userMessage);
        conversation.Messages.Add(assistantMessage);
        var turnUsage = new CopilotTokenUsage(10, 5, 15);
        var evaluationUsage = new CopilotTokenUsage(3, 2, 5);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            "run:late-cancelled-goal-evaluation");
        var evaluator = new CancellingGoalCompletionEvaluator(
            () => Assert.True(hostedRun.TryRequestCancel()),
            evaluationUsage);
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        var evaluatorField = typeof(CopilotChatViewModel).GetField(
            "_goalCompletionEvaluator",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Goal completion evaluator field was not found.");
        evaluatorField.SetValue(viewModel, evaluator);
        conversation.Goal = activeGoal;
        try
        {
            await ProcessGoalAfterTurnAsync(
                viewModel,
                hostedRun,
                conversation,
                profile,
                userMessage,
                assistantMessage,
                activeGoal.Id,
                turnUsage);

            Assert.NotNull(conversation.Goal);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Equal(1, conversation.Goal.TurnCount);
            Assert.Equal(0, conversation.Goal.EvaluationCount);
            Assert.Equal(
                turnUsage.Add(evaluationUsage).EffectiveTotalTokens,
                conversation.Goal.TokensUsed);
            var iteration = Assert.Single(conversation.Goal.IterationLog);
            Assert.False(iteration.Evaluated);
            Assert.False(iteration.ContinuationCounted);
            Assert.Equal(CopilotAgentStopReason.Completed, assistantMessage.AgentStopReason);
            Assert.False(assistantMessage.WasResponseInterrupted);
        }
        finally
        {
            hostedRun.Complete(error: null);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task CancellationBeforeGoalContinuationCommitPausesGoalAndRemovesAutomaticWork()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "cancelled-goal-continuation-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var createdAt = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var turnUsage = new CopilotTokenUsage(10, 5, 15);
        var continuedGoal = CopilotConversationGoal.Create(
                "Finish the verified runtime slice",
                createdAt)
            .WithTurnOutcome(
                CopilotConversationGoalState.Active,
                turnUsage,
                elapsedSeconds: 2,
                evaluated: true,
                continued: true,
                "More verified work remains.",
                createdAt.AddMinutes(1));
        conversation.Goal = continuedGoal;
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Turn complete")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var taskHost = new CopilotAgentTaskHost();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        conversation.Goal = continuedGoal;
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hostedRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            var completedTurnSnapshot = new CopilotAgentHostContextSnapshot("", "", []);
            var completedTurnRuntimeConfig = new CopilotTurnRuntimeConfigSnapshot(
                new CopilotAgentDefaultsConfig(),
                []);
            Assert.True(TryQueueGoalContinuation(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                profile,
                completedTurnSnapshot,
                completedTurnRuntimeConfig,
                continuedGoal.Id,
                "More verified work remains."));
            Assert.Single(taskHost.QueuedRuns);

            Assert.True(taskHost.RequestCancel(hostedRun.Id));

            var queued = TryQueueGoalContinuation(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                profile,
                completedTurnSnapshot,
                completedTurnRuntimeConfig,
                continuedGoal.Id,
                "More verified work remains.");

            Assert.False(queued);
            Assert.Empty(taskHost.QueuedRuns);
            Assert.NotNull(conversation.Goal);
            Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal.State);
            Assert.Equal(continuedGoal.TurnCount, conversation.Goal.TurnCount);
            Assert.Equal(continuedGoal.EvaluationCount, conversation.Goal.EvaluationCount);
            Assert.Equal(continuedGoal.TokensUsed, conversation.Goal.TokensUsed);
            Assert.Equal(continuedGoal.IterationLog.Count, conversation.Goal.IterationLog.Count);
            Assert.Contains(
                "continuation queueing cancelled",
                assistantMessage.ExecutionContent,
                StringComparison.Ordinal);
        }
        finally
        {
            foreach (var queuedRun in taskHost.QueuedRuns.ToArray())
                taskHost.RequestCancel(queuedRun.Id);
            releaseActive.TrySetResult();
            try
            {
                await hostedRun.Completion.WaitAsync(TestTimeout);
            }
            catch (OperationCanceledException)
            {
            }
            viewModel.Dispose();
        }
    }

    [Theory]
    [InlineData("/status")]
    [InlineData("/goal pause")]
    [InlineData("/goal clear")]
    public async Task QueuedLocalCommandPreservesGoalContinuationOrAppliesExplicitGoalControl(string localCommand)
    {
        const string newerDraft = "Preserve this draft while the queued command runs.";
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-goal-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var parentMessages = CompleteParentTurnForQueuedCommand(conversation);
        var now = DateTimeOffset.UtcNow;
        var continuedGoal = CopilotConversationGoal.Create("Finish the verified runtime slice", now)
            .WithTurnOutcome(CopilotConversationGoalState.Active, CopilotTokenUsage.Empty,
                elapsedSeconds: 2, evaluated: true, continued: true, "More verified work remains.", now);
        var newerAttachment = CopilotAttachmentItem.CreateContext("Evidence attached to the newer draft.");
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        conversation.Goal = continuedGoal;
        var activeRun = taskHost.Start(conversation.Id, CopilotAgentMode.Auto, async _ =>
        {
            activeStarted.TrySetResult();
            await releaseActive.Task;
        });
        CopilotHostedAgentRun? localRun = null;
        CopilotHostedAgentRun? automaticRun = null;
        var startedAgentRuns = new ConcurrentQueue<string>();
        EventHandler<CopilotAgentTaskHostChangedEventArgs> observeStarted = (_, args) =>
        {
            if (args.Kind == CopilotAgentTaskHostChangeKind.Started
                && args.Run.IsAgent && args.Run.Id != activeRun.Id)
                startedAgentRuns.Enqueue(args.Run.Id);
        };
        taskHost.Changed += observeStarted;

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = localCommand;
            Assert.True(viewModel.TryQueueCurrentRunFollowUp());
            var localItem = Assert.Single(viewModel.QueuedFollowUps);
            localRun = Assert.Single(taskHost.QueuedRuns);
            Assert.True(localItem.IsLocalCommand);
            Assert.False(localRun.IsAgent);
            viewModel.InputText = newerDraft;
            conversation.Attachments.Add(newerAttachment);

            Assert.True(TryQueueGoalContinuation(viewModel, activeRun, conversation,
                parentMessages[1], profile, new CopilotAgentHostContextSnapshot("", "", []),
                new CopilotTurnRuntimeConfigSnapshot(new CopilotAgentDefaultsConfig(), []),
                continuedGoal.Id, "More verified work remains."));
            var automaticItem = Assert.Single(viewModel.QueuedFollowUps, item => item.IsAutomaticGoalContinuation);
            automaticRun = Assert.Single(taskHost.QueuedRuns, run => run.Id == automaticItem.RunId);
            Assert.False(automaticItem.IsLocalCommand);
            Assert.Equal(continuedGoal.Id, automaticItem.GoalId);
            Assert.Equal(new[] { localRun.Id, automaticRun.Id }, taskHost.QueuedRuns.Select(run => run.Id));
            Assert.False(runtime.Entered.IsCompleted);

            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            await localRun.Completion.WaitAsync(TestTimeout);
            if (localCommand == "/status")
            {
                var request = await runtime.Entered.WaitAsync(TestTimeout);
                Assert.Same(automaticRun, taskHost.ActiveRun);
                Assert.Equal(automaticRun.Id, Assert.Single(startedAgentRuns));
                Assert.Single(taskHost.ScheduledRuns);
                Assert.Equal(automaticItem.Prompt, request.UserText);
                Assert.Equal(continuedGoal.Objective, request.ActiveGoalText);
                Assert.Empty(request.HostContext.Attachments);
                Assert.Equal(CopilotConversationGoalState.Active, conversation.Goal!.State);
                Assert.Contains("/status", viewModel.LocalCommandResultTitle, StringComparison.Ordinal);
            }
            else
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => automaticRun.Completion.WaitAsync(TestTimeout));
                Assert.False(runtime.Entered.IsCompleted);
                Assert.Empty(startedAgentRuns);
                Assert.Null(taskHost.ActiveRun);
                Assert.Empty(taskHost.ScheduledRuns);
                if (localCommand == "/goal pause")
                    Assert.Equal(CopilotConversationGoalState.Paused, conversation.Goal!.State);
                else
                    Assert.Null(conversation.Goal);
            }

            Assert.Empty(taskHost.QueuedRuns);
            Assert.Empty(viewModel.QueuedFollowUps);
            Assert.Equal(newerDraft, viewModel.InputText);
            Assert.Equal(newerDraft, conversation.DraftText);
            Assert.Equal(newerAttachment.Id, Assert.Single(conversation.Attachments).Id);
            Assert.Equal(localCommand == "/status" ? 4 : 2, conversation.Messages.Count);
            Assert.Same(parentMessages[1], conversation.Messages[1]);
            Assert.Equal(CopilotAgentStopReason.Completed, parentMessages[1].AgentStopReason);
            Assert.False(parentMessages[1].WasResponseInterrupted);
            if (conversation.Goal != null)
                Assert.Equal(continuedGoal.TurnCount, conversation.Goal.TurnCount);
        }
        finally
        {
            var ownedRuns = taskHost.ScheduledRuns
                .Concat(new[] { activeRun, localRun, automaticRun }.OfType<CopilotHostedAgentRun>())
                .Distinct().ToArray();
            taskHost.Shutdown();
            releaseActive.TrySetResult();
            try
            {
                await ShutdownQueuedCommandHostAsync(taskHost, runtime, ownedRuns);
            }
            finally
            {
                taskHost.Changed -= observeStarted;
                viewModel.Dispose();
            }
        }
    }

    [Fact]
    public async Task UserQuestionAnswerStateChangesOnlyWhenTheRuntimeEventArrives()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "question-answer-event-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var runtime = new AcceptingQuestionTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var releaseRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var hostedRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Plan,
            async _ => await releaseRun.Task);
        var question = new CopilotUserQuestionSnapshot
        {
            RequestId = "question:" + new string('a', 32),
            ConversationId = conversation.Id,
            TaskId = hostedRun.Id,
            Header = "Scope",
            Question = "Which scope should be used?",
            Options =
            [
                new CopilotUserQuestionOption
                {
                    RequestId = "question:" + new string('a', 32),
                    TaskId = hostedRun.Id,
                    Label = "Current (Recommended)",
                    Description = "Keep the current bounded scope.",
                },
                new CopilotUserQuestionOption
                {
                    RequestId = "question:" + new string('a', 32),
                    TaskId = hostedRun.Id,
                    Label = "Expand",
                    Description = "Include adjacent modules.",
                },
            ],
            RequestedAtUtc = DateTimeOffset.UtcNow,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            UserQuestion = question,
        };
        conversation.Messages.Add(assistantMessage);
        try
        {
            viewModel.InputText = "Keep the current scope";

            Assert.True(viewModel.SubmitUserQuestionAnswerCommand.CanExecute(null));
            viewModel.SubmitUserQuestionAnswerCommand.Execute(null);

            Assert.Equal(string.Empty, viewModel.InputText);
            Assert.Equal("Keep the current scope", runtime.Answer);
            var persistedQuestion = Assert.IsType<CopilotUserQuestionSnapshot>(assistantMessage.UserQuestion);
            Assert.NotSame(question, persistedQuestion);
            Assert.Equal(question.RequestId, persistedQuestion.RequestId);
            Assert.True(persistedQuestion.IsPending);

            ApplyAgentEvents(
                viewModel,
                hostedRun,
                conversation,
                assistantMessage,
                CopilotAgentEvent.UserQuestionResolved(
                    question.Resolve(
                        CopilotUserQuestionResolution.Answered,
                        "Keep the current scope")));

            Assert.Equal(
                CopilotUserQuestionResolution.Answered,
                assistantMessage.UserQuestion.Resolution);
            Assert.Equal("Keep the current scope", assistantMessage.UserQuestion.Answer);
        }
        finally
        {
            releaseRun.TrySetResult();
            await hostedRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task EditingGoalDuringActiveTurnQueuesOnlyTheLatestGoalAsNextWork()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "running-goal-edit-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var originalGoal = CopilotConversationGoal.Create(
            "Finish the original runtime objective",
            new DateTimeOffset(2026, 8, 11, 13, 0, 0, TimeSpan.Zero));
        conversation.Goal = originalGoal;
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);

            Assert.True(viewModel.EditConversationGoalCommand.CanExecute(null));

            viewModel.InputText = "/goal edit Finish the revised runtime objective";
            viewModel.SendCommand.Execute(null);

            var firstEditedGoal = Assert.IsType<CopilotConversationGoal>(conversation.Goal);
            Assert.NotEqual(originalGoal.Id, firstEditedGoal.Id);
            Assert.Equal("Finish the revised runtime objective", firstEditedGoal.Objective);
            var firstQueued = Assert.Single(viewModel.QueuedFollowUps);
            Assert.Equal(firstEditedGoal.Id, firstQueued.GoalId);
            Assert.Equal(firstEditedGoal.Objective, firstQueued.Prompt);
            Assert.True(firstQueued.IsGoalBound);
            Assert.False(firstQueued.IsAutomaticGoalContinuation);

            viewModel.InputText = "/goal edit Finish the latest runtime objective";
            viewModel.SendCommand.Execute(null);

            var latestGoal = Assert.IsType<CopilotConversationGoal>(conversation.Goal);
            Assert.NotEqual(firstEditedGoal.Id, latestGoal.Id);
            Assert.Equal("Finish the latest runtime objective", latestGoal.Objective);
            var latestQueued = Assert.Single(viewModel.QueuedFollowUps);
            Assert.NotEqual(firstQueued.RunId, latestQueued.RunId);
            Assert.Equal(latestGoal.Id, latestQueued.GoalId);
            Assert.Equal(latestGoal.Objective, latestQueued.Prompt);
            Assert.True(latestQueued.IsGoalBound);
            Assert.False(latestQueued.IsAutomaticGoalContinuation);
            Assert.Single(taskHost.QueuedRuns);
        }
        finally
        {
            foreach (var queuedRun in taskHost.QueuedRuns.ToArray())
                taskHost.RequestCancel(queuedRun.Id);
            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task EditingGoalDuringActiveTurnPausesItWhenTheFollowUpQueueIsFull()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "running-goal-edit-full-queue-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        conversation.Goal = CopilotConversationGoal.Create(
            "Finish the original runtime objective",
            new DateTimeOffset(2026, 8, 11, 13, 30, 0, TimeSpan.Zero));
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            for (var index = 0; index < taskHost.MaxQueuedRuns; index++)
            {
                Assert.True(taskHost.TryScheduleFollowUp(
                    conversation.Id,
                    CopilotAgentMode.Auto,
                    static _ => Task.CompletedTask,
                    out _,
                    out _));
            }

            viewModel.InputText = "/goal edit Finish the revised runtime objective";
            viewModel.SendCommand.Execute(null);

            var editedGoal = Assert.IsType<CopilotConversationGoal>(conversation.Goal);
            Assert.Equal("Finish the revised runtime objective", editedGoal.Objective);
            Assert.Equal(CopilotConversationGoalState.Paused, editedGoal.State);
            Assert.Empty(viewModel.QueuedFollowUps);
            Assert.Contains("队列已满", viewModel.LocalCommandResultText, StringComparison.Ordinal);
            Assert.Contains("目标已暂停", viewModel.LocalCommandResultText, StringComparison.Ordinal);
        }
        finally
        {
            foreach (var queuedRun in taskHost.QueuedRuns.ToArray())
                taskHost.RequestCancel(queuedRun.Id);
            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task SupersededExplicitGoalStartExitsWithoutRunningAgainstTheNewGoal()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "superseded-goal-start-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        conversation.Goal = CopilotConversationGoal.Create(
            "Finish the latest runtime objective",
            new DateTimeOffset(2026, 8, 11, 14, 0, 0, TimeSpan.Zero));
        var runtime = new GatedFailingTurnRuntime();
        var queuedFollowUp = new CopilotQueuedFollowUp(
            "superseded-goal-run",
            conversation.Id,
            conversation.Title,
            "Finish the superseded runtime objective",
            CopilotAgentMode.Auto,
            profile,
            new CopilotAgentHostContextSnapshot("", "", []),
            goalId: "superseded-goal-id",
            automaticGoalContinuation: false);

        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(
            conversation,
            config,
            runtime,
            new CopilotAgentTaskHost());
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto);
        var executeMethod = typeof(CopilotChatViewModel).GetMethod(
            "ExecuteQueuedFollowUpAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ExecuteQueuedFollowUpAsync was not found.");

        var execution = Assert.IsAssignableFrom<Task>(executeMethod.Invoke(
            viewModel,
            [hostedRun, queuedFollowUp]));
        await execution.WaitAsync(TestTimeout);

        Assert.False(runtime.Entered.IsCompleted);
        Assert.Equal("Finish the latest runtime objective", conversation.Goal.Objective);
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void PromptHistorySearchUsesAnOverlayAndRestoresTheCompleteComposerDraft()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = new CopilotConfig
        {
            SchemaVersion = CopilotConfig.CurrentSchemaVersion,
            McpBearerToken = "composer-search-overlay-test-token",
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };
        var skillReference = new CopilotAgentSkillReference
        {
            Name = "sample-skill",
            SkillFilePath = Path.GetFullPath(
                Path.Combine("skills", "sample-skill", "SKILL.md")),
        };
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        conversation.Id = "conversation-a";
        conversation.DraftText = "$sample-skill current draft";
        conversation.DraftRequestMode = CopilotAgentMode.Review;
        conversation.DraftWorkspaceReviewTarget = CopilotWorkspaceReviewTargetContext.WorkingTree();
        conversation.DraftAgentSkillReference = skillReference;
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "older prompt"));
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            new GatedFailingTurnRuntime(),
            new CopilotAgentTaskHost());
        try
        {
            Assert.True(viewModel.TryOpenPromptHistorySearch());

            viewModel.InputText = "older";

            Assert.Equal("older", viewModel.InputText);
            Assert.Equal("$sample-skill current draft", conversation.DraftText);
            Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
            Assert.Equal(
                CopilotWorkspaceReviewTarget.WorkingTree,
                conversation.DraftWorkspaceReviewTarget?.Target);
            Assert.Equal("sample-skill", conversation.DraftAgentSkillReference?.Name);

            viewModel.DismissPromptHistorySearch();

            Assert.Equal("$sample-skill current draft", viewModel.InputText);
            Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
            Assert.NotNull(conversation.DraftWorkspaceReviewTarget);
            Assert.Equal("sample-skill", conversation.DraftAgentSkillReference?.Name);
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("exactcap")]
    [InlineData("oversized_merged")]
    public async Task ComposerStashPreservesCompleteDraftAcrossSaveLoadWithoutReplacingNewerInput(string draftKind)
    {
        const string tailMarker = "FINAL_RECOVERY_END_91D44";
        const string newerDraft = "Keep this newer input while the original draft remains stashed.";
        var maximumCharacters = CopilotConversationHistoryWindow.MaximumContentCharacterLimit;
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "composer-stash-roundtrip-test-token");
        var originalDraft = draftKind == "exactcap"
            ? new string('x', maximumCharacters - tailMarker.Length) + tailMarker
            : "Draft to keep complete." + Environment.NewLine + tailMarker;
        var conversation = CreateConversation(profile, "conversation-a",
            draftKind == "oversized_merged" ? string.Empty : originalDraft);
        conversation.SetCustomTitle("Composer stash roundtrip");
        conversation.DraftRequestMode = CopilotAgentMode.Review;
        conversation.DraftWorkspaceReviewTarget = CopilotWorkspaceReviewTargetContext.WorkingTree();
        var firstAttachment = CopilotAttachmentItem.CreateContext("Evidence belonging to the first recovered request.");
        var laterAttachment = CopilotAttachmentItem.CreateContext("Evidence belonging to the later recovered request.");
        var newerAttachment = CopilotAttachmentItem.CreateContext("Evidence belonging to the newer draft.");
        var originalAttachments = new[] { firstAttachment, laterAttachment };
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id, ActiveProfileId = profile.Id, Conversations = [conversation],
        };
        var recoveryPrompts = new[]
        {
            new string('a', maximumCharacters / 2) + "FIRST_RECOVERY_END_7AC21",
            new string('b', maximumCharacters / 2) + tailMarker,
        };
        if (draftKind == "oversized_merged")
        {
            for (var index = 0; index < recoveryPrompts.Length; index++)
            {
                Assert.True(recoveryPrompts[index].Length <= maximumCharacters);
                state.QueuedFollowUpRecoveries.Add(new CopilotQueuedFollowUpRecoveryRecord
                {
                    RunId = $"recovered-request-{index}", ConversationId = conversation.Id, ProfileId = profile.Id,
                    Prompt = recoveryPrompts[index], ResumeAfterRestart = false,
                    ComposerState = CopilotComposerStash.Capture(recoveryPrompts[index], recoveryPrompts[index].Length,
                        CopilotAgentMode.Review, [originalAttachments[index]],
                        CopilotWorkspaceReviewTargetContext.WorkingTree()),
                });
            }
        }
        else
        {
            foreach (var attachment in originalAttachments)
                conversation.Attachments.Add(attachment);
        }
        var root = Directory.CreateTempSubdirectory("CopilotComposerStash-").FullName;
        var diskStore = new CopilotChatStateStore(root);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var restartedRuntime = new GatedFailingTurnRuntime();
        var restartedHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        CopilotChatViewModel? viewModel = null;
        CopilotChatViewModel? restartedViewModel = null;
        try
        {
            diskStore.Save(state);
            var loadedState = diskStore.Load();
            viewModel = new CopilotChatViewModel(new CopilotChatService(),
                new InMemoryStateStore(loadedState, diskStore.AttachmentDirectoryPath), config, runtime, taskHost);
            var loadedConversation = Assert.Single(viewModel.Conversations);
            var completeDraft = viewModel.InputText;
            if (draftKind == "oversized_merged")
            {
                Assert.True(completeDraft.Length > viewModel.ComposerMaximumCharacters);
                Assert.Contains(recoveryPrompts[0], completeDraft, StringComparison.Ordinal);
                Assert.Contains(recoveryPrompts[1], completeDraft, StringComparison.Ordinal);
                Assert.True(completeDraft.IndexOf(recoveryPrompts[0], StringComparison.Ordinal)
                    < completeDraft.IndexOf(recoveryPrompts[1], StringComparison.Ordinal));
            }
            else
            {
                Assert.Equal(originalDraft, completeDraft);
            }
            Assert.EndsWith(tailMarker, completeDraft, StringComparison.Ordinal);
            Assert.Empty(loadedState.QueuedFollowUpRecoveries);
            Assert.Null(taskHost.ActiveRun);
            Assert.False(runtime.Entered.IsCompleted);
            var caretIndex = completeDraft.Length - 2;

            Assert.True(viewModel.TryToggleComposerStash(caretIndex, out var capturedCaretIndex));

            Assert.Equal(-1, capturedCaretIndex);
            Assert.Equal(string.Empty, viewModel.InputText);
            Assert.Empty(loadedConversation.Attachments);
            var stash = Assert.IsType<CopilotComposerStash>(loadedConversation.ComposerStash);
            Assert.Equal(completeDraft, stash.Text);
            Assert.Equal(caretIndex, stash.CaretIndex);
            Assert.Equal(CopilotAgentMode.Review, stash.RequestMode);
            Assert.Equal(CopilotWorkspaceReviewTarget.WorkingTree, stash.WorkspaceReviewTarget?.Target);
            Assert.Equal(originalAttachments.Select(item => (item.Id, item.Value)),
                stash.Attachments.Select(item => (item.Id, item.Value)));

            viewModel.InputText = newerDraft;
            loadedConversation.Attachments.Add(newerAttachment);
            Assert.True(viewModel.TryToggleComposerStash(newerDraft.Length, out var unchangedCaretIndex));
            Assert.Equal(-1, unchangedCaretIndex);
            Assert.Equal(newerDraft, viewModel.InputText);
            Assert.Equal(newerAttachment.Id, Assert.Single(loadedConversation.Attachments).Id);
            Assert.Equal(completeDraft, loadedConversation.ComposerStash!.Text);
            diskStore.Save(loadedState);
            var reloadedState = diskStore.Load();
            viewModel.Dispose();
            viewModel = null;

            restartedViewModel = new CopilotChatViewModel(new CopilotChatService(),
                new InMemoryStateStore(reloadedState, diskStore.AttachmentDirectoryPath), config, restartedRuntime, restartedHost);
            var restoredConversation = Assert.Single(restartedViewModel.Conversations);
            Assert.Equal(newerDraft, restartedViewModel.InputText);
            Assert.Equal(newerAttachment.Id, Assert.Single(restoredConversation.Attachments).Id);
            Assert.Equal(completeDraft, restoredConversation.ComposerStash!.Text);
            Assert.True(restartedViewModel.TryToggleComposerStash(newerDraft.Length, out _));
            Assert.Equal(newerDraft, restartedViewModel.InputText);
            Assert.Equal(completeDraft, restoredConversation.ComposerStash!.Text);
            restartedViewModel.InputText = string.Empty;
            restoredConversation.Attachments.Clear();

            Assert.True(restartedViewModel.TryToggleComposerStash(0, out var restoredCaretIndex));

            Assert.Equal(caretIndex, restoredCaretIndex);
            Assert.Equal(completeDraft, restartedViewModel.InputText);
            Assert.Equal(completeDraft, restoredConversation.DraftText);
            Assert.EndsWith(tailMarker, restartedViewModel.InputText, StringComparison.Ordinal);
            Assert.Null(restoredConversation.ComposerStash);
            Assert.Equal(CopilotAgentMode.Review, restoredConversation.DraftRequestMode);
            Assert.Equal(CopilotWorkspaceReviewTarget.WorkingTree, restoredConversation.DraftWorkspaceReviewTarget?.Target);
            Assert.Equal(originalAttachments.Select(item => (item.Id, item.Value)),
                restoredConversation.Attachments.Select(item => (item.Id, item.Value)));
            if (draftKind == "oversized_merged")
            {
                restartedViewModel.SendCommand.Execute(null);
                Assert.Equal("输入过长", restartedViewModel.LocalCommandResultTitle);
                Assert.Equal(completeDraft, restartedViewModel.InputText);
                Assert.Equal(originalAttachments.Select(item => (item.Id, item.Value)),
                    restoredConversation.Attachments.Select(item => (item.Id, item.Value)));
            }
            Assert.Null(restartedHost.ActiveRun);
            Assert.Empty(restartedHost.ScheduledRuns);
            Assert.False(restartedRuntime.Entered.IsCompleted);
            Assert.Empty(restoredConversation.Messages);
        }
        finally
        {
            try
            {
                await ShutdownQueuedCommandHostAsync(taskHost, runtime);
                await ShutdownQueuedCommandHostAsync(restartedHost, restartedRuntime);
            }
            finally
            {
                viewModel?.Dispose();
                restartedViewModel?.Dispose();
                var resolvedRoot = Path.GetFullPath(root);
                Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetDirectoryName(resolvedRoot), ignoreCase: true);
                Assert.StartsWith("CopilotComposerStash-", Path.GetFileName(resolvedRoot), StringComparison.Ordinal);
                if (Directory.Exists(resolvedRoot))
                    Directory.Delete(resolvedRoot, recursive: true);
            }
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
    public async Task ActiveComposerSuggestsSlashCommandsThatCanRunFromTheNextTurn()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-command-completion-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/pl";

            Assert.Contains(
                viewModel.LocalCommandSuggestions,
                command => command.Name == "/plan");
            Assert.Contains("排到下一轮", viewModel.LocalCommandSuggestionHeader, StringComparison.Ordinal);
        }
        finally
        {
            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task QueueActionDefersSlashCommandAndPreservesNewerComposerDraft()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-status-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var queuedAttachment = CopilotAttachmentItem.CreateContext("queued command attachment");
        var newerAttachment = CopilotAttachmentItem.CreateContext("newer draft attachment");
        conversation.Attachments.Add(queuedAttachment);
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, new GatedFailingTurnRuntime(), taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
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

            Assert.True(viewModel.QueueFollowUpCommand.CanExecute(null));
            viewModel.QueueFollowUpCommand.Execute(null);

            var queuedItem = Assert.Single(viewModel.QueuedFollowUps);
            var queuedRun = Assert.Single(taskHost.QueuedRuns);
            Assert.True(queuedItem.IsLocalCommand);
            Assert.False(queuedRun.IsAgent);
            Assert.Equal(string.Empty, viewModel.LocalCommandResultTitle);
            Assert.DoesNotContain(queuedAttachment, conversation.Attachments);
            Assert.Empty(conversation.Messages);

            viewModel.InputText = "newer draft";
            conversation.Attachments.Add(newerAttachment);
            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            await queuedRun.Completion.WaitAsync(TestTimeout);

            Assert.Contains("/status", viewModel.LocalCommandResultTitle, StringComparison.Ordinal);
            Assert.Equal("newer draft", viewModel.InputText);
            Assert.Equal("newer draft", conversation.DraftText);
            Assert.Contains(conversation.Attachments, attachment => attachment.Id == queuedAttachment.Id);
            Assert.Contains(newerAttachment, conversation.Attachments);
            Assert.Empty(conversation.Messages);
            Assert.Empty(viewModel.QueuedFollowUps);
        }
        finally
        {
            releaseActive.TrySetResult();
            viewModel.Dispose();
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CancellingQueuedCommandBeforeDispatchPreservesGoalAndRestoresComposer(bool hasNewerDraft, bool restart)
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "cancelled-queued-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var queuedAttachment = CopilotAttachmentItem.CreateContext("queued command attachment");
        var newerAttachment = CopilotAttachmentItem.CreateContext("newer draft attachment");
        conversation.Attachments.Add(queuedAttachment);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id, ActiveProfileId = profile.Id, Conversations = [conversation],
        };
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(new CopilotChatService(), new InMemoryStateStore(state), config, runtime, taskHost);
        var originalGoal = CopilotConversationGoal.Create("Keep the active goal", DateTimeOffset.UtcNow);
        conversation.Goal = originalGoal;
        var originalMessages = restart ? CompleteParentTurnForQueuedCommand(conversation) : Array.Empty<CopilotChatMessage>();
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        EventHandler<CopilotAgentTaskHostChangedEventArgs>? cancelOnStart = null;
        CopilotHostedAgentRun? laterRun = null;
        var laterAttachment = CopilotAttachmentItem.CreateContext("later command follow-up attachment");
        const string laterPrompt = "Continue only after the queued command completes.";
        var queuedCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/goal clear";
            Assert.True(viewModel.TryQueueCurrentRunFollowUp());
            var queuedRun = Assert.Single(taskHost.QueuedRuns);
            Assert.True(Assert.Single(viewModel.QueuedFollowUps).IsLocalCommand);
            Assert.DoesNotContain(queuedAttachment, conversation.Attachments);
            var cancellationAccepted = false;
            cancelOnStart = (_, args) =>
            {
                if (args.Kind == CopilotAgentTaskHostChangeKind.Started && args.Run.Id == queuedRun.Id)
                    cancellationAccepted = taskHost.RequestCancel(queuedRun.Id);
                if (args.Kind == CopilotAgentTaskHostChangeKind.Completed && args.Run.Id == queuedRun.Id)
                    queuedCompleted.TrySetResult();
            };
            taskHost.Changed += cancelOnStart;
            if (restart)
            {
                viewModel.InputText = laterPrompt;
                conversation.Attachments.Add(laterAttachment);
                Assert.True(viewModel.TryQueueCurrentRunFollowUp());
                laterRun = Assert.Single(taskHost.QueuedRuns, run => run.Id != queuedRun.Id);
            }
            if (hasNewerDraft)
            {
                viewModel.InputText = "newer draft";
                conversation.Attachments.Add(newerAttachment);
            }

            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await queuedRun.Completion.WaitAsync(TestTimeout));
            await queuedCompleted.Task.WaitAsync(TestTimeout);

            var expectedDraft = hasNewerDraft
                ? "newer draft" + Environment.NewLine + Environment.NewLine + "/goal clear"
                : "/goal clear";
            Assert.True(cancellationAccepted);
            Assert.Same(originalGoal, conversation.Goal);
            Assert.Equal(expectedDraft, conversation.DraftText);
            Assert.Equal(expectedDraft, viewModel.InputText);
            Assert.Contains(conversation.Attachments, attachment => attachment.Id == queuedAttachment.Id);
            if (hasNewerDraft)
                Assert.Contains(newerAttachment, conversation.Attachments);
            Assert.Equal(string.Empty, viewModel.LocalCommandResultTitle);
            Assert.False(runtime.Entered.IsCompleted);
            Assert.Equal(originalMessages, conversation.Messages.ToArray());
            if (restart)
            {
                Assert.Null(taskHost.ActiveRun);
                Assert.Same(laterRun, Assert.Single(taskHost.QueuedRuns));
                Assert.False(laterRun!.HasStarted);
                Assert.False(laterRun.Completion.IsCompleted);
                Assert.Equal(laterRun.Id, Assert.Single(viewModel.QueuedFollowUps).RunId);
                Assert.Equal(laterRun.Id, Assert.Single(state.QueuedFollowUpRecoveries).RunId);
                await AssertQueuedCommandRestartRestoresLaterDraftAsync(state, config, viewModel, taskHost, laterRun,
                    originalMessages, expectedDraft + Environment.NewLine + Environment.NewLine + laterPrompt,
                    [newerAttachment, queuedAttachment, laterAttachment]);
            }
            else
            {
                Assert.Empty(viewModel.QueuedFollowUps);
            }
        }
        finally
        {
            taskHost.Changed -= cancelOnStart;
            releaseActive.TrySetResult();
            if (restart)
            {
                try
                {
                    await ShutdownQueuedCommandHostAsync(taskHost, runtime, activeRun);
                }
                finally
                {
                    viewModel.Dispose();
                }
            }
            else
            {
                viewModel.Dispose();
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedCommandPersistenceFailurePreservesGoalAndRestoresComposer(bool restart)
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "failed-queued-command-save-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var queuedAttachment = CopilotAttachmentItem.CreateContext("queued command attachment");
        var newerAttachment = CopilotAttachmentItem.CreateContext("newer draft attachment");
        var laterAttachment = CopilotAttachmentItem.CreateContext("later command follow-up attachment");
        const string laterPrompt = "Continue only after the queued command completes.";
        conversation.Attachments.Add(queuedAttachment);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
        var failSaves = 0;
        var saveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stateStore = new InMemoryStateStore(state, saveSerializedAsync: async cancellationToken =>
        {
            if (Volatile.Read(ref failSaves) == 0)
                return;
            saveEntered.TrySetResult();
            await releaseSave.Task.WaitAsync(cancellationToken);
            throw new IOException("Expected queued command persistence failure.");
        });
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(new CopilotChatService(), stateStore, config, runtime, taskHost);
        var originalGoal = CopilotConversationGoal.Create("Keep the active goal", DateTimeOffset.UtcNow);
        conversation.Goal = originalGoal;
        var originalMessages = restart ? CompleteParentTurnForQueuedCommand(conversation) : Array.Empty<CopilotChatMessage>();
        var activeRun = taskHost.Start(conversation.Id, CopilotAgentMode.Auto, _ => releaseActive.Task);
        EventHandler<CopilotAgentTaskHostChangedEventArgs>? failSaveOnStart = null;
        CopilotHostedAgentRun? laterRun = null;
        var queuedCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            viewModel.InputText = "/goal clear";
            Assert.True(viewModel.TryQueueCurrentRunFollowUp());
            var queuedRun = Assert.Single(taskHost.QueuedRuns);
            failSaveOnStart = (_, args) =>
            {
                if (args.Kind == CopilotAgentTaskHostChangeKind.Started && args.Run.Id == queuedRun.Id)
                    Volatile.Write(ref failSaves, 1);
                if (args.Kind == CopilotAgentTaskHostChangeKind.Completed && args.Run.Id == queuedRun.Id)
                    queuedCompleted.TrySetResult();
            };
            taskHost.Changed += failSaveOnStart;
            if (restart)
            {
                viewModel.InputText = laterPrompt;
                conversation.Attachments.Add(laterAttachment);
                Assert.True(viewModel.TryQueueCurrentRunFollowUp());
                laterRun = Assert.Single(taskHost.QueuedRuns, run => run.Id != queuedRun.Id);
            }
            viewModel.InputText = "newer draft";
            if (restart)
                conversation.Attachments.Add(newerAttachment);

            releaseActive.TrySetResult();
            await saveEntered.Task.WaitAsync(TestTimeout);
            Assert.Same(originalGoal, conversation.Goal);
            releaseSave.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            await Assert.ThrowsAsync<IOException>(async () =>
                await queuedRun.Completion.WaitAsync(TestTimeout));
            await queuedCompleted.Task.WaitAsync(TestTimeout);

            var expectedDraft = "newer draft" + Environment.NewLine + Environment.NewLine + "/goal clear";
            Assert.Same(originalGoal, conversation.Goal);
            Assert.Equal(expectedDraft, conversation.DraftText);
            Assert.Equal(expectedDraft, viewModel.InputText);
            Assert.Contains(conversation.Attachments, attachment => attachment.Id == queuedAttachment.Id);
            Assert.False(runtime.Entered.IsCompleted);
            Assert.Equal(string.Empty, viewModel.LocalCommandResultTitle);
            Assert.Equal(originalMessages, conversation.Messages.ToArray());
            if (restart)
            {
                Assert.Null(taskHost.ActiveRun);
                Assert.Same(laterRun, Assert.Single(taskHost.QueuedRuns));
                Assert.False(laterRun!.HasStarted);
                Assert.False(laterRun.Completion.IsCompleted);
                Assert.Equal(laterRun.Id, Assert.Single(viewModel.QueuedFollowUps).RunId);
                Assert.Equal(laterRun.Id, Assert.Single(state.QueuedFollowUpRecoveries).RunId);
                Volatile.Write(ref failSaves, 0);
                await AssertQueuedCommandRestartRestoresLaterDraftAsync(state, config, viewModel, taskHost, laterRun,
                    originalMessages, expectedDraft + Environment.NewLine + Environment.NewLine + laterPrompt,
                    [newerAttachment, queuedAttachment, laterAttachment]);
            }
            else
            {
                Assert.Empty(viewModel.QueuedFollowUps);
                Assert.Empty(state.QueuedFollowUpRecoveries);
            }
        }
        finally
        {
            taskHost.Changed -= failSaveOnStart;
            Volatile.Write(ref failSaves, 0);
            releaseSave.TrySetResult();
            releaseActive.TrySetResult();
            if (restart)
            {
                try
                {
                    await ShutdownQueuedCommandHostAsync(taskHost, runtime, activeRun);
                }
                finally
                {
                    viewModel.Dispose();
                }
            }
            else
            {
                viewModel.Dispose();
            }
        }
    }

    private static CopilotChatMessage[] CompleteParentTurnForQueuedCommand(CopilotConversationRecord conversation,
        CopilotWorkspaceReviewTargetContext? reviewTarget = null, CopilotAgentMode? requestMode = null)
    {
        conversation.SetCustomTitle("Queued command restart fixture");
        var mode = requestMode ?? (reviewTarget == null ? CopilotAgentMode.Auto : CopilotAgentMode.Review);
        var prompt = reviewTarget == null ? "Complete the parent request."
            : new CopilotWorkspaceReviewRequest(reviewTarget.Target, reviewTarget.Revision, string.Empty).BuildPrompt();
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, prompt)
        {
            RequestMode = mode, WorkspaceReviewTarget = reviewTarget,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "The parent request is complete.")
        {
            RequestMode = mode, AgentStopReason = CopilotAgentStopReason.Completed,
        };
        assistantMessage.MarkThinkingStarted();
        conversation.Messages.Add(userMessage);
        conversation.Messages.Add(assistantMessage);
        CopilotHostedTurnCompletion.CompleteTerminalTurn(conversation, assistantMessage, CopilotTokenUsage.Empty);
        return conversation.Messages.ToArray();
    }

    private static async Task AssertQueuedCommandRestartRestoresLaterDraftAsync(
        CopilotChatState state, CopilotConfig config, CopilotChatViewModel originalViewModel,
        CopilotAgentTaskHost originalHost, CopilotHostedAgentRun laterRun, CopilotChatMessage[] originalMessages,
        string expectedDraft, CopilotAttachmentItem[] expectedAttachments)
    {
        var root = Directory.CreateTempSubdirectory("CopilotQueuedCommandRestart-").FullName;
        var restartedHost = new CopilotAgentTaskHost();
        var restartedRuntime = new GatedFailingTurnRuntime();
        CopilotChatViewModel? restartedViewModel = null;
        try
        {
            // The owning InMemoryStateStore controls the pre-handler save boundary. This separate
            // real disk roundtrip preserves that settled state before the old test Host is stopped.
            var diskStore = new CopilotChatStateStore(root);
            diskStore.Save(state);
            var reloadedState = diskStore.Load();
            var savedLater = Assert.Single(reloadedState.QueuedFollowUpRecoveries);
            Assert.Equal(laterRun.Id, savedLater.RunId);
            Assert.Equal(expectedAttachments[^1].Id, Assert.Single(savedLater.ComposerState!.Attachments).Id);
            originalHost.Shutdown();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => laterRun.Completion.WaitAsync(TestTimeout));
            originalViewModel.Dispose();

            restartedViewModel = new CopilotChatViewModel(new CopilotChatService(),
                new InMemoryStateStore(reloadedState, diskStore.AttachmentDirectoryPath), config, restartedRuntime, restartedHost);
            var restored = Assert.Single(restartedViewModel.Conversations);
            Assert.Null(restartedHost.ActiveRun);
            Assert.Empty(restartedHost.ScheduledRuns);
            Assert.False(restartedRuntime.Entered.IsCompleted);
            Assert.Empty(restartedViewModel.QueuedFollowUps);
            Assert.Empty(reloadedState.QueuedFollowUpRecoveries);
            Assert.Equal(CopilotConversationGoalState.Paused, restored.Goal!.State);
            Assert.Equal(state.Conversations[0].Goal!.Id, restored.Goal.Id);
            Assert.Equal(state.Conversations[0].Goal!.Objective, restored.Goal.Objective);
            Assert.Equal(expectedDraft, restored.DraftText);
            Assert.Equal(expectedDraft, restartedViewModel.InputText);
            Assert.Equal(expectedAttachments.Select(item => (item.Id, item.Value)),
                restored.Attachments.Select(item => (item.Id, item.Value)));
            Assert.Equal(originalMessages.Select(message => (message.Id, message.Content)),
                restored.Messages.Select(message => (message.Id, message.Content)));
            Assert.Equal(CopilotAgentStopReason.Completed, restored.Messages[1].AgentStopReason);
            Assert.False(restored.Messages[1].WasResponseInterrupted);
            Assert.False(restored.Messages[1].IsThinkingInProgress);
            Assert.Equal(string.Empty, restartedViewModel.LocalCommandResultTitle);
        }
        finally
        {
            try
            {
                await ShutdownQueuedCommandHostAsync(restartedHost, restartedRuntime);
            }
            finally
            {
                restartedViewModel?.Dispose();
                var resolvedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
                var resolvedTemp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
                Assert.True(string.Equals(Path.GetDirectoryName(resolvedRoot), resolvedTemp, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(resolvedRoot).StartsWith("CopilotQueuedCommandRestart-", StringComparison.Ordinal));
                Directory.Delete(resolvedRoot, recursive: true);
            }
        }
    }

    private static async Task ShutdownQueuedCommandHostAsync(CopilotAgentTaskHost taskHost,
        GatedFailingTurnRuntime runtime, params CopilotHostedAgentRun[] additionalRuns)
    {
        var runs = taskHost.ScheduledRuns.Concat(additionalRuns).Distinct().ToArray();
        runtime.Release();
        taskHost.Shutdown();
        foreach (var run in runs)
        {
            var failure = await Record.ExceptionAsync(() => run.Completion.WaitAsync(TestTimeout));
            Assert.IsNotType<TimeoutException>(failure);
        }
    }

    [Theory]
    [InlineData("cancel-image", false, false)]
    [InlineData("cancel-image", true, false)]
    [InlineData("cancel-context", false, false)]
    [InlineData("cancel-context", true, false)]
    [InlineData("missing-image", false, false)]
    [InlineData("missing-image", true, false)]
    [InlineData("missing-image", false, true)]
    [InlineData("missing-image", true, true)]
    [InlineData("cancel-image", true, true)]
    [InlineData("cancel-context", true, true)]
    public async Task UnpreparedQueuedFollowUpFailureRestoresPromptAttachmentsAndNewerDraft(
        string failureKind,
        bool hasNewerDraft,
        bool restart)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            nameof(UnpreparedQueuedFollowUpFailureRestoresPromptAttachmentsAndNewerDraft),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var sourcePath = Path.Combine(root, "source.png");
        await File.WriteAllBytesAsync(
            sourcePath,
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        profile.SupportsImageInput = true;
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var queuedAttachment = failureKind == "cancel-context"
            ? CopilotAttachmentItem.CreateContext("queued context")
            : CopilotAttachmentItem.CreateImage(sourcePath, "queued image");
        var newerAttachment = CopilotAttachmentItem.CreateContext("newer attachment");
        conversation.Attachments.Add(queuedAttachment);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = [conversation],
        };
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queuedCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var config = CreateConfig(profile, "unprepared-queued-follow-up-test-token");
        var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state, Path.Combine(root, "attachments")),
            config,
            runtime,
            taskHost);
        CopilotChatMessage? parentAssistant = null;
        if (restart)
        {
            conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Complete the parent task.")
            {
                RequestMode = CopilotAgentMode.Auto,
            });
            parentAssistant = new CopilotChatMessage(CopilotChatRole.Assistant, "The parent task is complete.")
            {
                RequestMode = CopilotAgentMode.Auto,
            };
            parentAssistant.MarkThinkingStarted();
            conversation.Messages.Add(parentAssistant);
        }
        var originalMessages = conversation.Messages.ToArray();
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                await releaseActive.Task;
                if (parentAssistant != null)
                {
                    parentAssistant.AgentStopReason = CopilotAgentStopReason.Completed;
                    CopilotHostedTurnCompletion.CompleteTerminalTurn(conversation, parentAssistant, CopilotTokenUsage.Empty);
                }
            });
        EventHandler<CopilotAgentTaskHostChangedEventArgs>? observeQueuedRun = null;
        CopilotHostedAgentRun? queuedRun = null;
        CopilotHostedAgentRun? laterRun = null;
        CopilotChatViewModel? restartedViewModel = null;
        CopilotAgentTaskHost? restartedHost = null;
        GatedFailingTurnRuntime? restartedRuntime = null;
        CopilotHostedAgentRun? restartedRun = null;
        var laterAttachment = CopilotAttachmentItem.CreateContext("Context captured with the later follow-up.");

        try
        {
            viewModel.InputText = "inspect the queued attachment";
            Assert.True(viewModel.TryQueueCurrentRunFollowUp());
            queuedRun = Assert.Single(taskHost.QueuedRuns);
            Assert.False(Assert.Single(viewModel.QueuedFollowUps).IsLocalCommand);
            Assert.Equal(queuedRun.Id, Assert.Single(state.QueuedFollowUpRecoveries).RunId);
            Assert.Empty(conversation.Attachments);
            Assert.Equal(string.Empty, viewModel.InputText);
            viewModel.InputText = "Continue only after inspecting the attachment.";
            if (restart)
                conversation.Attachments.Add(laterAttachment);
            Assert.True(viewModel.TryQueueCurrentRunFollowUp());
            laterRun = Assert.Single(taskHost.QueuedRuns, run => run.Id != queuedRun.Id);
            var shouldCancel = failureKind != "missing-image";
            var cancellationAccepted = false;
            observeQueuedRun = (_, args) =>
            {
                if (args.Run.Id != queuedRun.Id)
                    return;
                if (args.Kind == CopilotAgentTaskHostChangeKind.Started && shouldCancel)
                    cancellationAccepted = taskHost.RequestCancel(queuedRun.Id);
                if (args.Kind == CopilotAgentTaskHostChangeKind.Completed)
                    queuedCompleted.TrySetResult();
            };
            taskHost.Changed += observeQueuedRun;
            if (hasNewerDraft)
            {
                viewModel.InputText = "newer draft";
                conversation.Attachments.Add(newerAttachment);
            }
            if (!shouldCancel)
                File.Delete(sourcePath);

            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            if (shouldCancel)
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                    await queuedRun.Completion.WaitAsync(TestTimeout));
            }
            else
            {
                await queuedRun.Completion.WaitAsync(TestTimeout);
            }
            await queuedCompleted.Task.WaitAsync(TestTimeout);

            Assert.Null(taskHost.ActiveRun);
            Assert.Equal(CopilotHostedRunState.Queued, laterRun.State);
            Assert.False(laterRun.HasStarted);
            Assert.False(laterRun.Completion.IsCompleted);
            Assert.False(laterRun.CancellationToken.IsCancellationRequested);
            Assert.Same(laterRun, Assert.Single(taskHost.QueuedRuns));
            var expectedDraft = hasNewerDraft
                ? "newer draft" + Environment.NewLine + Environment.NewLine + "inspect the queued attachment"
                : "inspect the queued attachment";
            Assert.Equal(shouldCancel, cancellationAccepted);
            Assert.Equal(expectedDraft, conversation.DraftText);
            Assert.Equal(expectedDraft, viewModel.InputText);
            Assert.Single(conversation.Attachments, attachment => attachment.Id == queuedAttachment.Id);
            if (hasNewerDraft)
                Assert.Contains(newerAttachment, conversation.Attachments);
            Assert.False(runtime.Entered.IsCompleted);
            Assert.Equal(originalMessages, conversation.Messages.ToArray());
            Assert.Equal(laterRun.Id, Assert.Single(state.QueuedFollowUpRecoveries).RunId);
            Assert.Equal(laterRun.Id, Assert.Single(viewModel.QueuedFollowUps).RunId);
            if (restart)
            {
                Assert.Equal(CopilotAgentStopReason.Completed, parentAssistant!.AgentStopReason);
                Assert.False(parentAssistant.WasResponseInterrupted);
                Assert.False(parentAssistant.IsThinkingInProgress);
                var diskStore = new CopilotChatStateStore(Path.Combine(root, "restart-state"));
                diskStore.Save(state);
                var reloadedState = diskStore.Load();
                var reloadedConversation = Assert.Single(reloadedState.Conversations);
                Assert.Equal(expectedDraft, reloadedConversation.DraftText);
                var reloadedLater = Assert.Single(reloadedState.QueuedFollowUpRecoveries);
                Assert.Equal(laterRun.Id, reloadedLater.RunId);
                Assert.Equal("Continue only after inspecting the attachment.", reloadedLater.ComposerState?.Text);
                Assert.Equal(laterAttachment.Value, Assert.Single(reloadedLater.ComposerState!.Attachments).Value);
                taskHost.Shutdown();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => laterRun.Completion.WaitAsync(TestTimeout));
                viewModel.Dispose();

                restartedHost = new CopilotAgentTaskHost();
                restartedRuntime = new GatedFailingTurnRuntime();
                restartedViewModel = new CopilotChatViewModel(
                    new CopilotChatService(),
                    new InMemoryStateStore(reloadedState, diskStore.AttachmentDirectoryPath),
                    config,
                    restartedRuntime,
                    restartedHost);
                restartedRun = restartedHost.ActiveRun ?? restartedHost.QueuedRuns.SingleOrDefault();

                Assert.Null(restartedHost.ActiveRun);
                Assert.False(restartedRuntime.Entered.IsCompleted);
                Assert.Empty(restartedHost.ScheduledRuns);
                Assert.Empty(restartedViewModel.QueuedFollowUps);
                Assert.Empty(reloadedState.QueuedFollowUpRecoveries);
                Assert.Equal(expectedDraft + Environment.NewLine + Environment.NewLine
                    + "Continue only after inspecting the attachment.", reloadedConversation.DraftText);
                Assert.Equal(reloadedConversation.DraftText, restartedViewModel.InputText);
                Assert.Equal(originalMessages.Select(message => message.Id),
                    reloadedConversation.Messages.Select(message => message.Id));
                var restoredAttachmentIds = hasNewerDraft
                    ? new[] { newerAttachment.Id, queuedAttachment.Id, laterAttachment.Id }
                    : new[] { queuedAttachment.Id, laterAttachment.Id };
                Assert.Equal(restoredAttachmentIds, reloadedConversation.Attachments.Select(attachment => attachment.Id));
                Assert.Equal(queuedAttachment.Value, Assert.Single(reloadedConversation.Attachments,
                    attachment => attachment.Id == queuedAttachment.Id).Value);
                Assert.Equal(laterAttachment.Value, Assert.Single(reloadedConversation.Attachments,
                    attachment => attachment.Id == laterAttachment.Id).Value);
            }
        }
        finally
        {
            taskHost.Changed -= observeQueuedRun;
            releaseActive.TrySetResult();
            runtime.Release();
            taskHost.Shutdown();
            restartedRuntime?.Release();
            restartedHost?.Shutdown();
            try
            {
                await Task.WhenAll(activeRun.Completion, queuedRun?.Completion ?? Task.CompletedTask,
                    laterRun?.Completion ?? Task.CompletedTask, restartedRun?.Completion ?? Task.CompletedTask).WaitAsync(TestTimeout);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                restartedViewModel?.Dispose();
                viewModel.Dispose();
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task QueuedImageIsCommittedToManagedStorageBeforeItsTurnStarts()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            nameof(QueuedImageIsCommittedToManagedStorageBeforeItsTurnStarts),
            Guid.NewGuid().ToString("N"));
        var attachmentDirectoryPath = Path.Combine(root, "attachments");
        var sourcePath = Path.Combine(root, "source.png");
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(
            sourcePath,
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        profile.SupportsImageInput = true;
        var config = CreateConfig(profile, "queued-image-admission-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var runtime = new GatedFailingTurnRuntime();
        var queuedFollowUp = new CopilotQueuedFollowUp(
            "queued-image-admission-run",
            conversation.Id,
            conversation.Title,
            "inspect the queued image",
            CopilotAgentMode.Auto,
            profile,
            new CopilotAgentHostContextSnapshot(
                "",
                "",
                [CopilotAttachmentItem.CreateImage(sourcePath, "Evidence")]));
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(
            conversation,
            config,
            runtime,
            new CopilotAgentTaskHost(),
            attachmentDirectoryPath);
        using var hostedRun = new CopilotHostedAgentRun(
            conversation.Id,
            CopilotAgentMode.Auto);
        var executeMethod = typeof(CopilotChatViewModel).GetMethod(
            "ExecuteQueuedFollowUpAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ExecuteQueuedFollowUpAsync was not found.");

        try
        {
            var execution = Assert.IsAssignableFrom<Task>(executeMethod.Invoke(
                viewModel,
                [hostedRun, queuedFollowUp]));
            var request = await runtime.Entered.WaitAsync(TestTimeout);
            var admittedImage = Assert.Single(request.HostContext.Attachments);

            Assert.StartsWith(
                Path.Combine(attachmentDirectoryPath, "image-"),
                admittedImage.Value,
                StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(admittedImage.Value));
            Assert.Equal(
                admittedImage.Value,
                Assert.Single(conversation.Messages, message => message.IsUser)
                    .Attachments.Single().Value);

            File.Delete(sourcePath);
            runtime.Release();
            await execution.WaitAsync(TestTimeout);
        }
        finally
        {
            runtime.Release();
            viewModel.Dispose();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
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

    [Theory]
    [InlineData("stop")]
    [InlineData("planner_failure")]
    [InlineData("provider_failure")]
    [InlineData("incomplete_summary")]
    [InlineData("not_needed")]
    [InlineData("failed_response_json")]
    [InlineData("failed_response_sse")]
    [InlineData("failed_response_without_usage")]
    [InlineData("cancel_billed_retry")]
    [InlineData("empty_response_json")]
    public async Task QueuedCompactionOutcomesKeepExpectedFollowUpDispatchAndDrainCommandContext(string outcome)
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        profile.MaxTokens = 4_096;
        var isResponsePayload = outcome is "failed_response_json" or "failed_response_sse"
            or "failed_response_without_usage" or "cancel_billed_retry" or "empty_response_json";
        var expectsCancellation = outcome is "stop" or "cancel_billed_retry";
        if (isResponsePayload)
            profile.BaseUrl = "https://unit.test/v1/responses";
        var otherProfile = CreateProfile("profile-b", "Profile B", "model-b");
        var config = CreateConfig(profile, "queued-compaction-stop-test-token");
        config.AgentDefaults.ContextWindowTokens = CopilotAgentTokenBudget.MinimumContextWindowTokens;
        config.Profiles.Add(otherProfile);
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var compactInputLimits = CopilotConversationRequestBuilder.ResolveHistoryLimits(
            config.AgentDefaults.ContextWindowTokens, profile.MaxTokens);
        var firstRequestLength = outcome == "planner_failure" ? compactInputLimits.MaximumCharacters + 1 : 15_000;
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, new string('u', firstRequestLength))
        {
            RequestMode = CopilotAgentMode.Auto,
        });
        if (outcome != "not_needed")
        {
            conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, new string('a', 15_000))
            {
                RequestMode = CopilotAgentMode.Auto,
            });
            conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Keep the recent request.")
            {
                RequestMode = CopilotAgentMode.Auto,
            });
            conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Keep the recent answer.")
            {
                RequestMode = CopilotAgentMode.Auto,
            });
        }
        var originalMessages = conversation.Messages.ToArray();
        var otherConversation = CreateConversation(otherProfile, "conversation-b", "Unrelated draft.");
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = [conversation, otherConversation],
        };
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var handler = new GatedCompactionHandler();
        using var client = new HttpClient(handler);
        CopilotChatViewModel? cancelTarget = null;
        var service = outcome == "cancel_billed_retry"
            ? new CopilotChatService(client, 3, _ => TimeSpan.Zero, (_, token) =>
            {
                Assert.NotNull(cancelTarget);
                cancelTarget.PrimaryActionCommand.Execute(null);
                Assert.True(token.IsCancellationRequested);
                return Task.FromCanceled(token);
            })
            : new CopilotChatService(client);
        using var viewModel = new CopilotChatViewModel(service,
            new InMemoryStateStore(state), config, runtime, taskHost);
        cancelTarget = viewModel;
        var activeRun = taskHost.Start(conversation.Id, CopilotAgentMode.Auto, _ => releaseActive.Task);
        CopilotHostedAgentRun? queuedRun = null;
        CopilotHostedAgentRun? laterRun = null;
        EventHandler<CopilotAgentTaskHostChangedEventArgs> onHostChanged = (_, args) =>
        {
            if (args.Kind == CopilotAgentTaskHostChangeKind.Completed && args.Run.Id == queuedRun?.Id)
                commandCompleted.TrySetResult();
            if (args.Kind == CopilotAgentTaskHostChangeKind.Completed && args.Run.Id == laterRun?.Id)
                laterCompleted.TrySetResult();
        };
        taskHost.Changed += onHostChanged;

        try
        {
            viewModel.InputText = "/compact";
            Assert.True(viewModel.TryQueueCurrentRunFollowUp());
            queuedRun = Assert.Single(taskHost.QueuedRuns);
            Assert.True(Assert.Single(viewModel.QueuedFollowUps).IsLocalCommand);
            viewModel.InputText = "Continue only after the current work completes.";
            Assert.True(viewModel.TryQueueCurrentRunFollowUp());
            laterRun = Assert.Single(taskHost.QueuedRuns, run => run.Id != queuedRun.Id);
            Assert.False(Assert.Single(viewModel.QueuedFollowUps, item => item.RunId == laterRun.Id).IsLocalCommand);
            viewModel.InputText = "Newer draft.";
            releaseActive.TrySetResult();
            await activeRun.Completion.WaitAsync(TestTimeout);
            if (outcome is "stop" or "provider_failure" or "incomplete_summary" || isResponsePayload)
            {
                var requestCancellation = await handler.Entered.Task.WaitAsync(TestTimeout);
                Assert.Equal(CopilotHostedRunState.Running, queuedRun.State);
                Assert.Equal("停止上下文压缩", viewModel.PrimaryActionToolTip);
                Assert.False(requestCancellation.IsCancellationRequested);
                if (outcome == "stop")
                {
                    viewModel.PrimaryActionCommand.Execute(null);
                    Assert.True(queuedRun.CancellationToken.IsCancellationRequested);
                    await handler.Canceled.Task.WaitAsync(TestTimeout);
                    Assert.True(requestCancellation.IsCancellationRequested);
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queuedRun.Completion.WaitAsync(TestTimeout));
                }
                else if (outcome == "provider_failure")
                {
                    handler.Respond("""
                        {"error":{"code":"invalid_request_error","type":"invalid_request_error","message":"Controlled compaction failure."}}
                        """, System.Net.HttpStatusCode.BadRequest);
                }
                else if (outcome == "incomplete_summary")
                {
                    handler.Respond("""
                        {"choices":[{"message":{"role":"assistant","content":"Partial summary."},"finish_reason":"length"}],
                         "usage":{"prompt_tokens":100,"completion_tokens":10,"total_tokens":110}}
                        """);
                }
                else
                {
                    var responsePayload = JObject.Parse("""
                        {"id":"resp_compaction_failed","object":"response","status":"failed","output":[],
                         "error":{"code":"insufficient_quota","type":"insufficient_quota","message":"Controlled compaction failure."},
                         "usage":{"input_tokens":12,"output_tokens":8,"total_tokens":20,"input_tokens_details":{"cached_tokens":3}}}
                        """);
                    if (outcome == "failed_response_without_usage")
                        responsePayload.Remove("usage");
                    if (outcome == "cancel_billed_retry")
                    {
                        var error = (JObject)responsePayload["error"]!;
                        error["code"] = "server_error";
                        error["type"] = "server_error";
                    }
                    if (outcome == "empty_response_json")
                    {
                        responsePayload["status"] = "completed";
                        responsePayload.Remove("error");
                    }
                    if (outcome == "failed_response_sse")
                    {
                        var failedEvent = new JObject { ["type"] = "response.failed", ["response"] = responsePayload };
                        handler.Respond("event: response.failed\ndata: " + failedEvent.ToString(Newtonsoft.Json.Formatting.None) + "\n\n",
                            contentType: "text/event-stream");
                    }
                    else
                    {
                        handler.Respond(responsePayload.ToString(Newtonsoft.Json.Formatting.None));
                    }
                }
            }
            if (outcome == "cancel_billed_retry")
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queuedRun.Completion.WaitAsync(TestTimeout));
            else if (outcome != "stop")
                await queuedRun.Completion.WaitAsync(TestTimeout);
            await commandCompleted.Task.WaitAsync(TestTimeout);
            Assert.Equal(CopilotHostedRunState.Completed, queuedRun.State);
            Assert.Equal(expectsCancellation, queuedRun.Completion.IsCanceled);
            Assert.Equal(expectsCancellation, queuedRun.CancellationToken.IsCancellationRequested);
            Assert.Equal(outcome is "planner_failure" or "not_needed" ? 0 : 1, handler.RequestCount);
            Assert.Null(conversation.Compaction);
            if (outcome == "incomplete_summary")
            {
                Assert.Equal(1, conversation.CompactionUsage?.RequestCount);
                Assert.Equal(new CopilotTokenUsage(100, 10, 110), conversation.CompactionUsage?.Usage);
            }
            else if (outcome is "failed_response_json" or "failed_response_sse" or "cancel_billed_retry" or "empty_response_json")
            {
                Assert.Equal(1, conversation.CompactionUsage?.RequestCount);
                Assert.Equal(new CopilotTokenUsage(12, 8, 20, 3), conversation.CompactionUsage?.Usage);
            }
            else
            {
                Assert.Null(conversation.CompactionUsage);
            }
            Assert.Equal(originalMessages, conversation.Messages.Take(originalMessages.Length).ToArray());
            Assert.Null(typeof(CopilotChatViewModel).GetField(
                "_queuedLocalCommandExecution", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewModel));
            if (outcome == "not_needed")
            {
                var laterRequest = await runtime.Entered.WaitAsync(TestTimeout);
                Assert.Equal("Continue only after the current work completes.", laterRequest.UserText);
                Assert.True(laterRun.HasStarted);
                Assert.Same(laterRun, taskHost.ActiveRun);
                Assert.Empty(viewModel.QueuedFollowUps);
                Assert.Empty(state.QueuedFollowUpRecoveries);
                runtime.Release();
                await laterRun.Completion.WaitAsync(TestTimeout);
                await laterCompleted.Task.WaitAsync(TestTimeout);
                Assert.Empty(taskHost.ScheduledRuns);
            }
            else
            {
                Assert.Null(taskHost.ActiveRun);
                Assert.Equal(originalMessages, conversation.Messages.ToArray());
                Assert.Equal(CopilotHostedRunState.Queued, laterRun.State);
                Assert.False(laterRun.HasStarted);
                Assert.False(laterRun.Completion.IsCompleted);
                Assert.False(laterRun.CancellationToken.IsCancellationRequested);
                Assert.Equal(laterRun.Id, Assert.Single(viewModel.QueuedFollowUps).RunId);
                Assert.Equal(laterRun.Id, Assert.Single(state.QueuedFollowUpRecoveries).RunId);
                Assert.Same(laterRun, Assert.Single(taskHost.ScheduledRuns));
                Assert.False(runtime.Entered.IsCompleted);
                Assert.Contains(outcome switch
                {
                    "stop" or "cancel_billed_retry" => "压缩已取消",
                    "planner_failure" => "压缩未开始",
                    _ => "压缩失败",
                }, viewModel.LocalCommandResultText, StringComparison.Ordinal);
            }
            Assert.False(viewModel.IsBusy);
            Assert.Same(conversation, viewModel.SelectedConversation);
            Assert.Same(profile, viewModel.SelectedProfile);
            Assert.Equal("Newer draft.", viewModel.InputText);
            Assert.Equal("Newer draft.", conversation.DraftText);
            Assert.Equal("Unrelated draft.", otherConversation.DraftText);
            Assert.Equal(otherProfile.Id, otherConversation.ProfileId);
            Assert.Equal("model-a", profile.Model);
            Assert.Equal("model-b", otherProfile.Model);
            Assert.Empty(otherConversation.Messages);
        }
        finally
        {
            runtime.Release();
            releaseActive.TrySetResult();
            taskHost.Shutdown();
            try
            {
                await Task.WhenAll(activeRun.Completion, queuedRun?.Completion ?? Task.CompletedTask,
                    laterRun?.Completion ?? Task.CompletedTask).WaitAsync(TestTimeout);
            }
            catch (OperationCanceledException)
            {
            }
            taskHost.Changed -= onHostChanged;
        }
    }

    [Fact]
    public async Task QueuedPlanCommandStartsBeforeLaterFollowUpWithoutConsumingNewerDraft()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-plan-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var planAttachment = CopilotAttachmentItem.CreateContext("plan command attachment");
        var newerAttachment = CopilotAttachmentItem.CreateContext("newer draft attachment");
        conversation.Attachments.Add(planAttachment);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        CopilotHostedAgentRun? planRun = null;
        CopilotQueuedFollowUp? laterFollowUp = null;

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/plan inspect the deferred command path";
            viewModel.QueueFollowUpCommand.Execute(null);
            var commandItem = Assert.Single(viewModel.QueuedFollowUps);
            Assert.True(commandItem.IsLocalCommand);

            viewModel.InputText = "run after the plan";
            viewModel.QueueFollowUpCommand.Execute(null);
            laterFollowUp = Assert.Single(viewModel.QueuedFollowUps, item => !item.IsLocalCommand);
            viewModel.InputText = "newer draft";
            conversation.Attachments.Add(newerAttachment);

            releaseActive.TrySetResult();
            var request = await runtime.Entered.WaitAsync(TestTimeout);
            planRun = taskHost.ActiveRun;

            Assert.NotNull(planRun);
            Assert.Equal(CopilotAgentMode.Plan, request.Mode);
            Assert.Equal("inspect the deferred command path", request.UserText);
            Assert.Equal(1, taskHost.GetQueuePosition(laterFollowUp.RunId));
            Assert.Equal("newer draft", viewModel.InputText);
            Assert.Equal("newer draft", conversation.DraftText);
            Assert.Contains(request.HostContext.Attachments, attachment => attachment.Id == planAttachment.Id);
            Assert.DoesNotContain(conversation.Attachments, attachment => attachment.Id == planAttachment.Id);
            Assert.Contains(newerAttachment, conversation.Attachments);
            Assert.DoesNotContain(
                conversation.Messages,
                message => message.IsUser && string.Equals(message.Content, "/plan inspect the deferred command path", StringComparison.Ordinal));
            Assert.Contains(
                conversation.Messages,
                message => message.IsUser && string.Equals(message.Content, "inspect the deferred command path", StringComparison.Ordinal));
        }
        finally
        {
            runtime.Release();
            if (planRun != null && !planRun.Completion.IsCompleted)
            {
                try
                {
                    await planRun.Completion.WaitAsync(TestTimeout);
                }
                catch
                {
                }
            }
            if (laterFollowUp != null)
                taskHost.RequestCancel(laterFollowUp.RunId);
            releaseActive.TrySetResult();
            if (!activeRun.Completion.IsCompleted)
                await activeRun.Completion.WaitAsync(TestTimeout);
            viewModel.Dispose();
        }
    }

    [Fact]
    public async Task QueuedRetryCommandSchedulesTheRetryWithoutSendingItsSlashText()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-retry-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "retry this request")
        {
            RequestMode = CopilotAgentMode.Auto,
        });
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "old answer")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        });
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        CopilotHostedAgentRun? retryRun = null;

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/retry";
            viewModel.QueueFollowUpCommand.Execute(null);
            var commandRun = Assert.Single(taskHost.QueuedRuns);
            viewModel.InputText = "newer draft";

            releaseActive.TrySetResult();
            var request = await runtime.Entered.WaitAsync(TestTimeout);
            retryRun = taskHost.ActiveRun;

            Assert.NotNull(retryRun);
            Assert.True(retryRun.IsAgent);
            Assert.Equal("retry this request", request.UserText);
            Assert.Equal(CopilotAgentMode.Auto, request.Mode);
            Assert.Equal("newer draft", viewModel.InputText);
            Assert.DoesNotContain(
                conversation.Messages,
                message => message.IsUser && string.Equals(message.Content, "/retry", StringComparison.Ordinal));
            await commandRun.Completion.WaitAsync(TestTimeout);
        }
        finally
        {
            runtime.Release();
            if (retryRun != null && !retryRun.Completion.IsCompleted)
            {
                try
                {
                    await retryRun.Completion.WaitAsync(TestTimeout);
                }
                catch
                {
                }
            }
            releaseActive.TrySetResult();
            if (!activeRun.Completion.IsCompleted)
                await activeRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task UnknownQueuedSlashCommandReportsLocallyWithoutCreatingAgentMessages()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-unknown-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(
            conversation,
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });

        await activeStarted.Task.WaitAsync(TestTimeout);
        viewModel.InputText = "/command-that-does-not-exist";
        viewModel.QueueFollowUpCommand.Execute(null);
        var queuedRun = Assert.Single(taskHost.QueuedRuns);

        releaseActive.TrySetResult();
        await activeRun.Completion.WaitAsync(TestTimeout);
        await queuedRun.Completion.WaitAsync(TestTimeout);

        Assert.NotEqual(string.Empty, viewModel.LocalCommandResultTitle);
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public async Task QueuedStopCommandDoesNotCancelItsDispatcherOrBlockTheNextFollowUp()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "queued-stop-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(conversation, config, runtime, taskHost);
        var activeRun = taskHost.Start(
            conversation.Id,
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        CopilotHostedAgentRun? followUpRun = null;

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            viewModel.InputText = "/stop";
            viewModel.QueueFollowUpCommand.Execute(null);
            viewModel.InputText = "run after queued stop";
            viewModel.QueueFollowUpCommand.Execute(null);

            releaseActive.TrySetResult();
            var request = await runtime.Entered.WaitAsync(TestTimeout);
            followUpRun = taskHost.ActiveRun;

            Assert.Equal("run after queued stop", request.UserText);
            Assert.Contains("没有正在运行的任务", viewModel.LocalCommandResultText, StringComparison.Ordinal);
        }
        finally
        {
            runtime.Release();
            if (followUpRun != null && !followUpRun.Completion.IsCompleted)
            {
                try
                {
                    await followUpRun.Completion.WaitAsync(TestTimeout);
                }
                catch
                {
                }
            }
            releaseActive.TrySetResult();
            if (!activeRun.Completion.IsCompleted)
                await activeRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Theory]
    [InlineData(false, CopilotAgentTaskHost.DefaultMaxQueuedRuns, "none")]
    [InlineData(true, CopilotAgentTaskHost.DefaultMaxQueuedRuns, "none")]
    [InlineData(false, 1, "none")]
    [InlineData(false, CopilotAgentTaskHost.DefaultMaxQueuedRuns, "review")]
    [InlineData(false, CopilotAgentTaskHost.DefaultMaxQueuedRuns, "skill")]
    [InlineData(false, CopilotAgentTaskHost.DefaultMaxQueuedRuns, "newer_auto")]
    public async Task MissingQueuedProfileRestoresLaterFollowUpInOrderInsteadOfDispatchingPastIt(
        bool secondRecordIsLegacy, int restartedHostCapacity, string preservedReference)
    {
        const string skillName = "queued-recovery-duplicate-skill";
        var sameReviewTarget = preservedReference == "review";
        var sameSkillReference = preservedReference == "skill";
        var preservesNewerAutoMode = preservedReference == "newer_auto";
        var preservesReference = sameReviewTarget || sameSkillReference;
        var skillInvocation = sameSkillReference ? "$" + skillName + " " : string.Empty;
        var firstPrompt = skillInvocation + "First update the configuration using the captured evidence.";
        var laterPrompt = skillInvocation + "Then validate the configuration after that update.";
        var newerDraft = preservesReference ? string.Empty : "Preserve this newer draft before the recovered requests.";
        var profileA = CreateProfile("profile-a", "Profile A", "model-a");
        var profileB = CreateProfile("profile-b", "Profile B", "model-b");
        var config = CreateConfig(profileA, "missing-queued-profile-test-token");
        config.Profiles.Add(profileB);
        var conversation = CreateConversation(profileA, "conversation-a", string.Empty);
        var reviewTarget = sameReviewTarget ? new CopilotWorkspaceReviewTargetContext
        {
            Target = CopilotWorkspaceReviewTarget.BaseBranch, Revision = "origin/develop",
        } : null;
        var parentMode = sameReviewTarget ? CopilotAgentMode.Review
            : preservesNewerAutoMode ? CopilotAgentMode.Plan : CopilotAgentMode.Auto;
        var originalMessages = CompleteParentTurnForQueuedCommand(conversation, reviewTarget, parentMode);
        var firstAttachment = CopilotAttachmentItem.CreateContext("Evidence captured with the first request.");
        var laterAttachment = CopilotAttachmentItem.CreateContext("Evidence captured with the later request.");
        var newerAttachment = CopilotAttachmentItem.CreateContext("Evidence attached to the newer draft.");
        conversation.Attachments.Add(firstAttachment);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id, ActiveProfileId = profileA.Id, Conversations = [conversation],
        };
        var runtime = new GatedFailingTurnRuntime();
        var taskHost = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var root = Directory.CreateTempSubdirectory("CopilotQueuedMissingProfile-").FullName;
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        var viewModel = new CopilotChatViewModel(new CopilotChatService(), new InMemoryStateStore(state), config, runtime, taskHost);
        var activeRun = taskHost.Start(conversation.Id, parentMode, async _ =>
        {
            activeStarted.TrySetResult();
            await releaseActive.Task;
        });
        CopilotHostedAgentRun? firstRun = null;
        CopilotHostedAgentRun? laterRun = null;
        var restartedHost = new CopilotAgentTaskHost(restartedHostCapacity);
        var restartedRuntime = new GatedFailingTurnRuntime();
        CopilotChatViewModel? restartedViewModel = null;
        CopilotLocalCommand? skillCompletion = null;
        CopilotAgentSkillReference? skillReference = null;

        try
        {
            await activeStarted.Task.WaitAsync(TestTimeout);
            if (sameSkillReference)
            {
                var firstSkillDirectory = Directory.CreateDirectory(Path.Combine(root, ".agents", "skills", "first")).FullName;
                var selectedSkillDirectory = Directory.CreateDirectory(Path.Combine(root, ".agents", "skills", "second")).FullName;
                File.WriteAllText(Path.Combine(firstSkillDirectory, "SKILL.md"),
                    "---\nname: " + skillName + "\ndescription: First implementation\n---\nUse the first implementation.");
                var selectedSkillPath = Path.Combine(selectedSkillDirectory, "SKILL.md");
                File.WriteAllText(selectedSkillPath,
                    "---\nname: " + skillName + "\ndescription: Selected implementation\n---\nUse the selected implementation.");
                var skills = CopilotAgentSkillCatalog.Discover([root], overrides: null,
                    applicationBaseDirectory: root, userProfileDirectory: root);
                Assert.Equal(2, skills.Count);
                Assert.All(skills, skill => Assert.Empty(skill.Dependencies));
                skillCompletion = Assert.Single(CopilotLocalCommandCatalog.Suggest("$queued-recovery-duplicate", skills),
                    command => command.AgentSkillReference?.SkillFilePath == selectedSkillPath);
                skillReference = Assert.IsType<CopilotAgentSkillReference>(skillCompletion.AgentSkillReference);
                Assert.True(viewModel.TryCompleteLocalCommand(skillCompletion));
            }
            viewModel.InputText = firstPrompt;
            Assert.True(viewModel.TryQueueCurrentRunFollowUp());
            firstRun = Assert.Single(taskHost.QueuedRuns);
            viewModel.SelectedProfile = profileB;
            if (sameSkillReference)
                Assert.True(viewModel.TryCompleteLocalCommand(skillCompletion));
            viewModel.InputText = laterPrompt;
            conversation.Attachments.Add(laterAttachment);
            Assert.True(viewModel.TryQueueCurrentRunFollowUp());
            laterRun = Assert.Single(taskHost.QueuedRuns, run => run.Id != firstRun.Id);
            if (preservesNewerAutoMode)
            {
                var staged = viewModel.QueueExternalPrompt(newerDraft,
                    startNewConversation: false, sendNow: false, mode: CopilotAgentMode.Auto);
                Assert.True(staged.Accepted);
                Assert.False(staged.WasSent);
                Assert.Equal(CopilotAgentMode.Auto, conversation.DraftRequestMode);
            }
            else
            {
                viewModel.InputText = newerDraft;
            }
            if (!preservesReference)
                conversation.Attachments.Add(newerAttachment);

            var diskStore = new CopilotChatStateStore(root);
            diskStore.Save(state);
            if (secondRecordIsLegacy)
            {
                var document = JObject.Parse(File.ReadAllText(diskStore.StateFilePath));
                var records = Assert.IsType<JArray>(document[nameof(CopilotChatState.QueuedFollowUpRecoveries)]);
                var laterRecord = Assert.IsType<JObject>(records[1]);
                Assert.Equal(laterRun.Id, laterRecord[nameof(CopilotQueuedFollowUpRecoveryRecord.RunId)]?.Value<string>());
                Assert.True(laterRecord.Remove(nameof(CopilotQueuedFollowUpRecoveryRecord.ResumeAfterRestart)));
                File.WriteAllText(diskStore.StateFilePath, document.ToString(Newtonsoft.Json.Formatting.None));
            }
            var reloadedState = diskStore.Load();
            Assert.Equal(new[] { firstRun.Id, laterRun.Id }, reloadedState.QueuedFollowUpRecoveries.Select(record => record.RunId));
            Assert.Equal(new[] { profileA.Id, profileB.Id }, reloadedState.QueuedFollowUpRecoveries.Select(record => record.ProfileId));
            Assert.Equal(new[] { true, !secondRecordIsLegacy },
                reloadedState.QueuedFollowUpRecoveries.Select(record => record.ResumeAfterRestart));
            Assert.Equal(new[] { firstAttachment.Id, laterAttachment.Id }, reloadedState.QueuedFollowUpRecoveries
                .Select(record => Assert.Single(record.ComposerState!.Attachments).Id));
            if (preservesNewerAutoMode)
            {
                var savedConversation = Assert.Single(reloadedState.Conversations);
                Assert.Equal(newerDraft, savedConversation.DraftText);
                Assert.Equal(CopilotAgentMode.Auto, savedConversation.DraftRequestMode);
                Assert.All(savedConversation.Messages, message => Assert.Equal(CopilotAgentMode.Plan, message.RequestMode));
                Assert.All(reloadedState.QueuedFollowUpRecoveries,
                    record => Assert.Equal(CopilotAgentMode.Plan, record.ComposerState!.RequestMode));
            }
            if (sameReviewTarget)
            {
                Assert.DoesNotContain(reviewTarget!.Revision, firstPrompt, StringComparison.Ordinal);
                Assert.DoesNotContain(reviewTarget.Revision, laterPrompt, StringComparison.Ordinal);
                Assert.All(reloadedState.QueuedFollowUpRecoveries, record =>
                {
                    Assert.Equal(CopilotAgentMode.Review, record.ComposerState!.RequestMode);
                    Assert.Equal(reviewTarget.Target, record.ComposerState.WorkspaceReviewTarget?.Target);
                    Assert.Equal(reviewTarget.Revision, record.ComposerState.WorkspaceReviewTarget?.Revision);
                });
            }
            if (sameSkillReference)
            {
                Assert.All(reloadedState.QueuedFollowUpRecoveries, record =>
                {
                    Assert.Equal(skillReference!.Name, record.ComposerState!.AgentSkillReference?.Name);
                    Assert.Equal(skillReference.SkillFilePath, record.ComposerState.AgentSkillReference?.SkillFilePath);
                });
            }
            Assert.False(runtime.Entered.IsCompleted);
            taskHost.Shutdown();
            releaseActive.TrySetResult();
            await ShutdownQueuedCommandHostAsync(taskHost, runtime, activeRun, firstRun, laterRun);
            viewModel.Dispose();
            Assert.True(config.Profiles.Remove(profileA));

            restartedViewModel = new CopilotChatViewModel(new CopilotChatService(),
                new InMemoryStateStore(reloadedState, diskStore.AttachmentDirectoryPath), config, restartedRuntime, restartedHost);
            var restored = Assert.Single(restartedViewModel.Conversations);

            Assert.Null(restartedHost.ActiveRun);
            Assert.Empty(restartedHost.ScheduledRuns);
            Assert.False(restartedRuntime.Entered.IsCompleted);
            Assert.Empty(restartedViewModel.QueuedFollowUps);
            Assert.Empty(reloadedState.QueuedFollowUpRecoveries);
            Assert.Equal(2, reloadedState.RecoveredQueuedFollowUpCount);
            Assert.Equal(0, reloadedState.ResumedQueuedFollowUpCount);
            Assert.StartsWith(newerDraft, restored.DraftText, StringComparison.Ordinal);
            Assert.Contains(firstPrompt, restored.DraftText, StringComparison.Ordinal);
            Assert.Contains(laterPrompt, restored.DraftText, StringComparison.Ordinal);
            Assert.True(restored.DraftText.IndexOf(firstPrompt, StringComparison.Ordinal)
                < restored.DraftText.IndexOf(laterPrompt, StringComparison.Ordinal));
            Assert.Equal(restored.DraftText, restartedViewModel.InputText);
            var expectedAttachments = preservesReference ? new[] { firstAttachment, laterAttachment }
                : new[] { newerAttachment, firstAttachment, laterAttachment };
            Assert.Equal(expectedAttachments.Select(item => (item.Id, item.Value)),
                restored.Attachments.Select(item => (item.Id, item.Value)));
            Assert.Equal(originalMessages.Select(message => (message.Id, message.Content)),
                restored.Messages.Select(message => (message.Id, message.Content)));
            Assert.Equal(CopilotAgentStopReason.Completed, restored.Messages[1].AgentStopReason);
            Assert.False(restored.Messages[1].WasResponseInterrupted);
            Assert.False(restored.Messages[1].IsThinkingInProgress);
            if (preservesNewerAutoMode)
            {
                Assert.Equal(CopilotAgentMode.Auto, restored.DraftRequestMode);
                Assert.Contains("[Plan] " + firstPrompt, restored.DraftText, StringComparison.Ordinal);
                Assert.Contains("[Plan] " + laterPrompt, restored.DraftText, StringComparison.Ordinal);
            }
            if (sameReviewTarget)
            {
                Assert.Equal(CopilotAgentMode.Review, restored.DraftRequestMode);
                Assert.Equal(reviewTarget!.Target, restored.DraftWorkspaceReviewTarget?.Target);
                Assert.Equal(reviewTarget.Revision, restored.DraftWorkspaceReviewTarget?.Revision);
            }
            if (sameSkillReference)
            {
                Assert.Equal(skillReference!.Name, restored.DraftAgentSkillReference?.Name);
                Assert.Equal(skillReference.SkillFilePath, restored.DraftAgentSkillReference?.SkillFilePath);
            }

            var recoveredDraft = restored.DraftText;
            restartedViewModel.SendCommand.Execute(null);
            var request = await restartedRuntime.Entered.WaitAsync(TestTimeout);

            Assert.NotNull(restartedHost.ActiveRun);
            Assert.Equal(profileB.Id, request.Profile.Id);
            Assert.Equal(recoveredDraft, request.UserText);
            Assert.Equal(expectedAttachments.Select(item => (item.Id, item.Value)),
                request.HostContext.Attachments.Select(item => (item.Id, item.Value)));
            if (preservesNewerAutoMode)
                Assert.Equal(CopilotAgentMode.Auto, request.Mode);
            if (sameReviewTarget)
            {
                Assert.Equal(CopilotAgentMode.Review, request.Mode);
                Assert.Equal(reviewTarget!.Target, request.WorkspaceReviewTarget?.Target);
                Assert.Equal(reviewTarget.Revision, request.WorkspaceReviewTarget?.Revision);
            }
            if (sameSkillReference)
            {
                Assert.Equal(skillReference!.Name, request.AgentSkillReference?.Name);
                Assert.Equal(skillReference.SkillFilePath, request.AgentSkillReference?.SkillFilePath);
            }
            Assert.Equal(string.Empty, restartedViewModel.InputText);
            Assert.Empty(restored.Attachments);
        }
        finally
        {
            var originalRuns = new[] { activeRun, firstRun, laterRun }.OfType<CopilotHostedAgentRun>().ToArray();
            var restartedRuns = restartedHost.ScheduledRuns.ToArray();
            taskHost.Shutdown();
            releaseActive.TrySetResult();
            restartedRuntime.Release();
            restartedHost.Shutdown();
            try
            {
                try
                {
                    await ShutdownQueuedCommandHostAsync(taskHost, runtime, originalRuns);
                }
                finally
                {
                    await ShutdownQueuedCommandHostAsync(restartedHost, restartedRuntime, restartedRuns);
                }
            }
            finally
            {
                restartedViewModel?.Dispose();
                viewModel.Dispose();
                var resolvedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
                var resolvedTemp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
                Assert.True(string.Equals(Path.GetDirectoryName(resolvedRoot), resolvedTemp, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(resolvedRoot).StartsWith("CopilotQueuedMissingProfile-", StringComparison.Ordinal));
                Directory.Delete(resolvedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestartedQueuedSlashCommandKeepsItsKindAndExecutesLocally()
    {
        var profile = CreateProfile("profile-a", "Profile A", "model-a");
        var config = CreateConfig(profile, "restarted-queued-command-test-token");
        var conversation = CreateConversation(profile, "conversation-a", string.Empty);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = profile.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            QueuedFollowUpRecoveries =
            [
                new CopilotQueuedFollowUpRecoveryRecord
                {
                    RunId = "queued-command-restart",
                    ConversationId = conversation.Id,
                    Prompt = "/status",
                    ComposerState = CopilotComposerStash.Capture(
                        "/status",
                        "/status".Length,
                        CopilotAgentMode.Auto,
                        []),
                    ProfileId = profile.Id,
                    QueuedAtUtc = new DateTimeOffset(2026, 8, 11, 16, 0, 0, TimeSpan.Zero),
                    ResumeAfterRestart = true,
                    IsLocalCommand = true,
                },
            ],
        };
        var taskHost = new CopilotAgentTaskHost();
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(state),
            config,
            new GatedFailingTurnRuntime(),
            taskHost);
        var restoredRun = taskHost.ActiveRun ?? taskHost.QueuedRuns.SingleOrDefault();

        if (restoredRun != null)
        {
            Assert.False(restoredRun.IsAgent);
            Assert.Null(restoredRun.RunControl);
            await restoredRun.Completion.WaitAsync(TestTimeout);
        }

        Assert.Contains("/status", viewModel.LocalCommandResultTitle, StringComparison.Ordinal);
        Assert.Empty(conversation.Messages);
        Assert.Empty(state.QueuedFollowUpRecoveries);
    }

    [Fact]
    public void PersistentFullAccessRestoresAndCanBeChangedFromComposer()
    {
        StaTest.Run(() =>
        {
            var profile = CreateProfile("profile-a", "Profile A", "model-a");
            var config = CreateConfig(profile, "persistent-full-access-test-token");
            var conversation = CreateConversation(profile, "conversation-a", string.Empty);
            var state = new CopilotChatState
            {
                ActiveConversationId = conversation.Id,
                ActiveProfileId = profile.Id,
                DefaultAccessMode = CopilotAgentAccessMode.UnrestrictedFullAccess,
                Conversations = [conversation],
            };
            using var solutionManagerScope = new IsolatedSolutionManagerScope();
            using var viewModel = new CopilotChatViewModel(
                new CopilotChatService(),
                new InMemoryStateStore(state),
                config,
                new GatedFailingTurnRuntime(),
                new CopilotAgentTaskHost());

            Assert.True(viewModel.IsComposerFullAccess);
            Assert.Equal(CopilotAgentAccessMode.UnrestrictedFullAccess, conversation.AccessMode);
            Assert.True(string.IsNullOrWhiteSpace(conversation.FullAccessWorkspacePath));

            viewModel.SetComposerAccessModeCommand.Execute(CopilotAgentAccessMode.ConfirmProtectedActions);
            Assert.Equal(CopilotAgentAccessMode.ConfirmProtectedActions, state.DefaultAccessMode);
            Assert.True(viewModel.IsComposerConfirmAccess);
            Assert.Equal(CopilotAgentAccessMode.ConfirmProtectedActions, conversation.AccessMode);

            viewModel.SetComposerAccessModeCommand.Execute(CopilotAgentAccessMode.UnrestrictedFullAccess);
            Assert.Equal(CopilotAgentAccessMode.UnrestrictedFullAccess, state.DefaultAccessMode);
            Assert.True(viewModel.IsComposerFullAccess);
            Assert.Equal(CopilotAgentAccessMode.UnrestrictedFullAccess, conversation.AccessMode);
        });
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

    private static bool CommitAgentRunStateAndResolveSteering(
        CopilotChatViewModel viewModel,
        CopilotHostedAgentRun hostedRun,
        CopilotConversationRecord conversation,
        CopilotAgentTaskEventJournalSnapshot journal,
        CopilotAgentSessionCheckpoint checkpoint,
        CopilotAgentStopReason stopReason)
    {
        var method = typeof(CopilotChatViewModel).GetMethod(
            "CommitAgentRunStateAndResolveSteering",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CommitAgentRunStateAndResolveSteering was not found.");
        return Assert.IsType<bool>(method.Invoke(
            viewModel,
            [hostedRun, conversation, journal, checkpoint, stopReason]));
    }

    private static async Task ProcessGoalAfterTurnAsync(
        CopilotChatViewModel viewModel,
        CopilotHostedAgentRun hostedRun,
        CopilotConversationRecord conversation,
        CopilotProfileConfig profile,
        CopilotChatMessage userMessage,
        CopilotChatMessage assistantMessage,
        string boundGoalId,
        CopilotTokenUsage turnUsage)
    {
        var method = typeof(CopilotChatViewModel).GetMethod(
            "ProcessGoalAfterTurnAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ProcessGoalAfterTurnAsync was not found.");
        var task = (Task?)method.Invoke(
            viewModel,
            [
                hostedRun,
                conversation,
                profile,
                userMessage,
                assistantMessage,
                boundGoalId,
                turnUsage,
            ]);
        Assert.NotNull(task);
        await task;
    }

    private static bool TryQueueGoalContinuation(
        CopilotChatViewModel viewModel,
        CopilotHostedAgentRun hostedRun,
        CopilotConversationRecord conversation,
        CopilotChatMessage assistantMessage,
        CopilotProfileConfig profile,
        CopilotAgentHostContextSnapshot completedTurnSnapshot,
        CopilotTurnRuntimeConfigSnapshot completedTurnRuntimeConfig,
        string goalId,
        string reason)
    {
        var method = typeof(CopilotChatViewModel).GetMethod(
            "TryQueueGoalContinuation",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TryQueueGoalContinuation was not found.");
        return Assert.IsType<bool>(method.Invoke(
            viewModel,
            [
                hostedRun,
                conversation,
                assistantMessage,
                profile,
                completedTurnSnapshot,
                completedTurnRuntimeConfig,
                goalId,
                reason,
            ]));
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

    private sealed class GatedFailingTurnRuntime(bool emitPendingChatDelta = false) : ICopilotTurnRuntime
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
            new(CopilotSteeringAdmissionReason.RuntimeUnavailable);

        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;

        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;

        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;

        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(
            CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken) =>
            Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }

    private sealed class AcceptingQuestionTurnRuntime : ICopilotTurnRuntime
    {
        public string Answer { get; private set; } = string.Empty;

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield break;
        }

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) =>
            new(CopilotSteeringAdmissionReason.RuntimeUnavailable);

        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;

        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;

        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer)
        {
            Answer = answer;
            return true;
        }

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

    private sealed class GatedGoalCompletionEvaluator(CopilotTokenUsage usage) : ICopilotGoalCompletionEvaluator
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public async Task<CopilotGoalEvaluationResult> EvaluateAsync(
            CopilotProfileConfig profile,
            CopilotConversationGoal goal,
            IReadOnlyList<CopilotRequestMessage> transcript,
            CopilotGoalTurnEvidence turnEvidence,
            CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return new CopilotGoalEvaluationResult(
                CopilotGoalEvaluationVerdict.Continue,
                "More verified work remains.",
                usage);
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class CancellingGoalCompletionEvaluator(
        Action requestCancellation,
        CopilotTokenUsage usage) : ICopilotGoalCompletionEvaluator
    {
        public Task<CopilotGoalEvaluationResult> EvaluateAsync(
            CopilotProfileConfig profile,
            CopilotConversationGoal goal,
            IReadOnlyList<CopilotRequestMessage> transcript,
            CopilotGoalTurnEvidence turnEvidence,
            CancellationToken cancellationToken)
        {
            requestCancellation();
            return Task.FromResult(new CopilotGoalEvaluationResult(
                CopilotGoalEvaluationVerdict.Continue,
                "More verified work remains.",
                usage));
        }
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

    private sealed class PausedAttachmentSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        public ManualResetEventSlim CallbackPosted { get; } = new();

        public override void Post(SendOrPostCallback callback, object? state)
        {
            _callbacks.Enqueue((callback, state));
            CallbackPosted.Set();
        }

        public void RunPending()
        {
            while (_callbacks.TryDequeue(out var callback))
                callback.Callback(callback.State);
        }

        public void Dispose() => CallbackPosted.Dispose();
    }
}
