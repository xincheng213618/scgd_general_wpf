using System;

namespace ColorVision.Engine.Templates.Flow.Versioning
{
    internal static class FlowRevisionStoreRules
    {
        public static string NormalizeFlowKey(string flowKey)
        {
            if (string.IsNullOrWhiteSpace(flowKey))
                throw new ArgumentException("FlowKey 不能为空。", nameof(flowKey));

            string normalized = flowKey.Trim();
            if (normalized.Length > 256)
                throw new ArgumentException("FlowKey 长度不能超过 256。", nameof(flowKey));
            if (normalized.IndexOfAny(['\r', '\n', '\0']) >= 0
                || normalized.StartsWith('/')
                || normalized.StartsWith('\\')
                || normalized.Contains(":\\", StringComparison.Ordinal)
                || normalized.Contains("file://", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("FlowKey 包含非法字符。", nameof(flowKey));
            return normalized;
        }

        public static string NormalizeHash(
            string hash,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(hash)
                || hash.Length != 64)
            {
                throw new ArgumentException(
                    "哈希必须是 64 位十六进制 SHA-256。",
                    parameterName);
            }
            foreach (char value in hash)
            {
                if (!Uri.IsHexDigit(value))
                {
                    throw new ArgumentException(
                        "哈希必须是 64 位十六进制 SHA-256。",
                        parameterName);
                }
            }
            return hash.ToLowerInvariant();
        }

        public static void EnsureExpectedHead(
            string flowKey,
            FlowRevisionWriteCondition expected,
            FlowRevision? actual)
        {
            ArgumentNullException.ThrowIfNull(expected);
            if (actual == null)
            {
                if (expected.ParentRevision != null
                    || !string.IsNullOrWhiteSpace(expected.BaseBinaryHash))
                {
                    throw new FlowRevisionConflictException(
                        flowKey,
                        expected,
                        null);
                }
                return;
            }

            string? expectedHash = string.IsNullOrWhiteSpace(
                expected.BaseBinaryHash)
                ? null
                : NormalizeHash(
                    expected.BaseBinaryHash,
                    nameof(expected.BaseBinaryHash));
            if (expected.ParentRevision != actual.Revision
                || !string.Equals(
                    expectedHash,
                    actual.BinaryHash,
                    StringComparison.Ordinal))
            {
                throw new FlowRevisionConflictException(
                    flowKey,
                    expected,
                    actual);
            }
        }

        public static FlowRevision CreateRevision(
            FlowRevisionAppendRequest request,
            FlowRevision? head)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.FullSnapshot);
            ArgumentNullException.ThrowIfNull(request.SemanticDocument);
            string flowKey = NormalizeFlowKey(request.FlowKey);
            EnsureExpectedHead(flowKey, request.Condition, head);
            string semanticHash = NormalizeHash(
                request.SemanticHash,
                nameof(request.SemanticHash));
            string layoutHash = NormalizeHash(
                request.LayoutHash,
                nameof(request.LayoutHash));
            string binaryHash = NormalizeHash(
                request.BinaryHash,
                nameof(request.BinaryHash));
            if (!string.Equals(
                semanticHash,
                FlowSemanticHash.ComputeSemanticHash(
                    request.SemanticDocument),
                StringComparison.Ordinal)
                || !string.Equals(
                    layoutHash,
                    FlowSemanticHash.ComputeLayoutHash(
                        request.SemanticDocument),
                    StringComparison.Ordinal)
                || !string.Equals(
                    binaryHash,
                    FlowSemanticHash.ComputeBinaryHash(
                        request.FullSnapshot),
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "流程版本内容与声明的哈希不一致。",
                    nameof(request));
            }
            return new FlowRevision
            {
                FlowKey = flowKey,
                Revision = (head?.Revision ?? 0) + 1,
                ParentRevision = head?.Revision,
                BaseBinaryHash = head?.BinaryHash,
                Source = request.Source,
                IsPublished = request.IsPublished,
                SemanticHash = semanticHash,
                LayoutHash = layoutHash,
                BinaryHash = binaryHash,
                FullSnapshot = (byte[])request.FullSnapshot.Clone(),
                SemanticDocument = request.SemanticDocument.DeepClone(),
                Author = NormalizeOptional(request.Author, 256),
                Message = NormalizeOptional(request.Message, 2_000),
                ExternalVersion = NormalizeOptional(
                    request.ExternalVersion,
                    256),
                RollbackOfRevision = request.RollbackOfRevision,
                CreatedTimeUtc = NormalizeUtc(request.CreatedTimeUtc),
            };
        }

        public static string? NormalizeOptional(
            string? value,
            int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            string normalized = value.Trim();
            if (normalized.Length > maximumLength)
            {
                throw new ArgumentException(
                    $"文本长度不能超过 {maximumLength}。");
            }
            return normalized;
        }

        public static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default)
                value = DateTime.UtcNow;
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            };
        }
    }
}
