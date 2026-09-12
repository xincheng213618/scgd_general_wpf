using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Output
{
    /// <summary>Consumes detached snapshots and encodes each export independently of live editor state.</summary>
    internal static class ImageSnapshotEncoder
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ImageSnapshotEncoder));

        internal static async Task SaveBitmapAsync(
            BitmapSource snapshot,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                log.Warn("Skip saving ImageView snapshot because file name is empty.");
                return;
            }
            if (!snapshot.IsFrozen)
            {
                throw new InvalidOperationException(
                    "ImageView snapshots must be frozen before background saving.");
            }

            await Task.Run(
                () => SaveSnapshot(snapshot, fileName, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        internal static void SaveSnapshot(
            BitmapSource snapshot,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            SaveSnapshot(
                snapshot,
                fileName,
                ImageViewSnapshotSaveOptions.Default,
                cancellationToken);
        }

        private static void SaveSnapshot(
            BitmapSource snapshot,
            string fileName,
            ImageViewSnapshotSaveOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            BitmapEncoder encoder = options.Format switch
            {
                ImageViewSnapshotFormat.Png => new PngBitmapEncoder(),
                ImageViewSnapshotFormat.Jpeg => new JpegBitmapEncoder
                {
                    QualityLevel = Math.Clamp(options.JpegQuality, 1, 100),
                },
                _ => throw new ArgumentOutOfRangeException(nameof(options), options.Format, "Unsupported snapshot format."),
            };
            encoder.Frames.Add(BitmapFrame.Create(snapshot));
            SaveEncoderAtomically(encoder, fileName, cancellationToken);
        }

        private static void SaveSourceSnapshot(
            BitmapSource source,
            string fileName,
            ImageViewSourceSaveOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (options.Format == ImageViewSourceFormat.Bmp
                && !CanBmpPreserveSourceBitDepth(source.Format))
            {
                throw new NotSupportedException(
                    $"BMP cannot preserve source pixel format {source.Format} ({source.Format.BitsPerPixel} bits per pixel). "
                    + "Use PNG or TIFF for 16-bit source images.");
            }

            string? directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            BitmapEncoder encoder = options.Format switch
            {
                ImageViewSourceFormat.Png => new PngBitmapEncoder(),
                ImageViewSourceFormat.Tiff => new TiffBitmapEncoder
                {
                    Compression = options.TiffCompression == ImageViewTiffCompression.Zip
                        ? TiffCompressOption.Zip
                        : TiffCompressOption.Lzw,
                },
                ImageViewSourceFormat.Bmp => new BmpBitmapEncoder(),
                _ => throw new ArgumentOutOfRangeException(nameof(options), options.Format, "Unsupported source image format."),
            };
            encoder.Frames.Add(BitmapFrame.Create(source));
            SaveEncoderAtomically(encoder, fileName, cancellationToken);
        }

        internal static bool CanBmpPreserveSourceBitDepth(PixelFormat format)
        {
            return format == PixelFormats.Bgr24
                || format == PixelFormats.Rgb24
                || format == PixelFormats.Bgr32
                || format == PixelFormats.Gray8
                || format == PixelFormats.Indexed8;
        }

        private static void SaveEncoderAtomically(
            BitmapEncoder encoder,
            string fileName,
            CancellationToken cancellationToken)
        {
            string? directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string temporaryFile = fileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream fileStream = new(
                    temporaryFile,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    encoder.Save(fileStream);
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryFile, fileName, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryFile))
                    File.Delete(temporaryFile);
            }
        }

        internal static async Task SaveSnapshotWithOptionsAsync(
            ImageViewSnapshot snapshot,
            string fileName,
            ImageViewSnapshotSaveOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            await SaveSnapshotExportsAsync(
                snapshot,
                new ImageViewSnapshotExportOptions
                {
                    RenderedFileName = fileName,
                    RenderedOptions = options,
                },
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Saves the rendered 8-bit scene, the original loaded pixels, or both from one captured snapshot.
        /// The source branch bypasses WPF scene rendering so its original pixel format and bit depth are retained.
        /// </summary>
        internal static async Task SaveSnapshotExportsAsync(
            ImageViewSnapshot snapshot,
            ImageViewSnapshotExportOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(options);
            try
            {
                bool saveRendered = !string.IsNullOrWhiteSpace(options.RenderedFileName);
                bool saveSource = !string.IsNullOrWhiteSpace(options.SourceFileName);
                if (!saveRendered && !saveSource)
                    return;
                ArgumentNullException.ThrowIfNull(options.RenderedOptions);
                ArgumentNullException.ThrowIfNull(options.SourceOptions);
                if (saveRendered
                    && saveSource
                    && string.Equals(
                        Path.GetFullPath(options.RenderedFileName!),
                        Path.GetFullPath(options.SourceFileName!),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("Rendered and source image exports must use different file paths.", nameof(options));
                }

                await RunOnSnapshotStaThreadAsync(
                    () => RenderAndSaveSnapshotExports(snapshot, options, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                snapshot.Dispose();
            }
        }

        private static void RenderAndSaveSnapshotExports(
            ImageViewSnapshot snapshot,
            ImageViewSnapshotExportOptions options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BitmapSource? source = MaterializeSnapshotSource(snapshot);

            if (!string.IsNullOrWhiteSpace(options.RenderedFileName))
            {
                DrawingGroup scene = ComposeSnapshotScene(snapshot, source);
                RenderAndSaveSnapshot(
                    snapshot,
                    scene,
                    options.RenderedFileName,
                    options.RenderedOptions,
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(options.SourceFileName))
            {
                if (source == null)
                    throw new InvalidOperationException("This snapshot does not contain original source pixels.");
                SaveSourceSnapshot(source, options.SourceFileName, options.SourceOptions, cancellationToken);
            }
        }

        private static BitmapSource? MaterializeSnapshotSource(ImageViewSnapshot snapshot)
        {
            SnapshotImageBufferLease? buffer = snapshot.TakeImageBuffer();
            if (buffer != null)
            {
                WriteableBitmap source;
                try
                {
                    source = buffer.ToWriteableBitmap(snapshot.DpiX, snapshot.DpiY);
                    source.Freeze();
                }
                finally
                {
                    buffer.Dispose();
                }
                return source;
            }
            return snapshot.FrozenSource;
        }

        private static DrawingGroup ComposeSnapshotScene(ImageViewSnapshot snapshot, BitmapSource? source)
        {
            if (source == null)
                return snapshot.Scene;

            DrawingGroup composedScene = new();
            composedScene.Children.Add(new ImageDrawing(
                source,
                new Rect(0, 0, snapshot.PixelWidth, snapshot.PixelHeight)));
            composedScene.Children.Add(snapshot.Scene);
            composedScene.Freeze();
            return composedScene;
        }

        private static void RenderAndSaveSnapshot(
            ImageViewSnapshot snapshot,
            DrawingGroup scene,
            string fileName,
            ImageViewSnapshotSaveOptions options,
            CancellationToken cancellationToken)
        {

            (int outputWidth, int outputHeight) = GetSnapshotOutputSize(snapshot, options.ScaleDivisor);
            DrawingVisual visual = new();
            using (DrawingContext context = visual.RenderOpen())
            {
                if (outputWidth != snapshot.PixelWidth || outputHeight != snapshot.PixelHeight)
                {
                    context.PushTransform(new ScaleTransform(
                        outputWidth / (double)snapshot.PixelWidth,
                        outputHeight / (double)snapshot.PixelHeight));
                    context.DrawDrawing(scene);
                    context.Pop();
                }
                else
                {
                    context.DrawDrawing(scene);
                }
            }

            RenderTargetBitmap renderedBitmap = new(
                outputWidth,
                outputHeight,
                snapshot.DpiX,
                snapshot.DpiY,
                PixelFormats.Pbgra32);
            renderedBitmap.Render(visual);
            cancellationToken.ThrowIfCancellationRequested();
            SaveSnapshot(renderedBitmap, fileName, options, cancellationToken);
        }

        private static (int Width, int Height) GetSnapshotOutputSize(
            ImageViewSnapshot snapshot,
            int scaleDivisor)
        {
            int normalizedDivisor = scaleDivisor is 2 or 4 ? scaleDivisor : 1;
            if (normalizedDivisor == 1)
                return (snapshot.PixelWidth, snapshot.PixelHeight);

            return (
                Math.Max(1, (int)Math.Round(snapshot.PixelWidth / (double)normalizedDivisor, MidpointRounding.AwayFromZero)),
                Math.Max(1, (int)Math.Round(snapshot.PixelHeight / (double)normalizedDivisor, MidpointRounding.AwayFromZero)));
        }

        private static Task RunOnSnapshotStaThreadAsync(
            Action action,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            TaskCompletionSource<object?> completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Thread thread = new(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    action();
                    completion.TrySetResult(null);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled(cancellationToken);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            })
            {
                IsBackground = true,
                Name = "ColorVision Image Snapshot Renderer",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }
    }
}
