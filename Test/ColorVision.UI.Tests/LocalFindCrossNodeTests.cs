using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.Jsons;
using FlowEngineLib.Base;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class LocalFindCrossNodeTests
{
    [Fact]
    public void NodeDefaultsExposeOnlyProductGeometryAndOpticsAndUseFullImage()
    {
        LocalFindCrossNode node = new();
        node.Create();

        Assert.Equal("LocalFindCross", node.NodeType);
        Assert.Equal("本地十字定位", node.Title);
        Assert.Equal(["IN"], node.GetAllInputOptions().Select(option => option.Text));
        Assert.Equal(["OUT"], node.GetAllOutputOptions().Select(option => option.Text));
        Assert.Equal(string.Empty, node.ImageFilePath);
        Assert.Equal(Int32Rect.Empty, node.SearchRegion);
        Assert.Equal(string.Empty, node.ResultDirectory);
        JObject parameters = JObject.Parse(node.ParameterJson);
        Assert.Equal(
            ["ExpectedAngleDegrees", "AngleToleranceDegrees", "opticsParams"],
            parameters.Properties().Select(property => property.Name));
        Assert.Equal(0, parameters.Value<double>("ExpectedAngleDegrees"));
        Assert.Equal(10, parameters.Value<double>("AngleToleranceDegrees"));
        Assert.Equal(25.4, parameters["opticsParams"]!.Value<double>("focusLength"), 8);
        Assert.Equal(3.76, parameters["opticsParams"]!.Value<double>("sensorPixSize"), 8);
        Assert.Null(parameters["opticsParams"]?["stdCenter"]);

        string[] hiddenKeys =
        [
            "DetectionMode", "PatternPolarity", "RotationMethod", "MinPatternContrast",
            "MinArmLengthPixels", "MinArmCoverage", "MinConfidence", "MaxProcessingSize",
            "Name", "CalibrationOffset", "distortion", "threshold", "CheckLine",
            "erodeAndDiate", "singleErodeKernel", "binaryRateInContours"
        ];
        Assert.All(hiddenKeys, key => Assert.Null(parameters[key]));
    }

    [Fact]
    public void ProductionNodeContractRejectsInternalAlgorithmStrategyKeys()
    {
        Assert.True(LocalFindCrossNodeServices.TryParseProductionOptions(
            LocalFindCrossNode.DefaultParameterJson,
            out FindCrossLocalOptions options,
            out string validError), validError);
        Assert.Equal(0, options.ExpectedAngleDegrees);
        Assert.Equal(10, options.AngleToleranceDegrees);

        Assert.False(LocalFindCrossNodeServices.TryParseProductionOptions(
            """{"DetectionMode":"OuterPanel"}""",
            out _,
            out string modeError));
        Assert.Contains("DetectionMode", modeError, StringComparison.Ordinal);

        Assert.False(LocalFindCrossNodeServices.TryParseProductionOptions(
            """{"MinConfidence":0.2}""",
            out _,
            out string thresholdError));
        Assert.Contains("MinConfidence", thresholdError, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationRoundTripsThroughNodeState()
    {
        LocalFindCrossNode original = new();
        original.Create();
        original.ImageFilePath = @"C:\images\cross.cvraw";
        original.ParameterJson = "{\"ExpectedAngleDegrees\":1.5,\"AngleToleranceDegrees\":8}";
        original.SearchRegion = new Int32Rect(2888, 1920, 3751, 2655);
        original.ResultDirectory = @"D:\results\cross";
        Dictionary<string, byte[]> state = ParseState(original.GetSaveData());

        LocalFindCrossNode restored = new();
        restored.Create();
        restored.OnLoadNode(state);

        Assert.Equal(original.ImageFilePath, restored.ImageFilePath);
        Assert.Equal(original.ParameterJson, restored.ParameterJson);
        Assert.Equal(original.SearchRegion, restored.SearchRegion);
        Assert.Equal(original.ResultDirectory, restored.ResultDirectory);
    }

    [Fact]
    public void MemoryFrameExecutionDetectsPersistsPublishesAndOutputsType63()
    {
        CVStartCFC action = CreateRawAction("local-find-cross");
        LocalFindCrossPersistenceRequest? persisted = null;
        LocalFindCrossPublishRequest? published = null;
        FakeNodeServices services = new()
        {
            DetectHandler = (image, roi, parameterJson) =>
            {
                Assert.Equal(64, image.cols);
                Assert.Equal(48, image.rows);
                Assert.Equal(1, image.channels);
                Assert.Equal(8, image.depth);
                Assert.NotEqual(IntPtr.Zero, image.pData);
                Assert.Equal(5, roi.X);
                Assert.Equal(6, roi.Y);
                Assert.Equal(40, roi.Width);
                Assert.Equal(30, roi.Height);
                Assert.Equal(1.25, JObject.Parse(parameterJson).Value<double>("ExpectedAngleDegrees"), 8);
                Assert.Equal(8, JObject.Parse(parameterJson).Value<double>("AngleToleranceDegrees"), 8);
                return CreateSuccessfulDetection();
            },
            PersistHandler = request =>
            {
                persisted = request;
                return new LocalFindCrossPersistenceResult
                {
                    MasterId = 73,
                    ResultFilePath = @"C:\results\cross-73.json"
                };
            },
            PublishHandler = request => published = request
        };
        LocalFindCrossNode node = new(services)
        {
            ParameterJson = "{\"ExpectedAngleDegrees\":1.25,\"AngleToleranceDegrees\":8}",
            SearchRegion = new Int32Rect(5, 6, 40, 30),
            ResultDirectory = @"C:\results"
        };
        try
        {
            LocalFindCrossNodeResultData result = node.ExecuteSynchronously(action);

            Assert.Equal(1, services.DetectCount);
            Assert.Equal(1, services.PersistCount);
            Assert.Equal(1, services.PublishCount);
            Assert.NotNull(persisted);
            Assert.Same(action, persisted!.Action);
            Assert.Equal(@"C:\results", persisted.ResultDirectory);
            Assert.Equal("Point_1", persisted.Result.Name);
            Assert.Equal(31, persisted.Result.CenterX);
            Assert.NotNull(published);
            Assert.Equal(73, published!.MasterId);
            Assert.Equal("FindCross", published.OperatorCode);
            Assert.Equal(action.SerialNumber, published.SerialNumber);
            Assert.Equal(73, Convert.ToInt32(action.Data["MasterId"]));
            Assert.Equal(63, Convert.ToInt32(action.Data["MasterResultType"]));
            Assert.Equal(31d, action.Data["LocalFindCrossCenterX"]);
            Assert.Equal(22d, action.Data["LocalFindCrossCenterY"]);
            Assert.Equal(@"C:\results\cross-73.json", action.Data["LocalFindCrossResultFile"]);
            Assert.Equal(73, result.MasterId);
            Assert.Equal(63, result.MasterResultType);
            Assert.Equal("Point_1", result.Result.Name);
            Assert.Null(result.ImageFilePath);
        }
        finally
        {
            action.RuntimeResources.Dispose();
        }
    }

    [Fact]
    public void CurrentFrameSourcePathIsPreservedForHistoricalResultOverlay()
    {
        const string sourcePath = @"C:\images\cross-source.tif";
        CVStartCFC action = CreateRawAction("local-find-cross-source", sourcePath);
        LocalFindCrossPersistenceRequest? persisted = null;
        FakeNodeServices services = new()
        {
            DetectHandler = (_, _, _) => CreateSuccessfulDetection(),
            PersistHandler = request =>
            {
                persisted = request;
                return new LocalFindCrossPersistenceResult
                {
                    MasterId = 74,
                    ResultFilePath = @"C:\results\cross-74.json"
                };
            }
        };
        try
        {
            LocalFindCrossNodeResultData result = new LocalFindCrossNode(services).ExecuteSynchronously(action);

            Assert.Equal(sourcePath, persisted!.ImageFilePath);
            Assert.Equal(sourcePath, result.ImageFilePath);
        }
        finally
        {
            action.RuntimeResources.Dispose();
        }
    }

    [Fact]
    public void TransformedFrameDoesNotReusePreTransformSourceForHistoricalOverlay()
    {
        const string sourcePath = @"C:\images\cross-source.tif";
        CVStartCFC action = CreateRawAction(
            "local-find-cross-transformed-source",
            sourcePath,
            calibrationTemplate: "OnSiteCalibration");
        LocalFindCrossPersistenceRequest? persisted = null;
        FakeNodeServices services = new()
        {
            DetectHandler = (_, _, _) => CreateSuccessfulDetection(),
            PersistHandler = request =>
            {
                persisted = request;
                return new LocalFindCrossPersistenceResult
                {
                    MasterId = 75,
                    ResultFilePath = @"C:\results\cross-75.json"
                };
            }
        };
        try
        {
            LocalFindCrossNodeResultData result = new LocalFindCrossNode(services).ExecuteSynchronously(action);

            Assert.Null(persisted!.ImageFilePath);
            Assert.Null(result.ImageFilePath);
        }
        finally
        {
            action.RuntimeResources.Dispose();
        }
    }

    [Fact]
    public void DetectionMustContainExactlyOneFiniteResult()
    {
        InvalidOperationException empty = Assert.Throws<InvalidOperationException>(() =>
            LocalFindCrossNode.ValidateDetection(
                new LocalFindCrossDetection { Success = true },
                100,
                80));
        InvalidOperationException multiple = Assert.Throws<InvalidOperationException>(() =>
            LocalFindCrossNode.ValidateDetection(
                new LocalFindCrossDetection
                {
                    Success = true,
                    Items = [CreateItem(), CreateItem()]
                },
                100,
                80));
        InvalidOperationException invalid = Assert.Throws<InvalidOperationException>(() =>
            LocalFindCrossNode.ValidateDetection(
                new LocalFindCrossDetection
                {
                    Success = true,
                    Items = [CreateItem(centerX: double.NaN)]
                },
                100,
                80));

        Assert.Contains("当前为 0 个", empty.Message);
        Assert.Contains("当前为 2 个", multiple.Message);
        Assert.Contains("非有限", invalid.Message);
    }

    [Fact]
    public void DetectionFailureSurfacesReasonDiagnosticsAndNegativeNativeCode()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            LocalFindCrossNode.ValidateDetection(
                new LocalFindCrossDetection
                {
                    Success = false,
                    FailureReason = "NoIntersection",
                    NativeReturnCode = -2,
                    Diagnostics = new { Stage = "FourEdgeFit" }
                },
                100,
                80));

        Assert.Contains("NoIntersection", exception.Message);
        Assert.Contains("原生返回码 -2", exception.Message);
        Assert.Contains("FourEdgeFit", exception.Message);
    }

    [Fact]
    public void PersistenceContractIsVersionOneType63SingleDetailAndLegacyJson()
    {
        CVStartCFC action = new("persistence-contract");
        LocalFindCrossNodeItem item = new()
        {
            Name = "Point_1",
            X = 2888,
            Y = 1920,
            Width = 3751,
            Height = 2655,
            CenterX = 4712,
            CenterY = 3199,
            RotationAngle = -0.17305171489715576,
            TiltX = -0.60909968614578247,
            TiltY = -0.076140284538269043
        };
        LocalFindCrossPersistenceRequest request = new()
        {
            Action = action,
            DeviceCode = "ALG-1",
            ImageFilePath = @"H:\ColorVision\Transfer\G0941\source.cvraw",
            ZIndex = 4,
            TotalTime = 18,
            Parameters = new { ExpectedAngleDegrees = 0, AngleToleranceDegrees = 10 },
            Result = item
        };

        AlgResultMasterModel master = LocalFindCrossResultPersistence.CreateMasterModel(request, 9);
        string resultJson = LocalFindCrossResultPersistence.BuildLegacyResultJson(item);
        DetailCommonModel detail = LocalFindCrossResultPersistence.CreateDetail(27, @"C:\results\find-cross.json");
        JObject root = JObject.Parse(resultJson);
        JToken persistedItem = Assert.Single(root["result"]!);
        JObject detailJson = JObject.Parse(detail.ResultJson);

        Assert.Equal(ViewResultAlgType.FindCross, master.ImgFileType);
        Assert.Equal("1.0", master.version);
        Assert.Equal(9, master.BatchId);
        Assert.Equal(4, master.Zindex);
        Assert.Equal(27, detail.PId);
        Assert.Equal(@"C:\results\find-cross.json", detailJson.Value<string>("ResultFileName"));
        Assert.Equal("Point_1", persistedItem.Value<string>("name"));
        Assert.Equal(4712, persistedItem["center"]?.Value<int>("x"));
        Assert.Equal(3199, persistedItem["center"]?.Value<int>("y"));
        Assert.Equal(-0.60909968614578247, persistedItem["tilt"]!.Value<double>("tilt_x"), 14);
    }

    [Fact]
    public void PersistenceTransactionCommitsExactlyOneCommonDetail()
    {
        FakeResultTransaction transaction = new() { MasterId = 307 };

        int masterId = LocalFindCrossResultPersistence.SaveDatabaseCore(
            new AlgResultMasterModel
            {
                ImgFileType = ViewResultAlgType.FindCross,
                version = "1.0"
            },
            @"C:\results\find-cross.json",
            () => transaction);

        Assert.Equal(307, masterId);
        Assert.Equal(["Begin", "InsertMaster", "InsertDetail", "Commit", "Dispose"], transaction.Events);
        Assert.NotNull(transaction.Detail);
        Assert.Equal(307, transaction.Detail!.PId);
        Assert.Equal(@"C:\results\find-cross.json", JObject.Parse(transaction.Detail.ResultJson).Value<string>("ResultFileName"));
    }

    [Fact]
    public void PersistenceTransactionRollsBackWhenDetailCountIsNotOne()
    {
        FakeResultTransaction transaction = new() { MasterId = 307, InsertedDetailCount = 0 };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            LocalFindCrossResultPersistence.SaveDatabaseCore(
                new AlgResultMasterModel(),
                @"C:\results\find-cross.json",
                () => transaction));

        Assert.Contains("应写入 1 条，实际写入 0 条", exception.Message);
        Assert.Equal(["Begin", "InsertMaster", "InsertDetail", "Rollback", "Dispose"], transaction.Events);
    }

    private static LocalFindCrossDetection CreateSuccessfulDetection() => new()
    {
        Success = true,
        Items = [CreateItem()],
        Diagnostics = new { Confidence = 0.91, CandidateCount = 1 },
        NativeReturnCode = 512,
        RawJson = "{\"result\":[]}"
    };

    private static LocalFindCrossNodeItem CreateItem(double centerX = 31) => new()
    {
        Name = "Point_1",
        X = 5,
        Y = 6,
        Width = 50,
        Height = 32,
        CenterX = centerX,
        CenterY = 22,
        RotationAngle = -0.17,
        TiltX = -0.6,
        TiltY = -0.07
    };

    private static CVStartCFC CreateRawAction(
        string serialNumber,
        string? sourceFilePath = null,
        string? calibrationTemplate = null)
    {
        const int width = 64;
        const int height = 48;
        LocalFlowFrame frame = LocalFlowFrame.Allocate(
            new LocalFrameMetadata
            {
                Width = width,
                Height = height,
                SourceBpp = 8,
                Channels = 1,
                PrimaryBufferKind = LocalFrameBufferKind.CvRaw,
                SourceFilePath = sourceFilePath,
                CalibrationTemplate = calibrationTemplate ?? string.Empty
            },
            rawLength: width * height,
            cieLength: 0);
        CVStartCFC action = new(serialNumber);
        action.SetCurrentFrame(frame);
        return action;
    }

    private sealed class FakeNodeServices : ILocalFindCrossNodeServices
    {
        public Func<string, LocalFlowFrame> LoadFrameHandler { get; init; } = _ => throw new InvalidOperationException("File loading was not configured.");
        public Func<int, MeasureResultImgModel?> GetImageResultHandler { get; init; } = _ => throw new InvalidOperationException("Image-result loading was not configured.");
        public Func<HImage, RoiRect, string, LocalFindCrossDetection> DetectHandler { get; init; } = (_, _, _) => throw new InvalidOperationException("Detection was not configured.");
        public Func<LocalFindCrossPersistenceRequest, LocalFindCrossPersistenceResult> PersistHandler { get; init; } = _ => throw new InvalidOperationException("Persistence was not configured.");
        public Action<LocalFindCrossPublishRequest> PublishHandler { get; init; } = _ => { };

        public int DetectCount { get; private set; }
        public int PersistCount { get; private set; }
        public int PublishCount { get; private set; }

        public LocalFlowFrame LoadFrame(string filePath) => LoadFrameHandler(filePath);

        public MeasureResultImgModel? GetImageResult(int masterId) => GetImageResultHandler(masterId);

        public LocalFindCrossDetection Detect(HImage image, RoiRect roi, string parameterJson)
        {
            DetectCount++;
            return DetectHandler(image, roi, parameterJson);
        }

        public LocalFindCrossPersistenceResult Persist(LocalFindCrossPersistenceRequest request)
        {
            PersistCount++;
            return PersistHandler(request);
        }

        public void Publish(LocalFindCrossPublishRequest request)
        {
            PublishCount++;
            PublishHandler(request);
        }
    }

    private sealed class FakeResultTransaction : ILocalFindCrossResultTransaction
    {
        public List<string> Events { get; } = [];
        public int MasterId { get; init; } = 1;
        public int InsertedDetailCount { get; init; } = 1;
        public DetailCommonModel? Detail { get; private set; }

        public void Begin() => Events.Add("Begin");

        public int InsertMaster(AlgResultMasterModel model)
        {
            Assert.NotNull(model);
            Events.Add("InsertMaster");
            return MasterId;
        }

        public int InsertDetail(DetailCommonModel model)
        {
            Detail = model;
            Events.Add("InsertDetail");
            return InsertedDetailCount;
        }

        public void Commit() => Events.Add("Commit");

        public void Rollback() => Events.Add("Rollback");

        public void Dispose() => Events.Add("Dispose");
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
            state[key] = value;
        }
        return state;
    }
}
