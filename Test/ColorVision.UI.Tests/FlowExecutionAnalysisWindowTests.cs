using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Diagnostics;

namespace ColorVision.UI.Tests;

public class FlowExecutionAnalysisWindowTests
{
    [Fact]
    public void ResolveBatchSerialNumberPrefersExecutedCode()
    {
        var batch = new MeasureBatchModel
        {
            Name = "panel-sn",
            Code = "panel-sn_20260728122431",
        };

        string serialNumber = FlowExecutionAnalysisWindow.ResolveBatchSerialNumber(batch);

        Assert.Equal("panel-sn_20260728122431", serialNumber);
    }

    [Fact]
    public void ResolveBatchSerialNumberFallsBackToNameAndNodeRecord()
    {
        var namedBatch = new MeasureBatchModel { Name = "same-name-and-code" };
        var unnamedBatch = new MeasureBatchModel();
        var record = new FlowNodeRecord { SerialNumber = "recorded-run" };

        Assert.Equal(
            "same-name-and-code",
            FlowExecutionAnalysisWindow.ResolveBatchSerialNumber(namedBatch, record));
        Assert.Equal(
            "recorded-run",
            FlowExecutionAnalysisWindow.ResolveBatchSerialNumber(unnamedBatch, record));
    }
}
