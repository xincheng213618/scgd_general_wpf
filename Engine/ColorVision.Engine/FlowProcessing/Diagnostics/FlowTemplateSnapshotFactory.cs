using System;
using System.Security.Cryptography;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal static class FlowTemplateSnapshotFactory
    {
        public static FlowTemplateSnapshot Create(
            int templateId,
            string dataBase64,
            int? templateRevision = null,
            DateTime? capturedTimeUtc = null,
            string? flowKey = null)
        {
            ArgumentNullException.ThrowIfNull(dataBase64);

            byte[] content = Convert.FromBase64String(dataBase64);
            // The decoder may share its empty result. Keep each snapshot's array independent.
            if (content.Length == 0)
                content = (byte[])content.Clone();
            return CreateSnapshot(templateId, content, templateRevision, capturedTimeUtc, flowKey);
        }

        public static FlowTemplateSnapshot Create(
            int templateId,
            byte[] content,
            int? templateRevision = null,
            DateTime? capturedTimeUtc = null,
            string? flowKey = null)
        {
            ArgumentNullException.ThrowIfNull(content);

            return CreateSnapshot(templateId, (byte[])content.Clone(), templateRevision, capturedTimeUtc, flowKey);
        }

        private static FlowTemplateSnapshot CreateSnapshot(
            int templateId,
            byte[] stableContent,
            int? templateRevision,
            DateTime? capturedTimeUtc,
            string? flowKey)
        {
            Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(stableContent, hash);
            return new FlowTemplateSnapshot
            {
                TemplateId = templateId,
                FlowKey = string.IsNullOrWhiteSpace(flowKey) ? null : flowKey.Trim(),
                TemplateRevision = templateRevision,
                ContentHash = Convert.ToHexStringLower(hash),
                Content = stableContent,
                ContentLength = stableContent.Length,
                CapturedTimeUtc = capturedTimeUtc ?? DateTime.UtcNow,
            };
        }
    }
}
