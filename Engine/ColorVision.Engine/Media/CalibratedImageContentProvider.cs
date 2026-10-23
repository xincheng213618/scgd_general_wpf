using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.POI;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Tif;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Windows;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media;

/// <summary>Optional computation adapter. Container decoding remains entirely in ImageEditor.</summary>
public sealed class CalibratedImageContentProvider : IImageContentProvider, IImageComponent
{
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
                bool bound = parameters.Schema == ColorVisionTiffParameters.CurrentSchema
                    && parameters.Rows == pixels.PixelHeight && parameters.Cols == pixels.PixelWidth
                    && parameters.PixelChannels > 0 && parameters.Bpp == pixels.Format.BitsPerPixel / parameters.PixelChannels
                    && (parameters.PixelLayout == pixels.Format.ToString()
                        || (parameters.PixelLayout is "Bgr24" or "Rgb24" && (pixels.Format == PixelFormats.Bgr24 || pixels.Format == PixelFormats.Rgb24)))
                    && !string.IsNullOrWhiteSpace(parameters.PixelSha256)
                    && string.Equals(parameters.PixelSha256, ColorVisionPixelBinding.ComputeSha256(pixels, cancellationToken), StringComparison.OrdinalIgnoreCase);
                if (bound && parameters.PixelRole == "calibration-input" && snapshot?.CanReplay == true
                    && snapshot.Width == pixels.PixelWidth && snapshot.Height == pixels.PixelHeight
                    && parameters.PixelChannels == snapshot.Channels)
                {
                    using CVCIEFile raw = CopyCalibrationInput(pixels, snapshot, cancellationToken, out ColorCalibrationSnapshot normalized);
                    snapshot = normalized;
                    buffer = new PoiMeasurementBuffer(raw, normalized);
                }
                else if (bound && parameters.PixelRole == "xyz-channel" && parameters.MeasurementChannel == "Y"
                    && parameters.PixelChannels == 1 && pixels.Format == PixelFormats.Gray32Float)
                {
                    int stride = checked(pixels.PixelWidth * sizeof(float));
                    byte[] data = GC.AllocateUninitializedArray<byte>(checked(stride * pixels.PixelHeight));
                    pixels.CopyPixels(data, stride, 0);
                    buffer = new PoiMeasurementBuffer(data, pixels.PixelWidth, pixels.PixelHeight, 32, 1);
                    measurement = new CVCIEFile { Cols = pixels.PixelWidth, Rows = pixels.PixelHeight, Bpp = 32, Channels = 1, Data = data, FileExtType = CVType.CIE };
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

    private static CVCIEFile CopyCalibrationInput(BitmapSource pixels, ColorCalibrationSnapshot snapshot, CancellationToken token, out ColorCalibrationSnapshot normalized)
    {
        int channels = snapshot.Channels;
        int bpp = snapshot.RawBpp;
        PixelFormat format = pixels.Format;
        if ((channels == 1 && (bpp == 8 ? format != PixelFormats.Gray8 : format != PixelFormats.Gray16))
            || (channels == 3 && (bpp == 8 ? format != PixelFormats.Bgr24 && format != PixelFormats.Rgb24 : format != PixelFormats.Rgb48)))
            throw new InvalidDataException("校正输入样本格式不受支持。");
        int stride = checked(pixels.PixelWidth * channels * (bpp / 8));
        byte[] bytes = GC.AllocateUninitializedArray<byte>(checked(stride * pixels.PixelHeight));
        if (channels == 3 && bpp == 16)
        {
            // Convert a row at a time into native planar RGB without another image-sized copy.
            byte[] row = ArrayPool<byte>.Shared.Rent(stride);
            try
            {
                int planeLength = bytes.Length / 3;
                for (int y = 0; y < pixels.PixelHeight; y++)
                {
                    token.ThrowIfCancellationRequested();
                    pixels.CopyPixels(new Int32Rect(0, y, pixels.PixelWidth, 1), row, stride, 0);
                    for (int x = 0; x < pixels.PixelWidth; x++)
                        for (int c = 0; c < 3; c++)
                        {
                            int source = x * 6 + c * 2;
                            int target = c * planeLength + (y * pixels.PixelWidth + x) * 2;
                            bytes[target] = row[source]; bytes[target + 1] = row[source + 1];
                        }
                }
            }
            finally { ArrayPool<byte>.Shared.Return(row); }
        }
        else
        {
            pixels.CopyPixels(bytes, stride, 0);
            if (channels == 3 && format == PixelFormats.Rgb24)
                for (int index = 0; index < bytes.Length; index += 3) (bytes[index], bytes[index + 2]) = (bytes[index + 2], bytes[index]);
        }
        normalized = snapshot with { InterleavedBgr = channels == 3 && bpp == 8 };
        return new CVCIEFile { Cols = pixels.PixelWidth, Rows = pixels.PixelHeight, Bpp = bpp, Channels = channels, Data = bytes, FileExtType = CVType.Raw };
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
                Context.ImageView.SetLayerController(CvRawLayerController.CreateLive(Context.ImageView, Measurement, new WriteableBitmap(source), "composite"));
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
