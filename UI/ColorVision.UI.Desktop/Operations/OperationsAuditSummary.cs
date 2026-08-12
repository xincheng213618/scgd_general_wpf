namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsAuditSummary
    {
        public DateTimeOffset Timestamp { get; init; }

        public string ActorType { get; init; } = string.Empty;

        public string Action { get; init; } = string.Empty;

        public string Outcome { get; init; } = string.Empty;
    }

    public static class OperationsAuditSummaryFactory
    {
        public static OperationsAuditSummary Create(OperationsAuditEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            return new OperationsAuditSummary
            {
                Timestamp = entry.Timestamp,
                ActorType = entry.ActorType,
                Action = entry.Action,
                Outcome = entry.Outcome,
            };
        }
    }
}
