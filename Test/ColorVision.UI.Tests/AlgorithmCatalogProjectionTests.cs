using ColorVision.Algorithms;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Collections;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmCatalogProjectionTests
{

    [Fact]
    public void EligibleDescriptorAutomaticallyAppearsWithoutChangingEitherHostList()
    {
        AlgorithmCatalog catalog = new();
        AlgorithmDescriptor descriptor = Descriptor(
            "test.catalog-projection",
            AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Interactive,
            new AlgorithmPresentationMetadata(
                BatchImageProcessingOrder: 7,
                InteractiveEntries: [new AlgorithmInteractivePresentation("TestCatalogProjection", 9, "Test projection")]));
        catalog.Register(descriptor);

        AlgorithmDescriptor batchProjection = Assert.Single(AlgorithmCatalogProjection.ForBatchImageProcessing(catalog));
        Assert.NotSame(descriptor, batchProjection);
        Assert.True(AlgorithmDescriptorContract.Equals(descriptor, batchProjection));
        ArgumentException incompleteRuntime = Assert.Throws<ArgumentException>(() => BatchImageAlgorithms.CreateAll(catalog));
        Assert.Contains("AlgorithmRuntime", incompleteRuntime.Message, StringComparison.Ordinal);
        AlgorithmInteractiveCatalogEntry interactive = Assert.Single(AlgorithmCatalogProjection.ForInteractiveMenu(catalog));
        Assert.NotSame(descriptor, interactive.Descriptor);
        Assert.True(AlgorithmDescriptorContract.Equals(descriptor, interactive.Descriptor));
        Assert.Equal("TestCatalogProjection", interactive.Presentation.CompatibilityId);
    }

    [Fact]
    public void ProjectionRequiresTheCompleteHostCapabilitySet()
    {
        AlgorithmCatalog catalog = new();
        catalog.Register(Descriptor(
            "test.batch-missing-headless",
            AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Local,
            new AlgorithmPresentationMetadata(BatchImageProcessingOrder: 1)));
        catalog.Register(Descriptor(
            "test.interactive-missing-local",
            AlgorithmHostCapabilities.Interactive,
            new AlgorithmPresentationMetadata(InteractiveEntries: [new AlgorithmInteractivePresentation("MissingLocal", 1)])));

        Assert.Empty(AlgorithmCatalogProjection.ForBatchImageProcessing(catalog));
        Assert.Empty(AlgorithmCatalogProjection.ForInteractiveMenu(catalog));
    }

    [Fact]
    public void CatalogRejectsAmbiguousOrInvalidPresentationMetadata()
    {
        AlgorithmCatalog catalog = new();
        catalog.Register(Descriptor(
            "test.presentation-owner",
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            new AlgorithmPresentationMetadata(InteractiveEntries: [new AlgorithmInteractivePresentation("SharedMenuId", 1)])));

        Assert.Throws<InvalidOperationException>(() => catalog.Register(Descriptor(
            "test.presentation-conflict",
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            new AlgorithmPresentationMetadata(InteractiveEntries: [new AlgorithmInteractivePresentation("sharedmenuid", 2)]))));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AlgorithmCatalog().Register(Descriptor(
            "test.presentation-negative-order",
            AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            new AlgorithmPresentationMetadata(BatchImageProcessingOrder: -1))));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AlgorithmCatalog().Register(Descriptor(
            "test.presentation-negative-group-order",
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            new AlgorithmPresentationMetadata(InteractiveEntries:
            [
                new AlgorithmInteractivePresentation(
                    "GroupedEntry",
                    1)
                {
                    Group = new AlgorithmInteractiveGroupPresentation("InvalidGroup", -1),
                },
            ]))));
        Assert.Throws<InvalidOperationException>(() => new AlgorithmCatalog().Register(Descriptor(
            "test.presentation-group-id-conflict",
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            new AlgorithmPresentationMetadata(InteractiveEntries:
            [
                new AlgorithmInteractivePresentation(
                    "SameId",
                    1)
                {
                    Group = new AlgorithmInteractiveGroupPresentation("SameId", 1),
                },
            ]))));

        AlgorithmCatalog groupedCatalog = new();
        groupedCatalog.Register(Descriptor(
            "test.presentation-group-owner",
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            new AlgorithmPresentationMetadata(InteractiveEntries:
            [
                new AlgorithmInteractivePresentation(
                    "FirstGroupedEntry",
                    1)
                {
                    Group = new AlgorithmInteractiveGroupPresentation("SharedGroup", 1, "Shared"),
                },
            ])));
        Assert.Throws<InvalidOperationException>(() => groupedCatalog.Register(Descriptor(
            "test.presentation-group-conflict",
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            new AlgorithmPresentationMetadata(InteractiveEntries:
            [
                new AlgorithmInteractivePresentation(
                    "SecondGroupedEntry",
                    2)
                {
                    Group = new AlgorithmInteractiveGroupPresentation("SharedGroup", 2, "Changed"),
                },
            ]))));
    }

    [Fact]
    public void RegisterPrevalidatesAliasesAndPresentationWithoutLeavingPartialState()
    {
        AlgorithmCatalog catalog = new();
        AlgorithmDescriptor owner = Descriptor(
            "test.transaction-owner",
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Headless,
            new AlgorithmPresentationMetadata(
                BatchImageProcessingOrder: 1,
                InteractiveEntries: [new AlgorithmInteractivePresentation("TransactionShared", 1)]));
        catalog.Register(owner, "taken-alias");

        AlgorithmDescriptor aliasCandidate = Descriptor(
            "test.transaction-alias-candidate",
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Headless,
            new AlgorithmPresentationMetadata(
                BatchImageProcessingOrder: 2,
                InteractiveEntries: [new AlgorithmInteractivePresentation("TransactionAliasCandidate", 2)]));
        Assert.Throws<InvalidOperationException>(() => catalog.Register(aliasCandidate, "taken-alias"));
        Assert.False(catalog.TryResolve(aliasCandidate.Id, out _));
        Assert.False(catalog.TryResolveAlias(aliasCandidate.Id.Value, out _));

        catalog.Register(aliasCandidate, "retry-alias");
        Assert.True(catalog.TryResolveAlias("retry-alias", out AlgorithmDescriptor? retried));
        Assert.NotSame(aliasCandidate, retried);
        Assert.True(AlgorithmDescriptorContract.Equals(aliasCandidate, retried));

        AlgorithmDescriptor presentationCandidate = Descriptor(
            "test.transaction-presentation-candidate",
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            new AlgorithmPresentationMetadata(
                InteractiveEntries: [new AlgorithmInteractivePresentation("transactionshared", 3)]));
        Assert.Throws<InvalidOperationException>(() => catalog.Register(presentationCandidate));
        Assert.False(catalog.TryResolve(presentationCandidate.Id, out _));

        AlgorithmDescriptor presentationRetry = presentationCandidate with
        {
            Presentation = new AlgorithmPresentationMetadata(
                InteractiveEntries: [new AlgorithmInteractivePresentation("TransactionRetry", 3)]),
        };
        catalog.Register(presentationRetry);
        Assert.True(catalog.TryResolve(presentationRetry.Id, out _));

        AlgorithmDescriptor batchOrderCandidate = Descriptor(
            "test.transaction-batch-order",
            AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            new AlgorithmPresentationMetadata(BatchImageProcessingOrder: 1));
        Assert.Throws<InvalidOperationException>(() => catalog.Register(batchOrderCandidate));
        Assert.False(catalog.TryResolve(batchOrderCandidate.Id, out _));
        catalog.Register(batchOrderCandidate with
        {
            Presentation = new AlgorithmPresentationMetadata(BatchImageProcessingOrder: 3),
        });
        Assert.True(catalog.TryResolve(batchOrderCandidate.Id, out _));
    }

    [Fact]
    public void RegisterDeepFreezesEveryNestedDescriptorCollection()
    {
        List<string> allowed = ["first", "second"];
        List<AlgorithmParameterField> fields =
        [
            new AlgorithmParameterField("mode", "string", JsonSerializer.SerializeToElement("first"), AllowedValues: allowed),
        ];
        HashSet<AlgorithmImageFormat> supported = [AlgorithmImageFormat.Gray8];
        HashSet<AlgorithmImageFormat> outputs = [AlgorithmImageFormat.Gray8];
        List<AlgorithmInteractivePresentation> entries = [new("FrozenEntry", 1, "Frozen")];
        AlgorithmDescriptor source = new(
            new AlgorithmId("test.deep-freeze"),
            new AlgorithmVersion(1, 0, 0),
            "freeze",
            "test",
            "freeze",
            typeof(NoAlgorithmParameters),
            new AlgorithmParameterSchema(1, fields, AlgorithmJson.ToElement(new NoAlgorithmParameters())),
            supported,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            OutputFormats: outputs)
        {
            Presentation = new AlgorithmPresentationMetadata(InteractiveEntries: entries),
        };
        AlgorithmCatalog catalog = new();
        catalog.Register(source);

        allowed.Add("third");
        fields.Clear();
        supported.Add(AlgorithmImageFormat.Gray16);
        outputs.Clear();
        entries.Add(new AlgorithmInteractivePresentation("LateEntry", 2));

        Assert.True(catalog.TryResolve(source.Id, out AlgorithmDescriptor? resolved));
        Assert.NotSame(source, resolved);
        AlgorithmParameterField field = Assert.Single(resolved!.ParameterSchema.Fields);
        Assert.Equal(new[] { "first", "second" }, field.AllowedValues);
        Assert.Equal(new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 }, resolved.SupportedFormats);
        Assert.Equal(new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 }, resolved.OutputFormats);
        Assert.Equal("FrozenEntry", Assert.Single(resolved.Presentation!.InteractiveEntries!).CompatibilityId);
        Assert.Throws<NotSupportedException>(() => ((IList<AlgorithmParameterField>)resolved.ParameterSchema.Fields).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)field.AllowedValues!).Add("mutation"));
        Assert.Throws<NotSupportedException>(() => ((IList<AlgorithmInteractivePresentation>)resolved.Presentation.InteractiveEntries!).Clear());
    }

    [Fact]
    public async Task ConcurrentSourceMutationCannotPublishAPartialDescriptorAndStableRetrySucceeds()
    {
        using ManualResetEventSlim enumerationStarted = new();
        using ManualResetEventSlim continueEnumeration = new();
        List<AlgorithmParameterField> backing =
        [
            new AlgorithmParameterField("first", "number", JsonSerializer.SerializeToElement(1)),
            new AlgorithmParameterField("second", "number", JsonSerializer.SerializeToElement(2)),
        ];
        NonFailFastGatedReadOnlyList<AlgorithmParameterField> gated = new(backing, enumerationStarted, continueEnumeration);
        AlgorithmDescriptor descriptor = new(
            new AlgorithmId("test.concurrent-freeze"),
            new AlgorithmVersion(1, 0, 0),
            "concurrent",
            "test",
            "concurrent",
            typeof(NoAlgorithmParameters),
            new AlgorithmParameterSchema(1, gated, AlgorithmJson.ToElement(new NoAlgorithmParameters())),
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 },
            AlgorithmHostCapabilities.Local);
        AlgorithmCatalog catalog = new();

        Task registration = Task.Run(() => catalog.Register(descriptor));
        Assert.True(enumerationStarted.Wait(TimeSpan.FromSeconds(5)));
        backing.Add(new AlgorithmParameterField("third", "number", JsonSerializer.SerializeToElement(3)));
        continueEnumeration.Set();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await registration);
        Assert.False(catalog.TryResolve(descriptor.Id, out _));
        Assert.False(catalog.TryResolveAlias(descriptor.Id.Value, out _));

        AlgorithmDescriptor stable = descriptor with
        {
            ParameterSchema = descriptor.ParameterSchema with { Fields = backing.ToArray() },
        };
        catalog.Register(stable);
        Assert.True(catalog.TryResolve(stable.Id, out AlgorithmDescriptor? resolved));
        Assert.Equal(3, resolved!.ParameterSchema.Fields.Count);
    }

    [Fact]
    public async Task ConcurrentConflictingRegistrationsPublishExactlyOneCompleteDescriptor()
    {
        AlgorithmCatalog catalog = new();
        AlgorithmDescriptor first = Descriptor(
            "test.concurrent-first",
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local
                | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Headless,
            new AlgorithmPresentationMetadata(
                BatchImageProcessingOrder: 17,
                InteractiveEntries: [new AlgorithmInteractivePresentation("ConcurrentEntry", 4)]));
        AlgorithmDescriptor second = Descriptor(
            "test.concurrent-second",
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local
                | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Headless,
            new AlgorithmPresentationMetadata(
                BatchImageProcessingOrder: 17,
                InteractiveEntries: [new AlgorithmInteractivePresentation("ConcurrentEntry", 4)]));
        using Barrier start = new(3);
        Task<Exception?> registerFirst = Task.Run(() => Register(first));
        Task<Exception?> registerSecond = Task.Run(() => Register(second));
        Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(5)));
        Exception?[] outcomes = await Task.WhenAll(registerFirst, registerSecond);

        Assert.Single(outcomes, outcome => outcome == null);
        Assert.Single(outcomes, outcome => outcome is InvalidOperationException);
        AlgorithmDescriptor winner = Assert.Single(catalog.Descriptors);
        AlgorithmDescriptor loser = winner.Id == first.Id ? second : first;
        Assert.True(catalog.TryResolve(winner.Id, out AlgorithmDescriptor? resolved));
        Assert.True(AlgorithmDescriptorContract.Equals(winner, resolved!));
        Assert.False(catalog.TryResolve(loser.Id, out _));
        Assert.False(catalog.TryResolveAlias(loser.Id.Value, out _));
        Assert.True(catalog.TryResolveAlias("shared-concurrent", out AlgorithmDescriptor? alias));
        Assert.Equal(winner.Id, alias!.Id);
        Assert.Single(AlgorithmCatalogProjection.ForInteractiveMenu(catalog));
        Assert.Single(AlgorithmCatalogProjection.ForBatchImageProcessing(catalog));

        Exception? Register(AlgorithmDescriptor descriptor)
        {
            Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(5)));
            try
            {
                catalog.Register(descriptor, "shared-concurrent");
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }
    }

    [Fact]
    public async Task ConcurrentAliasConflictPublishesOneDescriptorAndLeavesNoLoserResidue()
    {
        AlgorithmDescriptor first = Descriptor(
            "test.alias-race-first",
            AlgorithmHostCapabilities.Local,
            new AlgorithmPresentationMetadata());
        AlgorithmDescriptor second = Descriptor(
            "test.alias-race-second",
            AlgorithmHostCapabilities.Local,
            new AlgorithmPresentationMetadata());
        await AssertIndependentConcurrentConflict(
            first,
            second,
            (catalog, descriptor) => catalog.Register(descriptor, "shared-alias-race"),
            catalog => Assert.True(catalog.TryResolveAlias("shared-alias-race", out _)));
    }

    private static AlgorithmDescriptor Descriptor(
        string id,
        AlgorithmHostCapabilities capabilities,
        AlgorithmPresentationMetadata presentation)
    {
        NoAlgorithmParameters defaults = new();
        return new AlgorithmDescriptor(
            new AlgorithmId(id),
            new AlgorithmVersion(1, 0, 0),
            id,
            "test",
            "test descriptor",
            typeof(NoAlgorithmParameters),
            new AlgorithmParameterSchema(1, Array.Empty<AlgorithmParameterField>(), AlgorithmJson.ToElement(defaults)),
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 },
            capabilities)
        {
            Presentation = presentation,
        };
    }

    private static async Task AssertIndependentConcurrentConflict(
        AlgorithmDescriptor first,
        AlgorithmDescriptor second,
        Action<AlgorithmCatalog, AlgorithmDescriptor> register,
        Action<AlgorithmCatalog> assertWinningProjection)
    {
        AlgorithmCatalog catalog = new();
        using Barrier start = new(3);
        Task<Exception?> a = Task.Run(() => Register(first));
        Task<Exception?> b = Task.Run(() => Register(second));
        Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(5)));
        Exception?[] outcomes = await Task.WhenAll(a, b);

        Assert.Single(outcomes, outcome => outcome == null);
        Assert.Single(outcomes, outcome => outcome is InvalidOperationException);
        AlgorithmDescriptor winner = Assert.Single(catalog.Descriptors);
        AlgorithmDescriptor loser = winner.Id == first.Id ? second : first;
        Assert.True(catalog.TryResolve(winner.Id, out AlgorithmDescriptor? resolved));
        Assert.True(AlgorithmDescriptorContract.Equals(winner, resolved!));
        Assert.False(catalog.TryResolve(loser.Id, out _));
        Assert.False(catalog.TryResolveAlias(loser.Id.Value, out _));
        assertWinningProjection(catalog);

        Exception? Register(AlgorithmDescriptor descriptor)
        {
            Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(5)));
            try
            {
                register(catalog, descriptor);
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }
    }

    private sealed class NonFailFastGatedReadOnlyList<T>(
        List<T> values,
        ManualResetEventSlim started,
        ManualResetEventSlim continuation) : IReadOnlyList<T>
    {
        public T this[int index] => values[index];
        public int Count => values.Count;

        public IEnumerator<T> GetEnumerator()
        {
            T[] snapshot = values.ToArray();
            if (!started.IsSet)
            {
                started.Set();
                continuation.Wait();
            }
            foreach (T value in snapshot) yield return value;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
