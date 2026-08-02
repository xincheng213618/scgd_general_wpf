using System;
using System.Linq;

namespace ColorVision.Copilot
{
    /// <summary>
    /// Durable provenance for a copied conversation branch. This metadata supports
    /// navigation and grouping only; it never carries checkpoints or authorization.
    /// </summary>
    public sealed class CopilotConversationBranchOrigin
    {
        public const int CurrentSchemaVersion = 1;
        private const int MaximumIdentifierCharacters = 128;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public string ParentConversationId { get; set; } = string.Empty;

        public string RootConversationId { get; set; } = string.Empty;

        public string ThroughMessageId { get; set; } = string.Empty;

        public DateTimeOffset ForkedAtUtc { get; set; }

        public bool IsStructurallyValid(string? ownerConversationId = null)
        {
            var ownerId = ownerConversationId?.Trim() ?? string.Empty;
            return SchemaVersion == CurrentSchemaVersion
                && IsValidIdentifier(ParentConversationId)
                && IsValidIdentifier(RootConversationId)
                && IsValidIdentifier(ThroughMessageId)
                && ForkedAtUtc != default
                && (ownerId.Length == 0
                    || (!string.Equals(ParentConversationId, ownerId, StringComparison.Ordinal)
                        && !string.Equals(RootConversationId, ownerId, StringComparison.Ordinal)));
        }

        private static bool IsValidIdentifier(string? value)
        {
            var normalized = value?.Trim() ?? string.Empty;
            return normalized.Length is > 0 and <= MaximumIdentifierCharacters
                && string.Equals(value, normalized, StringComparison.Ordinal)
                && normalized.All(character => !char.IsControl(character));
        }
    }
}
