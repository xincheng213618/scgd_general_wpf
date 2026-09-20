using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.FindLightArea;
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
        public static FovCameraCalibration Resolve(string? preferredCameraDeviceCode)
        {
            if (string.IsNullOrWhiteSpace(preferredCameraDeviceCode))
                return new FovCameraCalibration { CameraDeviceCode = string.Empty };

            DeviceCamera[] deviceCameras = ServiceManager.Current?.DeviceServices.OfType<DeviceCamera>().ToArray()
                ?? Array.Empty<DeviceCamera>();
            if (!string.IsNullOrWhiteSpace(preferredCameraDeviceCode))
            {
                DeviceCamera? selected = deviceCameras.FirstOrDefault(camera =>
                    string.Equals(camera.Code, preferredCameraDeviceCode, StringComparison.OrdinalIgnoreCase));
                if (selected != null) return FromDevice(selected);

                selected = deviceCameras.FirstOrDefault(camera =>
                    string.Equals(camera.Config.CameraCode, preferredCameraDeviceCode, StringComparison.OrdinalIgnoreCase));
                if (selected != null) return FromDevice(selected);
            }

            var physicalCameras = PhyCameraManager.GetInstance().PhyCameras.ToArray();
            if (!string.IsNullOrWhiteSpace(preferredCameraDeviceCode))
            {
                var physical = physicalCameras.FirstOrDefault(camera =>
                    string.Equals(camera.Code, preferredCameraDeviceCode, StringComparison.OrdinalIgnoreCase));
                if (physical != null)
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
                CameraDeviceCode = preferredCameraDeviceCode?.Trim() ?? string.Empty
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
        public required string SerialNumber { get; init; }
        public required string NodeId { get; init; }
        public int ZIndex { get; init; }
        public int MasterId { get; init; }
    }

    internal interface ILocalFovNodeServices
    {
        LocalFlowFrame LoadFrame(string filePath);
        MeasureResultImgModel? GetImageResult(int masterId);
        MeasureResultImgModel? FindImageResult(string? imageFilePath);
        AlgResultMasterModel? GetAlgorithmResult(int masterId);
        IReadOnlyList<LuminousAreaPoint> GetLightAreaCorners(int masterId);
        FovCameraCalibration ResolveCalibration(string? sourceCameraDeviceCode);
        FovCalculationResult DetectAndCalculate(
            HImage image,
            RoiRect roi,
            double fovDist,
            double cameraDegrees,
            IReadOnlyList<LuminousAreaPoint>? coarseCorners);
        LocalFovPersistenceResult Persist(LocalFovPersistenceRequest request);
        void Publish(LocalFovPublishRequest request);
    }

    internal sealed class LocalFovNodeServices : ILocalFovNodeServices
    {
        public static LocalFovNodeServices Instance { get; } = new();

        public LocalFlowFrame LoadFrame(string filePath) => LocalFrameFileService.Load(filePath);
        public MeasureResultImgModel? GetImageResult(int masterId) => MeasureImgResultDao.Instance.GetById(masterId);
        public MeasureResultImgModel? FindImageResult(string? imageFilePath)
        {
            if (string.IsNullOrWhiteSpace(imageFilePath) || !MySqlControl.GetInstance().IsConnect) return null;
            try
            {
                string fullPath = Path.GetFullPath(imageFilePath.Trim());
                string fileName = Path.GetFileName(fullPath);
                using SqlSugarClient db = MySqlControl.CreateDbClient();
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
        public AlgResultMasterModel? GetAlgorithmResult(int masterId) => AlgResultMasterDao.Instance.GetById(masterId);
        public IReadOnlyList<LuminousAreaPoint> GetLightAreaCorners(int masterId) =>
            AlgResultLightAreaDao.Instance.GetAllByPid(masterId)
                .OrderBy(item => item.Id)
                .Select(item => new LuminousAreaPoint(item.PosX, item.PosY))
                .ToArray();
        public FovCameraCalibration ResolveCalibration(string? sourceCameraDeviceCode) =>
            FovCameraCalibrationResolver.Resolve(sourceCameraDeviceCode);
        public FovCalculationResult DetectAndCalculate(
            HImage image,
            RoiRect roi,
            double fovDist,
            double cameraDegrees,
            IReadOnlyList<LuminousAreaPoint>? coarseCorners) =>
            FovCalculator.DetectAndCalculate(
                image,
                roi,
                fovDist,
                cameraDegrees,
                LocalFindLuminousAreaNode.DefaultMinimumConfidence,
                coarseCorners: coarseCorners);
        public LocalFovPersistenceResult Persist(LocalFovPersistenceRequest request) => LocalFovResultPersistence.Save(request);
        public void Publish(LocalFovPublishRequest request) => ResultMessageBus.Default.PublishPersisted(
            ResultRoutes.LocalFlow, ResultKinds.Algorithm, string.Empty, "FOV", request.SerialNumber,
            request.NodeId, request.ZIndex, request.MasterId, (int)ViewResultAlgType.FOV);
    }

    internal static class LocalFovResultPersistence
    {
        internal const string ResultVersion = "2.0";

        public static LocalFovPersistenceResult Save(LocalFovPersistenceRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            AlgResultMasterModel master = CreateMasterModel(request);
            if (request.ResultCode != 0)
                return new LocalFovPersistenceResult { MasterId = SaveMasterOnlyCore(master, AlgResultMasterDao.Instance.SaveAndReturnId) };

            FovMeasurement measurement = request.Measurement
                ?? throw new InvalidOperationException("成功的本地 FOV 结果缺少测量值。");
            string resultFilePath = WriteResultFile(BuildLegacyResultJson(measurement));
            try
            {
                int masterId = SaveDatabaseCore(master, resultFilePath, static () => new SqlSugarLocalFlowResultTransaction<DetailCommonModel>());
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

        internal static int SaveMasterOnlyCore(AlgResultMasterModel master, Func<AlgResultMasterModel, int> saveMaster)
        {
            if (master.ResultCode.GetValueOrDefault() == 0)
                throw new InvalidOperationException("成功的 FOV 结果不能省略结果明细。");
            int masterId = saveMaster(master);
            if (masterId <= 0) throw new InvalidOperationException("保存 FOV 失败主记录失败。");
            return masterId;
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

        internal static int SaveDatabaseCore(
            AlgResultMasterModel master,
            string resultFilePath,
            Func<ILocalFlowResultTransaction<DetailCommonModel>> transactionFactory) =>
            LocalFlowResultPersistence.SaveAlgorithmResultWithDetailsCore(
                master,
                ViewResultAlgType.FOV,
                masterId => new[] { CreateDetail(masterId, resultFilePath) },
                transactionFactory);

        private static string WriteResultFile(string resultJson)
        {
            string directory = Path.Combine(
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

    [STNode("Flow_CustomNodes", "FOV计算")]
    public sealed class LocalFovNode : LocalFlowNodeBase
    {
        internal const int CalculationFailureResultCode = -1;
        private readonly ILocalFovNodeServices services;
        private double fovDist = 9410;
        private double cameraDegrees = 74.2;
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
            double configuredFovDist = FovDist;
            double selectedCameraDegrees = CameraDegrees;
            FovCalculator.ValidateCalibration(configuredFovDist, selectedCameraDegrees);
            LocalFovInputContext input = ResolveInput(action);
            LocalFlowFrame? ownedFrame = null;
            LocalFlowFrameLease? lease = null;
            string? imageFile = null;
            try
            {
                string? frameCameraDeviceCode = null;
                int frameMasterId = -1;
                Guid? frameId = null;
                if (input.Corners == null)
                {
                    LocalFlowFrame frame = ResolveFrame(action, input, out ownedFrame, out imageFile);
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
                FovCameraCalibration calibration = FlowNodeTiming.Run("ResolveCalibration", () => services.ResolveCalibration(sourceCameraDeviceCode));
                double selectedFovDist = configuredFovDist;
                MeasureBatchModel batch = BatchResultMasterDao.Instance.GetByNameOrCode(action.SerialNumber)
                    ?? throw new InvalidOperationException($"找不到流程批次：{action.SerialNumber}");
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
                        input.Corners));
                    stopwatch.Stop();
                }
                catch (Exception calculationException) when (calculationException is InvalidOperationException or ArgumentException)
                {
                    stopwatch.Stop();
                    int failedTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                    int sourceMasterId = input.SourceMasterId > 0 ? input.SourceMasterId : frameMasterId;
                    object failedParameters = BuildParameters(input, sourceMasterId, frameId, imageFile, calibration, selectedFovDist, selectedCameraDegrees, null, calculationException.Message);
                    LocalFovPersistenceResult failed = FlowNodeTiming.Run("PersistResult", () => services.Persist(new LocalFovPersistenceRequest
                    {
                        BatchId = batch.Id,
                        ImageFilePath = imageFile,
                        ZIndex = zIndex,
                        TotalTime = failedTime,
                        ResultCode = CalculationFailureResultCode,
                        ResultDescription = calculationException.Message,
                        Parameters = failedParameters
                    }));
                    FlowNodeTiming.Run("PublishResult", () => services.Publish(new LocalFovPublishRequest
                    {
                        SerialNumber = action.SerialNumber,
                        NodeId = nodeId,
                        ZIndex = zIndex,
                        MasterId = failed.MasterId
                    }));
                    throw new InvalidOperationException(calculationException.Message, calculationException);
                }

                int totalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                int successfulSourceMasterId = input.SourceMasterId > 0 ? input.SourceMasterId : frameMasterId;
                object parameters = BuildParameters(input, successfulSourceMasterId, frameId, imageFile, calibration, selectedFovDist, selectedCameraDegrees, calculation, null);
                LocalFovPersistenceResult persisted = FlowNodeTiming.Run("PersistResult", () => services.Persist(new LocalFovPersistenceRequest
                {
                    BatchId = batch.Id,
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
            Localization = "GeometricCorners"
        });

        private LocalFovInputContext ResolveInput(CVStartCFC action)
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
                MeasureResultImgModel imageResult = FlowNodeTiming.Run("ResolveImageResult", () => services.GetImageResult(masterId))
                    ?? throw new InvalidOperationException($"找不到 IN 图像结果：MasterId={masterId}。");
                return new LocalFovInputContext
                {
                    InputMasterId = masterId,
                    InputMasterResultType = masterResultType,
                    SourceMasterId = masterId,
                    ImageResult = imageResult
                };
            }

            if (masterResultType == (int)ViewResultAlgType.FindLightArea
                || masterResultType == (int)ViewResultAlgType.LightArea)
            {
                AlgResultMasterModel areaResult = FlowNodeTiming.Run("ResolveAlgorithmResult", () => services.GetAlgorithmResult(masterId))
                    ?? throw new InvalidOperationException($"找不到 IN 发光区结果：MasterId={masterId}。");
                IReadOnlyList<LuminousAreaPoint> corners = FlowNodeTiming.Run("LoadCorners", () => services.GetLightAreaCorners(masterId));
                if (corners.Count != 4)
                    throw new InvalidOperationException($"IN 发光区结果必须包含 4 个角点，当前为 {corners.Count} 个。");
                int sourceMasterId = ReadSourceMasterId(areaResult.Params);
                MeasureResultImgModel? imageResult = sourceMasterId > 0
                    ? FlowNodeTiming.Run("ResolveImageResult", () => services.GetImageResult(sourceMasterId))
                    : FlowNodeTiming.Run("ResolveImageResult", () => services.FindImageResult(areaResult.ImgFile));
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

        private LocalFlowFrame ResolveFrame(
            CVStartCFC action,
            LocalFovInputContext input,
            out LocalFlowFrame? ownedFrame,
            out string? imageFile)
        {
            ownedFrame = null;
            imageFile = null;
            if (action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame) && currentFrame != null)
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
            FovCalculationResult? calculation,
            string? failureReason)
        {
            return new
            {
                Algorithm = "LocalFOV2",
                Formula = "2*atan((pixelDistance/FovDist)*tan(cameraDegrees/2))*180/PI",
                FovDist = fovDist,
                cameraDegrees,
                BoundaryMode = calculation?.BoundaryMode ?? (input.Corners != null ? "UpstreamCorners" : "RobustV2"),
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
