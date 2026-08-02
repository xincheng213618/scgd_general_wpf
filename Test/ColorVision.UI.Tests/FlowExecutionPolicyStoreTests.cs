using ColorVision.Engine.Templates.Flow.Routing;
using FlowEngineLib.Runtime;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FlowExecutionPolicyStoreTests
{
    private const string FlowKey = "flow:11111111111111111111111111111111";
    private const string SourceNode =
        "98c02e1b-e0a7-4868-a7cf-c7be72d376f1";
    private const string TargetNode =
        "3b1ed2a1-6b93-4aab-ae6a-1478b5bb8677";

    [Fact]
    public void RoundTripNormalizesAndReturnsImmutableSnapshot()
    {
        using TempDirectory directory = new();
        var store = new JsonFlowExecutionPolicyStore(directory.Path);
        var mutableKinds = new List<FlowFailureKind>
        {
            FlowFailureKind.Technical,
            FlowFailureKind.Business,
        };
        var route = new FlowErrorRoutePolicy(
            $" {SourceNode.ToUpperInvariant()} ",
            $" {TargetNode.ToUpperInvariant()} ",
            targetInputIndex: 2,
            mutableKinds);
        mutableKinds.Add(FlowFailureKind.Timeout);

        FlowExecutionPolicySnapshot saved = store.Save(
            new FlowExecutionPolicySaveRequest(
                $" {FlowKey} ",
                expectedRevision: 0,
                errorRoutes:
                [
                    route,
                ],
                retryPolicies:
                [
                    new FlowRetryPolicy(
                        SourceNode,
                        maxAttempts: 4,
                        initialDelayMs: 100,
                        backoff: 2,
                        maxDelayMs: 2_000,
                        retryableKinds:
                        [
                            FlowFailureKind.Timeout,
                            FlowFailureKind.Technical,
                        ]),
                ]));

        Assert.Equal(FlowKey, saved.FlowKey);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(64, saved.ContentHash.Length);
        Assert.Equal(DateTimeKind.Utc, saved.UpdatedTimeUtc.Kind);
        FlowErrorRoutePolicy savedRoute = Assert.Single(
            saved.ErrorRoutes);
        Assert.Equal(SourceNode, savedRoute.SourceNodeId);
        Assert.Equal(TargetNode, savedRoute.TargetNodeId);
        Assert.Equal(
            [
                FlowFailureKind.Business,
                FlowFailureKind.Technical,
            ],
            savedRoute.FailureKinds);
        Assert.Single(saved.RetryPolicies);

        Assert.IsType<ReadOnlyCollection<FlowErrorRoutePolicy>>(
            saved.ErrorRoutes);
        Assert.Throws<NotSupportedException>(() =>
            ((ICollection<FlowErrorRoutePolicy>)saved.ErrorRoutes).Add(
                route));
        Assert.Throws<NotSupportedException>(() =>
            ((ICollection<FlowFailureKind>)savedRoute.FailureKinds).Add(
                FlowFailureKind.Contract));

        var reopened =
            new JsonFlowExecutionPolicyStore(directory.Path);
        FlowExecutionPolicySnapshot loaded = reopened.Load(FlowKey);
        Assert.Equal(saved.Revision, loaded.Revision);
        Assert.Equal(saved.ContentHash, loaded.ContentHash);
        Assert.Equal(
            saved.UpdatedTimeUtc,
            loaded.UpdatedTimeUtc);
        Assert.Equal(
            savedRoute.FailureKinds,
            Assert.Single(loaded.ErrorRoutes).FailureKinds);
        Assert.Single(Directory.GetFiles(
            directory.Path,
            "*.flow-routing.json"));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.stn"));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.cvflow"));
    }

    [Fact]
    public void ExpectedRevisionRejectsStaleWriterWithoutChangingContent()
    {
        using TempDirectory directory = new();
        var first = new JsonFlowExecutionPolicyStore(directory.Path);
        var second = new JsonFlowExecutionPolicyStore(directory.Path);
        FlowExecutionPolicySnapshot stale = second.Load(FlowKey);
        Assert.Equal(0, stale.Revision);

        FlowExecutionPolicySnapshot saved = first.Save(
            CreateRequest(expectedRevision: 0));
        string filePath = Assert.Single(
            Directory.GetFiles(
                directory.Path,
                "*.flow-routing.json"));
        byte[] beforeConflict = File.ReadAllBytes(filePath);

        FlowExecutionPolicyConflictException exception =
            Assert.Throws<FlowExecutionPolicyConflictException>(() =>
                second.Save(CreateRequest(stale.Revision)));

        Assert.Equal(0, exception.ExpectedRevision);
        Assert.Equal(1, exception.ActualRevision);
        Assert.Equal(beforeConflict, File.ReadAllBytes(filePath));
        Assert.Equal(saved.ContentHash, first.Load(FlowKey).ContentHash);
    }

    [Fact]
    public void CorruptSidecarFailsSafeAndSaveWillNotOverwriteEvidence()
    {
        using TempDirectory directory = new();
        var store = new JsonFlowExecutionPolicyStore(directory.Path);
        store.Save(CreateRequest(expectedRevision: 0));
        string filePath = Assert.Single(
            Directory.GetFiles(
                directory.Path,
                "*.flow-routing.json"));
        File.WriteAllText(filePath, """{"schemaVersion":1,"flowKey":""");
        byte[] corruptEvidence = File.ReadAllBytes(filePath);

        bool loaded = store.TryLoad(
            FlowKey,
            out FlowExecutionPolicySnapshot fallback,
            out string? error);

        Assert.False(loaded);
        Assert.Equal(FlowKey, fallback.FlowKey);
        Assert.Equal(0, fallback.Revision);
        Assert.Empty(fallback.ErrorRoutes);
        Assert.Empty(fallback.RetryPolicies);
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Throws<FlowExecutionPolicyCorruptException>(() =>
            store.Load(FlowKey));
        Assert.Throws<FlowExecutionPolicyCorruptException>(() =>
            store.Save(CreateRequest(expectedRevision: 1)));
        Assert.Equal(corruptEvidence, File.ReadAllBytes(filePath));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public void ContentHashIsStableAcrossEquivalentInputOrder()
    {
        using TempDirectory firstDirectory = new();
        using TempDirectory secondDirectory = new();
        var first =
            new JsonFlowExecutionPolicyStore(firstDirectory.Path);
        var second =
            new JsonFlowExecutionPolicyStore(secondDirectory.Path);
        const string source2 =
            "512f24a2-fe19-4a09-bcc8-950a88efb985";
        const string target2 =
            "f0510056-5093-43af-ac26-2f5a819666a3";

        FlowExecutionPolicySnapshot firstSnapshot = first.Save(
            new FlowExecutionPolicySaveRequest(
                FlowKey,
                expectedRevision: 0,
                errorRoutes:
                [
                    new FlowErrorRoutePolicy(
                        source2,
                        target2,
                        0,
                        [FlowFailureKind.Timeout]),
                    new FlowErrorRoutePolicy(
                        SourceNode,
                        TargetNode,
                        1,
                        [
                            FlowFailureKind.Technical,
                            FlowFailureKind.Business,
                        ]),
                ],
                retryPolicies:
                [
                    CreateRetry(source2),
                    CreateRetry(SourceNode),
                ]));
        FlowExecutionPolicySnapshot secondSnapshot = second.Save(
            new FlowExecutionPolicySaveRequest(
                $" {FlowKey} ",
                expectedRevision: 0,
                errorRoutes:
                [
                    new FlowErrorRoutePolicy(
                        SourceNode.ToUpperInvariant(),
                        TargetNode.ToUpperInvariant(),
                        1,
                        [
                            FlowFailureKind.Business,
                            FlowFailureKind.Technical,
                        ]),
                    new FlowErrorRoutePolicy(
                        source2.ToUpperInvariant(),
                        target2.ToUpperInvariant(),
                        0,
                        [FlowFailureKind.Timeout]),
                ],
                retryPolicies:
                [
                    CreateRetry(SourceNode.ToUpperInvariant()),
                    CreateRetry(source2.ToUpperInvariant()),
                ]));

        Assert.Equal(
            firstSnapshot.ContentHash,
            secondSnapshot.ContentHash);
    }

    [Fact]
    public void AmbiguousRoutesDuplicateRetriesAndCanceledRetryAreRejected()
    {
        using TempDirectory directory = new();
        var store = new JsonFlowExecutionPolicyStore(directory.Path);

        Assert.Throws<ArgumentException>(() =>
            store.Save(new FlowExecutionPolicySaveRequest(
                FlowKey,
                expectedRevision: 0,
                errorRoutes:
                [
                    new FlowErrorRoutePolicy(
                        SourceNode,
                        TargetNode,
                        0,
                        [FlowFailureKind.Technical]),
                    new FlowErrorRoutePolicy(
                        SourceNode,
                        "512f24a2-fe19-4a09-bcc8-950a88efb985",
                        0,
                        [FlowFailureKind.Technical]),
                ])));

        Assert.Throws<ArgumentException>(() =>
            store.Save(new FlowExecutionPolicySaveRequest(
                FlowKey,
                expectedRevision: 0,
                retryPolicies:
                [
                    CreateRetry(SourceNode),
                    CreateRetry(SourceNode.ToUpperInvariant()),
                ])));

        Assert.Throws<ArgumentException>(() =>
            store.Save(new FlowExecutionPolicySaveRequest(
                FlowKey,
                expectedRevision: 0,
                retryPolicies:
                [
                    new FlowRetryPolicy(
                        SourceNode,
                        maxAttempts: 2,
                        initialDelayMs: 0,
                        backoff: 1,
                        maxDelayMs: 0,
                        retryableKinds:
                        [
                            FlowFailureKind.Canceled,
                        ]),
                ])));

        Assert.Throws<ArgumentException>(() =>
            store.Save(new FlowExecutionPolicySaveRequest(
                FlowKey,
                expectedRevision: 0,
                errorRoutes:
                [
                    new FlowErrorRoutePolicy(
                        "legacy-node-name",
                        TargetNode,
                        0,
                        [FlowFailureKind.Technical]),
                ])));

        Assert.False(Directory.Exists(directory.Path)
            && Directory.EnumerateFileSystemEntries(
                directory.Path).Any());
    }

    private static FlowExecutionPolicySaveRequest CreateRequest(
        long expectedRevision)
    {
        return new FlowExecutionPolicySaveRequest(
            FlowKey,
            expectedRevision,
            errorRoutes:
            [
                new FlowErrorRoutePolicy(
                    SourceNode,
                    TargetNode,
                    0,
                    [FlowFailureKind.Technical]),
            ],
            retryPolicies:
            [
                CreateRetry(SourceNode),
            ]);
    }

    private static FlowRetryPolicy CreateRetry(string nodeId)
    {
        return new FlowRetryPolicy(
            nodeId,
            maxAttempts: 3,
            initialDelayMs: 50,
            backoff: 2,
            maxDelayMs: 1_000,
            retryableKinds:
            [
                FlowFailureKind.Technical,
                FlowFailureKind.Timeout,
            ]);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"colorvision-flow-routing-{Guid.NewGuid():N}");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }

}
