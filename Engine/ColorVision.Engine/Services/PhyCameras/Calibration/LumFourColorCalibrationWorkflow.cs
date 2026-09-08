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
        float[] Exposure);

    public sealed record LumFourColorSpectrumCapture(
        ColorCorrectionYxy Measurement,
        IReadOnlyList<ColorCorrectionSpectrumPoint> Spectrum,
        int ResultId,
        DateTimeOffset CapturedAt);

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
                return new LumFourColorCieCapture(
                    data,
                    lease.Metadata.Width,
                    lease.Metadata.Height,
                    lease.Metadata.CieBpp,
                    lease.Metadata.Channels,
                    lease.Metadata.Gain,
                    lease.Metadata.Exposure.ToArray());
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
            ColorCorrectionSpectrumPoint[] spectrum = result.Spectrum
                .Select(point => new ColorCorrectionSpectrumPoint(point.Wavelength, point.Value))
                .ToArray();
            return new LumFourColorSpectrumCapture(
                new ColorCorrectionYxy(result.Y, result.CieX, result.CieY),
                spectrum,
                result.ResultId,
                result.CapturedAt);
        }
    }

    public sealed class LumFourColorCalibrationSample : ViewModelBase
    {
        public LumFourColorCalibrationSample(LumFourColorCorrectionTarget target)
        {
            Target = target;
        }

        public LumFourColorCorrectionTarget Target { get; }
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
        public float? CameraGain => Frame?.Gain;
        public string CameraExposure => Frame == null
            ? string.Empty
            : string.Join(" / ", Frame.Exposure.Select(value => value.ToString("G7", CultureInfo.CurrentCulture)));
        public double? ReferenceY { get; private set; }
        public double? ReferenceCieX { get; private set; }
        public double? ReferenceCieY { get; private set; }
        public IReadOnlyList<ColorCorrectionSpectrumPoint> Spectrum { get; private set; } = Array.Empty<ColorCorrectionSpectrumPoint>();
        public int? SpectrumResultId { get; private set; }
        public DateTimeOffset? SpectrumCapturedAt { get; private set; }
        public bool HasImage => Frame != null;
        public bool HasCameraMeasurement => CameraY.HasValue && CameraCieX.HasValue && CameraCieY.HasValue;
        public bool HasSpectrumMeasurement => ReferenceY.HasValue && ReferenceCieX.HasValue && ReferenceCieY.HasValue && Spectrum.Count > 0;
        public bool IsComplete => HasCameraMeasurement && HasSpectrumMeasurement;
        public string Progress => IsComplete
            ? "已完成"
            : HasCameraMeasurement
                ? "待采集光谱"
                : HasSpectrumMeasurement
                    ? HasImage ? "待绘制 POI" : "待相机取图"
                    : HasImage ? "待 POI / 光谱" : "待相机 / 光谱";

        public void SetFrame(LumFourColorCieCapture frame, WriteableBitmap preview)
        {
            Frame = frame;
            Preview = preview;
            Poi = null;
            CameraX = CameraY = CameraZ = CameraCieX = CameraCieY = null;
            RaiseStateChanged();
        }

        public void SetCameraMeasurement(PoiMeasurementPoint poi, PoiMeasurementResult result)
        {
            if (!float.IsFinite(result.X) || !float.IsFinite(result.Y) || !float.IsFinite(result.Z) ||
                !float.IsFinite(result.ChromaX) || !float.IsFinite(result.ChromaY))
            {
                throw new InvalidOperationException("POI 返回的 XYZ 或 CIE x/y 无效。");
            }

            Poi = poi;
            CameraX = result.X;
            CameraY = result.Y;
            CameraZ = result.Z;
            CameraCieX = result.ChromaX;
            CameraCieY = result.ChromaY;
            RaiseStateChanged();
        }

        public void SetSpectrumMeasurement(LumFourColorSpectrumCapture result)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (!double.IsFinite(result.Measurement.Y) || !double.IsFinite(result.Measurement.CieX) ||
                !double.IsFinite(result.Measurement.CieY) || result.Spectrum == null || result.Spectrum.Count == 0 ||
                result.Spectrum.Any(point => !double.IsFinite(point.Wavelength) || !double.IsFinite(point.Value)))
            {
                throw new InvalidOperationException("光谱仪返回的 Y、CIE x/y 或光谱数据无效。");
            }

            ReferenceY = result.Measurement.Y;
            ReferenceCieX = result.Measurement.CieX;
            ReferenceCieY = result.Measurement.CieY;
            Spectrum = result.Spectrum;
            SpectrumResultId = result.ResultId;
            SpectrumCapturedAt = result.CapturedAt;
            RaiseStateChanged();
        }

        public ColorCorrectionMeasurement CreateMeasurement()
        {
            if (!HasCameraMeasurement || !HasSpectrumMeasurement)
                throw new InvalidOperationException($"{Name} 的相机或光谱数据尚未完成。");

            return new ColorCorrectionMeasurement(
                new ColorCorrectionYxy(CameraY!.Value, CameraCieX!.Value, CameraCieY!.Value),
                new ColorCorrectionYxy(ReferenceY!.Value, ReferenceCieX!.Value, ReferenceCieY!.Value),
                Spectrum);
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
            OnPropertyChanged(nameof(CameraGain));
            OnPropertyChanged(nameof(CameraExposure));
            OnPropertyChanged(nameof(ReferenceY));
            OnPropertyChanged(nameof(ReferenceCieX));
            OnPropertyChanged(nameof(ReferenceCieY));
            OnPropertyChanged(nameof(Spectrum));
            OnPropertyChanged(nameof(SpectrumResultId));
            OnPropertyChanged(nameof(SpectrumCapturedAt));
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(HasCameraMeasurement));
            OnPropertyChanged(nameof(HasSpectrumMeasurement));
            OnPropertyChanged(nameof(IsComplete));
            OnPropertyChanged(nameof(Progress));
        }
    }

    public sealed class LumFourColorCalibrationSession
    {
        public ObservableCollection<LumFourColorCalibrationSample> Samples { get; } = new();
        public bool IsSinglePoint { get; private set; }
        public bool IsComplete => Samples.Count > 0 && Samples.All(sample => sample.IsComplete);

        public void SetMode(bool singlePoint)
        {
            IsSinglePoint = singlePoint;
            Samples.Clear();
            if (singlePoint)
            {
                Samples.Add(new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.SinglePoint));
                return;
            }

            Samples.Add(new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.Red));
            Samples.Add(new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.Green));
            Samples.Add(new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.Blue));
            Samples.Add(new LumFourColorCalibrationSample(LumFourColorCorrectionTarget.White));
        }

        public CVRawManualCieConfig Calculate(CVRawManualCieConfig source)
        {
            if (!IsComplete)
                throw new InvalidOperationException("请先完成全部相机和光谱采集。");

            if (IsSinglePoint)
                return LumFourColorCorrectionCalculator.CorrectSinglePoint(source, Samples[0].CreateMeasurement());

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
                return new LumFourColorCieCapture(data, file.Cols, file.Rows, file.Bpp, file.Channels, file.Gain, file.Exp?.ToArray() ?? Array.Empty<float>());
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
