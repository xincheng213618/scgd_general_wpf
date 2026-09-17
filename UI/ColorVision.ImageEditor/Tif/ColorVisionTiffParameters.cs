using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Tif
{
    /// <summary>
    /// Versioned ColorVision acquisition parameters stored in TIFF ImageDescription (tag 270).
    /// </summary>
    public sealed class ColorVisionTiffParameters
    {
        public const string CurrentSchema = "ColorVision.CVImage/1";
        internal const string ImageDescriptionQuery = "/ifd/{ushort=270}";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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

        public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

        public static bool TryParse(string? json, out ColorVisionTiffParameters? parameters)
        {
            parameters = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                ColorVisionTiffParameters? candidate = JsonSerializer.Deserialize<ColorVisionTiffParameters>(json, SerializerOptions);
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

        internal static bool TryRead(BitmapMetadata? metadata, out ColorVisionTiffParameters? parameters)
        {
            parameters = null;
            if (metadata == null)
                return false;

            try
            {
                string? description = metadata.GetQuery(ImageDescriptionQuery) as string;
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
        }
    }
}
