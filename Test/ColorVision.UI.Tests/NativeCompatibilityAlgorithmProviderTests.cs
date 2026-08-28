using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class NativeCompatibilityAlgorithmProviderTests
{
    [Fact]
    public void RemoveMoireIsRejectedWhenTheRequiredLibraryOrExportIsUnavailable()
    {
        NativeCompatibilityAlgorithmProvider provider = new(() =>
            NativeAlgorithmAvailability.Unavailable("probe sentinel"));
        AlgorithmDescriptor descriptor = RemoveMoireDescriptor();
        using AlgorithmImageBuffer image = Gray8();

        bool canExecute = provider.CanExecute(descriptor, [Input(image)], out string? reason);

        Assert.False(canExecute);
        Assert.Contains("native_dependency_unavailable", reason, StringComparison.Ordinal);
        Assert.Contains("probe sentinel", reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunnerReturnsStructuredProviderUnavailableAndReleasesTransferredInput()
    {
        AlgorithmCatalog catalog = StandardAlgorithmCatalog.Create();
        NativeCompatibilityAlgorithmProvider provider = new(() =>
            NativeAlgorithmAvailability.Unavailable("missing test export"));
        using AlgorithmExecutionScheduler scheduler = new(nativeConcurrency: 1);
        AlgorithmRunner runner = new(catalog, [provider], scheduler);
        AlgorithmImageBuffer image = Gray8();

        using AlgorithmResult result = await runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.RemoveMoire, new NoAlgorithmParameters()),
            Inputs = [Input(image, AlgorithmInputOwnership.Transferred)],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

        AlgorithmFailure failure = Assert.Single(result.Failures);
        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Equal("provider_unavailable", failure.Code);
        Assert.Contains("missing test export", failure.Details!["provider_dependency_unavailable"], StringComparison.Ordinal);
        Assert.True(image.IsDisposed);
    }

    [Fact]
    public void AvailabilityProbeRunsOnlyForTheImplementedAlgorithmShape()
    {
        int calls = 0;
        NativeCompatibilityAlgorithmProvider provider = new(() =>
        {
            calls++;
            return NativeAlgorithmAvailability.Available;
        });
        using AlgorithmImageBuffer image = Gray8();

        Assert.False(provider.CanExecute(
            StandardAlgorithmCatalog.Create().Descriptors.Single(item => item.Id == StandardAlgorithmIds.Invert),
            [Input(image)],
            out string? unsupportedReason));
        Assert.Equal("algorithm_not_implemented", unsupportedReason);
        Assert.Equal(0, calls);

        Assert.True(provider.CanExecute(RemoveMoireDescriptor(), [Input(image)], out string? reason));
        Assert.Null(reason);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void DefaultProbeAcceptsThePackagedRuntimeWhenItsRealExportIsPresent()
    {
        string packagedLibrary = Path.Combine(
            AppContext.BaseDirectory,
            "runtimes",
            "win-x64",
            "native",
            "opencv_helper.dll");
        if (!File.Exists(packagedLibrary)) return;

        NativeCompatibilityAlgorithmProvider provider = new();
        using AlgorithmImageBuffer image = Gray8();

        Assert.True(provider.CanExecute(RemoveMoireDescriptor(), [Input(image)], out string? reason), reason);
    }

    private static AlgorithmDescriptor RemoveMoireDescriptor() =>
        StandardAlgorithmCatalog.Create().Descriptors.Single(item => item.Id == StandardAlgorithmIds.RemoveMoire);

    private static AlgorithmImageBuffer Gray8() => new(2, 2, 2, AlgorithmImageFormat.Gray8, [1, 2, 3, 4]);

    private static AlgorithmInput Input(
        AlgorithmImageBuffer image,
        AlgorithmInputOwnership ownership = AlgorithmInputOwnership.Borrowed) => new()
    {
        Name = "source",
        Image = image,
        Ownership = ownership,
    };
}
