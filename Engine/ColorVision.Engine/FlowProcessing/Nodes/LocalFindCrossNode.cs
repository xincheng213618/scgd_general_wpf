using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.Jsons;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using Newtonsoft.Json;
using SqlSugar;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.Nodes
{
    internal sealed class LocalFindCrossNodeItem
    {
        public string Name { get; init; } = string.Empty;
        public double X { get; init; }
        public double Y { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
        public double CenterX { get; init; }
        public double CenterY { get; init; }
        public double RotationAngle { get; init; }
        public double TiltX { get; init; }
        public double TiltY { get; init; }
    }

    internal sealed class LocalFindCrossDetection
    {
        public bool Success { get; init; }
        public IReadOnlyList<LocalFindCrossNodeItem> Items { get; init; } = Array.Empty<LocalFindCrossNodeItem>();
        public object? Diagnostics { get; init; }
        public string FailureReason { get; init; } = string.Empty;
        public string RawJson { get; init; } = string.Empty;
        public int NativeReturnCode { get; init; }
        public string InteropDiagnostic { get; init; } = string.Empty;
    }

    internal sealed class LocalFindCrossNodeResultData
    {
        public int MasterId { get; init; }
        public int MasterResultType { get; init; } = (int)ViewResultAlgType.FindCross;
        public int SourceMasterId { get; init; }
        public string? FrameId { get; init; }
        public string? ImageFilePath { get; init; }
        public string? ResultFilePath { get; init; }
        public LocalFindCrossNodeItem Result { get; init; } = new();
        public object? Diagnostics { get; init; }
        public int TotalTime { get; init; }
    }

    internal sealed class LocalFindCrossPersistenceRequest
    {
        public required CVStartCFC Action { get; init; }
        public string? ImageFilePath { get; init; }
        public required string DeviceCode { get; init; }
        public int ZIndex { get; init; }
        public int TotalTime { get; init; }
        public required object Parameters { get; init; }
        public required LocalFindCrossNodeItem Result { get; init; }
        public string ResultDirectory { get; init; } = string.Empty;
    }

    internal sealed class LocalFindCrossPersistenceResult
    {
        public int MasterId { get; init; }
        public required string ResultFilePath { get; init; }
    }

    internal sealed class LocalFindCrossPublishRequest
    {
        public required string DeviceCode { get; init; }
        public required string OperatorCode { get; init; }
        public required string SerialNumber { get; init; }
        public required string NodeId { get; init; }
        public int ZIndex { get; init; }
        public int MasterId { get; init; }
    }

    internal interface ILocalFindCrossNodeServices
    {
        LocalFlowFrame LoadFrame(string filePath);
        MeasureResultImgModel? GetImageResult(int masterId);
        LocalFindCrossDetection Detect(HImage image, RoiRect roi, string parameterJson);
        LocalFindCrossPersistenceResult Persist(LocalFindCrossPersistenceRequest request);
        void Publish(LocalFindCrossPublishRequest request);
    }

    internal sealed class LocalFindCrossNodeServices : ILocalFindCrossNodeServices
    {
        private static readonly System.Text.Json.JsonSerializerOptions ProductionParameterSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
        };

        public static LocalFindCrossNodeServices Instance { get; } = new();

        private LocalFindCrossNodeServices()
        {
        }

        public LocalFlowFrame LoadFrame(string filePath) => LocalFrameFileService.Load(filePath);

        public MeasureResultImgModel? GetImageResult(int masterId) => MeasureImgResultDao.Instance.GetById(masterId);

        public LocalFindCrossDetection Detect(HImage image, RoiRect roi, string parameterJson)
        {
            if (!TryParseProductionOptions(parameterJson, out FindCrossLocalOptions options, out string error))
            {
                return new LocalFindCrossDetection
                {
                    FailureReason = "InvalidConfiguration",
                    InteropDiagnostic = error
                };
            }

            FindCrossLocalResult result = FindCrossLocal.Run(image, roi, options);
            return new LocalFindCrossDetection
            {
                Success = result.Success,
                Items = result.Items.Select(item => new LocalFindCrossNodeItem
                {
                    Name = item.Name,
                    X = item.X,
                    Y = item.Y,
                    Width = item.Width,
                    Height = item.Height,
                    CenterX = item.Center.X,
                    CenterY = item.Center.Y,
                    RotationAngle = item.RotationAngle,
                    TiltX = item.TiltX,
                    TiltY = item.TiltY
                }).ToArray(),
                Diagnostics = result.Diagnostics,
                FailureReason = result.FailureReason,
                RawJson = result.RawJson,
                NativeReturnCode = result.NativeReturnCode,
                InteropDiagnostic = result.InteropDiagnostic
            };
        }

        internal static bool TryParseProductionOptions(
            string parameterJson,
            out FindCrossLocalOptions options,
            out string error)
        {
            options = new FindCrossLocalOptions();
            if (string.IsNullOrWhiteSpace(parameterJson))
            {
                error = "FindCross production parameter JSON cannot be blank.";
                return false;
            }

            try
            {
                options = System.Text.Json.JsonSerializer.Deserialize<FindCrossLocalOptions>(
                    parameterJson,
                    ProductionParameterSerializerOptions)
                    ?? throw new System.Text.Json.JsonException("FindCross production parameter JSON cannot be null.");
                error = string.Empty;
                return true;
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
            {
                error = ex.Message;
                return false;
            }
        }

        public LocalFindCrossPersistenceResult Persist(LocalFindCrossPersistenceRequest request) =>
            LocalFindCrossResultPersistence.Save(request);

        public void Publish(LocalFindCrossPublishRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ResultMessageBus.Default.PublishPersisted(
                ResultRoutes.Algorithm,
                ResultKinds.Algorithm,
                request.DeviceCode,
                request.OperatorCode,
                request.SerialNumber,
                request.NodeId,
                request.ZIndex,
                request.MasterId,
                (int)ViewResultAlgType.FindCross);
        }
    }

    internal interface ILocalFindCrossResultTransaction : IDisposable
    {
        void Begin();
        int InsertMaster(AlgResultMasterModel model);
        int InsertDetail(DetailCommonModel model);
        void Commit();
        void Rollback();
    }

    internal sealed class SqlSugarLocalFindCrossResultTransaction : ILocalFindCrossResultTransaction
    {
        private readonly SqlSugarClient db = MySqlControl.CreateDbClient();

        public void Begin() => db.Ado.BeginTran();

        public int InsertMaster(AlgResultMasterModel model) => db.Insertable(model).ExecuteReturnIdentity();

        public int InsertDetail(DetailCommonModel model) => db.Insertable(model).ExecuteCommand();

        public void Commit() => db.Ado.CommitTran();

        public void Rollback() => db.Ado.RollbackTran();

        public void Dispose() => db.Dispose();
    }

    internal static class LocalFindCrossResultPersistence
    {
        internal const string ResultVersion = "1.0";

        public static LocalFindCrossPersistenceResult Save(LocalFindCrossPersistenceRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            MeasureBatchModel batch = BatchResultMasterDao.Instance.GetByNameOrCode(request.Action.SerialNumber)
                ?? throw new InvalidOperationException($"找不到流程批次：{request.Action.SerialNumber}");
            string resultFilePath = WriteResultFile(request.ResultDirectory, BuildLegacyResultJson(request.Result));
            try
            {
                AlgResultMasterModel master = CreateMasterModel(request, batch.Id);
                int masterId = SaveDatabaseCore(
                    master,
                    resultFilePath,
                    static () => new SqlSugarLocalFindCrossResultTransaction());
                return new LocalFindCrossPersistenceResult
                {
                    MasterId = masterId,
                    ResultFilePath = resultFilePath
                };
            }
            catch (Exception saveException)
            {
                try
                {
                    File.Delete(resultFilePath);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException("保存本地 FindCross 结果失败，且清理未引用的结果文件失败。", saveException, cleanupException);
                }
                throw;
            }
        }

        internal static string BuildLegacyResultJson(LocalFindCrossNodeItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return JsonConvert.SerializeObject(new
            {
                result = new[]
                {
                    new
                    {
                        center = new
                        {
                            x = ToLegacyCenterCoordinate(item.CenterX, nameof(item.CenterX)),
                            y = ToLegacyCenterCoordinate(item.CenterY, nameof(item.CenterY))
                        },
                        h = item.Height,
                        name = item.Name,
                        rotationAngle = item.RotationAngle,
                        tilt = new { tilt_x = item.TiltX, tilt_y = item.TiltY },
                        w = item.Width,
                        x = item.X,
                        y = item.Y
                    }
                }
            }, Formatting.Indented);
        }

        internal static AlgResultMasterModel CreateMasterModel(LocalFindCrossPersistenceRequest request, int batchId)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (batchId <= 0) throw new ArgumentOutOfRangeException(nameof(batchId), "流程批次 ID 无效。");
            return new AlgResultMasterModel
            {
                TId = null,
                TName = "LocalFindCross",
                ImgFile = NullIfWhiteSpace(request.ImageFilePath),
                ImgFileType = ViewResultAlgType.FindCross,
                version = ResultVersion,
                BatchId = batchId,
                Zindex = request.ZIndex,
                Params = JsonConvert.SerializeObject(request.Parameters),
                DeviceCode = NullIfWhiteSpace(request.DeviceCode),
                ResultCode = 0,
                Result = "ok",
                TotalTime = request.TotalTime,
                CreateDate = DateTime.Now
            };
        }

        internal static DetailCommonModel CreateDetail(int masterId, string resultFilePath)
        {
            if (masterId <= 0) throw new ArgumentOutOfRangeException(nameof(masterId), "FindCross 结果主表 ID 无效。");
            if (string.IsNullOrWhiteSpace(resultFilePath)) throw new ArgumentException("FindCross 结果文件路径不能为空。", nameof(resultFilePath));
            return new DetailCommonModel
            {
                PId = masterId,
                ResultJson = JsonConvert.SerializeObject(new ResultFile { ResultFileName = resultFilePath })
            };
        }

        internal static int SaveDatabaseCore(
            AlgResultMasterModel master,
            string resultFilePath,
            Func<ILocalFindCrossResultTransaction> transactionFactory)
        {
            ArgumentNullException.ThrowIfNull(master);
            ArgumentException.ThrowIfNullOrWhiteSpace(resultFilePath);
            ArgumentNullException.ThrowIfNull(transactionFactory);
            using ILocalFindCrossResultTransaction transaction = transactionFactory()
                ?? throw new InvalidOperationException("无法创建本地 FindCross 结果数据库事务。");
            transaction.Begin();
            try
            {
                int masterId = transaction.InsertMaster(master);
                if (masterId <= 0)
                    throw new InvalidOperationException("保存本地 FindCross 结果主表失败。");
                int inserted = transaction.InsertDetail(CreateDetail(masterId, resultFilePath));
                if (inserted != 1)
                    throw new InvalidOperationException($"保存本地 FindCross 结果明细失败：应写入 1 条，实际写入 {inserted} 条。");
                transaction.Commit();
                return masterId;
            }
            catch (Exception saveException)
            {
                try
                {
                    transaction.Rollback();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException("保存本地 FindCross 结果失败，且数据库事务回滚失败。", saveException, rollbackException);
                }
                throw;
            }
        }

        private static string WriteResultFile(string configuredDirectory, string resultJson)
        {
            string directory = string.IsNullOrWhiteSpace(configuredDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "Results", "FindCross")
                : NormalizeDirectory(configuredDirectory);
            Directory.CreateDirectory(directory);
            string resultFilePath = Path.Combine(
                directory,
                $"FindCross_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.json");
            string temporaryFilePath = resultFilePath + ".tmp";
            try
            {
                using (FileStream stream = new(temporaryFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new(stream, new UTF8Encoding(false)))
                {
                    writer.Write(resultJson);
                }
                File.Move(temporaryFilePath, resultFilePath);
                return resultFilePath;
            }
            catch
            {
                File.Delete(temporaryFilePath);
                throw;
            }
        }

        private static string NormalizeDirectory(string value)
        {
            try
            {
                return Path.GetFullPath(value.Trim());
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new InvalidOperationException($"FindCross 结果目录无效：{value}", ex);
            }
        }

        private static string? NullIfWhiteSpace(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;

        private static int ToLegacyCenterCoordinate(double value, string name)
        {
            if (!double.IsFinite(value) || value < int.MinValue || value > int.MaxValue)
                throw new InvalidOperationException($"FindCross {name} 无法写入 1.0 结果文件：{value}。");
            return checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
        }
    }

    [STNode("Flow_CustomNodes", "本地十字定位")]
    public sealed class LocalFindCrossNode : LocalFlowNodeBase
    {
        internal const string DefaultParameterJson = """
            {
              "ExpectedAngleDegrees": 0,
              "AngleToleranceDegrees": 10,
              "opticsParams": {
                "focusLength": 25.4,
                "sensorPixSize": 3.76
              }
            }
            """;

        private string imageFilePath = string.Empty;
        private string parameterJson = DefaultParameterJson;
        private string resultDirectory = string.Empty;
        private Int32Rect searchRegion = Int32Rect.Empty;
        private readonly ILocalFindCrossNodeServices services;

        [Category("本地十字定位")]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        [STNodeProperty("图像文件", "可选后备输入；优先使用上游本地内存帧，没有内存帧时先读该文件，再回退到 IN 图像结果。需要历史结果底图时，请让上游本地相机/校正节点保存实际帧文件。", true)]
        public string ImageFilePath
        {
            get => imageFilePath;
            set
            {
                imageFilePath = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        [Category("本地十字定位")]
        [STNodeProperty("算法参数(JSON)", "生产参数只需名义角度、最大允许旋转偏差和光学校准；极性、阈值、臂长、可信度、处理尺寸及旋转算法均由内部鲁棒配置管理。省略 stdCenter 时以整幅图像中心作为光学基准。", true)]
        public string ParameterJson
        {
            get => parameterJson;
            set
            {
                parameterJson = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        [Category("本地十字定位")]
        [STNodeProperty("搜索区域", "可选 ROI（X,Y,Width,Height）；默认 0,0,0,0 为通用整幅图像模式。现场 G0941 复现需设为 2888,1920,3751,2655", true, DescriptorType = typeof(Int32RectNodePropertyDescriptor))]
        public Int32Rect SearchRegion
        {
            get => searchRegion;
            set
            {
                searchRegion = value;
                OnPropertyChanged();
            }
        }

        [Category("本地十字定位")]
        [STNodeProperty("结果目录", "可选；留空时保存到当前用户 LocalAppData 下的 ColorVision\\Results\\FindCross", true)]
        public string ResultDirectory
        {
            get => resultDirectory;
            set
            {
                resultDirectory = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public LocalFindCrossNode() : this(LocalFindCrossNodeServices.Instance)
        {
        }

        internal LocalFindCrossNode(ILocalFindCrossNodeServices services)
            : base("本地十字定位", "LocalFindCross", "FindCross")
        {
            this.services = services ?? throw new ArgumentNullException(nameof(services));
            SelectFirstAvailableDevice<DeviceAlgorithm>();
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action) =>
            new() { Data = ExecuteSynchronously(action) };

        internal LocalFindCrossNodeResultData ExecuteSynchronously(CVStartCFC action)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (string.IsNullOrWhiteSpace(ParameterJson))
                throw new InvalidOperationException("FindCross 算法参数 JSON 不能为空。");

            LocalFlowFrame? ownedFrame = null;
            LocalFlowFrame frame = ResolveFrame(action, out ownedFrame, out string? imageFile);
            bool loadedFromFile = ownedFrame != null;
            try
            {
                using LocalFlowFrameLease lease = frame.Acquire();
                if (!lease.IsFlipApplied)
                    throw new InvalidOperationException("当前图像的方向变换尚未完成，无法生成可供后续映射使用的十字中心结果。");

                RoiRect roi = LocalFindLuminousAreaNode.ResolveRoi(SearchRegion, lease.Metadata.Width, lease.Metadata.Height);
                HImage image = LocalFindLuminousAreaNode.CreateBorrowedImage(lease);
                Stopwatch stopwatch = Stopwatch.StartNew();
                LocalFindCrossDetection detection = services.Detect(image, roi, ParameterJson);
                stopwatch.Stop();
                LocalFindCrossNodeItem result = ValidateDetection(detection, lease.Metadata.Width, lease.Metadata.Height);
                int totalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                string algorithmDeviceCode = ResolveAvailableDeviceCode<DeviceAlgorithm>();
                object parameters = new
                {
                    Algorithm = "LocalFindCross",
                    SourceMasterId = lease.MasterId,
                    FrameId = lease.FrameId.ToString("N"),
                    ImageFilePath = imageFile,
                    SearchRegion = new { roi.X, roi.Y, roi.Width, roi.Height },
                    ParameterJson,
                    Result = result,
                    detection.Diagnostics,
                    detection.NativeReturnCode,
                    RawJson = NullIfWhiteSpace(detection.RawJson),
                    InteropDiagnostic = NullIfWhiteSpace(detection.InteropDiagnostic),
                    PrimaryBufferKind = lease.Metadata.PrimaryBufferKind.ToString(),
                    SourceFilePath = NullIfWhiteSpace(lease.Metadata.SourceFilePath),
                    CvRawFilePath = NullIfWhiteSpace(frame.CvRawFilePath),
                    CvCieFilePath = NullIfWhiteSpace(frame.CvCieFilePath),
                    CalibrationTemplate = NullIfWhiteSpace(lease.Metadata.CalibrationTemplate),
                    FlipMode = lease.Metadata.FlipMode.ToString(),
                    FlipApplied = lease.IsFlipApplied,
                    ImageRead = loadedFromFile,
                    MemoryOnly = string.IsNullOrWhiteSpace(imageFile)
                };
                LocalFindCrossPersistenceResult persisted = services.Persist(new LocalFindCrossPersistenceRequest
                {
                    Action = action,
                    ImageFilePath = imageFile,
                    DeviceCode = algorithmDeviceCode,
                    ZIndex = ZIndex,
                    TotalTime = totalTime,
                    Parameters = parameters,
                    Result = result,
                    ResultDirectory = ResultDirectory
                });
                if (persisted.MasterId <= 0)
                    throw new InvalidOperationException("本地 FindCross 持久化返回了无效主表 ID。");
                if (string.IsNullOrWhiteSpace(persisted.ResultFilePath))
                    throw new InvalidOperationException("本地 FindCross 持久化未返回结果文件路径。");

                if (ownedFrame != null)
                {
                    action.SetCurrentFrame(ownedFrame);
                    ownedFrame = null;
                }
                action.Data["LocalFindCrossResult"] = result;
                action.Data["LocalFindCrossCenterX"] = result.CenterX;
                action.Data["LocalFindCrossCenterY"] = result.CenterY;
                action.Data["LocalFindCrossRotationAngle"] = result.RotationAngle;
                action.Data["LocalFindCrossTiltX"] = result.TiltX;
                action.Data["LocalFindCrossTiltY"] = result.TiltY;
                action.Data["LocalFindCrossDiagnostics"] = detection.Diagnostics ?? new object();
                action.Data["LocalFindCrossResultFile"] = persisted.ResultFilePath;
                if (!string.IsNullOrWhiteSpace(detection.RawJson))
                    action.Data["LocalFindCrossRawJson"] = detection.RawJson;
                if (!string.IsNullOrWhiteSpace(detection.InteropDiagnostic))
                    action.Data["LocalFindCrossInteropDiagnostic"] = detection.InteropDiagnostic;
                action.MasterValue(null, persisted.MasterId, (int)ViewResultAlgType.FindCross);
                services.Publish(new LocalFindCrossPublishRequest
                {
                    DeviceCode = algorithmDeviceCode,
                    OperatorCode = OperatorCode,
                    SerialNumber = action.SerialNumber,
                    NodeId = NodeID,
                    ZIndex = ZIndex,
                    MasterId = persisted.MasterId
                });
                return new LocalFindCrossNodeResultData
                {
                    MasterId = persisted.MasterId,
                    SourceMasterId = lease.MasterId,
                    FrameId = lease.FrameId.ToString("N"),
                    ImageFilePath = imageFile,
                    ResultFilePath = persisted.ResultFilePath,
                    Result = result,
                    Diagnostics = detection.Diagnostics,
                    TotalTime = totalTime
                };
            }
            finally
            {
                ownedFrame?.Dispose();
            }
        }

        protected override string BuildRunPayload(CVStartCFC action)
        {
            return JsonConvert.SerializeObject(new
            {
                ServiceName = NodeName,
                EventName = OperatorCode,
                action.SerialNumber,
                ImageFilePath,
                SearchRegion,
                ParameterJson,
                ResultDirectory,
                Algorithm = "LocalFindCross"
            });
        }

        internal static LocalFindCrossNodeItem ValidateDetection(
            LocalFindCrossDetection detection,
            int imageWidth,
            int imageHeight)
        {
            ArgumentNullException.ThrowIfNull(detection);
            if (imageWidth <= 0 || imageHeight <= 0)
                throw new InvalidOperationException($"图像尺寸无效：{imageWidth}x{imageHeight}。");
            if (!detection.Success)
            {
                string reason = string.IsNullOrWhiteSpace(detection.FailureReason)
                    ? "算法拒绝当前图像，但未提供失败原因"
                    : detection.FailureReason;
                string nativeCode = detection.NativeReturnCode < 0 ? $"，原生返回码 {detection.NativeReturnCode}" : string.Empty;
                string diagnostics = detection.Diagnostics == null ? string.Empty : $"；诊断：{JsonConvert.SerializeObject(detection.Diagnostics)}";
                string interop = string.IsNullOrWhiteSpace(detection.InteropDiagnostic) ? string.Empty : $"；互操作：{detection.InteropDiagnostic}";
                throw new InvalidOperationException($"本地 FindCross 定位失败：{reason}{nativeCode}{diagnostics}{interop}。");
            }
            if (detection.Items.Count != 1)
                throw new InvalidOperationException($"本地 FindCross 必须返回且只返回 1 个十字结果，当前为 {detection.Items.Count} 个。");

            LocalFindCrossNodeItem item = detection.Items[0];
            if (string.IsNullOrWhiteSpace(item.Name))
                throw new InvalidOperationException("本地 FindCross 返回的结果名称为空。");
            double[] values =
            [
                item.X, item.Y, item.Width, item.Height,
                item.CenterX, item.CenterY, item.RotationAngle, item.TiltX, item.TiltY
            ];
            if (values.Any(value => !double.IsFinite(value)))
                throw new InvalidOperationException("本地 FindCross 返回了非有限数值。");
            if (item.Width <= 0 || item.Height <= 0)
                throw new InvalidOperationException($"本地 FindCross 返回的包围框尺寸无效：{item.Width}x{item.Height}。");
            if (item.CenterX < 0 || item.CenterX >= imageWidth || item.CenterY < 0 || item.CenterY >= imageHeight)
                throw new InvalidOperationException($"本地 FindCross 中心超出图像范围：({item.CenterX},{item.CenterY})，图像={imageWidth}x{imageHeight}。");
            return item;
        }

        private LocalFlowFrame ResolveFrame(CVStartCFC action, out LocalFlowFrame? ownedFrame, out string? imageFile)
        {
            ownedFrame = null;
            imageFile = null;
            if (action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame) && currentFrame != null)
            {
                imageFile = ResolveFrameFile(currentFrame);
                return currentFrame;
            }

            string? fallbackFile = ResolveFallbackFile(action, out int sourceMasterId);
            if (fallbackFile != null)
            {
                if (!File.Exists(fallbackFile)) throw new FileNotFoundException("FindCross 图像文件不存在。", fallbackFile);
                ownedFrame = services.LoadFrame(fallbackFile);
                if (sourceMasterId > 0) ownedFrame.MasterId = sourceMasterId;
                imageFile = fallbackFile;
                return ownedFrame;
            }

            throw new InvalidOperationException("流程中没有可用的本地图像内存帧或图像结果；请连接本地取图/校正节点，或配置图像文件。");
        }

        private string? ResolveFallbackFile(CVStartCFC action, out int sourceMasterId)
        {
            sourceMasterId = -1;
            if (!string.IsNullOrWhiteSpace(ImageFilePath))
                return NormalizeImagePath(ImageFilePath, "配置的图像文件");

            bool hasInput = TryGetInputMasterResult(action, 0, out int masterId, out int masterResultType, out _);
            if (!hasInput)
            {
                masterId = ReadActionInt(action, "MasterId");
                masterResultType = ReadActionInt(action, "MasterResultType");
            }
            if (masterId <= 0) return null;
            if (masterResultType is not (int)CVCommCore.CVResultType.Camera_Img
                and not (int)CVCommCore.CVResultType.Algorithm_Calibration)
            {
                throw new InvalidOperationException(
                    $"IN 接收到的不是图像结果：MasterId={masterId}，ResultType={masterResultType}。请将图像或本地校正节点连接到 IN。");
            }

            MeasureResultImgModel imageResult = services.GetImageResult(masterId)
                ?? throw new InvalidOperationException($"找不到 IN 图像结果：MasterId={masterId}。");
            sourceMasterId = masterId;
            string? firstCandidate = null;
            foreach (string? candidate in new[] { imageResult.FileUrl, imageResult.RawFile })
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                string fullPath = NormalizeImagePath(candidate, $"IN 图像结果 {masterId}");
                firstCandidate ??= fullPath;
                if (File.Exists(fullPath)) return fullPath;
            }
            if (firstCandidate == null)
                throw new InvalidOperationException($"IN 图像结果没有可读取的文件路径：MasterId={masterId}。");
            return firstCandidate;
        }

        private static string NormalizeImagePath(string value, string source)
        {
            try
            {
                return Path.GetFullPath(value.Trim());
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new InvalidOperationException($"{source}路径无效：{value}", ex);
            }
        }

        private static int ReadActionInt(CVStartCFC action, string key)
        {
            if (!action.Data.TryGetValue(key, out object? value) || value == null) return -1;
            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return -1;
            }
        }

        private static string? ResolveFrameFile(LocalFlowFrame frame)
        {
            string file = frame.Metadata.PrimaryBufferKind == LocalFrameBufferKind.CvCie
                ? frame.CvCieFilePath
                : frame.CvRawFilePath;
            string? exactFrameFile = NullIfWhiteSpace(file);
            if (exactFrameFile != null) return exactFrameFile;

            // SourceFilePath is safe for historical overlays only while the in-memory
            // primary buffer still uses the source bitmap's geometry and pixels. A
            // calibrated or mirrored frame must not silently point the result viewer at
            // the pre-transform source image; upstream SaveFiles should provide CvRaw/
            // CvCie in those cases.
            bool sourceStillMatchesPrimary = frame.Metadata.PrimaryBufferKind == LocalFrameBufferKind.CvRaw
                && frame.Metadata.FlipMode == CVImageFlipMode.None
                && string.IsNullOrWhiteSpace(frame.Metadata.CalibrationTemplate);
            return sourceStillMatchesPrimary ? NullIfWhiteSpace(frame.Metadata.SourceFilePath) : null;
        }

        private static string? NullIfWhiteSpace(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
