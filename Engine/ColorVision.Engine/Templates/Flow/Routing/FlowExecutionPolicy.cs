using FlowEngineLib.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace ColorVision.Engine.Templates.Flow.Routing
{
    public sealed class FlowErrorRoutePolicy
    {
        [JsonConstructor]
        public FlowErrorRoutePolicy(
            string sourceNodeId,
            string targetNodeId,
            int targetInputIndex,
            IReadOnlyList<FlowFailureKind>? failureKinds)
        {
            SourceNodeId = sourceNodeId ?? string.Empty;
            TargetNodeId = targetNodeId ?? string.Empty;
            TargetInputIndex = targetInputIndex;
            FailureKinds = Copy(failureKinds);
        }

        public string SourceNodeId { get; }

        public string TargetNodeId { get; }

        public int TargetInputIndex { get; }

        public IReadOnlyList<FlowFailureKind> FailureKinds { get; }

        private static ReadOnlyCollection<FlowFailureKind> Copy(
            IReadOnlyList<FlowFailureKind>? values)
        {
            return new ReadOnlyCollection<FlowFailureKind>(
                values?.ToArray() ?? Array.Empty<FlowFailureKind>());
        }
    }

    public sealed class FlowRetryPolicy
    {
        [JsonConstructor]
        public FlowRetryPolicy(
            string nodeId,
            int maxAttempts,
            int initialDelayMs,
            double backoff,
            int maxDelayMs,
            IReadOnlyList<FlowFailureKind>? retryableKinds)
        {
            NodeId = nodeId ?? string.Empty;
            MaxAttempts = maxAttempts;
            InitialDelayMs = initialDelayMs;
            Backoff = backoff;
            MaxDelayMs = maxDelayMs;
            RetryableKinds = new ReadOnlyCollection<FlowFailureKind>(
                retryableKinds?.ToArray()
                    ?? Array.Empty<FlowFailureKind>());
        }

        public string NodeId { get; }

        /// <summary>
        /// Total number of attempts, including the initial attempt.
        /// </summary>
        public int MaxAttempts { get; }

        public int InitialDelayMs { get; }

        public double Backoff { get; }

        public int MaxDelayMs { get; }

        public IReadOnlyList<FlowFailureKind> RetryableKinds { get; }
    }

    /// <summary>
    /// Immutable, detached view of all execution policies for one stable
    /// FlowKey. Revision zero represents an absent sidecar.
    /// </summary>
    public sealed class FlowExecutionPolicySnapshot
    {
        internal FlowExecutionPolicySnapshot(
            string flowKey,
            long revision,
            string contentHash,
            DateTime updatedTimeUtc,
            IReadOnlyList<FlowErrorRoutePolicy> errorRoutes,
            IReadOnlyList<FlowRetryPolicy> retryPolicies)
        {
            FlowKey = flowKey;
            Revision = revision;
            ContentHash = contentHash;
            UpdatedTimeUtc = updatedTimeUtc;
            ErrorRoutes = new ReadOnlyCollection<FlowErrorRoutePolicy>(
                errorRoutes.ToArray());
            RetryPolicies = new ReadOnlyCollection<FlowRetryPolicy>(
                retryPolicies.ToArray());
        }

        public string FlowKey { get; }

        public long Revision { get; }

        public string ContentHash { get; }

        public DateTime UpdatedTimeUtc { get; }

        public IReadOnlyList<FlowErrorRoutePolicy> ErrorRoutes { get; }

        public IReadOnlyList<FlowRetryPolicy> RetryPolicies { get; }
    }

    public sealed class FlowExecutionPolicySaveRequest
    {
        public FlowExecutionPolicySaveRequest(
            string flowKey,
            long expectedRevision,
            IEnumerable<FlowErrorRoutePolicy>? errorRoutes = null,
            IEnumerable<FlowRetryPolicy>? retryPolicies = null)
        {
            FlowKey = flowKey ?? string.Empty;
            ExpectedRevision = expectedRevision;
            ErrorRoutes = new ReadOnlyCollection<FlowErrorRoutePolicy>(
                errorRoutes?.ToArray()
                    ?? Array.Empty<FlowErrorRoutePolicy>());
            RetryPolicies = new ReadOnlyCollection<FlowRetryPolicy>(
                retryPolicies?.ToArray()
                    ?? Array.Empty<FlowRetryPolicy>());
        }

        public string FlowKey { get; }

        public long ExpectedRevision { get; }

        public IReadOnlyList<FlowErrorRoutePolicy> ErrorRoutes { get; }

        public IReadOnlyList<FlowRetryPolicy> RetryPolicies { get; }
    }

    public interface IFlowExecutionPolicyStore
    {
        FlowExecutionPolicySnapshot Load(string flowKey);

        bool TryLoad(
            string flowKey,
            out FlowExecutionPolicySnapshot snapshot,
            out string? failureReason);

        FlowExecutionPolicySnapshot Save(
            FlowExecutionPolicySaveRequest request);
    }

    public sealed class FlowExecutionPolicyConflictException :
        InvalidOperationException
    {
        public FlowExecutionPolicyConflictException(
            string flowKey,
            long expectedRevision,
            long actualRevision)
            : base(
                $"流程 {flowKey} 的执行策略已变化；期望 revision "
                + $"{expectedRevision}，实际 {actualRevision}。")
        {
            FlowKey = flowKey;
            ExpectedRevision = expectedRevision;
            ActualRevision = actualRevision;
        }

        public string FlowKey { get; }

        public long ExpectedRevision { get; }

        public long ActualRevision { get; }
    }

    public sealed class FlowExecutionPolicyCorruptException :
        IOException
    {
        public FlowExecutionPolicyCorruptException(
            string flowKey,
            string filePath,
            string message,
            Exception? innerException = null)
            : base(
                $"流程 {flowKey} 的执行策略侧车损坏：{message}",
                innerException)
        {
            FlowKey = flowKey;
            FilePath = filePath;
        }

        public string FlowKey { get; }

        public string FilePath { get; }
    }
}
