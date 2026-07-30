using FlowEngineLib.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorVision.Engine.Templates.Flow.Routing
{
    internal static class FlowExecutionPolicyRules
    {
        private const int MaximumNodeIdLength = 256;
        private const int MaximumDelayMs = 86_400_000;

        private static readonly JsonSerializerOptions HashJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            Converters =
            {
                new JsonStringEnumConverter(),
            },
        };

        public static string NormalizeFlowKey(string flowKey)
        {
            if (string.IsNullOrWhiteSpace(flowKey))
                throw new ArgumentException("FlowKey 不能为空。", nameof(flowKey));

            string normalized = flowKey.Trim();
            if (normalized.Length > 256)
            {
                throw new ArgumentException(
                    "FlowKey 长度不能超过 256。",
                    nameof(flowKey));
            }
            EnsureNoControlCharacters(normalized, nameof(flowKey));
            return normalized;
        }

        public static NormalizedFlowExecutionPolicy Normalize(
            string flowKey,
            IEnumerable<FlowErrorRoutePolicy>? errorRoutes,
            IEnumerable<FlowRetryPolicy>? retryPolicies)
        {
            string normalizedFlowKey = NormalizeFlowKey(flowKey);
            var normalizedRoutes = new List<FlowErrorRoutePolicy>();
            var routeBindings =
                new HashSet<(string NodeId, FlowFailureKind Kind)>();
            foreach (FlowErrorRoutePolicy route in
                errorRoutes ?? Array.Empty<FlowErrorRoutePolicy>())
            {
                ArgumentNullException.ThrowIfNull(route);
                string sourceNodeId = NormalizeNodeId(
                    route.SourceNodeId,
                    nameof(route.SourceNodeId));
                string targetNodeId = NormalizeNodeId(
                    route.TargetNodeId,
                    nameof(route.TargetNodeId));
                if (string.Equals(
                    sourceNodeId,
                    targetNodeId,
                    StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"错误路由 {sourceNodeId} 不能指向节点自身。",
                        nameof(errorRoutes));
                }
                if (route.TargetInputIndex < 0
                    || route.TargetInputIndex > 1_023)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(errorRoutes),
                        "目标输入索引必须介于 0 和 1023 之间。");
                }

                IReadOnlyList<FlowFailureKind> failureKinds =
                    NormalizeKinds(
                        route.FailureKinds,
                        nameof(route.FailureKinds));
                foreach (FlowFailureKind kind in failureKinds)
                {
                    if (!routeBindings.Add((sourceNodeId, kind)))
                    {
                        throw new ArgumentException(
                            $"节点 {sourceNodeId} 的 {kind} 错误存在重复路由。",
                            nameof(errorRoutes));
                    }
                }

                normalizedRoutes.Add(new FlowErrorRoutePolicy(
                    sourceNodeId,
                    targetNodeId,
                    route.TargetInputIndex,
                    failureKinds));
            }

            var normalizedRetries = new List<FlowRetryPolicy>();
            var retryNodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (FlowRetryPolicy retry in
                retryPolicies ?? Array.Empty<FlowRetryPolicy>())
            {
                ArgumentNullException.ThrowIfNull(retry);
                string nodeId = NormalizeNodeId(
                    retry.NodeId,
                    nameof(retry.NodeId));
                if (!retryNodeIds.Add(nodeId))
                {
                    throw new ArgumentException(
                        $"节点 {nodeId} 存在重复重试策略。",
                        nameof(retryPolicies));
                }
                if (retry.MaxAttempts < 1 || retry.MaxAttempts > 100)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(retryPolicies),
                        "最大尝试次数必须介于 1 和 100 之间。");
                }
                if (retry.InitialDelayMs < 0
                    || retry.InitialDelayMs > MaximumDelayMs)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(retryPolicies),
                        $"初始延迟必须介于 0 和 {MaximumDelayMs} 毫秒之间。");
                }
                if (!double.IsFinite(retry.Backoff)
                    || retry.Backoff < 1
                    || retry.Backoff > 100)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(retryPolicies),
                        "退避倍数必须是介于 1 和 100 之间的有限数值。");
                }
                if (retry.MaxDelayMs < retry.InitialDelayMs
                    || retry.MaxDelayMs > MaximumDelayMs)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(retryPolicies),
                        $"最大延迟必须不小于初始延迟且不超过 {MaximumDelayMs} 毫秒。");
                }

                IReadOnlyList<FlowFailureKind> retryableKinds =
                    NormalizeKinds(
                        retry.RetryableKinds,
                        nameof(retry.RetryableKinds));
                if (retryableKinds.Contains(FlowFailureKind.Canceled))
                {
                    throw new ArgumentException(
                        "Canceled 不能配置为可重试错误。",
                        nameof(retryPolicies));
                }

                normalizedRetries.Add(new FlowRetryPolicy(
                    nodeId,
                    retry.MaxAttempts,
                    retry.InitialDelayMs,
                    retry.Backoff,
                    retry.MaxDelayMs,
                    retryableKinds));
            }

            normalizedRoutes.Sort(CompareRoutes);
            normalizedRetries.Sort(
                static (left, right) => string.Compare(
                    left.NodeId,
                    right.NodeId,
                    StringComparison.Ordinal));

            string contentHash = ComputeContentHash(
                normalizedFlowKey,
                normalizedRoutes,
                normalizedRetries);
            return new NormalizedFlowExecutionPolicy(
                normalizedFlowKey,
                normalizedRoutes,
                normalizedRetries,
                contentHash);
        }

        public static string NormalizeHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash)
                || hash.Length != 64
                || hash.Any(value => !Uri.IsHexDigit(value)))
            {
                throw new ArgumentException(
                    "内容哈希必须是 64 位十六进制 SHA-256。",
                    nameof(hash));
            }
            return hash.ToLowerInvariant();
        }

        public static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default)
                throw new ArgumentException("更新时间不能为空。", nameof(value));
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            };
        }

        private static string NormalizeNodeId(
            string nodeId,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                throw new ArgumentException(
                    "节点 ID 不能为空。",
                    parameterName);
            }

            string normalized = nodeId.Trim();
            if (normalized.Length > MaximumNodeIdLength)
            {
                throw new ArgumentException(
                    $"节点 ID 长度不能超过 {MaximumNodeIdLength}。",
                    parameterName);
            }
            EnsureNoControlCharacters(normalized, parameterName);
            if (!Guid.TryParse(normalized, out Guid guid)
                || guid == Guid.Empty)
            {
                throw new ArgumentException(
                    "节点 ID 必须是非空 GUID。",
                    parameterName);
            }
            return guid.ToString("D");
        }

        private static FlowFailureKind[] NormalizeKinds(
            IReadOnlyList<FlowFailureKind>? values,
            string parameterName)
        {
            if (values == null || values.Count == 0)
            {
                throw new ArgumentException(
                    "错误类型集合不能为空。",
                    parameterName);
            }

            var unique = new HashSet<FlowFailureKind>();
            foreach (FlowFailureKind value in values)
            {
                if (!Enum.IsDefined(value))
                {
                    throw new ArgumentOutOfRangeException(
                        parameterName,
                        value,
                        "包含无法识别的错误类型。");
                }
                if (!unique.Add(value))
                {
                    throw new ArgumentException(
                        $"错误类型 {value} 重复。",
                        parameterName);
                }
            }
            return unique.OrderBy(value => value).ToArray();
        }

        private static int CompareRoutes(
            FlowErrorRoutePolicy left,
            FlowErrorRoutePolicy right)
        {
            int result = string.Compare(
                left.SourceNodeId,
                right.SourceNodeId,
                StringComparison.Ordinal);
            if (result != 0)
                return result;
            result = left.FailureKinds[0].CompareTo(
                right.FailureKinds[0]);
            if (result != 0)
                return result;
            result = string.Compare(
                left.TargetNodeId,
                right.TargetNodeId,
                StringComparison.Ordinal);
            return result != 0
                ? result
                : left.TargetInputIndex.CompareTo(
                    right.TargetInputIndex);
        }

        private static string ComputeContentHash(
            string flowKey,
            IReadOnlyList<FlowErrorRoutePolicy> errorRoutes,
            IReadOnlyList<FlowRetryPolicy> retryPolicies)
        {
            byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(
                new FlowExecutionPolicyHashDocument
                {
                    FlowKey = flowKey,
                    ErrorRoutes = errorRoutes,
                    RetryPolicies = retryPolicies,
                },
                HashJsonOptions);
            return Convert.ToHexString(
                SHA256.HashData(canonical)).ToLowerInvariant();
        }

        private static void EnsureNoControlCharacters(
            string value,
            string parameterName)
        {
            if (value.Any(char.IsControl))
            {
                throw new ArgumentException(
                    "值包含控制字符。",
                    parameterName);
            }
        }

        private sealed class FlowExecutionPolicyHashDocument
        {
            public string FlowKey { get; init; } = string.Empty;

            public IReadOnlyList<FlowErrorRoutePolicy> ErrorRoutes
            {
                get;
                init;
            } = Array.Empty<FlowErrorRoutePolicy>();

            public IReadOnlyList<FlowRetryPolicy> RetryPolicies
            {
                get;
                init;
            } = Array.Empty<FlowRetryPolicy>();
        }
    }

    internal sealed record NormalizedFlowExecutionPolicy(
        string FlowKey,
        IReadOnlyList<FlowErrorRoutePolicy> ErrorRoutes,
        IReadOnlyList<FlowRetryPolicy> RetryPolicies,
        string ContentHash);
}
