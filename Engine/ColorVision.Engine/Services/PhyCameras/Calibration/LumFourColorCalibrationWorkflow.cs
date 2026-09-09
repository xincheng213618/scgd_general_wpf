using ColorVision.Common.MVVM;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.POI;
using ColorVision.FileIO;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Services.PhyCameras.Calibration
{
    public sealed record LumFourColorCieCapture(
        byte[] Data,
        int Width,
        int Height,
        int BitsPerChannel,
        int Channels,
        float Gain,
        float[] Exposure)
    {
        public string Source { get; init; } = string.Empty;
        public string? CalibrationHash { get; init; }
    }

    public sealed record LumFourColorSpectrumCapture(
        ColorCorrectionYxy Measurement,
        IReadOnlyList<ColorCorrectionSpectrumPoint> Spectrum,
        int ResultId,
        DateTimeOffset CapturedAt)
    {
        public double? PeakAd { get; init; }
        public double? IntegrationTime { get; init; }
        public int? NdPort { get; init; }
        public string Source { get; init; } = string.Empty;

        public static LumFourColorSpectrumCapture FromMeasurement(SpectrumColorMeasurement result) => new(
            new ColorCorrectionYxy(result.Y, result.CieX, result.CieY),
            result.Spectrum.Select(point => new ColorCorrectionSpectrumPoint(point.Wavelength, point.Value)).ToArray(),
            result.ResultId, result.CapturedAt)
        {
            PeakAd = result.PeakAd, IntegrationTime = result.IntegrationTime,
            NdPort = result.NdPort, Source = result.DeviceCode,
        };
    }

    public interface ILumFourColorCameraCaptureProvider
    {
        Task<LumFourColorCieCapture> CaptureAsync(LumFourColorCorrectionTarget target, CancellationToken cancellationToken = default);
    }

    public interface ILumFourColorSpectrumCaptureProvider
    {
        Task<LumFourColorSpectrumCapture> CaptureAsync(LumFourColorCorrectionTarget target, CancellationToken cancellationToken = default);
    }

    internal sealed class LocalLumFourColorCameraCaptureProvider(DeviceCamera device, CalibrationParam calibration) : ILumFourColorCameraCaptureProvider
    {
        public Task<LumFourColorCieCapture> CaptureAsync(LumFourColorCorrectionTarget target, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!device.TryGetCalibrationTemplateFiles(calibration, out var files, out string? error))
                    throw new InvalidOperationException(error ?? "无法解析相机校正模板。");
                var colorFile = files.SingleOrDefault(file => file.CalibrationType is CalibrationType.LumFourColor or CalibrationType.LumMultiColor);
                if (colorFile == null)
                    throw new InvalidOperationException("请选择启用了四色或多色校正文件的相机模板。");
                var calibrationSource = LumFourColorSourceSnapshot.Load(colorFile.FullPath);
                if (calibrationSource.CalibrationFile.CalibrationType != colorFile.CalibrationType)
                    throw new InvalidOperationException("模板的校正类型与文件格式不一致：a…i 文件应选择四色校正，Gain/pa 文件应选择多色校正。");
                EnsureCameraConnected();
                LocalCameraCaptureResult result = LocalCameraCaptureService.Capture(new LocalCameraCaptureRequest
                {
                    Device = device,
                    Calibration = calibration,
                    FlipMode = device.DisplayConfig.FlipMode,
                    SaveFiles = false,
                });
                using LocalFlowFrame frame = result.Frame;
                using LocalFlowFrameLease lease = frame.Acquire();
                if (!lease.HasCie)
                    throw new InvalidOperationException("当前校正模板没有生成 CIE 图像。");

                byte[] data = lease.CopyCieToArray();
                cancellationToken.ThrowIfCancellationRequested();
                calibrationSource.EnsureUnchanged();
                return new LumFourColorCieCapture(
                    data,
                    lease.Metadata.Width,
                    lease.Metadata.Height,
                    lease.Metadata.CieBpp,
                    lease.Metadata.Channels,
                    lease.Metadata.Gain,
                    lease.Metadata.Exposure.ToArray())
                {
                    Source = $"{device.Name} · {calibration.Name}",
                    CalibrationHash = calibrationSource.Hash,
                };
            }, cancellationToken);
        }

        private void EnsureCameraConnected()
        {
            if (device.LocalCameraSession.IsOpen)
                return;
            if (device.Config.TakeImageMode == TakeImageMode.Live)
                throw new InvalidOperationException("当前相机为 Live 模式，请切换到测量模式后重试。");

            string cameraId = device.Config.CameraID?.Trim() ?? string.Empty;
            if (cameraId.Length == 0)
                throw new InvalidOperationException($"相机“{device.Code}”未配置 Camera ID。");

            int errorCode = device.LocalCameraSession.Open(
                cameraId,
                device.Config.TakeImageMode,
                device.Config.ImageBpp == ImageBpp.bpp16 ? 16 : 8);
            if (errorCode == cvErrorDefine.CV_ERR_SUCCESS)
                return;

            string errorMessage = string.Empty;
            cvCameraCSLib.CM_GetErrorMessage(errorCode, ref errorMessage);
            throw new InvalidOperationException($"相机“{device.Code}”连接失败：{(string.IsNullOrWhiteSpace(errorMessage) ? "未知相机错误" : errorMessage)} ({errorCode})");
        }
    }

    internal sealed class DeviceLumFourColorSpectrumCaptureProvider(DeviceSpectrum device) : ILumFourColorSpectrumCaptureProvider
    {
        public async Task<LumFourColorSpectrumCapture> CaptureAsync(LumFourColorCorrectionTarget target, CancellationToken cancellationToken = default)
        {
            SpectrumColorMeasurement result = await device.CaptureColorMeasurementAsync(cancellationToken);
            return LumFourColorSpectrumCapture.FromMeasurement(result);
        }
    }

    public sealed class LumFourColorCalibrationSample : ViewModelBase
    {
        public LumFourColorCalibrationSample(LumFourColorCorrectionTarget target)
        {
            Target = target;
        }

        public LumFourColorCorrectionTarget Target { get; }
        public event EventHandler? Changed;
        public string Name => Target switch
        {
            LumFourColorCorrectionTarget.SinglePoint => "单点",
            LumFourColorCorrectionTarget.Red => "R",
            LumFourColorCorrectionTarget.Green => "G",
            LumFourColorCorrectionTarget.Blue => "B",
            LumFourColorCorrectionTarget.White => "W",
            _ => string.Empty,
        };

        public LumFourColorCieCapture? Frame { get; private set; }
        public WriteableBitmap? Preview { get; private set; }
        public PoiMeasurementPoint? Poi { get; private set; }
        public double? CameraX { get; private set; }
        public double? CameraY { get; private set; }
        public double? CameraZ { get; private set; }
        public double? CameraCieX { get; private set; }
        public double? CameraCieY { get; private set; }
        private string cameraYInput = string.Empty, cameraCieXInput = string.Empty, cameraCieYInput = string.Empty;
        private PoiMeasurementResult? acquiredCamera;
        public string CameraYInput { get => cameraYInput; set { if (cameraYInput == value) return; cameraYInput = value; OnPropertyChanged(); UpdateManualCamera(); } }
        public string CameraCieXInput { get => cameraCieXInput; set { if (cameraCieXInput == value) return; cameraCieXInput = value; OnPropertyChanged(); UpdateManualCamera(); } }
        public string CameraCieYInput { get => cameraCieYInput; set { if (cameraCieYInput == value) return; cameraCieYInput = value; OnPropertyChanged(); UpdateManualCamera(); } }
        public bool IsCameraEdited { get; private set; }
        public string CameraInputError { get; private set; } = string.Empty;
        public bool CanRestoreCamera => IsCameraEdited && acquiredCamera.HasValue;
        public string CameraQuality => IsCameraEdited ? HasCameraMeasurement ? "手动值" : CameraInputError : HasCameraMeasurement ? "POI 测量" : "";
        public float? CameraGain => Frame?.Gain;
        public string CameraExposure => Frame == null
            ? string.Empty
            : string.Join(" / ", Frame.Exposure.Select(value => value.ToString("G7", CultureInfo.CurrentCulture)));
        public double? ReferenceY { get; private set; }
        public double? ReferenceCieX { get; private set; }
        public double? ReferenceCieY { get; private set; }
        private string referenceYInput = string.Empty, referenceCieXInput = string.Empty, referenceCieYInput = string.Empty;
        private ColorCorrectionYxy? acquiredReference;
        public string ReferenceYInput { get => referenceYInput; set { if (referenceYInput == value) return; referenceYInput = value; OnPropertyChanged(); UpdateManualReference(); } }
        public string ReferenceCieXInput { get => referenceCieXInput; set { if (referenceCieXInput == value) return; referenceCieXInput = value; OnPropertyChanged(); UpdateManualReference(); } }
        public string ReferenceCieYInput { get => referenceCieYInput; set { if (referenceCieYInput == value) return; referenceCieYInput = value; OnPropertyChanged(); UpdateManualReference(); } }
        public bool IsReferenceEdited { get; private set; }
        public string ReferenceInputError { get; private set; } = string.Empty;
        public bool CanRestoreReference => IsReferenceEdited && acquiredReference.HasValue;
        public IReadOnlyList<ColorCorrectionSpectrumPoint> Spectrum { get; private set; } = Array.Empty<ColorCorrectionSpectrumPoint>();
        public int? SpectrumResultId { get; private set; }
        public DateTimeOffset? SpectrumCapturedAt { get; private set; }
        public double? SpectrumPeakAd { get; private set; }
        public double? SpectrumIpPercent => LumFourColorDataChecks.IpPercent(SpectrumPeakAd);
        public double? SpectrumIntegrationTime { get; private set; }
        public int? SpectrumNdPort { get; private set; }
        public string SpectrumSource { get; private set; } = string.Empty;
        public string CameraSource => Frame?.Source ?? string.Empty;
        public string SpectrumSourceDescription => IsReferenceEdited
            ? acquiredReference.HasValue ? $"手动修改 · 原始来源：{SpectrumSource}" : "手动录入 · 无原始采集记录"
            : SpectrumSource;
        public string SpectrumDetailsHeading => IsReferenceEdited ? "原始光谱明细（仅供核对）" : "光谱明细";
        public string SpectrumMetadataHeading => IsReferenceEdited ? "原始记录（仅供核对）" : "测量记录";
        public string SpectrumQuality => IsReferenceEdited
            ? HasSpectrumMeasurement ? "手动值" : ReferenceInputError
            : !HasSpectrumMeasurement ? "" :
            !SpectrumPeakAd.HasValue ? "IP 未知" : $"IP {SpectrumIpPercent:F2}% · {(LumFourColorDataChecks.SpectrumWarning(SpectrumPeakAd) == null ? "合格" : "待复核")}";
        public string CameraState => HasCameraMeasurement ? IsCameraEdited ? "相机 · 手动" : "相机 ✓" : "相机 —";
        public string SpectrumState => HasSpectrumMeasurement ? IsReferenceEdited ? "光谱 · 手动" : "光谱 ✓" : "光谱 —";
        public string PoiDescription => Poi is PoiMeasurementPoint poi ? $"{(poi.Shape == PoiMeasurementShape.Circle ? "圆形" : "矩形")} · 中心 ({poi.X}, {poi.Y}) · {poi.Width} × {poi.Height} px" : "尚未绘制";
        public bool HasImage => Frame != null;
        public bool HasCameraMeasurement => CameraY.HasValue && CameraCieX.HasValue && CameraCieY.HasValue;
        public bool HasSpectrumMeasurement => ReferenceY.HasValue && ReferenceCieX.HasValue && ReferenceCieY.HasValue && (IsReferenceEdited || Spectrum.Count > 0);
        public bool IsComplete => HasCameraMeasurement && HasSpectrumMeasurement;
        public string Progress => IsComplete
            ? IsReferenceEdited ? "手动参考待复核" : LumFourColorDataChecks.SpectrumWarning(SpectrumPeakAd) == null ? "数据齐全" : "光谱待复核"
            : HasCameraMeasurement
                ? "待采集光谱"
                : HasSpectrumMeasurement
                    ? HasImage ? "待绘制 POI" : "待相机取图"
                    : HasImage ? "待 POI / 光谱" : "待相机 / 光谱";

        public void SetFrame(LumFourColorCieCapture frame, WriteableBitmap preview)
        {
            Frame = frame;
            Preview = preview;
            ClearCameraMeasurement();
        }

        public void SetCameraMeasurement(PoiMeasurementPoint poi, PoiMeasurementResult result)
        {
            ClearCameraMeasurement();
            if (!float.IsFinite(result.X) || !float.IsFinite(result.Y) || !float.IsFinite(result.Z) ||
                !float.IsFinite(result.ChromaX) || !float.IsFinite(result.ChromaY))
            {
                throw new InvalidOperationException("POI 返回的 XYZ 或 CIE x/y 无效。");
            }
            LumFourColorDataChecks.ValidateYxy(new ColorCorrectionYxy(result.Y, result.ChromaX, result.ChromaY), "相机 POI");

            Poi = poi;
            CameraX = result.X;
            CameraY = result.Y;
            CameraZ = result.Z;
            CameraCieX = result.ChromaX;
            CameraCieY = result.ChromaY;
            acquiredCamera = result;
            SetCameraInputs(result);
            RaiseStateChanged();
        }

        private void SetCameraInputs(PoiMeasurementResult? value)
        {
            cameraYInput = value?.Y.ToString("R", CultureInfo.CurrentCulture) ?? string.Empty;
            cameraCieXInput = value?.ChromaX.ToString("R", CultureInfo.CurrentCulture) ?? string.Empty;
            cameraCieYInput = value?.ChromaY.ToString("R", CultureInfo.CurrentCulture) ?? string.Empty;
            OnPropertyChanged(nameof(CameraYInput));
            OnPropertyChanged(nameof(CameraCieXInput));
            OnPropertyChanged(nameof(CameraCieYInput));
        }

        private void UpdateManualCamera()
        {
            IsCameraEdited = true;
            CameraX = CameraY = CameraZ = CameraCieX = CameraCieY = null;
            CameraInputError = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(cameraYInput) || string.IsNullOrWhiteSpace(cameraCieXInput) || string.IsNullOrWhiteSpace(cameraCieYInput))
                    throw new InvalidOperationException("请完整填写相机 Y、x、y。");
                var value = new ColorCorrectionYxy(ReadCameraInput(cameraYInput, acquiredCamera?.Y, "相机 Y"), ReadCameraInput(cameraCieXInput, acquiredCamera?.ChromaX, "相机 x"), ReadCameraInput(cameraCieYInput, acquiredCamera?.ChromaY, "相机 y"));
                LumFourColorDataChecks.ValidateYxy(value, "相机值");
                CameraY = value.Y;
                CameraCieX = value.CieX;
                CameraCieY = value.CieY;
                CameraX = value.Y * (value.CieX / value.CieY);
                CameraZ = value.Y * ((1 - value.CieX - value.CieY) / value.CieY);
            }
            catch (InvalidOperationException ex) { CameraInputError = ex.Message; }
            RaiseStateChanged();
        }

        private static double ReadCameraInput(string text, float? original, string name) => original.HasValue
            && text == original.Value.ToString("R", CultureInfo.CurrentCulture) ? original.Value : ParseInputNumber(text, name);

        public void RestoreCamera()
        {
            if (acquiredCamera is not PoiMeasurementResult original) return;
            CameraX = original.X;
            CameraY = original.Y;
            CameraZ = original.Z;
            CameraCieX = original.ChromaX;
            CameraCieY = original.ChromaY;
            IsCameraEdited = false;
            CameraInputError = string.Empty;
            SetCameraInputs(original);
            RaiseStateChanged();
        }

        public void SetSpectrumMeasurement(LumFourColorSpectrumCapture result)
        {
            ClearSpectrum();
            ArgumentNullException.ThrowIfNull(result);
            if (!double.IsFinite(result.Measurement.Y) || !double.IsFinite(result.Measurement.CieX) ||
                !double.IsFinite(result.Measurement.CieY) || result.Spectrum == null || result.Spectrum.Count == 0 ||
                result.Spectrum.Any(point => !double.IsFinite(point.Wavelength) || !double.IsFinite(point.Value)))
            {
                throw new InvalidOperationException("光谱仪返回的 Y、CIE x/y 或光谱数据无效。");
            }
            LumFourColorDataChecks.ValidateYxy(result.Measurement, "光谱测量值");
            _ = LumFourColorDataChecks.SpectrumWarning(result.PeakAd);
            for (int i = 0; i < result.Spectrum.Count; i++)
            {
                if (result.Spectrum[i].Wavelength <= 0 || (i > 0 && result.Spectrum[i].Wavelength <= result.Spectrum[i - 1].Wavelength))
                    throw new InvalidOperationException("光谱波长必须为正数并严格递增，不能重复或错序。");
            }

            ReferenceY = result.Measurement.Y;
            ReferenceCieX = result.Measurement.CieX;
            ReferenceCieY = result.Measurement.CieY;
            Spectrum = Array.AsReadOnly(result.Spectrum.ToArray());
            SpectrumResultId = result.ResultId;
            SpectrumCapturedAt = result.CapturedAt == default ? null : result.CapturedAt;
            SpectrumPeakAd = result.PeakAd;
            SpectrumIntegrationTime = result.IntegrationTime;
            SpectrumNdPort = result.NdPort;
            SpectrumSource = result.Source;
            acquiredReference = result.Measurement;
            SetReferenceInputs(result.Measurement);
            RaiseStateChanged();
        }

        private void SetReferenceInputs(ColorCorrectionYxy? values)
        {
            referenceYInput = values?.Y.ToString("R", CultureInfo.CurrentCulture) ?? string.Empty;
            referenceCieXInput = values?.CieX.ToString("R", CultureInfo.CurrentCulture) ?? string.Empty;
            referenceCieYInput = values?.CieY.ToString("R", CultureInfo.CurrentCulture) ?? string.Empty;
            OnPropertyChanged(nameof(ReferenceYInput));
            OnPropertyChanged(nameof(ReferenceCieXInput));
            OnPropertyChanged(nameof(ReferenceCieYInput));
        }

        private void UpdateManualReference()
        {
            IsReferenceEdited = true;
            ReferenceY = ReferenceCieX = ReferenceCieY = null;
            ReferenceInputError = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(referenceYInput) || string.IsNullOrWhiteSpace(referenceCieXInput) || string.IsNullOrWhiteSpace(referenceCieYInput))
                    throw new InvalidOperationException("请完整填写参考 Y、CIE x、CIE y。");
                var value = new ColorCorrectionYxy(ParseInputNumber(referenceYInput, "光谱 Y"), ParseInputNumber(referenceCieXInput, "光谱 x"), ParseInputNumber(referenceCieYInput, "光谱 y"));
                LumFourColorDataChecks.ValidateYxy(value, "手动参考值");
                ReferenceY = value.Y;
                ReferenceCieX = value.CieX;
                ReferenceCieY = value.CieY;
            }
            catch (InvalidOperationException ex) { ReferenceInputError = ex.Message; }
            RaiseStateChanged();
        }

        private static double ParseInputNumber(string text, string name)
        {
            if ((!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
                && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) || !double.IsFinite(value))
                throw new InvalidOperationException($"{name} 必须是有限数值。");
            return value;
        }

        public void RestoreReference()
        {
            if (acquiredReference is not ColorCorrectionYxy original) return;
            ReferenceY = original.Y;
            ReferenceCieX = original.CieX;
            ReferenceCieY = original.CieY;
            IsReferenceEdited = false;
            ReferenceInputError = string.Empty;
            SetReferenceInputs(original);
            RaiseStateChanged();
        }

        public void ClearCameraMeasurement()
        {
            Poi = null;
            acquiredCamera = null;
            IsCameraEdited = false;
            CameraInputError = string.Empty;
            SetCameraInputs(null);
            CameraX = CameraY = CameraZ = CameraCieX = CameraCieY = null;
            RaiseStateChanged();
        }

        public void ClearCamera()
        {
            Frame = null;
            Preview = null;
            ClearCameraMeasurement();
        }

        public void ClearSpectrum()
        {
            ReferenceY = ReferenceCieX = ReferenceCieY = null;
            acquiredReference = null;
            IsReferenceEdited = false;
            ReferenceInputError = string.Empty;
            SetReferenceInputs(null);
            Spectrum = Array.Empty<ColorCorrectionSpectrumPoint>();
            SpectrumResultId = null;
            SpectrumCapturedAt = null;
            SpectrumPeakAd = SpectrumIntegrationTime = null;
            SpectrumNdPort = null;
            SpectrumSource = string.Empty;
            RaiseStateChanged();
        }

        internal IReadOnlyList<string> GetWarnings(string sourceHash)
        {
            List<string> warnings = new();
            if (IsCameraEdited)
                warnings.Add($"{Name}：相机 Y / x / y 已手动录入或修改，请核对数值与原校正文件");
            else if (Frame?.CalibrationHash == null)
                warnings.Add($"{Name}：导入图像未记录校正模板，请核对使用的是当前原校正文件");
            else if (Frame.CalibrationHash != sourceHash)
                warnings.Add($"{Name}：图像使用的四色校正文件与当前原文件不一致");
            if (HasSpectrumMeasurement && IsReferenceEdited)
                warnings.Add($"{Name}：参考 Y / x / y 已手动录入或修改，请核对数值、色块和测量来源；原始记录的 IP 不能验证手动参考值");
            if (HasSpectrumMeasurement && !IsReferenceEdited && LumFourColorDataChecks.SpectrumWarning(SpectrumPeakAd) is string warning)
                warnings.Add($"{Name}：{warning}");
            if (HasSpectrumMeasurement && !IsReferenceEdited && !SpectrumCapturedAt.HasValue)
                warnings.Add($"{Name}：缺少光谱采集时间，请核对是否为当前色块的数据");
            return warnings;
        }

        public ColorCorrectionMeasurement CreateMeasurement()
        {
            if (!HasCameraMeasurement || !HasSpectrumMeasurement)
                throw new InvalidOperationException($"{Name} 的相机或光谱数据尚未完成。");

            return new ColorCorrectionMeasurement(
                new ColorCorrectionYxy(CameraY!.Value, CameraCieX!.Value, CameraCieY!.Value),
                new ColorCorrectionYxy(ReferenceY!.Value, ReferenceCieX!.Value, ReferenceCieY!.Value),
                IsReferenceEdited ? null : Spectrum);
        }

        private void RaiseStateChanged()
        {
            OnPropertyChanged(nameof(Frame));
            OnPropertyChanged(nameof(Preview));
            OnPropertyChanged(nameof(Poi));
            OnPropertyChanged(nameof(CameraX));
            OnPropertyChanged(nameof(CameraY));
            OnPropertyChanged(nameof(CameraZ));
            OnPropertyChanged(nameof(CameraCieX));
            OnPropertyChanged(nameof(CameraCieY));
            OnPropertyChanged(nameof(IsCameraEdited));
            OnPropertyChanged(nameof(CameraInputError));
            OnPropertyChanged(nameof(CanRestoreCamera));
            OnPropertyChanged(nameof(CameraQuality));
            OnPropertyChanged(nameof(CameraGain));
            OnPropertyChanged(nameof(CameraExposure));
            OnPropertyChanged(nameof(ReferenceY));
            OnPropertyChanged(nameof(ReferenceCieX));
            OnPropertyChanged(nameof(ReferenceCieY));
            OnPropertyChanged(nameof(IsReferenceEdited));
            OnPropertyChanged(nameof(ReferenceInputError));
            OnPropertyChanged(nameof(CanRestoreReference));
            OnPropertyChanged(nameof(SpectrumSourceDescription));
            OnPropertyChanged(nameof(SpectrumMetadataHeading));
            OnPropertyChanged(nameof(SpectrumDetailsHeading));
            OnPropertyChanged(nameof(Spectrum));
            OnPropertyChanged(nameof(SpectrumResultId));
            OnPropertyChanged(nameof(SpectrumCapturedAt));
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(HasCameraMeasurement));
            OnPropertyChanged(nameof(HasSpectrumMeasurement));
            OnPropertyChanged(nameof(IsComplete));
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(CameraSource));
            OnPropertyChanged(nameof(CameraState));
            OnPropertyChanged(nameof(PoiDescription));
            OnPropertyChanged(nameof(SpectrumSource));
            OnPropertyChanged(nameof(SpectrumPeakAd));
            OnPropertyChanged(nameof(SpectrumIpPercent));
            OnPropertyChanged(nameof(SpectrumIntegrationTime));
            OnPropertyChanged(nameof(SpectrumNdPort));
            OnPropertyChanged(nameof(SpectrumQuality));
            OnPropertyChanged(nameof(SpectrumState));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public sealed class LumFourColorCalibrationSession
    {
        public ObservableCollection<LumFourColorCalibrationSample> Samples { get; } = new();
        public LumFourColorCorrectionMode Mode { get; private set; }
        public bool IsSinglePoint => Mode == LumFourColorCorrectionMode.SinglePoint;
        public bool IsComplete => Samples.Count > 0 && Samples.All(sample => sample.IsComplete);

        public void SetMode(LumFourColorCorrectionMode mode)
        {
            if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            Mode = mode;
            Samples.Clear();
            if (IsSinglePoint)
            {
                Samples.Add(new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.SinglePoint));
                return;
            }

            Samples.Add(new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.Red));
            Samples.Add(new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.Green));
            Samples.Add(new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.Blue));
            if (mode == LumFourColorCorrectionMode.MatlabRgbw)
                Samples.Add(new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.White));
        }

        public CVRawManualCieConfig Calculate(CVRawManualCieConfig source)
        {
            if (!IsComplete)
                throw new InvalidOperationException("请先完成全部相机和光谱采集。");

            var duplicate = Samples.Where(sample => sample.SpectrumResultId > 0 && !sample.IsReferenceEdited)
                .GroupBy(sample => (sample.SpectrumSource, sample.SpectrumResultId))
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
                throw new InvalidOperationException($"{string.Join("、", duplicate.Select(sample => sample.Name))} 使用了同一条光谱结果，请为每个色块选择对应的测量数据。");

            if (IsSinglePoint)
                return LumFourColorCorrectionCalculator.CorrectSinglePoint(source, Samples[0].CreateMeasurement());

            if (Mode == LumFourColorCorrectionMode.PythonRgb)
                return LumFourColorCorrectionCalculator.CorrectPythonRgb(Samples[0].CreateMeasurement(), Samples[1].CreateMeasurement(), Samples[2].CreateMeasurement());

            return LumFourColorCorrectionCalculator.CorrectFourColor(source, new LumFourColorCorrectionMeasurements(
                Samples[0].CreateMeasurement(),
                Samples[1].CreateMeasurement(),
                Samples[2].CreateMeasurement(),
                Samples[3].CreateMeasurement()));
        }
    }

    internal static class LumFourColorCieService
    {
        public static LumFourColorCieCapture Load(string filePath)
        {
            if (!CVFileUtil.IsCIEFile(filePath))
                throw new InvalidOperationException("请选择 CVCIE 文件。");

            if (!CVFileUtil.Read(filePath, out CVCIEFile file))
                throw new InvalidOperationException("无法读取 CVCIE 文件。");
            using (file)
            {
                byte[] data = file.Data?.ToArray() ?? throw new InvalidOperationException("CVCIE 文件没有图像数据。");
                return new LumFourColorCieCapture(data, file.Cols, file.Rows, file.Bpp, file.Channels, file.Gain, file.Exp?.ToArray() ?? Array.Empty<float>()) { Source = System.IO.Path.GetFullPath(filePath) };
            }
        }

        public static WriteableBitmap Render(LumFourColorCieCapture frame)
        {
            using CVCIEFile file = new()
            {
                Data = frame.Data,
                Cols = frame.Width,
                Rows = frame.Height,
                Bpp = frame.BitsPerChannel,
                Channels = frame.Channels,
                Gain = frame.Gain,
                Exp = frame.Exposure,
                FileExtType = CVType.CIE,
            };
            return CvcieSrgbRenderer.Render(file, CvcieBrightnessMode.Auto, 1d);
        }

        public static PoiMeasurementResult Measure(LumFourColorCieCapture frame, PoiMeasurementPoint poi)
        {
            using PoiMeasurementBuffer buffer = new(frame.Data, frame.Width, frame.Height, frame.BitsPerChannel, frame.Channels);
            return PoiMeasurementService.CalculateRaw(buffer, new[] { poi })[0];
        }
    }
}
