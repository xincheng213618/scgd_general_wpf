using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Templates.Flow.Versioning;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FlowSubflowDefinitionStoreTests
{
    [Fact]
    public void JsonStoreIsAppendOnlyIdempotentAndOrderIndependent()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonFlowSubflowDefinitionStore(directory.Path);
        FlowSubflowCall first = CreateCall("first", "child:a");
        FlowSubflowCall second = CreateCall("second", "child:b");

        StoredFlowSubflowDefinition created = store.Append(
            "flow:parent",
            3,
            new FlowSubflowSidecar([second, first]));
        StoredFlowSubflowDefinition repeated = store.Append(
            "flow:parent",
            3,
            new FlowSubflowSidecar([first, second]));

        Assert.Equal(created.SidecarHash, repeated.SidecarHash);
        Assert.Equal(
            ["first", "second"],
            repeated.Sidecar.Calls.Select(call => call.CallId));
        Assert.Single(
            Directory.GetFiles(
                directory.Path,
                "subflow.json",
                SearchOption.AllDirectories));

        FlowSubflowDefinitionConflictException conflict =
            Assert.Throws<FlowSubflowDefinitionConflictException>(() =>
                store.Append(
                    "flow:parent",
                    3,
                    new FlowSubflowSidecar(
                        [CreateCall("changed", "child:c")])));
        Assert.Equal("flow:parent", conflict.FlowKey);
        Assert.Equal(3, conflict.Revision);
        Assert.Equal(created.SidecarHash, conflict.ExistingHash);
        Assert.NotEqual(
            conflict.ExistingHash,
            conflict.IncomingHash);
    }

    [Fact]
    public void ResolverUsesPinnedRevisionAndHashWithMatchingSidecar()
    {
        using var fixture = new ResolverFixture();
        FlowRevision first = fixture.AppendRevision([1, 2, 3]);
        FlowRevision second = fixture.AppendRevision([4, 5, 6]);
        FlowSubflowSidecar firstSidecar = new(
            [CreateCall("pinned", "child:pinned")]);
        fixture.Sidecars.Append(
            first.FlowKey,
            first.Revision,
            firstSidecar);

        ResolvedFlowDefinition resolved =
            Assert.IsType<ResolvedFlowDefinition>(
                fixture.Resolver.Resolve(
                    new FlowDefinitionReference(
                        first.FlowKey,
                        first.Revision.ToString(),
                        $"sha256:{first.BinaryHash}")));

        Assert.Equal("1", resolved.Revision);
        Assert.Equal(first.BinaryHash, resolved.ContentHash);
        Assert.Equal(first.FullSnapshot, resolved.CanvasData);
        Assert.Equal(
            "pinned",
            Assert.Single(resolved.Sidecar!.Calls).CallId);
        Assert.NotEqual(second.FullSnapshot, resolved.CanvasData);
    }

    [Fact]
    public void ResolverCanPinByContentHashOnly()
    {
        using var fixture = new ResolverFixture();
        FlowRevision first = fixture.AppendRevision([10]);
        fixture.AppendRevision([20]);

        ResolvedFlowDefinition resolved =
            Assert.IsType<ResolvedFlowDefinition>(
                fixture.Resolver.Resolve(
                    new FlowDefinitionReference(
                        first.FlowKey,
                        ContentHash: first.BinaryHash)));

        Assert.Equal(first.Revision.ToString(), resolved.Revision);
        Assert.Equal(first.BinaryHash, resolved.ContentHash);
        Assert.Equal(first.FullSnapshot, resolved.CanvasData);
    }

    [Fact]
    public void ResolverUsesHeadAndReturnsItsConcreteIdentity()
    {
        using var fixture = new ResolverFixture();
        fixture.AppendRevision([1]);
        FlowRevision head = fixture.AppendRevision([2]);
        fixture.Sidecars.Append(
            head.FlowKey,
            head.Revision,
            new FlowSubflowSidecar(
                [CreateCall("head", "child:head")]));

        ResolvedFlowDefinition resolved =
            Assert.IsType<ResolvedFlowDefinition>(
                fixture.Resolver.Resolve(
                    new FlowDefinitionReference(head.FlowKey)));

        Assert.Equal(head.Revision.ToString(), resolved.Revision);
        Assert.Equal(head.BinaryHash, resolved.ContentHash);
        Assert.Equal(head.FullSnapshot, resolved.CanvasData);
        Assert.Equal(
            "head",
            Assert.Single(resolved.Sidecar!.Calls).CallId);
    }

    [Fact]
    public void ResolverTreatsMissingSidecarAsEmpty()
    {
        using var fixture = new ResolverFixture();
        FlowRevision revision = fixture.AppendRevision([7, 8]);

        ResolvedFlowDefinition resolved =
            Assert.IsType<ResolvedFlowDefinition>(
                fixture.Resolver.Resolve(
                    new FlowDefinitionReference(
                        revision.FlowKey,
                        revision.Revision.ToString())));

        Assert.NotNull(resolved.Sidecar);
        Assert.Empty(resolved.Sidecar!.Calls);
    }

    [Fact]
    public void ResolverRejectsRevisionAndHashMismatch()
    {
        using var fixture = new ResolverFixture();
        FlowRevision first = fixture.AppendRevision([1]);
        FlowRevision second = fixture.AppendRevision([2]);

        FlowSubflowResolutionException exception =
            Assert.Throws<FlowSubflowResolutionException>(() =>
                fixture.Resolver.Resolve(
                    new FlowDefinitionReference(
                        first.FlowKey,
                        first.Revision.ToString(),
                        second.BinaryHash)));

        Assert.Equal(
            FlowSubflowResolutionError.RevisionHashMismatch,
            exception.Error);
    }

    private static FlowSubflowCall CreateCall(
        string callId,
        string childFlowKey)
    {
        byte suffix = checked((byte)callId.Length);
        return new FlowSubflowCall(
            callId,
            new FlowPortReference(
                new Guid(
                    suffix,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    1),
                0),
            new FlowPortReference(
                new Guid(
                    suffix,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    2),
                0),
            new FlowDefinitionReference(childFlowKey));
    }

    private sealed class ResolverFixture : IDisposable
    {
        private readonly TemporaryDirectory directory = new();
        private readonly FlowRevisionService revisionService;
        private FlowRevision? head;

        public ResolverFixture()
        {
            var revisions = new InMemoryFlowRevisionStore();
            revisionService = new FlowRevisionService(revisions);
            Sidecars = new JsonFlowSubflowDefinitionStore(
                directory.Path);
            Resolver = new FlowRevisionSubflowResolver(
                revisions,
                Sidecars);
        }

        public JsonFlowSubflowDefinitionStore Sidecars { get; }

        public FlowRevisionSubflowResolver Resolver { get; }

        public FlowRevision AppendRevision(byte[] snapshot)
        {
            FlowRevision revision = revisionService.CreateRevision(
                new FlowRevisionCreateRequest
                {
                    FlowKey = "flow:resolver",
                    FullSnapshot = snapshot,
                    SemanticDocument = CreateDocument(),
                    Source = FlowRevisionSource.Editor,
                    Condition = head == null
                        ? FlowRevisionWriteCondition.Initial
                        : FlowRevisionWriteCondition.FromHead(head),
                });
            head = revision;
            return revision;
        }

        public void Dispose()
        {
            directory.Dispose();
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ColorVision.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }

    private static FlowSemanticDocument CreateDocument()
    {
        return new FlowSemanticDocument
        {
            Nodes =
            [
                new FlowSemanticNode
                {
                    NodeId = "node",
                    TypeKey = "test.node",
                },
            ],
        };
    }
}
