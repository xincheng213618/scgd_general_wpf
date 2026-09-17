using ColorVision.Algorithms;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.ImageEditor.Algorithms;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class LocalRgbCrossNodeTests
{
    [Fact]
    public void ReusesFindCrossStorageAndReloadsPoiOnEachExecution()
    {
        var services = new Services(); var node = new LocalRgbCrossNode(services) { TemplateName = "nine", SearchRegionPoiTemplate = "发光区", SearchRegion = new(900,900,40,40) };
        var action = new CVStartCFC("nine-test");
        action.SetCurrentFrame(LocalFlowFrame.Allocate(new LocalFrameMetadata { Width = 400, Height = 400, SourceBpp = 16, Channels = 3, PrimaryBufferKind = LocalFrameBufferKind.CvRaw }, 400*400*6, 0));
        try
        {
            node.ExecuteSynchronously(action); node.ExecuteSynchronously(action);
            Assert.Equal(2, services.Reads); Assert.Equal(2, services.Published);
            Assert.Equal(63, action.Data["MasterResultType"]);
            Assert.Equal(123, action.Data["MasterId"]);
            var saved = services.Request!;
            var master = LocalRgbCrossResultPersistence.CreateMaster(saved, 17);
            Assert.Equal(ViewResultAlgType.FindCross, master.ImgFileType); Assert.Equal("2.0", master.version);
            Assert.Equal(45, master.TId); Assert.Equal("nine", master.TName); Assert.Equal(17, master.BatchId);
            Assert.Equal("INVALID", master.Result); Assert.Equal(0, master.ResultCode);
            var detail = LocalFindCrossResultPersistence.CreateDetail(123, @"C:\Results\nine.json");
            Assert.Equal(123, detail.PId); Assert.Equal(@"C:\Results\nine.json", JsonConvert.DeserializeObject<ResultFile>(detail.ResultJson)!.ResultFileName);
            Assert.Equal("colorvision.rgb-cross-measurement", saved.Measurement.GetProperty("schemaId").GetString());
            Assert.Equal(9, saved.Measurement.GetProperty("points").GetArrayLength());
            Assert.DoesNotContain("limit", saved.Measurement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"rgbCross\":{\"unknown\":1}}")]
    [InlineData("{\"rgbCross\":{\"maximumEdgeSeparationPixels\":2}}")]
    [InlineData("{\"rgbCross\":{\"minimumContrast\":-1}}")]
    public void RejectsLegacyOrUnsafeNinePointTemplate(string json) => Assert.ThrowsAny<Exception>(() => LocalRgbCrossNodeServices.ParseParameters(json));

    [Fact]
    public void ExplicitNinePointSectionCoexistsWithLegacyParameters()
    {
        var p = LocalRgbCrossNodeServices.ParseParameters("{\"threshold\":80,\"rgbCross\":{\"targetThreshold\":0.6}}");
        Assert.Equal(.6,p.TargetThreshold); Assert.Null(p.MaximumEdgeSeparationPixels);
        var n = new LocalRgbCrossNode();n.Create();Assert.Equal("LocalRgbCross",n.NodeType);Assert.Single(n.GetAllInputOptions());Assert.Single(n.GetAllOutputOptions());
    }

    private sealed class Services : ILocalRgbCrossNodeServices
    {
        public int Reads, Published; public LocalRgbCrossSaveRequest? Request;
        public LocalRgbCrossTemplate LoadTemplate(string name) => new(45,name,new());
        public Int32Rect LoadSearchRegionTemplate(string name) => new(50 + Reads++,50,300,300);
        public LocalFlowFrame LoadFrame(string path) => throw new InvalidOperationException();
        public MeasureResultImgModel? GetImageResult(int id) => throw new InvalidOperationException();
        public AlgorithmResult Detect(LocalFlowFrameLease frame, AlgorithmInvocation invocation)
        {
            Assert.Equal(49+Reads, ((RectangleAlgorithmRoi)invocation.Roi!).X);
            return LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(frame, invocation).AsTask().GetAwaiter().GetResult();
        }
        public LocalRgbCrossSavedResult Save(LocalRgbCrossSaveRequest request) { Request=request;return new(123,@"C:\Results\nine.json"); }
        public void Publish(string serialNumber,string nodeId,int zIndex,int masterId) => Published++;
    }
}
