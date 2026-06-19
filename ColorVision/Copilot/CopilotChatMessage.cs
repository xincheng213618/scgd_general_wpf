using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Copilot
{
    public enum CopilotChatRole
    {
        User,
        Assistant,
    }

    public enum CopilotAttachmentType
    {
        File,
        Context,
        Image,
        WebPage,
    }

    public readonly record struct CopilotStreamDelta(string ReasoningContent, string Content)
    {
        public static CopilotStreamDelta Empty => new(string.Empty, string.Empty);

        public bool HasReasoning => !string.IsNullOrWhiteSpace(ReasoningContent);

        public bool HasContent => !string.IsNullOrWhiteSpace(Content);

        public bool HasAny => HasReasoning || HasContent;
    }

    public readonly record struct CopilotTokenUsage(int InputTokens, int OutputTokens, int TotalTokens)
    {
        public static CopilotTokenUsage Empty => new(0, 0, 0);

        public bool HasAny => InputTokens > 0 || OutputTokens > 0 || TotalTokens > 0;

        public int EffectiveTotalTokens => TotalTokens > 0 ? TotalTokens : Math.Max(0, InputTokens) + Math.Max(0, OutputTokens);

        public CopilotTokenUsage MergeProgress(CopilotTokenUsage other)
        {
            if (!HasAny)
                return other;

            if (!other.HasAny)
                return this;

            var inputTokens = other.InputTokens > 0 ? Math.Max(InputTokens, other.InputTokens) : InputTokens;
            var outputTokens = other.OutputTokens > 0 ? Math.Max(OutputTokens, other.OutputTokens) : OutputTokens;
            var totalTokens = other.TotalTokens > 0
                ? Math.Max(EffectiveTotalTokens, other.TotalTokens)
                : Math.Max(0, inputTokens) + Math.Max(0, outputTokens);

            return new CopilotTokenUsage(inputTokens, outputTokens, totalTokens);
        }

        public CopilotTokenUsage Add(CopilotTokenUsage other)
        {
            if (!HasAny)
                return other;

            if (!other.HasAny)
                return this;

            var inputTokens = Math.Max(0, InputTokens) + Math.Max(0, other.InputTokens);
            var outputTokens = Math.Max(0, OutputTokens) + Math.Max(0, other.OutputTokens);
            return new CopilotTokenUsage(inputTokens, outputTokens, inputTokens + outputTokens);
        }

        public static string FormatCount(int value)
        {
            var normalized = Math.Max(0, value);
            return normalized >= 1000
                ? $"{normalized / 1000d:0.#}k"
                : normalized.ToString();
        }
    }

    public readonly record struct CopilotChatReply(CopilotStreamDelta Delta, CopilotTokenUsage Usage)
    {
        public static CopilotChatReply Empty => new(CopilotStreamDelta.Empty, CopilotTokenUsage.Empty);

        public string ReasoningContent => Delta.ReasoningContent;

        public string Content => Delta.Content;
    }

    public sealed class CopilotChatMessage : ViewModelBase
    {
        private static readonly char[] ExecutionLineSeparators = { '\r', '\n' };

        public CopilotChatMessage()
        {
        }

        public CopilotChatMessage(CopilotChatRole role, string content)
        {
            Role = role;
            _content = content ?? string.Empty;
            CreatedAt = DateTime.Now;
        }

        public CopilotChatRole Role
        {
            get => _role;
            set
            {
                if (SetProperty(ref _role, value))
                {
                    OnPropertyChanged(nameof(IsUser));
                    OnPropertyChanged(nameof(Header));
                }
            }
        }
        private CopilotChatRole _role;

        [JsonIgnore]
        public bool IsUser => Role == CopilotChatRole.User;

        [JsonIgnore]
        public string Header => IsUser ? CopilotUiText.UserHeader : string.IsNullOrWhiteSpace(AssistantName) ? "AI" : AssistantName;

        public string AssistantName
        {
            get => _assistantName;
            set
            {
                if (SetProperty(ref _assistantName, value ?? string.Empty))
                    OnPropertyChanged(nameof(Header));
            }
        }
        private string _assistantName = string.Empty;

        public DateTime CreatedAt
        {
            get => _createdAt;
            set
            {
                if (SetProperty(ref _createdAt, value))
                    OnPropertyChanged(nameof(TimeLabel));
            }
        }
        private DateTime _createdAt = DateTime.Now;

        [JsonIgnore]
        public string TimeLabel => CreatedAt.ToString("HH:mm");

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value ?? string.Empty);
        }
        private string _content;

        public string RequestContent
        {
            get => _requestContent;
            set => SetProperty(ref _requestContent, value ?? string.Empty);
        }
        private string _requestContent = string.Empty;

        public CopilotAgentMode RequestMode
        {
            get => _requestMode;
            set => SetProperty(ref _requestMode, value);
        }
        private CopilotAgentMode _requestMode = CopilotAgentMode.Chat;

        [JsonIgnore]
        public string ModelContent => string.IsNullOrWhiteSpace(RequestContent) ? Content : RequestContent;

        public string ExecutionContent
        {
            get => _executionContent;
            set
            {
                if (SetProperty(ref _executionContent, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasExecutionTrace));
                    OnPropertyChanged(nameof(HasExecutionFailures));
                    OnPropertyChanged(nameof(ExecutionSummary));
                    OnPropertyChanged(nameof(ExecutionSummaryToolTip));
                }
            }
        }
        private string _executionContent = string.Empty;

        [JsonIgnore]
        public bool HasExecutionTrace => !string.IsNullOrWhiteSpace(ExecutionContent);

        [JsonIgnore]
        public bool HasExecutionFailures => AnalyzeExecutionTrace(ExecutionContent).FailedCount > 0;

        public bool IsExecutionExpanded
        {
            get => _isExecutionExpanded;
            set => SetProperty(ref _isExecutionExpanded, value);
        }
        private bool _isExecutionExpanded = true;

        public bool IsExecutionInProgress
        {
            get => _isExecutionInProgress;
            set
            {
                if (SetProperty(ref _isExecutionInProgress, value))
                {
                    OnPropertyChanged(nameof(ExecutionHeader));
                    OnPropertyChanged(nameof(ExecutionSummary));
                    OnPropertyChanged(nameof(ExecutionSummaryToolTip));
                }
            }
        }
        private bool _isExecutionInProgress;

        [JsonIgnore]
        public string ExecutionHeader => IsExecutionInProgress ? CopilotUiText.ExecutionInProgressHeader : CopilotUiText.ExecutionHeader;

        [JsonIgnore]
        public string ExecutionSummary => BuildExecutionSummary(ExecutionContent, IsExecutionInProgress);

        [JsonIgnore]
        public string ExecutionSummaryToolTip
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ExecutionContent))
                    return ExecutionSummary;

                return $"{ExecutionHeader}: {ExecutionSummary}{Environment.NewLine}{Environment.NewLine}{TrimForTooltip(ExecutionContent)}";
            }
        }

        public string ReasoningContent
        {
            get => _reasoningContent;
            set
            {
                SetProperty(ref _reasoningContent, value ?? string.Empty);
                OnPropertyChanged(nameof(HasReasoning));
            }
        }
        private string _reasoningContent = string.Empty;

        [JsonIgnore]
        public bool HasReasoning => !string.IsNullOrWhiteSpace(ReasoningContent);

        public bool IsReasoningExpanded
        {
            get => _isReasoningExpanded;
            set => SetProperty(ref _isReasoningExpanded, value);
        }
        private bool _isReasoningExpanded = true;

        public bool IsReasoningInProgress
        {
            get => _isReasoningInProgress;
            set
            {
                SetProperty(ref _isReasoningInProgress, value);
                OnPropertyChanged(nameof(ReasoningHeader));
            }
        }
        private bool _isReasoningInProgress;

        [JsonIgnore]
        public string ReasoningHeader => IsReasoningInProgress ? CopilotUiText.ReasoningInProgressHeader : CopilotUiText.ReasoningHeader;

        public bool EnsureValid()
        {
            var changed = false;

            if (CreatedAt == default)
            {
                CreatedAt = DateTime.Now;
                changed = true;
            }

            if (_content == null)
            {
                Content = string.Empty;
                changed = true;
            }

            if (_reasoningContent == null)
            {
                ReasoningContent = string.Empty;
                changed = true;
            }

            if (_executionContent == null)
            {
                ExecutionContent = string.Empty;
                changed = true;
            }

            if (_requestContent == null)
            {
                RequestContent = string.Empty;
                changed = true;
            }

            if (_assistantName == null)
            {
                AssistantName = string.Empty;
                changed = true;
            }

            if (!Enum.IsDefined(RequestMode))
            {
                RequestMode = CopilotAgentMode.Chat;
                changed = true;
            }

            return changed;
        }

        private static string BuildExecutionSummary(string? content, bool isInProgress)
        {
            var text = content ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return isInProgress ? "Starting" : string.Empty;

            var (toolCount, failedCount, latestTool) = AnalyzeExecutionTrace(text);

            if (toolCount == 0)
            {
                var firstLine = text
                    .Split(ExecutionLineSeparators, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
                return isInProgress
                    ? TrimForInline(string.IsNullOrWhiteSpace(firstLine) ? "Running" : firstLine)
                    : "Trace available";
            }

            var builder = new StringBuilder(isInProgress ? "Running" : "Completed");
            builder.Append(" - ").Append(toolCount).Append(toolCount == 1 ? " tool" : " tools");

            if (failedCount > 0)
                builder.Append(" - ").Append(failedCount).Append(" failed");

            if (!string.IsNullOrWhiteSpace(latestTool))
                builder.Append(" - latest ").Append(TrimForInline(latestTool));

            return builder.ToString();
        }

        private static (int ToolCount, int FailedCount, string LatestTool) AnalyzeExecutionTrace(string? content)
        {
            var toolCount = 0;
            var failedCount = 0;
            var latestTool = string.Empty;

            foreach (var rawLine in (content ?? string.Empty).Split(ExecutionLineSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.Length > 2 && line[0] == '[')
                {
                    var closeIndex = line.IndexOf(']');
                    if (closeIndex > 1)
                    {
                        latestTool = line[1..closeIndex].Trim();
                        toolCount++;
                    }
                }

                if (line.StartsWith("Status:", StringComparison.OrdinalIgnoreCase)
                    && line.Contains("Failed", StringComparison.OrdinalIgnoreCase))
                {
                    failedCount++;
                }
            }

            return (toolCount, failedCount, latestTool);
        }

        private static string TrimForInline(string value)
        {
            var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 48 ? text : text[..48] + "...";
        }

        private static string TrimForTooltip(string value)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length <= 1600 ? text : text[..1600] + Environment.NewLine + "...";
        }
    }

    public sealed class CopilotConversationRecord : ViewModelBase
    {
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, NormalizeText(value));
        }
        private string _id = Guid.NewGuid().ToString("N");

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, NormalizeText(value));
        }
        private string _title = CopilotUiText.NewConversationTitle;

        public bool HasCustomTitle
        {
            get => _hasCustomTitle;
            set => SetProperty(ref _hasCustomTitle, value);
        }
        private bool _hasCustomTitle;

        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (SetProperty(ref _isPinned, value))
                {
                    OnPropertyChanged(nameof(PinLabel));
                    OnPropertyChanged(nameof(PinMenuText));
                }
            }
        }
        private bool _isPinned;

        public string PreviewText
        {
            get => _previewText;
            set => SetProperty(ref _previewText, value ?? string.Empty);
        }
        private string _previewText = CopilotUiText.EmptyConversationPreview;

        public string ProfileId
        {
            get => _profileId;
            set => SetProperty(ref _profileId, NormalizeText(value));
        }
        private string _profileId = string.Empty;

        public string ProfileDisplayName
        {
            get => _profileDisplayName;
            set => SetProperty(ref _profileDisplayName, NormalizeText(value));
        }
        private string _profileDisplayName = string.Empty;

        public int LastUsageInputTokens
        {
            get => _lastUsageInputTokens;
            set => SetProperty(ref _lastUsageInputTokens, Math.Max(0, value));
        }
        private int _lastUsageInputTokens;

        public int LastUsageOutputTokens
        {
            get => _lastUsageOutputTokens;
            set => SetProperty(ref _lastUsageOutputTokens, Math.Max(0, value));
        }
        private int _lastUsageOutputTokens;

        public int LastUsageTotalTokens
        {
            get => _lastUsageTotalTokens;
            set => SetProperty(ref _lastUsageTotalTokens, Math.Max(0, value));
        }
        private int _lastUsageTotalTokens;

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }
        private DateTime _createdAt = DateTime.Now;

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set
            {
                if (SetProperty(ref _updatedAt, value))
                    OnPropertyChanged(nameof(UpdatedLabel));
            }
        }
        private DateTime _updatedAt = DateTime.Now;

        public ObservableCollection<CopilotChatMessage> Messages { get; set; } = new();

        public ObservableCollection<CopilotAttachmentItem> Attachments { get; set; } = new();

        [JsonIgnore]
        public string UpdatedLabel => UpdatedAt.Date == DateTime.Today ? UpdatedAt.ToString("HH:mm") : UpdatedAt.ToString("M/d");

        [JsonIgnore]
        public string PinLabel => IsPinned ? CopilotUiText.PinnedLabel : string.Empty;

        [JsonIgnore]
        public string PinMenuText => IsPinned ? CopilotUiText.UnpinMenuText : CopilotUiText.PinMenuText;

        [JsonIgnore]
        public CopilotTokenUsage LastUsage => new(LastUsageInputTokens, LastUsageOutputTokens, LastUsageTotalTokens);

        public bool EnsureValid()
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
                changed = true;
            }

            if (CreatedAt == default)
            {
                CreatedAt = DateTime.Now;
                changed = true;
            }

            if (UpdatedAt == default)
            {
                UpdatedAt = CreatedAt;
                changed = true;
            }

            Messages ??= new ObservableCollection<CopilotChatMessage>();
            Attachments ??= new ObservableCollection<CopilotAttachmentItem>();

            foreach (var message in Messages)
            {
                changed |= message.EnsureValid();
            }

            foreach (var attachment in Attachments)
            {
                changed |= attachment.EnsureValid();
            }

            return changed;
        }

        public void Touch()
        {
            UpdatedAt = DateTime.Now;
        }

        public void SetLastUsage(CopilotTokenUsage usage)
        {
            LastUsageInputTokens = usage.InputTokens;
            LastUsageOutputTokens = usage.OutputTokens;
            LastUsageTotalTokens = usage.EffectiveTotalTokens;
        }

        public void ClearLastUsage()
        {
            LastUsageInputTokens = 0;
            LastUsageOutputTokens = 0;
            LastUsageTotalTokens = 0;
        }

        public void RefreshSummary()
        {
            var firstUserMessage = Messages.FirstOrDefault(message => message.Role == CopilotChatRole.User && !string.IsNullOrWhiteSpace(message.Content));
            var generatedTitle = firstUserMessage == null ? CopilotUiText.NewConversationTitle : BuildPreview(firstUserMessage.Content, 24);
            if (!HasCustomTitle || string.IsNullOrWhiteSpace(Title))
                Title = generatedTitle;

            var lastVisibleMessage = Messages.LastOrDefault(message => !string.IsNullOrWhiteSpace(message.Content));
            if (lastVisibleMessage != null)
            {
                PreviewText = BuildPreview(lastVisibleMessage.Content, 42);
                return;
            }

            PreviewText = Attachments.Count > 0
                ? CopilotUiText.FormatAttachmentMountedCount(Attachments.Count)
                : CopilotUiText.EmptyConversationPreview;
        }

        public void SetCustomTitle(string title)
        {
            Title = title;
            HasCustomTitle = true;
        }

        public void SetGeneratedTitle(string title)
        {
            Title = title;
            HasCustomTitle = true;
        }

        public static CopilotConversationRecord CreateEmpty(string profileId, string profileDisplayName)
        {
            return new CopilotConversationRecord
            {
                HasCustomTitle = false,
                ProfileId = profileId,
                ProfileDisplayName = profileDisplayName,
                Title = CopilotUiText.NewConversationTitle,
                PreviewText = CopilotUiText.EmptyConversationPreview,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
            };
        }

        private static string BuildPreview(string content, int maxLength)
        {
            var normalized = (content ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length <= maxLength)
                return normalized;

            return normalized[..maxLength] + "...";
        }

        private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;
    }

    public sealed class CopilotAttachmentItem : ViewModelBase
    {
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, NormalizeText(value));
        }
        private string _id = Guid.NewGuid().ToString("N");

        public CopilotAttachmentType Type
        {
            get => _type;
            set
            {
                if (SetProperty(ref _type, value))
                {
                    ResetPreviewImage();
                    OnPropertyChanged(nameof(BadgeText));
                    OnPropertyChanged(nameof(DisplayLabel));
                }
            }
        }
        private CopilotAttachmentType _type;

        public string Title
        {
            get => _title;
            set
            {
                if (SetProperty(ref _title, NormalizeText(value)))
                    OnPropertyChanged(nameof(DisplayLabel));
            }
        }
        private string _title = string.Empty;

        public string Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value?.Trim() ?? string.Empty))
                {
                    ResetPreviewImage();
                    OnPropertyChanged(nameof(DisplayLabel));
                    OnPropertyChanged(nameof(TooltipText));
                }
            }
        }
        private string _value = string.Empty;

        public string Source
        {
            get => _source;
            set
            {
                if (SetProperty(ref _source, value?.Trim() ?? string.Empty))
                    OnPropertyChanged(nameof(TooltipText));
            }
        }
        private string _source = string.Empty;

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }
        private DateTime _createdAt = DateTime.Now;

        [JsonIgnore]
        public string BadgeText => Type switch
        {
            CopilotAttachmentType.File => CopilotUiText.FileBadge,
            CopilotAttachmentType.Image => CopilotUiText.ImageBadge,
            CopilotAttachmentType.WebPage => CopilotUiText.WebPageBadge,
            _ => CopilotUiText.ContextBadge,
        };

        [JsonIgnore]
        public string DisplayLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title))
                    return Title;

                if (Type == CopilotAttachmentType.File || Type == CopilotAttachmentType.Image)
                    return Path.GetFileName(Value);

                if (Type == CopilotAttachmentType.WebPage)
                    return TryGetHostLabel(Source);

                return BuildPreview(Value, 20);
            }
        }

        [JsonIgnore]
        public string TooltipText => Type == CopilotAttachmentType.WebPage && !string.IsNullOrWhiteSpace(Source)
            ? Source
            : Value;

        [JsonIgnore]
        public ImageSource? PreviewImage
        {
            get
            {
                if (Type != CopilotAttachmentType.Image || string.IsNullOrWhiteSpace(Value) || !File.Exists(Value))
                    return null;

                if (_previewImage != null && string.Equals(_previewImagePath, Value, StringComparison.OrdinalIgnoreCase))
                    return _previewImage;

                try
                {
                    using var stream = new FileStream(Value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();

                    _previewImage = image;
                    _previewImagePath = Value;
                    return _previewImage;
                }
                catch
                {
                    return null;
                }
            }
        }

        [JsonIgnore]
        public bool HasPreviewImage => PreviewImage != null;

        [JsonIgnore]
        public bool IsImage => Type == CopilotAttachmentType.Image;

        [JsonIgnore]
        public bool IsStoredImageFile => Type == CopilotAttachmentType.Image && !string.IsNullOrWhiteSpace(Value);

        [JsonIgnore]
        public string ImageFallbackText => HasPreviewImage ? string.Empty : CopilotUiText.ImagePreviewUnavailable;

        [JsonIgnore]
        public string ImageMetaText => CreatedAt.ToString("M/d HH:mm");

        private ImageSource? _previewImage;

        private string _previewImagePath = string.Empty;

        public bool EnsureValid()
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
                changed = true;
            }

            if (CreatedAt == default)
            {
                CreatedAt = DateTime.Now;
                changed = true;
            }

            if (_value == null)
            {
                Value = string.Empty;
                changed = true;
            }

            if (_title == null)
            {
                Title = string.Empty;
                changed = true;
            }

            if (_source == null)
            {
                Source = string.Empty;
                changed = true;
            }

            return changed;
        }

        public static CopilotAttachmentItem CreateFile(string filePath)
        {
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.File,
                Title = Path.GetFileName(filePath),
                Value = filePath,
                CreatedAt = DateTime.Now,
            };
        }

        public static CopilotAttachmentItem CreateContext(string text, string? title = null, string? source = null)
        {
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.Context,
                Title = string.IsNullOrWhiteSpace(title) ? BuildPreview(text, 18) : title,
                Source = source ?? string.Empty,
                Value = text,
                CreatedAt = DateTime.Now,
            };
        }

        public static CopilotAttachmentItem CreateImage(string imagePath, string? title = null)
        {
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.Image,
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(imagePath) : title,
                Value = imagePath,
                CreatedAt = DateTime.Now,
            };
        }

        public static CopilotAttachmentItem CreateWebPage(string url, string title, string content)
        {
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.WebPage,
                Title = title,
                Source = url,
                Value = content,
                CreatedAt = DateTime.Now,
            };
        }

        private void ResetPreviewImage()
        {
            _previewImage = null;
            _previewImagePath = string.Empty;
            OnPropertyChanged(nameof(PreviewImage));
            OnPropertyChanged(nameof(HasPreviewImage));
            OnPropertyChanged(nameof(ImageFallbackText));
        }

        private static string BuildPreview(string content, int maxLength)
        {
            var normalized = (content ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length <= maxLength)
                return normalized;

            return normalized[..maxLength] + "...";
        }

        private static string TryGetHostLabel(string? value)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
                return uri.Host;

            return BuildPreview(value ?? string.Empty, 20);
        }

        private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;
    }

    public readonly record struct CopilotRequestMessage(string Role, string Content);

    public sealed class CopilotProviderOption
    {
        public string Label { get; init; } = string.Empty;

        public CopilotProviderType Value { get; init; }
    }
}
