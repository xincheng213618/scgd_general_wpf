using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace ColorVision.Core;

public sealed class TemplateMatchingImage
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Depth { get; set; }
    public int Channels { get; set; }
    public byte[] Pixels { get; set; } = [];
    [JsonIgnore] public int Stride => checked(Width * Channels * (Depth / 8));

    public void Validate()
    {
        if (Width <= 0 || Height <= 0 || (long)Width * Height > 16_777_216 ||
            !(Depth == 8 && Channels is 1 or 3 or 4 || Depth == 16 && Channels is 1 or 3 || Depth == 32 && Channels == 1))
            throw new InvalidDataException("模板图像尺寸或格式无效；支持 8 位灰度/彩色、16 位灰度/RGB、32 位浮点灰度，最多 1600 万像素。");
        if (Pixels == null || Pixels.LongLength != (long)Stride * Height || Pixels.Length > 128 * 1024 * 1024)
            throw new InvalidDataException("模板像素长度与图像尺寸不一致或超过 128 MiB。");
    }

    internal HImage Borrow(IntPtr pointer) => new() { cols = Width, rows = Height, depth = Depth, channels = Channels, stride = Stride, pData = pointer, isDispose = true };

    public static TemplateMatchingImage Capture(HImage source, RoiRect roi)
    {
        ValidateImage(source);
        TemplateMatching.ValidateRoi(roi, source.cols, source.rows);
        if (roi.Width == 0) roi = new RoiRect(0, 0, source.cols, source.rows);
        TemplateMatchingImage image = new() { Width = roi.Width, Height = roi.Height, Depth = source.depth, Channels = source.channels };
        long length = (long)image.Stride * image.Height;
        if (length > 128 * 1024 * 1024 || (long)image.Width * image.Height > 16_777_216) throw new InvalidDataException("参考图像过大，请框选较小区域。");
        image.Pixels = new byte[checked((int)length)];
        int offset = checked(roi.X * source.channels * (source.depth / 8));
        for (int row = 0; row < roi.Height; row++)
            Marshal.Copy(IntPtr.Add(source.pData, checked((roi.Y + row) * source.stride + offset)), image.Pixels, row * image.Stride, image.Stride);
        image.Validate();
        return image;
    }

    internal static void ValidateImage(HImage source)
    {
        if (source.pData == IntPtr.Zero || source.cols <= 0 || source.rows <= 0 || source.depth is not (8 or 16 or 32) || source.channels is not (1 or 3 or 4) ||
            source.stride < (long)source.cols * source.channels * (source.depth / 8))
            throw new ArgumentException("匹配输入图像缓冲区无效。");
    }

    public BitmapSource CreatePreview()
    {
        Validate();
        if (Depth == 32)
        {
            float[] values = new float[Width * Height];
            Buffer.BlockCopy(Pixels, 0, values, 0, Pixels.Length);
            float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
            foreach (float value in values)
                if (float.IsFinite(value)) { minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value); }
            byte[] preview = new byte[values.Length];
            double range = (double)maximum - minimum;
            for (int i = 0; i < values.Length; i++)
                if (float.IsFinite(values[i]) && range > 0) preview[i] = (byte)Math.Clamp(((double)values[i] - minimum) * 255 / range, 0, 255);
            BitmapSource gray = BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Gray8, null, preview, Width);
            gray.Freeze();
            return gray;
        }
        GCHandle pin = GCHandle.Alloc(Pixels, GCHandleType.Pinned);
        try
        {
            BitmapSource bitmap = Borrow(pin.AddrOfPinnedObject()).ToWriteableBitmap();
            bitmap.Freeze();
            return bitmap;
        }
        finally { pin.Free(); }
    }
}

public sealed class TemplateMatchingDocument
{
    [JsonRequired] public string Schema { get; set; } = "colorvision.template-matching/v1";
    [JsonRequired] public string Name { get; set; } = "匹配模板";
    [JsonRequired] public TemplateMatchingImage Image { get; set; } = new();
    [JsonRequired] public TemplateMatchingOptions Options { get; set; } = new();

    public string ToJson()
    {
        Validate();
        return JsonSerializer.Serialize(this, TemplateMatchingFile.JsonOptions);
    }

    public static TemplateMatchingDocument FromJson(string json)
    {
        TemplateMatchingDocument document = JsonSerializer.Deserialize<TemplateMatchingDocument>(json, TemplateMatchingFile.JsonOptions)
            ?? throw new InvalidDataException("匹配配置内容为空。");
        document.Validate();
        return document;
    }

    public void Validate()
    {
        if (Schema != "colorvision.template-matching/v1") throw new InvalidDataException("不支持的匹配模板版本。");
        if (string.IsNullOrWhiteSpace(Name) || Image == null || Options == null) throw new InvalidDataException("匹配模板名称、图像和参数不能为空。");
        Image.Validate();
        Options.Validate();
    }

    public string Fingerprint()
    {
        Validate();
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this, TemplateMatchingFile.JsonOptions)));
    }
}

/// <summary>A self-contained, versioned reference image and recipe; no database or external image paths.</summary>
public static class TemplateMatchingFile
{
    public const string Extension = ".cvtm";
    public const string Filter = "ColorVision 匹配模板 (*.cvtm)|*.cvtm";
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "Templates", "Matching");
    internal static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public static TemplateMatchingDocument Load(string path)
    {
        using FileStream stream = File.OpenRead(path);
        if (stream.Length > 180L * 1024 * 1024) throw new InvalidDataException("模板文件超过 180 MiB。");
        TemplateMatchingDocument document = JsonSerializer.Deserialize<TemplateMatchingDocument>(stream, JsonOptions)
            ?? throw new InvalidDataException("模板文件内容为空。");
        document.Validate();
        return document;
    }

    public static void Save(string path, TemplateMatchingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, document, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
