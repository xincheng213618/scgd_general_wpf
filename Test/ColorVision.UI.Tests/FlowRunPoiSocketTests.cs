using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.Services.Flow;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.SocketProtocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests;

public sealed class FlowRunPoiSocketTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(" ", null)]
    [InlineData("white", 42)]
    [InlineData(null, 0)]
    public async Task InvalidSelectorsNeverStartAFlow(string? name, int? id)
    {
        var service = new FlowPoiExecutionService(
            _ => throw new InvalidOperationException("Must not execute"),
            _ => throw new InvalidOperationException("Must not read"));

        FlowPoiExecutionResult result = await service.RunAsync(new() { FlowName = name, FlowId = id });

        Assert.Equal(400, result.Code);
    }

    [Fact]
    public void SelectionUsesTemplateIdentityAndRejectsAmbiguousNames()
    {
        StaTest.Run(() =>
        {
            TemplateModel<FlowParam>[] templates =
            [
                new("White", new FlowParam { Id = 42, Name = "White" }),
                new("White", new FlowParam { Id = -1000000001, Name = "White" })
            ];

            Assert.Same(templates[1], FlowPoiExecutionService.ResolveTemplate(templates, new() { FlowId = -1000000001 }));
            Assert.Equal(409, Assert.Throws<FlowPoiExecutionException>(() =>
                FlowPoiExecutionService.ResolveTemplate(templates, new() { FlowName = "white" })).Code);
            Assert.Equal(404, Assert.Throws<FlowPoiExecutionException>(() =>
                FlowPoiExecutionService.ResolveTemplate(templates, new() { FlowId = 1 })).Code);
            Assert.Same(templates[0], FlowPoiExecutionService.ResolveTemplate(templates.Take(1), new() { FlowName = " white " }));
        });
    }

    [Fact]
    public async Task PoiReadWaitsForFinalizationAndUsesOnlyReturnedBatch()
    {
        var finalized = new TaskCompletionSource<FlowPoiRun>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readBatches = new List<int>();
        var service = new FlowPoiExecutionService(_ => finalized.Task, batchId =>
        {
            readBatches.Add(batchId);
            return [Point];
        });

        Task<FlowPoiExecutionResult> pending = service.RunAsync(new() { FlowName = "White" });
        Assert.False(pending.IsCompleted);
        Assert.Empty(readBatches);
        finalized.SetResult(Run(FlowFinalOutcome.Succeeded));
        FlowPoiExecutionResult result = await pending;

        Assert.Equal(200, result.Code);
        Assert.Equal([137], readBatches);
        Assert.Equal("execution-137", result.Data!.ExecutionSerialNumber);
        Assert.Equal(137, result.Data.BatchId);
        Assert.Same(Point, Assert.Single(result.Data.Poi));
    }

    [Theory]
    [InlineData(FlowFinalOutcome.Failed, 500)]
    [InlineData(FlowFinalOutcome.Canceled, 499)]
    [InlineData(FlowFinalOutcome.TimedOut, 504)]
    public async Task UnsuccessfulRunsDoNotReturnPartialOrHistoricalPoi(FlowFinalOutcome outcome, int expectedCode)
    {
        var service = new FlowPoiExecutionService(_ => Task.FromResult(Run(outcome)),
            _ => throw new InvalidOperationException("Must not read"));

        FlowPoiExecutionResult result = await service.RunAsync(new() { FlowId = 42 });

        Assert.Equal(expectedCode, result.Code);
        Assert.Empty(result.Data!.Poi);
        Assert.Equal(outcome.ToString(), result.Data.Status);
    }

    [Fact]
    public async Task EmptyAndUnavailableResultsRemainFailuresWithExecutionIdentity()
    {
        var empty = new FlowPoiExecutionService(_ => Task.FromResult(Run(FlowFinalOutcome.Succeeded)), _ => []);
        var unavailable = new FlowPoiExecutionService(_ => Task.FromResult(Run(FlowFinalOutcome.Succeeded)),
            _ => throw new InvalidOperationException("Database unavailable"));

        FlowPoiExecutionResult noPoi = await empty.RunAsync(new() { FlowId = 42 });
        FlowPoiExecutionResult readFailed = await unavailable.RunAsync(new() { FlowId = 42 });

        Assert.Equal(422, noPoi.Code);
        Assert.Equal(500, readFailed.Code);
        Assert.Contains("Database unavailable", readFailed.Message);
        Assert.Equal(137, readFailed.Data!.BatchId);
    }

    [Theory]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(503)]
    public async Task RejectedExecutionPreservesFailureCodeWithoutQueryingOldBatch(int code)
    {
        var service = new FlowPoiExecutionService(_ => throw new FlowPoiExecutionException(code, "Rejected"),
            _ => throw new InvalidOperationException("Must not read"));

        FlowPoiExecutionResult result = await service.RunAsync(new() { FlowId = 42 });

        Assert.Equal(code, result.Code);
        Assert.Null(result.Data);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"FlowIndex\":1}")]
    public void MalformedSocketParamsNeverInvokeExecution(string parameters)
    {
        var handler = new FlowRunPoiSocketHandler(_ => throw new InvalidOperationException("Must not execute"));

        SocketResponse result = handler.Handle(null!, Request(parameters));

        Assert.Equal(400, result.Code);
        Assert.Equal("poi-1", result.MsgID);
        Assert.Equal("external-sn", result.SerialNumber);
    }

    [Fact]
    public void SocketRoundTripPreservesCorrelationAndDistinctXyzFields()
    {
        var service = new FlowPoiExecutionService(_ => Task.FromResult(Run(FlowFinalOutcome.SucceededWithWarnings)), _ => [Point]);
        var handler = new FlowRunPoiSocketHandler(service.RunAsync);
        var request = JsonConvert.DeserializeObject<SocketRequest>(
            "{\"Version\":\"1.0\",\"EventName\":\"FlowRunPoi\",\"MsgID\":\"poi-1\",\"SerialNumber\":\"external-sn\",\"Params\":\"{\\\"FlowId\\\":42}\"}")!;

        SocketResponse response = handler.Handle(null!, request);
        JObject json = JObject.Parse(JsonConvert.SerializeObject(response));

        Assert.Equal(200, response.Code);
        Assert.Equal("FlowRunPoi", response.EventName);
        Assert.Equal(request.MsgID, response.MsgID);
        Assert.Equal(request.Version, response.Version);
        Assert.Equal(request.SerialNumber, response.SerialNumber);
        Assert.Equal(10, (double)json["Data"]!["Poi"]![0]!["X"]!);
        Assert.Equal(0.3, (double)json["Data"]!["Poi"]![0]!["x"]!);
        Assert.Equal("center", (string)json["Data"]!["Poi"]![0]!["Name"]!);
    }

    [Fact]
    public void PersistedPointKeepsAlgorithmIdentityGeometryAndColorValues()
    {
        FlowPoiPoint point = FlowPoiExecutionService.CreatePoint(
            new AlgResultMasterModel { Id = 301, TName = "White POI" },
            new PoiPointResultModel
            {
                Id = 302, Pid = 301, PoiId = 7, PoiName = "center", PoiX = 100, PoiY = 200, PoiWidth = 3, PoiHeight = 4,
                Value = "{\"X\":10,\"Y\":20,\"Z\":30,\"x\":0.3,\"y\":0.4,\"u\":0.2,\"v\":0.5,\"CCT\":6500,\"Wave\":550}"
            });

        Assert.Equal(301, point.AlgorithmResultId);
        Assert.Equal("White POI", point.AlgorithmName);
        Assert.Equal(7, point.PoiId);
        Assert.Equal("center", point.Name);
        Assert.Equal((100, 200, 3, 4), (point.PixelX, point.PixelY, point.Width, point.Height));
        Assert.Equal((10d, 20d, 30d), (point.X, point.Y, point.Z));
        // Legacy POI JSON fields deserialize as float before the public double projection.
        Assert.Equal(((double)0.3f, (double)0.4f, (double)0.2f, 0.5, 6500d, 550d),
            (point.x, point.y, point.u, point.v, point.CCT, point.Wave));
    }

    [Fact]
    public void MissingPointValuesCannotBecomeSuccessfulZeroMeasurements()
    {
        Assert.Throws<InvalidOperationException>(() => FlowPoiExecutionService.CreatePoint(
            new AlgResultMasterModel { Id = 301, TName = "White POI" },
            new PoiPointResultModel { Id = 302, Pid = 301, PoiId = 7, PoiName = "center", Value = null }));
    }

    private static SocketRequest Request(string parameters) => new()
    {
        Version = "1.0", MsgID = "poi-1", EventName = "FlowRunPoi", SerialNumber = "external-sn", Params = parameters
    };

    private static FlowPoiRun Run(FlowFinalOutcome outcome) => new(42, "White", 137,
        new FlowRunFinalizedData(
            new FlowControlData { SerialNumber = "execution-137", TotalTime = 150, Message = "Done" },
            outcome, [], DateTime.UtcNow));

    private static readonly FlowPoiPoint Point = new(301, "White POI", 7, "center", 100, 200, 3, 3,
        10, 20, 30, 0.3, 0.4, 0.2, 0.5, 6500, 550);
}
