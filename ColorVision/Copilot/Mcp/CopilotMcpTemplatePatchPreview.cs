using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Copilot.Mcp
{
    public sealed class CopilotMcpTemplatePatchPreview
    {
        public string PreviewId { get; init; } = string.Empty;

        public string TemplateIdentifier { get; init; } = string.Empty;

        public string SourceId { get; init; } = string.Empty;

        public string CurrentJson { get; init; } = string.Empty;

        public string ProposedChangesJson { get; init; } = string.Empty;

        public string PatchedJson { get; init; } = string.Empty;

        public string CurrentJsonHash { get; init; } = string.Empty;

        public IReadOnlyList<string> Changes { get; init; } = Array.Empty<string>();

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset ExpiresAt { get; init; }
    }

    public sealed class CopilotMcpTemplatePatchPreviewStore
    {
        private static readonly Lazy<CopilotMcpTemplatePatchPreviewStore> LazyInstance = new(() => new CopilotMcpTemplatePatchPreviewStore());
        private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(10);
        private readonly object _syncRoot = new();
        private readonly List<CopilotMcpTemplatePatchPreview> _previews = new();

        private CopilotMcpTemplatePatchPreviewStore()
        {
        }

        public static CopilotMcpTemplatePatchPreviewStore Instance => LazyInstance.Value;

        public TimeSpan PreviewLifetime { get; set; } = DefaultLifetime;

        public CopilotMcpTemplatePatchPreview Create(
            string templateIdentifier,
            string sourceId,
            string currentJson,
            string proposedChangesJson,
            string patchedJson,
            IReadOnlyList<string> changes)
        {
            PruneExpired();
            var now = DateTimeOffset.UtcNow;
            var preview = new CopilotMcpTemplatePatchPreview
            {
                PreviewId = CreatePreviewId(),
                TemplateIdentifier = Sanitize(templateIdentifier),
                SourceId = Sanitize(sourceId),
                CurrentJson = currentJson ?? string.Empty,
                ProposedChangesJson = proposedChangesJson ?? string.Empty,
                PatchedJson = patchedJson ?? string.Empty,
                CurrentJsonHash = ComputeHash(currentJson),
                Changes = changes.ToArray(),
                CreatedAt = now,
                ExpiresAt = now.Add(PreviewLifetime),
            };

            lock (_syncRoot)
            {
                _previews.Add(preview);
            }

            return preview;
        }

        public bool TryGet(string previewId, out CopilotMcpTemplatePatchPreview preview, out string message)
        {
            PruneExpired();
            preview = null!;
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(previewId))
            {
                message = "The apply_template_patch tool requires preview_id from preview_template_patch.";
                return false;
            }

            lock (_syncRoot)
            {
                var match = _previews.FirstOrDefault(item => string.Equals(item.PreviewId, previewId.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match == null)
                {
                    message = "No active template patch preview exists for the supplied preview_id. Call preview_template_patch first.";
                    return false;
                }

                if (match.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    _previews.Remove(match);
                    message = "The template patch preview has expired. Call preview_template_patch again.";
                    return false;
                }

                preview = match;
                return true;
            }
        }

        public void ClearForTests()
        {
            lock (_syncRoot)
            {
                _previews.Clear();
                PreviewLifetime = DefaultLifetime;
            }
        }

        private void PruneExpired()
        {
            lock (_syncRoot)
            {
                var now = DateTimeOffset.UtcNow;
                _previews.RemoveAll(preview => preview.ExpiresAt <= now);
            }
        }

        private static string CreatePreviewId()
        {
            Span<byte> bytes = stackalloc byte[6];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string ComputeHash(string? value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
        }

        private static string Sanitize(string? value)
        {
            var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 240 ? text : text[..240] + "...";
        }
    }
}