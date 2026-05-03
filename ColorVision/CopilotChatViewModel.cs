using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ColorVision
{
    public class CopilotChatViewModel : ViewModelBase
    {
        private readonly CopilotChatService _chatService;
        private CancellationTokenSource? _currentRequestCts;

        public CopilotChatViewModel()
            : this(new CopilotChatService())
        {
        }

        public CopilotChatViewModel(CopilotChatService chatService)
        {
            _chatService = chatService;

            ProviderOptions = new ReadOnlyCollection<CopilotProviderOption>(new[]
            {
                new CopilotProviderOption { Label = "OpenAI Compatible", Value = CopilotProviderType.OpenAICompatible },
                new CopilotProviderOption { Label = "Anthropic Compatible", Value = CopilotProviderType.AnthropicCompatible },
            });

            Messages.CollectionChanged += Messages_CollectionChanged;
            LoadEditableSettingsFromConfig();
            IsSettingsOpen = !CopilotConfig.Instance.IsConfigured;
            StatusText = CopilotConfig.Instance.IsConfigured
                ? $"已准备就绪 · {GetProviderLabel(SelectedProviderType)} · {EditableModel}"
                : "请先配置 Base URL、API Key 和模型。";

            SendCommand = new RelayCommand(_ => _ = SendAsync());
            NewChatCommand = new RelayCommand(_ => StartNewChat());
            CancelCommand = new RelayCommand(_ => CancelCurrentReply());
            ToggleSettingsCommand = new RelayCommand(_ => IsSettingsOpen = !IsSettingsOpen);
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings(showStatusMessage: true));
        }

        public ObservableCollection<CopilotChatMessage> Messages { get; } = new();

        public IReadOnlyList<CopilotProviderOption> ProviderOptions { get; }

        public ICommand SendCommand { get; }

        public ICommand NewChatCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand ToggleSettingsCommand { get; }

        public ICommand SaveSettingsCommand { get; }

        public bool IsConversationEmpty => Messages.Count == 0;

        public CopilotProviderType SelectedProviderType
        {
            get => _selectedProviderType;
            set
            {
                _selectedProviderType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectionSummary));
            }
        }
        private CopilotProviderType _selectedProviderType;

        public string EditableBaseUrl
        {
            get => _editableBaseUrl;
            set
            {
                _editableBaseUrl = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectionSummary));
            }
        }
        private string _editableBaseUrl = string.Empty;

        public string EditableApiKey
        {
            get => _editableApiKey;
            set
            {
                _editableApiKey = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectionSummary));
            }
        }
        private string _editableApiKey = string.Empty;

        public string EditableModel
        {
            get => _editableModel;
            set
            {
                _editableModel = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectionSummary));
            }
        }
        private string _editableModel = string.Empty;

        public string EditableSystemPrompt
        {
            get => _editableSystemPrompt;
            set => SetProperty(ref _editableSystemPrompt, value ?? string.Empty);
        }
        private string _editableSystemPrompt = string.Empty;

        public int EditableMaxTokens
        {
            get => _editableMaxTokens;
            set => SetProperty(ref _editableMaxTokens, value);
        }
        private int _editableMaxTokens;

        public double EditableTemperature
        {
            get => _editableTemperature;
            set => SetProperty(ref _editableTemperature, value);
        }
        private double _editableTemperature;

        public string InputText
        {
            get => _inputText;
            set => SetProperty(ref _inputText, value ?? string.Empty);
        }
        private string _inputText = string.Empty;

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value ?? string.Empty);
        }
        private string _statusText = string.Empty;

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }
        private bool _isBusy;

        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set => SetProperty(ref _isSettingsOpen, value);
        }
        private bool _isSettingsOpen;

        public string ConnectionSummary
        {
            get
            {
                var provider = GetProviderLabel(SelectedProviderType);
                var model = string.IsNullOrWhiteSpace(EditableModel) ? "未设置模型" : EditableModel.Trim();
                var baseUrl = string.IsNullOrWhiteSpace(EditableBaseUrl) ? "未设置地址" : EditableBaseUrl.Trim();
                var auth = string.IsNullOrWhiteSpace(EditableApiKey) ? "未配置 Key" : "Key 已填写";
                return $"{provider} · {model} · {auth}\n{baseUrl}";
            }
        }

        private async Task SendAsync()
        {
            if (IsBusy)
                return;

            var prompt = (InputText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(prompt))
                return;

            SaveSettings(showStatusMessage: false);
            if (!CopilotConfig.Instance.IsConfigured)
            {
                StatusText = "请先填写 Base URL、API Key 和模型。";
                IsSettingsOpen = true;
                return;
            }

            var userMessage = new CopilotChatMessage(CopilotChatRole.User, prompt);
            Messages.Add(userMessage);

            var history = BuildConversationHistory();

            var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
            Messages.Add(assistantMessage);
            InputText = string.Empty;

            IsBusy = true;
            StatusText = $"{GetProviderLabel(CopilotConfig.Instance.ProviderType)} 正在回复...";

            _currentRequestCts?.Cancel();
            _currentRequestCts?.Dispose();
            _currentRequestCts = new CancellationTokenSource();

            try
            {
                await _chatService.StreamReplyAsync(
                    CopilotConfig.Instance,
                    history,
                    delta => assistantMessage.Content += delta,
                    _currentRequestCts.Token);

                if (string.IsNullOrWhiteSpace(assistantMessage.Content))
                    assistantMessage.Content = "接口返回成功，但没有可显示的文本。";

                StatusText = $"回复完成 · {CopilotConfig.Instance.Model}";
            }
            catch (OperationCanceledException)
            {
                if (string.IsNullOrWhiteSpace(assistantMessage.Content))
                    assistantMessage.Content = "已取消当前回复。";

                StatusText = "已取消当前回复。";
            }
            catch (Exception ex)
            {
                assistantMessage.Content = string.IsNullOrWhiteSpace(assistantMessage.Content)
                    ? $"请求失败：{ex.Message}"
                    : assistantMessage.Content;

                StatusText = ex.Message;
            }
            finally
            {
                IsBusy = false;
                _currentRequestCts?.Dispose();
                _currentRequestCts = null;
            }
        }

        private void StartNewChat()
        {
            CancelCurrentReply();
            Messages.Clear();
            StatusText = CopilotConfig.Instance.IsConfigured
                ? $"新会话已创建 · {CopilotConfig.Instance.Model}"
                : "请先配置 API 连接信息。";
        }

        private void CancelCurrentReply()
        {
            if (!IsBusy)
                return;

            _currentRequestCts?.Cancel();
        }

        private void SaveSettings(bool showStatusMessage)
        {
            var config = CopilotConfig.Instance;
            config.ProviderType = SelectedProviderType;
            config.BaseUrl = EditableBaseUrl;
            config.ApiKey = EditableApiKey;
            config.Model = EditableModel;
            config.SystemPrompt = EditableSystemPrompt;
            config.MaxTokens = EditableMaxTokens;
            config.Temperature = EditableTemperature;

            ConfigHandler.GetInstance().Save<CopilotConfig>();

            OnPropertyChanged(nameof(ConnectionSummary));
            if (showStatusMessage)
                StatusText = "配置已保存。";
        }

        private void LoadEditableSettingsFromConfig()
        {
            var config = CopilotConfig.Instance;
            SelectedProviderType = config.ProviderType;
            EditableBaseUrl = config.BaseUrl;
            EditableApiKey = config.ApiKey;
            EditableModel = config.Model;
            EditableSystemPrompt = config.SystemPrompt;
            EditableMaxTokens = config.MaxTokens;
            EditableTemperature = config.Temperature;
        }

        private List<CopilotRequestMessage> BuildConversationHistory()
        {
            return Messages
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .Select(message => new CopilotRequestMessage(
                    message.IsUser ? "user" : "assistant",
                    message.Content.Trim()))
                .ToList();
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsConversationEmpty));
        }

        private static string GetProviderLabel(CopilotProviderType providerType)
        {
            return providerType == CopilotProviderType.AnthropicCompatible
                ? "Anthropic Compatible"
                : "OpenAI Compatible";
        }
    }
}