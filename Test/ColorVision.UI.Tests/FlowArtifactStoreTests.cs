using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Artifacts.Persistence;
using System.Collections.Concurrent;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class FlowArtifactStoreTests
{
    [Fact]
    public void PutArtifact_IsContentAddressedAndDefensivelyCloned()
    {
        var store = new InMemoryFlowArtifactStore();
        byte[] content = Encoding.UTF8.GetBytes("same-content");

        FlowArtifactBlob first = store.PutArtifact(content);
        content[0] = (byte)'X';
        FlowArtifactBlob second = store.PutArtifact(
            Encoding.UTF8.GetBytes("same-content"));

        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal("same-content", Encoding.UTF8.GetString(
            second.Content));

        first.Content[0] = (byte)'Y';
        FlowArtifactBlob persisted =
            Assert.IsType<FlowArtifactBlob>(
                store.GetArtifact(first.Hash));
        Assert.Equal("same-content", Encoding.UTF8.GetString(
            persisted.Content));
    }

    [Fact]
    public void AppendDraft_PersistsPartsDependenciesAndHead()
    {
        var store = new InMemoryFlowArtifactStore();
        FlowArtifactRevision revision = store.Append(
            CreateWriteRequest("flow:root"));

        Assert.Equal(1, revision.Revision);
        Assert.Null(revision.ParentRevision);
        Assert.Equal(
            FlowArtifactRevisionState.Draft,
            revision.State);
        Assert.Equal(
            new[]
            {
                FlowArtifactRoles.AuthoringCanvas,
                FlowArtifactRoles.CompiledCanvas,
            },
            revision.Artifacts.Select(item => item.Role));
        FlowArtifactDependency dependency =
            Assert.Single(revision.Dependencies);
        Assert.Equal("call-1", dependency.DependencyKey);
        Assert.Equal("flow:child", dependency.FlowKey);
        Assert.Equal(7, dependency.Revision);
        Assert.Equal(
            Hash("child-r7-content"),
            dependency.ContentHash);
        Assert.Equal(
            Hash("child-r7-definition"),
            dependency.DefinitionHash);

        FlowArtifactReference reference =
            Assert.IsType<FlowArtifactReference>(
                store.GetReference("flow:root"));
        Assert.Equal(1, reference.LastRevision);
        Assert.Equal(1, reference.HeadRevision);
        Assert.Equal(
            revision.RevisionHash,
            reference.HeadRevisionHash);
        Assert.Null(reference.PublishedRevision);
        Assert.Equal(
            revision.RevisionHash,
            store.GetHead("flow:root")?.RevisionHash);
        Assert.Null(store.GetPublished("flow:root"));

        foreach (FlowArtifactDescriptor descriptor in
            revision.Artifacts)
        {
            FlowArtifactBlob blob =
                Assert.IsType<FlowArtifactBlob>(
                    store.GetArtifact(descriptor.Hash));
            Assert.Equal(
                descriptor.ContentLength,
                blob.Content.Length);
        }
    }

    [Fact]
    public void Append_WithStaleHead_IsAtomicAndDoesNotCreateReference()
    {
        var store = new InMemoryFlowArtifactStore();
        var stale = CreateWriteRequest(
            "flow:root",
            new FlowArtifactHeadCondition(
                1,
                Hash("missing")));

        Assert.Throws<FlowArtifactHeadConflictException>(
            () => store.Append(stale));

        Assert.Null(store.GetReference("flow:root"));
        Assert.Empty(store.ListRevisions("flow:root"));
    }

    [Fact]
    public void PublishThenAbortDraft_RewindsHeadButNotAllocator()
    {
        var store = new InMemoryFlowArtifactStore();
        FlowArtifactRevision first = store.Append(
            CreateWriteRequest("flow:root"));
        FlowArtifactRevision published = store.Publish(
            new FlowArtifactRevisionTransitionRequest
            {
                FlowKey = "flow:root",
                Revision = first.Revision,
                ExpectedHead =
                    FlowArtifactHeadCondition.FromRevision(first),
                Actor = "operator",
                Message = "approve",
            });

        Assert.Equal(
            FlowArtifactRevisionState.Published,
            published.State);
        Assert.Equal(
            published.RevisionHash,
            store.GetPublished("flow:root")?.RevisionHash);

        FlowArtifactRevision second = store.Append(
            CreateWriteRequest(
                "flow:root",
                FlowArtifactHeadCondition.FromRevision(published),
                authoring: "authoring-v2",
                compiled: "compiled-v2"));
        FlowArtifactRevision aborted = store.Abort(
            new FlowArtifactRevisionTransitionRequest
            {
                FlowKey = "flow:root",
                Revision = second.Revision,
                ExpectedHead =
                    FlowArtifactHeadCondition.FromRevision(second),
                Actor = "operator",
                Message = "discard",
            });

        Assert.Equal(
            FlowArtifactRevisionState.Aborted,
            aborted.State);
        Assert.Equal(
            published.RevisionHash,
            store.GetHead("flow:root")?.RevisionHash);
        Assert.Equal(
            published.RevisionHash,
            store.GetPublished("flow:root")?.RevisionHash);

        FlowArtifactRevision third = store.Append(
            CreateWriteRequest(
                "flow:root",
                FlowArtifactHeadCondition.FromRevision(published),
                authoring: "authoring-v3",
                compiled: "compiled-v3"));
        Assert.Equal(3, third.Revision);
        Assert.Equal(1, third.ParentRevision);
        Assert.Equal(3, store.GetReference("flow:root")?.LastRevision);
    }

    [Fact]
    public void AppendPublished_UpdatesHeadAndPublishedAtomically()
    {
        var store = new InMemoryFlowArtifactStore();
        FlowArtifactRevision revision = store.Append(
            CreateWriteRequest(
                "flow:root",
                publishImmediately: true));

        Assert.Equal(
            FlowArtifactRevisionState.Published,
            revision.State);
        FlowArtifactReference reference =
            Assert.IsType<FlowArtifactReference>(
                store.GetReference("flow:root"));
        Assert.Equal(revision.Revision, reference.HeadRevision);
        Assert.Equal(
            revision.Revision,
            reference.PublishedRevision);
        Assert.Equal(
            reference.HeadRevisionHash,
            reference.PublishedRevisionHash);
    }

    [Fact]
    public void SameBlob_CanBackAuthoringAndCompiledRoles()
    {
        var store = new InMemoryFlowArtifactStore();
        FlowArtifactRevision revision = store.Append(
            CreateWriteRequest(
                "flow:root",
                authoring: "same-stnd",
                compiled: "same-stnd"));

        Assert.Equal(2, revision.Artifacts.Count);
        Assert.Single(
            revision.Artifacts
                .Select(item => item.Hash)
                .Distinct(StringComparer.Ordinal));
        Assert.NotNull(
            store.GetArtifact(revision.Artifacts[0].Hash));
    }

    [Fact]
    public void RevisionHash_IsIndependentOfInputOrdering()
    {
        var firstStore = new InMemoryFlowArtifactStore();
        var secondStore = new InMemoryFlowArtifactStore();
        FlowArtifactRevisionWriteRequest first =
            CreateWriteRequest("flow:root");
        FlowArtifactRevisionWriteRequest second =
            new()
            {
                FlowKey = first.FlowKey,
                Artifacts = first.Artifacts.Reverse().ToArray(),
                Dependencies =
                    first.Dependencies.Reverse().ToArray(),
                ExpectedHead = first.ExpectedHead,
                Source = first.Source,
                Author = first.Author,
                Message = first.Message,
                ExternalVersion = first.ExternalVersion,
                CreatedTimeUtc = first.CreatedTimeUtc,
            };

        FlowArtifactRevision left =
            firstStore.Append(first);
        FlowArtifactRevision right =
            secondStore.Append(second);

        Assert.Equal(left.RevisionHash, right.RevisionHash);
    }

    [Fact]
    public async Task ConcurrentInitialCas_AllowsExactlyOneWriter()
    {
        var store = new InMemoryFlowArtifactStore();
        using var start = new ManualResetEventSlim(false);
        var outcomes = new ConcurrentBag<string>();

        Task[] tasks = Enumerable.Range(0, 8)
            .Select(index => Task.Run(() =>
            {
                start.Wait();
                try
                {
                    store.Append(CreateWriteRequest(
                        "flow:root",
                        authoring: $"authoring-{index}",
                        compiled: $"compiled-{index}"));
                    outcomes.Add("created");
                }
                catch (FlowArtifactHeadConflictException)
                {
                    outcomes.Add("conflict");
                }
            }))
            .ToArray();
        start.Set();
        await Task.WhenAll(tasks);

        Assert.Equal(1, outcomes.Count(item => item == "created"));
        Assert.Equal(7, outcomes.Count(item => item == "conflict"));
        Assert.Single(store.ListRevisions("flow:root"));
    }

    [Fact]
    public void DuplicateRoles_AreRejectedBeforeAnyWrite()
    {
        var store = new InMemoryFlowArtifactStore();
        var request = new FlowArtifactRevisionWriteRequest
        {
            FlowKey = "flow:root",
            Artifacts =
            [
                Content("same", "one"),
                Content("same", "two"),
            ],
        };

        Assert.Throws<ArgumentException>(
            () => store.Append(request));
        Assert.Null(store.GetReference("flow:root"));
    }

    [Fact]
    public void SchemaModels_ParticipateInExplicitMainDatabaseMigration()
    {
        Assert.Equal(5, FlowArtifactSchemaMigrator.ModelTypes.Count);
        Assert.Equal(5, FlowArtifactSchemaMigrator.TableNames.Count);
        Assert.Equal(
            FlowArtifactSchemaMigrator.TableNames.Count,
            FlowArtifactSchemaMigrator.TableNames
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(
            FlowArtifactSchemaMigrator.ModelTypes,
            type => Assert.True(
                typeof(IInitTables).IsAssignableFrom(type),
                type.FullName));
    }

    private static FlowArtifactRevisionWriteRequest
        CreateWriteRequest(
            string flowKey,
            FlowArtifactHeadCondition? expectedHead = null,
            string authoring = "authoring",
            string compiled = "compiled",
            bool publishImmediately = false)
    {
        return new FlowArtifactRevisionWriteRequest
        {
            FlowKey = flowKey,
            Artifacts =
            [
                Content(
                    FlowArtifactRoles.AuthoringCanvas,
                    authoring),
                Content(
                    FlowArtifactRoles.CompiledCanvas,
                    compiled),
            ],
            Dependencies =
            [
                new FlowArtifactDependency
                {
                    DependencyKey = "call-1",
                    FlowKey = "flow:child",
                    Revision = 7,
                    ContentHash = Hash("child-r7-content"),
                    DefinitionHash = Hash("child-r7-definition"),
                },
            ],
            ExpectedHead = expectedHead
                ?? FlowArtifactHeadCondition.Initial,
            PublishImmediately = publishImmediately,
            Source = "editor",
            Author = "tester",
            Message = "save",
            ExternalVersion = "v1",
            CreatedTimeUtc = new DateTime(
                2026,
                7,
                31,
                0,
                0,
                0,
                DateTimeKind.Utc),
        };
    }

    private static FlowArtifactContent Content(
        string role,
        string value)
    {
        return new FlowArtifactContent
        {
            Role = role,
            ContentType = "application/octet-stream",
            Content = Encoding.UTF8.GetBytes(value),
        };
    }

    private static string Hash(string value)
    {
        return FlowArtifactStoreRules.ComputeBlobHash(
            Encoding.UTF8.GetBytes(value));
    }
}
