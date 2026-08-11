using ColorVision.UI;
using cvColorVision;
using Spectrum.Calibration;

namespace Spectrum.Tests;

public class SpectrometerFeatureExtensionTests
{
    [Fact]
    public void AssemblyHandlerDiscoversAmplitudeCalibrationProvider()
    {
        AssemblyHandler assemblyHandler = AssemblyHandler.GetInstance();
        assemblyHandler.RegisterAssembly(typeof(SpectrumAmplitudeCalibrationFeatureProvider).Assembly);
        assemblyHandler.ClearCaches();

        try
        {
            ISpectrometerFeatureProvider provider = Assert.Single(
                assemblyHandler.LoadImplementations<ISpectrometerFeatureProvider>(),
                candidate => candidate is SpectrumAmplitudeCalibrationFeatureProvider);

            Assert.Equal("spectrum.amplitude-calibration", provider.Metadata.Id);
            Assert.True(provider.Metadata.RequiresExclusiveDeviceAccess);
            Assert.False(provider.Metadata.ShowCompletionMessage);
        }
        finally
        {
            assemblyHandler.ClearCaches();
        }
    }
}
