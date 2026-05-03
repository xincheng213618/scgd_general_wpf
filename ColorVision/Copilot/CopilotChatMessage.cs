using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

    public sealed class CopilotChatMessage : ViewModelBase
    {
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
        public string Header => IsUser ? "你" : string.IsNullOrWhiteSpace(AssistantName) ? "AI" : AssistantName;

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
                SetProperty(ref _executionContent, value ?? string.Empty);
                OnPropertyChanged(nameof(HasExecutionTrace));
            }
        }
        private string _executionContent = string.Empty;

        [JsonIgnore]
        public bool HasExecutionTrace => !string.IsNullOrWhiteSpace(ExecutionContent);

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
                SetProperty(ref _isExecutionInProgress, value);
                OnPropertyChanged(nameof(ExecutionHeader));
            }
        }
        private bool _isExecutionInProgress;

        [JsonIgnore]
        public string ExecutionHeader => IsExecutionInProgress ? "执行中" : "执行过程";

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
        public string ReasoningHeader => IsReasoningInProgress ? "推理中" : "推理详情";

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
        private string _title = "新会话";

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
        private string _previewText = "点击 + 新建或直接输入问题";

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
        public string PinLabel => IsPinned ? "置顶" : string.Empty;

        [JsonIgnore]
        public string PinMenuText => IsPinned ? "取消置顶" : "置顶";

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

        public void RefreshSummary()
        {
            var firstUserMessage = Messages.FirstOrDefault(message => message.Role == CopilotChatRole.User && !string.IsNullOrWhiteSpace(message.Content));
            var generatedTitle = firstUserMessage == null ? "新会话" : BuildPreview(firstUserMessage.Content, 24);
            if (!HasCustomTitle || string.IsNullOrWhiteSpace(Title))
                Title = generatedTitle;

            var lastVisibleMessage = Messages.LastOrDefault(message => !string.IsNullOrWhiteSpace(message.Content));
            if (lastVisibleMessage != null)
            {
                PreviewText = BuildPreview(lastVisibleMessage.Content, 42);
                return;
            }

            PreviewText = Attachments.Count > 0
                ? $"已挂载 {Attachments.Count} 个文件/上下文"
                : "点击 + 新建或直接输入问题";
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
                Title = "新会话",
                PreviewText = "点击 + 新建或直接输入问题",
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
            CopilotAttachmentType.File => "文件",
            CopilotAttachmentType.Image => "图片",
            CopilotAttachmentType.WebPage => "网页",
            _ => "上下文",
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
        public string ImageFallbackText => HasPreviewImage ? string.Empty : "图片预览不可用";

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

        public static CopilotAttachmentItem CreateContext(string text)
        {
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.Context,
                Title = BuildPreview(text, 18),
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