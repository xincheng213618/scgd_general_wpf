using ColorVision.Algorithms;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.Jsons;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using System.Text;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class LocalRgbCrossNodeTests
{
    [Fact]
    public void ReusesFindCrossStorageAndReloadsPoiOnEachExecution()
    {
        var services = new Services { ExpectedThreshold = 0.6 }; var node = new LocalRgbCrossNode(services) { ParameterJson = "{\"TargetThreshold\":0.6}", SearchRegionPoiTemplate = "发光区", SearchRegion = new(900,900,40,40) };
        var action = new CVStartCFC("nine-test");
        var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata { Width = 400, Height = 400, SourceBpp = 16, Channels = 3, PrimaryBufferKind = LocalFrameBufferKind.CvRaw }, 400*400*6, 0);
        using (var lease = frame.Acquire()) System.Runtime.InteropServices.Marshal.Copy(new byte[lease.RawLength], 0, lease.RawPointer, lease.RawLength);
        action.SetCurrentFrame(frame);
        try
        {
            node.ExecuteSynchronously(action); node.ExecuteSynchronously(action);
            Assert.Equal(2, services.Reads); Assert.Equal(2, services.Published);
            Assert.Equal(63, action.Data["MasterResultType"]);
            Assert.Equal(123, action.Data["MasterId"]);
            var saved = services.Request!;
            var master = LocalRgbCrossResultPersistence.CreateMaster(saved, 17);
            Assert.Equal(ViewResultAlgType.FindCross, master.ImgFileType); Assert.Equal("2.0", master.version);
            Assert.Null(master.TId); Assert.Equal("LocalRgbCross", master.TName); Assert.Equal(17, master.BatchId);
            Assert.Equal("INVALID", master.Result); Assert.Equal(0, master.ResultCode);
            var detail = LocalFindCrossResultPersistence.CreateDetail(123, @"C:\Results\nine.json");
            Assert.Equal(123, detail.PId); Assert.Equal(@"C:\Results\nine.json", JsonConvert.DeserializeObject<ResultFile>(detail.ResultJson)!.ResultFileName);
            Assert.Equal("colorvision.rgb-cross-measurement", saved.Measurement.GetProperty("schemaId").GetString());
            Assert.Equal(9, saved.Measurement.GetProperty("points").GetArrayLength());
            Assert.DoesNotContain("limit", saved.Measurement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void LegacyTemplateSelectionIsIgnoredAndRemainingSettingsRoundTrip()
    {
        var node = new LocalRgbCrossNode { SearchRegionPoiTemplate = "发光区", SearchRegion = new(10,20,300,300),
            ParameterJson = "{\"MinimumArmCoverage\":0.75}", ImageFilePath = @"C:\images\input.cvraw", ResultDirectory = @"C:\results\FindCross" };
        node.Create();
        node.Title = "九点十字 RGB 分离";
        var state = ParseState(node.GetSaveData());
        Assert.False(state.ContainsKey("TemplateName"));
        state["TemplateName"] = Encoding.UTF8.GetBytes("已删除的旧模板");
        var restored = new LocalRgbCrossNode(); restored.Create(); restored.OnLoadNode(state);
        Assert.Equal(node.SearchRegionPoiTemplate, restored.SearchRegionPoiTemplate);
        Assert.Equal(node.SearchRegion, restored.SearchRegion);
        Assert.Equal(node.ImageFilePath, restored.ImageFilePath);
        Assert.Equal(node.ResultDirectory, restored.ResultDirectory);
        Assert.Equal(node.ParameterJson, restored.ParameterJson);
        state.Remove("ParameterJson");
        var legacy = new LocalRgbCrossNode(); legacy.Create(); legacy.OnLoadNode(state);
        Assert.Equal("{}", legacy.ParameterJson);
        Assert.False(ParseState(restored.GetSaveData()).ContainsKey("TemplateName"));
        Assert.Equal("LocalRgbCross", restored.NodeType);
        Assert.Equal("十字 RGB 分离", restored.Title);
        Assert.Single(restored.GetAllInputOptions()); Assert.Single(restored.GetAllOutputOptions());
    }

    private sealed class Services : ILocalRgbCrossNodeServices
    {
        public double ExpectedThreshold = 0.5;
        public int Reads, Published; public LocalRgbCrossSaveRequest? Request;
        public string ResolveResultDirectory(string configuredDirectory) => @"C:\Results";
        public Int32Rect LoadSearchRegionTemplate(string name) => new(50 + Reads++,50,300,300);
        public LocalFlowFrame LoadFrame(string path) => throw new InvalidOperationException();
        public MeasureResultImgModel? GetImageResult(int id) => throw new InvalidOperationException();
        public AlgorithmResult Detect(LocalFlowFrameLease frame, AlgorithmInvocation invocation)
        {
            Assert.Equal(49+Reads, ((RectangleAlgorithmRoi)invocation.Roi!).X);
            Assert.Equal(ExpectedThreshold, invocation.Parameters.GetProperty("targetThreshold").GetDouble());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, invocation.Parameters.GetProperty("maximumEdgeSeparationPixels").ValueKind);
            return LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(frame, invocation).AsTask().GetAwaiter().GetResult();
        }
        public LocalRgbCrossSavedResult Save(LocalRgbCrossSaveRequest request) { Request=request;return new(123,@"C:\Results\nine.json"); }
        public void Publish(string serialNumber,string nodeId,int zIndex,int masterId) => Published++;
    }
    private static Dictionary<string, byte[]> ParseState(byte[] data)
    {
        int position = 0;
        position += data[position] + 1;
        position += data[position] + 1;
        Dictionary<string, byte[]> state = new();
        while (position < data.Length)
        {
            int keyLength = BitConverter.ToInt32(data, position);
            position += sizeof(int);
            string key = Encoding.UTF8.GetString(data, position, keyLength);
            position += keyLength;
            int valueLength = BitConverter.ToInt32(data, position);
            position += sizeof(int);
            byte[] value = new byte[valueLength];
            Array.Copy(data, position, value, 0, valueLength);
            position += valueLength;
            state.Add(key, value);
        }
        return state;
    }
}
