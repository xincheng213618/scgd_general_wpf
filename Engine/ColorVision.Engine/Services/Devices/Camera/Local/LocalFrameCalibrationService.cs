using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

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
            if (calibrationFiles.Count == 0)
            {
                throw new InvalidOperationException($"校正模板“{calibrationTemplate}”没有选择任何校正文件。");
            }

            DeviceCameraCalibrationFile[] colorFiles = calibrationFiles.Where(file => IsColorCalibration(file.CalibrationType)).ToArray();
            if (colorFiles.Length > 1)
            {
                throw new InvalidOperationException($"校正模板“{calibrationTemplate}”同时选择了多个亮度/颜色校正文件，本地校正只能使用一个。");
            }

            bool generatesCie = colorFiles.Length == 1;
            bool hasBasicCorrection = calibrationFiles.Any(file => !IsColorCalibration(file.CalibrationType));
            int rawLength = GetExpectedRawLength(source);
            int cieLength = generatesCie
                ? checked(4 * source.Metadata.Width * source.Metadata.Height * source.Metadata.Channels)
                : 0;
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
                PrimaryBufferKind = generatesCie ? LocalFrameBufferKind.CvCie : LocalFrameBufferKind.CvRaw
            };
            LocalFlowFrame result = LocalFlowFrame.Allocate(metadata, generatesCie ? 0 : rawLength, cieLength);
            IntPtr temporaryRaw = IntPtr.Zero;
            try
            {
                using LocalFlowFrameLease destination = result.Acquire();
                IntPtr workingRaw = source.RawPointer;
                if (hasBasicCorrection)
                {
                    if (generatesCie)
                    {
                        temporaryRaw = Marshal.AllocHGlobal(rawLength);
                        if (temporaryRaw == IntPtr.Zero) throw new OutOfMemoryException("分配本地校正 RAW 临时缓冲区失败。");
                        workingRaw = temporaryRaw;
                    }
                    else
                    {
                        workingRaw = destination.RawPointer;
                    }
                    CopyMemory(source.RawPointer, workingRaw, rawLength);
                }

                float[] exposure = generatesCie ? NormalizeExposure(source.Metadata.Exposure) : Array.Empty<float>();
                cacheManager.Execute(
                    new LocalCalibrationLayout(source.Metadata.Width, source.Metadata.Height, source.Metadata.SourceBpp, source.Metadata.Channels),
                    calibrationFiles,
                    workingRaw,
                    destination.CiePointer,
                    exposure);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
            finally
            {
                if (temporaryRaw != IntPtr.Zero) Marshal.FreeHGlobal(temporaryRaw);
            }
        }

        private static void ValidateSource(LocalFlowFrameLease source)
        {
            if (!source.HasRaw) throw new InvalidOperationException("当前本地帧没有 RAW 内存，无法执行校正。");
            if (source.RawPointer == IntPtr.Zero) throw new InvalidOperationException("当前本地帧的 RAW 指针无效。");
            if (source.Metadata.Width <= 0 || source.Metadata.Height <= 0) throw new InvalidOperationException("当前本地帧的图像尺寸无效。");
            if (source.Metadata.SourceBpp <= 0 || source.Metadata.SourceBpp % 8 != 0) throw new InvalidOperationException("当前本地帧的位深无效。");
            if (source.Metadata.Channels is not (1 or 3)) throw new NotSupportedException($"本地校正仅支持单通道或三通道 RAW，当前通道数：{source.Metadata.Channels}。");
            int expectedLength = GetExpectedRawLength(source);
            if (expectedLength > source.RawLength) throw new InvalidOperationException($"RAW 内存长度不足：需要 {expectedLength} 字节，实际 {source.RawLength} 字节。");
        }

        private static int GetExpectedRawLength(LocalFlowFrameLease source)
            => checked((source.Metadata.SourceBpp / 8) * source.Metadata.Width * source.Metadata.Height * source.Metadata.Channels);

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

        private static unsafe void CopyMemory(IntPtr source, IntPtr destination, int length)
        {
            Buffer.MemoryCopy(source.ToPointer(), destination.ToPointer(), length, length);
        }

        private static bool IsColorCalibration(CalibrationType type)
            => type is CalibrationType.Luminance or CalibrationType.LumOneColor or CalibrationType.LumFourColor or CalibrationType.LumMultiColor;
    }
}
