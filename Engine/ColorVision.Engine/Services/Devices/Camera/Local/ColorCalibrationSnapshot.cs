using ColorVision.Core;
using ColorVision.FileIO;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;

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
        public double[] Coefficients { get; init; } = Array.Empty<double>();
        public float[] Exposure { get; init; } = Array.Empty<float>();
        public string Template { get; init; } = string.Empty;
        public DateTime CalibratedUtc { get; init; }
        // An original sensor RAW may precede the basic stages that produced the color input.
        // Its parameters are still recorded, but replay requires the corrected RAW payload.
        public bool CanReplay { get; init; } = true;

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
                || (TransformKind == 2 ? Channels != 1 : Channels != 3)
                || Coefficients == null || Coefficients.Length != 9 || Coefficients.Any(value => !double.IsFinite(value))
                || Exposure == null || Exposure.Length != 3 || Exposure.Any(value => !float.IsFinite(value) || value <= 0))
                throw new InvalidDataException("RAW 色度校正参数无效或不受支持。");
            _ = checked((long)Width * Height * Channels * (RawBpp / 8));
        }

        internal void Save(string filePath, bool canReplay)
        {
            Validate();
            CVFileMetadata.SetProperty(filePath, PropertyKind, 1, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this with { CanReplay = canReplay })));
        }

        internal static ColorCalibrationSnapshot? Read(string filePath, CVCIEFile header)
        {
            if (!CVFileMetadata.Read(filePath).TryGetValue(PropertyKind, out CVFileProperty? property) || property.Version != 1) return null;
            ColorCalibrationSnapshot snapshot = JsonConvert.DeserializeObject<ColorCalibrationSnapshot>(Encoding.UTF8.GetString(property.Value))
                ?? throw new InvalidDataException("RAW 色度校正参数为空。");
            snapshot.Validate();
            if (snapshot.Width != header.Cols || snapshot.Height != header.Rows || snapshot.Channels != header.Channels
                || (header.FileExtType == CVType.Raw && snapshot.RawBpp != header.Bpp))
                throw new InvalidDataException("色度校正参数与当前图像布局不一致。");
            return snapshot;
        }
    }
}
