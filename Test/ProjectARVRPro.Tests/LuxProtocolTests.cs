using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Blank;
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
        var replies = new List<string>();
        new LuxCommandSession(request, replies.Add).CompleteFlow(null, null, "Completed", null);
        Assert.Equal(expected, Assert.Single(replies));
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
        Assert.Equal("H03AR02,SN,00;FlowFailed:Failed,detail;", new LuxRequest("H03AR", "02", "SN").FailureResponse("Failed", "detail"));
    }

    [Theory]
    [InlineData(1, 2, 3, "1,2,3")]
    [InlineData(1, 0, -3, "1,0,-3")]
    public void OpticCenterReplyUsesThisFlowsRotationXTiltYTiltRegardlessOfStepIndex(double rotation, double xTilt, double yTilt, string expectedValues)
    {
        var process = new OpticCenterLuxProcess();
        ProjectARVRReuslt result = OpticCenterFlow(process, rotation, xTilt, yTilt);
        result.TestType = 7;
        result.Result = false; // Recipe FAIL still returns the actual measurements.
        Assert.Equal($"H03AR02,SN,{expectedValues},00;", CompleteFlow(new("H03AR", "02", "SN"), process, result));
        Assert.Equal($"H0302,SN,{expectedValues},00;", CompleteFlow(new("H03", "02", "SN"), process, result));
        Assert.Equal($"H03AR21,SN,{expectedValues},00;", CompleteFlow(new("H03AR", "21", "SN"), process, result));
        Assert.Equal($"H0321,SN,{expectedValues},00;", CompleteFlow(new("H03", "21", "SN"), process, result));
    }

    [Theory]
    [InlineData("missing-result")]
    [InlineData("different-sn")]
    [InlineData("no-batch")]
    [InlineData("failed-flow")]
    [InlineData("wrong-process")]
    [InlineData("empty-snapshot")]
    [InlineData("invalid-json")]
    [InlineData("missing-axis")]
    [InlineData("image-center-only")]
    [InlineData("nan")]
    [InlineData("infinity")]
    public void IncompleteOpticCenterReplyDoesNotReuseValuesFromTheSameProcessAndSn(string invalidCase)
    {
        var request = new LuxRequest("H03", "21", "SN");
        var process = new OpticCenterLuxProcess();
        Assert.Equal("H0321,SN,9,8,7,00;", CompleteFlow(request, process, OpticCenterFlow(process, 9, 8, 7)));

        ProjectARVRReuslt? current = OpticCenterFlow(process, 1, 2, 3);
        switch (invalidCase)
        {
            case "missing-result": current = null; break;
            case "different-sn": current.SN = "OTHER"; break;
            case "no-batch": current.BatchId = 0; break;
            case "failed-flow": current.FlowStatus = FlowStatus.Failed; break;
            case "wrong-process": current.ProcessTypeFullName = typeof(OpticCenterProcess).FullName; break;
            case "empty-snapshot": current.ViewResultJson = string.Empty; break;
            case "invalid-json": current.ViewResultJson = "{"; break;
            case "missing-axis":
            case "image-center-only":
                var center = JObject.Parse(current.ViewResultJson!);
                if (invalidCase == "missing-axis") center.Remove("OptCenterYTilt");
                else
                {
                    center["ImageCenterRotation"] = center["OptCenterRotation"];
                    center["ImageCenterXTilt"] = center["OptCenterXTilt"];
                    center["ImageCenterYTilt"] = center["OptCenterYTilt"];
                    center.Remove("OptCenterRotation");
                    center.Remove("OptCenterXTilt");
                    center.Remove("OptCenterYTilt");
                }
                current.ViewResultJson = center.ToString();
                break;
            case "nan": current = OpticCenterFlow(process, double.NaN, 2, 3); break;
            case "infinity": current = OpticCenterFlow(process, 1, double.PositiveInfinity, 3); break;
        }

        Assert.Equal("H0321,SN", CompleteFlow(request, process, current));
    }

    [Theory]
    [InlineData("H03AR", "02")]
    [InlineData("H03", "21")]
    public void SelectedOpticCenterProcessAlsoControlsTheFailurePrefix(string machineNo, string socketCode)
    {
        var process = new OpticCenterLuxProcess();
        Assert.Equal($"{machineNo}{socketCode},SNFlowFailed:Failed,detail;",
            CompleteFlow(new(machineNo, socketCode, "SN"), process, OpticCenterFlow(process, 1, 2, 3), "Failed", "detail"));
    }

    [Fact]
    public void SpecialReplyIsOptInAndFailureDoesNotCallTheProcessProvider()
    {
        var request = new LuxRequest("H03AR", "02", "SN");
        var regular = new OpticCenterProcess();
        Assert.Equal(request.ResponsePrefix, CompleteFlow(request, regular, OpticCenterFlow(regular, 9, 8, 7)));

        var process = new ProbeResponseProcess();
        ProjectARVRReuslt result = OpticCenterFlow(process, 1, 2, 3);
        Assert.Equal(request.ResponsePrefix, CompleteFlow(request, process, result));
        process.CreateResponse = context => new($"custom:{context.SerialNumber}:{context.Result!.BatchId}");
        Assert.Equal("custom:SN:12", CompleteFlow(request, process, result));

        process.CreateResponse = _ => throw new InvalidOperationException("provider failure");
        Assert.Equal(request.ResponsePrefix, CompleteFlow(request, process, result));
        process.CreateResponse = _ => { Assert.Fail("Failure replies must bypass the provider"); return null; };
        Assert.Equal("H03AR02,SN,00;FlowFailed:Failed,detail;", CompleteFlow(request, process, result, "Failed", "detail"));
    }

    [Fact]
    public void LuxOpticCenterRestoresItsSubclassAndIndependentInheritedRecipe()
    {
        var process = new OpticCenterLuxProcess();
        process.Config.RecipeConfig.OptCenterXTilt.Fix = 2;
        process.Config.Key = "Center_B";
        var result = OpticCenterFlow(process, 1, 2, 3);
        ResultProcessResolver.Capture(result, process);
        var restored = Assert.IsType<OpticCenterLuxProcess>(ResultProcessResolver.Resolve(result, [process], []));
        Assert.NotSame(process.Config.RecipeConfig, restored.Config.RecipeConfig);
        Assert.Equal(2, restored.Config.RecipeConfig.OptCenterXTilt.Fix);
        Assert.Equal("Center_B", restored.Config.Key);
        Assert.Equal(3, restored.GetObjectiveCsvRows(result).Count);
        Assert.All(restored.GetObjectiveCsvRows(result), row => Assert.Equal("Center_B", row.TestScreen));
        Assert.Equal("H0321,SN,1,2,3,00;", CompleteFlow(new("H03", "21", "SN"), restored, result));

        ProcessTypeOption option = Assert.Single(ProcessTypeCatalog.CreateOptions([restored]));
        Assert.Equal("光学中心（回包）", option.DisplayName);
        Assert.Equal(ProcessTypeCatalog.GeometrySubcategory, option.Subcategory);
        Assert.IsAssignableFrom<ILuxFlowResponseProvider>(restored);
    }

    private static ProjectARVRReuslt OpticCenterFlow(IProcess process, double rotation, double xTilt, double yTilt) => new()
    {
        BatchId = 12,
        SN = "SN",
        FlowStatus = FlowStatus.Completed,
        ProcessTypeFullName = process.GetType().FullName,
        ViewResultJson = JsonConvert.SerializeObject(new OpticCenterTestResult
        {
            OptCenterRotation = new() { Name = "OptCenterRotation", Value = rotation },
            OptCenterXTilt = new() { Name = "OptCenterXTilt", Value = xTilt },
            OptCenterYTilt = new() { Name = "OptCenterYTilt", Value = yTilt }
        })
    };

    private static string CompleteFlow(LuxRequest request, IProcess? process, ProjectARVRReuslt? result, string eventName = "Completed", string? parameters = null)
    {
        var replies = new List<string>();
        var session = new LuxCommandSession(request, replies.Add);
        session.CompleteFlow(process, result, eventName, parameters);
        session.Complete("duplicate");
        return Assert.Single(replies);
    }

    private sealed class ProbeResponseProcess : BlankProcess, ILuxFlowResponseProvider
    {
        internal Func<LuxFlowResponseContext, LuxFlowResponse?> CreateResponse { get; set; } = _ => null;
        public LuxFlowResponse? BuildCompletedResponse(LuxFlowResponseContext context) => CreateResponse(context);
    }

    [Fact]
    public void AsyncCompletionKeepsItsRequestPrefixAndRepliesOnce()
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
        running!.CompleteFlow(null, null, "Completed", null);
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

    // LUX must wait for a confirmed local connection before issuing the measurement or replying.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FluxWaitsForRequiredOpenAndReturnsOnlyTheMeasurementReply(bool openLocal)
    {
        MsgRecord open = PendingCommand("Open"), measure = PendingCommand("GetData");
        var measured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commands = new List<string>();
        Task<MsgReturn?> pending = LuxDeviceMeasurements.RequestFluxMeasurementAsync(openLocal,
            () => { commands.Add("Open"); return open; },
            () => { commands.Add("GetData"); measured.SetResult(); return measure; }, CancellationToken.None);
        if (openLocal)
        {
            Assert.Equal(["Open"], commands);
            Assert.False(measured.Task.IsCompleted);
            Assert.False(pending.IsCompleted);
            CompleteCommand(open, MsgRecordState.Success);
        }
        await measured.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(openLocal ? ["Open", "GetData"] : ["GetData"], commands);
        Assert.False(pending.IsCompleted);
        CompleteCommand(measure, MsgRecordState.Success);
        Assert.Same(measure.MsgReturn, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData(MsgRecordState.Fail)]
    [InlineData(MsgRecordState.Timeout)]
    public async Task FluxDoesNotMeasureWhenLocalOpenFails(MsgRecordState failure)
    {
        MsgRecord open = PendingCommand("Open");
        Task<MsgReturn?> pending = LuxDeviceMeasurements.RequestFluxMeasurementAsync(true, () => open,
            () => throw new InvalidOperationException("Measurement must not follow failed open."), CancellationToken.None);
        CompleteCommand(open, failure);
        Assert.Null(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledFluxDoesNotStartMeasurementAfterALateOpenReply(bool canceledBeforeOpen)
    {
        MsgRecord open = PendingCommand("Open");
        using var cancellation = new CancellationTokenSource();
        int openCalls = 0, measurements = 0;
        if (canceledBeforeOpen) cancellation.Cancel();
        Task<MsgReturn?> pending = LuxDeviceMeasurements.RequestFluxMeasurementAsync(true,
            () => { openCalls++; return open; },
            () => { measurements++; return PendingCommand("GetData"); }, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        CompleteCommand(open, MsgRecordState.Success);
        Assert.Equal(canceledBeforeOpen ? 0 : 1, openCalls);
        Assert.Equal(0, measurements);
    }

    private static MsgRecord PendingCommand(string eventName)
    {
        MsgRecord record = null!;
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() => record = new MsgRecord
            { MsgSend = new MsgSend { EventName = eventName }, MsgRecordState = MsgRecordState.Sended });
        return record;
    }

    private static void CompleteCommand(MsgRecord record, MsgRecordState state) =>
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
        {
            record.MsgReturn = new MsgReturn { EventName = record.MsgSend.EventName, Code = state == MsgRecordState.Success ? 0 : -1 };
            record.MsgRecordState = state;
        });
}
