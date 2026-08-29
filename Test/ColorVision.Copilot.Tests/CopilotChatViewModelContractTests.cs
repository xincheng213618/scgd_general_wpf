using System.Reflection;
using System.Windows.Input;
using ColorVision.Copilot;
using ColorVision.UI.Menus;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotChatViewModelContractTests
{
    private const BindingFlags InstanceMembers =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly string[] ExpectedCommandProperties =
    [
        "AcceptPromptHistoryPrefixCompletionCommand",
        "AddContextAttachmentCommand",
        "AddFileAttachmentCommand",
        "AddWebPageAttachmentCommand",
        "AnswerUserQuestionOptionCommand",
        "ApprovePendingActionCommand",
        "AttachActiveDocumentCommand",
        "AttachCurrentLiveContextCommand",
        "BranchConversationCommand",
        "CancelMessageEditCommand",
        "ClearConversationGoalCommand",
        "ClearConversationSearchCommand",
        "CloseConversationFindCommand",
        "CompactConversationCommand",
        "CompleteLocalCommandCommand",
        "ContinueAgentTasksCommand",
        "ContinuePlanningCommand",
        "CopyLatestResponseCommand",
        "CopyMessageCommand",
        "CopyPendingActionIdCommand",
        "CopyPendingActionPayloadCommand",
        "DeleteConversationCommand",
        "DeleteQueuedFollowUpCommand",
        "DismissAgentTaskCommand",
        "DismissLocalCommandResultCommand",
        "EditConversationGoalCommand",
        "EditMessageCommand",
        "EditQueuedFollowUpCommand",
        "ExecuteApprovedPlanCommand",
        "ExportConversationCommand",
        "FindNextConversationMatchCommand",
        "FindPreviousConversationMatchCommand",
        "MarkAllActivityReadCommand",
        "MoveQueuedFollowUpDownCommand",
        "MoveQueuedFollowUpUpCommand",
        "NewChatCommand",
        "OpenAgentRunNoticeCommand",
        "OpenAgentTaskCommand",
        "OpenAttachmentCommand",
        "OpenBranchOriginCommand",
        "OpenCodeReviewPaneCommand",
        "OpenCompletionNoticeCommand",
        "OpenConversationFindCommand",
        "OpenSettingsCommand",
        "OpenWorkspaceChangeFileCommand",
        "PasteImageAttachmentCommand",
        "PauseConversationGoalCommand",
        "PrimaryActionCommand",
        "QueueFollowUpCommand",
        "RefreshMessageCommand",
        "RejectPendingActionCommand",
        "RemoveAttachmentCommand",
        "RenameConversationCommand",
        "RequestWorkspaceRollbackCommand",
        "ResumeAgentTaskCommand",
        "ResumeConversationGoalCommand",
        "RetryMessageCommand",
        "RetryStatePersistenceCommand",
        "SelectComposerReferenceCommand",
        "SelectConversationCommand",
        "SelectPromptHistorySearchResultCommand",
        "SendCommand",
        "SendQueuedFollowUpNowCommand",
        "SetComposerAccessModeCommand",
        "ShowContextDiagnosticsCommand",
        "ShowConversationGoalHistoryCommand",
        "ShowUsageDiagnosticsCommand",
        "SteerCommand",
        "SubmitUserQuestionAnswerCommand",
        "ToggleActivityViewCommand",
        "ToggleAgentTaskPanelCommand",
        "TogglePinConversationCommand",
    ];

    private static readonly string[] PanelEvents =
    [
        "AccessModeSelectionRequested",
        "ConversationSearchRequested",
        "MessageNavigationRequested",
        "ProfileSelectionRequested",
        "PropertyChanged",
        "ReasoningSelectionRequested",
    ];

    private static readonly string[] PanelProperties =
    [
        "CanOpenExpandedComposerEditor",
        "CanSelectProfile",
        "CanShowCompactHistory",
        "CanShowConversationRewindShortcut",
        "CanSteerCurrentRun",
        "ComposerMaximumCharacters",
        "CurrentConversationFindMatch",
        "HasComposerReferenceSuggestions",
        "HasConfigurableReasoning",
        "HasConversationSearchQuery",
        "HasLocalCommandSuggestions",
        "InputText",
        "IsBusy",
        "IsComposerReferenceMentionActive",
        "IsComposerReferenceSearchPending",
        "IsConversationEmpty",
        "IsConversationFindOpen",
        "IsInputEmpty",
        "IsNavigatingPromptHistory",
        "IsPromptHistorySearchOpen",
        "Messages",
        "SelectedConversation",
        "SelectedLocalCommandSuggestionIndex",
        "SelectedProfile",
        "SelectedPromptHistorySearchResult",
        "UseMultilineComposer",
    ];

    private static readonly string[] PanelMethods =
    [
        "AddFileAttachmentsAsync",
        "CancelPromptHistoryNavigation",
        "DismissComposerReferenceSuggestions",
        "DismissPromptHistorySearch",
        "FlushConversationSearchRefresh",
        "MoveConversationFind",
        "OpenConversationFind",
        "RefreshConversationFind",
        "SetSelectedProfileReasoningMode",
        "ShowConversationRewindPointsFromKeyboard",
        "ShowKeyboardShortcutHelp",
        "TryAcceptPromptHistoryPrefixCompletion",
        "TryBeginPasteClipboardImageAttachment",
        "TryCompleteComposerReference",
        "TryCompleteLocalCommand",
        "TryCompleteLocalCommandForSubmission",
        "TryCompletePromptHistorySearch",
        "TryNavigateComposerReference",
        "TryNavigateLocalCommandSuggestion",
        "TryNavigatePromptHistory",
        "TryNavigatePromptHistorySearch",
        "TryOpenPromptHistorySearch",
        "TrySendCurrentRunFollowUpNow",
        "TryStopCurrentReplyFromKeyboard",
        "TrySubmitAlternateCurrentRunFollowUp",
        "TryToggleComposerStash",
        "TryTogglePromptHistorySearchScope",
    ];

    [Fact]
    public void PublicConstructorSignaturesRemainCompatible()
    {
        var actual = typeof(CopilotChatViewModel)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(constructor => constructor.GetParameters().Length)
            .ThenBy(FormatConstructor)
            .Select(FormatConstructor)
            .ToArray();

        Assert.Equal(
        [
            FormatConstructor(),
            FormatConstructor(typeof(CopilotChatService)),
            FormatConstructor(typeof(CopilotChatService), typeof(ICopilotChatStateStore)),
        ],
        actual);
    }

    [Fact]
    public void PublicCommandPropertySurfaceMatchesTheCompatibilityBaseline()
    {
        var actual = typeof(CopilotChatViewModel)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => typeof(ICommand).IsAssignableFrom(property.PropertyType))
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedCommandProperties, actual);
    }

    [Fact]
    public void PanelCodeBehindDependenciesRemainAvailable()
    {
        var viewModelType = typeof(CopilotChatViewModel);

        foreach (var eventName in PanelEvents)
            Assert.NotNull(viewModelType.GetEvent(eventName, InstanceMembers));

        foreach (var propertyName in PanelProperties)
            Assert.NotNull(viewModelType.GetProperty(propertyName, InstanceMembers));

        var methods = viewModelType.GetMethods(InstanceMembers);
        foreach (var methodName in PanelMethods)
        {
            Assert.Contains(methods, method =>
                string.Equals(method.Name, methodName, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ShortcutHelpExposesTheActivityViewGesture()
    {
        var shortcut = Assert.Single(
            CopilotKeyboardShortcutHelp.Entries,
            entry => string.Equals(entry.Keys, "Ctrl+Alt+U", StringComparison.Ordinal));

        Assert.Equal("全局", shortcut.Scope);
        Assert.Contains("活动视图", shortcut.Action, StringComparison.Ordinal);
    }

    [Fact]
    public void PanelServiceIdentitySurfaceRemainsSingletonShaped()
    {
        var serviceType = typeof(CopilotPanelService);
        var getInstance = serviceType.GetMethod(
            nameof(CopilotPanelService.GetInstance),
            BindingFlags.Static | BindingFlags.Public);
        var constructor = Assert.Single(serviceType.GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic));

        Assert.Equal("CopilotChatPanel", CopilotPanelService.PanelId);
        Assert.NotNull(getInstance);
        Assert.Equal(serviceType, getInstance.ReturnType);
        Assert.Empty(getInstance.GetParameters());
        Assert.True(constructor.IsPrivate);
        Assert.Empty(constructor.GetParameters());

        var service = CopilotPanelService.GetInstance();
        Assert.Same(service, CopilotServiceRegistry.Current);
    }

    [Fact]
    public void MainStatusBarExposesChatAssistantShortcut()
    {
        StatusBarMeta? item = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                item = Assert.Single(new CopilotStatusBarProvider().GetStatusBarIconMetadata());
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
        Assert.NotNull(item);
        Assert.Equal("CopilotAgent", item.Id);
        Assert.Equal(CopilotUiText.CopilotPanelTitle, item.Name);
        Assert.Equal(MenuItemConstants.MainWindowTarget, item.TargetName);
        Assert.Equal(StatusBarAlignment.Right, item.Alignment);
        Assert.True(item.IsVisible);
        Assert.NotNull(item.Command);
        Assert.NotNull(item.IconContent);
    }

    private static string FormatConstructor(ConstructorInfo constructor) =>
        FormatConstructor(constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray());

    private static string FormatConstructor(params Type[] parameterTypes) =>
        $"({string.Join(", ", parameterTypes.Select(type => type.FullName))})";
}
