using ColorVision.Algorithms;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Collections;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmCatalogProjectionTests
{
    [Fact]
    public void ParameterContractUsesSemanticJsonWhileFieldOrderRemainsPresentationSignificant()
    {
        AlgorithmDescriptor canonical = StandardAlgorithmCatalog.Create().Descriptors.Single(
            descriptor => descriptor.Id == StandardAlgorithmIds.Invert);
        AlgorithmParameterField first = new("first", "Object", Element("{\"x\":1,\"y\":2}"));
        AlgorithmParameterField firstReordered = first with { DefaultValue = Element("{\"y\":2,\"x\":1.0}") };
        AlgorithmParameterField second = new("second", "Array", Element("[1,2]"));
        AlgorithmDescriptor left = canonical with
        {
            ParameterSchema = new AlgorithmParameterSchema(
                1,
                [first, second],
                Element("{\"mode\":true,\"nested\":{\"a\":1,\"b\":[1,2]}}")),
        };
        AlgorithmDescriptor reorderedObjects = canonical with
        {
            ParameterSchema = new AlgorithmParameterSchema(
                1,
                [firstReordered, second],
                Element("{\"nested\":{\"b\":[1,2],\"a\":1.0},\"mode\":true}")),
        };
        AlgorithmDescriptor reorderedArray = reorderedObjects with
        {
            ParameterSchema = reorderedObjects.ParameterSchema with
            {
                Defaults = Element("{\"nested\":{\"b\":[2,1],\"a\":1},\"mode\":true}"),
            },
        };
        AlgorithmDescriptor changedValue = reorderedObjects with
        {
            ParameterSchema = reorderedObjects.ParameterSchema with
            {
                Defaults = Element("{\"nested\":{\"b\":[1,2],\"a\":2},\"mode\":true}"),
            },
        };
        AlgorithmDescriptor reorderedFields = reorderedObjects with
        {
            ParameterSchema = reorderedObjects.ParameterSchema with
            {
                Fields = [second, firstReordered],
            },
        };

        Assert.True(AlgorithmDescriptorContract.ParameterContractEquals(left, reorderedObjects));
        Assert.False(AlgorithmDescriptorContract.ParameterContractEquals(left, reorderedArray));
        Assert.False(AlgorithmDescriptorContract.ParameterContractEquals(left, changedValue));
        Assert.False(AlgorithmDescriptorContract.ParameterContractEquals(left, reorderedFields));

        static JsonElement Element(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    [Fact]
    public void StandardCatalogOwnsBatchAndInteractiveCompatibilityOrder()
    {
        AlgorithmCatalog catalog = StandardAlgorithmCatalog.Create();
        AlgorithmId[] expectedBatch =
        [
            StandardAlgorithmIds.Invert,
            StandardAlgorithmIds.PseudoColor,
            StandardAlgorithmIds.AutoLevels,
            StandardAlgorithmIds.WhiteBalance,
            StandardAlgorithmIds.BasicAdjustment,
            StandardAlgorithmIds.Threshold,
            StandardAlgorithmIds.Sharpen,
            StandardAlgorithmIds.GaussianBlur,
            StandardAlgorithmIds.MedianBlur,
            StandardAlgorithmIds.Canny,
            StandardAlgorithmIds.HistogramEqualization,
            StandardAlgorithmIds.Morphology,
            StandardAlgorithmIds.Denoise,
            StandardAlgorithmIds.GeometricTransform,
            StandardAlgorithmIds.LensDistortionCorrection,
            StandardAlgorithmIds.ImagingCorrection,
        ];
        string[] expectedInteractive =
        [
            "InvertImage", "AutoLevelsAdjust", "WhiteBalance", "BasicAdjustment", "Threshold", "RemoveMoire",
            "Sharpen", "GaussianBlur", "MedianBlur", "EdgeDetection", "HistogramEqualization",
            "Erode", "Dilate", "MorphologyEx", "BilateralFilter", "Blur", "GeometricTransform", "ImageRegistration", "LensDistortionCorrection", "ImagingCorrection", "FrequencySpectrum", "MoireAnalysis",
        ];

        Assert.Equal(expectedBatch, AlgorithmCatalogProjection.ForBatchImageProcessing(catalog).Select(item => item.Id));
        Assert.Equal(expectedBatch, BatchImageAlgorithms.CreateAll(catalog).Skip(1).Select(item => item.Descriptor!.Id));
        AlgorithmInteractiveCatalogEntry[] interactiveEntries = AlgorithmCatalogProjection.ForInteractiveMenu(catalog).ToArray();
        Assert.Equal(expectedInteractive, interactiveEntries.Select(item => item.Presentation.CompatibilityId));
        Assert.Equal(expectedInteractive.Length, interactiveEntries.Select(item => item.Presentation.CompatibilityId).Distinct().Count());
        Assert.All(interactiveEntries, item => Assert.True(catalog.TryResolveAlias(item.Presentation.CompatibilityId, out AlgorithmDescriptor? resolved)
            && resolved?.Id == item.Descriptor.Id, item.Presentation.CompatibilityId));
        AlgorithmInteractiveCatalogEntry[] filters = interactiveEntries
            .Where(item => item.Presentation.Group?.Id == "AlgorithmFilters")
            .ToArray();
        Assert.Equal(new[] { "GaussianBlur", "MedianBlur", "BilateralFilter", "Blur" },
            filters.Select(item => item.Presentation.CompatibilityId));
        Assert.All(filters, item => Assert.Equal("滤波", item.Presentation.Group!.DisplayName));
        Assert.Equal(expectedBatch.Length, AlgorithmCatalogProjection.ForBatchImageProcessing(catalog)
            .Select(item => item.Presentation!.BatchImageProcessingOrder).Distinct().Count());

        AlgorithmDescriptor canny = catalog.Descriptors.Single(item => item.Id == StandardAlgorithmIds.Canny);
        CannyParameters catalogDefaults = AlgorithmJson.Deserialize<CannyParameters>(canny.ParameterSchema.Defaults);
        CannyParameters batchDefaults = Assert.IsType<CannyParameters>(BatchImageAlgorithms.CreateAll(catalog)
            .Single(item => item.Descriptor?.Id == StandardAlgorithmIds.Canny).Options);
        Assert.Equal(catalogDefaults.LowThreshold, batchDefaults.LowThreshold);
        Assert.Equal(catalogDefaults.HighThreshold, batchDefaults.HighThreshold);
    }

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

    [Fact]
    public async Task ConcurrentInteractiveCompatibilityConflictPublishesOneDescriptorAndLeavesNoLoserResidue()
    {
        const AlgorithmHostCapabilities capabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local;
        AlgorithmDescriptor first = Descriptor(
            "test.interactive-race-first",
            capabilities,
            new AlgorithmPresentationMetadata(InteractiveEntries: [new AlgorithmInteractivePresentation("SharedInteractiveRace", 1)]));
        AlgorithmDescriptor second = Descriptor(
            "test.interactive-race-second",
            capabilities,
            new AlgorithmPresentationMetadata(InteractiveEntries: [new AlgorithmInteractivePresentation("SharedInteractiveRace", 2)]));
        await AssertIndependentConcurrentConflict(
            first,
            second,
            (catalog, descriptor) => catalog.Register(descriptor),
            catalog => Assert.Single(AlgorithmCatalogProjection.ForInteractiveMenu(catalog)));
    }

    [Fact]
    public async Task ConcurrentBatchOrderConflictPublishesOneDescriptorAndLeavesNoLoserResidue()
    {
        const AlgorithmHostCapabilities capabilities = AlgorithmHostCapabilities.Batch
            | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local;
        AlgorithmDescriptor first = Descriptor(
            "test.batch-race-first",
            capabilities,
            new AlgorithmPresentationMetadata(BatchImageProcessingOrder: 23));
        AlgorithmDescriptor second = Descriptor(
            "test.batch-race-second",
            capabilities,
            new AlgorithmPresentationMetadata(BatchImageProcessingOrder: 23));
        await AssertIndependentConcurrentConflict(
            first,
            second,
            (catalog, descriptor) => catalog.Register(descriptor),
            catalog => Assert.Single(AlgorithmCatalogProjection.ForBatchImageProcessing(catalog)));
    }

    [Fact]
    public void AlgorithmsContextMenuRendersTheCatalogProjectionIncludingGenericFallback()
    {
        AlgorithmInteractiveGroupPresentation group = new("TestFilterGroup", 40, "Test filters");
        AlgorithmCatalog catalog = new();
        catalog.Register(Descriptor(
            "test.context-menu-projection",
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            new AlgorithmPresentationMetadata(
                InteractiveEntries:
                [
                    new AlgorithmInteractivePresentation("TestContextMenuProjection", 42, "Projected algorithm") { Group = group },
                ])));

        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRuntime runtime = new(catalog, [new ProjectionProvider()], scheduler);
        AlgorithmRuntime undeclaredRuntime = new(catalog, [new UndeclaredProjectionProvider()], scheduler);
        ImageView view = WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView result = new(runtime);
            WriteableBitmap bitmap = new(1, 1, 96, 96, System.Windows.Media.PixelFormats.Gray8, null);
            bitmap.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 1 }, 1, 0);
            result.SetImageSource(bitmap, enableEditorImageServices: false, configureDefaultLayerController: false);
            return result;
        });
        ImageView undeclaredView = WpfTestHost.Invoke(() =>
        {
            ImageView result = new(undeclaredRuntime);
            WriteableBitmap bitmap = new(1, 1, 96, 96, System.Windows.Media.PixelFormats.Gray8, null);
            bitmap.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 1 }, 1, 0);
            result.SetImageSource(bitmap, enableEditorImageServices: false, configureDefaultLayerController: false);
            return result;
        });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                AlgorithmsContextMenu menu = new(view.EditorContext.ProcessingContext, runtime);
                List<ColorVision.UI.Menus.MenuItemMetadata> items = menu.GetContextMenuItems();
                ColorVision.UI.Menus.MenuItemMetadata category = Assert.Single(items, item => item.GuidId == group.Id);
                Assert.Equal("Algorithms", category.OwnerGuid);
                Assert.Equal(group.Order, category.Order);
                Assert.Equal(group.DisplayName, category.Header);
                Assert.Null(category.Command);
                ColorVision.UI.Menus.MenuItemMetadata projected = Assert.Single(items, item => item.GuidId == "TestContextMenuProjection");
                Assert.Equal(group.Id, projected.OwnerGuid);
                Assert.Equal(1, projected.Order);
                Assert.Equal("Projected algorithm", projected.Header);
                Assert.NotNull(projected.Command);

                AlgorithmsContextMenu undeclaredMenu = new(undeclaredView.EditorContext.ProcessingContext, undeclaredRuntime);
                Assert.DoesNotContain(
                    undeclaredMenu.GetContextMenuItems(),
                    item => item.GuidId == "TestContextMenuProjection");
            });
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
            WpfTestHost.Invoke(undeclaredView.Dispose);
        }
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

    private static void EnsureImageViewTestResources()
    {
        Application application = Application.Current ?? new Application();
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    private sealed class ProjectionProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        public AlgorithmProviderMetadata Metadata { get; } = new(
            "projection-provider",
            "Projection provider",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            1,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            bool supported = descriptor.Id.Value == "test.context-menu-projection";
            reason = supported ? null : "algorithm_not_supported";
            return supported;
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            reason = null;
            return true;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class UndeclaredProjectionProvider : IImageAlgorithmProvider
    {
        public AlgorithmProviderMetadata Metadata { get; } = new(
            "undeclared-projection-provider",
            "Undeclared projection provider",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            1,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            reason = null;
            return descriptor.Id.Value == "test.context-menu-projection";
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
            => throw new NotSupportedException();
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
