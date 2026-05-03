using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

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
        public string Header => IsUser ? "你" : "AI";

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
                    OnPropertyChanged(nameof(BadgeText));
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
                    OnPropertyChanged(nameof(DisplayLabel));
                    OnPropertyChanged(nameof(TooltipText));
                }
            }
        }
        private string _value = string.Empty;

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }
        private DateTime _createdAt = DateTime.Now;

        [JsonIgnore]
        public string BadgeText => Type == CopilotAttachmentType.File ? "文件" : "上下文";

        [JsonIgnore]
        public string DisplayLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title))
                    return Title;

                if (Type == CopilotAttachmentType.File)
                    return Path.GetFileName(Value);

                return BuildPreview(Value, 20);
            }
        }

        [JsonIgnore]
        public string TooltipText => Value;

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

        private static string BuildPreview(string content, int maxLength)
        {
            var normalized = (content ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length <= maxLength)
                return normalized;

            return normalized[..maxLength] + "...";
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