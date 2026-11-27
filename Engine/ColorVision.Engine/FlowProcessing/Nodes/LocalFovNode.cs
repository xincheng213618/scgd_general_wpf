using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Services.Results.LightArea;
using ColorVision.Engine.Templates.Jsons;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Engine.FlowProcessing.Nodes
{
    internal sealed record FovCameraCalibration
    {
        public required string CameraDeviceCode { get; init; }
        public string? PhysicalCameraCode { get; init; }
    }

    internal static class FovCameraCalibrationResolver
    {
        public static FovCameraCalibration Resolve(string? preferredCameraDeviceCode, MySqlConnectedEndpoint endpoint)
        {
            if (string.IsNullOrWhiteSpace(preferredCameraDeviceCode))
                return new FovCameraCalibration { CameraDeviceCode = string.Empty };

            DeviceCamera[] deviceCameras = ServiceManager.Current?.DeviceServices.OfType<DeviceCamera>()
                .Where(camera => camera.ResultDatabaseEndpoint == null || ReferenceEquals(camera.ResultDatabaseEndpoint, endpoint))
                .ToArray() ?? Array.Empty<DeviceCamera>();
            DeviceCamera? selected = deviceCameras.FirstOrDefault(camera =>
                string.Equals(camera.Code, preferredCameraDeviceCode, StringComparison.OrdinalIgnoreCase));
            if (selected != null) return FromDevice(selected);

            selected = deviceCameras.FirstOrDefault(camera =>
                string.Equals(camera.Config.CameraCode, preferredCameraDeviceCode, StringComparison.OrdinalIgnoreCase));
            if (selected != null) return FromDevice(selected);

            if (endpoint.IsCurrent)
            {
                var physical = PhyCameraManager.Current?.PhyCameras.FirstOrDefault(camera =>
                    string.Equals(camera.Code, preferredCameraDeviceCode, StringComparison.OrdinalIgnoreCase));
                if (physical != null && endpoint.IsCurrent)
                {
                    return new FovCameraCalibration
                    {
                        CameraDeviceCode = preferredCameraDeviceCode,
                        PhysicalCameraCode = physical.Code
                    };
                }
            }
            return new FovCameraCalibration
            {
                CameraDeviceCode = preferredCameraDeviceCode.Trim()
            };
        }

        private static FovCameraCalibration FromDevice(DeviceCamera camera)
        {
            return new FovCameraCalibration
            {
                CameraDeviceCode = camera.Code,
                PhysicalCameraCode = camera.Config.CameraCode
            };
        }
    }

    internal sealed record LocalFovInputContext
    {
        public int InputMasterId { get; init; } = -1;
        public int InputMasterResultType { get; init; } = -1;
        public int SourceMasterId { get; init; } = -1;
        public int? LightAreaMasterId { get; init; }
        public MeasureResultImgModel? ImageResult { get; init; }
        public string? AlgorithmImageFilePath { get; init; }
        public IReadOnlyList<LuminousAreaPoint>? Corners { get; init; }
    }

    internal sealed record LocalFovNodeResultData
    {
        public int MasterId { get; init; }
        public int MasterResultType { get; init; } = (int)ViewResultAlgType.FOV;
        public int SourceMasterId { get; init; }
        public int? LightAreaMasterId { get; init; }
        public string? ImageFilePath { get; init; }
        public string ResultFilePath { get; init; } = string.Empty;
        public required FovCalculationResult Calculation { get; init; }
        public int TotalTime { get; init; }
    }

    internal sealed record LocalFovPersistenceRequest
    {
        public required MySqlConnectedEndpoint DatabaseEndpoint { get; init; }
        public int BatchId { get; init; }
        public string? ImageFilePath { get; init; }
        public int ZIndex { get; init; }
        public int TotalTime { get; init; }
        public int ResultCode { get; init; }
        public string ResultDescription { get; init; } = "ok";
        public required object Parameters { get; init; }
        public FovMeasurement? Measurement { get; init; }
    }

    internal sealed record LocalFovPersistenceResult
    {
        public int MasterId { get; init; }
        public string ResultFilePath { get; init; } = string.Empty;
    }

    internal sealed record LocalFovPublishRequest
    {
        public required MySqlConnectedEndpoint DatabaseEndpoint { get; init; }
        public required string SerialNumber { get; init; }
        public required string NodeId { get; init; }
        public int ZIndex { get; init; }
        public int MasterId { get; init; }
    }

    internal interface ILocalFovNodeServices
    {
        LocalFlowFrame LoadFrame(string filePath);
        MeasureResultImgModel? GetImageResult(MySqlConnectedEndpoint endpoint, int masterId);
        MeasureResultImgModel? FindImageResult(MySqlConnectedEndpoint endpoint, string? imageFilePath);
        AlgResultMasterModel? GetAlgorithmResult(MySqlConnectedEndpoint endpoint, int masterId);
        IReadOnlyList<LuminousAreaPoint> GetLightAreaCorners(MySqlConnectedEndpoint endpoint, int masterId);
        FovCameraCalibration ResolveCalibration(string? sourceCameraDeviceCode, MySqlConnectedEndpoint endpoint);
        FovCalculationResult DetectAndCalculate(
            HImage image,
            RoiRect roi,
            double fovDist,
            double cameraDegrees,
            IReadOnlyList<LuminousAreaPoint>? coarseCorners,
            FovMeasurementMode mode,
            double minimumConfidence);
        LocalFovPersistenceResult Persist(LocalFovPersistenceRequest request);
        void Publish(LocalFovPublishRequest request);
    }

    internal sealed class LocalFovNodeServices(string? resultDirectory = null) : ILocalFovNodeServices
    {
        public static LocalFovNodeServices Instance { get; } = new();

        public LocalFlowFrame LoadFrame(string filePath) => LocalFrameFileService.Load(filePath);
        public MeasureResultImgModel? GetImageResult(MySqlConnectedEndpoint endpoint, int masterId) =>
            MeasureImgResultDao.ReadResult(endpoint, masterId);
        public MeasureResultImgModel? FindImageResult(MySqlConnectedEndpoint endpoint, string? imageFilePath)
        {
            if (string.IsNullOrWhiteSpace(imageFilePath)) return null;
            try
            {
                string fullPath = Path.GetFullPath(imageFilePath.Trim());
                string fileName = Path.GetFileName(fullPath);
                using SqlSugarClient db = endpoint.CreateDbClient();
                return db.Queryable<MeasureResultImgModel>()
                    .Where(item => item.FileUrl == fullPath || item.RawFile == fullPath || item.RawFile == fileName)
                    .OrderBy(item => item.Id, OrderByType.Desc)
                    .First();
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }
        public AlgResultMasterModel? GetAlgorithmResult(MySqlConnectedEndpoint endpoint, int masterId)
        {
            using var db = endpoint.CreateDbClient();
            return db.Queryable<AlgResultMasterModel>().First(item => item.Id == masterId);
        }
        public IReadOnlyList<LuminousAreaPoint> GetLightAreaCorners(MySqlConnectedEndpoint endpoint, int masterId)
        {
            using var db = endpoint.CreateDbClient();
            return db.Queryable<AlgResultLightAreaModel>()
                .Where(item => item.Pid == masterId)
                .OrderBy(item => item.Id)
                .ToList()
                .Select(item => new LuminousAreaPoint(item.PosX, item.PosY))
                .ToArray();
        }
        public FovCameraCalibration ResolveCalibration(string? sourceCameraDeviceCode, MySqlConnectedEndpoint endpoint) =>
            FovCameraCalibrationResolver.Resolve(sourceCameraDeviceCode, endpoint);
        public FovCalculationResult DetectAndCalculate(
            HImage image,
            RoiRect roi,
            double fovDist,
            double cameraDegrees,
            IReadOnlyList<LuminousAreaPoint>? coarseCorners,
            FovMeasurementMode mode,
            double minimumConfidence) =>
            FovCalculator.DetectAndCalculate(
                image,
                roi,
                fovDist,
                cameraDegrees,
                mode,
                minimumConfidence,
                coarseCorners: coarseCorners);
        public LocalFovPersistenceResult Persist(LocalFovPersistenceRequest request) => LocalFovResultPersistence.Save(request, resultDirectory);
        public void Publish(LocalFovPublishRequest request) => ResultMessageBus.Default.PublishPersisted(
            ResultRoutes.LocalFlow, ResultKinds.Algorithm, string.Empty, "FOV", request.SerialNumber,
            request.NodeId, request.ZIndex, request.MasterId, (int)ViewResultAlgType.FOV, databaseEndpoint: request.DatabaseEndpoint);
    }

    internal static class LocalFovResultPersistence
    {
        internal const string ResultVersion = "2.0";

        public static LocalFovPersistenceResult Save(LocalFovPersistenceRequest request, string? resultDirectory)
        {
            ArgumentNullException.ThrowIfNull(request);
            AlgResultMasterModel master = CreateMasterModel(request);
            if (request.ResultCode != 0)
            {
                using var db = request.DatabaseEndpoint.CreateDbClient();
                int masterId = db.Insertable(master).ExecuteReturnIdentity();
                if (masterId <= 0) throw new InvalidOperationException("保存 FOV 失败主记录失败。");
                return new LocalFovPersistenceResult { MasterId = masterId };
            }

            FovMeasurement measurement = request.Measurement
                ?? throw new InvalidOperationException("成功的本地 FOV 结果缺少测量值。");
            string resultFilePath = WriteResultFile(BuildLegacyResultJson(measurement), resultDirectory);
            try
            {
                int masterId = LocalFlowResultPersistence.SaveAlgorithmResultWithDetailsCore(
                    master, ViewResultAlgType.FOV, id => new[] { CreateDetail(id, resultFilePath) },
                    () => new SqlSugarLocalFlowResultTransaction<DetailCommonModel>(request.DatabaseEndpoint));
                return new LocalFovPersistenceResult { MasterId = masterId, ResultFilePath = resultFilePath };
            }
            catch (Exception saveException)
            {
                try { File.Delete(resultFilePath); }
                catch (Exception cleanupException)
                {
                    throw new AggregateException("保存 FOV 结果失败，且清理未引用的结果文件失败。", saveException, cleanupException);
                }
                throw;
            }
        }

        internal static string BuildLegacyResultJson(FovMeasurement measurement)
        {
            ArgumentNullException.ThrowIfNull(measurement);
            return JsonConvert.SerializeObject(new
            {
                result = new
                {
                    D_Fov = measurement.DiagonalFovDegrees,
                    H_Fov = measurement.HorizontalFovDegrees,
                    V_FOV = measurement.VerticalFovDegrees,
                    clolorVisionH_Fov = measurement.DirectionalHorizontalFovDegrees,
                    clolorVisionV_Fov = measurement.DirectionalVerticalFovDegrees,
                    leftDownToRightUp = measurement.LeftDownToRightUpDegrees,
                    leftUpToRightDown = measurement.LeftUpToRightDownDegrees,
                    message = "Success"
                }
            }, Formatting.Indented);
        }

        internal static AlgResultMasterModel CreateMasterModel(LocalFovPersistenceRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.BatchId <= 0) throw new ArgumentOutOfRangeException(nameof(request), request.BatchId, "BatchId 必须大于 0。");
            ArgumentException.ThrowIfNullOrWhiteSpace(request.ResultDescription);
            return new AlgResultMasterModel
            {
                TId = null,
                TName = "LocalFOV",
                ImgFile = string.IsNullOrWhiteSpace(request.ImageFilePath) ? null : request.ImageFilePath,
                ImgFileType = ViewResultAlgType.FOV,
                version = ResultVersion,
                BatchId = request.BatchId,
                Zindex = request.ZIndex,
                Params = JsonConvert.SerializeObject(request.Parameters),
                DeviceCode = null,
                ResultCode = request.ResultCode,
                Result = request.ResultDescription,
                TotalTime = request.TotalTime,
                CreateDate = DateTime.Now
            };
        }

        internal static DetailCommonModel CreateDetail(int masterId, string resultFilePath)
        {
            if (masterId <= 0) throw new ArgumentOutOfRangeException(nameof(masterId));
            ArgumentException.ThrowIfNullOrWhiteSpace(resultFilePath);
            return new DetailCommonModel
            {
                PId = masterId,
                ResultJson = JsonConvert.SerializeObject(new ResultFile { ResultFileName = resultFilePath })
            };
        }

        private static string WriteResultFile(string resultJson, string? directory)
        {
            directory ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ColorVision", "Results", "FOV");
            Directory.CreateDirectory(directory);
            string resultFilePath = Path.Combine(directory, $"FOV_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.json");
            string temporaryFilePath = resultFilePath + ".tmp";
            try
            {
                using (FileStream stream = new(temporaryFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new(stream, new UTF8Encoding(false)))
                    writer.Write(resultJson);
                File.Move(temporaryFilePath, resultFilePath);
                return resultFilePath;
            }
            catch
            {
                File.Delete(temporaryFilePath);
                throw;
            }
        }
    }

    [STNode("Flow_LocalAlgorithms", "FOV计算", CategoryOrder = 9910, NodeOrder = 20)]
    [FlowEngineLib.FlowNodeDocumentation("将发光区的像素跨度按相机标定换算为水平、垂直和对角视场角。",
        Usage = "设置配对的 FovDist 与 cameraDegrees。默认四角模式可直接接发光区结果，轮廓模式必须提供同源图像像素。",
        Notes = "四角与轮廓模式的跨度口径不同。结果为几何视场，不能替代亮度或对比度限定的有效视场；无效标定会失败，轮廓模式还会拒绝裁切和可分辨孔洞。",
        TopicId = "algorithms.fov-local")]
    public sealed class LocalFovNode : LocalFlowNodeBase
    {
        internal const int CalculationFailureResultCode = -1;
        private readonly ILocalFovNodeServices services;
        private double fovDist = 9410;
        private double cameraDegrees = 74.2;
        private double minimumConfidence = LocalFindLuminousAreaNode.DefaultMinimumConfidence;
        private FovMeasurementMode measurementMode;
        private double luminanceBoundaryRatio = FovLuminousAreaDetector.DefaultBoundaryRatio;

        [Category("FOV")]
        [STNodeProperty("FovDist", "FOV 计算系数，默认 9410；应与当前相机及镜头标定一致。", true)]
        public double FovDist
        {
            get => fovDist;
            set { fovDist = value; OnPropertyChanged(); }
        }

        [Category("FOV")]
        [STNodeProperty("cameraDegrees", "相机镜头有效像素范围对应的标定角度，默认 74.2。", true)]
        [PropertyEditorType(typeof(CameraDegreesPropertiesEditor))]
        public double CameraDegrees
        {
            get => cameraDegrees;
            set { cameraDegrees = value; OnPropertyChanged(); }
        }

        [Category("FOV")]
        [STNodeProperty("测量模式", "默认四角模式保持原有计算；轮廓模式独立读图，支持曲边及非四边形，测量中心水平、垂直与斜向跨度。相机角度参数不变。", true)]
        public FovMeasurementMode MeasurementMode
        {
            get => measurementMode;
            set { measurementMode = value; OnPropertyChanged(); }
        }

        // Preserve the four-corner threshold stored by older flows.
        [Browsable(false)]
        [STNodeProperty("最低可信度", "旧流程保存的四角自动定位门槛。", true)]
        public double MinimumConfidence
        {
            get => minimumConfidence;
            set
            {
                minimumConfidence = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : LocalFindLuminousAreaNode.DefaultMinimumConfidence;
                OnPropertyChanged();
            }
        }

        // Kept for old serialized nodes; no longer a geometric FOV setting.
        [Browsable(false)]
        [STNodeProperty("亮度边界比例", "旧配置兼容字段，不参与几何 FOV 计算。", true)]
        public double LuminanceBoundaryRatio
        {
            get => luminanceBoundaryRatio;
            set { luminanceBoundaryRatio = value; OnPropertyChanged(); }
        }

        public LocalFovNode() : this(LocalFovNodeServices.Instance)
        {
        }

        internal LocalFovNode(ILocalFovNodeServices services)
            : base("FOV计算", "LocalFOV", "FOV")
        {
            this.services = services ?? throw new ArgumentNullException(nameof(services));
        }

        protected override string GetCompactSummaryValue() => $"{FovDist:0.##}";

        protected override IReadOnlyList<string> GetCompactSummaryLines() =>
        [
            GetCompactSummaryValue(),
            $"{CameraDegrees:0.##}°"
        ];

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action) =>
            new() { Data = ExecuteSynchronously(action) };

        internal LocalFovNodeResultData ExecuteSynchronously(CVStartCFC action)
        {
            ArgumentNullException.ThrowIfNull(action);
            MySqlConnectedEndpoint? endpoint = LocalFlowResultPersistence.ResolveEndpoint(action);
            double configuredFovDist = FovDist;
            double selectedCameraDegrees = CameraDegrees;
            double selectedMinimumConfidence = MinimumConfidence;
            FovMeasurementMode selectedMode = MeasurementMode;
            FovCalculator.ValidateCalibration(configuredFovDist, selectedCameraDegrees);
            LocalFovInputContext input = ResolveInput(action, selectedMode, endpoint);
            LocalFlowFrame? ownedFrame = null;
            LocalFlowFrameLease? lease = null;
            string? imageFile = null;
            try
            {
                string? frameCameraDeviceCode = null;
                int frameMasterId = -1;
                Guid? frameId = null;
                if (input.Corners == null || selectedMode == FovMeasurementMode.CurvedBoundary)
                {
                    LocalFlowFrame frame = ResolveFrame(action, input, out ownedFrame, out imageFile,
                        requireSourceMatch: input.LightAreaMasterId != null);
                    lease = frame.Acquire();
                    if (!lease.IsFlipApplied)
                        throw new InvalidOperationException("当前图像的方向变换尚未完成，无法计算 FOV。");
                    frameCameraDeviceCode = lease.Metadata.DeviceCode;
                    frameMasterId = lease.MasterId;
                    frameId = lease.FrameId;
                }
                else
                {
                    // A persisted luminous-area result is sufficient. Do not require
                    // pixels or load an unrelated current frame to re-detect its edges.
                    imageFile = ResolveImageFile(input);
                }

                string? sourceCameraDeviceCode = FirstNonEmpty(frameCameraDeviceCode, input.ImageResult?.DeviceCode);
                MySqlConnectedEndpoint database = RequireEndpoint(endpoint);
                FovCameraCalibration calibration = FlowNodeTiming.Run("ResolveCalibration", () => services.ResolveCalibration(sourceCameraDeviceCode, database));
                double selectedFovDist = configuredFovDist;
                int batchId = LocalFlowResultPersistence.ResolveBatchId(action, database);
                int zIndex = ZIndex;
                string nodeId = NodeID;

                Stopwatch stopwatch = Stopwatch.StartNew();
                FovCalculationResult calculation;
                try
                {
                    calculation = FlowNodeTiming.Run("Algorithm", () => services.DetectAndCalculate(
                        lease == null ? new HImage() : LocalFindLuminousAreaNode.CreateBorrowedImage(lease),
                        new RoiRect(),
                        selectedFovDist,
                        selectedCameraDegrees,
                        input.Corners,
                        selectedMode,
                        selectedMinimumConfidence));
                    stopwatch.Stop();
                }
                catch (Exception calculationException) when (calculationException is InvalidOperationException or ArgumentException)
                {
                    stopwatch.Stop();
                    int failedTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                    int sourceMasterId = input.SourceMasterId > 0 ? input.SourceMasterId : frameMasterId;
                    object failedParameters = BuildParameters(input, sourceMasterId, frameId, imageFile, calibration,
                        selectedFovDist, selectedCameraDegrees, selectedMode, selectedMinimumConfidence, null, calculationException.Message);
                    LocalFovPersistenceResult failed = FlowNodeTiming.Run("PersistResult", () => services.Persist(new LocalFovPersistenceRequest
                    {
                        DatabaseEndpoint = database,
                        BatchId = batchId,
                        ImageFilePath = imageFile,
                        ZIndex = zIndex,
                        TotalTime = failedTime,
                        ResultCode = CalculationFailureResultCode,
                        ResultDescription = calculationException.Message,
                        Parameters = failedParameters
                    }));
                    FlowNodeTiming.Run("PublishResult", () => services.Publish(new LocalFovPublishRequest
                    {
                        DatabaseEndpoint = database,
                        SerialNumber = action.SerialNumber,
                        NodeId = nodeId,
                        ZIndex = zIndex,
                        MasterId = failed.MasterId
                    }));
                    throw new InvalidOperationException(calculationException.Message, calculationException);
                }

                int totalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                int successfulSourceMasterId = input.SourceMasterId > 0 ? input.SourceMasterId : frameMasterId;
                object parameters = BuildParameters(input, successfulSourceMasterId, frameId, imageFile, calibration,
                    selectedFovDist, selectedCameraDegrees, selectedMode, selectedMinimumConfidence, calculation, null);
                LocalFovPersistenceResult persisted = FlowNodeTiming.Run("PersistResult", () => services.Persist(new LocalFovPersistenceRequest
                {
                    DatabaseEndpoint = database,
                    BatchId = batchId,
                    ImageFilePath = imageFile,
                    ZIndex = zIndex,
                    TotalTime = totalTime,
                    Parameters = parameters,
                    Measurement = calculation.Measurement
                }));
                if (persisted.MasterId <= 0 || string.IsNullOrWhiteSpace(persisted.ResultFilePath))
                    throw new InvalidOperationException("FOV 持久化未返回有效主表 ID 和结果文件。");

                if (ownedFrame != null)
                {
                    action.SetCurrentFrame(ownedFrame);
                    ownedFrame = null;
                }
                action.Data["LocalFovMeasurement"] = calculation.Measurement;
                action.Data["LocalFovCorners"] = calculation.Measurement.Corners;
                action.Data["LocalFovResultFile"] = persisted.ResultFilePath;
                action.MasterValue(null, persisted.MasterId, (int)ViewResultAlgType.FOV);
                FlowNodeTiming.Run("PublishResult", () => services.Publish(new LocalFovPublishRequest
                {
                    DatabaseEndpoint = database,
                    SerialNumber = action.SerialNumber,
                    NodeId = nodeId,
                    ZIndex = zIndex,
                    MasterId = persisted.MasterId
                }));
                return new LocalFovNodeResultData
                {
                    MasterId = persisted.MasterId,
                    SourceMasterId = successfulSourceMasterId,
                    LightAreaMasterId = input.LightAreaMasterId,
                    ImageFilePath = imageFile,
                    ResultFilePath = persisted.ResultFilePath,
                    Calculation = calculation,
                    TotalTime = totalTime
                };
            }
            finally
            {
                lease?.Dispose();
                ownedFrame?.Dispose();
            }
        }

        protected override string BuildRunPayload(CVStartCFC action) => JsonConvert.SerializeObject(new
        {
            ServiceName = NodeName,
            EventName = OperatorCode,
            action.SerialNumber,
            FovDist,
            cameraDegrees = CameraDegrees,
            MinimumConfidence = MeasurementMode == FovMeasurementMode.FourCorners ? MinimumConfidence : (double?)null,
            MeasurementMode = MeasurementMode.ToString(),
            Localization = MeasurementMode == FovMeasurementMode.CurvedBoundary ? "CurvedBoundary" : "GeometricCorners"
        });

        internal LocalFovInputContext ResolveInput(CVStartCFC action, FovMeasurementMode mode, MySqlConnectedEndpoint? endpoint)
        {
            bool hasInput = TryGetInputMasterResult(action, 0, out int masterId, out int masterResultType, out _);
            if (!hasInput)
            {
                masterId = ReadActionInt(action, "MasterId");
                masterResultType = ReadActionInt(action, "MasterResultType");
            }
            if (masterId <= 0) return new LocalFovInputContext();

            if (masterResultType is (int)CVCommCore.CVResultType.Camera_Img or (int)CVCommCore.CVResultType.Algorithm_Calibration)
            {
                MySqlConnectedEndpoint database = RequireEndpoint(endpoint);
                MeasureResultImgModel imageResult = FlowNodeTiming.Run("ResolveImageResult", () => services.GetImageResult(database, masterId))
                    ?? throw new InvalidOperationException($"找不到 IN 图像结果：MasterId={masterId}。");
                return new LocalFovInputContext
                {
                    InputMasterId = masterId,
                    InputMasterResultType = masterResultType,
                    SourceMasterId = masterId,
                    ImageResult = imageResult
                };
            }

            if (masterResultType == (int)ViewResultAlgType.FindLightArea)
            {
                MySqlConnectedEndpoint database = RequireEndpoint(endpoint);
                AlgResultMasterModel areaResult = FlowNodeTiming.Run("ResolveAlgorithmResult", () => services.GetAlgorithmResult(database, masterId))
                    ?? throw new InvalidOperationException($"找不到 IN 发光区结果：MasterId={masterId}。");
                IReadOnlyList<LuminousAreaPoint>? corners = mode == FovMeasurementMode.CurvedBoundary
                    ? null : FlowNodeTiming.Run("LoadCorners", () => services.GetLightAreaCorners(database, masterId));
                if (corners != null && corners.Count != 4)
                    throw new InvalidOperationException($"IN 发光区结果必须包含 4 个角点，当前为 {corners.Count} 个。");
                int sourceMasterId = ReadSourceMasterId(areaResult.Params);
                MeasureResultImgModel? imageResult = sourceMasterId > 0
                    ? FlowNodeTiming.Run("ResolveImageResult", () => services.GetImageResult(database, sourceMasterId))
                    : FlowNodeTiming.Run("ResolveImageResult", () => services.FindImageResult(database, areaResult.ImgFile));
                return new LocalFovInputContext
                {
                    InputMasterId = masterId,
                    InputMasterResultType = masterResultType,
                    SourceMasterId = sourceMasterId > 0 ? sourceMasterId : imageResult?.Id ?? -1,
                    LightAreaMasterId = masterId,
                    ImageResult = imageResult,
                    AlgorithmImageFilePath = areaResult.ImgFile,
                    Corners = corners
                };
            }

            throw new InvalidOperationException(
                $"IN 不是图像或发光区结果：MasterId={masterId}，ResultType={masterResultType}。");
        }

        private static MySqlConnectedEndpoint RequireEndpoint(MySqlConnectedEndpoint? endpoint) =>
            endpoint ?? throw new InvalidOperationException("本地 FOV 流程需要连接 MySQL。");

        private LocalFlowFrame ResolveFrame(
            CVStartCFC action,
            LocalFovInputContext input,
            out LocalFlowFrame? ownedFrame,
            out string? imageFile,
            bool requireSourceMatch = false)
        {
            ownedFrame = null;
            imageFile = null;
            if (action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame) && currentFrame != null
                && (!requireSourceMatch || (input.SourceMasterId > 0
                    ? currentFrame.MasterId == input.SourceMasterId
                    : ResolveImageFile(input) is string sourcePath
                        && string.Equals(sourcePath, currentFrame.ResolveResultImageFilePath(), StringComparison.OrdinalIgnoreCase))))
            {
                FlowNodeTiming.Skip("OpenImage");
                imageFile = currentFrame.ResolveResultImageFilePath();
                return currentFrame;
            }

            string? fallbackFile = ResolveImageFile(input);
            if (fallbackFile == null)
                throw new InvalidOperationException("流程中没有可用图像；请连接本地图像、取图、校正或发光区定位节点。");
            ownedFrame = FlowNodeTiming.Run("OpenImage", () =>
            {
                if (!File.Exists(fallbackFile)) throw new FileNotFoundException("FOV 图像文件不存在。", fallbackFile);
                return services.LoadFrame(fallbackFile);
            });
            if (input.SourceMasterId > 0) ownedFrame.MasterId = input.SourceMasterId;
            imageFile = fallbackFile;
            return ownedFrame;
        }

        private static string? ResolveImageFile(LocalFovInputContext input)
        {
            foreach (string? candidate in new[] { input.ImageResult?.FileUrl, input.ImageResult?.RawFile, input.AlgorithmImageFilePath })
            {
                string? path = NormalizeExistingPath(candidate);
                if (path != null) return path;
            }
            return null;
        }

        private static object BuildParameters(
            LocalFovInputContext input,
            int sourceMasterId,
            Guid? frameId,
            string? imageFile,
            FovCameraCalibration calibration,
            double fovDist,
            double cameraDegrees,
            FovMeasurementMode mode,
            double minimumConfidence,
            FovCalculationResult? calculation,
            string? failureReason)
        {
            return new
            {
                Algorithm = "LocalFOV2",
                Formula = "2*atan((pixelDistance/FovDist)*tan(cameraDegrees/2))*180/PI",
                FovDist = fovDist,
                cameraDegrees,
                MinimumConfidence = mode == FovMeasurementMode.FourCorners ? minimumConfidence : (double?)null,
                MeasurementMode = mode.ToString(),
                BoundaryMode = calculation?.BoundaryMode ?? (mode == FovMeasurementMode.CurvedBoundary
                    ? "CurvedBoundary" : input.Corners != null ? "UpstreamCorners" : "RobustV2"),
                Boundary = calculation?.Measurement.Boundary,
                CameraCalibration = new
                {
                    calibration.CameraDeviceCode,
                    calibration.PhysicalCameraCode
                },
                SourceMasterId = sourceMasterId,
                input.LightAreaMasterId,
                FrameId = frameId?.ToString("N"),
                ImageFilePath = imageFile,
                Localization = calculation == null ? null : new
                {
                    UsedUpstreamCorners = calculation.UsedProvidedCorners,
                    Algorithm = calculation.Detection?.Algorithm,
                    calculation.Detection?.Confidence,
                    calculation.Detection?.SideQuality,
                    calculation.Detection?.Warnings,
                    CoarseAlgorithm = calculation.CoarseDetection?.Algorithm ?? (calculation.UsedProvidedCorners ? "UpstreamFindLightArea" : null),
                    CoarseConfidence = calculation.CoarseDetection?.Confidence
                },
                Corners = calculation?.Measurement.Corners.Select((point, index) => new
                {
                    Name = new[] { "LT", "RT", "RB", "LB" }[index],
                    point.X,
                    point.Y
                }),
                FailureReason = failureReason
            };
        }

        private static int ReadSourceMasterId(string? parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters)) return -1;
            try { return JObject.Parse(parameters).Value<int?>("SourceMasterId") ?? -1; }
            catch (JsonException) { return -1; }
        }

        private static int ReadActionInt(CVStartCFC action, string key)
        {
            if (!action.Data.TryGetValue(key, out object? value) || value == null) return -1;
            try { return Convert.ToInt32(value); }
            catch { return -1; }
        }

        private static string? NormalizeExistingPath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            try
            {
                string fullPath = Path.GetFullPath(value.Trim());
                return File.Exists(fullPath) ? fullPath : null;
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }

        private static string? FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
