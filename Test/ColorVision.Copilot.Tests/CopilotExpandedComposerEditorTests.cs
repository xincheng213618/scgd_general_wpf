using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotExpandedComposerEditorTests
{
    [Fact]
    public void ModalEditorRequiresReviewOfEachRecoveredDraftBeforeSavingTheMergedText()
    {
        Run(fixture =>
        {
            const string editedText = "修改：光谱 🧪\r\n编辑器中尚未保存的内容  \t\r\n";
            string? mergedText = null;
            var mergedCaret = -1;
            fixture.Prompt.CaretIndex = 3;
            fixture.OpenEditor(editor =>
            {
                var input = Assert.IsType<TextBox>(editor.FindName("InputTextBox"));
                Assert.Equal(3, editor.ResultCaretIndex);
                Assert.Equal(fixture.ViewModel.ComposerMaximumCharacters, input.MaxLength);
                input.Text = editedText;
                Assert.Equal(editedText, editor.RawResultText);

                var firstRecovery = string.Join("\r\n", Enumerable.Range(1, 16)
                    .Select(index => $"恢复请求 {index}：请保留这一段完整的测量说明与中文、Unicode 🧪。"));
                fixture.RestoreQueuedDraft(firstRecovery, fixture.RecoveredAttachment);
                var firstDraft = fixture.ViewModel.InputText;
                Click(editor, "OkButton");
                AssertConflict(editor, fixture, editedText, firstDraft);
                fixture.AssertNonTextStateIsPreserved();

                fixture.RestoreQueuedDraft("又一条排队请求在比较期间恢复，不能被旧的保存决定覆盖。");
                var newestDraft = fixture.ViewModel.InputText;
                Assert.NotEqual(firstDraft, newestDraft);
                var saveBinding = Assert.Single(editor.InputBindings.OfType<KeyBinding>(), binding =>
                    binding.Key == Key.Enter && binding.Modifiers == ModifierKeys.Control);
                Assert.Same(CopilotTextInputWindow.ConfirmInputCommand, saveBinding.Command);
                Assert.True(CopilotTextInputWindow.ConfirmInputCommand.CanExecute(null, input));
                CopilotTextInputWindow.ConfirmInputCommand.Execute(null, input);
                AssertConflict(editor, fixture, editedText, newestDraft);
                fixture.AssertNonTextStateIsPreserved();
                RenderConflictIfRequested(editor);

                mergedText = "已合并编辑与恢复内容：\r\n" + newestDraft + "\r\n" + editedText;
                mergedCaret = mergedText.IndexOf("恢复", StringComparison.Ordinal);
                input.Text = mergedText;
                input.CaretIndex = mergedCaret;
                CopilotTextInputWindow.ConfirmInputCommand.Execute(null, input);
                Assert.Equal(true, editor.DialogResult);
            });

            Assert.Equal(mergedText, fixture.ViewModel.InputText);
            Assert.Equal(mergedText, fixture.Conversation.DraftText);
            Assert.Equal(mergedCaret, fixture.Prompt.CaretIndex);
            fixture.AssertNonTextStateIsPreserved();
        });
    }

    [Fact]
    public void CancellingAConflictedModalEditorKeepsTheRecoveredDraftAndItsAttachments()
    {
        Run(fixture =>
        {
            string? recoveredDraft = null;
            fixture.OpenEditor(editor =>
            {
                var input = Assert.IsType<TextBox>(editor.FindName("InputTextBox"));
                input.Text = "仅在大编辑器中的修改，取消后不替换恢复草稿。";
                fixture.RestoreQueuedDraft("准备失败后恢复的原排队请求。", fixture.RecoveredAttachment);
                recoveredDraft = fixture.ViewModel.InputText;
                Click(editor, "OkButton");
                AssertConflict(editor, fixture, input.Text, recoveredDraft);
                Click(editor, "CancelButton");
                Assert.Equal(false, editor.DialogResult);
            });

            Assert.Equal(recoveredDraft, fixture.ViewModel.InputText);
            Assert.Equal(recoveredDraft, fixture.Conversation.DraftText);
            fixture.AssertNonTextStateIsPreserved();
        });
    }

    private static void AssertConflict(
        CopilotTextInputWindow editor,
        EditorFixture fixture,
        string editedText,
        string recoveredText)
    {
        Assert.True(editor.IsVisible);
        Assert.Null(editor.DialogResult);
        Assert.Equal(editedText, editor.RawResultText);
        Assert.Equal(recoveredText, fixture.ViewModel.InputText);
        Assert.Equal(recoveredText, fixture.Conversation.DraftText);
        Assert.Equal(Visibility.Visible, Assert.IsType<StackPanel>(editor.FindName("AcceptanceFeedbackPanel")).Visibility);
        var comparison = Assert.IsType<TextBox>(editor.FindName("ComparisonTextBox"));
        Assert.True(comparison.IsReadOnly);
        Assert.Equal(recoveredText, comparison.Text);
        Assert.Equal(0, comparison.MaxLength);
        Assert.Contains("再次保存", Assert.IsType<TextBlock>(editor.FindName("AcceptanceFeedbackTextBlock")).Text, StringComparison.Ordinal);
    }

    private static void Click(CopilotTextInputWindow editor, string name) =>
        Assert.IsType<Button>(editor.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void RenderConflictIfRequested(CopilotTextInputWindow editor)
    {
        var renderPath = Environment.GetEnvironmentVariable("COLORVISION_COPILOT_EDITOR_RENDER_PATH");
        if (string.IsNullOrWhiteSpace(renderPath))
            return;

        editor.UpdateLayout();
        var content = Assert.IsAssignableFrom<FrameworkElement>(editor.Content);
        var size = new Size(content.ActualWidth, content.ActualHeight);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(size));
            context.DrawRectangle(new VisualBrush(content), null, new Rect(size));
        }
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.GetFullPath(renderPath));
        encoder.Save(output);
    }

    private static void Run(Action<EditorFixture> action)
    {
        StaTest.Run(() =>
        {
            try
            {
                using var fixture = new EditorFixture();
                action(fixture);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        }, TimeSpan.FromSeconds(20), "The expanded composer modal interaction did not complete.");
    }

    private sealed class EditorFixture : IDisposable
    {
        private static readonly FieldInfo SolutionInstance = typeof(SolutionManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private static readonly MethodInfo OpenExpandedEditor = typeof(CopilotChatPanel).GetMethod("OpenExpandedPromptEditor", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly MethodInfo SynchronizeRecoveredDraft = typeof(CopilotChatViewModel).GetMethod("SynchronizeSelectedDraftAfterQueuedRecovery", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly object? _previousSolution = SolutionInstance.GetValue(null);
        private readonly object _isolatedSolution = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly CopilotAgentTaskHost _host = new();
        private readonly CopilotComposerStash _stash;

        public EditorFixture()
        {
            SolutionInstance.SetValue(null, _isolatedSolution);
            var profile = new CopilotProfileConfig
            {
                Id = "expanded-editor-profile", Name = "Expanded editor profile",
                VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "expanded-editor-test-key", BaseUrl = "https://example.test/v1", Model = "test-model",
            };
            Conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.Name);
            Conversation.DraftText = "原来的输入框草稿。";
            Conversation.DraftRequestMode = CopilotAgentMode.Review;
            Conversation.DraftWorkspaceReviewTarget = CopilotWorkspaceReviewTargetContext.WorkingTree();
            Conversation.Attachments.Add(OriginalAttachment);
            Conversation.ComposerStash = CopilotComposerStash.Capture("已暂存的独立草稿，不能覆盖。", 3, CopilotAgentMode.Plan, []);
            _stash = Conversation.ComposerStash!;
            State = new CopilotChatState
            {
                ActiveConversationId = Conversation.Id, ActiveProfileId = profile.Id, Conversations = [Conversation],
            };
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "expanded-editor-test-token", Profiles = [profile],
            };
            var service = new CopilotChatService();
            ViewModel = new CopilotChatViewModel(service, new MemoryStateStore(State), config, new CopilotTurnRuntime(service), _host);
            Panel = new CopilotChatPanel { DataContext = ViewModel };
            Prompt = Assert.IsType<TextBox>(Panel.FindName("PromptTextBox"));
            Owner = new Window
            {
                Content = Panel, Width = 800, Height = 600, Left = -10000, Top = -10000,
                WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false, Opacity = 0,
            };
            Owner.Show();
            Owner.UpdateLayout();
        }

        public CopilotChatState State { get; }
        public CopilotConversationRecord Conversation { get; }
        public CopilotChatViewModel ViewModel { get; }
        public CopilotChatPanel Panel { get; }
        public TextBox Prompt { get; }
        public Window Owner { get; }
        public CopilotAttachmentItem OriginalAttachment { get; } = CopilotAttachmentItem.CreateContext("Existing attachment text", "Original attachment");
        public CopilotAttachmentItem RecoveredAttachment { get; } = CopilotAttachmentItem.CreateContext("Recovered attachment text", "Recovered attachment");

        public void RestoreQueuedDraft(string text, params CopilotAttachmentItem[] attachments)
        {
            var runId = Guid.NewGuid().ToString("N");
            State.QueuedFollowUpRecoveries.Add(new CopilotQueuedFollowUpRecoveryRecord
            {
                RunId = runId, ConversationId = Conversation.Id,
                ComposerState = CopilotComposerStash.Capture(text, text.Length, CopilotAgentMode.Review, attachments),
            });
            Assert.True(CopilotQueuedFollowUpRecovery.RestoreRecordToDraft(State, runId));
            Assert.Empty(State.QueuedFollowUpRecoveries);
            SynchronizeRecoveredDraft.Invoke(ViewModel, null);
            Assert.Contains(text, ViewModel.InputText, StringComparison.Ordinal);
        }

        public void AssertNonTextStateIsPreserved()
        {
            Assert.Same(_stash, Conversation.ComposerStash);
            Assert.Equal("已暂存的独立草稿，不能覆盖。", Conversation.ComposerStash!.Text);
            Assert.Equal(CopilotAgentMode.Review, Conversation.DraftRequestMode);
            Assert.Equal(CopilotWorkspaceReviewTarget.WorkingTree, Conversation.DraftWorkspaceReviewTarget?.Target);
            Assert.Equal([OriginalAttachment.Id, RecoveredAttachment.Id], Conversation.Attachments.Select(attachment => attachment.Id));
            Assert.Same(OriginalAttachment, Conversation.Attachments[0]);
        }

        public void OpenEditor(Action<CopilotTextInputWindow> inspect)
        {
            Exception? failure = null;
            Owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
            {
                try
                {
                    var editor = Assert.Single(Owner.OwnedWindows.OfType<CopilotTextInputWindow>());
                    inspect(editor);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    foreach (var window in Owner.OwnedWindows.Cast<Window>().ToArray())
                        window.Close();
                }
            });
            Assert.True((bool)OpenExpandedEditor.Invoke(Panel, null)!);
            var frame = new DispatcherFrame();
            Owner.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        public void Dispose()
        {
            foreach (var window in Owner.OwnedWindows.Cast<Window>().ToArray())
                window.Close();
            Panel.DataContext = null;
            Owner.Content = null;
            Owner.Close();
            _host.Shutdown();
            ViewModel.Dispose();
            if (ReferenceEquals(SolutionInstance.GetValue(null), _isolatedSolution))
                SolutionInstance.SetValue(null, _previousSolution);
        }
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
}
