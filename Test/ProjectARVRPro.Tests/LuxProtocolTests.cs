using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process.OpticCenter;
using ProjectARVRPro.Services;

namespace ProjectARVRPro.Tests;

public sealed class LuxProtocolTests
{
    // These golden replies protect the existing PLC contract, including its unusual semicolon placement.
    [Theory]
    [InlineData("T0000,SN001;", "H0300,SN001,00;")]
    [InlineData("T0021,SN001;", "H0321,SN001,00;")]
    [InlineData("  T0021,SN,002;  ", "H0321,SN,002,00;")]
    public void LegacyCommandsKeepTheirReplyPrefix(string wire, string expected)
    {
        LuxRequest request = Assert.IsType<LuxRequest>(LuxRequest.Parse(wire, "H03", out string? error));
        Assert.Null(error);
        Assert.Equal(expected, request.CompletedResponse(null));
    }

    [Theory]
    [InlineData("T0000,;", "No SN")]
    [InlineData("T0000", "No SN")]
    [InlineData("X0000,SN;", "No right Code X0000")]
    public void InvalidRequestsKeepLegacyErrorsAndDoNotStartWork(string wire, string expected)
    {
        var dispatcher = new LuxSocketDispatcher(() => "H03", (_, _) => throw new Exception("Invalid command dispatched"));
        Assert.Equal(expected, dispatcher.Handle(null!, wire));
    }

    [Fact]
    public void DeviceAndFailureRepliesKeepTheOriginalFraming()
    {
        var vid = new LuxRequest("H03", "01", "SN");
        var flux = new LuxRequest("H03", "31", "SN");
        Assert.Equal("H0301,SN,00;123", vid.MeasurementResponse(123));
        Assert.Equal("H0331,SN,00;0", flux.MeasurementResponse(0));
        Assert.Equal("H0331,SN,00;125.42", flux.MeasurementResponse(125.42f));
        Assert.Equal("H0331,SN,00;", flux.MeasurementResponse(null));
        Assert.Equal("H0321,SN,00;FlowFailed:OverTime,Timeout;", new LuxRequest("H03", "21", "SN").FailureResponse("OverTime", "Timeout"));
        Assert.Equal("H03AR02,SNFlowFailed:Failed,detail;", new LuxRequest("H03AR", "02", "SN").FailureResponse("Failed", "detail"));
    }

    [Fact]
    public void OpticCenterReplyUsesRotationXTiltYTiltRegardlessOfStepIndex()
    {
        var center = new OpticCenterTestResult
        {
            OptCenterRotation = new() { Value = 1 }, OptCenterXTilt = new() { Value = 2 }, OptCenterYTilt = new() { Value = 3 }
        };
        Assert.Equal("H03AR02,SN,1,2,3,00;", new LuxRequest("H03AR", "02", "SN").CompletedResponse(center));
        Assert.Equal("H0302,SN,00;", new LuxRequest("H03", "02", "SN").CompletedResponse(center));
        Assert.Equal("H03AR21,SN,00;", new LuxRequest("H03AR", "21", "SN").CompletedResponse(center));
    }

    [Fact]
    public void AsyncCompletionKeepsItsConnectionAndMachinePrefixAndRepliesOnce()
    {
        var first = new List<string>();
        var second = new List<string>();
        string machineNo = "H03";
        LuxCommandSession? running = null;
        var dispatcher = new LuxSocketDispatcher(() => machineNo, (_, command) =>
        {
            if (command.SocketCode == "00") return command.ResponsePrefix;
            running = new LuxCommandSession(command, first.Add);
            return null;
        });
        Assert.Null(dispatcher.Handle(null!, "T0021,FIRST;"));
        machineNo = "H03AR";
        second.Add(dispatcher.Handle(null!, "T0000,SECOND;")!);
        running!.Complete(running.Request.CompletedResponse(null));
        running.Complete("duplicate");
        Assert.Equal("H0321,FIRST,00;", Assert.Single(first));
        Assert.Equal("H03AR00,SECOND,00;", Assert.Single(second));
    }

    [Fact]
    public void BothServiceAndLocalVidResultsUseTheConvertedDistance()
    {
        Assert.Equal(123, LuxDeviceMeasurements.ReadVid(new MsgReturn { EventName = "AutoFocus", Data = new { Position = 999, VidPosition = 123 } }));
        Assert.Equal(456, LuxDeviceMeasurements.ReadVid(new MsgReturn { EventName = "GetPosition", Data = JObject.Parse("{nPosition:999,VidPos:456}") }));
        Assert.Null(LuxDeviceMeasurements.ReadVid(new MsgReturn { EventName = "AutoFocus", Data = new { Position = 999 } }));
        var local = new SpectumResultEntity { Id = 0, LuminousFlux = 789 };
        Assert.Equal(789, LuxDeviceMeasurements.ReadLocalSpectrumModel(JObject.FromObject(new { Model = local }))!.LuminousFlux);
    }

    [Fact]
    public async Task DeviceCompletionHandlesAlreadyFinishedPendingAndCanceledRecords()
    {
        MsgRecord? record = null;
        var response = new MsgReturn { EventName = "AutoFocus", Data = new { VidPosition = 12 } };
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() => record = new MsgRecord { MsgReturn = response, MsgRecordState = MsgRecordState.Success });
        Assert.Same(response, await LuxDeviceMeasurements.WaitForResultAsync(record!, CancellationToken.None));

        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() => record!.MsgRecordState = MsgRecordState.Sended);
        Task<MsgReturn?> pending = LuxDeviceMeasurements.WaitForResultAsync(record!, CancellationToken.None);
        Assert.False(pending.IsCompleted);
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() => record!.MsgRecordState = MsgRecordState.Success);
        Assert.Same(response, await pending.WaitAsync(TimeSpan.FromSeconds(5)));

        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() => record!.MsgRecordState = MsgRecordState.Sended);
        using var cancellation = new CancellationTokenSource();
        Task<MsgReturn?> canceled = LuxDeviceMeasurements.WaitForResultAsync(record!, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() => record!.MsgRecordState = MsgRecordState.Fail);
        Assert.Null(await LuxDeviceMeasurements.WaitForResultAsync(record!, CancellationToken.None));
    }
}
