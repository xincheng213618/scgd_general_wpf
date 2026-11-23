using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Exports;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Blank;
using ProjectARVRPro.Process.OpticCenter;
using ProjectARVRPro.Services;
using System.IO;

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
    public void ConfigurationAndLegacyCommandsShareTheTextDispatcher()
    {
        const string directory = @"\\mes-nas\results\立景;{D}";
        var config = new ViewResultManagerConfig { CsvSavePath = @"C:\old", CustomXlsxSavePath = @"C:\reports" };
        int writes = 0;
        var dispatcher = new LuxSocketDispatcher(() => "H03", (_, request) => request.ResponsePrefix,
            request => LuxConfigurationProtocol.Handle(request, config, () => false, () => writes++));

        string set = JsonConvert.SerializeObject(new { method = "set", ResultSavePath = directory });
        Assert.Equal("{\"status\":1,\"reson\":\"\"}", dispatcher.Handle(null!, set));
        Assert.Equal("H0300,SN001,00;", dispatcher.Handle(null!, "T0000,SN001;"));
        string? get = dispatcher.Handle(null!, """
            {"method":"get","param":["ResultSavePath","camera_exposure","SaveImage"]}
            """);
        var response = JObject.Parse(Assert.IsType<string>(get));
        Assert.Equal(1, response.Value<int>("status"));
        Assert.Equal(directory, response.Value<string>("ResultSavePath"));
        Assert.Equal(0, response.Value<int>("camera_exposure"));
        Assert.Equal(string.Empty, response.Value<string>("SaveImage"));
        Assert.Equal(1, writes);
        var options = CustomTestResultExportOptions.Capture(config, useResultDirectory: true);
        Assert.Equal(directory, options.Directory);
        Assert.Equal(directory, options.CsvDirectory);
        Assert.Equal(@"C:\reports", CustomTestResultExportOptions.Capture(config).Directory);
    }

    [Theory]
    [InlineData("""{"method":"get","param":["ResultSavePath","unknown"]}""")]
    [InlineData("""{"method":"get","param":[]}""")]
    [InlineData("""{"method":"set","ResultSavePath":"relative"}""")]
    [InlineData("""{"method":"set","ResultSavePath":"C:\\invalid|"}""")]
    [InlineData("""{"method":"set","ResultSavePath":"C:\\result","SaveImage":"C:\\other"}""")]
    public void InvalidSettingsRequestsReportFailureWithoutChangingTheDirectory(string request)
    {
        var config = new ViewResultManagerConfig { CsvSavePath = @"C:\old" };
        var response = JObject.Parse(LuxConfigurationProtocol.Handle(request, config, () => false, () => Assert.Fail("Unexpected save")));
        Assert.Equal(0, response.Value<int>("status"));
        Assert.False(string.IsNullOrEmpty(response.Value<string>("reson")));
        Assert.Null(response["ResultSavePath"]);
        Assert.Equal(@"C:\old", config.CsvSavePath);
    }

    [Fact]
    public void DirectoryStaysUnchangedWhileBusyOrWhenSavingFails()
    {
        const string set = """{"method":"set","ResultSavePath":"C:\\result"}""";
        var config = new ViewResultManagerConfig { CsvSavePath = @"C:\old" };
        int writes = 0;
        void FailSave()
        {
            writes++;
            throw new IOException("Disk unavailable");
        }

        Assert.Equal(0, JObject.Parse(LuxConfigurationProtocol.Handle(set, config, () => true, FailSave)).Value<int>("status"));
        Assert.Equal(0, writes);
        Assert.Equal(0, JObject.Parse(LuxConfigurationProtocol.Handle(set, config, () => false, FailSave)).Value<int>("status"));
        Assert.Equal(1, writes);
        Assert.Equal(@"C:\old", config.CsvSavePath);
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
    public async Task FluxWaitsForRequiredOpenAndReturnsOnlyTheMeasurementResult(bool openLocal)
    {
        MsgRecord open = PendingCommand("Open");
        var measurementStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var measurement = new TaskCompletionSource<(bool Succeeded, SpectumResultEntity? Result)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commands = new List<string>();
        var pending = LuxDeviceMeasurements.RequestFluxMeasurementAsync(openLocal,
            () => { commands.Add("Open"); return open; },
            cancellation =>
            {
                commands.Add("GetData");
                measurementStarted.SetResult();
                return measurement.Task.WaitAsync(cancellation);
            }, CancellationToken.None);
        if (openLocal)
        {
            Assert.Equal(["Open"], commands);
            Assert.False(measurementStarted.Task.IsCompleted);
            Assert.False(pending.IsCompleted);
            CompleteCommand(open, MsgRecordState.Success);
        }
        await measurementStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(openLocal ? ["Open", "GetData"] : ["GetData"], commands);
        Assert.False(pending.IsCompleted);
        var captured = new SpectumResultEntity { LuminousFlux = 0 };
        measurement.SetResult((true, captured));
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(result.Succeeded);
        Assert.Same(captured, result.Result);
    }

    [Theory]
    [InlineData(MsgRecordState.Fail)]
    [InlineData(MsgRecordState.Timeout)]
    public async Task FluxDoesNotMeasureWhenLocalOpenFails(MsgRecordState failure)
    {
        MsgRecord open = PendingCommand("Open");
        var pending = LuxDeviceMeasurements.RequestFluxMeasurementAsync(true, () => open,
            _ => throw new InvalidOperationException("Measurement must not follow failed open."), CancellationToken.None);
        CompleteCommand(open, failure);
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
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
        var pending = LuxDeviceMeasurements.RequestFluxMeasurementAsync(true,
            () => { openCalls++; return open; },
            _ => { measurements++; return Task.FromResult<(bool, SpectumResultEntity?)>((false, null)); }, cancellation.Token);
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
