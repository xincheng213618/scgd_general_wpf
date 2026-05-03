using ColorVision.Common.MVVM;
using ColorVision.UI;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public class CopilotChatViewModel : ViewModelBase
    {
        private const int AttachmentContentLimit = 12000;

        private readonly CopilotChatService _chatService;
        private readonly CopilotConfig _config;
        private readonly ObservableCollection<CopilotChatMessage> _emptyMessages = new();
        private readonly ObservableCollection<CopilotAttachmentItem> _emptyAttachments = new();
        private CancellationTokenSource? _currentRequestCts;
        private CopilotConversationRecord? _selectedConversation;
        private CopilotProfileConfig? _selectedProfile;

        public CopilotChatViewModel()
            : this(new CopilotChatService())
        {
        }

        public CopilotChatViewModel(CopilotChatService chatService)
        {
            _chatService = chatService;
            _config = CopilotConfig.Instance;

            if (_config.EnsureInitialized())
                PersistState();

            var initialConversation = Conversations.Count > 0
                ? Conversations[0]
                : CopilotConversationRecord.CreateEmpty(_config.ActiveProfileId, string.Empty);

            if (Conversations.Count == 0)
                Conversations.Add(initialConversation);

            SelectConversation(Conversations.FirstOrDefault(conversation => conversation.Id == _config.ActiveConversationId) ?? initialConversation, persist: false);

            SendCommand = new RelayCommand(_ => _ = SendAsync());
            NewChatCommand = new RelayCommand(_ => StartNewChat());
            CancelCommand = new RelayCommand(_ => CancelCurrentReply());
            PrimaryActionCommand = new RelayCommand(_ => ExecutePrimaryAction());
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
            AddFileAttachmentCommand = new RelayCommand(_ => AddFileAttachment(), _ => !IsBusy);
            AddContextAttachmentCommand = new RelayCommand(_ => AddContextAttachment(), _ => !IsBusy);
            RemoveAttachmentCommand = new RelayCommand<CopilotAttachmentItem>(RemoveAttachment, attachment => !IsBusy && attachment != null);
            RenameConversationCommand = new RelayCommand<CopilotConversationRecord>(RenameConversation, conversation => !IsBusy && conversation != null);
            DeleteConversationCommand = new RelayCommand<CopilotConversationRecord>(DeleteConversation, conversation => !IsBusy && conversation != null);
            TogglePinConversationCommand = new RelayCommand<CopilotConversationRecord>(TogglePinConversation, conversation => !IsBusy && conversation != null);
        }

        public ObservableCollection<CopilotConversationRecord> Conversations => _config.Conversations;

        public ObservableCollection<CopilotProfileConfig> Profiles => _config.Profiles;

        public ObservableCollection<CopilotChatMessage> Messages => SelectedConversation?.Messages ?? _emptyMessages;

        public ObservableCollection<CopilotAttachmentItem> Attachments => SelectedConversation?.Attachments ?? _emptyAttachments;

        public ICommand SendCommand { get; }

        public ICommand NewChatCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand PrimaryActionCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand AddFileAttachmentCommand { get; }

        public ICommand AddContextAttachmentCommand { get; }

        public ICommand RemoveAttachmentCommand { get; }

        public ICommand RenameConversationCommand { get; }

        public ICommand DeleteConversationCommand { get; }

        public ICommand TogglePinConversationCommand { get; }

        public bool IsConversationEmpty => Messages.Count == 0;

        public bool HasAttachments => Attachments.Count > 0;

        public string EmptyStateText => _config.IsConfigured
            ? "从右侧选择历史会话，或点击 + 新建会话。"
            : "先点右上角配置添加模型，再开始对话。";

        public string PrimaryActionGlyph => IsBusy ? "■" : "↑";

        public string PrimaryActionToolTip => IsBusy ? "停止生成" : "发送";

        public CopilotConversationRecord? SelectedConversation
        {
            get => _selectedConversation;
            set => SelectConversation(value, persist: true);
        }

        public CopilotProfileConfig? SelectedProfile
        {
            get => _selectedProfile;
            set => SelectProfile(value, syncConversation: true, persist: true);
        }

        public string InputText
        {
            get => _inputText;
            set => SetProperty(ref _inputText, value ?? string.Empty);
        }
        private string _inputText = string.Empty;

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSwitchConversation));
                OnPropertyChanged(nameof(CanSelectProfile));
                OnPropertyChanged(nameof(PrimaryActionGlyph));
                OnPropertyChanged(nameof(PrimaryActionToolTip));
                CommandManager.InvalidateRequerySuggested();
            }
        }
        private bool _isBusy;

        public bool CanSwitchConversation => !IsBusy;

        public bool CanSelectProfile => !IsBusy && Profiles.Count > 0;

        private async Task SendAsync()
        {
            if (IsBusy)
                return;

            var prompt = (InputText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(prompt))
                return;

            if (SelectedProfile == null || !SelectedProfile.IsConfigured)
            {
                OpenSettings();
                return;
            }

            var requestProfile = SelectedProfile.Clone();
            var conversation = EnsureConversation();
            conversation.ProfileId = requestProfile.Id;
            conversation.ProfileDisplayName = requestProfile.DisplayLabel;

            var userMessage = new CopilotChatMessage(CopilotChatRole.User, prompt);
            Messages.Add(userMessage);
            UpdateConversationMetadata(conversation, touch: true);
            PersistState();

            var history = BuildConversationHistory();

            var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
            Messages.Add(assistantMessage);
            InputText = string.Empty;

            IsBusy = true;

            _currentRequestCts?.Cancel();
            _currentRequestCts?.Dispose();
            _currentRequestCts = new CancellationTokenSource();

            try
            {
                await _chatService.StreamReplyAsync(
                    requestProfile,
                    history,
                    delta => ApplyAssistantDelta(assistantMessage, delta),
                    _currentRequestCts.Token);

                assistantMessage.IsReasoningInProgress = false;

                if (string.IsNullOrWhiteSpace(assistantMessage.Content))
                {
                    assistantMessage.Content = assistantMessage.HasReasoning
                        ? "未收到最终回答，只拿到了推理内容。"
                        : "接口返回成功，但没有可显示的文本。";
                }

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
            }
            catch (OperationCanceledException)
            {
                assistantMessage.IsReasoningInProgress = false;

                if (string.IsNullOrWhiteSpace(assistantMessage.Content))
                    assistantMessage.Content = "已取消当前回复。";

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
            }
            catch (Exception ex)
            {
                assistantMessage.IsReasoningInProgress = false;
                assistantMessage.Content = string.IsNullOrWhiteSpace(assistantMessage.Content)
                    ? $"请求失败：{ex.Message}"
                    : assistantMessage.Content;

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
            }
            finally
            {
                IsBusy = false;
                _currentRequestCts?.Dispose();
                _currentRequestCts = null;
            }
        }

        private void ApplyAssistantDelta(CopilotChatMessage assistantMessage, CopilotStreamDelta delta)
        {
            if (delta.HasReasoning)
            {
                assistantMessage.ReasoningContent += delta.ReasoningContent;
                assistantMessage.IsReasoningInProgress = true;
                assistantMessage.IsReasoningExpanded = true;
            }

            if (delta.HasContent)
            {
                var isFirstContentChunk = string.IsNullOrWhiteSpace(assistantMessage.Content);
                assistantMessage.Content += delta.Content;
                assistantMessage.IsReasoningInProgress = false;
                if (isFirstContentChunk && assistantMessage.HasReasoning)
                    assistantMessage.IsReasoningExpanded = false;
            }
        }

        private void StartNewChat()
        {
            CancelCurrentReply();
            var conversation = CreateConversation();
            SelectConversation(conversation, persist: false);
            PersistState();
        }

        private void ExecutePrimaryAction()
        {
            if (IsBusy)
            {
                CancelCurrentReply();
                return;
            }

            _ = SendAsync();
        }

        private void CancelCurrentReply()
        {
            if (!IsBusy)
                return;

            _currentRequestCts?.Cancel();
        }

        private void OpenSettings()
        {
            var window = new CopilotSettingsWindow
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (window.ShowDialog() != true)
                return;

            ReloadStateFromConfig();
        }

        private void ReloadStateFromConfig()
        {
            var preferredConversationId = SelectedConversation?.Id ?? _config.ActiveConversationId;
            var preferredProfileId = SelectedProfile?.Id ?? _config.ActiveProfileId;

            if (_config.EnsureInitialized())
                PersistState();

            OnPropertyChanged(nameof(Profiles));
            OnPropertyChanged(nameof(Conversations));
            OnPropertyChanged(nameof(EmptyStateText));
            OnPropertyChanged(nameof(CanSelectProfile));

            var conversation = Conversations.FirstOrDefault(item => item.Id == preferredConversationId)
                ?? Conversations.FirstOrDefault();

            SelectConversation(conversation, persist: false, preferredProfileId: preferredProfileId);
        }

        private List<CopilotRequestMessage> BuildConversationHistory()
        {
            var history = new List<CopilotRequestMessage>();

            var attachmentContext = BuildAttachmentContextBlock();
            if (!string.IsNullOrWhiteSpace(attachmentContext))
            {
                history.Add(new CopilotRequestMessage("user", attachmentContext));
            }

            history.AddRange(Messages
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .Select(message => new CopilotRequestMessage(
                    message.IsUser ? "user" : "assistant",
                    message.Content.Trim())));

            return history;
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsConversationEmpty));
        }

        private void SelectConversation(CopilotConversationRecord? conversation, bool persist, string? preferredProfileId = null)
        {
            if (ReferenceEquals(_selectedConversation, conversation))
            {
                if (!string.IsNullOrWhiteSpace(preferredProfileId))
                {
                    var preferredProfile = ResolveProfile(preferredProfileId) ?? ResolveProfile(_selectedConversation?.ProfileId);
                    SelectProfile(preferredProfile, syncConversation: false, persist: false);
                }
                return;
            }

            if (_selectedConversation != null)
                _selectedConversation.Messages.CollectionChanged -= Messages_CollectionChanged;

            _selectedConversation = conversation;
            if (_selectedConversation != null)
                _selectedConversation.Messages.CollectionChanged += Messages_CollectionChanged;

            OnPropertyChanged(nameof(SelectedConversation));
            OnPropertyChanged(nameof(Messages));
            OnPropertyChanged(nameof(Attachments));
            OnPropertyChanged(nameof(HasAttachments));
            OnPropertyChanged(nameof(IsConversationEmpty));

            _config.ActiveConversationId = conversation?.Id ?? string.Empty;

            var profile = ResolveProfile(preferredProfileId)
                ?? ResolveProfile(conversation?.ProfileId)
                ?? ResolveProfile(_config.ActiveProfileId)
                ?? Profiles.FirstOrDefault();

            SelectProfile(profile, syncConversation: false, persist: false);

            if (conversation != null && profile != null)
            {
                conversation.ProfileId = profile.Id;
                conversation.ProfileDisplayName = profile.DisplayLabel;
                conversation.RefreshSummary();
            }

            if (persist)
                PersistState();
        }

        private void SelectProfile(CopilotProfileConfig? profile, bool syncConversation, bool persist)
        {
            if (ReferenceEquals(_selectedProfile, profile))
                return;

            _selectedProfile = profile;
            OnPropertyChanged(nameof(SelectedProfile));

            _config.ActiveProfileId = profile?.Id ?? string.Empty;

            if (syncConversation && SelectedConversation != null && profile != null)
            {
                SelectedConversation.ProfileId = profile.Id;
                SelectedConversation.ProfileDisplayName = profile.DisplayLabel;
                SelectedConversation.RefreshSummary();
            }

            if (persist)
                PersistState();
        }

        private CopilotConversationRecord EnsureConversation()
        {
            if (SelectedConversation != null)
                return SelectedConversation;

            var conversation = CreateConversation();
            SelectConversation(conversation, persist: false);
            return conversation;
        }

        private CopilotConversationRecord CreateConversation()
        {
            var profile = SelectedProfile ?? ResolveProfile(_config.ActiveProfileId) ?? Profiles.FirstOrDefault();
            var conversation = CopilotConversationRecord.CreateEmpty(profile?.Id ?? string.Empty, profile?.DisplayLabel ?? string.Empty);
            Conversations.Insert(GetUnpinnedInsertIndex(), conversation);
            return conversation;
        }

        private void UpdateConversationMetadata(CopilotConversationRecord conversation, bool touch)
        {
            if (touch)
                conversation.Touch();

            if (SelectedProfile != null)
            {
                conversation.ProfileId = SelectedProfile.Id;
                conversation.ProfileDisplayName = SelectedProfile.DisplayLabel;
            }

            conversation.RefreshSummary();
            BringConversationToFront(conversation);
        }

        private void BringConversationToFront(CopilotConversationRecord conversation)
        {
            MoveConversationToPreferredIndex(conversation);
            _config.ActiveConversationId = conversation.Id;
        }

        private void RenameConversation(CopilotConversationRecord? conversation)
        {
            if (conversation == null)
                return;

            var window = new CopilotTextInputWindow("重命名会话", "输入新的会话名称", conversation.Title)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.ResultText))
                return;

            conversation.SetCustomTitle(window.ResultText);
            PersistState();
        }

        private void DeleteConversation(CopilotConversationRecord? conversation)
        {
            if (conversation == null)
                return;

            if (MessageBox.Show(
                Application.Current.GetActiveWindow(),
                $"确定要删除会话“{conversation.Title}”吗？",
                "ColorVision",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            var currentIndex = Conversations.IndexOf(conversation);
            Conversations.Remove(conversation);

            if (Conversations.Count == 0)
            {
                var replacement = CreateConversation();
                SelectConversation(replacement, persist: false);
            }
            else
            {
                var nextIndex = Math.Clamp(currentIndex, 0, Conversations.Count - 1);
                SelectConversation(Conversations[nextIndex], persist: false);
            }

            PersistState();
        }

        private void TogglePinConversation(CopilotConversationRecord? conversation)
        {
            if (conversation == null)
                return;

            conversation.IsPinned = !conversation.IsPinned;
            MoveConversationToPreferredIndex(conversation);
            PersistState();
        }

        private void AddFileAttachment()
        {
            var conversation = EnsureConversation();
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                CheckFileExists = true,
                Filter = "所有文件|*.*",
            };

            if (dialog.ShowDialog(Application.Current.GetActiveWindow()) != true)
                return;

            foreach (var fileName in dialog.FileNames.Where(fileName => !string.IsNullOrWhiteSpace(fileName)))
            {
                if (conversation.Attachments.Any(item => item.Type == CopilotAttachmentType.File && string.Equals(item.Value, fileName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                conversation.Attachments.Add(CopilotAttachmentItem.CreateFile(fileName));
            }

            UpdateAttachmentsState(conversation);
        }

        private void AddContextAttachment()
        {
            var conversation = EnsureConversation();
            var window = new CopilotTextInputWindow("挂载上下文", "输入要附加到当前会话的上下文说明", string.Empty, isMultiline: true)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.ResultText))
                return;

            conversation.Attachments.Add(CopilotAttachmentItem.CreateContext(window.ResultText));
            UpdateAttachmentsState(conversation);
        }

        private void RemoveAttachment(CopilotAttachmentItem? attachment)
        {
            if (attachment == null || SelectedConversation == null)
                return;

            if (!SelectedConversation.Attachments.Remove(attachment))
                return;

            UpdateAttachmentsState(SelectedConversation);
        }

        private CopilotProfileConfig? ResolveProfile(string? profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return null;

            foreach (var profile in Profiles)
            {
                if (string.Equals(profile.Id, profileId, StringComparison.Ordinal))
                    return profile;
            }

            return null;
        }

        private void PersistState()
        {
            ConfigHandler.GetInstance().Save<CopilotConfig>();
            OnPropertyChanged(nameof(EmptyStateText));
            OnPropertyChanged(nameof(HasAttachments));
        }

        private void UpdateAttachmentsState(CopilotConversationRecord conversation)
        {
            conversation.RefreshSummary();
            OnPropertyChanged(nameof(Attachments));
            OnPropertyChanged(nameof(HasAttachments));
            PersistState();
        }

        private void MoveConversationToPreferredIndex(CopilotConversationRecord conversation)
        {
            var currentIndex = Conversations.IndexOf(conversation);
            if (currentIndex < 0)
                return;

            var targetIndex = conversation.IsPinned ? 0 : GetUnpinnedInsertIndex(conversation);
            if (currentIndex == targetIndex)
                return;

            Conversations.Move(currentIndex, targetIndex);
        }

        private int GetUnpinnedInsertIndex(CopilotConversationRecord? exclude = null)
        {
            var count = 0;
            foreach (var conversation in Conversations)
            {
                if (ReferenceEquals(conversation, exclude))
                    continue;

                if (!conversation.IsPinned)
                    break;

                count++;
            }

            return count;
        }

        private string BuildAttachmentContextBlock()
        {
            if (SelectedConversation == null || SelectedConversation.Attachments.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("以下是当前会话挂载的附加上下文。它们是用户明确提供的参考信息，回答时请按需使用：");

            foreach (var attachment in SelectedConversation.Attachments)
            {
                if (attachment.Type == CopilotAttachmentType.File)
                {
                    builder.AppendLine(BuildFileAttachmentBlock(attachment));
                    continue;
                }

                builder.AppendLine($"[上下文] {attachment.DisplayLabel}");
                builder.AppendLine(attachment.Value);
                builder.AppendLine();
            }

            return builder.ToString().Trim();
        }

        private static string BuildFileAttachmentBlock(CopilotAttachmentItem attachment)
        {
            try
            {
                if (!File.Exists(attachment.Value))
                    return $"[文件] {attachment.Value}\n文件当前不存在，无法读取。\n";

                var content = File.ReadAllText(attachment.Value);
                if (content.Length > AttachmentContentLimit)
                    content = content[..AttachmentContentLimit] + "\n...<已截断>";

                var fence = ResolveCodeFence(attachment.Value);
                return $"[文件] {attachment.Value}\n~~~{fence}\n{content}\n~~~\n";
            }
            catch (Exception ex)
            {
                return $"[文件] {attachment.Value}\n读取失败：{ex.Message}\n";
            }
        }

        private static string ResolveCodeFence(string filePath)
        {
            var extension = Path.GetExtension(filePath).Trim().TrimStart('.');
            return string.IsNullOrWhiteSpace(extension) ? string.Empty : extension;
        }
    }
}