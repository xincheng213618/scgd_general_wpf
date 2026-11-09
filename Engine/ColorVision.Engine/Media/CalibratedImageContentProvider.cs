using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.POI;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Tif;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media;

/// <summary>Optional computation adapter. Container decoding remains entirely in ImageEditor.</summary>
public sealed class CalibratedImageContentProvider : IImageContentProvider, IImageComponent
{
    private const int PixelBlockBytes = 512 * 1024;

    public void Execute(ImageView imageView) => imageView.RegisterImageContentProvider(this);

    public async Task<IImageContentCapability?> PrepareAsync(EditorContext context, DecodedImageContent content, CancellationToken cancellationToken)
    {
        ColorVisionTiffParameters? parameters = content.Parameters;
        if (parameters == null) return null;
        return await Task.Run<IImageContentCapability?>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ColorCalibrationSnapshot? snapshot = null;
            try
            {
                if (parameters.Calibration is { } json)
                {
                    snapshot = json.ToObject<ColorCalibrationSnapshot>();
                    snapshot?.Validate();
                }
            }
            catch (Exception error) when (error is JsonException or InvalidDataException or OverflowException)
            {
                // Malformed metadata must never prevent the base decoder publishing its pixels.
                snapshot = null;
            }

            PoiMeasurementBuffer? buffer = null;
            CVCIEFile? measurement = null;
            try
            {
                BitmapSource pixels = content.Pixels;
                if (parameters.PixelSha256 is { Length: SHA256.HashSizeInBytes * 2 } expectedHash && HasBindingLayout(parameters, pixels))
                {
                    if (parameters.PixelRole == "calibration-input" && snapshot?.CanReplay == true
                        && snapshot.Width == pixels.PixelWidth && snapshot.Height == pixels.PixelHeight
                        && parameters.PixelChannels == snapshot.Channels && MatchesRawFormat(pixels.Format, snapshot)
                        && ReadBoundSamples(pixels, expectedHash, cancellationToken) is { } rawSamples)
                    {
                        using CVCIEFile raw = new()
                        {
                            Cols = pixels.PixelWidth, Rows = pixels.PixelHeight, Bpp = snapshot.RawBpp,
                            Channels = snapshot.Channels, Data = rawSamples, FileExtType = CVType.Raw
                        };
                        snapshot = snapshot with { InterleavedBgr = raw.Channels == 3 && raw.Bpp == 8 };
                        buffer = new PoiMeasurementBuffer(raw, snapshot);
                    }
                    else if (parameters.PixelRole == "xyz-channel" && parameters.MeasurementChannel == "Y"
                        && parameters.PixelChannels == 1 && pixels.Format == PixelFormats.Gray32Float
                        && ReadBoundSamples(pixels, expectedHash, cancellationToken) is { } data)
                    {
                        buffer = new PoiMeasurementBuffer(data, pixels.PixelWidth, pixels.PixelHeight, 32, 1);
                        measurement = new CVCIEFile { Cols = pixels.PixelWidth, Rows = pixels.PixelHeight, Bpp = 32, Channels = 1, Data = data, FileExtType = CVType.CIE };
                    }
                }
            }
            catch (Exception error) when (error is InvalidDataException or NotSupportedException or ArgumentException or OverflowException)
            {
                buffer?.Dispose();
                buffer = null;
                measurement?.Dispose();
                measurement = null;
            }
            if (snapshot == null && buffer == null) return null;
            if (cancellationToken.IsCancellationRequested) { buffer?.Dispose(); measurement?.Dispose(); cancellationToken.ThrowIfCancellationRequested(); }
            return new EmbeddedCapability(context, content.FilePath, snapshot, buffer, measurement);
        }, cancellationToken);
    }

    private static bool HasBindingLayout(ColorVisionTiffParameters parameters, BitmapSource pixels) =>
        parameters.Schema == ColorVisionTiffParameters.CurrentSchema
        && pixels.PixelWidth > 0 && pixels.PixelHeight > 0
        && parameters.Rows == pixels.PixelHeight && parameters.Cols == pixels.PixelWidth
        && parameters.PixelChannels > 0 && parameters.Bpp == pixels.Format.BitsPerPixel / parameters.PixelChannels
        && (parameters.PixelLayout == pixels.Format.ToString()
            || (parameters.PixelLayout is "Bgr24" or "Rgb24" && (pixels.Format == PixelFormats.Bgr24 || pixels.Format == PixelFormats.Rgb24)));

    private static bool MatchesRawFormat(PixelFormat format, ColorCalibrationSnapshot snapshot) => (snapshot.Channels, snapshot.RawBpp) switch
    {
        (1, 8) => format == PixelFormats.Gray8,
        (1, 16) => format == PixelFormats.Gray16,
        (3, 8) => format == PixelFormats.Bgr24 || format == PixelFormats.Rgb24,
        (3, 16) => format == PixelFormats.Rgb48,
        _ => false,
    };

    private static unsafe byte[]? ReadBoundSamples(BitmapSource pixels, string expectedHash, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Span<byte> expectedDigest = stackalloc byte[SHA256.HashSizeInBytes];
        if (Convert.FromHexString(expectedHash.AsSpan(), expectedDigest, out _, out _) != OperationStatus.Done) return null;
        int width = pixels.PixelWidth;
        int height = pixels.PixelHeight;
        PixelFormat format = pixels.Format;
        bool planar = format == PixelFormats.Rgb48;
        bool bgr = format == PixelFormats.Bgr24;
        bool rgb = format == PixelFormats.Rgb24;
        int stride = checked(width * (format.BitsPerPixel / 8));
        int rowsPerBlock = Math.Max(1, Math.Min(height, PixelBlockBytes / stride));
        byte[] bytes = GC.AllocateUninitializedArray<byte>(checked(stride * height));
        int planeLength = bytes.Length / 3;
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[]? scratch = planar || bgr ? ArrayPool<byte>.Shared.Rent(stride * rowsPerBlock) : null;
        try
        {
            fixed (byte* destination = bytes, staging = scratch)
            {
                for (int top = 0; top < height;)
                {
                    token.ThrowIfCancellationRequested();
                    int rows = Math.Min(rowsPerBlock, height - top);
                    int length = stride * rows;
                    int pixelCount = width * rows;
                    byte* packed = planar ? staging : destination + top * stride;
                    pixels.CopyPixels(new Int32Rect(0, top, width, rows), (IntPtr)packed, length, stride);
                    // The digest uses tight RGB samples. The RAW reader expects planar RGB16 or interleaved BGR8.
                    if (bgr) SwapRedBlue24(packed, staging, pixelCount);
                    hash.AppendData(new ReadOnlySpan<byte>(bgr ? staging : packed, length));
                    if (planar) SplitRgb48(packed, destination + top * width * sizeof(ushort), planeLength, pixelCount);
                    else if (rgb) SwapRedBlue24(packed, packed, pixelCount);
                    top += rows;
                }
            }
        }
        finally
        {
            if (scratch != null) ArrayPool<byte>.Shared.Return(scratch);
        }

        token.ThrowIfCancellationRequested();
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        hash.GetHashAndReset(digest);
        return digest.SequenceEqual(expectedDigest) ? bytes : null;
    }

    private static unsafe void SwapRedBlue24(byte* source, byte* destination, int pixelCount)
    {
        for (int pixel = 0; pixel < pixelCount; pixel++, source += 3, destination += 3)
        {
            byte first = source[0];
            destination[0] = source[2];
            destination[1] = source[1];
            destination[2] = first;
        }
    }

    private static unsafe void SplitRgb48(byte* source, byte* destination, int planeLength, int pixelCount)
    {
        ushort* packed = (ushort*)source;
        ushort* red = (ushort*)destination;
        ushort* green = (ushort*)(destination + planeLength);
        ushort* blue = (ushort*)(destination + 2 * planeLength);
        for (int pixel = 0; pixel < pixelCount; pixel++, packed += 3)
        {
            red[pixel] = packed[0];
            green[pixel] = packed[1];
            blue[pixel] = packed[2];
        }
    }

    private sealed record EmbeddedCapability(EditorContext Context, string Path, ColorCalibrationSnapshot? Snapshot,
        PoiMeasurementBuffer? Buffer, CVCIEFile? Measurement) : CalibratedImageCapability(Context)
    {
        private bool activated;
        public override void Activate()
        {
            if (activated) return;
            activated = true;
            base.Activate();
            if (Snapshot != null && Context.ImageView.ImageContent?.Parameters is { } parameters)
            {
                parameters.CalibrationSummary = Snapshot.ToSummary();
                Context.ImageView.SetImageParameterRecord(parameters);
            }
            Context.Config.SetImageMetadata(RawCalibrationInformationProvider.MetadataKey, Snapshot, nameof(CalibratedImageContentProvider), "文件内嵌实际色度校正记录");
            if (Buffer == null)
            {
                Context.Config.SetOpenerRuntime("HasCieMeasurements", false, nameof(CalibratedImageContentProvider), "仅保留参数记录");
                Context.Config.SetOpenerRuntime("IsCVCIE", false, nameof(CalibratedImageContentProvider), "实际文件保持原格式");
                return;
            }
            InitializeEmbedded(Buffer, Snapshot, Path, Context.ImageView.ImageContent?.Pixels);
            if (Measurement != null && Context.ImageView.ViewBitmapSource is BitmapSource source)
            {
                // The layer controller already takes a frozen snapshot of a mutable source.
                WriteableBitmap display = source as WriteableBitmap ?? new WriteableBitmap(source);
                Context.ImageView.SetLayerController(CvRawLayerController.CreateLive(Context.ImageView, Measurement, display, "composite"));
            }
        }
        public override Action DetachForRetirement(long contentVersion)
        {
            Action release = base.DetachForRetirement(contentVersion);
            return () =>
            {
                try { release(); }
                finally
                {
                    Buffer?.Dispose();
                    Measurement?.Dispose();
                }
            };
        }
    }
}
