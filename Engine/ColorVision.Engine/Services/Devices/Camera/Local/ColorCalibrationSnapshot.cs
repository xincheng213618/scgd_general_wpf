using ColorVision.Core;
using ColorVision.FileIO;
using Newtonsoft.Json;
using ColorVision.ImageEditor.Tif;
using FlowEngineLib.Algorithm;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    /// <summary>The executed color transform, independent of mutable calibration files.</summary>
    internal sealed record ColorCalibrationSnapshot
    {
        internal const string PropertyKind = "colorvision.calibration.color";
        public int Width { get; init; }
        public int Height { get; init; }
        public int RawBpp { get; init; }
        public int Channels { get; init; }
        public int CalibrationType { get; init; }
        public int TransformKind { get; init; }
        public bool InterleavedBgr { get; init; } = true;
        // For saved RAW, the payload has already been mirrored by this transform relative to sensor coordinates.
        public CVImageFlipMode AppliedRawFlip { get; init; } = CVImageFlipMode.None;
        public double[] Coefficients { get; init; } = Array.Empty<double>();
        public float[] Exposure { get; init; } = Array.Empty<float>();
        public string Template { get; init; } = string.Empty;
        public DateTime CalibratedUtc { get; init; }
        // Reference and original coefficients allow a repeated correction to replace, not compound, the previous one.
        public SpectrumColorCorrection? SpectrumCorrection { get; init; }
        // An original sensor RAW may precede the basic stages that produced the color input.
        // Its parameters are still recorded, but replay requires the corrected RAW payload.
        public bool CanReplay { get; init; } = true;
        public ImageCalibrationSummary? Provenance { get; init; }

        internal ImageCalibrationSummary ToSummary() => (Provenance ?? new ImageCalibrationSummary()) with
        {
            Template = Template, CalibratedUtc = CalibratedUtc, CalibrationType = CalibrationType,
            TransformKind = TransformKind,
            ReferenceCorrection = SpectrumCorrection == null ? null : $"{SpectrumCorrection.Mode} / {SpectrumCorrection.PointName}"
        };

        internal static ColorCalibrationSnapshot Create(RawColorTransformV1 transform, int width, int height, int bpp, float[] exposure, string template)
        {
            var snapshot = new ColorCalibrationSnapshot
            {
                Width = width, Height = height, RawBpp = bpp, Channels = transform.Channels,
                CalibrationType = transform.CalibrationType, TransformKind = transform.Kind,
                InterleavedBgr = transform.InterleavedBgr != 0, Coefficients = (double[])transform.Coefficients.Clone(),
                Exposure = (float[])exposure.Clone(), Template = template, CalibratedUtc = DateTime.UtcNow,
            };
            snapshot.Validate();
            return snapshot;
        }

        internal RawColorTransformV1 ToNative()
        {
            Validate();
            RawColorTransformV1 transform = RawColorTransformV1.Create();
            transform.CalibrationType = CalibrationType;
            transform.Channels = Channels;
            transform.Kind = TransformKind;
            transform.InterleavedBgr = InterleavedBgr ? 1 : 0;
            transform.Coefficients = (double[])Coefficients.Clone();
            return transform;
        }

        internal void Validate()
        {
            if (Width <= 0 || Height <= 0 || RawBpp is not (8 or 16) || TransformKind is < 0 or > 2
                || AppliedRawFlip is not (CVImageFlipMode.None or CVImageFlipMode.X or CVImageFlipMode.Y or CVImageFlipMode.XY)
                || (TransformKind == 2 ? Channels != 1 : Channels != 3)
                || Coefficients == null || Coefficients.Length != 9 || Coefficients.Any(value => !double.IsFinite(value))
                || Exposure == null || Exposure.Length != 3 || Exposure.Any(value => !float.IsFinite(value) || value <= 0))
                throw new InvalidDataException("RAW 色度校正参数无效或不受支持。");
            _ = checked((long)Width * Height * Channels * (RawBpp / 8));
        }

        internal void Save(string filePath, bool canReplay)
            => CVFileMetadata.SetProperties(filePath, CreateProperties(canReplay));

        internal void UpdateRawFile(string path, bool canReplay, CVImageFlipMode? resultFlip = null)
            => CVFileMetadata.UpdateProperties(path, (layout, properties) =>
            {
                if (layout != new CVFilePixelLayout(Width, Height, RawBpp, Channels))
                    throw new InvalidDataException("目标 RAW 的位深、通道或尺寸与本次校正不匹配。");
                // WPF frames use packed BGR. A metadata-only update follows the target payload's current encoding.
                var stored = ReadOptionalRaw(path, properties, layout);
                CVImageFlipMode storedFlip = stored?.AppliedRawFlip ?? CVImageFlipMode.None;
                var snapshot = this with { InterleavedBgr = stored?.InterleavedBgr ?? true, AppliedRawFlip = storedFlip };
                return snapshot.CreateProperties(canReplay && (!resultFlip.HasValue || resultFlip.Value == storedFlip));
            });

        internal static ColorCalibrationSnapshot? ReadOptionalRaw(string path, IReadOnlyDictionary<string, CVFileProperty> properties, CVFilePixelLayout layout)
        {
            try { return Read(properties, layout.Width, layout.Height, layout.Channels, layout.BitsPerChannel); }
            catch (Exception error) when (error is InvalidDataException or JsonException or OverflowException)
            {
                log4net.LogManager.GetLogger(typeof(ColorCalibrationSnapshot)).Warn($"RAW 色度校正参数不可用，保留原图：{path}", error);
                return null;
            }
        }

        internal Dictionary<string, CVFileProperty> CreateProperties(bool canReplay)
        {
            Validate();
            return new Dictionary<string, CVFileProperty>
            {
                [PropertyKind] = CameraFileMetadata.CreateProperty(this with { CanReplay = canReplay }),
                [CameraFileMetadata.ProvenanceKind] = CameraFileMetadata.CreateProperty(ToSummary())
            };
        }

        internal static ColorCalibrationSnapshot? Read(string filePath, CVCIEFile header)
            => Read(CVFileMetadata.Read(filePath), header);

        internal static ColorCalibrationSnapshot? Read(IReadOnlyDictionary<string, CVFileProperty> properties, CVCIEFile header)
            => Read(properties, header.Cols, header.Rows, header.Channels, header.FileExtType == CVType.Raw ? header.Bpp : null);

        private static ColorCalibrationSnapshot? Read(IReadOnlyDictionary<string, CVFileProperty> properties,
            int width, int height, int channels, int? rawBpp)
        {
            if (!properties.TryGetValue(PropertyKind, out CVFileProperty? property) || property.Version != 1) return null;
            ColorCalibrationSnapshot snapshot = JsonConvert.DeserializeObject<ColorCalibrationSnapshot>(Encoding.UTF8.GetString(property.Value))
                ?? throw new InvalidDataException("RAW 色度校正参数为空。");
            snapshot.Validate();
            if (snapshot.Width != width || snapshot.Height != height || snapshot.Channels != channels
                || (rawBpp.HasValue && snapshot.RawBpp != rawBpp.Value))
                throw new InvalidDataException("色度校正参数与当前图像布局不一致。");
            return snapshot;
        }
    }
}
