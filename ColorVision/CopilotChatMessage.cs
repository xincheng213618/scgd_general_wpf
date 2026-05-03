using ColorVision.Common.MVVM;
using System;

namespace ColorVision
{
    public enum CopilotChatRole
    {
        User,
        Assistant,
    }

    public sealed class CopilotChatMessage : ViewModelBase
    {
        public CopilotChatMessage(CopilotChatRole role, string content)
        {
            Role = role;
            _content = content ?? string.Empty;
            CreatedAt = DateTime.Now;
        }

        public CopilotChatRole Role { get; }

        public bool IsUser => Role == CopilotChatRole.User;

        public string Header => IsUser ? "你" : "AI";

        public DateTime CreatedAt { get; }

        public string TimeLabel => CreatedAt.ToString("HH:mm");

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value ?? string.Empty);
        }
        private string _content;
    }

    public readonly record struct CopilotRequestMessage(string Role, string Content);

    public sealed class CopilotProviderOption
    {
        public string Label { get; init; } = string.Empty;

        public CopilotProviderType Value { get; init; }
    }
}