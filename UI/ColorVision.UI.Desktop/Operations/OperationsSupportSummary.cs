namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsSupportSessionSummary
    {
        public string SessionId { get; init; } = string.Empty;
        public string Mode { get; init; } = "diagnostics";
        public string Status { get; init; } = "expired";
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
        public DateTimeOffset? LocalConsentAt { get; init; }
        public int RemainingSeconds { get; init; }
        public bool CanSendMessages { get; init; }
        public int MessageCount { get; init; }
    }

    public sealed class OperationsSupportMessageSummary
    {
        public string Direction { get; init; } = "from_support";
        public string Text { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
    }

    public static class OperationsSupportSummaryFactory
    {
        public const string PrivacyNotice =
            "会话仅返回当前设备的状态和有限文本；不返回设备 ID、电脑账户、内部任务 ID 或审计标识。请勿发送密码、密钥或客户数据。";

        public static OperationsSupportSessionSummary Create(
            OperationsSupportSession session,
            int messageCount,
            DateTimeOffset? now = null)
        {
            ArgumentNullException.ThrowIfNull(session);
            DateTimeOffset current = now ?? DateTimeOffset.UtcNow;
            bool expired = session.ExpiresAt <= current;
            string status = expired && session.Status is "awaiting_local_consent" or "active"
                ? "expired"
                : session.Status;
            return new OperationsSupportSessionSummary
            {
                SessionId = session.SessionId,
                Mode = session.Mode,
                Status = status,
                CreatedAt = session.CreatedAt,
                ExpiresAt = session.ExpiresAt,
                LocalConsentAt = session.LocalConsentAt,
                RemainingSeconds = status == "active"
                    ? Math.Max(0, (int)Math.Ceiling((session.ExpiresAt - current).TotalSeconds))
                    : 0,
                CanSendMessages = status == "active",
                MessageCount = Math.Max(0, messageCount),
            };
        }

        public static OperationsSupportMessageSummary Create(OperationsSupportMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            return new OperationsSupportMessageSummary
            {
                Direction = message.Source == "device" ? "from_device" : "from_support",
                Text = message.Text,
                CreatedAt = message.CreatedAt,
            };
        }
    }
}
