using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    /// <summary>
    /// Applies a camera calibration template to a process-local RAW buffer.
    /// Basic correction-only templates return corrected RAW; templates containing one
    /// luminance/color item return CIE without mutating the upstream frame.
    /// </summary>
    internal static class LocalFrameCalibrationService
    {
        public static LocalFlowFrame Calibrate(
            LocalFlowFrameLease source,
            LocalCalibrationCacheManager cacheManager,
            IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles,
            string calibrationTemplate)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(cacheManager);
            ArgumentNullException.ThrowIfNull(calibrationFiles);
            ValidateSource(source);
            CalibrationPlan plan = CreatePlan(
                source.Metadata.Width,
                source.Metadata.Height,
                source.Metadata.Channels,
                calibrationFiles,
                calibrationTemplate);
            int rawLength = GetExpectedRawLength(source);
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
                PrimaryBufferKind = plan.GeneratesCie ? LocalFrameBufferKind.CvCie : LocalFrameBufferKind.CvRaw,
                FlipMode = source.Metadata.FlipMode,
                IsMirrorReady = true
            };
            LocalFlowFrame result = LocalFlowFrame.Allocate(metadata, plan.GeneratesCie ? 0 : rawLength, plan.CieLength);
            try
            {
                using (LocalFlowFrameLease destination = result.Acquire())
                {
                    float[] exposure = plan.GeneratesCie ? NormalizeExposure(source.Metadata.Exposure) : Array.Empty<float>();
                    cacheManager.ExecuteFromSource(
                        new LocalCalibrationLayout(source.Metadata.Width, source.Metadata.Height, source.Metadata.SourceBpp, source.Metadata.Channels),
                        calibrationFiles,
                        source.RawPointer,
                        plan.GeneratesCie ? IntPtr.Zero : destination.RawPointer,
                        destination.CiePointer,
                        exposure);
                }
                LocalFrameMirrorService.ApplyPending(result);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Applies calibration to a frame captured directly into process-local memory.
        /// Color templates preserve the captured RAW and write CIE beside it; basic-only
        /// templates correct the captured RAW in place to avoid another full-frame copy.
        /// </summary>
        public static void CalibrateCapturedFrame(
            LocalFlowFrameLease frame,
            LocalCalibrationCacheManager cacheManager,
            IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles,
            string calibrationTemplate)
        {
            ArgumentNullException.ThrowIfNull(frame);
            ArgumentNullException.ThrowIfNull(cacheManager);
            ArgumentNullException.ThrowIfNull(calibrationFiles);
            ValidateSource(frame);
            CalibrationPlan plan = CreatePlan(
                frame.Metadata.Width,
                frame.Metadata.Height,
                frame.Metadata.Channels,
                calibrationFiles,
                calibrationTemplate);
            LocalCalibrationLayout layout = new(
                frame.Metadata.Width,
                frame.Metadata.Height,
                frame.Metadata.SourceBpp,
                frame.Metadata.Channels);

            if (plan.GeneratesCie)
            {
                if (frame.CiePointer == IntPtr.Zero || frame.CieLength < plan.CieLength)
                {
                    throw new InvalidOperationException($"CIE buffer is too small: required {plan.CieLength} bytes, actual {frame.CieLength} bytes.");
                }
                cacheManager.ExecuteFromSource(
                    layout,
                    calibrationFiles,
                    frame.RawPointer,
                    IntPtr.Zero,
                    frame.CiePointer,
                    NormalizeExposure(frame.Metadata.Exposure));
                return;
            }

            cacheManager.Execute(layout, calibrationFiles, frame.RawPointer, IntPtr.Zero, Array.Empty<float>());
        }

        public static int GetRequiredCieLength(
            int width,
            int height,
            int sourceChannels,
            IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles,
            string calibrationTemplate)
            => CreatePlan(width, height, sourceChannels, calibrationFiles, calibrationTemplate).CieLength;

        private static void ValidateSource(LocalFlowFrameLease source)
        {
            if (source.Metadata.FlipMode != FlowEngineLib.Algorithm.CVImageFlipMode.None
                && source.IsRawFlipApplied)
            {
                throw new InvalidOperationException("The RAW frame has already been mirrored and can no longer be used with sensor-coordinate spatial calibration maps.");
            }
            if (!source.HasRaw) throw new InvalidOperationException("当前本地帧没有 RAW 内存，无法执行校正。");
            if (source.RawPointer == IntPtr.Zero) throw new InvalidOperationException("当前本地帧的 RAW 指针无效。");
            if (source.Metadata.Width <= 0 || source.Metadata.Height <= 0) throw new InvalidOperationException("当前本地帧的图像尺寸无效。");
            if (source.Metadata.SourceBpp is not (8 or 16)) throw new NotSupportedException($"本地校正仅支持 8 位或 16 位 RAW，当前位深：{source.Metadata.SourceBpp}。");
            if (source.Metadata.Channels is not (1 or 3)) throw new NotSupportedException($"本地校正仅支持单通道或三通道 RAW，当前通道数：{source.Metadata.Channels}。");
            int expectedLength = GetExpectedRawLength(source);
            if (expectedLength > source.RawLength) throw new InvalidOperationException($"RAW 内存长度不足：需要 {expectedLength} 字节，实际 {source.RawLength} 字节。");
        }

        private static int GetExpectedRawLength(LocalFlowFrameLease source)
            => checked((source.Metadata.SourceBpp / 8) * source.Metadata.Width * source.Metadata.Height * source.Metadata.Channels);

        private static CalibrationPlan CreatePlan(
            int width,
            int height,
            int sourceChannels,
            IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles,
            string calibrationTemplate)
        {
            if (calibrationFiles.Count == 0)
            {
                throw new InvalidOperationException($"校正模板“{calibrationTemplate}”没有选择任何校正文件。");
            }

            DeviceCameraCalibrationFile[] colorFiles = calibrationFiles.Where(file => IsColorCalibration(file.CalibrationType)).ToArray();
            if (colorFiles.Length > 1)
            {
                throw new InvalidOperationException($"校正模板“{calibrationTemplate}”同时选择了多个亮度/颜色校正文件，本地校正只能使用一个。");
            }
            if (width <= 0 || height <= 0)
            {
                throw new InvalidOperationException($"Invalid calibration image size: {width}x{height}.");
            }
            if (colorFiles.Length == 0) return new CalibrationPlan(false, 0);

            int cieChannels = GetCieChannelCount(colorFiles[0], sourceChannels);
            return new CalibrationPlan(true, checked(4 * width * height * cieChannels));
        }

        private static float[] NormalizeExposure(float[] exposure)
        {
            if (exposure == null || exposure.Length == 0) throw new InvalidOperationException("RAW 图像没有曝光时间，无法执行亮度/颜色校正。");
            float[] normalized = new float[3];
            for (int index = 0; index < normalized.Length; index++)
            {
                normalized[index] = exposure[Math.Min(index, exposure.Length - 1)];
                if (!float.IsFinite(normalized[index]) || normalized[index] <= 0)
                {
                    throw new InvalidOperationException($"RAW 图像曝光时间必须是有限正数，通道 {index} 的值为 {normalized[index]}。");
                }
            }
            return normalized;
        }

        private static bool IsColorCalibration(CalibrationType type)
            => type is CalibrationType.Luminance or CalibrationType.LumOneColor or CalibrationType.LumFourColor or CalibrationType.LumMultiColor;

        private static int GetCieChannelCount(DeviceCameraCalibrationFile colorFile, int sourceChannels)
        {
            int requiredChannels = colorFile.CalibrationType == CalibrationType.Luminance ? 1 : 3;
            if (sourceChannels != requiredChannels)
            {
                string calibrationName = colorFile.CalibrationType == CalibrationType.Luminance ? "亮度校正" : "单色/四色/多色校正";
                throw new InvalidOperationException($"{calibrationName}“{colorFile.DisplayName}”要求 {requiredChannels} 通道 RAW，当前为 {sourceChannels} 通道。");
            }
            return requiredChannels;
        }

        private readonly record struct CalibrationPlan(bool GeneratesCie, int CieLength);
    }
}
