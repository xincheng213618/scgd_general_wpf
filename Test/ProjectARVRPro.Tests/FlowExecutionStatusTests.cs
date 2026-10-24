using ColorVision.Themes;
using ColorVision.UI.Controls;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ProjectARVRPro.Tests;

[CollectionDefinition(nameof(FlowExecutionStatusTestGroup), DisableParallelization = true)]
public sealed class FlowExecutionStatusTestGroup { }

[Collection(nameof(FlowExecutionStatusTestGroup))]
public sealed class FlowExecutionStatusTests
{
    [Fact]
    public void LongFailureKeepsFullDiagnosticTextWhenSummaryIsFlattened()
    {
        const string reason = "十字图像文件不存在。\r\n请检查目录：C:\\images\\cross\\sample.tif";
        var status = FlowExecutionStatusInfo.Finished("flow2", "Failed", reason, null);
        Assert.DoesNotContain('\n', status.Message);
        Assert.Contains(reason, status.Details);
        Assert.Contains("flow2", status.Details);
        Assert.Empty(status.ElapsedText); // Preparation failures must not show the previous run's time.
    }

    [Fact]
    public void CompletedFlowDoesNotPresentItsPayloadAsAnErrorOrClaimInspectionPass()
    {
        var status = FlowExecutionStatusInfo.Finished("flow2", "Completed", "{\"result\":false}", 1234);
        Assert.Equal("流程执行完成", status.Message);
        Assert.Equal("1234 ms", status.ElapsedText);
        Assert.Contains("{\"result\":false}", status.Details);
        Assert.Equal(FlowExecutionStatusKind.Completed, status.Kind);
    }

    [Fact]
    public void ElapsedTimeKeepsTotalMillisecondsAndDoesNotProduceNegativeRemainingEstimate()
    {
        var status = FlowExecutionStatusInfo.Running("flow2", "L/BV相机", 3_665_800, 60000);
        Assert.Equal("3665800 ms", status.ElapsedText);
        Assert.DoesNotContain("预计剩余", status.Details);
        Assert.Contains("上次执行", status.Details);
    }

    [Fact]
    public void AuxiliaryMessagesKeepTheirRawContentWithoutChangingTheFlowOutcome()
    {
        const string reply = "N: returned\r\nraw MES response";
        var completed = FlowExecutionStatusInfo.Finished("KB", "Completed", null, 1200);
        var withReply = completed.WithAdditionalMessage("MES 返回", reply);
        Assert.Equal(completed.Kind, withReply.Kind);
        Assert.Equal(completed.ElapsedText, withReply.ElapsedText);
        Assert.Contains(reply, withReply.Details);
        Assert.DoesNotContain('\n', withReply.Message);
        var notice = FlowExecutionStatusInfo.Notice("服务重启失败\n请重试", isError: true);
        Assert.Empty(notice.ElapsedText);
        Assert.Equal("服务重启失败\n请重试", notice.Details);
    }
}
