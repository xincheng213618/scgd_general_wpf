using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.FindLightArea;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class LocalFindLuminousAreaNodeTests
{
    [Fact]
    public void NodeDefaultsToAutomaticFullFrameDetection()
    {
        LocalFindLuminousAreaNode node = new();
        node.Create();

        Assert.Equal("LocalFindLuminousAreaV2", node.NodeType);
        Assert.Equal("本地发光区定位(V2)", node.Title);
        Assert.Equal(["IN"], node.GetAllInputOptions().Select(option => option.Text));
        Assert.Equal(["OUT"], node.GetAllOutputOptions().Select(option => option.Text));
        Assert.Equal(string.Empty, node.ImageFilePath);
        Assert.Equal(string.Empty, node.SavePOITempName);
        Assert.Equal(Int32Rect.Empty, node.SearchRegion);
        Assert.Equal(LocalFindLuminousAreaNode.DefaultMinimumConfidence, node.MinimumConfidence);
        Assert.Null(typeof(LocalFindLuminousAreaNode).GetProperty("BufferLen"));
        Assert.Null(typeof(LocalFindLuminousAreaNode).GetProperty("OIndex"));
        Assert.Equal(typeof(FlowPoiTemplateEditor), FlowNodePropertyEditorAttribute.Resolve(
            typeof(LocalFindLuminousAreaNode), nameof(LocalFindLuminousAreaNode.SavePOITempName)));
    }

    [Fact]
    public void LocalOnlyDeviceCodeIsHiddenFromUserConfiguration()
    {
        var deviceCode = typeof(LocalFindLuminousAreaNode).GetProperty(nameof(LocalFindLuminousAreaNode.DeviceCode));

        Assert.NotNull(deviceCode);
        Assert.False(FlowNodePropertyMetadataProvider.Instance.IsBrowsable(deviceCode!));
    }

    [Fact]
    public void UserConfigurationRoundTripsThroughNodeState()
    {
        LocalFindLuminousAreaNode original = new();
        original.Create();
        original.ImageFilePath = @"C:\images\white.cvraw";
        original.SavePOITempName = "POI_W_AUTO";
        original.SearchRegion = new Int32Rect(12, 34, 560, 780);
        original.MinimumConfidence = 0.72;
        Dictionary<string, byte[]> state = ParseState(original.GetSaveData());

        LocalFindLuminousAreaNode restored = new();
        restored.Create();
        restored.OnLoadNode(state);

        Assert.Equal(original.ImageFilePath, restored.ImageFilePath);
        Assert.Equal(original.SavePOITempName, restored.SavePOITempName);
        Assert.Equal(original.SearchRegion, restored.SearchRegion);
        Assert.Equal(original.MinimumConfidence, restored.MinimumConfidence);
    }

    [Fact]
    public void SharedResultParserFeedsStrictLocalCornerOrder()
    {
        const string json = """
            {
              "Success": true,
              "Algorithm": "RobustV2",
              "Corners": [
                { "X": 1.25, "Y": 2.5 },
                { "X": 101.5, "Y": 3.0 },
                { "X": 99.75, "Y": 52.25 },
                { "X": 0.5, "Y": 50.0 }
              ],
              "Confidence": 0.91,
              "SideQuality": { "Top": { "Coverage": 0.88 } },
              "FailureReason": "",
              "Warnings": ["DarkCorner"]
            }
            """;

        Assert.True(LuminousAreaResultParser.TryParseV2(json, out LuminousAreaDetectionResult detection, out string error), error);
        LocalLuminousAreaCorner[] corners = LocalFindLuminousAreaNode.ValidateDetection(detection, 0.55);

        Assert.Equal(["LT", "RT", "RB", "LB"], corners.Select(corner => corner.Name));
        Assert.Collection(
            corners,
            corner => AssertCorner(corner, 1.25f, 2.5f),
            corner => AssertCorner(corner, 101.5f, 3f),
            corner => AssertCorner(corner, 99.75f, 52.25f),
            corner => AssertCorner(corner, 0.5f, 50f));
        Assert.Equal(0.91, detection.Confidence!.Value, 8);
        Assert.Equal("DarkCorner", Assert.Single(detection.Warnings));
        LuminousAreaSideQuality quality = Assert.Single(detection.SideQuality);
        Assert.Equal("Top", quality.Side);
        Assert.Equal(0.88, quality.Metrics["Coverage"], 8);
    }

    [Fact]
    public void ResultParserSurfacesNativeRejectionReason()
    {
        const string json = """
            {
              "Success": false,
              "Algorithm": "RobustV2",
              "Corners": [],
              "Confidence": 0.31,
              "FailureReason": "InsufficientSideSupport",
              "Warnings": []
            }
            """;

        Assert.True(LuminousAreaResultParser.TryParseV2(json, out LuminousAreaDetectionResult detection, out string error), error);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => LocalFindLuminousAreaNode.ValidateDetection(detection, 0.55));

        Assert.Contains("InsufficientSideSupport", exception.Message);
        Assert.Contains("0.310", exception.Message);
    }

    [Fact]
    public void AlgorithmRejectionDoesNotReportJsonLengthAsNativeErrorCode()
    {
        LuminousAreaDetectionResult rejection = LuminousAreaDetectionResult
            .CreateFailure("RobustV2", "LowConfidence")
            .WithNativeContext("{\"Success\":false}", 312);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => LocalFindLuminousAreaNode.ValidateDetection(rejection, 0.45));

        Assert.DoesNotContain("原生返回码", exception.Message);
    }

    [Fact]
    public void SystemFailureReportsNegativeNativeErrorCode()
    {
        LuminousAreaDetectionResult rejection = LuminousAreaDetectionResult.CreateFailure(
            "RobustV2", "NativeCallFailed", nativeReturnCode: -2);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => LocalFindLuminousAreaNode.ValidateDetection(rejection, 0.45));

        Assert.Contains("原生返回码 -2", exception.Message);
    }

    [Fact]
    public void ResultParserRejectsConfidenceBelowConfiguredGate()
    {
        const string json = """
            {
              "Success": true,
              "Algorithm": "RobustV2",
              "Corners": [
                { "X": 0, "Y": 0 },
                { "X": 100, "Y": 0 },
                { "X": 100, "Y": 50 },
                { "X": 0, "Y": 50 }
              ],
              "Confidence": 0.54,
              "FailureReason": "",
              "Warnings": []
            }
            """;

        Assert.True(LuminousAreaResultParser.TryParseV2(json, out LuminousAreaDetectionResult detection, out string error), error);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => LocalFindLuminousAreaNode.ValidateDetection(detection, 0.55));

        Assert.Contains("置信度不足", exception.Message);
        Assert.Contains("0.540", exception.Message);
        Assert.Contains("0.550", exception.Message);
    }

    [Fact]
    public void DetailsAreBuiltInLtRtRbLbInsertionOrder()
    {
        LocalLuminousAreaCorner[] corners =
        [
            new() { Name = "LT", X = 10, Y = 20 },
            new() { Name = "RT", X = 110, Y = 21 },
            new() { Name = "RB", X = 109, Y = 71 },
            new() { Name = "LB", X = 9, Y = 70 }
        ];

        var details = LocalLuminousAreaResultPersistence.CreateDetails(42, corners);

        Assert.Equal(4, details.Count);
        Assert.All(details, detail => Assert.Equal(42, detail.Pid));
        Assert.Equal(corners.Select(corner => corner.X), details.Select(detail => detail.PosX));
        Assert.Equal(corners.Select(corner => corner.Y), details.Select(detail => detail.PosY));
    }

    [Theory]
    [InlineData(LocalLuminousAreaPoiTemplateUpdater.RectPointType, (int)LocalLuminousAreaPoiTemplateShape.Rectangle, 60, 46)]
    [InlineData(LocalLuminousAreaPoiTemplateUpdater.LeftTopRectPointType, (int)LocalLuminousAreaPoiTemplateShape.LeftTopRectangle, 9, 20)]
    public void PoiSaveTemplateUpdatesSingleServiceRectangle(
        int pointType,
        int expectedShape,
        int expectedX,
        int expectedY)
    {
        PoiDetailModel source = new()
        {
            Id = 7,
            Pid = 3,
            Name = "Area",
            Type = (GraphicTypes)pointType,
            PixX = 1,
            PixY = 2,
            PixWidth = 3,
            PixHeight = 4
        };

        LocalLuminousAreaPoiTemplateUpdate update = LocalLuminousAreaPoiTemplateUpdater.BuildUpdate(
            [source], CreateFractionalLuminousAreaPoints(), "POI_W_AUTO");

        Assert.Equal((LocalLuminousAreaPoiTemplateShape)expectedShape, update.Shape);
        PoiDetailModel detail = Assert.Single(update.Details);
        Assert.Equal(7, detail.Id);
        Assert.Equal(3, detail.Pid);
        Assert.Equal("Area", detail.Name);
        Assert.Equal(pointType, (int)detail.Type);
        Assert.Equal(expectedX, detail.PixX);
        Assert.Equal(expectedY, detail.PixY);
        Assert.Equal(102, detail.PixWidth);
        Assert.Equal(52, detail.PixHeight);
        Assert.Equal(1, source.PixX);
        Assert.Equal(2, source.PixY);
    }

    [Fact]
    public void PoiSaveTemplateRectangleIncludesIntegerMaximumLikeOpenCvBoundingRect()
    {
        PoiDetailModel source = new() { Id = 8, Pid = 3, Type = (GraphicTypes)LocalLuminousAreaPoiTemplateUpdater.LeftTopRectPointType };
        LuminousAreaPoint[] corners =
        [
            new(10, 20),
            new(110, 20),
            new(110, 70),
            new(10, 70)
        ];

        PoiDetailModel detail = Assert.Single(LocalLuminousAreaPoiTemplateUpdater.BuildUpdate([source], corners).Details);

        Assert.Equal(10, detail.PixX);
        Assert.Equal(20, detail.PixY);
        Assert.Equal(101, detail.PixWidth);
        Assert.Equal(51, detail.PixHeight);
    }

    [Theory]
    [InlineData(LocalLuminousAreaPoiTemplateUpdater.PolygonFourPointType)]
    [InlineData(LocalLuminousAreaPoiTemplateUpdater.LeftTopRectPointType)]
    public void PoiSaveTemplateUpdatesFourCornersAndNormalizesServicePointType(int storedPointType)
    {
        PoiDetailModel[] source = Enumerable.Range(0, 4)
            .Select(index => new PoiDetailModel
            {
                Id = 20 + index,
                Pid = 5,
                Name = new[] { "LeftTop", "RightTop", "RightBottom", "LeftBottom" }[index],
                Type = (GraphicTypes)storedPointType,
                PixWidth = 8,
                PixHeight = 9
            })
            .ToArray();

        LocalLuminousAreaPoiTemplateUpdate update = LocalLuminousAreaPoiTemplateUpdater.BuildUpdate(
            source, CreateFractionalLuminousAreaPoints(), "POI_W_AUTO");

        Assert.Equal(LocalLuminousAreaPoiTemplateShape.PolygonFour, update.Shape);
        Assert.Equal([10, 110, 109, 9], update.Details.Select(detail => detail.PixX));
        Assert.Equal([20, 21, 71, 70], update.Details.Select(detail => detail.PixY));
        Assert.All(update.Details, detail =>
        {
            Assert.Equal(LocalLuminousAreaPoiTemplateUpdater.PolygonFourPointType, (int)detail.Type);
            Assert.Equal(0, detail.PixWidth);
            Assert.Equal(0, detail.PixHeight);
        });
    }

    [Fact]
    public void PoiSaveTemplateUsesFirstFourPointRowAsLegacyServiceDiscriminator()
    {
        int[] storedTypes =
        [
            LocalLuminousAreaPoiTemplateUpdater.PolygonFourPointType,
            (int)GraphicTypes.Circle,
            (int)GraphicTypes.Rect,
            (int)GraphicTypes.Polygon
        ];
        PoiDetailModel[] source = storedTypes.Select((type, index) => new PoiDetailModel
        {
            Id = 30 + index,
            Pid = 6,
            Type = (GraphicTypes)type
        }).ToArray();

        LocalLuminousAreaPoiTemplateUpdate update = LocalLuminousAreaPoiTemplateUpdater.BuildUpdate(
            source, CreateFractionalLuminousAreaPoints(), "POI_W_AUTO");

        Assert.Equal(LocalLuminousAreaPoiTemplateShape.PolygonFour, update.Shape);
        Assert.All(update.Details, detail =>
            Assert.Equal(LocalLuminousAreaPoiTemplateUpdater.PolygonFourPointType, (int)detail.Type));
    }

    [Fact]
    public void PoiSaveTemplateRejectsUnsupportedShapeWithoutMutatingSource()
    {
        PoiDetailModel source = new()
        {
            Id = 1,
            Pid = 2,
            Type = GraphicTypes.Circle,
            PixX = 33,
            PixY = 44
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            LocalLuminousAreaPoiTemplateUpdater.BuildUpdate([source], CreateFractionalLuminousAreaPoints(), "BadTemplate"));

        Assert.Contains("Rect/LTRect", exception.Message);
        Assert.Contains("PolygonFour", exception.Message);
        Assert.Equal(33, source.PixX);
        Assert.Equal(44, source.PixY);
    }

    [Fact]
    public void ReversedOrShiftedCornerOrderIsRejected()
    {
        LuminousAreaPoint lt = new(10, 20);
        LuminousAreaPoint rt = new(110, 21);
        LuminousAreaPoint rb = new(109, 71);
        LuminousAreaPoint lb = new(9, 70);
        IReadOnlyList<LuminousAreaPoint>[] invalidOrders =
        [
            [lt, lb, rb, rt],
            [rt, rb, lb, lt]
        ];

        foreach (IReadOnlyList<LuminousAreaPoint> corners in invalidOrders)
        {
            LuminousAreaDetectionResult detection = new(
                true, "RobustV2", corners, 0.9, null, string.Empty, null);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => LocalFindLuminousAreaNode.ValidateDetection(detection, 0.45));

            Assert.Contains("LT、RT、RB、LB", exception.Message);
        }
    }

    [Fact]
    public void SearchRegionUsesZeroRectForFullFrameAndRejectsOutOfBoundsRoi()
    {
        RoiRect fullFrame = LocalFindLuminousAreaNode.ResolveRoi(Int32Rect.Empty, 1000, 800);
        RoiRect region = LocalFindLuminousAreaNode.ResolveRoi(new Int32Rect(10, 20, 300, 200), 1000, 800);

        Assert.Equal(0, fullFrame.Width);
        Assert.Equal(0, fullFrame.Height);
        Assert.Equal(10, region.X);
        Assert.Equal(20, region.Y);
        Assert.Equal(300, region.Width);
        Assert.Equal(200, region.Height);
        Assert.Throws<InvalidOperationException>(
            () => LocalFindLuminousAreaNode.ResolveRoi(new Int32Rect(900, 700, 200, 200), 1000, 800));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 1)]
    public void CieDescriptorBorrowsLuminancePlane(int planeCount, int luminancePlaneIndex)
    {
        const int width = 7;
        const int height = 5;
        int planeBytes = width * height * sizeof(float);
        using LocalFlowFrame frame = LocalFlowFrame.Allocate(
            new LocalFrameMetadata
            {
                Width = width,
                Height = height,
                SourceBpp = 16,
                CieBpp = 32,
                Channels = planeCount,
                PrimaryBufferKind = LocalFrameBufferKind.CvCie
            },
            rawLength: 0,
            cieLength: planeBytes * planeCount);
        using LocalFlowFrameLease lease = frame.Acquire();

        HImage image = LocalFindLuminousAreaNode.CreateBorrowedImage(lease);

        Assert.Equal(height, image.rows);
        Assert.Equal(width, image.cols);
        Assert.Equal(1, image.channels);
        Assert.Equal(32, image.depth);
        Assert.Equal(width * sizeof(float), image.stride);
        Assert.True(image.isDispose);
        Assert.Equal(IntPtr.Add(lease.CiePointer, luminancePlaneIndex * planeBytes), image.pData);
    }

    [Fact]
    public void CiePrimaryWithoutCieBufferDoesNotSilentlyFallBackToRaw()
    {
        const int width = 7;
        const int height = 5;
        using LocalFlowFrame frame = LocalFlowFrame.Allocate(
            new LocalFrameMetadata
            {
                Width = width,
                Height = height,
                SourceBpp = 8,
                CieBpp = 32,
                Channels = 1,
                PrimaryBufferKind = LocalFrameBufferKind.CvCie
            },
            rawLength: width * height,
            cieLength: 0);
        using LocalFlowFrameLease lease = frame.Acquire();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => LocalFindLuminousAreaNode.CreateBorrowedImage(lease));

        Assert.Contains("没有可用的 CIE 数据", exception.Message);
    }

    [Fact]
    public void RawDescriptorKeepsInterleavedLayout()
    {
        const int width = 7;
        const int height = 5;
        const int channels = 3;
        const int depth = 16;
        int stride = width * channels * (depth / 8);
        using LocalFlowFrame frame = LocalFlowFrame.Allocate(
            new LocalFrameMetadata
            {
                Width = width,
                Height = height,
                SourceBpp = depth,
                Channels = channels,
                PrimaryBufferKind = LocalFrameBufferKind.CvRaw
            },
            rawLength: stride * height,
            cieLength: 0);
        using LocalFlowFrameLease lease = frame.Acquire();

        HImage image = LocalFindLuminousAreaNode.CreateBorrowedImage(lease);

        Assert.Equal(channels, image.channels);
        Assert.Equal(depth, image.depth);
        Assert.Equal(stride, image.stride);
        Assert.Equal(lease.RawPointer, image.pData);
    }

    [Fact]
    public void FileExecutionLoadsRawDetectsPersistsPublishesAndOutputsMaster()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"ColorVision-LuminousArea-{Guid.NewGuid():N}.png");
        CVStartCFC? action = null;
        try
        {
            WriteGray8Png(filePath, 8, 6);
            LocalFindLuminousAreaPersistenceRequest? persisted = null;
            LocalFindLuminousAreaPublishRequest? published = null;
            FakeNodeServices services = new()
            {
                LoadFrameHandler = LocalFrameFileService.Load,
                DetectHandler = (image, roi, minimumConfidence) =>
                {
                    Assert.Equal(8, image.cols);
                    Assert.Equal(6, image.rows);
                    Assert.Equal(1, image.channels);
                    Assert.Equal(8, image.depth);
                    Assert.NotEqual(IntPtr.Zero, image.pData);
                    Assert.Equal(0, roi.Width);
                    Assert.Equal(0, roi.Height);
                    Assert.Equal(LocalFindLuminousAreaNode.DefaultMinimumConfidence, minimumConfidence, 8);
                    return CreateSuccessfulDetection();
                },
                PersistHandler = request =>
                {
                    persisted = request;
                    IReadOnlyCollection<AlgResultLightAreaModel> details = request.CreateDetails(73);
                    Assert.Equal(4, details.Count);
                    Assert.All(details, detail => Assert.Equal(73, detail.Pid));
                    return 73;
                },
                PublishHandler = request => published = request
            };
            LocalFindLuminousAreaNode node = new(services) { ImageFilePath = filePath };
            action = new CVStartCFC("file-execution");

            LocalFindLuminousAreaNodeResultData result = node.ExecuteSynchronously(action);

            Assert.Equal(1, services.LoadCount);
            Assert.Equal(1, services.DetectCount);
            Assert.Equal(1, services.PersistCount);
            Assert.Equal(1, services.PublishCount);
            Assert.NotNull(persisted);
            Assert.Same(action, persisted!.Action);
            Assert.Equal("RobustV2", persisted.Algorithm);
            Assert.Equal(Path.GetFullPath(filePath), persisted.ImageFilePath);
            Assert.Equal(["LT", "RT", "RB", "LB"], persisted.Corners.Select(corner => corner.Name));
            Assert.NotNull(published);
            Assert.Equal(73, published!.MasterId);
            Assert.Equal("FindLightArea", published.OperatorCode);
            Assert.Equal(action.SerialNumber, published.SerialNumber);
            Assert.Equal(73, Convert.ToInt32(action.Data["MasterId"]));
            Assert.Equal((int)ViewResultAlgType.FindLightArea, Convert.ToInt32(action.Data["MasterResultType"]));
            Assert.Equal("LT,RT,RB,LB", action.Data["LocalLuminousAreaCornerOrder"]);
            Assert.Equal(73, result.MasterId);
            Assert.Equal((int)ViewResultAlgType.FindLightArea, result.MasterResultType);
            Assert.Equal(Path.GetFullPath(filePath), result.ImageFilePath);
            Assert.NotNull(result.FrameId);
            Assert.True(action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame));
            Assert.NotNull(currentFrame);
            Assert.Equal(result.FrameId, currentFrame!.FrameId.ToString("N"));
        }
        finally
        {
            action?.RuntimeResources.Dispose();
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void ConfiguredPoiTemplateUpdatesBeforeResultPersistence()
    {
        CVStartCFC action = CreateRawAction("poi-save-template");
        List<string> events = [];
        LocalFindLuminousAreaPersistenceRequest? persisted = null;
        FakeNodeServices services = new()
        {
            DetectHandler = (_, _, _) => CreateSuccessfulDetection(),
            UpdatePoiTemplateHandler = (templateName, corners) =>
            {
                events.Add("UpdatePoiTemplate");
                Assert.Equal("POI_W_AUTO", templateName);
                Assert.Equal(["LT", "RT", "RB", "LB"], corners.Select(corner => corner.Name));
                return LocalLuminousAreaPoiTemplateShape.PolygonFour;
            },
            PersistHandler = request =>
            {
                events.Add("Persist");
                persisted = request;
                return 181;
            },
            PublishHandler = _ => events.Add("Publish")
        };
        LocalFindLuminousAreaNode node = new(services) { SavePOITempName = " POI_W_AUTO " };
        try
        {
            LocalFindLuminousAreaNodeResultData result = node.ExecuteSynchronously(action);

            Assert.Equal(["UpdatePoiTemplate", "Persist", "Publish"], events);
            Assert.Equal(1, services.UpdatePoiTemplateCount);
            Assert.Equal("POI_W_AUTO", result.SavePoiTemplateName);
            Assert.Equal(nameof(LocalLuminousAreaPoiTemplateShape.PolygonFour), result.SavePoiTemplateShape);
            Assert.Equal("POI_W_AUTO", action.Data["LocalLuminousAreaSavePOITemplate"]);
            Assert.Equal(nameof(LocalLuminousAreaPoiTemplateShape.PolygonFour), action.Data["LocalLuminousAreaSavePOITemplateShape"]);
            Assert.NotNull(persisted);
            JObject parameters = JObject.FromObject(persisted!.Parameters);
            Assert.Equal("POI_W_AUTO", parameters["SavePOITemplate"]?.Value<string>("Name"));
            Assert.Equal(nameof(LocalLuminousAreaPoiTemplateShape.PolygonFour), parameters["SavePOITemplate"]?.Value<string>("Shape"));
        }
        finally
        {
            action.RuntimeResources.Dispose();
        }
    }

    [Fact]
    public void PoiTemplateUpdateFailureDoesNotPersistOrPublishResult()
    {
        CVStartCFC action = CreateRawAction("poi-save-template-failure");
        FakeNodeServices services = new()
        {
            DetectHandler = (_, _, _) => CreateSuccessfulDetection(),
            UpdatePoiTemplateHandler = (_, _) => throw new InvalidOperationException("invalid POI template"),
            PersistHandler = _ => throw new InvalidOperationException("Persist must not run.")
        };
        LocalFindLuminousAreaNode node = new(services) { SavePOITempName = "BadTemplate" };
        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => node.ExecuteSynchronously(action));

            Assert.Equal("invalid POI template", exception.Message);
            Assert.Equal(1, services.UpdatePoiTemplateCount);
            Assert.Equal(0, services.PersistCount);
            Assert.Equal(0, services.PublishCount);
            Assert.False(action.Data.ContainsKey("MasterId"));
        }
        finally
        {
            action.RuntimeResources.Dispose();
        }
    }

    [Fact]
    public void InputImageResultFallsBackToPersistedFileWhenMemoryFrameIsUnavailable()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"ColorVision-LuminousArea-Input-{Guid.NewGuid():N}.png");
        CVStartCFC action = new("input-image-result");
        try
        {
            WriteGray8Png(filePath, 8, 6);
            action.MasterValue(null, 92, (int)CVCommCore.CVResultType.Camera_Img);
            FakeNodeServices services = new()
            {
                GetImageResultHandler = masterId =>
                {
                    Assert.Equal(92, masterId);
                    return new MeasureResultImgModel { Id = masterId, FileUrl = filePath };
                },
                LoadFrameHandler = LocalFrameFileService.Load,
                DetectHandler = (_, _, _) => CreateSuccessfulDetection(),
                PersistHandler = _ => 193
            };
            LocalFindLuminousAreaNode node = new(services);

            LocalFindLuminousAreaNodeResultData result = node.ExecuteSynchronously(action);

            Assert.Equal(1, services.GetImageResultCount);
            Assert.Equal(1, services.LoadCount);
            Assert.Equal(Path.GetFullPath(filePath), result.ImageFilePath);
            Assert.Equal(92, result.SourceMasterId);
            Assert.Equal(193, result.MasterId);
        }
        finally
        {
            action.RuntimeResources.Dispose();
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void UpstreamFrameTakesPriorityOverConfiguredFallbackFile()
    {
        CVStartCFC action = CreateRawAction("upstream-priority");
        Assert.True(action.TryGetCurrentFrame(out LocalFlowFrame? expectedFrame));
        FakeNodeServices services = new()
        {
            DetectHandler = (_, _, _) => CreateSuccessfulDetection(),
            PersistHandler = _ => 79
        };
        LocalFindLuminousAreaNode node = new(services)
        {
            ImageFilePath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.cvraw")
        };
        try
        {
            LocalFindLuminousAreaNodeResultData result = node.ExecuteSynchronously(action);

            Assert.Equal(0, services.LoadCount);
            Assert.True(action.TryGetCurrentFrame(out LocalFlowFrame? actualFrame));
            Assert.Same(expectedFrame, actualFrame);
            Assert.Equal(expectedFrame!.FrameId.ToString("N"), result.FrameId);
            Assert.Null(result.ImageFilePath);
        }
        finally
        {
            action.RuntimeResources.Dispose();
        }
    }

    [Fact]
    public void UpstreamCieExecutionPassesYPlaneToDetectorAndPreservesSourceMaster()
    {
        const int width = 7;
        const int height = 5;
        const string sourcePath = @"C:\capture\original.cvraw";
        int planeBytes = width * height * sizeof(float);
        LocalFlowFrame frame = LocalFlowFrame.Allocate(
            new LocalFrameMetadata
            {
                Width = width,
                Height = height,
                SourceBpp = 16,
                CieBpp = 32,
                Channels = 3,
                PrimaryBufferKind = LocalFrameBufferKind.CvCie,
                SourceFilePath = sourcePath,
                CalibrationTemplate = "cal-a"
            },
            rawLength: 0,
            cieLength: planeBytes * 3);
        frame.MasterId = 29;
        CVStartCFC action = new("cie-execution");
        action.SetCurrentFrame(frame);
        using LocalFlowFrameLease expectedLease = frame.Acquire();
        IntPtr expectedY = IntPtr.Add(expectedLease.CiePointer, planeBytes);
        LocalFindLuminousAreaPersistenceRequest? persisted = null;
        FakeNodeServices services = new()
        {
            DetectHandler = (image, _, _) =>
            {
                Assert.Equal(1, image.channels);
                Assert.Equal(32, image.depth);
                Assert.Equal(width * sizeof(float), image.stride);
                Assert.Equal(expectedY, image.pData);
                return CreateSuccessfulDetection();
            },
            PersistHandler = request =>
            {
                persisted = request;
                IReadOnlyCollection<AlgResultLightAreaModel> details = request.CreateDetails(101);
                Assert.Equal(4, details.Count);
                Assert.All(details, detail => Assert.Equal(101, detail.Pid));
                return 101;
            }
        };
        LocalFindLuminousAreaNode node = new(services);
        try
        {
            LocalFindLuminousAreaNodeResultData result = node.ExecuteSynchronously(action);

            Assert.Equal(29, result.SourceMasterId);
            Assert.NotNull(result.FrameId);
            Assert.Equal(101, result.MasterId);
            Assert.Equal(101, Convert.ToInt32(action.Data["MasterId"]));
            Assert.NotNull(persisted);
            Assert.Null(persisted!.ImageFilePath);
            JObject parameters = JObject.FromObject(persisted.Parameters);
            Assert.Equal(nameof(LocalFrameBufferKind.CvCie), parameters.Value<string>("PrimaryBufferKind"));
            Assert.Equal(sourcePath, parameters.Value<string>("SourceFilePath"));
            Assert.Null(parameters["CvCieFilePath"]?.Value<string>());
            Assert.Equal("cal-a", parameters.Value<string>("CalibrationTemplate"));
            Assert.True(parameters.Value<bool>("MemoryOnly"));
        }
        finally
        {
            action.RuntimeResources.Dispose();
        }
    }

    [Fact]
    public void DetectionFailurePersistsAndPublishesFailedMasterWithoutOutputMaster()
    {
        CVStartCFC action = CreateRawAction("detection-failure");
        LocalFindLuminousAreaPersistenceRequest? persisted = null;
        LocalFindLuminousAreaPublishRequest? published = null;
        FakeNodeServices services = new()
        {
            DetectHandler = (_, _, _) => LuminousAreaDetectionResult.CreateFailure("RobustV2", "NoCandidate"),
            PersistHandler = request =>
            {
                persisted = request;
                return 117;
            },
            PublishHandler = request => published = request
        };
        LocalFindLuminousAreaNode node = new(services);
        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => node.ExecuteSynchronously(action));

            Assert.Contains("NoCandidate", exception.Message);
            Assert.Equal(1, services.PersistCount);
            Assert.Equal(1, services.PublishCount);
            Assert.NotNull(persisted);
            Assert.Equal(LocalFindLuminousAreaNode.DetectionFailureResultCode, persisted!.ResultCode);
            Assert.Equal(exception.Message, persisted.Result);
            Assert.Empty(persisted.Corners);
            JObject parameters = JObject.FromObject(persisted.Parameters);
            Assert.False(parameters.Value<bool>("Success"));
            Assert.Equal("NoCandidate", parameters.Value<string>("FailureReason"));
            Assert.NotNull(published);
            Assert.Equal(117, published!.MasterId);
            Assert.False(action.Data.ContainsKey("MasterId"));
        }
        finally
        {
            action.RuntimeResources.Dispose();
        }
    }

    [Fact]
    public void PersistenceFailureDoesNotPublishOrExposeUncommittedMaster()
    {
        CVStartCFC action = CreateRawAction("persistence-failure");
        FakeNodeServices services = new()
        {
            DetectHandler = (_, _, _) => CreateSuccessfulDetection(),
            PersistHandler = _ => throw new InvalidOperationException("transaction crashed")
        };
        LocalFindLuminousAreaNode node = new(services);
        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => node.ExecuteSynchronously(action));

            Assert.Equal("transaction crashed", exception.Message);
            Assert.Equal(1, services.PersistCount);
            Assert.Equal(0, services.PublishCount);
            Assert.False(action.Data.ContainsKey("MasterId"));
        }
        finally
        {
            action.RuntimeResources.Dispose();
        }
    }

    [Fact]
    public void PublishFailureKeepsCommittedMasterReferenceForRecovery()
    {
        CVStartCFC action = CreateRawAction("publish-failure");
        FakeNodeServices services = new()
        {
            DetectHandler = (_, _, _) => CreateSuccessfulDetection(),
            PersistHandler = _ => 211,
            PublishHandler = _ => throw new InvalidOperationException("publish crashed")
        };
        LocalFindLuminousAreaNode node = new(services);
        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => node.ExecuteSynchronously(action));

            Assert.Equal("publish crashed", exception.Message);
            Assert.Equal(1, services.PersistCount);
            Assert.Equal(1, services.PublishCount);
            Assert.Equal(211, Convert.ToInt32(action.Data["MasterId"]));
            Assert.Equal((int)ViewResultAlgType.FindLightArea, Convert.ToInt32(action.Data["MasterResultType"]));
        }
        finally
        {
            action.RuntimeResources.Dispose();
        }
    }

    [Fact]
    public void TransactionCoreCommitsMasterAndDetailsFromAssignedId()
    {
        FakeResultTransaction transaction = new() { MasterId = 307 };
        int factoryMasterId = -1;
        LocalLuminousAreaCorner[] corners = CreateLocalCorners();

        int masterId = LocalFlowResultPersistence.SaveAlgorithmResultWithDetailsCore(
            new AlgResultMasterModel(),
            ViewResultAlgType.FindLightArea,
            id =>
            {
                factoryMasterId = id;
                return LocalLuminousAreaResultPersistence.CreateDetails(id, corners);
            },
            () => transaction);

        Assert.Equal(307, masterId);
        Assert.Equal(307, factoryMasterId);
        Assert.Equal(["Begin", "InsertMaster", "InsertDetails", "Commit", "Dispose"], transaction.Events);
        Assert.Equal(4, transaction.Details.Count);
        Assert.All(transaction.Details, detail => Assert.Equal(307, detail.Pid));
        Assert.Equal(corners.Select(corner => corner.X), transaction.Details.Select(detail => detail.PosX));
        Assert.Equal(corners.Select(corner => corner.Y), transaction.Details.Select(detail => detail.PosY));
    }

    [Fact]
    public void TransactionCoreRollsBackWhenDetailFactoryCrashes()
    {
        FakeResultTransaction transaction = new() { MasterId = 401 };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            LocalFlowResultPersistence.SaveAlgorithmResultWithDetailsCore<AlgResultLightAreaModel>(
                new AlgResultMasterModel(),
                ViewResultAlgType.FindLightArea,
                _ => throw new InvalidOperationException("detail factory crashed"),
                () => transaction));

        Assert.Equal("detail factory crashed", exception.Message);
        Assert.Equal(["Begin", "InsertMaster", "Rollback", "Dispose"], transaction.Events);
    }

    [Fact]
    public void TransactionCoreRollsBackShortDetailInsert()
    {
        FakeResultTransaction transaction = new() { MasterId = 409, InsertedDetailCount = 3 };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            LocalFlowResultPersistence.SaveAlgorithmResultWithDetailsCore(
                new AlgResultMasterModel(),
                ViewResultAlgType.FindLightArea,
                id => LocalLuminousAreaResultPersistence.CreateDetails(id, CreateLocalCorners()),
                () => transaction));

        Assert.Contains("应写入 4 条，实际写入 3 条", exception.Message);
        Assert.Equal(["Begin", "InsertMaster", "InsertDetails", "Rollback", "Dispose"], transaction.Events);
    }

    [Fact]
    public void TransactionCorePreservesSaveAndRollbackFailures()
    {
        FakeResultTransaction transaction = new()
        {
            MasterId = -1,
            RollbackException = new InvalidOperationException("rollback crashed")
        };

        AggregateException exception = Assert.Throws<AggregateException>(() =>
            LocalFlowResultPersistence.SaveAlgorithmResultWithDetailsCore(
                new AlgResultMasterModel(),
                ViewResultAlgType.FindLightArea,
                id => LocalLuminousAreaResultPersistence.CreateDetails(id, CreateLocalCorners()),
                () => transaction));

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Contains("主表失败", exception.InnerExceptions[0].Message);
        Assert.Equal("rollback crashed", exception.InnerExceptions[1].Message);
        Assert.Equal(["Begin", "InsertMaster", "Rollback", "Dispose"], transaction.Events);
    }

    private static void AssertCorner(LocalLuminousAreaCorner corner, float x, float y)
    {
        Assert.Equal(x, corner.X);
        Assert.Equal(y, corner.Y);
    }

    private static LuminousAreaDetectionResult CreateSuccessfulDetection() => new(
        true,
        "RobustV2",
        [
            new LuminousAreaPoint(1, 1),
            new LuminousAreaPoint(6, 1),
            new LuminousAreaPoint(6, 4),
            new LuminousAreaPoint(1, 4)
        ],
        0.86,
        null,
        string.Empty,
        ["Synthetic"]);

    private static LocalLuminousAreaCorner[] CreateLocalCorners() =>
    [
        new() { Name = "LT", X = 1, Y = 1 },
        new() { Name = "RT", X = 6, Y = 1 },
        new() { Name = "RB", X = 6, Y = 4 },
        new() { Name = "LB", X = 1, Y = 4 }
    ];

    private static LuminousAreaPoint[] CreateFractionalLuminousAreaPoints() =>
    [
        new(10.2, 20.7),
        new(110.1, 21.2),
        new(109.4, 71.9),
        new(9.8, 70.3)
    ];

    private static CVStartCFC CreateRawAction(string serialNumber)
    {
        const int width = 8;
        const int height = 6;
        LocalFlowFrame frame = LocalFlowFrame.Allocate(
            new LocalFrameMetadata
            {
                Width = width,
                Height = height,
                SourceBpp = 8,
                Channels = 1,
                PrimaryBufferKind = LocalFrameBufferKind.CvRaw
            },
            rawLength: width * height,
            cieLength: 0);
        CVStartCFC action = new(serialNumber);
        action.SetCurrentFrame(frame);
        return action;
    }

    private static void WriteGray8Png(string filePath, int width, int height)
    {
        WpfTestHost.Invoke(() =>
        {
            byte[] pixels = Enumerable.Range(0, width * height).Select(index => (byte)(index % 256)).ToArray();
            BitmapSource source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, pixels, width);
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using FileStream stream = File.Create(filePath);
            encoder.Save(stream);
        });
    }

    private sealed class FakeNodeServices : ILocalFindLuminousAreaNodeServices
    {
        public Func<string, LocalFlowFrame> LoadFrameHandler { get; init; } = _ => throw new InvalidOperationException("File loading was not configured.");
        public Func<int, MeasureResultImgModel?> GetImageResultHandler { get; init; } = _ => throw new InvalidOperationException("Image-result loading was not configured.");
        public Func<HImage, RoiRect, double, LuminousAreaDetectionResult> DetectHandler { get; init; } = (_, _, _) => throw new InvalidOperationException("Detection was not configured.");
        public Func<string, IReadOnlyList<LocalLuminousAreaCorner>, LocalLuminousAreaPoiTemplateShape> UpdatePoiTemplateHandler { get; init; } = (_, _) => throw new InvalidOperationException("POI-template updating was not configured.");
        public Func<LocalFindLuminousAreaPersistenceRequest, int> PersistHandler { get; init; } = _ => throw new InvalidOperationException("Persistence was not configured.");
        public Action<LocalFindLuminousAreaPublishRequest> PublishHandler { get; init; } = _ => { };

        public int LoadCount { get; private set; }
        public int GetImageResultCount { get; private set; }
        public int DetectCount { get; private set; }
        public int UpdatePoiTemplateCount { get; private set; }
        public int PersistCount { get; private set; }
        public int PublishCount { get; private set; }

        public LocalFlowFrame LoadFrame(string filePath)
        {
            LoadCount++;
            return LoadFrameHandler(filePath);
        }

        public MeasureResultImgModel? GetImageResult(int masterId)
        {
            GetImageResultCount++;
            return GetImageResultHandler(masterId);
        }

        public LuminousAreaDetectionResult Detect(HImage image, RoiRect roi, double minimumConfidence)
        {
            DetectCount++;
            return DetectHandler(image, roi, minimumConfidence);
        }

        public LocalLuminousAreaPoiTemplateShape UpdatePoiTemplate(
            string templateName,
            IReadOnlyList<LocalLuminousAreaCorner> corners)
        {
            UpdatePoiTemplateCount++;
            return UpdatePoiTemplateHandler(templateName, corners);
        }

        public int Persist(LocalFindLuminousAreaPersistenceRequest request)
        {
            PersistCount++;
            return PersistHandler(request);
        }

        public void Publish(LocalFindLuminousAreaPublishRequest request)
        {
            PublishCount++;
            PublishHandler(request);
        }
    }

    private sealed class FakeResultTransaction : ILocalFlowResultTransaction<AlgResultLightAreaModel>
    {
        public List<string> Events { get; } = [];
        public List<AlgResultLightAreaModel> Details { get; private set; } = [];
        public int MasterId { get; init; } = 1;
        public int? InsertedDetailCount { get; init; }
        public Exception? RollbackException { get; init; }

        public void Begin() => Events.Add("Begin");

        public int InsertMaster(AlgResultMasterModel model)
        {
            Assert.NotNull(model);
            Events.Add("InsertMaster");
            return MasterId;
        }

        public int InsertDetails(IReadOnlyCollection<AlgResultLightAreaModel> details)
        {
            Events.Add("InsertDetails");
            Details = details.ToList();
            return InsertedDetailCount ?? Details.Count;
        }

        public void Commit() => Events.Add("Commit");

        public void Rollback()
        {
            Events.Add("Rollback");
            if (RollbackException != null) throw RollbackException;
        }

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
