using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    /// <summary>
    /// Applies a camera calibration template directly to a process-local unmanaged RAW buffer.
    /// Normal corrections are in-place; the generated CIE data is written to a new unmanaged buffer.
    /// </summary>
    internal static class LocalFrameCalibrationService
    {
        private static readonly SemaphoreSlim CalibrationLock = new(1, 1);

        public static LocalFlowFrame Calibrate(
            LocalFlowFrameLease source,
            IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles,
            string calibrationTemplate)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(calibrationFiles);
            ValidateSource(source);

            DeviceCameraCalibrationFile[] colorFiles = calibrationFiles.Where(file => IsColorCalibration(file.CalibrationType)).ToArray();
            if (colorFiles.Length == 0)
            {
                throw new InvalidOperationException($"校正模板“{calibrationTemplate}”没有选择亮度/颜色校正文件，无法生成 CIE 内存。");
            }
            if (colorFiles.Length > 1)
            {
                throw new InvalidOperationException($"校正模板“{calibrationTemplate}”同时选择了多个亮度/颜色校正文件，本地校正只能使用一个。");
            }

            int cieLength = checked(4 * source.Metadata.Width * source.Metadata.Height * source.Metadata.Channels);
            LocalFrameMetadata metadata = new()
            {
                Width = source.Metadata.Width,
                Height = source.Metadata.Height,
                SourceBpp = source.Metadata.SourceBpp,
                CieBpp = 32,
                Channels = source.Metadata.Channels,
                Gain = source.Metadata.Gain,
                Exposure = source.Metadata.Exposure.ToArray(),
                DeviceCode = source.Metadata.DeviceCode,
                SourceFilePath = source.Metadata.SourceFilePath,
                CalibrationTemplate = calibrationTemplate,
                CaptureTime = source.Metadata.CaptureTime,
                PrimaryBufferKind = LocalFrameBufferKind.CvCie
            };
            LocalFlowFrame result = LocalFlowFrame.Allocate(metadata, 0, cieLength);
            try
            {
                CalibrationLock.Wait();
                try
                {
                    using LocalFlowFrameLease destination = result.Acquire();
                    CalibrateCore(source, destination.CiePointer, calibrationFiles, colorFiles[0]);
                }
                finally
                {
                    CalibrationLock.Release();
                }
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        private static void CalibrateCore(
            LocalFlowFrameLease source,
            IntPtr destination,
            IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles,
            DeviceCameraCalibrationFile colorFile)
        {
            IntPtr handle = cvCameraCSLib.CreatCalibrationManage();
            if (handle == IntPtr.Zero) throw new InvalidOperationException("创建本地校正上下文失败。");
            try
            {
                foreach (DeviceCameraCalibrationFile file in calibrationFiles)
                {
                    if (!IsSupported(file.CalibrationType))
                    {
                        throw new NotSupportedException($"本地指针校正暂不支持校正项：{file.DisplayName}（{file.CalibrationType}）。");
                    }
                    if (cvCameraCSLib.CM_SetCalibParam(handle, file.CalibrationType, true, file.FullPath) != 1)
                    {
                        throw new InvalidOperationException($"加载校正文件失败：{file.DisplayName}（{file.FullPath}）。");
                    }
                }

                int width = source.Metadata.Width;
                int height = source.Metadata.Height;
                int bpp = source.Metadata.SourceBpp;
                uint channels = checked((uint)source.Metadata.Channels);
                foreach (DeviceCameraCalibrationFile file in calibrationFiles)
                {
                    if (IsColorCalibration(file.CalibrationType)) continue;
                    bool success = file.CalibrationType switch
                    {
                        CalibrationType.DarkNoise => cvCameraCSLib.CM_SCGD_SDP_DarkNoise(handle, width, height, bpp, channels, source.RawPointer),
                        CalibrationType.DefectWPoint => cvCameraCSLib.CM_SCGD_SDP_DefectWPoint(handle, width, height, bpp, channels, source.RawPointer),
                        CalibrationType.DefectBPoint => cvCameraCSLib.CM_SCGD_SDP_DefectBPoint(handle, width, height, bpp, channels, source.RawPointer),
                        CalibrationType.DefectPoint => cvCameraCSLib.CM_SCGD_SDP_DefectPoint(handle, width, height, bpp, channels, source.RawPointer),
                        CalibrationType.DSNU => cvCameraCSLib.CM_SCGD_SDP_DSNU(handle, width, height, bpp, channels, source.RawPointer),
                        CalibrationType.Uniformity => cvCameraCSLib.CM_SCGD_SDP_Uniformity(handle, width, height, bpp, channels, source.RawPointer),
                        CalibrationType.Distortion => cvCameraCSLib.CM_SCGD_SDP_Distortion(handle, width, height, bpp, channels, source.RawPointer),
                        CalibrationType.ColorShift => cvCameraCSLib.CM_SCGD_SDP_ColorShift(handle, width, height, bpp, channels, source.RawPointer),
                        CalibrationType.LineArity => cvCameraCSLib.CM_SCGD_SDP_LineArity(handle, width, height, bpp, channels, source.RawPointer),
                        CalibrationType.ColorDiff => cvCameraCSLib.CM_SCGD_SDP_ColorDiff(handle, width, height, bpp, channels, source.RawPointer),
                        CalibrationType.AngleShift => cvCameraCSLib.CM_SCGD_SDP_AngleShift(handle, width, height, bpp, channels, source.RawPointer),
                        _ => false
                    };
                    if (!success) throw new InvalidOperationException($"执行本地校正失败：{file.DisplayName}（{file.CalibrationType}）。");
                }

                float[] exposure = NormalizeExposure(source.Metadata.Exposure);
                bool colorSuccess = colorFile.CalibrationType switch
                {
                    CalibrationType.Luminance => cvCameraCSLib.CM_SCGD_SDP_Luminance(handle, checked((uint)width), checked((uint)height), bpp, channels, source.RawPointer, destination, exposure),
                    CalibrationType.LumOneColor => cvCameraCSLib.CM_SCGD_SDP_ColorOne(handle, checked((uint)width), checked((uint)height), bpp, channels, source.RawPointer, destination, exposure),
                    CalibrationType.LumFourColor => cvCameraCSLib.CM_SCGD_SDP_ColorFour(handle, checked((uint)width), checked((uint)height), bpp, channels, source.RawPointer, destination, exposure),
                    CalibrationType.LumMultiColor => cvCameraCSLib.CM_SCGD_SDP_ColorMulti(handle, checked((uint)width), checked((uint)height), bpp, channels, source.RawPointer, destination, exposure),
                    _ => false
                };
                if (!colorSuccess) throw new InvalidOperationException($"生成本地 CIE 内存失败：{colorFile.DisplayName}（{colorFile.CalibrationType}）。");
            }
            finally
            {
                _ = cvCameraCSLib.ReleaseCalibrationManage(handle);
            }
        }

        private static void ValidateSource(LocalFlowFrameLease source)
        {
            if (!source.HasRaw) throw new InvalidOperationException("当前本地帧没有 RAW 内存，无法执行校正。");
            if (source.RawPointer == IntPtr.Zero) throw new InvalidOperationException("当前本地帧的 RAW 指针无效。");
            if (source.Metadata.Width <= 0 || source.Metadata.Height <= 0) throw new InvalidOperationException("当前本地帧的图像尺寸无效。");
            if (source.Metadata.SourceBpp <= 0 || source.Metadata.SourceBpp % 8 != 0) throw new InvalidOperationException("当前本地帧的位深无效。");
            if (source.Metadata.Channels is not (1 or 3)) throw new NotSupportedException($"本地校正仅支持单通道或三通道 RAW，当前通道数：{source.Metadata.Channels}。");
            long expectedLength = (long)(source.Metadata.SourceBpp / 8) * source.Metadata.Width * source.Metadata.Height * source.Metadata.Channels;
            if (expectedLength > source.RawLength) throw new InvalidOperationException($"RAW 内存长度不足：需要 {expectedLength} 字节，实际 {source.RawLength} 字节。");
        }

        private static float[] NormalizeExposure(float[] exposure)
        {
            if (exposure == null || exposure.Length == 0) throw new InvalidOperationException("RAW 图像没有曝光时间，无法执行亮度/颜色校正。");
            float[] normalized = new float[3];
            for (int index = 0; index < normalized.Length; index++)
            {
                normalized[index] = exposure[Math.Min(index, exposure.Length - 1)];
            }
            return normalized;
        }

        private static bool IsColorCalibration(CalibrationType type)
            => type is CalibrationType.Luminance or CalibrationType.LumOneColor or CalibrationType.LumFourColor or CalibrationType.LumMultiColor;

        private static bool IsSupported(CalibrationType type)
            => type is CalibrationType.DarkNoise
                or CalibrationType.DefectWPoint
                or CalibrationType.DefectBPoint
                or CalibrationType.DefectPoint
                or CalibrationType.DSNU
                or CalibrationType.Uniformity
                or CalibrationType.Distortion
                or CalibrationType.ColorShift
                or CalibrationType.LineArity
                or CalibrationType.ColorDiff
                or CalibrationType.AngleShift
                or CalibrationType.Luminance
                or CalibrationType.LumOneColor
                or CalibrationType.LumFourColor
                or CalibrationType.LumMultiColor;
    }
}
