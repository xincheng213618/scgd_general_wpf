using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Artifacts.Persistence
{
    public static class FlowArtifactRoles
    {
        public const string AuthoringCanvas = "authoring-canvas";

        public const string CompiledCanvas = "compiled-canvas";

        public const string SemanticDocument = "semantic-document";

        public const string SubflowSidecar = "subflow-sidecar";

        public const string AuthoringPolicy = "authoring-policy";

        public const string ExecutionPolicy = "execution-policy";

        public const string CompilationMap = "compilation-map";

        public const string CompilationManifest = "compilation-manifest";
    }

    public enum FlowArtifactRevisionState
    {
        Draft = 0,
        Published = 1,
        Aborted = 2,
    }

    public sealed class FlowArtifactContent
    {
        public string Role { get; init; } = string.Empty;

        public string? ContentType { get; init; }

        public byte[] Content { get; init; } = Array.Empty<byte>();
    }

    public sealed class FlowArtifactDependency
    {
        /// <summary>
        /// Stable slot within the authoring graph, normally the subflow node
        /// GUID. A revision cannot bind the same slot twice.
        /// </summary>
        public string DependencyKey { get; init; } = string.Empty;

        public string FlowKey { get; init; } = string.Empty;

        public int Revision { get; init; }

        public string ContentHash { get; init; } = string.Empty;

        public string DefinitionHash { get; init; } = string.Empty;

        public FlowArtifactDependency DeepClone()
        {
            return new FlowArtifactDependency
            {
                DependencyKey = DependencyKey,
                FlowKey = FlowKey,
                Revision = Revision,
                ContentHash = ContentHash,
                DefinitionHash = DefinitionHash,
            };
        }
    }

    public sealed class FlowArtifactDescriptor
    {
        public string Role { get; init; } = string.Empty;

        public string Hash { get; init; } = string.Empty;

        public int ContentLength { get; init; }

        public string? ContentType { get; init; }

        public FlowArtifactDescriptor DeepClone()
        {
            return new FlowArtifactDescriptor
            {
                Role = Role,
                Hash = Hash,
                ContentLength = ContentLength,
                ContentType = ContentType,
            };
        }
    }

    public sealed class FlowArtifactBlob
    {
        public string Hash { get; init; } = string.Empty;

        public byte[] Content { get; init; } = Array.Empty<byte>();

        public DateTime CreatedTimeUtc { get; init; }

        public FlowArtifactBlob DeepClone()
        {
            return new FlowArtifactBlob
            {
                Hash = Hash,
                Content = (byte[])Content.Clone(),
                CreatedTimeUtc = CreatedTimeUtc,
            };
        }
    }

    public sealed class FlowArtifactRevision
    {
        public string FlowKey { get; init; } = string.Empty;

        public int Revision { get; init; }

        public int? ParentRevision { get; init; }

        public string RevisionHash { get; init; } = string.Empty;

        public FlowArtifactRevisionState State { get; init; }

        public IReadOnlyList<FlowArtifactDescriptor> Artifacts { get; init; } =
            Array.Empty<FlowArtifactDescriptor>();

        public IReadOnlyList<FlowArtifactDependency> Dependencies { get; init; } =
            Array.Empty<FlowArtifactDependency>();

        public string? Source { get; init; }

        public string? Author { get; init; }

        public string? Message { get; init; }

        public string? ExternalVersion { get; init; }

        public DateTime CreatedTimeUtc { get; init; }

        public DateTime StateChangedTimeUtc { get; init; }

        public string? StateChangedBy { get; init; }

        public string? StateChangeMessage { get; init; }

        public FlowArtifactRevision DeepClone()
        {
            return new FlowArtifactRevision
            {
                FlowKey = FlowKey,
                Revision = Revision,
                ParentRevision = ParentRevision,
                RevisionHash = RevisionHash,
                State = State,
                Artifacts = Artifacts
                    .Select(item => item.DeepClone())
                    .ToArray(),
                Dependencies = Dependencies
                    .Select(item => item.DeepClone())
                    .ToArray(),
                Source = Source,
                Author = Author,
                Message = Message,
                ExternalVersion = ExternalVersion,
                CreatedTimeUtc = CreatedTimeUtc,
                StateChangedTimeUtc = StateChangedTimeUtc,
                StateChangedBy = StateChangedBy,
                StateChangeMessage = StateChangeMessage,
            };
        }
    }

    public sealed class FlowArtifactReference
    {
        public string FlowKey { get; init; } = string.Empty;

        /// <summary>
        /// Monotonic allocator. It is not rewound when a draft is aborted.
        /// </summary>
        public int LastRevision { get; init; }

        public int? HeadRevision { get; init; }

        public string? HeadRevisionHash { get; init; }

        public int? PublishedRevision { get; init; }

        public string? PublishedRevisionHash { get; init; }

        public DateTime UpdatedTimeUtc { get; init; }

        public FlowArtifactReference DeepClone()
        {
            return new FlowArtifactReference
            {
                FlowKey = FlowKey,
                LastRevision = LastRevision,
                HeadRevision = HeadRevision,
                HeadRevisionHash = HeadRevisionHash,
                PublishedRevision = PublishedRevision,
                PublishedRevisionHash = PublishedRevisionHash,
                UpdatedTimeUtc = UpdatedTimeUtc,
            };
        }
    }

    public sealed record FlowArtifactHeadCondition(
        int? Revision,
        string? RevisionHash)
    {
        public static FlowArtifactHeadCondition Initial { get; } =
            new(null, null);

        public static FlowArtifactHeadCondition FromRevision(
            FlowArtifactRevision revision)
        {
            ArgumentNullException.ThrowIfNull(revision);
            return new FlowArtifactHeadCondition(
                revision.Revision,
                revision.RevisionHash);
        }
    }

    public sealed class FlowArtifactRevisionWriteRequest
    {
        public string FlowKey { get; init; } = string.Empty;

        public IReadOnlyCollection<FlowArtifactContent> Artifacts { get; init; } =
            Array.Empty<FlowArtifactContent>();

        public IReadOnlyCollection<FlowArtifactDependency> Dependencies { get; init; } =
            Array.Empty<FlowArtifactDependency>();

        public FlowArtifactHeadCondition ExpectedHead { get; init; } =
            FlowArtifactHeadCondition.Initial;

        /// <summary>
        /// When true, the revision and the published reference are committed
        /// atomically. Otherwise the revision starts as a draft.
        /// </summary>
        public bool PublishImmediately { get; init; }

        public string? Source { get; init; }

        public string? Author { get; init; }

        public string? Message { get; init; }

        public string? ExternalVersion { get; init; }

        public DateTime? CreatedTimeUtc { get; init; }
    }

    public sealed class FlowArtifactRevisionTransitionRequest
    {
        public string FlowKey { get; init; } = string.Empty;

        public int Revision { get; init; }

        public FlowArtifactHeadCondition ExpectedHead { get; init; } =
            FlowArtifactHeadCondition.Initial;

        public string? Actor { get; init; }

        public string? Message { get; init; }

        public DateTime? ChangedTimeUtc { get; init; }
    }

    public interface IFlowArtifactStore
    {
        FlowArtifactBlob PutArtifact(byte[] content);

        FlowArtifactBlob? GetArtifact(string hash);

        FlowArtifactReference? GetReference(string flowKey);

        FlowArtifactRevision? GetHead(string flowKey);

        FlowArtifactRevision? GetPublished(string flowKey);

        FlowArtifactRevision? GetRevision(
            string flowKey,
            int revision);

        IReadOnlyList<FlowArtifactRevision> ListRevisions(
            string flowKey);

        FlowArtifactRevision Append(
            FlowArtifactRevisionWriteRequest request);

        FlowArtifactRevision Publish(
            FlowArtifactRevisionTransitionRequest request);

        FlowArtifactRevision Abort(
            FlowArtifactRevisionTransitionRequest request);
    }

    public sealed class FlowArtifactHeadConflictException :
        InvalidOperationException
    {
        public FlowArtifactHeadConflictException(
            string flowKey,
            FlowArtifactHeadCondition expected,
            FlowArtifactReference? actual)
            : base(CreateMessage(flowKey, expected, actual))
        {
            FlowKey = flowKey;
            Expected = expected;
            Actual = actual?.DeepClone();
        }

        public string FlowKey { get; }

        public FlowArtifactHeadCondition Expected { get; }

        public FlowArtifactReference? Actual { get; }

        private static string CreateMessage(
            string flowKey,
            FlowArtifactHeadCondition expected,
            FlowArtifactReference? actual)
        {
            string expectedValue =
                $"{expected.Revision?.ToString() ?? "<none>"}/"
                + $"{expected.RevisionHash ?? "<none>"}";
            string actualValue = actual?.HeadRevision == null
                ? "<none>/<none>"
                : $"{actual.HeadRevision}/{actual.HeadRevisionHash}";
            return $"流程 {flowKey} 的共享版本基线已变化；"
                + $"期望 {expectedValue}，实际 {actualValue}。";
        }
    }
}
