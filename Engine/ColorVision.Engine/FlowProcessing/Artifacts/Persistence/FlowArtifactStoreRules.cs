using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Engine.FlowProcessing.Artifacts.Persistence
{
    internal static class FlowArtifactStoreRules
    {
        private const int MaximumFlowKeyLength = 256;
        private const int MaximumRoleLength = 64;
        private const int MaximumDependencyKeyLength = 2_000;

        public static string NormalizeFlowKey(string flowKey)
        {
            if (string.IsNullOrWhiteSpace(flowKey))
                throw new ArgumentException(
                    "FlowKey 不能为空。",
                    nameof(flowKey));

            string normalized = flowKey.Trim();
            if (normalized.Length > MaximumFlowKeyLength)
            {
                throw new ArgumentException(
                    $"FlowKey 长度不能超过 {MaximumFlowKeyLength}。",
                    nameof(flowKey));
            }
            if (normalized.IndexOfAny(['\r', '\n', '\0']) >= 0
                || normalized.StartsWith('/')
                || normalized.StartsWith('\\')
                || normalized.Contains(":\\", StringComparison.Ordinal)
                || normalized.Contains(
                    "file://",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "FlowKey 包含非法字符。",
                    nameof(flowKey));
            }
            return normalized;
        }

        public static string NormalizeHash(
            string hash,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(hash)
                || hash.Length != 64
                || hash.Any(value => !Uri.IsHexDigit(value)))
            {
                throw new ArgumentException(
                    "哈希必须是 64 位十六进制 SHA-256。",
                    parameterName);
            }
            return hash.ToLowerInvariant();
        }

        public static string ComputeBlobHash(byte[] content)
        {
            ArgumentNullException.ThrowIfNull(content);
            return Convert.ToHexString(
                    SHA256.HashData(content))
                .ToLowerInvariant();
        }

        public static PreparedFlowArtifactRevision Prepare(
            FlowArtifactRevisionWriteRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            string flowKey = NormalizeFlowKey(request.FlowKey);
            FlowArtifactHeadCondition condition =
                NormalizeCondition(request.ExpectedHead);
            if (request.Artifacts == null
                || request.Artifacts.Count == 0)
            {
                throw new ArgumentException(
                    "流程版本至少需要一个 artifact。",
                    nameof(request));
            }

            PreparedFlowArtifactPart[] artifacts = request.Artifacts
                .Select(PrepareArtifact)
                .OrderBy(item => item.Role, StringComparer.Ordinal)
                .ToArray();
            if (artifacts
                .GroupBy(item => item.Role, StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
            {
                throw new ArgumentException(
                    "同一流程版本不能包含重复的 artifact 角色。",
                    nameof(request));
            }

            FlowArtifactDependency[] dependencies =
                (request.Dependencies
                    ?? Array.Empty<FlowArtifactDependency>())
                .Select(PrepareDependency)
                .OrderBy(
                    item => item.DependencyKey,
                    StringComparer.Ordinal)
                .ToArray();
            if (dependencies
                .GroupBy(
                    item => item.DependencyKey,
                    StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
            {
                throw new ArgumentException(
                    "同一流程版本不能包含重复的依赖槽位。",
                    nameof(request));
            }

            DateTime createdTimeUtc = NormalizeUtc(
                request.CreatedTimeUtc ?? DateTime.UtcNow);
            string revisionHash = ComputeRevisionHash(
                artifacts,
                dependencies);
            return new PreparedFlowArtifactRevision(
                flowKey,
                artifacts,
                dependencies,
                condition,
                revisionHash,
                request.PublishImmediately,
                NormalizeOptional(request.Source, 64),
                NormalizeOptional(request.Author, 256),
                NormalizeOptional(request.Message, 2_000),
                NormalizeOptional(request.ExternalVersion, 256),
                createdTimeUtc);
        }

        public static PreparedFlowArtifactTransition Prepare(
            FlowArtifactRevisionTransitionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                request.Revision);
            return new PreparedFlowArtifactTransition(
                NormalizeFlowKey(request.FlowKey),
                request.Revision,
                NormalizeCondition(request.ExpectedHead),
                NormalizeOptional(request.Actor, 256),
                NormalizeOptional(request.Message, 2_000),
                NormalizeUtc(
                    request.ChangedTimeUtc
                        ?? DateTime.UtcNow));
        }

        public static FlowArtifactHeadCondition NormalizeCondition(
            FlowArtifactHeadCondition? condition)
        {
            ArgumentNullException.ThrowIfNull(condition);
            if (condition.Revision == null
                && string.IsNullOrWhiteSpace(condition.RevisionHash))
            {
                return FlowArtifactHeadCondition.Initial;
            }
            if (condition.Revision is not > 0
                || string.IsNullOrWhiteSpace(condition.RevisionHash))
            {
                throw new ArgumentException(
                    "期望 head 必须同时包含版本号和版本哈希。",
                    nameof(condition));
            }
            return new FlowArtifactHeadCondition(
                condition.Revision,
                NormalizeHash(
                    condition.RevisionHash,
                    nameof(condition.RevisionHash)));
        }

        public static void EnsureExpectedHead(
            string flowKey,
            FlowArtifactHeadCondition expected,
            FlowArtifactReference? actual)
        {
            ArgumentNullException.ThrowIfNull(expected);
            int? actualRevision = actual?.HeadRevision;
            string? actualHash = actual?.HeadRevisionHash;
            if (expected.Revision != actualRevision
                || !string.Equals(
                    expected.RevisionHash,
                    actualHash,
                    StringComparison.Ordinal))
            {
                throw new FlowArtifactHeadConflictException(
                    flowKey,
                    expected,
                    actual);
            }
        }

        public static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default)
                value = DateTime.UtcNow;
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc),
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

        public static string ComputeRevisionHash(
            IEnumerable<PreparedFlowArtifactPart> artifacts,
            IEnumerable<FlowArtifactDependency> dependencies)
        {
            ArgumentNullException.ThrowIfNull(artifacts);
            ArgumentNullException.ThrowIfNull(dependencies);
            return ComputeRevisionHashCore(
                artifacts.Select(item =>
                    new RevisionHashArtifact(
                        item.Role,
                        item.Hash,
                        item.ContentType,
                        item.Content.Length)),
                dependencies);
        }

        public static string ComputeRevisionHash(
            IEnumerable<FlowArtifactDescriptor> artifacts,
            IEnumerable<FlowArtifactDependency> dependencies)
        {
            ArgumentNullException.ThrowIfNull(artifacts);
            ArgumentNullException.ThrowIfNull(dependencies);
            return ComputeRevisionHashCore(
                artifacts.Select(item =>
                    new RevisionHashArtifact(
                        item.Role,
                        NormalizeHash(
                            item.Hash,
                            nameof(item.Hash)),
                        NormalizeOptional(
                            item.ContentType,
                            128),
                        item.ContentLength)),
                dependencies);
        }

        private static string ComputeRevisionHashCore(
            IEnumerable<RevisionHashArtifact> artifacts,
            IEnumerable<FlowArtifactDependency> dependencies)
        {
            using IncrementalHash hash =
                IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            AppendField(hash, "colorvision-flow-artifact-revision/v1");

            foreach (RevisionHashArtifact artifact in artifacts
                .OrderBy(item => item.Role, StringComparer.Ordinal))
            {
                if (artifact.ContentLength < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(artifacts),
                        "Artifact 内容长度不能为负数。");
                }
                AppendField(hash, "artifact");
                AppendField(hash, artifact.Role);
                AppendField(hash, artifact.Hash);
                AppendField(hash, artifact.ContentType ?? string.Empty);
                AppendField(
                    hash,
                    artifact.ContentLength.ToString(
                        System.Globalization.CultureInfo.InvariantCulture));
            }
            foreach (FlowArtifactDependency dependency in dependencies
                .OrderBy(
                    item => item.DependencyKey,
                    StringComparer.Ordinal))
            {
                AppendField(hash, "dependency");
                AppendField(hash, dependency.DependencyKey);
                AppendField(hash, dependency.FlowKey);
                AppendField(
                    hash,
                    dependency.Revision.ToString(
                        System.Globalization.CultureInfo.InvariantCulture));
                AppendField(hash, dependency.ContentHash);
                AppendField(hash, dependency.DefinitionHash);
            }
            return Convert.ToHexString(
                    hash.GetHashAndReset())
                .ToLowerInvariant();
        }

        private static PreparedFlowArtifactPart PrepareArtifact(
            FlowArtifactContent artifact)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            if (string.IsNullOrWhiteSpace(artifact.Role))
                throw new ArgumentException("Artifact 角色不能为空。");
            string role = artifact.Role.Trim();
            if (role.Length > MaximumRoleLength
                || role.IndexOfAny(['\r', '\n', '\0']) >= 0)
            {
                throw new ArgumentException(
                    $"Artifact 角色长度不能超过 {MaximumRoleLength}，"
                    + "且不能包含控制字符。");
            }
            byte[] content = artifact.Content
                ?? throw new ArgumentException(
                    "Artifact 内容不能为 null。",
                    nameof(artifact));
            byte[] stableContent = (byte[])content.Clone();
            return new PreparedFlowArtifactPart(
                role,
                NormalizeOptional(artifact.ContentType, 128),
                stableContent,
                ComputeBlobHash(stableContent));
        }

        private static FlowArtifactDependency PrepareDependency(
            FlowArtifactDependency dependency)
        {
            ArgumentNullException.ThrowIfNull(dependency);
            if (string.IsNullOrWhiteSpace(dependency.DependencyKey))
            {
                throw new ArgumentException(
                    "依赖槽位不能为空。",
                    nameof(dependency));
            }
            string dependencyKey = dependency.DependencyKey.Trim();
            if (dependencyKey.Length > MaximumDependencyKeyLength
                || dependencyKey.IndexOfAny(['\r', '\n', '\0']) >= 0)
            {
                throw new ArgumentException(
                    $"依赖槽位长度不能超过 {MaximumDependencyKeyLength}，"
                    + "且不能包含控制字符。",
                    nameof(dependency));
            }
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                dependency.Revision);
            return new FlowArtifactDependency
            {
                DependencyKey = dependencyKey,
                FlowKey = NormalizeFlowKey(dependency.FlowKey),
                Revision = dependency.Revision,
                ContentHash = NormalizeHash(
                    dependency.ContentHash,
                    nameof(dependency.ContentHash)),
                DefinitionHash = NormalizeHash(
                    dependency.DefinitionHash,
                    nameof(dependency.DefinitionHash)),
            };
        }

        private static void AppendField(
            IncrementalHash hash,
            string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            byte[] length = BitConverter.GetBytes(bytes.Length);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
    }

    internal sealed record PreparedFlowArtifactPart(
        string Role,
        string? ContentType,
        byte[] Content,
        string Hash);

    internal sealed record PreparedFlowArtifactRevision(
        string FlowKey,
        IReadOnlyList<PreparedFlowArtifactPart> Artifacts,
        IReadOnlyList<FlowArtifactDependency> Dependencies,
        FlowArtifactHeadCondition ExpectedHead,
        string RevisionHash,
        bool PublishImmediately,
        string? Source,
        string? Author,
        string? Message,
        string? ExternalVersion,
        DateTime CreatedTimeUtc);

    internal sealed record PreparedFlowArtifactTransition(
        string FlowKey,
        int Revision,
        FlowArtifactHeadCondition ExpectedHead,
        string? Actor,
        string? Message,
        DateTime ChangedTimeUtc);

    internal sealed record RevisionHashArtifact(
        string Role,
        string Hash,
        string? ContentType,
        int ContentLength);
}
