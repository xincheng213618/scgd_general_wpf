using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Core;
using cvColorVision;
using FlowEngineLib.Algorithm;
using ColorVision.Engine.Services.PhyCameras.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using ColorVision.ImageEditor.Tif;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal enum LocalColorCalibrationOutput
    {
        FullCie,
        Deferred
    }

    internal readonly record struct LocalCalibrationRoi(int X, int Y, int Width, int Height)
    {
        public bool IsConfigured => Width > 0 && Height > 0;

        public static LocalCalibrationRoi Resolve(PhyCameraCfg? cameraConfig, int imageWidth, int imageHeight)
            => FromCameraConfig(cameraConfig).ForImage(imageWidth, imageHeight);

        internal static LocalCalibrationRoi FromCameraConfig(PhyCameraCfg? config)
            => config == null ? default : new(config.PointX, config.PointY, config.Width, config.Height);

        internal static LocalCalibrationRoi ResolveForFrame(LocalFrameMetadata metadata, PhyCameraCfg? cameraConfig)
            => ResolveForFrame(metadata, FromCameraConfig(cameraConfig));

        internal static LocalCalibrationRoi ResolveForFrame(LocalFrameMetadata metadata, LocalCalibrationRoi fallbackRoi)
            => metadata.Acquisition?.SensorRoi is ImageSensorRoi captured
                ? new LocalCalibrationRoi(captured.X, captured.Y, captured.Width, captured.Height).ForImage(metadata.Width, metadata.Height)
                : fallbackRoi.ForImage(metadata.Width, metadata.Height);

        internal ImageSensorRoi ToAcquisitionRoi()
            => IsConfigured ? new(X, Y, Width, Height) : new(0, 0, 0, 0);

        private LocalCalibrationRoi ForImage(int imageWidth, int imageHeight)
        {
            if (!IsConfigured || Width != imageWidth || Height != imageHeight)
            {
                return default;
            }
            if (X < 0 || Y < 0)
            {
                throw new InvalidOperationException($"物理相机 ROI 起点不能为负数：({X},{Y})。");
            }
            return this;
        }
    }

    /// <summary>
    /// A revision of a template. Its ordered resources are independent of the frame layout, exposure and result requested.
    /// The device cache owns the native context so this object can be reused without holding a file or device handle.
    /// </summary>
    internal sealed class LocalPreparedCalibration
    {
        private readonly LocalCalibrationCacheManager cache;
        private readonly LocalCalibrationAsset[] assets;
        private readonly LocalCalibrationAsset[] basic;
        private readonly LocalCalibrationAsset? color;
        private readonly string template;
        private readonly uint inputDepth;

        internal LocalPreparedCalibration(LocalCalibrationCacheManager cache, string template, LocalCalibrationAsset[] assets, uint inputDepth)
        {
            this.cache = cache;
            this.template = template;
            this.assets = assets;
            this.inputDepth = inputDepth;
            color = assets.SingleOrDefault(file => LocalFrameCalibrationService.IsColorCalibration(file.CalibrationType));
            basic = color == null ? assets : assets.Where(file => file != color).ToArray();
        }

        public bool HasColor => color != null;
        public bool HasBasicCalibration => basic.Length != 0;

        public CalibrationOutputMode OutputFor(LocalColorCalibrationOutput preference, bool requireColor = false)
        {
            if (!HasColor)
            {
                if (requireColor) throw MissingColor();
                return CalibrationOutputMode.RawOnly;
            }
            return preference switch
            {
                LocalColorCalibrationOutput.FullCie => CalibrationOutputMode.FullCie,
                LocalColorCalibrationOutput.Deferred => CalibrationOutputMode.DeferredColor,
                _ => throw new ArgumentOutOfRangeException(nameof(preference))
            };
        }

        public void Apply(LocalFlowFrame frame, CalibrationOutputMode output, LocalCalibrationRoi fallbackRoi = default,
            IReadOnlyList<float>? zeroExposureFallback = null)
        {
            using var calibrationStage = FlowNodeTiming.Measure("Calibration");
            ArgumentNullException.ThrowIfNull(frame);
            if (output is not (CalibrationOutputMode.RawOnly or CalibrationOutputMode.FullCie or CalibrationOutputMode.DeferredColor))
                throw new ArgumentOutOfRangeException(nameof(output));
            bool useColor = output != CalibrationOutputMode.RawOnly;
            if (useColor && color == null) throw MissingColor();
            LocalFrameMetadata metadata = frame.Metadata;
            if (useColor)
            {
                int requiredChannels = color!.CalibrationType == CalibrationType.Luminance ? 1 : 3;
                if (metadata.Channels != requiredChannels)
                    throw new InvalidOperationException($"亮度/颜色校正“{color.DisplayName}”要求 {requiredChannels} 通道 RAW，当前为 {metadata.Channels} 通道。");
            }
            int cieLength = output == CalibrationOutputMode.FullCie
                ? checked(sizeof(float) * metadata.Width * metadata.Height * metadata.Channels) : 0;
            float[] effectiveExposure = useColor
                ? LocalFrameCalibrationService.ResolveExposureForCalibration(metadata.Exposure, zeroExposureFallback)
                : metadata.Exposure;
            float[] normalizedExposure = useColor ? LocalFrameCalibrationService.NormalizeExposure(effectiveExposure) : [];
            frame.ApplyCalibration(template, cieLength, HasBasicCalibration, effectiveExposure, (source, cie) =>
            {
                ValidateSource(source);
                var layout = LocalCalibrationLayout.FromFrame(source.Metadata);
                if (inputDepth == 8 && layout.Bpp == 16)
                    throw new InvalidOperationException($"校正模板“{template}”要求 8 位 RAW；输入为 16 位，无法无损转换。");
                var calibrationRoi = LocalCalibrationRoi.ResolveForFrame(source.Metadata, fallbackRoi);
                CVImageFlipMode flip = source.Metadata.FlipMode;
                LocalFrameMirrorService.ValidateFlipMode(flip);
                if (flip != CVImageFlipMode.None && source.IsRawFlipApplied && HasBasicCalibration)
                {
                    throw new InvalidOperationException("已翻转的 RAW 只能继续执行亮度/色度校正；不能再次执行常规或基于传感器坐标的空间校正。");
                }
                CVImageFlipMode rawOutputFlip = cieLength == 0 && !source.IsRawFlipApplied ? flip : CVImageFlipMode.None;
                LocalCalibrationExecution execution = cache.ExecutePrepared(useColor ? assets : basic, layout, source.RawPointer,
                    (ulong)source.RawLength, cie, (ulong)cieLength, normalizedExposure, calibrationRoi, output, rawOutputFlip);
                if (cieLength > 0 && !source.IsRawFlipApplied)
                {
                    FlowNodeTiming.Run("MirrorImage", () => LocalFrameMirrorService.FlipPlanarCie(
                        cie, cieLength, layout.Width, layout.Height, layout.Channels, flip));
                }
                else FlowNodeTiming.Skip("MirrorImage");

                ImageCalibrationSummary provenance = new()
                {
                    Template = template, CalibratedUtc = DateTime.UtcNow,
                    Backend = cache.BackendName, Sources = execution.Sources
                };
                ColorCalibrationSnapshot? snapshot = execution.Transform is RawColorTransformV1 transform
                    ? ColorCalibrationSnapshot.Create(transform, layout.Width, layout.Height, layout.Bpp,
                        normalizedExposure, template) with { Provenance = provenance }
                    : null;
                return new(snapshot, snapshot?.ToSummary() ?? provenance, rawOutputFlip != CVImageFlipMode.None);
            }, promoteRaw8To16: inputDepth == 16);
            calibrationStage?.Complete();
        }

        private InvalidOperationException MissingColor()
            => new($"校正模板“{template}”没有亮度/颜色校正模型，无法生成 Y/XYZ 或按需测量。");

        private static void ValidateSource(LocalFlowFrameLease source)
        {
            if (!source.HasRaw || source.RawPointer == IntPtr.Zero) throw new InvalidOperationException("当前本地帧没有 RAW 内存，无法执行校正。");
            if (source.IsBufferFlipFailed(LocalFrameBufferKind.CvRaw))
                throw new InvalidOperationException("RAW 镜像失败，当前帧不能继续校正。");
            if (source.Metadata.Width <= 0 || source.Metadata.Height <= 0) throw new InvalidOperationException("当前本地帧的图像尺寸无效。");
            if (source.Metadata.SourceBpp is not (8 or 16)) throw new NotSupportedException($"本地校正仅支持 8 位或 16 位 RAW，当前位深：{source.Metadata.SourceBpp}。");
            if (source.Metadata.Channels is not (1 or 3)) throw new NotSupportedException($"本地校正仅支持单通道或三通道 RAW，当前通道数：{source.Metadata.Channels}。");
            int length = checked((source.Metadata.SourceBpp / 8) * source.Metadata.Width * source.Metadata.Height * source.Metadata.Channels);
            if (length > source.RawLength) throw new InvalidOperationException($"RAW 内存长度不足：需要 {length} 字节，实际 {source.RawLength} 字节。");
        }
    }

    internal static class LocalFrameCalibrationService
    {
        internal static void ReuseColorCalibration(LocalFlowFrame frame, LocalColorCalibrationOutput output)
        {
            ColorCalibrationSnapshot snapshot = frame.ColorCalibration
                ?? throw new InvalidOperationException("切换色度输出模式需要带校正参数的 RAW 输入，不能使用仅有 CIE 的帧。");
            if (!frame.HasRaw || !snapshot.CanReplay)
                throw new InvalidOperationException("切换色度输出模式需要可回放的 RAW 输入。");
            int cieLength = output == LocalColorCalibrationOutput.FullCie
                ? checked(sizeof(float) * frame.Metadata.Width * frame.Metadata.Height * frame.Metadata.Channels)
                : 0;
            frame.ApplyCalibration(snapshot.Template, cieLength, false, null, (source, cie) =>
            {
                LocalFrameMetadata metadata = source.Metadata;
                LocalFrameMirrorService.ValidateFlipMode(metadata.FlipMode);
                if (cieLength > 0)
                {
                    RawColorTransformV1 transform = snapshot.ToNative();
                    int result = OpenCVMediaHelper.M_TransformRawColorV1(metadata.Width, metadata.Height, metadata.SourceBpp,
                        source.RawPointer, (ulong)source.RawLength, in transform, -1, cie, (ulong)cieLength / sizeof(float));
                    if (result != OpenCVCalibration.PoiOk) throw new InvalidOperationException($"生成本地 CIE 内存失败，错误码：{result}。");
                    if (!source.IsRawFlipApplied)
                        LocalFrameMirrorService.FlipPlanarCie(cie, cieLength, metadata.Width, metadata.Height,
                            metadata.Channels, metadata.FlipMode);
                }
                else if (!source.IsRawFlipApplied)
                {
                    LocalFrameMirrorService.FlipRaw(source.RawPointer, source.RawLength, metadata.Width, metadata.Height,
                        metadata.SourceBpp, metadata.Channels, metadata.FlipMode);
                }
                return new(snapshot, source.CalibrationProvenance, cieLength == 0 && !source.IsRawFlipApplied);
            });
        }

        internal static float[] ResolveExposureForCalibration(float[] exposure, IReadOnlyList<float>? zeroExposureFallback)
        {
            ArgumentNullException.ThrowIfNull(exposure);
            return exposure.Length > 0
                && exposure.All(value => value == 0)
                && zeroExposureFallback != null
                ? zeroExposureFallback.ToArray()
                : exposure;
        }

        internal static float[] NormalizeExposure(IReadOnlyList<float> exposure)
        {
            if (exposure == null || exposure.Count == 0) throw new InvalidOperationException("RAW 图像没有曝光时间，无法执行亮度/颜色校正。");
            float[] normalized = new float[3];
            for (int index = 0; index < normalized.Length; index++)
            {
                normalized[index] = exposure[Math.Min(index, exposure.Count - 1)];
                if (!float.IsFinite(normalized[index]) || normalized[index] <= 0)
                {
                    throw new InvalidOperationException($"RAW 图像曝光时间必须是有限正数，通道 {index} 的值为 {normalized[index]}。");
                }
            }
            return normalized;
        }

        internal static bool IsColorCalibration(CalibrationType type)
            => type is CalibrationType.Luminance or CalibrationType.LumOneColor or CalibrationType.LumFourColor or CalibrationType.LumMultiColor;
    }
}
