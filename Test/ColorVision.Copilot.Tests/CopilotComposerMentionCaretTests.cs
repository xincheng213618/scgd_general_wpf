using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotComposerMentionCaretTests
{
    [Fact]
    public void WorkspaceResultsPreserveTheCandidateChosenWhileIndexingWasPending()
    {
        Run(fixture =>
        {
            var root = Directory.CreateTempSubdirectory("CopilotMentionSelection-").FullName;
            var catalogType = typeof(CopilotComposerReferenceCatalog);
            var gate = catalogType.GetField("FileIndexLock", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            var fields = new[] { "_cachedWorkspaceRoot", "_cachedWorkspaceFiles", "_workspaceFileIndexTask", "_hasCachedWorkspaceFileIndex", "_workspaceFileIndexGeneration" }
                .Select(name => catalogType.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!).ToArray();
            object?[] previous;
            lock (gate)
                previous = fields.Select(field => field.GetValue(null)).ToArray();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var context = new CopilotPausedAdmissionContext(TimeSpan.FromSeconds(10), "The workspace selection refresh did not finish.");
            var previousContext = SynchronizationContext.Current;
            Task? refresh = null;
            try
            {
                foreach (var name in new[] { "selection-probe-a", "selection-probe-b" })
                {
                    var skillRoot = Directory.CreateDirectory(Path.Combine(root, ".agents", "skills", name)).FullName;
                    File.WriteAllText(Path.Combine(skillRoot, "SKILL.md"),
                        "---\nname: " + name + "\ndescription: Selection probe skill\n---\nKeep the selected skill.\n");
                }
                File.WriteAllText(Path.Combine(root, "selection-probe-file.txt"), "Workspace evidence");
                var skills = CopilotAgentSkillCatalog.Discover([root], null, root, root);
                var immediate = CopilotComposerReferenceCatalog.SearchImmediate("selection-probe", null, true, skills);
                Assert.Equal(2, immediate.Count);
                var getIndex = catalogType.GetMethod("GetWorkspaceFilesAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
                var realIndex = Assert.IsAssignableFrom<Task>(getIndex.Invoke(null, [root, true, CancellationToken.None]));
                realIndex.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                var holdIndex = typeof(CopilotComposerMentionCaretTests).GetMethod(nameof(HoldWorkspaceIndex), BindingFlags.Static | BindingFlags.NonPublic)!
                    .MakeGenericMethod(getIndex.ReturnType.GetGenericArguments()[0]);
                var pendingIndex = Assert.IsAssignableFrom<Task>(holdIndex.Invoke(null, [realIndex, release.Task]));
                lock (gate)
                    fields[2].SetValue(null, pendingIndex);

                fixture.SetDraft("@selection-probe", "@selection-probe".Length);
                typeof(CopilotChatViewModel).GetMethod("ApplyComposerReferenceSuggestions", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(fixture.ViewModel, [immediate, null]);
                var originalSelection = fixture.ViewModel.SelectedComposerReference!.Value;
                var version = (long)typeof(CopilotChatViewModel).GetField("_composerReferenceRefreshVersion", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(fixture.ViewModel)!;
                SynchronizationContext.SetSynchronizationContext(context);
                refresh = Assert.IsAssignableFrom<Task>(typeof(CopilotChatViewModel)
                    .GetMethod("RefreshWorkspaceComposerReferencesAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(fixture.ViewModel, ["selection-probe", root, false, immediate, originalSelection, version, CancellationToken.None]));
                Assert.False(refresh.IsCompleted);
                Assert.True(fixture.ViewModel.TryNavigateComposerReference(previous: false));
                var chosen = fixture.ViewModel.SelectedComposerReference!;
                Assert.NotEqual(originalSelection, chosen.Value);

                release.TrySetResult();
                context.Complete(refresh);
                refresh.GetAwaiter().GetResult();

                Assert.Contains(fixture.ViewModel.ComposerReferenceSuggestions, item => item.Kind == CopilotComposerReferenceKind.File);
                Assert.Equal(chosen.Value, fixture.ViewModel.SelectedComposerReference!.Value);
                Assert.True(fixture.ViewModel.TryCompleteComposerReference());
                Assert.Equal("$" + chosen.AgentSkillReference!.Name + " ", fixture.ViewModel.InputText);
                Assert.Equal(chosen.AgentSkillReference.Name, fixture.ViewModel.SelectedConversation!.DraftAgentSkillReference!.Name);
            }
            finally
            {
                release.TrySetResult();
                try
                {
                    if (refresh is { IsCompleted: false })
                        context.Complete(refresh);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                    lock (gate)
                    {
                        for (var index = 0; index < fields.Length; index++)
                            fields[index].SetValue(null, previous[index]);
                    }
                    var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
                    var tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
                    Assert.Equal(tempRoot, Path.GetDirectoryName(fullRoot), ignoreCase: true);
                    Assert.StartsWith("CopilotMentionSelection-", Path.GetFileName(fullRoot), StringComparison.Ordinal);
                    Directory.Delete(fullRoot, recursive: true);
                }
            }
        });
    }

    private static async Task<T> HoldWorkspaceIndex<T>(Task<T> index, Task release)
    {
        var files = await index.ConfigureAwait(false);
        await release.ConfigureAwait(false);
        return files;
    }

    [Theory]
    [InlineData("\nKeep the final instructions.")]
    [InlineData(" Keep the final instructions.")]
    public void MenuInsertionOpensTheMentionAtTheCaretWithoutConsumingTheSuffix(string suffix)
    {
        Run(fixture =>
        {
            fixture.SetDraft("Review" + suffix, "Review".Length);
            fixture.InvokePanel("ComposerReferenceMenuItem_Click", new MenuItem(), new RoutedEventArgs());
            DrainDispatcher();

            Assert.Equal("Review @" + suffix, fixture.ViewModel.InputText);
            Assert.Equal("Review @".Length, fixture.Prompt.CaretIndex);
            Assert.True(fixture.ViewModel.IsComposerReferenceMentionActive);
            Assert.Empty(fixture.ViewModel.Attachments);
        });
    }

    [Theory]
    [InlineData(false, "\nKeep the final instructions.")]
    [InlineData(false, " Keep the final instructions.")]
    [InlineData(true, "\nKeep the final instructions.")]
    [InlineData(true, " Keep the final instructions.")]
    public void TabCompletionPreservesTheSuffixAndLeavesTheCaretAfterTheReference(bool skill, string suffix)
    {
        Run(fixture =>
        {
            fixture.SetDraft("Review @probe" + suffix, "Review @probe".Length);
            var reference = CreateReference(skill);
            fixture.ViewModel.ComposerReferenceSuggestions.Clear();
            fixture.ViewModel.ComposerReferenceSuggestions.Add(reference);
            fixture.ViewModel.SelectedComposerReference = reference;
            var key = new KeyEventArgs(Keyboard.PrimaryDevice, new TestPresentationSource(), 0, Key.Tab)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };
            fixture.Prompt.RaiseEvent(key);
            DrainDispatcher();

            var prefix = skill ? "Review $probe-skill " : "Review @[Probe context] ";
            Assert.True(key.Handled);
            Assert.Equal(prefix + suffix, fixture.ViewModel.InputText);
            Assert.Equal(prefix.Length, fixture.Prompt.CaretIndex);
            Assert.Equal(skill ? 0 : 1, fixture.ViewModel.Attachments.Count);
            if (skill)
                Assert.Equal("probe-skill", fixture.ViewModel.SelectedConversation?.DraftAgentSkillReference?.Name);
        });
    }

    [Fact]
    public void ClickingACandidatePreservesTheSuffixAndRestoresTheInsertionCaret()
    {
        Run(fixture =>
        {
            fixture.SetDraft("Review @probe\nKeep this paragraph.", "Review @probe".Length);
            var reference = CreateReference(skill: false);
            var button = new Button { Command = fixture.ViewModel.SelectComposerReferenceCommand, CommandParameter = reference };
            button.Click += (sender, args) => fixture.InvokePanel("ComposerReferenceSuggestionButton_Click", sender!, args);
            typeof(Button).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(button, null);
            DrainDispatcher();

            Assert.Equal("Review @[Probe context] \nKeep this paragraph.", fixture.ViewModel.InputText);
            Assert.Equal("Review @[Probe context] ".Length, fixture.Prompt.CaretIndex);
            Assert.Single(fixture.ViewModel.Attachments);
        });
    }

    [Fact]
    public void TypingAMiddleQueryUpdatesTheTextBindingAndActiveMention()
    {
        Run(fixture =>
        {
            fixture.SetDraft("Review @\nKeep this paragraph.", "Review @".Length);
            fixture.Prompt.SelectedText = "probe";
            fixture.Prompt.CaretIndex = "Review @probe".Length;
            DrainDispatcher();

            Assert.Equal("Review @probe\nKeep this paragraph.", fixture.ViewModel.InputText);
            Assert.True(fixture.ViewModel.IsComposerReferenceMentionActive);
            Assert.True(fixture.ViewModel.TryCompleteComposerReference(CreateReference(skill: false)));
            Assert.Equal("Review @[Probe context] \nKeep this paragraph.", fixture.ViewModel.InputText);
        });
    }

    [Fact]
    public void MentionCommandOpensItsQueryAtTheEndAfterTheTextBindingSettles()
    {
        Run(fixture =>
        {
            fixture.SetDraft("/mention probe", "/mention probe".Length);
            var execute = typeof(CopilotChatViewModel).GetMethod("TryExecuteLocalCommand", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.True((bool)execute.Invoke(fixture.ViewModel, ["/mention probe", false, null])!);
            DrainDispatcher();

            Assert.Equal("@probe", fixture.ViewModel.InputText);
            Assert.Equal("@probe".Length, fixture.Prompt.CaretIndex);
            Assert.True(fixture.ViewModel.IsComposerReferenceMentionActive);
            Assert.Empty(fixture.ViewModel.Attachments);
        });
    }

    [Fact]
    public void MovingTheCaretBeforeTheMentionClosesThePopover()
    {
        Run(fixture =>
        {
            fixture.SetDraft("Review @probe", "Review @probe".Length);
            Assert.True(fixture.ViewModel.IsComposerReferenceMentionActive);
            fixture.Prompt.CaretIndex = 0;
            DrainDispatcher();
            Assert.False(fixture.ViewModel.IsComposerReferenceMentionActive);
        });
    }

    [Fact]
    public void SelectingTextClosesThePopoverWithoutReplacingTheSelection()
    {
        Run(fixture =>
        {
            fixture.SetDraft("Review @probe", "Review @probe".Length);
            fixture.Prompt.Select(0, fixture.Prompt.Text.Length);
            DrainDispatcher();
            Assert.False(fixture.ViewModel.IsComposerReferenceMentionActive);
            Assert.Equal("Review @probe", fixture.ViewModel.InputText);
        });
    }

    [Fact]
    public void ExistingEndOfDraftCompletionStillAttachesTheReference()
    {
        Run(fixture =>
        {
            fixture.SetDraft("Review @probe", "Review @probe".Length);
            Assert.True(fixture.ViewModel.TryCompleteComposerReference(CreateReference(skill: false)));
            Assert.Equal("Review @[Probe context] ", fixture.ViewModel.InputText);
            Assert.Single(fixture.ViewModel.Attachments);
        });
    }

    private static CopilotComposerReferenceItem CreateReference(bool skill) => new()
    {
        Kind = skill ? CopilotComposerReferenceKind.Skill : CopilotComposerReferenceKind.Menu,
        Title = "Probe context",
        Value = "probe-context",
        SourceId = "caret-tests/probe-context",
        ContextContent = "Bounded context for the mention caret test.",
        AgentSkillReference = skill ? new CopilotAgentSkillReference
        {
            Name = "probe-skill", SkillFilePath = Path.Combine(Path.GetTempPath(), "caret-test-skill", "SKILL.md"),
        } : null,
    };

    private static void Run(Action<MentionFixture> action) => StaTest.Run(() =>
    {
        try { using var fixture = new MentionFixture(); action(fixture); }
        finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
    });

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private sealed class MentionFixture : IDisposable
    {
        private static readonly FieldInfo InstanceField = typeof(SolutionManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previousInstance = InstanceField.GetValue(null);
        private readonly SolutionManager _testInstance = (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly CopilotAgentTaskHost _host = new();
        private readonly CopilotChatPanel _panel;

        public MentionFixture()
        {
            InstanceField.SetValue(null, _testInstance);
            var profile = new CopilotProfileConfig
            {
                Id = "mention-caret-profile", Name = "Mention caret profile", VendorType = CopilotVendorType.Custom,
                ProviderType = CopilotProviderType.OpenAICompatible, ApiKey = "test-key", BaseUrl = "https://unit.test/v1", Model = "test-model",
            };
            var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.Name);
            var state = new CopilotChatState
            {
                ActiveConversationId = conversation.Id, ActiveProfileId = profile.Id,
                Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            };
            var config = new CopilotConfig { SchemaVersion = CopilotConfig.CurrentSchemaVersion, McpBearerToken = "test-token", Profiles = [profile] };
            ViewModel = new CopilotChatViewModel(new CopilotChatService(), new MemoryStateStore(state), config, new UnusedTurnRuntime(), _host);
            _panel = new CopilotChatPanel { DataContext = ViewModel };
            Prompt = (TextBox)_panel.FindName("PromptTextBox");
            DrainDispatcher();
        }

        public CopilotChatViewModel ViewModel { get; }
        public TextBox Prompt { get; }

        public void SetDraft(string text, int caret)
        {
            ViewModel.InputText = text;
            Prompt.GetBindingExpression(TextBox.TextProperty)!.UpdateTarget();
            Prompt.CaretIndex = caret;
            DrainDispatcher();
        }

        public void InvokePanel(string name, params object[] args) =>
            typeof(CopilotChatPanel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_panel, args);

        public void Dispose()
        {
            _panel.DataContext = null;
            _host.Shutdown();
            ViewModel.Dispose();
            if (ReferenceEquals(InstanceField.GetValue(null), _testInstance))
                InstanceField.SetValue(null, _previousInstance);
        }
    }

    private sealed class TestPresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private sealed class MemoryStateStore(CopilotChatState state) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => string.Empty;
        public CopilotChatState Load() => state;
        public void Save(CopilotChatState value) { }
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());
        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";
        public string Serialize(CopilotChatState value) => "{}";
        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private sealed class UnusedTurnRuntime : ICopilotTurnRuntime
    {
        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(CopilotTurnRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            throw new InvalidOperationException("Mention tests must not start a model request.");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }
}
