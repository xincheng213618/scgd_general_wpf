using ColorVision.Common.MVVM;
using ColorVision.UI;
using HtmlAgilityPack;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ColorVision.Copilot
{
    public class CopilotChatViewModel : ViewModelBase
    {
        private const int AttachmentContentLimit = 12000;
        private const int MaxWebPageDownloadBytes = 256 * 1024;
        private const int MaxWebPageInjectionChars = 8000;
        private static readonly Regex HttpUrlRegex = new(@"https?://[^\s\""""'<>]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly char[] UrlTrimCharacters = { '.', ',', ';', ':', '!', '?', ')', ']', '}', '>', '"', '\'', '，', '。', '；', '：', '！', '？', '）', '】', '》', '、' };
        private static readonly HttpClient WebPageHttpClient = CreateWebPageHttpClient();

        private readonly record struct CopilotFetchedWebPage(string Url, string Title, string Description, string Content);

        private readonly CopilotChatService _chatService;
        private readonly CopilotConfig _config;
        private readonly CopilotChatStateStore _stateStore;
        private readonly ObservableCollection<CopilotChatMessage> _emptyMessages = new();
        private readonly ObservableCollection<CopilotAttachmentItem> _emptyAttachments = new();
        private CancellationTokenSource? _currentRequestCts;
        private CopilotChatState _state = new();
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
            _stateStore = CopilotChatStateStore.Instance;

            if (_config.EnsureInitialized())
                PersistConfig();

            _state = _stateStore.Load();
            if (_state.EnsureInitialized(_config))
                PersistState();

            var initialConversation = Conversations.Count > 0
                ? Conversations[0]
                : CopilotConversationRecord.CreateEmpty(_state.ActiveProfileId, string.Empty);

            if (Conversations.Count == 0)
                Conversations.Add(initialConversation);

            SelectConversation(Conversations.FirstOrDefault(conversation => conversation.Id == _state.ActiveConversationId) ?? initialConversation, persist: false);

            SendCommand = new RelayCommand(_ => _ = SendAsync());
            NewChatCommand = new RelayCommand(_ => StartNewChat());
            CancelCommand = new RelayCommand(_ => CancelCurrentReply());
            PrimaryActionCommand = new RelayCommand(_ => ExecutePrimaryAction());
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
            AddFileAttachmentCommand = new RelayCommand(_ => AddFileAttachment(), _ => !IsBusy);
            AddContextAttachmentCommand = new RelayCommand(_ => AddContextAttachment(), _ => !IsBusy);
            AddWebPageAttachmentCommand = new RelayCommand(_ => _ = AddWebPageAttachmentAsync(), _ => !IsBusy);
            PasteImageAttachmentCommand = new RelayCommand(_ => PasteImageAttachment(), _ => !IsBusy);
            CopyMessageCommand = new RelayCommand<CopilotChatMessage>(CopyMessage, message => message != null);
            RetryMessageCommand = new RelayCommand<CopilotChatMessage>(message => _ = RetryMessageAsync(message, refreshWebContext: false), CanRegenerateMessage);
            RefreshMessageCommand = new RelayCommand<CopilotChatMessage>(message => _ = RetryMessageAsync(message, refreshWebContext: true), CanRegenerateMessage);
            RemoveAttachmentCommand = new RelayCommand<CopilotAttachmentItem>(RemoveAttachment, attachment => !IsBusy && attachment != null);
            RenameConversationCommand = new RelayCommand<CopilotConversationRecord>(RenameConversation, conversation => !IsBusy && conversation != null);
            DeleteConversationCommand = new RelayCommand<CopilotConversationRecord>(DeleteConversation, conversation => !IsBusy && conversation != null);
            TogglePinConversationCommand = new RelayCommand<CopilotConversationRecord>(TogglePinConversation, conversation => !IsBusy && conversation != null);
        }

        public ObservableCollection<CopilotConversationRecord> Conversations => _state.Conversations;

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

        public ICommand AddWebPageAttachmentCommand { get; }

        public ICommand PasteImageAttachmentCommand { get; }

        public ICommand CopyMessageCommand { get; }

        public ICommand RetryMessageCommand { get; }

        public ICommand RefreshMessageCommand { get; }

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

            IsBusy = true;

            _currentRequestCts?.Cancel();
            _currentRequestCts?.Dispose();
            _currentRequestCts = new CancellationTokenSource();

            CopilotChatMessage? assistantMessage = null;

            try
            {
                var requestContent = await BuildUserRequestContentAsync(prompt, _currentRequestCts.Token);

                var userMessage = new CopilotChatMessage(CopilotChatRole.User, prompt)
                {
                    RequestContent = requestContent,
                };
                Messages.Add(userMessage);
                UpdateConversationMetadata(conversation, touch: true);
                PersistState();

                var history = BuildConversationHistory();

                assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
                {
                    AssistantName = ResolveAssistantHeader(requestProfile),
                };
                Messages.Add(assistantMessage);
                InputText = string.Empty;

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
                QueueConversationTitleGeneration(conversation, requestProfile);
            }
            catch (OperationCanceledException)
            {
                if (assistantMessage == null)
                    return;

                assistantMessage.IsReasoningInProgress = false;

                if (string.IsNullOrWhiteSpace(assistantMessage.Content))
                    assistantMessage.Content = "已取消当前回复。";

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
            }
            catch (Exception ex)
            {
                if (assistantMessage == null)
                {
                    assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, $"请求失败：{ex.Message}")
                    {
                        AssistantName = ResolveAssistantHeader(requestProfile),
                    };
                    Messages.Add(assistantMessage);
                }

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
            var preferredConversationId = SelectedConversation?.Id ?? _state.ActiveConversationId;
            var preferredProfileId = SelectedProfile?.Id ?? _state.ActiveProfileId;

            if (_config.EnsureInitialized())
                PersistConfig();

            _state = _stateStore.Load();
            if (_state.EnsureInitialized(_config))
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
                .Where(message => !string.IsNullOrWhiteSpace(message.ModelContent))
                .Select(message => new CopilotRequestMessage(
                    message.IsUser ? "user" : "assistant",
                    message.ModelContent.Trim())));

            return history;
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsConversationEmpty));
            CommandManager.InvalidateRequerySuggested();
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

            _state.ActiveConversationId = conversation?.Id ?? string.Empty;

            var profile = ResolveProfile(preferredProfileId)
                ?? ResolveProfile(conversation?.ProfileId)
                ?? ResolveProfile(_state.ActiveProfileId)
                ?? Profiles.FirstOrDefault();

            SelectProfile(profile, syncConversation: false, persist: false);

            var shouldPersist = persist;

            if (conversation != null && profile != null)
            {
                conversation.ProfileId = profile.Id;
                conversation.ProfileDisplayName = profile.DisplayLabel;
                conversation.RefreshSummary();
            }

            if (conversation != null && EnsureAssistantHeaders(conversation, profile))
                shouldPersist = true;

            if (shouldPersist)
                PersistState();
        }

        private void SelectProfile(CopilotProfileConfig? profile, bool syncConversation, bool persist)
        {
            if (ReferenceEquals(_selectedProfile, profile))
                return;

            _selectedProfile = profile;
            OnPropertyChanged(nameof(SelectedProfile));

            _state.ActiveProfileId = profile?.Id ?? string.Empty;

            var shouldPersist = persist;

            if (syncConversation && SelectedConversation != null && profile != null)
            {
                SelectedConversation.ProfileId = profile.Id;
                SelectedConversation.ProfileDisplayName = profile.DisplayLabel;
                SelectedConversation.RefreshSummary();

                if (EnsureAssistantHeaders(SelectedConversation, profile))
                    shouldPersist = true;
            }

            if (shouldPersist)
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
            var profile = SelectedProfile ?? ResolveProfile(_state.ActiveProfileId) ?? Profiles.FirstOrDefault();
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

        private void QueueConversationTitleGeneration(CopilotConversationRecord conversation, CopilotProfileConfig requestProfile)
        {
            if (!ShouldGenerateConversationTitle(conversation))
                return;

            _ = GenerateConversationTitleAsync(conversation, requestProfile.Clone());
        }

        private static bool ShouldGenerateConversationTitle(CopilotConversationRecord conversation)
        {
            if (conversation.HasCustomTitle)
                return false;

            var userMessageCount = conversation.Messages.Count(message => message.Role == CopilotChatRole.User && !string.IsNullOrWhiteSpace(message.Content));
            var assistantMessageCount = conversation.Messages.Count(message => message.Role == CopilotChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Content));
            return userMessageCount == 1 && assistantMessageCount == 1;
        }

        private async Task GenerateConversationTitleAsync(CopilotConversationRecord conversation, CopilotProfileConfig requestProfile)
        {
            var titlePrompt = BuildConversationTitlePrompt(conversation);
            if (string.IsNullOrWhiteSpace(titlePrompt))
                return;

            try
            {
                requestProfile.SystemPrompt = "你是会话标题生成器。请根据给定对话生成一个简短、自然的中文标题。只返回标题本身，不要解释。";
                requestProfile.MaxTokens = Math.Min(requestProfile.MaxTokens, 32);
                requestProfile.Temperature = 0.2;

                var titleBuilder = new StringBuilder();
                await _chatService.StreamReplyAsync(
                    requestProfile,
                    new[]
                    {
                        new CopilotRequestMessage("user", titlePrompt),
                    },
                    delta =>
                    {
                        if (delta.HasContent)
                            titleBuilder.Append(delta.Content);
                    },
                    CancellationToken.None);

                var generatedTitle = NormalizeGeneratedTitle(titleBuilder.ToString());
                if (string.IsNullOrWhiteSpace(generatedTitle) || Application.Current == null)
                    return;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!Conversations.Contains(conversation) || conversation.HasCustomTitle)
                        return;

                    conversation.SetGeneratedTitle(generatedTitle);
                    PersistState();
                });
            }
            catch
            {
            }
        }

        private void BringConversationToFront(CopilotConversationRecord conversation)
        {
            MoveConversationToPreferredIndex(conversation);
            _state.ActiveConversationId = conversation.Id;
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

            RemoveManagedAttachmentFiles(conversation.Attachments);

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

        private async Task AddWebPageAttachmentAsync()
        {
            var conversation = EnsureConversation();
            var window = new CopilotTextInputWindow("挂载网页", "输入要抓取并附加到当前会话的网页地址", "https://")
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.ResultText))
                return;

            var url = NormalizeWebPageUrl(window.ResultText);
            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    "网页地址格式不正确。",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var webPage = await LoadWebPageContentAsync(url, CancellationToken.None);
                var attachment = CopilotAttachmentItem.CreateWebPage(url, webPage.Title, BuildStoredWebPageContent(webPage));

                var existingAttachment = conversation.Attachments.FirstOrDefault(item => item.Type == CopilotAttachmentType.WebPage && string.Equals(item.Source, url, StringComparison.OrdinalIgnoreCase));
                if (existingAttachment != null)
                {
                    existingAttachment.Title = attachment.Title;
                    existingAttachment.Value = attachment.Value;
                    existingAttachment.Source = attachment.Source;
                    existingAttachment.CreatedAt = attachment.CreatedAt;
                }
                else
                {
                    conversation.Attachments.Add(attachment);
                }

                UpdateAttachmentsState(conversation);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"抓取网页失败：{ex.Message}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void PasteImageAttachment()
        {
            if (TryPasteClipboardImageAttachment())
                return;

            MessageBox.Show(
                Application.Current.GetActiveWindow(),
                "剪贴板里没有可挂载的图片。",
                "ColorVision",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public bool TryPasteClipboardImageAttachment()
        {
            if (IsBusy)
                return false;

            try
            {
                if (!Clipboard.ContainsImage())
                    return false;

                var image = Clipboard.GetImage();
                if (image == null)
                    return false;

                var conversation = EnsureConversation();
                var imagePath = SaveClipboardImage(image);
                var title = $"粘贴图片 {DateTime.Now:HH:mm:ss}";
                conversation.Attachments.Add(CopilotAttachmentItem.CreateImage(imagePath, title));
                UpdateAttachmentsState(conversation);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"粘贴图片失败：{ex.Message}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
        }

        private void CopyMessage(CopilotChatMessage? message)
        {
            if (message == null)
                return;

            var text = BuildMessageClipboardText(message);
            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"复制消息失败：{ex.Message}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private bool CanRegenerateMessage(CopilotChatMessage? message)
        {
            if (IsBusy || message == null || SelectedConversation == null || SelectedProfile == null || !SelectedProfile.IsConfigured)
                return false;

            return TryResolveLatestTurn(message, out _, out _, out _);
        }

        private async Task RetryMessageAsync(CopilotChatMessage? message, bool refreshWebContext)
        {
            if (!TryResolveLatestTurn(message, out var conversation, out var userMessage, out var assistantMessage))
                return;

            if (SelectedProfile == null || !SelectedProfile.IsConfigured)
            {
                OpenSettings();
                return;
            }

            var prompt = (userMessage.Content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(prompt))
                return;

            var requestProfile = SelectedProfile.Clone();
            conversation.ProfileId = requestProfile.Id;
            conversation.ProfileDisplayName = requestProfile.DisplayLabel;

            IsBusy = true;

            _currentRequestCts?.Cancel();
            _currentRequestCts?.Dispose();
            _currentRequestCts = new CancellationTokenSource();

            CopilotChatMessage? replacementAssistantMessage = null;

            try
            {
                if (refreshWebContext || string.IsNullOrWhiteSpace(userMessage.RequestContent))
                    userMessage.RequestContent = await BuildUserRequestContentAsync(prompt, _currentRequestCts.Token);

                if (assistantMessage != null)
                    conversation.Messages.Remove(assistantMessage);

                replacementAssistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
                {
                    AssistantName = ResolveAssistantHeader(requestProfile),
                };
                conversation.Messages.Add(replacementAssistantMessage);

                var history = BuildConversationHistory();

                await _chatService.StreamReplyAsync(
                    requestProfile,
                    history,
                    delta => ApplyAssistantDelta(replacementAssistantMessage, delta),
                    _currentRequestCts.Token);

                replacementAssistantMessage.IsReasoningInProgress = false;

                if (string.IsNullOrWhiteSpace(replacementAssistantMessage.Content))
                {
                    replacementAssistantMessage.Content = replacementAssistantMessage.HasReasoning
                        ? "未收到最终回答，只拿到了推理内容。"
                        : "接口返回成功，但没有可显示的文本。";
                }

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
                QueueConversationTitleGeneration(conversation, requestProfile);
            }
            catch (OperationCanceledException)
            {
                if (replacementAssistantMessage == null)
                    return;

                replacementAssistantMessage.IsReasoningInProgress = false;

                if (string.IsNullOrWhiteSpace(replacementAssistantMessage.Content))
                    replacementAssistantMessage.Content = "已取消当前回复。";

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
            }
            catch (Exception ex)
            {
                if (replacementAssistantMessage == null)
                {
                    replacementAssistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, $"请求失败：{ex.Message}")
                    {
                        AssistantName = ResolveAssistantHeader(requestProfile),
                    };
                    conversation.Messages.Add(replacementAssistantMessage);
                }

                replacementAssistantMessage.IsReasoningInProgress = false;
                replacementAssistantMessage.Content = string.IsNullOrWhiteSpace(replacementAssistantMessage.Content)
                    ? $"请求失败：{ex.Message}"
                    : replacementAssistantMessage.Content;

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

        private bool TryResolveLatestTurn(CopilotChatMessage? message, out CopilotConversationRecord conversation, out CopilotChatMessage userMessage, out CopilotChatMessage? assistantMessage)
        {
            conversation = SelectedConversation!;
            userMessage = null!;
            assistantMessage = null;

            if (message == null || SelectedConversation == null)
                return false;

            var messages = SelectedConversation.Messages;
            var targetIndex = messages.IndexOf(message);
            if (targetIndex < 0)
                return false;

            var userIndex = message.IsUser ? targetIndex : FindPreviousUserMessageIndex(messages, targetIndex - 1);
            if (userIndex < 0)
                return false;

            var resolvedAssistantIndex = userIndex + 1 < messages.Count && !messages[userIndex + 1].IsUser
                ? userIndex + 1
                : -1;

            if (!message.IsUser && resolvedAssistantIndex != targetIndex)
                return false;

            var turnEndIndex = resolvedAssistantIndex >= 0 ? resolvedAssistantIndex : userIndex;
            if (turnEndIndex != messages.Count - 1)
                return false;

            conversation = SelectedConversation;
            userMessage = messages[userIndex];
            assistantMessage = resolvedAssistantIndex >= 0 ? messages[resolvedAssistantIndex] : null;
            return true;
        }

        private static int FindPreviousUserMessageIndex(ObservableCollection<CopilotChatMessage> messages, int startIndex)
        {
            for (var index = startIndex; index >= 0; index--)
            {
                if (messages[index].IsUser)
                    return index;
            }

            return -1;
        }

        private static string BuildMessageClipboardText(CopilotChatMessage message)
        {
            var content = (message.Content ?? string.Empty).Trim();
            var reasoning = (message.ReasoningContent ?? string.Empty).Trim();

            if (message.IsUser || string.IsNullOrWhiteSpace(reasoning))
                return content;

            if (string.IsNullOrWhiteSpace(content))
                return reasoning;

            return string.Join(Environment.NewLine, new[]
            {
                "推理：",
                reasoning,
                string.Empty,
                "回答：",
                content,
            });
        }

        private void RemoveAttachment(CopilotAttachmentItem? attachment)
        {
            if (attachment == null || SelectedConversation == null)
                return;

            if (!SelectedConversation.Attachments.Remove(attachment))
                return;

            TryDeleteManagedAttachmentFile(attachment);

            UpdateAttachmentsState(SelectedConversation);
        }

        private static bool EnsureAssistantHeaders(CopilotConversationRecord conversation, CopilotProfileConfig? profile)
        {
            var assistantHeader = ResolveAssistantHeader(conversation, profile);
            var changed = false;

            foreach (var message in conversation.Messages)
            {
                if (message.IsUser || !string.IsNullOrWhiteSpace(message.AssistantName))
                    continue;

                message.AssistantName = assistantHeader;
                changed = true;
            }

            return changed;
        }

        private static string ResolveAssistantHeader(CopilotProfileConfig profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.Model))
                return profile.Model;

            if (!string.IsNullOrWhiteSpace(profile.DisplayLabel))
                return profile.DisplayLabel;

            return "AI";
        }

        private static string ResolveAssistantHeader(CopilotConversationRecord conversation, CopilotProfileConfig? profile)
        {
            if (profile != null)
                return ResolveAssistantHeader(profile);

            if (!string.IsNullOrWhiteSpace(conversation.ProfileDisplayName))
                return conversation.ProfileDisplayName;

            if (!string.IsNullOrWhiteSpace(conversation.ProfileId))
                return conversation.ProfileId;

            return "AI";
        }

        private static string BuildConversationTitlePrompt(CopilotConversationRecord conversation)
        {
            var firstUserMessage = conversation.Messages.FirstOrDefault(message => message.Role == CopilotChatRole.User && !string.IsNullOrWhiteSpace(message.Content));
            var firstAssistantMessage = conversation.Messages.FirstOrDefault(message => message.Role == CopilotChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Content));
            if (firstUserMessage == null || firstAssistantMessage == null)
                return string.Empty;

            return string.Join(Environment.NewLine, new[]
            {
                "请为下面这段对话生成一个简短中文会话标题。",
                "要求：6 到 14 个字，直接返回标题，不要解释，不要引号，不要句号。",
                $"用户：{TruncateForTitlePrompt(firstUserMessage.Content, 180)}",
                $"助手：{TruncateForTitlePrompt(firstAssistantMessage.Content, 260)}",
            });
        }

        private static string NormalizeGeneratedTitle(string rawTitle)
        {
            var title = (rawTitle ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            title = title.Trim('"', '\'', '“', '”', '‘', '’', '《', '》', '【', '】', '「', '」');

            if (title.StartsWith("标题", StringComparison.Ordinal))
            {
                var separatorIndex = title.IndexOfAny(new[] { ':', '：', '-', ' ' });
                if (separatorIndex >= 0 && separatorIndex < title.Length - 1)
                    title = title[(separatorIndex + 1)..].Trim();
            }

            if (title.Length > 18)
                title = title[..18].Trim();

            return title;
        }

        private static string TruncateForTitlePrompt(string content, int maxLength)
        {
            var normalized = (content ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length <= maxLength)
                return normalized;

            return normalized[..maxLength] + "...";
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
            _stateStore.Save(_state);
            OnPropertyChanged(nameof(HasAttachments));
        }

        private void PersistConfig()
        {
            ConfigHandler.GetInstance().Save<CopilotConfig>();
            OnPropertyChanged(nameof(EmptyStateText));
            OnPropertyChanged(nameof(CanSelectProfile));
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

                if (attachment.Type == CopilotAttachmentType.Image)
                {
                    builder.AppendLine(BuildImageAttachmentBlock(attachment));
                    continue;
                }

                if (attachment.Type == CopilotAttachmentType.WebPage)
                {
                    builder.AppendLine(BuildWebPageAttachmentBlock(attachment));
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

        private static string BuildImageAttachmentBlock(CopilotAttachmentItem attachment)
        {
            if (!File.Exists(attachment.Value))
                return $"[图片] {attachment.DisplayLabel}\n本地图片附件不存在：{attachment.Value}\n";

            return string.Join(Environment.NewLine, new[]
            {
                $"[图片] {attachment.DisplayLabel}",
                $"本地图片路径：{attachment.Value}",
                "当前版本会在界面显示图片预览，但不会自动把像素内容上传给模型。",
                string.Empty,
            });
        }

        private static string BuildWebPageAttachmentBlock(CopilotAttachmentItem attachment)
        {
            var content = attachment.Value ?? string.Empty;
            if (content.Length > AttachmentContentLimit)
                content = content[..AttachmentContentLimit] + "\n...<已截断>";

            return string.Join(Environment.NewLine, new[]
            {
                $"[网页] {attachment.DisplayLabel}",
                $"来源：{attachment.Source}",
                content,
                string.Empty,
            });
        }

        private async Task<string> BuildUserRequestContentAsync(string prompt, CancellationToken cancellationToken)
        {
            var urls = ExtractHttpUrls(prompt);
            if (urls.Count == 0)
                return prompt;

            var builder = new StringBuilder(prompt);
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("[本地网页上下文注入]");
            builder.AppendLine("以下网页内容由本地程序在发送前实际抓取。你必须只基于这些抓取结果回答网页问题，不要再说无法浏览互联网；如果抓取失败，或抓取内容里没有相关信息，必须明确说明无法基于真实网页内容完成分析，不能假设网页包含未抓取到的信息。");

            var remainingCharacters = MaxWebPageInjectionChars;
            foreach (var url in urls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var contextBlock = await BuildWebPageContextBlockAsync(url, cancellationToken);
                if (contextBlock.Length > remainingCharacters)
                {
                    builder.AppendLine();
                    builder.Append(contextBlock[..remainingCharacters]);
                    builder.AppendLine();
                    builder.AppendLine("...<网页上下文已截断>");
                    break;
                }

                builder.AppendLine();
                builder.AppendLine(contextBlock);
                remainingCharacters -= contextBlock.Length;

                if (remainingCharacters <= 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("...<网页上下文已截断>");
                    break;
                }
            }

            return builder.ToString().TrimEnd();
        }

        private async Task<string> BuildWebPageContextBlockAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                var page = await LoadWebPageContentAsync(url, cancellationToken);
                return BuildFetchedWebPageContextBlock(page);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return BuildFailedWebPageContextBlock(url, ex.Message);
            }
        }

        private static string BuildFetchedWebPageContextBlock(CopilotFetchedWebPage page)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[网页抓取成功] {page.Url}");
            builder.AppendLine($"标题：{page.Title}");
            if (!string.IsNullOrWhiteSpace(page.Description))
                builder.AppendLine($"描述：{page.Description}");
            builder.AppendLine("正文：");
            builder.AppendLine(page.Content);
            return builder.ToString().TrimEnd();
        }

        private static string BuildFailedWebPageContextBlock(string url, string failureMessage)
        {
            return string.Join(Environment.NewLine, new[]
            {
                $"[网页抓取失败] {url}",
                $"失败原因：{failureMessage}",
                "本地程序未能抓取到真实网页内容。你必须明确说明无法基于真实网页内容分析该网页，不能假设网页中存在任何未抓取到的信息。",
            });
        }

        private static List<string> ExtractHttpUrls(string text)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return results;

            foreach (Match match in HttpUrlRegex.Matches(text))
            {
                var candidate = match.Value.Trim().TrimEnd(UrlTrimCharacters);
                if (!string.IsNullOrWhiteSpace(candidate)
                    && !results.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                {
                    results.Add(candidate);
                }
            }

            return results;
        }

        private static string BuildStoredWebPageContent(CopilotFetchedWebPage page)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(page.Description))
            {
                builder.AppendLine($"描述：{page.Description}");
                builder.AppendLine();
            }

            builder.Append(page.Content);
            return builder.ToString();
        }

        private string SaveClipboardImage(BitmapSource image)
        {
            Directory.CreateDirectory(_stateStore.AttachmentDirectoryPath);

            var filePath = Path.Combine(
                _stateStore.AttachmentDirectoryPath,
                $"clipboard-{DateTime.Now:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.png");

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            encoder.Save(stream);

            return filePath;
        }

        private void RemoveManagedAttachmentFiles(IEnumerable<CopilotAttachmentItem> attachments)
        {
            foreach (var attachment in attachments.ToList())
            {
                TryDeleteManagedAttachmentFile(attachment);
            }
        }

        private void TryDeleteManagedAttachmentFile(CopilotAttachmentItem attachment)
        {
            if (!attachment.IsStoredImageFile || string.IsNullOrWhiteSpace(attachment.Value))
                return;

            try
            {
                var attachmentRoot = Path.GetFullPath(_stateStore.AttachmentDirectoryPath);
                var filePath = Path.GetFullPath(attachment.Value);
                if (!filePath.StartsWith(attachmentRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
                    return;

                File.Delete(filePath);
            }
            catch
            {
            }
        }

        private static string ResolveCodeFence(string filePath)
        {
            var extension = Path.GetExtension(filePath).Trim().TrimStart('.');
            return string.IsNullOrWhiteSpace(extension) ? string.Empty : extension;
        }

        private static HttpClient CreateWebPageHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ColorVision-Copilot/1.0");
            return client;
        }

        private static string NormalizeWebPageUrl(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "https://" + normalized;
            }

            return normalized;
        }

        private static async Task<CopilotFetchedWebPage> LoadWebPageContentAsync(string url, CancellationToken cancellationToken)
        {
            var uri = NormalizeAndValidateWebPageUri(url);
            await EnsureAllowedWebPageUriAsync(uri, cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await WebPageHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(mediaType)
                && !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                && !mediaType.Contains("text/plain", StringComparison.OrdinalIgnoreCase)
                && !mediaType.Contains("xhtml", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"目标地址返回的内容类型不受支持：{mediaType}");
            }

            var html = await ReadWebPageContentAsync(response, cancellationToken);
            return ExtractWebPageContent(uri, html);
        }

        private static CopilotFetchedWebPage ExtractWebPageContent(Uri uri, string html)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html ?? string.Empty);

            foreach (var removableNode in document.DocumentNode.SelectNodes("//script|//style|//noscript|//svg") ?? Enumerable.Empty<HtmlNode>())
            {
                removableNode.Remove();
            }

            var title = HtmlEntity.DeEntitize(document.DocumentNode.SelectSingleNode("//title")?.InnerText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(title))
                title = uri.Host;

            var description = ExtractWebPageDescription(document);

            var bodyNode = document.DocumentNode.SelectSingleNode("//main")
                ?? document.DocumentNode.SelectSingleNode("//article")
                ?? document.DocumentNode.SelectSingleNode("//body")
                ?? document.DocumentNode;
            var lines = bodyNode
                .DescendantsAndSelf()
                .Where(node => node.NodeType == HtmlNodeType.Text)
                .Select(node => HtmlEntity.DeEntitize(node.InnerText))
                .Select(NormalizeWebPageLine)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            var content = string.Join(Environment.NewLine, lines).Trim();
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("未能提取网页正文。当前网页可能需要脚本渲染。");

            if (content.Length > MaxWebPageInjectionChars)
                content = content[..MaxWebPageInjectionChars] + "\n...<已截断>";

            return new CopilotFetchedWebPage(uri.ToString(), title, description, content);
        }

        private static string ExtractWebPageDescription(HtmlDocument document)
        {
            var descriptionNode = document.DocumentNode.SelectSingleNode("//meta[translate(@name, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='description']")
                ?? document.DocumentNode.SelectSingleNode("//meta[translate(@property, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='og:description']")
                ?? document.DocumentNode.SelectSingleNode("//meta[translate(@name, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='twitter:description']");

            return HtmlEntity.DeEntitize(descriptionNode?.GetAttributeValue("content", string.Empty) ?? string.Empty).Trim();
        }

        private static string NormalizeWebPageLine(string value)
        {
            var normalized = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length == 0)
                return string.Empty;

            while (normalized.Contains("  ", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
            }

            return normalized;
        }

        private static Uri NormalizeAndValidateWebPageUri(string url)
        {
            var normalized = NormalizeWebPageUrl(url);
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("网页地址格式不正确。");

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("只允许抓取 http/https 网页地址。");
            }

            if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("禁止抓取 localhost 或本机回环地址。");

            return uri;
        }

        private static async Task EnsureAllowedWebPageUriAsync(Uri uri, CancellationToken cancellationToken)
        {
            if (IPAddress.TryParse(uri.Host, out var parsedAddress))
            {
                if (IsBlockedWebPageAddress(parsedAddress))
                    throw new InvalidOperationException("禁止抓取内网、本地或保留 IP 地址。");

                return;
            }

            var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
            if (addresses.Length == 0)
                throw new InvalidOperationException("无法解析目标网页地址。");

            if (addresses.Any(IsBlockedWebPageAddress))
                throw new InvalidOperationException("目标网页地址解析到了本地、内网或保留 IP，已拒绝访问。");
        }

        private static bool IsBlockedWebPageAddress(IPAddress address)
        {
            if (IPAddress.IsLoopback(address))
                return true;

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
                    return true;

                var bytes = address.GetAddressBytes();
                return bytes.Length > 0 && (bytes[0] & 0xFE) == 0xFC;
            }

            if (address.AddressFamily != AddressFamily.InterNetwork)
                return true;

            var bytesV4 = address.GetAddressBytes();
            if (bytesV4.Length != 4)
                return true;

            return bytesV4[0] switch
            {
                0 => true,
                10 => true,
                127 => true,
                169 when bytesV4[1] == 254 => true,
                172 when bytesV4[1] >= 16 && bytesV4[1] <= 31 => true,
                192 when bytesV4[1] == 168 => true,
                100 when bytesV4[1] >= 64 && bytesV4[1] <= 127 => true,
                _ => false,
            };
        }

        private static async Task<string> ReadWebPageContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            var totalBytes = 0;

            while (true)
            {
                var bytesRead = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
                if (bytesRead <= 0)
                    break;

                totalBytes += bytesRead;
                if (totalBytes > MaxWebPageDownloadBytes)
                    throw new InvalidOperationException($"网页内容超过大小限制（{MaxWebPageDownloadBytes / 1024} KB）。");

                await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
            }

            buffer.Position = 0;
            using var reader = new StreamReader(buffer, Encoding.UTF8, true);
            return await reader.ReadToEndAsync(cancellationToken);
        }
    }
}