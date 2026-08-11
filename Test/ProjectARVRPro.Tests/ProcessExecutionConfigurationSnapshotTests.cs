using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.UI;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Chessboard;
using System.IO;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class ProcessExecutionConfigurationSnapshotTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), $"ProjectARVRPro-ConfigSnapshot-{Guid.NewGuid():N}");

    [Fact]
    public void ProcessExporterUsesOuterTaskSnapshotAfterReloadToB()
    {
        var configA = new ViewResultManagerConfig { CsvSavePath = Path.Combine(tempRoot, "A"), SaveByDate = false };
        var configB = new ViewResultManagerConfig { CsvSavePath = Path.Combine(tempRoot, "B"), SaveByDate = false };
        ViewResultManagerConfig current = configA;
        using var owner = new RuntimeConfigOwner<ViewResultManagerConfig>(
            () => current,
            snapshotFactory: Clone);
        ViewResultManagerConfig runningTask = owner.Capture();

        current = configB;
        Assert.True(owner.Reload());

        var context = new IProcessExecutionContext
        {
            Result = new ProjectARVRReuslt { SN = "SN-outer" },
            ResultConfig = runningTask,
        };
        ChessboardCsvExporter.SavePoixyuvDatas(
            Array.Empty<PoiResultCIExyuvData>(),
            context,
            "snapshot",
            calculation: null,
            reportedContrast: null,
            contrastResultName: string.Empty,
            contrastSource: string.Empty);

        Assert.Single(Directory.GetFiles(Path.Combine(configA.CsvSavePath, "SN-outer"), "snapshot_*.csv"));
        Assert.False(Directory.Exists(configB.CsvSavePath));
    }

    private static ViewResultManagerConfig Clone(ViewResultManagerConfig config)
    {
        return new ViewResultManagerConfig
        {
            CsvSavePath = config.CsvSavePath,
            SaveByDate = config.SaveByDate,
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
            Directory.Delete(tempRoot, recursive: true);
    }
}
