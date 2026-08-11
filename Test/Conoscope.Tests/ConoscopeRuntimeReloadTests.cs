using ColorVision.UI;
using Conoscope.Core;
using OpenCvSharp;
using System.IO;

namespace Conoscope.Tests;

public sealed class ConoscopeRuntimeReloadTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), $"Conoscope-Reload-{Guid.NewGuid():N}");

    [Fact]
    public void ReloadSwitchesMatricesAndSavePathsWhileRunningSnapshotKeepsA()
    {
        Directory.CreateDirectory(tempRoot);
        ConoscopeConfig configA = CreateConfig("A", 1, 2);
        ConoscopeConfig configB = CreateConfig("B", 10, 20);
        var savedConfigs = new List<ConoscopeConfig>();
        var notifier = new TestConfigReloadNotifier();
        ConoscopeConfig current = configA;

        using var manager = new ConoscopeManager(
            () => current,
            notifier,
            config => new ConoscopeGlobalReferenceStore(config, savedConfigs.Add));
        using ConoscopeRuntimeSnapshot runningA = manager.CaptureRuntimeSnapshot();

        Assert.Equal(1, runningA.GlobalReferences.ColorDifferenceReferenceUMat!.At<float>(0, 0));
        Assert.Equal(2, runningA.GlobalReferences.ColorDifferenceReferenceVMat!.At<float>(0, 0));

        current = configB;
        notifier.RaiseConfigsReloaded();

        Assert.Same(configB, manager.Config);
        Assert.Equal(10, manager.GlobalReferences.ColorDifferenceReferenceUMat!.At<float>(0, 0));
        Assert.Equal(20, manager.GlobalReferences.ColorDifferenceReferenceVMat!.At<float>(0, 0));
        Assert.Equal(1, runningA.GlobalReferences.ColorDifferenceReferenceUMat!.At<float>(0, 0));

        using Mat saveA = new(1, 1, MatType.CV_32FC1, Scalar.All(31));
        runningA.GlobalReferences.SaveContrastReference(ContrastReferenceKind.Black, saveA, "saved-a");

        using (ConoscopeRuntimeSnapshot nextB = manager.CaptureRuntimeSnapshot())
        using (Mat saveB = new(1, 1, MatType.CV_32FC1, Scalar.All(41)))
        {
            nextB.GlobalReferences.SaveContrastReference(ContrastReferenceKind.Black, saveB, "saved-b");
        }

        Assert.Equal([configA, configB], savedConfigs);
        Assert.Equal(31, ReadValue(configA.ContrastBlackReferenceYMatPath));
        Assert.Equal(41, ReadValue(configB.ContrastBlackReferenceYMatPath));
        Assert.Equal("saved-a", configA.ContrastBlackReferenceDisplayName);
        Assert.Equal("saved-b", configB.ContrastBlackReferenceDisplayName);
    }

    private ConoscopeConfig CreateConfig(string name, float u, float v)
    {
        string directory = Path.Combine(tempRoot, name);
        Directory.CreateDirectory(directory);
        string uPath = Path.Combine(directory, "u.bin");
        string vPath = Path.Combine(directory, "v.bin");
        using Mat uMat = new(1, 1, MatType.CV_32FC1, Scalar.All(u));
        using Mat vMat = new(1, 1, MatType.CV_32FC1, Scalar.All(v));
        ConoscopeReferenceMatSerializer.Save(uPath, uMat);
        ConoscopeReferenceMatSerializer.Save(vPath, vMat);

        return new ConoscopeConfig
        {
            ColorDifferenceReferenceUMatPath = uPath,
            ColorDifferenceReferenceVMatPath = vPath,
            ContrastBlackReferenceYMatPath = Path.Combine(directory, "black.bin")
        };
    }

    private static float ReadValue(string filePath)
    {
        using Mat mat = ConoscopeReferenceMatSerializer.Load(filePath);
        return mat.At<float>(0, 0);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
            Directory.Delete(tempRoot, recursive: true);
    }

    private sealed class TestConfigReloadNotifier : IConfigReloadNotifier
    {
        public event EventHandler? ConfigsReloaded;

        public void RaiseConfigsReloaded() => ConfigsReloaded?.Invoke(this, EventArgs.Empty);
    }
}
