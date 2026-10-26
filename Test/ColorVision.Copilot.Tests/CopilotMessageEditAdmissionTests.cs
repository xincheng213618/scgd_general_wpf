using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotMessageEditAdmissionTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private const string OriginalPrompt = "Original request with a captured image.";
    private const string SubmittedEdit = "The edited request submitted before admission.";
    private const string NewerText = "A later edit or draft that must not be consumed.";

    [Theory]
    [InlineData(false, "keyboard")]
    [InlineData(false, "button")]
    [InlineData(false, "command")]
    [InlineData(false, "dispose")]
    [InlineData(true, "button")]
    public void CancelImagePreparationPreservesDraftAndDoesNotSchedule(bool retry, string cancellationAction)
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            using var context = new CopilotPausedAdmissionContext(TestTimeout, "Cancelled image preparation did not drain.");
            var previousContext = SynchronizationContext.Current;
            Task? send = null;
            Task StartRequest() => retry
                ? InvokeTask(fixture.ViewModel, "RetryMessageAsync", [fixture.OriginalUser, false], [typeof(CopilotChatMessage), typeof(bool)])
                : InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes);
            try
            {
                fixture.Conversation.Attachments.Add(Assert.Single(fixture.OriginalUser.Attachments).CreateSnapshot());
                fixture.ViewModel.InputText = SubmittedEdit;
                SynchronizationContext.SetSynchronizationContext(context);
                send = StartRequest();
                Assert.True(context.WaitForCallback(TestTimeout));
                Assert.False(send.IsCompleted);
                Assert.Empty(fixture.Runtime.Requests);
                Assert.Equal("■", fixture.ViewModel.PrimaryActionGlyph);

                if (cancellationAction == "keyboard")
                {
                    fixture.ViewModel.SelectedConversation = fixture.OtherConversation;
                    Assert.False(fixture.ViewModel.TryStopCurrentReplyFromKeyboard());
                    fixture.ViewModel.SelectedConversation = fixture.Conversation;
                }
                // A newer draft is not owned by the suspended request either.
                fixture.ViewModel.InputText = NewerText;
                fixture.Conversation.Attachments.Add(CopilotAttachmentItem.CreateContext("Newer attachment"));
                var retainedAttachments = fixture.Conversation.Attachments.ToArray();
                switch (cancellationAction)
                {
                    case "keyboard":
                        Assert.True(fixture.ViewModel.TryStopCurrentReplyFromKeyboard());
                        break;
                    case "button":
                        fixture.ViewModel.PrimaryActionCommand.Execute(null);
                        break;
                    case "command":
                        Assert.True((bool)typeof(CopilotChatViewModel).GetMethod("TryExecuteLocalCommand",
                            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(fixture.ViewModel, ["/stop", false, null])!);
                        break;
                    default:
                        fixture.ViewModel.Dispose();
                        break;
                }

                // A cancelled preparation must not consume another stop while its UI continuation drains.
                Assert.False(fixture.ViewModel.TryStopCurrentReplyFromKeyboard());
                context.Complete(send);
                send.GetAwaiter().GetResult();
                Assert.Empty(fixture.Runtime.Requests);
                Assert.Empty(fixture.Host.ScheduledRuns);
                Assert.Equal([fixture.OriginalUser, fixture.OriginalAssistant], fixture.Conversation.Messages.ToArray());
                Assert.Equal(NewerText, fixture.ViewModel.InputText);
                Assert.Equal(retainedAttachments, fixture.Conversation.Attachments.ToArray());
                Assert.False(fixture.ViewModel.TryStopCurrentReplyFromKeyboard());
                Assert.NotEqual("■", fixture.ViewModel.PrimaryActionGlyph);

                if (cancellationAction != "dispose")
                {
                    // Cancellation releases admission ownership, so the user can submit again.
                    send = StartRequest();
                    context.Complete(send);
                    send.GetAwaiter().GetResult();
                    Assert.Single(fixture.Runtime.Requests);
                }
            }
            finally
            {
                if (send is { IsCompleted: false })
                {
                    fixture.ViewModel.Dispose();
                    context.Complete(send);
                }
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }, TimeSpan.FromSeconds(40), "The image preparation cancellation test did not finish.");
    }

    [Fact]
    public void CancelImagePreparationCancelsItsQueuedCommandOwnerAndLeavesLaterFollowUpQueued()
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            using var context = new CopilotPausedAdmissionContext(TestTimeout, "The cancelled command image preparation did not drain.");
            using var imageLease = new CopilotManagedImageAttachmentLease();
            var previousContext = SynchronizationContext.Current;
            Task<IReadOnlyList<CopilotAttachmentItem>?>? preparation = null;
            var followUpCalls = 0;
            Assert.True(fixture.Host.TryRestoreQueuedLocalCommand(
                "image-preparation-command", fixture.Conversation.Id, CopilotAgentMode.Auto, null,
                run => preparation = Assert.IsAssignableFrom<Task<IReadOnlyList<CopilotAttachmentItem>?>>(
                    InvokeTask(fixture.ViewModel, "TryPersistImageAttachmentsAsync",
                        [fixture.OriginalUser.Attachments.ToArray(), imageLease, fixture.Conversation, run, null],
                        [typeof(IReadOnlyList<CopilotAttachmentItem>), typeof(CopilotManagedImageAttachmentLease),
                            typeof(CopilotConversationRecord), typeof(CopilotHostedAgentRun), typeof(Func<bool>)])),
                out var owner));
            Assert.True(fixture.Host.TryRestoreQueuedFollowUp(
                "later-image-preparation-follow-up", fixture.Conversation.Id, CopilotAgentMode.Auto, null,
                _ =>
                {
                    Interlocked.Increment(ref followUpCalls);
                    return Task.CompletedTask;
                },
                out var followUp));
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                Assert.True(fixture.Host.TryStartQueuedRun(owner!.Id));
                Assert.True(context.WaitForCallback(TestTimeout));
                Assert.NotNull(preparation);
                Assert.False(preparation.IsCompleted);
                Assert.Same(owner, fixture.Host.ActiveRun);
                Assert.True(owner.IsQueuedLocalCommand);

                fixture.ViewModel.PrimaryActionCommand.Execute(null);

                Assert.True(owner.CancellationToken.IsCancellationRequested);
                Assert.Equal(CopilotHostedRunState.CancelRequested, owner.State);
                context.Complete(preparation);
                Assert.Null(preparation.GetAwaiter().GetResult());
                context.Complete(owner.Completion);
                Assert.ThrowsAny<OperationCanceledException>(() => owner.Completion.GetAwaiter().GetResult());
                Assert.Equal(0, Volatile.Read(ref followUpCalls));
                Assert.Same(followUp, Assert.Single(fixture.Host.QueuedRuns));
                Assert.False(followUp!.HasStarted);
                Assert.Empty(fixture.Runtime.Requests);
            }
            finally
            {
                fixture.Host.Shutdown();
                if (preparation is { IsCompleted: false })
                    context.Complete(preparation);
                if (owner is { Completion.IsCompleted: false })
                    context.Complete(owner.Completion);
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }, TimeSpan.FromSeconds(40), "The queued command image preparation cancellation test did not finish.");
    }

    [Fact]
    public void EditingLatestMessageClosesHistorySearchAndSubmitsTheEditedComposer()
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            Assert.Empty(fixture.ViewModel.InputText);
            Assert.Empty(fixture.Conversation.Attachments);
            Assert.True(fixture.ViewModel.TryOpenPromptHistorySearch());
            Assert.True(fixture.ViewModel.EditMessageCommand.CanExecute(fixture.OriginalUser));

            fixture.ViewModel.EditMessageCommand.Execute(fixture.OriginalUser);

            Assert.False(fixture.ViewModel.IsPromptHistorySearchOpen);
            Assert.True(fixture.ViewModel.IsEditingMessage);
            Assert.Equal(OriginalPrompt, fixture.ViewModel.InputText);
            Assert.Equal(OriginalPrompt, fixture.Conversation.DraftText);
            var originalAttachment = Assert.Single(fixture.OriginalUser.Attachments);
            var restoredAttachment = Assert.Single(fixture.Conversation.Attachments);
            Assert.NotSame(originalAttachment, restoredAttachment);
            Assert.Equal(originalAttachment.Type, restoredAttachment.Type);
            Assert.Equal(originalAttachment.Value, restoredAttachment.Value);

            fixture.ViewModel.InputText = NewerText;
            fixture.ViewModel.DismissPromptHistorySearch();
            Assert.Equal(NewerText, fixture.ViewModel.InputText);
            Assert.Equal(NewerText, fixture.Conversation.DraftText);
            InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes)
                .WaitAsync(TestTimeout).GetAwaiter().GetResult();

            var request = Assert.Single(fixture.Runtime.Requests);
            Assert.Equal(NewerText, request.UserText);
            Assert.Equal(fixture.Conversation.Id, request.ConversationId);
            Assert.Equal(originalAttachment.Type, Assert.Single(request.HostContext.Attachments).Type);
            Assert.Equal(2, fixture.Conversation.Messages.Count);
            Assert.Equal(NewerText, fixture.Conversation.Messages[0].Content);
            Assert.NotSame(fixture.OriginalUser, fixture.Conversation.Messages[0]);
            Assert.NotSame(fixture.OriginalAssistant, fixture.Conversation.Messages[1]);
            Assert.False(fixture.ViewModel.IsEditingMessage);
        }, TimeSpan.FromSeconds(30), "The history-search message edit test did not finish.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedOrdinarySendDuringImageAdmissionIsScheduledOnlyOnce(bool secondCompletesFirst)
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            using var firstContext = new CopilotPausedAdmissionContext(TestTimeout, "The first ordinary send did not finish.");
            using var secondContext = new CopilotPausedAdmissionContext(TestTimeout, "The repeated ordinary send did not finish.");
            var contexts = new[] { firstContext, secondContext };
            var sends = new Task?[2];
            Task? resend = null;
            var previousContext = SynchronizationContext.Current;
            try
            {
                const string prompt = "Inspect this image once for the submitted draft.";
                var originalImage = Assert.Single(fixture.OriginalUser.Attachments);
                fixture.Conversation.Attachments.Add(originalImage.CreateSnapshot());
                fixture.ViewModel.InputText = prompt;
                for (var index = 0; index < sends.Length; index++)
                {
                    SynchronizationContext.SetSynchronizationContext(contexts[index]);
                    var send = InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes);
                    sends[index] = send;
                    // A duplicate may be rejected before image admission once this is fixed.
                    if (!send.IsCompleted)
                        Assert.True(contexts[index].WaitForCallback(TestTimeout));
                }
                Assert.False(sends[0]!.IsCompleted);
                Assert.Empty(fixture.Runtime.Requests);

                var completionOrder = secondCompletesFirst ? new[] { 1, 0 } : new[] { 0, 1 };
                foreach (var index in completionOrder)
                {
                    SynchronizationContext.SetSynchronizationContext(contexts[index]);
                    contexts[index].Complete(sends[index]!);
                    sends[index]!.GetAwaiter().GetResult();
                }

                var request = Assert.Single(fixture.Runtime.Requests);
                Assert.Equal(prompt, request.UserText);
                Assert.Equal(fixture.Conversation.Id, request.ConversationId);
                Assert.Single(request.HostContext.Attachments);
                Assert.Equal(4, fixture.Conversation.Messages.Count);
                Assert.Same(fixture.OriginalUser, fixture.Conversation.Messages[0]);
                Assert.Same(fixture.OriginalAssistant, fixture.Conversation.Messages[1]);
                Assert.Equal(prompt, fixture.Conversation.Messages[2].Content);
                Assert.Empty(fixture.ViewModel.InputText);
                Assert.Empty(fixture.Conversation.Attachments);

                // Writing the same text again is a new submission, not the old suspended send.
                fixture.ViewModel.InputText = prompt;
                fixture.Conversation.Attachments.Add(originalImage.CreateSnapshot());
                SynchronizationContext.SetSynchronizationContext(firstContext);
                resend = InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes);
                firstContext.Complete(resend);
                resend.GetAwaiter().GetResult();
                Assert.Equal(2, fixture.Runtime.Requests.Count);
                Assert.All(fixture.Runtime.Requests, item => Assert.Equal(prompt, item.UserText));
                Assert.Equal(6, fixture.Conversation.Messages.Count);
                Assert.Equal(prompt, fixture.Conversation.Messages[4].Content);
            }
            finally
            {
                try
                {
                    for (var index = 0; index < sends.Length; index++)
                    {
                        if (sends[index] is not { IsCompleted: false } pending)
                            continue;
                        SynchronizationContext.SetSynchronizationContext(contexts[index]);
                        contexts[index].Complete(pending);
                    }
                    if (resend is { IsCompleted: false })
                    {
                        SynchronizationContext.SetSynchronizationContext(firstContext);
                        firstContext.Complete(resend);
                    }
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
            }
        }, TimeSpan.FromSeconds(40), "The repeated-send admission test did not finish.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedContextAttachmentDuringImageAdmissionAllowsAnotherSubmission(bool replaceExistingContext)
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            using var firstContext = new CopilotPausedAdmissionContext(TestTimeout, "The original attachment submission did not finish.");
            using var secondContext = new CopilotPausedAdmissionContext(TestTimeout, "The changed attachment submission did not finish.");
            var contexts = new[] { firstContext, secondContext };
            var sends = new Task?[2];
            var previousContext = SynchronizationContext.Current;
            try
            {
                const string prompt = "Inspect the image with the attached context.";
                const string originalContext = "Context captured by the first submission.";
                const string changedContext = "Context captured by the second submission.";
                fixture.Conversation.Attachments.Add(Assert.Single(fixture.OriginalUser.Attachments).CreateSnapshot());
                if (replaceExistingContext)
                    fixture.Conversation.Attachments.Add(CopilotAttachmentItem.CreateContext(originalContext));
                fixture.ViewModel.InputText = prompt;
                SynchronizationContext.SetSynchronizationContext(firstContext);
                sends[0] = InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes);
                Assert.True(firstContext.WaitForCallback(TestTimeout));
                Assert.False(sends[0]!.IsCompleted);

                var attachment = CopilotAttachmentItem.CreateContext(changedContext);
                if (replaceExistingContext)
                    fixture.Conversation.Attachments[1] = attachment;
                else
                    fixture.Conversation.Attachments.Add(attachment);
                // Leave the composer text untouched: only the attachment snapshot changes.
                Assert.Equal(prompt, fixture.ViewModel.InputText);
                SynchronizationContext.SetSynchronizationContext(secondContext);
                sends[1] = InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes);
                if (!sends[1]!.IsCompleted)
                    Assert.True(secondContext.WaitForCallback(TestTimeout));

                for (var index = 0; index < sends.Length; index++)
                {
                    SynchronizationContext.SetSynchronizationContext(contexts[index]);
                    contexts[index].Complete(sends[index]!);
                    sends[index]!.GetAwaiter().GetResult();
                }

                var requests = fixture.Runtime.Requests.ToArray();
                Assert.Equal(2, requests.Length);
                Assert.All(requests, request => Assert.Equal(prompt, request.UserText));
                var firstAttachments = requests[0].HostContext.Attachments;
                var secondAttachments = requests[1].HostContext.Attachments;
                Assert.Equal(replaceExistingContext ? 2 : 1, firstAttachments.Count);
                Assert.Single(firstAttachments, item => item.Type == CopilotAttachmentType.Image);
                if (replaceExistingContext)
                    Assert.Equal(originalContext, Assert.Single(firstAttachments, item => item.Type == CopilotAttachmentType.Context).Value);
                else
                    Assert.DoesNotContain(firstAttachments, item => item.Type == CopilotAttachmentType.Context);
                Assert.Equal(2, secondAttachments.Count);
                Assert.Single(secondAttachments, item => item.Type == CopilotAttachmentType.Image);
                Assert.Equal(changedContext, Assert.Single(secondAttachments, item => item.Type == CopilotAttachmentType.Context).Value);
                Assert.Equal(6, fixture.Conversation.Messages.Count);
            }
            finally
            {
                try
                {
                    for (var index = 0; index < sends.Length; index++)
                    {
                        if (sends[index] is not { IsCompleted: false } pending)
                            continue;
                        SynchronizationContext.SetSynchronizationContext(contexts[index]);
                        contexts[index].Complete(pending);
                    }
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
            }
        }, TimeSpan.FromSeconds(40), "The changed-attachment admission test did not finish.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedImageAdmissionDoesNotOverwriteNewerLocalCommandFeedback(bool switchConversation)
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            using var context = new CopilotPausedAdmissionContext(TestTimeout, "The failed image admission did not drain.");
            var previousContext = SynchronizationContext.Current;
            Task? send = null;
            try
            {
                const string prompt = "Inspect this image before the newer local command.";
                const string newerDraft = "A newer draft after viewing local command help.";
                fixture.ViewModel.InputText = "/help";
                InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes).GetAwaiter().GetResult();
                var image = Assert.Single(fixture.OriginalUser.Attachments).CreateSnapshot();
                fixture.Conversation.Attachments.Add(image);
                fixture.ViewModel.InputText = prompt;
                File.WriteAllText(fixture.StoragePath, "This file blocks the fixture's attachment directory.");

                SynchronizationContext.SetSynchronizationContext(context);
                send = InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes);
                Assert.True(context.WaitForCallback(TestTimeout));
                Assert.False(send.IsCompleted);
                Assert.False(fixture.ViewModel.IsBusy);
                if (switchConversation)
                    Assert.True(fixture.ViewModel.TrySelectConversation(fixture.OtherConversation.Id));
                var feedbackOwner = fixture.ViewModel.SelectedConversation;

                // Repeating the same help still owns newer feedback, even when its text is unchanged.
                fixture.ViewModel.InputText = "/help";
                InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes).GetAwaiter().GetResult();
                var newerTitle = fixture.ViewModel.LocalCommandResultTitle;
                var newerText = fixture.ViewModel.LocalCommandResultText;
                Assert.StartsWith("/help", newerTitle, StringComparison.Ordinal);
                Assert.NotEmpty(newerText);
                fixture.ViewModel.InputText = newerDraft;

                context.Complete(send);
                send.GetAwaiter().GetResult();

                Assert.Equal(newerTitle, fixture.ViewModel.LocalCommandResultTitle);
                Assert.Equal(newerText, fixture.ViewModel.LocalCommandResultText);
                Assert.Same(feedbackOwner, fixture.ViewModel.SelectedConversation);
                Assert.Equal(newerDraft, fixture.ViewModel.InputText);
                Assert.Equal(switchConversation ? prompt : newerDraft, fixture.Conversation.DraftText);
                Assert.Same(image, Assert.Single(fixture.Conversation.Attachments));
                Assert.Equal([fixture.OriginalUser, fixture.OriginalAssistant], fixture.Conversation.Messages.ToArray());
                Assert.Empty(fixture.Runtime.Requests);
                Assert.Empty(fixture.Host.ScheduledRuns);
            }
            finally
            {
                try
                {
                    SynchronizationContext.SetSynchronizationContext(context);
                    if (send is { IsCompleted: false })
                        context.Complete(send);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
            }
        }, TimeSpan.FromSeconds(40), "The stale image admission feedback test did not finish.");
    }

    [Fact]
    public void FailedImageAdmissionAllowsTheUnchangedDraftAndAttachmentToBeSentAgain()
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            using var context = new CopilotPausedAdmissionContext(TestTimeout, "The storage-failure admission test did not finish.");
            var previousContext = SynchronizationContext.Current;
            Task? send = null;
            try
            {
                const string prompt = "Retry this draft after attachment storage is repaired.";
                var image = Assert.Single(fixture.OriginalUser.Attachments).CreateSnapshot();
                fixture.Conversation.Attachments.Add(image);
                fixture.ViewModel.InputText = prompt;
                File.WriteAllText(fixture.StoragePath, "This file blocks the fixture's attachment directory.");

                SynchronizationContext.SetSynchronizationContext(context);
                send = InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes);
                context.Complete(send);
                send.GetAwaiter().GetResult();

                Assert.Empty(fixture.Runtime.Requests);
                Assert.Equal(2, fixture.Conversation.Messages.Count);
                Assert.Equal(prompt, fixture.ViewModel.InputText);
                Assert.Same(image, Assert.Single(fixture.Conversation.Attachments));
                Assert.Equal("图片保存失败", fixture.ViewModel.LocalCommandResultTitle);
                Assert.True(File.Exists(fixture.StoragePath));

                // Remove only this fixture's blocker; do not recreate the draft or attachment.
                File.Delete(fixture.StoragePath);
                send = InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes);
                context.Complete(send);
                send.GetAwaiter().GetResult();

                var request = Assert.Single(fixture.Runtime.Requests);
                Assert.Equal(prompt, request.UserText);
                Assert.Single(request.HostContext.Attachments);
                Assert.Equal(4, fixture.Conversation.Messages.Count);
                Assert.Empty(fixture.ViewModel.InputText);
                Assert.Empty(fixture.Conversation.Attachments);
            }
            finally
            {
                try
                {
                    SynchronizationContext.SetSynchronizationContext(context);
                    if (send is { IsCompleted: false })
                        context.Complete(send);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
            }
        }, TimeSpan.FromSeconds(40), "The failed-admission retry test did not finish.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedEditedImageAdmissionReportsOnlyToItsOriginalEditLifetime(bool cancelEdit)
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            using var context = new CopilotPausedAdmissionContext(TestTimeout, "The edited image failure did not drain.");
            var previousContext = SynchronizationContext.Current;
            Task? send = null;
            try
            {
                Assert.True(fixture.ViewModel.EditMessageCommand.CanExecute(fixture.OriginalUser));
                fixture.ViewModel.EditMessageCommand.Execute(fixture.OriginalUser);
                Assert.True(fixture.ViewModel.IsEditingMessage);
                fixture.ViewModel.InputText = SubmittedEdit;
                File.WriteAllText(fixture.StoragePath, "This file blocks the fixture's attachment directory.");

                SynchronizationContext.SetSynchronizationContext(context);
                send = InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes);
                Assert.True(context.WaitForCallback(TestTimeout));
                Assert.False(send.IsCompleted);
                Assert.False(fixture.ViewModel.IsBusy);
                SynchronizationContext.SetSynchronizationContext(previousContext);
                if (cancelEdit)
                {
                    Assert.True(fixture.ViewModel.CancelMessageEditCommand.CanExecute(null));
                    fixture.ViewModel.CancelMessageEditCommand.Execute(null);
                    Assert.False(fixture.ViewModel.IsEditingMessage);
                }
                fixture.ViewModel.InputText = NewerText;
                var retainedAttachments = fixture.Conversation.Attachments.ToArray();
                var retainedTitle = fixture.ViewModel.LocalCommandResultTitle;
                var retainedText = fixture.ViewModel.LocalCommandResultText;

                SynchronizationContext.SetSynchronizationContext(context);
                context.Complete(send);
                send.GetAwaiter().GetResult();

                Assert.Equal(!cancelEdit, fixture.ViewModel.IsEditingMessage);
                Assert.Equal(NewerText, fixture.ViewModel.InputText);
                Assert.Equal(NewerText, fixture.Conversation.DraftText);
                Assert.Equal(retainedAttachments, fixture.Conversation.Attachments.ToArray());
                Assert.Equal([fixture.OriginalUser, fixture.OriginalAssistant], fixture.Conversation.Messages.ToArray());
                Assert.Empty(fixture.Runtime.Requests);
                Assert.Empty(fixture.Host.ScheduledRuns);
                if (cancelEdit)
                {
                    Assert.Equal(retainedTitle, fixture.ViewModel.LocalCommandResultTitle);
                    Assert.Equal(retainedText, fixture.ViewModel.LocalCommandResultText);
                }
                else
                {
                    Assert.Equal("图片保存失败", fixture.ViewModel.LocalCommandResultTitle);
                    Assert.NotEmpty(fixture.ViewModel.LocalCommandResultText);
                }
            }
            finally
            {
                try
                {
                    SynchronizationContext.SetSynchronizationContext(context);
                    if (send is { IsCompleted: false })
                        context.Complete(send);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
            }
        }, TimeSpan.FromSeconds(40), "The edited image failure feedback test did not finish.");
    }

    [Theory]
    [InlineData("same-conversation")]
    [InlineData("other-conversation")]
    public void OrdinarySendDoesNotInvalidateAnEditStartedDuringImageAdmission(string transition)
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            using var context = new CopilotPausedAdmissionContext(
                TestTimeout,
                "The ordinary send did not finish after image admission.");
            var previousContext = SynchronizationContext.Current;
            Task? send = null;
            try
            {
                const string ordinaryPrompt = "Send this new request with the image.";
                fixture.Conversation.Attachments.Add(Assert.Single(fixture.OriginalUser.Attachments).CreateSnapshot());
                fixture.ViewModel.InputText = ordinaryPrompt;
                SynchronizationContext.SetSynchronizationContext(context);
                send = InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes);
                Assert.True(context.WaitForCallback(TestTimeout));
                Assert.False(send.IsCompleted);
                Assert.False(fixture.ViewModel.IsBusy);
                Assert.Empty(fixture.Runtime.Requests);

                SynchronizationContext.SetSynchronizationContext(previousContext);
                Assert.True(fixture.ViewModel.TryToggleComposerStash(ordinaryPrompt.Length, out _));
                Assert.Empty(fixture.ViewModel.InputText);
                Assert.Empty(fixture.Conversation.Attachments);
                var editOtherConversation = transition == "other-conversation";
                if (editOtherConversation)
                    Assert.True(fixture.ViewModel.TrySelectConversation(fixture.OtherConversation.Id));
                var editedConversation = editOtherConversation ? fixture.OtherConversation : fixture.Conversation;
                var editedUser = editOtherConversation ? fixture.OtherUser : fixture.OriginalUser;
                Assert.True(fixture.ViewModel.EditMessageCommand.CanExecute(editedUser));
                fixture.ViewModel.EditMessageCommand.Execute(editedUser);
                fixture.ViewModel.InputText = NewerText;
                Assert.True(fixture.ViewModel.IsEditingMessage);
                var editAttachments = editedConversation.Attachments.ToArray();
                var editedMessages = editedConversation.Messages.ToArray();

                SynchronizationContext.SetSynchronizationContext(context);
                context.Complete(send);
                send.GetAwaiter().GetResult();

                if (editOtherConversation)
                {
                    var request = Assert.Single(fixture.Runtime.Requests);
                    Assert.Equal(ordinaryPrompt, request.UserText);
                    Assert.Equal(fixture.Conversation.Id, request.ConversationId);
                }
                else
                {
                    Assert.Empty(fixture.Runtime.Requests);
                }
                Assert.Equal(editedMessages, editedConversation.Messages.ToArray());
                Assert.Same(editedConversation, fixture.ViewModel.SelectedConversation);
                Assert.True(fixture.ViewModel.IsEditingMessage);
                Assert.Equal(NewerText, fixture.ViewModel.InputText);
                Assert.Equal(editAttachments, editedConversation.Attachments.ToArray());
                Assert.Equal(ordinaryPrompt, fixture.Conversation.ComposerStash?.Text);

                // The newer edit must remain a valid replacement, not be discarded
                // by CancelMessageEdit because the older send appended another turn.
                SynchronizationContext.SetSynchronizationContext(previousContext);
                InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes)
                    .WaitAsync(TestTimeout).GetAwaiter().GetResult();
                Assert.Equal(editOtherConversation ? 2 : 1, fixture.Runtime.Requests.Count);
                var editRequest = fixture.Runtime.Requests.Last();
                Assert.Equal(NewerText, editRequest.UserText);
                Assert.Equal(editedConversation.Id, editRequest.ConversationId);
                Assert.Equal(NewerText, editedConversation.Messages[0].Content);
                Assert.Equal(2, editedConversation.Messages.Count);
                Assert.False(fixture.ViewModel.IsEditingMessage);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
                if (send is { IsCompleted: false })
                    context.Complete(send);
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }, TimeSpan.FromSeconds(40), "The ordinary-send edit admission test did not finish.");
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("reopen")]
    [InlineData("switch-edit-other")]
    [InlineData("replace")]
    [InlineData("unchanged")]
    [InlineData("newer-draft")]
    public void EditedSendRevalidatesItsEditSessionAfterImageAdmission(string transition)
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            using var context = new CopilotPausedAdmissionContext(
                TestTimeout,
                "The edited send did not finish after image admission.");
            var previousContext = SynchronizationContext.Current;
            Task? send = null;
            try
            {
                Assert.True(fixture.ViewModel.EditMessageCommand.CanExecute(fixture.OriginalUser));
                fixture.ViewModel.EditMessageCommand.Execute(fixture.OriginalUser);
                Assert.True(fixture.ViewModel.IsEditingMessage);
                fixture.ViewModel.InputText = SubmittedEdit;
                SynchronizationContext.SetSynchronizationContext(context);
                send = InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes);
                Assert.True(context.WaitForCallback(TestTimeout));
                Assert.False(send.IsCompleted);
                Assert.False(fixture.ViewModel.IsBusy);
                Assert.Single(Directory.GetFiles(fixture.StoragePath, "image-*.png"));
                Assert.Empty(fixture.Runtime.Requests);

                // Only the first image-admission continuation is suspended. Later
                // user actions still enter through the actual VM command methods.
                SynchronizationContext.SetSynchronizationContext(previousContext);
                if (transition is "cancel" or "reopen")
                {
                    Assert.True(fixture.ViewModel.CancelMessageEditCommand.CanExecute(null));
                    fixture.ViewModel.CancelMessageEditCommand.Execute(null);
                    Assert.False(fixture.ViewModel.IsEditingMessage);
                    if (transition == "reopen")
                    {
                        fixture.ViewModel.EditMessageCommand.Execute(fixture.OriginalUser);
                        Assert.True(fixture.ViewModel.IsEditingMessage);
                    }
                    fixture.ViewModel.InputText = NewerText;
                }
                else if (transition == "switch-edit-other")
                {
                    Assert.True(fixture.ViewModel.TrySelectConversation(fixture.OtherConversation.Id));
                    Assert.False(fixture.ViewModel.IsEditingMessage);
                    Assert.True(fixture.ViewModel.EditMessageCommand.CanExecute(fixture.OtherUser));
                    fixture.ViewModel.EditMessageCommand.Execute(fixture.OtherUser);
                    fixture.ViewModel.InputText = NewerText;
                    Assert.True(fixture.ViewModel.IsEditingMessage);
                }
                else if (transition == "replace")
                {
                    fixture.ViewModel.InputText = NewerText;
                    var image = Assert.Single(fixture.Conversation.Attachments);
                    InvokeTask(fixture.ViewModel, "RemoveAttachment", [image], [typeof(CopilotAttachmentItem)])
                        .WaitAsync(TestTimeout).GetAwaiter().GetResult();
                    InvokeTask(fixture.ViewModel, "SendAsync", [], Type.EmptyTypes)
                        .WaitAsync(TestTimeout).GetAwaiter().GetResult();
                    Assert.Equal(NewerText, Assert.Single(fixture.Runtime.Requests).UserText);
                    Assert.DoesNotContain(fixture.OriginalUser, fixture.Conversation.Messages);
                    Assert.False(fixture.ViewModel.IsEditingMessage);
                    Assert.NotNull(fixture.Conversation.AgentSessionCheckpoint);
                }
                else if (transition == "newer-draft")
                {
                    fixture.ViewModel.InputText = NewerText;
                    fixture.Conversation.Attachments.Add(CopilotAttachmentItem.CreateContext("Newer draft attachment"));
                }

                var originalMessagesBeforeResume = fixture.Conversation.Messages.ToArray();
                var otherMessagesBeforeResume = fixture.OtherConversation.Messages.ToArray();
                var checkpointBeforeResume = fixture.Conversation.AgentSessionCheckpoint;
                var draftBeforeResume = fixture.ViewModel.InputText;
                var attachmentsBeforeResume = fixture.ViewModel.SelectedConversation!.Attachments.ToArray();
                SynchronizationContext.SetSynchronizationContext(context);
                context.Complete(send);
                send.GetAwaiter().GetResult();

                if (transition is "unchanged" or "newer-draft")
                {
                    var request = Assert.Single(fixture.Runtime.Requests);
                    Assert.Equal(SubmittedEdit, request.UserText);
                    Assert.Equal(fixture.Conversation.Id, request.ConversationId);
                    Assert.Single(request.HostContext.Attachments);
                    Assert.Equal(2, fixture.Conversation.Messages.Count);
                    Assert.Equal(SubmittedEdit, fixture.Conversation.Messages[0].Content);
                    Assert.NotSame(fixture.OriginalUser, fixture.Conversation.Messages[0]);
                    Assert.NotSame(fixture.OriginalAssistant, fixture.Conversation.Messages[1]);
                    if (transition == "unchanged")
                    {
                        Assert.Empty(fixture.ViewModel.InputText);
                        Assert.Empty(fixture.Conversation.Attachments);
                    }
                    else
                    {
                        Assert.Equal(NewerText, fixture.ViewModel.InputText);
                        Assert.Contains(fixture.Conversation.Attachments, item => item.Value == "Newer draft attachment");
                    }
                }
                else
                {
                    Assert.Equal(transition == "replace" ? 1 : 0, fixture.Runtime.Requests.Count);
                    Assert.DoesNotContain(fixture.Runtime.Requests, request => request.UserText == SubmittedEdit);
                    Assert.Equal(originalMessagesBeforeResume, fixture.Conversation.Messages.ToArray());
                    Assert.True(CopilotAgentSessionCheckpoint.AreEquivalent(checkpointBeforeResume, fixture.Conversation.AgentSessionCheckpoint));
                    Assert.Equal(draftBeforeResume, fixture.ViewModel.InputText);
                    Assert.Equal(attachmentsBeforeResume, fixture.ViewModel.SelectedConversation!.Attachments.ToArray());
                    Assert.Equal(transition is "reopen" or "switch-edit-other", fixture.ViewModel.IsEditingMessage);
                }
                Assert.Equal(otherMessagesBeforeResume, fixture.OtherConversation.Messages.ToArray());
                if (transition == "switch-edit-other")
                    Assert.Same(fixture.OtherConversation, fixture.ViewModel.SelectedConversation);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
                if (send is { IsCompleted: false })
                    context.Complete(send);
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }, TimeSpan.FromSeconds(40), "The edit admission test did not finish.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FileAttachmentLimitFeedbackPreservesNewerLocalCommandFeedback(bool showNewerFeedback)
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            using var context = new CopilotPausedAdmissionContext(TestTimeout, "The file attachment operation did not drain.");
            var previousContext = SynchronizationContext.Current;
            Task<int>? addFiles = null;
            var filePaths = Enumerable.Range(0, CopilotComposerAttachmentService.MaximumAttachmentCount + 1)
                .Select(index => Path.Combine(Path.GetDirectoryName(fixture.StoragePath)!, $"attachment-{index}.txt"))
                .ToArray();
            foreach (var filePath in filePaths)
                File.WriteAllText(filePath, "A file from the over-capacity attachment batch.");

            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                addFiles = fixture.ViewModel.AddFileAttachmentsAsync(filePaths);
                Assert.True(context.WaitForCallback(TestTimeout));
                Assert.False(addFiles.IsCompleted);
                Assert.True(fixture.ViewModel.IsBusy);
                Assert.False(fixture.ViewModel.CanSwitchConversation);

                string? newerTitle = null;
                string? newerText = null;
                if (showNewerFeedback)
                {
                    fixture.ViewModel.InputText = "/help";
                    Assert.True(fixture.ViewModel.SendCommand.CanExecute(null));
                    fixture.ViewModel.SendCommand.Execute(null);
                    newerTitle = fixture.ViewModel.LocalCommandResultTitle;
                    newerText = fixture.ViewModel.LocalCommandResultText;
                    Assert.StartsWith("/help", newerTitle, StringComparison.Ordinal);
                }

                const string newerDraft = "The draft written while the earlier file batch is resolving.";
                fixture.ViewModel.InputText = newerDraft;
                context.Complete(addFiles);
                Assert.Equal(CopilotComposerAttachmentService.MaximumAttachmentCount, addFiles.GetAwaiter().GetResult());
                Assert.Equal(filePaths.Take(CopilotComposerAttachmentService.MaximumAttachmentCount),
                    fixture.Conversation.Attachments.Select(attachment => attachment.Value));
                Assert.Equal(newerDraft, fixture.ViewModel.InputText);
                Assert.Equal([fixture.OriginalUser, fixture.OriginalAssistant], fixture.Conversation.Messages.ToArray());
                Assert.Empty(fixture.Runtime.Requests);
                Assert.False(fixture.ViewModel.IsBusy);
                if (showNewerFeedback)
                {
                    Assert.Equal(newerTitle, fixture.ViewModel.LocalCommandResultTitle);
                    Assert.Equal(newerText, fixture.ViewModel.LocalCommandResultText);
                }
                else
                {
                    Assert.Equal("部分文件未添加", fixture.ViewModel.LocalCommandResultTitle);
                    Assert.NotEmpty(fixture.ViewModel.LocalCommandResultText);
                }
            }
            finally
            {
                try
                {
                    SynchronizationContext.SetSynchronizationContext(context);
                    if (addFiles is { IsCompleted: false })
                        context.Complete(addFiles);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
            }
        }, TimeSpan.FromSeconds(40), "The file attachment feedback test did not finish.");
    }

    [Theory]
    [InlineData("newer-feedback", true)]
    [InlineData("newer-feedback", false)]
    [InlineData("switch-conversation", true)]
    [InlineData("unchanged", true)]
    [InlineData("unchanged", false)]
    public void ClipboardExportPreservesNewerFeedbackWithoutAbandoningTheCopy(string transition, bool copySucceeds)
    {
        StaTest.Run(() =>
        {
            using var fixture = new Fixture();
            var command = Assert.IsType<CopilotLocalCommand>(CopilotLocalCommandCatalog.FindExact("/export"));
            var copiedText = string.Empty;
            var copyCalls = 0;
            string? retainedTitle = null;
            string? retainedText = null;
            CopilotConversationRecord? selectedAfterCopy = null;

            // The fake clipboard records the real generated export, then exercises actual UI commands
            // before the export publishes its completion. No system clipboard or dialog is involved.
            Func<string, (bool Succeeded, string ErrorMessage)> writeClipboard = text =>
            {
                copiedText = text;
                copyCalls++;
                Assert.False(fixture.ViewModel.ExportConversationCommand.CanExecute(fixture.Conversation));
                Assert.False(fixture.ViewModel.IsBusy);
                Assert.True(fixture.ViewModel.CanSwitchConversation);
                if (transition == "switch-conversation")
                    Assert.True(fixture.ViewModel.TrySelectConversation(fixture.OtherConversation.Id));
                if (transition == "newer-feedback")
                {
                    fixture.ViewModel.InputText = "/help";
                    Assert.True(fixture.ViewModel.SendCommand.CanExecute(null));
                    fixture.ViewModel.SendCommand.Execute(null);
                    Assert.StartsWith("/help", fixture.ViewModel.LocalCommandResultTitle, StringComparison.Ordinal);
                }
                retainedTitle = fixture.ViewModel.LocalCommandResultTitle;
                retainedText = fixture.ViewModel.LocalCommandResultText;
                selectedAfterCopy = fixture.ViewModel.SelectedConversation;
                return (copySucceeds, copySucceeds ? string.Empty : "The fixture clipboard rejected the copy.");
            };

            using var context = new CopilotPausedAdmissionContext(TestTimeout, "The clipboard export did not drain.");
            var previousContext = SynchronizationContext.Current;
            Task? export = null;
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                export = fixture.ViewModel.ExportConversationToClipboardAsync(fixture.Conversation, command, writeClipboard);
                context.Complete(export);
                export.GetAwaiter().GetResult();
            }
            finally
            {
                try
                {
                    SynchronizationContext.SetSynchronizationContext(context);
                    if (export is { IsCompleted: false })
                        context.Complete(export);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
            }

            Assert.Equal(1, copyCalls);
            Assert.Contains(OriginalPrompt, copiedText, StringComparison.Ordinal);
            Assert.Contains("Original completed answer.", copiedText, StringComparison.Ordinal);
            Assert.DoesNotContain("Other conversation request", copiedText, StringComparison.Ordinal);
            Assert.True(fixture.ViewModel.ExportConversationCommand.CanExecute(fixture.Conversation));
            Assert.Same(selectedAfterCopy, fixture.ViewModel.SelectedConversation);
            Assert.Empty(fixture.Runtime.Requests);
            Assert.Empty(fixture.Host.ScheduledRuns);
            if (transition != "unchanged")
            {
                Assert.Equal(retainedTitle, fixture.ViewModel.LocalCommandResultTitle);
                Assert.Equal(retainedText, fixture.ViewModel.LocalCommandResultText);
            }
            else
            {
                Assert.StartsWith("/export", fixture.ViewModel.LocalCommandResultTitle, StringComparison.Ordinal);
                Assert.Contains(copySucceeds ? "已复制" : "复制失败", fixture.ViewModel.LocalCommandResultText, StringComparison.Ordinal);
            }
        }, TimeSpan.FromSeconds(40), "The clipboard export feedback test did not finish.");
    }

    private sealed class Fixture : IDisposable
    {
        private static readonly FieldInfo SolutionInstance = typeof(SolutionManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previousSolution = SolutionInstance.GetValue(null);
        private readonly object _isolatedSolution = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly string _root = Directory.CreateTempSubdirectory("CopilotMessageEditAdmission-").FullName;

        public Fixture()
        {
            SolutionInstance.SetValue(null, _isolatedSolution);
            var source = Path.Combine(_root, "source.png");
            CreateImage(source);
            StoragePath = Path.Combine(_root, "managed");
            var profile = new CopilotProfileConfig
            {
                Id = "edit-admission-profile", Name = "Edit admission profile",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "edit-admission-test-key", BaseUrl = "https://example.test/v1",
                Model = "edit-admission-model", SupportsImageInput = true,
            };
            Conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            OriginalUser = new CopilotChatMessage(CopilotChatRole.User, OriginalPrompt)
            {
                RequestMode = CopilotAgentMode.Auto,
                AttachmentSnapshotCaptured = true,
                Attachments = [CopilotAttachmentItem.CreateImage(source)],
            };
            OriginalAssistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Original completed answer.")
            {
                RequestMode = CopilotAgentMode.Auto,
                AgentStopReason = CopilotAgentStopReason.Completed,
            };
            Conversation.Messages.Add(OriginalUser);
            Conversation.Messages.Add(OriginalAssistant);
            OtherConversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            OtherUser = new CopilotChatMessage(CopilotChatRole.User, "Other conversation request")
            {
                RequestMode = CopilotAgentMode.Auto,
                AttachmentSnapshotCaptured = true,
            };
            OtherConversation.Messages.Add(OtherUser);
            OtherConversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Other completed answer"));
            var state = new CopilotChatState
            {
                ActiveConversationId = Conversation.Id, ActiveProfileId = profile.Id,
                Conversations = [Conversation, OtherConversation],
            };
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "edit-admission-test-token", Profiles = [profile],
            };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStore(state, StoragePath), config, Runtime, Host);
        }

        public string StoragePath { get; }
        public CopilotConversationRecord Conversation { get; }
        public CopilotConversationRecord OtherConversation { get; }
        public CopilotChatMessage OriginalUser { get; }
        public CopilotChatMessage OriginalAssistant { get; }
        public CopilotChatMessage OtherUser { get; }
        public CopilotChatViewModel ViewModel { get; }
        public CopilotAgentTaskHost Host { get; } = new();
        public CompletingRuntime Runtime { get; } = new();

        public void Dispose()
        {
            Host.Shutdown();
            ViewModel.Dispose();
            if (ReferenceEquals(SolutionInstance.GetValue(null), _isolatedSolution))
                SolutionInstance.SetValue(null, _previousSolution);
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_root));
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            Assert.True(string.Equals(Path.GetDirectoryName(root), temp, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(root).StartsWith("CopilotMessageEditAdmission-", StringComparison.Ordinal));
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class CompletingRuntime : ICopilotTurnRuntime
    {
        public ConcurrentQueue<CopilotTurnRequest> Requests { get; } = new();
        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Enqueue(request);
            var ledger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                Items = [new CopilotAgentTaskItem { Id = 1, Title = "Continue edited work", Description = "Preserve the new recovery point." }],
            };
            var journal = new CopilotAgentTaskEventJournalBuilder(request.TaskEventJournalBaseline);
            journal.RecordRunStarted();
            journal.RecordTaskLedger(ledger, "edit-admission-test");
            journal.RecordStop(CopilotAgentStopReason.Paused);
            var checkpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(CopilotAgentSessionCheckpoint.Create(request.Profile, "{}",
                CopilotCapabilityCatalog.Shared.GetSnapshot(), taskEventJournal: journal.Snapshot()));
            var result = new CopilotAgentRunResult
            {
                PreparedUserMessageContent = request.UserText, TaskLedger = ledger,
                StopReason = CopilotAgentStopReason.Paused, TaskEventJournal = journal.Snapshot(), SessionCheckpoint = checkpoint,
            };
            await Task.CompletedTask;
            yield return new CopilotTurnStartedEvent(request.TaskId, request.Mode);
            yield return new CopilotTurnPlanUpdatedEvent(CopilotTurnPlanSnapshot.FromTaskLedger(ledger));
            yield return new CopilotTurnAgentEvent(CopilotAgentEvent.CheckpointUpdated(checkpoint, ledger));
            yield return new CopilotTurnAgentEvent(CopilotAgentEvent.CheckpointReady());
            yield return new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("The edited task paused with recoverable work."));
            yield return new CopilotTurnAgentEvent(CopilotAgentEvent.Completed());
            yield return CopilotTurnCompletedEvent.Completed(request.TaskId, CopilotTurnResult.FromAgent(request.Mode, CopilotTokenUsage.Empty, result));
        }
        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class MemoryStore(CopilotChatState state, string path) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => path;
        public CopilotChatState Load() => state;
        public void Save(CopilotChatState value) { }
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());
        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";
        public string Serialize(CopilotChatState value) => "{}";
        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private static Task InvokeTask(CopilotChatViewModel viewModel, string methodName, object?[] arguments, Type[] parameterTypes) =>
        Assert.IsAssignableFrom<Task>(typeof(CopilotChatViewModel).GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic, parameterTypes)!.Invoke(viewModel, arguments));

    private static void CreateImage(string path)
    {
        const int dimension = 1_024;
        var pixels = new byte[dimension * dimension * 4];
        new Random(42).NextBytes(pixels);
        var bitmap = BitmapSource.Create(dimension, dimension, 96, 96, PixelFormats.Bgra32, null, pixels, dimension * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        Assert.InRange(stream.Length, 1, CopilotImagePayloadLoader.MaximumImageBytes);
    }

}

internal sealed class CopilotPausedAdmissionContext(
    TimeSpan timeout,
    string timeoutMessage) : SynchronizationContext, IDisposable
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
    private readonly AutoResetEvent _posted = new(false);

    public override void Post(SendOrPostCallback callback, object? state)
    {
        _callbacks.Enqueue((callback, state));
        _posted.Set();
    }

    public bool WaitForCallback(TimeSpan waitTimeout) => _posted.WaitOne(waitTimeout);

    public void Complete(Task operation)
    {
        var deadline = DateTime.UtcNow + timeout;
        var waits = new[] { _posted, ((IAsyncResult)operation).AsyncWaitHandle };
        while (!operation.IsCompleted)
        {
            if (_callbacks.TryDequeue(out var callback))
            {
                callback.Callback(callback.State);
                continue;
            }
            var remaining = deadline - DateTime.UtcNow;
            Assert.True(remaining > TimeSpan.Zero, timeoutMessage);
            Assert.NotEqual(WaitHandle.WaitTimeout, WaitHandle.WaitAny(waits, remaining));
        }
    }

    public void Dispose() => _posted.Dispose();
}
