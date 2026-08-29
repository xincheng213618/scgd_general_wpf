using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.FindLightArea;
using ColorVision.Engine.Templates.POI;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.Nodes
{
    public sealed class Int32RectNodePropertyDescriptor : STNodePropertyDescriptor
    {
        protected override byte[] GetBytesFromValue()
        {
            if (PropertyInfo.GetValue(Node) is not Int32Rect value)
                return Array.Empty<byte>();
            byte[] bytes = new byte[sizeof(int) * 4];
            BitConverter.TryWriteBytes(bytes.AsSpan(0, sizeof(int)), value.X);
            BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(int), sizeof(int)), value.Y);
            BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(int) * 2, sizeof(int)), value.Width);
            BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(int) * 3, sizeof(int)), value.Height);
            return bytes;
        }

        protected override object GetValueFromBytes(byte[] byData)
        {
            if (byData == null || byData.Length != sizeof(int) * 4)
                throw new InvalidOperationException("ROI 节点属性数据长度无效。");
            return new Int32Rect(
                BitConverter.ToInt32(byData, 0),
                BitConverter.ToInt32(byData, sizeof(int)),
                BitConverter.ToInt32(byData, sizeof(int) * 2),
                BitConverter.ToInt32(byData, sizeof(int) * 3));
        }
    }

    internal sealed class LocalLuminousAreaCorner
    {
        public string Name { get; init; } = string.Empty;
        public float X { get; init; }
        public float Y { get; init; }
    }

    internal sealed class LocalFindLuminousAreaNodeResultData
    {
        public int MasterId { get; init; }
        public int MasterResultType { get; init; } = (int)ViewResultAlgType.FindLightArea;
        public int SourceMasterId { get; init; }
        public string? FrameId { get; init; }
        public string? ImageFilePath { get; init; }
        public string Algorithm { get; init; } = string.Empty;
        public double Confidence { get; init; }
        public IReadOnlyList<LocalLuminousAreaCorner> Corners { get; init; } = Array.Empty<LocalLuminousAreaCorner>();
        public IReadOnlyList<LuminousAreaSideQuality> SideQuality { get; init; } = Array.Empty<LuminousAreaSideQuality>();
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
        public string? SavePoiTemplateName { get; init; }
        public string? SavePoiTemplateShape { get; init; }
        public int TotalTime { get; init; }
        public object? POIResult => Corners;
    }

    internal sealed class LocalFindLuminousAreaPersistenceRequest
    {
        public required CVStartCFC Action { get; init; }
        public required string Algorithm { get; init; }
        public string? ImageFilePath { get; init; }
        public required string DeviceCode { get; init; }
        public int ZIndex { get; init; }
        public int TotalTime { get; init; }
        public required object Parameters { get; init; }
        public required IReadOnlyList<LocalLuminousAreaCorner> Corners { get; init; }

        public IReadOnlyCollection<AlgResultLightAreaModel> CreateDetails(int masterId) =>
            LocalLuminousAreaResultPersistence.CreateDetails(masterId, Corners);
    }

    internal sealed class LocalFindLuminousAreaPublishRequest
    {
        public required string DeviceCode { get; init; }
        public required string OperatorCode { get; init; }
        public required string SerialNumber { get; init; }
        public required string NodeId { get; init; }
        public int ZIndex { get; init; }
        public int MasterId { get; init; }
    }

    internal interface ILocalFindLuminousAreaNodeServices
    {
        LocalFlowFrame LoadFrame(string filePath);
        MeasureResultImgModel? GetImageResult(int masterId);
        LuminousAreaDetectionResult Detect(HImage image, RoiRect roi, double minimumConfidence);
        LocalLuminousAreaPoiTemplateShape UpdatePoiTemplate(string templateName, IReadOnlyList<LocalLuminousAreaCorner> corners);
        int Persist(LocalFindLuminousAreaPersistenceRequest request);
        void Publish(LocalFindLuminousAreaPublishRequest request);
    }

    internal sealed class LocalFindLuminousAreaNodeServices : ILocalFindLuminousAreaNodeServices
    {
        public static LocalFindLuminousAreaNodeServices Instance { get; } = new();

        private LocalFindLuminousAreaNodeServices()
        {
        }

        public LocalFlowFrame LoadFrame(string filePath) => LocalFrameFileService.Load(filePath);

        public MeasureResultImgModel? GetImageResult(int masterId) => MeasureImgResultDao.Instance.GetById(masterId);

        public LuminousAreaDetectionResult Detect(HImage image, RoiRect roi, double minimumConfidence) =>
            LuminousAreaNative.DetectV2(image, roi, minimumConfidence);

        public LocalLuminousAreaPoiTemplateShape UpdatePoiTemplate(
            string templateName,
            IReadOnlyList<LocalLuminousAreaCorner> corners) =>
            LocalLuminousAreaPoiTemplateUpdater.Update(
                templateName,
                corners.Select(corner => new LuminousAreaPoint(corner.X, corner.Y)).ToArray());

        public int Persist(LocalFindLuminousAreaPersistenceRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return LocalFlowResultPersistence.SaveAlgorithmResultWithDetails(
                request.Action,
                ViewResultAlgType.FindLightArea,
                null,
                request.Algorithm,
                request.ImageFilePath,
                request.DeviceCode,
                request.ZIndex,
                request.TotalTime,
                request.Parameters,
                request.CreateDetails);
        }

        public void Publish(LocalFindLuminousAreaPublishRequest request)
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
                (int)ViewResultAlgType.FindLightArea);
        }
    }

    internal static class LocalLuminousAreaResultPersistence
    {
        private static readonly string[] CornerNames = ["LT", "RT", "RB", "LB"];

        public static List<AlgResultLightAreaModel> CreateDetails(int masterId, IReadOnlyList<LocalLuminousAreaCorner> corners)
        {
            if (masterId <= 0) throw new ArgumentOutOfRangeException(nameof(masterId), "发光区结果主表 ID 无效。");
            ArgumentNullException.ThrowIfNull(corners);
            if (corners.Count != 4) throw new ArgumentException("发光区结果必须包含 4 个角点。", nameof(corners));
            List<AlgResultLightAreaModel> details = new(corners.Count);
            for (int index = 0; index < corners.Count; index++)
            {
                LocalLuminousAreaCorner corner = corners[index];
                if (!string.Equals(corner.Name, CornerNames[index], StringComparison.Ordinal))
                    throw new ArgumentException($"发光区角点顺序必须为 LT、RT、RB、LB；索引 {index} 实际为 {corner.Name}。", nameof(corners));
                details.Add(new AlgResultLightAreaModel
                {
                    Pid = masterId,
                    PosX = corner.X,
                    PosY = corner.Y
                });
            }
            return details;
        }

    }

    [STNode("Flow_CustomNodes", "本地发光区定位(V2)")]
    [FlowNodePropertyEditorAttribute(nameof(SavePOITempName), typeof(FlowPoiTemplateEditor))]
    public sealed class LocalFindLuminousAreaNode : LocalFlowNodeBase
    {
        internal const double DefaultMinimumConfidence = 0.25;
        private static readonly string[] CornerNames = ["LT", "RT", "RB", "LB"];

        private string imageFilePath = string.Empty;
        private string savePoiTempName = string.Empty;
        private Int32Rect searchRegion = Int32Rect.Empty;
        private double minimumConfidence = DefaultMinimumConfidence;
        private readonly ILocalFindLuminousAreaNodeServices services;

        [Category("本地发光区定位")]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        [STNodeProperty("图像文件", "可选后备输入；优先使用上游本地内存帧，没有内存帧时先读该文件，再回退到 IN 图像结果", true)]
        public string ImageFilePath
        {
            get => imageFilePath;
            set
            {
                imageFilePath = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        [Category("本地发光区定位")]
        [STNodeProperty("POI保存模板", "可选；定位成功后按旧发光区服务契约更新一个 Rect/LTRect 或四个 PolygonFour 模板明细", true)]
        public string SavePOITempName
        {
            get => savePoiTempName;
            set
            {
                savePoiTempName = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        [Category("本地发光区定位")]
        [STNodeProperty("搜索区域", "可选 ROI（X,Y,Width,Height）；0,0,0,0 表示整幅图像", true, DescriptorType = typeof(Int32RectNodePropertyDescriptor))]
        public Int32Rect SearchRegion
        {
            get => searchRegion;
            set
            {
                searchRegion = value;
                OnPropertyChanged();
            }
        }

        [Category("本地发光区定位")]
        [STNodeProperty("最小置信度", "低于该值时拒绝输出四角点；范围 0 到 1", true)]
        public double MinimumConfidence
        {
            get => minimumConfidence;
            set
            {
                minimumConfidence = value;
                OnPropertyChanged();
            }
        }

        public LocalFindLuminousAreaNode() : this(LocalFindLuminousAreaNodeServices.Instance)
        {
        }

        internal LocalFindLuminousAreaNode(ILocalFindLuminousAreaNodeServices services)
            : base("本地发光区定位(V2)", "LocalFindLuminousAreaV2", "FindLightArea")
        {
            this.services = services ?? throw new ArgumentNullException(nameof(services));
            SelectFirstAvailableDevice<DeviceAlgorithm>();
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            return new LocalNodeExecutionResult { Data = ExecuteSynchronously(action) };
        }

        internal LocalFindLuminousAreaNodeResultData ExecuteSynchronously(CVStartCFC action)
        {
            ArgumentNullException.ThrowIfNull(action);
            ValidateMinimumConfidence(MinimumConfidence);
            LocalFlowFrame? ownedFrame = null;
            LocalFlowFrame frame = ResolveFrame(action, out ownedFrame, out string? imageFile);
            bool loadedFromFile = ownedFrame != null;
            try
            {
                using LocalFlowFrameLease lease = frame.Acquire();
                if (!lease.IsFlipApplied)
                    throw new InvalidOperationException("当前图像的方向变换尚未完成，无法生成可供后续映射使用的发光区角点。");

                RoiRect roi = ResolveRoi(SearchRegion, lease.Metadata.Width, lease.Metadata.Height);
                HImage image = CreateBorrowedImage(lease);
                Stopwatch stopwatch = Stopwatch.StartNew();
                LuminousAreaDetectionResult detection = services.Detect(image, roi, MinimumConfidence);
                stopwatch.Stop();
                LocalLuminousAreaCorner[] corners = ValidateDetection(detection, MinimumConfidence);
                double confidence = detection.Confidence!.Value;
                int totalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                string? savePoiTemplateName = string.IsNullOrWhiteSpace(SavePOITempName) ? null : SavePOITempName.Trim();
                LocalLuminousAreaPoiTemplateShape? savePoiTemplateShape = savePoiTemplateName == null
                    ? null
                    : services.UpdatePoiTemplate(savePoiTemplateName, corners);
                string algorithmDeviceCode = ResolveAvailableDeviceCode<DeviceAlgorithm>();
                int masterId = services.Persist(new LocalFindLuminousAreaPersistenceRequest
                {
                    Action = action,
                    Algorithm = detection.Algorithm,
                    ImageFilePath = imageFile,
                    DeviceCode = algorithmDeviceCode,
                    ZIndex = ZIndex,
                    TotalTime = totalTime,
                    Parameters = new
                    {
                        Algorithm = detection.Algorithm,
                        SourceMasterId = lease.MasterId,
                        FrameId = lease.FrameId.ToString("N"),
                        ImageFilePath = imageFile,
                        SearchRegion = new { roi.X, roi.Y, roi.Width, roi.Height },
                        MinimumConfidence,
                        Confidence = confidence,
                        CornerOrder = CornerNames,
                        Corners = corners,
                        SavePOITemplate = savePoiTemplateName == null ? null : new
                        {
                            Name = savePoiTemplateName,
                            Shape = savePoiTemplateShape?.ToString()
                        },
                        detection.SideQuality,
                        detection.Warnings,
                        PrimaryBufferKind = lease.Metadata.PrimaryBufferKind.ToString(),
                        SourceFilePath = NullIfWhiteSpace(lease.Metadata.SourceFilePath),
                        CvRawFilePath = NullIfWhiteSpace(frame.CvRawFilePath),
                        CvCieFilePath = NullIfWhiteSpace(frame.CvCieFilePath),
                        CalibrationTemplate = NullIfWhiteSpace(lease.Metadata.CalibrationTemplate),
                        FlipMode = lease.Metadata.FlipMode.ToString(),
                        FlipApplied = lease.IsFlipApplied,
                        ImageRead = loadedFromFile,
                        MemoryOnly = string.IsNullOrWhiteSpace(imageFile)
                    },
                    Corners = corners
                });
                if (ownedFrame != null)
                {
                    action.SetCurrentFrame(ownedFrame);
                    ownedFrame = null;
                }
                action.Data["LocalLuminousAreaConfidence"] = confidence;
                action.Data["LocalLuminousAreaCornerOrder"] = "LT,RT,RB,LB";
                action.Data["LocalLuminousAreaWarnings"] = detection.Warnings.ToArray();
                if (savePoiTemplateName != null)
                {
                    action.Data["LocalLuminousAreaSavePOITemplate"] = savePoiTemplateName;
                    action.Data["LocalLuminousAreaSavePOITemplateShape"] = savePoiTemplateShape!.Value.ToString();
                }
                action.MasterValue(null, masterId, (int)ViewResultAlgType.FindLightArea);
                services.Publish(new LocalFindLuminousAreaPublishRequest
                {
                    DeviceCode = algorithmDeviceCode,
                    OperatorCode = OperatorCode,
                    SerialNumber = action.SerialNumber,
                    NodeId = NodeID,
                    ZIndex = ZIndex,
                    MasterId = masterId
                });
                return new LocalFindLuminousAreaNodeResultData
                {
                    MasterId = masterId,
                    SourceMasterId = lease.MasterId,
                    FrameId = lease.FrameId.ToString("N"),
                    ImageFilePath = imageFile,
                    Algorithm = detection.Algorithm,
                    Confidence = confidence,
                    Corners = corners,
                    SideQuality = detection.SideQuality,
                    Warnings = detection.Warnings,
                    SavePoiTemplateName = savePoiTemplateName,
                    SavePoiTemplateShape = savePoiTemplateShape?.ToString(),
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
                MinimumConfidence,
                SavePOITempName,
                Algorithm = "RobustV2"
            });
        }

        internal static LocalLuminousAreaCorner[] ValidateDetection(LuminousAreaDetectionResult detection, double minimumConfidence)
        {
            ArgumentNullException.ThrowIfNull(detection);
            ValidateMinimumConfidence(minimumConfidence);
            if (!detection.Success)
            {
                string reason = string.IsNullOrWhiteSpace(detection.FailureReason)
                    ? "算法拒绝当前图像，但未提供失败原因"
                    : detection.FailureReason;
                string confidence = detection.Confidence.HasValue ? $"，置信度 {detection.Confidence.Value:F3}" : string.Empty;
                string nativeCode = detection.NativeReturnCode < 0 ? $"，原生返回码 {detection.NativeReturnCode}" : string.Empty;
                string diagnostic = string.IsNullOrWhiteSpace(detection.Diagnostic) ? string.Empty : $"；{detection.Diagnostic}";
                throw new InvalidOperationException($"发光区定位失败：{reason}{confidence}{nativeCode}{diagnostic}。");
            }
            if (!string.Equals(detection.Algorithm, "RobustV2", StringComparison.Ordinal))
                throw new InvalidOperationException($"发光区定位返回了不受支持的算法标识：{detection.Algorithm}。");
            if (!detection.Confidence.HasValue || !double.IsFinite(detection.Confidence.Value)
                || detection.Confidence.Value < 0 || detection.Confidence.Value > 1)
                throw new InvalidOperationException("发光区定位返回了无效置信度。");
            if (detection.Confidence.Value < minimumConfidence)
                throw new InvalidOperationException($"发光区定位置信度不足：{detection.Confidence.Value:F3}，要求至少 {minimumConfidence:F3}。");
            if (detection.Corners.Count != 4 || detection.Corners.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
                throw new InvalidOperationException($"发光区定位必须返回 4 个有效角点，当前为 {detection.Corners.Count} 个。");
            if (!LuminousAreaResultParser.TryValidateOrderedCorners(detection.Corners, out string geometryError))
                throw new InvalidOperationException($"发光区定位四角点无效：{geometryError}");

            LocalLuminousAreaCorner[] corners = new LocalLuminousAreaCorner[CornerNames.Length];
            for (int index = 0; index < corners.Length; index++)
            {
                LuminousAreaPoint source = detection.Corners[index];
                if (source.X < float.MinValue || source.X > float.MaxValue || source.Y < float.MinValue || source.Y > float.MaxValue)
                    throw new InvalidOperationException($"发光区定位角点 {CornerNames[index]} 坐标超出可保存范围。");
                corners[index] = new LocalLuminousAreaCorner
                {
                    Name = CornerNames[index],
                    X = (float)source.X,
                    Y = (float)source.Y
                };
            }
            return corners;
        }

        internal static RoiRect ResolveRoi(Int32Rect requested, int imageWidth, int imageHeight)
        {
            if (imageWidth <= 0 || imageHeight <= 0)
                throw new InvalidOperationException($"图像尺寸无效：{imageWidth}x{imageHeight}。");
            if (requested == Int32Rect.Empty || (requested.X == 0 && requested.Y == 0 && requested.Width == 0 && requested.Height == 0))
                return new RoiRect();
            if (requested.X < 0 || requested.Y < 0 || requested.Width <= 0 || requested.Height <= 0)
                throw new InvalidOperationException("搜索区域必须位于图像内，且 Width、Height 必须同时大于 0；0,0,0,0 表示整图。");
            if ((long)requested.X + requested.Width > imageWidth || (long)requested.Y + requested.Height > imageHeight)
                throw new InvalidOperationException($"搜索区域超出图像范围：ROI=({requested.X},{requested.Y},{requested.Width},{requested.Height})，图像={imageWidth}x{imageHeight}。");
            return new RoiRect(requested.X, requested.Y, requested.Width, requested.Height);
        }

        internal static HImage CreateBorrowedImage(LocalFlowFrameLease lease)
        {
            ArgumentNullException.ThrowIfNull(lease);
            if (lease.Metadata.PrimaryBufferKind == LocalFrameBufferKind.CvCie)
            {
                if (!lease.HasCie)
                    throw new InvalidOperationException("当前帧声明以 CIE 为主缓冲区，但没有可用的 CIE 数据；拒绝静默改用 RAW，以免结果来源记录错误。");
                return CreateBorrowedCieLuminanceImage(lease);
            }

            IntPtr pointer = lease.RawPointer;
            if (pointer == IntPtr.Zero || lease.RawLength <= 0)
                throw new InvalidOperationException("当前本地图像内存帧没有可用的主图像缓冲区。");
            int depth = lease.Metadata.SourceBpp;
            int channels = lease.Metadata.Channels;
            if (depth is not (8 or 16) || channels is not (1 or 3))
                throw new NotSupportedException($"发光区定位仅支持 8/16 位、1/3 通道交错 RAW；当前为 {depth} 位、{channels} 通道。");
            int stride = checked(lease.Metadata.Width * channels * (depth / 8));
            int requiredLength = checked(stride * lease.Metadata.Height);
            if (lease.RawLength < requiredLength)
                throw new InvalidOperationException($"RAW 图像缓冲区长度不足：需要 {requiredLength} 字节，实际 {lease.RawLength} 字节。");
            return new HImage
            {
                rows = lease.Metadata.Height,
                cols = lease.Metadata.Width,
                channels = channels,
                depth = depth,
                stride = stride,
                isDispose = true,
                pData = pointer
            };
        }

        private static HImage CreateBorrowedCieLuminanceImage(LocalFlowFrameLease lease)
        {
            if (lease.CiePointer == IntPtr.Zero || lease.CieLength <= 0)
                throw new InvalidOperationException("当前本地图像内存帧没有可用的 CIE 缓冲区。");
            if (lease.Metadata.CieBpp != 32)
                throw new NotSupportedException($"发光区定位仅支持 32 位浮点 CIE 平面；当前位深为 {lease.Metadata.CieBpp}。");

            int planeBytes = checked(lease.Metadata.Width * lease.Metadata.Height * sizeof(float));
            if (planeBytes <= 0 || lease.CieLength % planeBytes != 0)
                throw new InvalidOperationException($"CIE 缓冲区长度 {lease.CieLength} 与 {lease.Metadata.Width}x{lease.Metadata.Height} 浮点平面不匹配。");
            int planeCount = lease.CieLength / planeBytes;
            if (planeCount is not (1 or 3))
                throw new NotSupportedException($"发光区定位仅支持单平面 Y 或三平面 XYZ CIE；当前平面数为 {planeCount}。");

            // CIE 内存按 X、Y、Z 三个连续 float 平面排列。定位只需要亮度，
            // 三平面时借用第二个 Y 平面，避免把 planar 数据误当成交错 XYZ。
            IntPtr luminancePointer = planeCount == 3
                ? IntPtr.Add(lease.CiePointer, planeBytes)
                : lease.CiePointer;
            return new HImage
            {
                rows = lease.Metadata.Height,
                cols = lease.Metadata.Width,
                channels = 1,
                depth = 32,
                stride = checked(lease.Metadata.Width * sizeof(float)),
                isDispose = true,
                pData = luminancePointer
            };
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
                if (!File.Exists(fallbackFile)) throw new FileNotFoundException("发光区定位图像文件不存在。", fallbackFile);
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
            return NullIfWhiteSpace(file);
        }

        private static string? NullIfWhiteSpace(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;

        private static void ValidateMinimumConfidence(double value)
        {
            if (!double.IsFinite(value) || value < 0 || value > 1)
                throw new InvalidOperationException("最小置信度必须在 0 到 1 之间。");
        }
    }
}
