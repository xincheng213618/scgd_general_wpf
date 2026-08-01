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
            return Create(templateId, content, templateRevision, capturedTimeUtc, flowKey);
        }

        public static FlowTemplateSnapshot Create(
            int templateId,
            byte[] content,
            int? templateRevision = null,
            DateTime? capturedTimeUtc = null,
            string? flowKey = null)
        {
            ArgumentNullException.ThrowIfNull(content);

            byte[] stableContent = (byte[])content.Clone();
            return new FlowTemplateSnapshot
            {
                TemplateId = templateId,
                FlowKey = string.IsNullOrWhiteSpace(flowKey) ? null : flowKey.Trim(),
                TemplateRevision = templateRevision,
                ContentHash = Convert.ToHexString(SHA256.HashData(stableContent)).ToLowerInvariant(),
                Content = stableContent,
                ContentLength = stableContent.Length,
                CapturedTimeUtc = capturedTimeUtc ?? DateTime.UtcNow,
            };
        }
    }
}
