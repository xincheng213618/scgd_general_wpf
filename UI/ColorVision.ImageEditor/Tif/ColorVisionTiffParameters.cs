using System;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Tif
{
    /// <summary>
    /// Versioned ColorVision acquisition records stored in TIFF ImageDescription or PNG tEXt.
    /// </summary>
    public sealed class ColorVisionTiffParameters
    {
        public const string CurrentSchema = "ColorVision.CVImage/2";
        internal const string ImageDescriptionQuery = "/ifd/{ushort=270}";
        public const string PngMetadataKeyword = "ColorVision";
        internal const string PngDescriptionQuery = "/tEXt/{str=ColorVision}";

        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
            DateParseHandling = DateParseHandling.None,
        };

        public string Schema { get; set; } = CurrentSchema;
        public string SourceType { get; set; } = string.Empty;
        public string ExportedChannel { get; set; } = string.Empty;
        public string? InputFileName { get; set; }
        public string? AssociatedSourceFileName { get; set; }
        public uint FileVersion { get; set; }
        public int Rows { get; set; }
        public int Cols { get; set; }
        public int Bpp { get; set; }
        public int SourceChannels { get; set; }
        public int NdPort { get; set; }
        public float Gain { get; set; }
        public float[] Exposure { get; set; } = [];
        public JToken? Calibration { get; set; }
        public string? PixelRole { get; set; }
        public string? PixelLayout { get; set; }
        public string? PixelSha256 { get; set; }
        public int PixelChannels { get; set; }
        public string? MeasurementChannel { get; set; }
        public ImageAcquisitionRecord? Acquisition { get; set; }
        public ImageCalibrationSummary? CalibrationSummary { get; set; }
        public ImageRenderedOutput? RenderedOutput { get; set; }

        internal ColorVisionTiffParameters ToDisplayRecord()
        {
            // Whitelist record fields: a screenshot must never inherit a transform or pixel binding.
            ColorVisionTiffParameters record = new()
            {
                SourceType = SourceType, ExportedChannel = "Snapshot", InputFileName = InputFileName,
                AssociatedSourceFileName = AssociatedSourceFileName, FileVersion = FileVersion,
                Rows = Rows, Cols = Cols, Bpp = Bpp, SourceChannels = SourceChannels,
                NdPort = NdPort, Gain = Gain, Exposure = (float[])Exposure.Clone(),
                Acquisition = Acquisition, CalibrationSummary = CalibrationSummary, PixelRole = "display"
            };
            // Detach nested arrays as well as the top-level record from mutable producer state.
            return JsonConvert.DeserializeObject<ColorVisionTiffParameters>(record.ToJson(), SerializerSettings)!;
        }

        public string ToJson() => JsonConvert.SerializeObject(this, SerializerSettings);

        public static bool TryParse(string? json, out ColorVisionTiffParameters? parameters)
        {
            parameters = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                JObject? document = JsonConvert.DeserializeObject<JObject>(json, SerializerSettings);
                if (document?.GetValue("schema", StringComparison.OrdinalIgnoreCase)?.Type != JTokenType.String) return false;
                ColorVisionTiffParameters? candidate = document.ToObject<ColorVisionTiffParameters>(JsonSerializer.Create(SerializerSettings));
                if (candidate == null
                    || string.IsNullOrWhiteSpace(candidate.Schema)
                    || !candidate.Schema.StartsWith("ColorVision.CVImage/", StringComparison.Ordinal))
                    return false;

                candidate.Exposure ??= [];
                parameters = candidate;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        /// <summary>Reads the container record without decoding image samples or granting measurement capabilities.</summary>
        public static bool TryReadFile(string filePath, out ColorVisionTiffParameters? parameters)
        {
            parameters = null;
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            try
            {
                string extension = Path.GetExtension(filePath);
                if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase)) return false;
                using var stream = File.OpenRead(filePath);
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                return decoder.Frames.Count > 0 && TryRead(decoder.Frames[0].Metadata as BitmapMetadata, out parameters);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
                or NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.COMException or FileFormatException)
            {
                return false;
            }
        }

        internal static bool TryRead(BitmapMetadata? metadata, out ColorVisionTiffParameters? parameters)
        {
            parameters = null;
            if (metadata == null)
                return false;

            try
            {
                string? description = string.Equals(metadata.Format, "jpg", StringComparison.OrdinalIgnoreCase)
                    ? metadata.Comment
                    : metadata.GetQuery(string.Equals(metadata.Format, "png", StringComparison.OrdinalIgnoreCase) ? PngDescriptionQuery : ImageDescriptionQuery) as string;
                return TryParse(description?.TrimEnd('\0'), out parameters);
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (COMException)
            {
                return false;
            }
        }
    }
}
