using System;
using System.Diagnostics;
using System.Globalization;
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
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            Stopwatch queueStopwatch = Stopwatch.StartNew();
            TimeSpan materializeElapsed = TimeSpan.Zero;
            TimeSpan renderElapsed = TimeSpan.Zero;
            TimeSpan encodeWriteElapsed = TimeSpan.Zero;
            string outcome = "Completed";
            SnapshotEncodeWorker.Reservation? encodeReservation = null;
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

                encodeReservation = await SnapshotEncodeWorker.ReserveAsync(cancellationToken).ConfigureAwait(false);
                PreparedSnapshot prepared = await SnapshotStaWorker.RunAsync(
                    () => PrepareSnapshotExports(
                        snapshot,
                        options,
                        queueStopwatch,
                        ref materializeElapsed,
                        ref renderElapsed,
                        cancellationToken),
                    cancellationToken).ConfigureAwait(false);
                Stopwatch encodeWriteStopwatch = Stopwatch.StartNew();
                try
                {
                    await encodeReservation.RunAsync(
                        () => SavePreparedSnapshotExports(prepared, options, cancellationToken),
                        cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    encodeWriteStopwatch.Stop();
                    encodeWriteElapsed = encodeWriteStopwatch.Elapsed;
                }
            }
            catch (OperationCanceledException)
            {
                outcome = "Canceled";
                throw;
            }
            catch
            {
                outcome = "Failed";
                throw;
            }
            finally
            {
                queueStopwatch.Stop();
                totalStopwatch.Stop();
                encodeReservation?.Dispose();
                snapshot.Dispose();
                LogExportTiming(
                    options.DiagnosticContext,
                    outcome,
                    queueStopwatch.Elapsed,
                    materializeElapsed,
                    renderElapsed,
                    encodeWriteElapsed,
                    totalStopwatch.Elapsed);
            }
        }

        private static PreparedSnapshot PrepareSnapshotExports(
            ImageViewSnapshot snapshot,
            ImageViewSnapshotExportOptions options,
            Stopwatch queueStopwatch,
            ref TimeSpan materializeElapsed,
            ref TimeSpan renderElapsed,
            CancellationToken cancellationToken)
        {
            queueStopwatch.Stop();
            cancellationToken.ThrowIfCancellationRequested();
            Stopwatch materializeStopwatch = Stopwatch.StartNew();
            BitmapSource? source;
            try
            {
                source = MaterializeSnapshotSource(snapshot);
            }
            finally
            {
                materializeStopwatch.Stop();
                materializeElapsed = materializeStopwatch.Elapsed;
            }

            BitmapSource? rendered = null;
            if (!string.IsNullOrWhiteSpace(options.RenderedFileName))
            {
                Stopwatch renderStopwatch = Stopwatch.StartNew();
                try
                {
                    DrawingGroup scene = ComposeSnapshotScene(snapshot, source);
                    rendered = RenderSnapshot(snapshot, scene, options.RenderedOptions, cancellationToken);
                }
                finally
                {
                    renderStopwatch.Stop();
                    renderElapsed = renderStopwatch.Elapsed;
                }
            }

            return new PreparedSnapshot(
                rendered,
                string.IsNullOrWhiteSpace(options.SourceFileName) ? null : source);
        }

        private static void SavePreparedSnapshotExports(
            PreparedSnapshot prepared,
            ImageViewSnapshotExportOptions options,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(options.RenderedFileName))
            {
                BitmapSource rendered = prepared.Rendered
                    ?? throw new InvalidOperationException("The rendered snapshot was not prepared.");
                SaveSnapshot(rendered, options.RenderedFileName, options.RenderedOptions, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(options.SourceFileName))
            {
                BitmapSource source = prepared.Source
                    ?? throw new InvalidOperationException("The source snapshot was not prepared.");
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

        private static RenderTargetBitmap RenderSnapshot(
            ImageViewSnapshot snapshot,
            DrawingGroup scene,
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
            renderedBitmap.Freeze();
            return renderedBitmap;
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

        private static void LogExportTiming(
            string? diagnosticContext,
            string outcome,
            TimeSpan queue,
            TimeSpan materialize,
            TimeSpan render,
            TimeSpan encodeWrite,
            TimeSpan total)
        {
            string context = string.IsNullOrWhiteSpace(diagnosticContext)
                ? "Task=ImageSnapshotExport"
                : diagnosticContext.Trim();
            log.Info(string.Format(
                CultureInfo.InvariantCulture,
                "ImageSnapshotExportTiming {0} Status={1} QueueMs={2:F3} MaterializeMs={3:F3} RenderMs={4:F3} EncodeWriteMs={5:F3} TotalMs={6:F3} EncodeConcurrency={7}",
                context,
                outcome,
                queue.TotalMilliseconds,
                materialize.TotalMilliseconds,
                render.TotalMilliseconds,
                encodeWrite.TotalMilliseconds,
                total.TotalMilliseconds,
                SnapshotEncodeWorker.MaxConcurrency));
        }

        private sealed record PreparedSnapshot(BitmapSource? Rendered, BitmapSource? Source);

    }
}
