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

        private static bool SaveSnapshot(
            BitmapSource snapshot,
            string fileName,
            ImageViewSnapshotSaveOptions options,
            CancellationToken cancellationToken = default,
            string? parametersJson = null,
            bool useAdaptivePng = false)
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
            BitmapFrame frame = ImageSnapshotMetadata.CreateFrame(snapshot, parametersJson,
                options.Format == ImageViewSnapshotFormat.Png ? "png" : "jpg");
            if (useAdaptivePng && AdaptiveSnapshotPng.TryEncode(frame, options.ScaleDivisor == 2, cancellationToken) is { } png)
            {
                SaveAtomically(png.Write, fileName, cancellationToken);
                return true;
            }
            encoder.Frames.Add(frame);
            SaveEncoderAtomically(encoder, fileName, cancellationToken);
            return false;
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

        internal static void SaveEncoderAtomically(
            BitmapEncoder encoder,
            string fileName,
            CancellationToken cancellationToken = default) =>
            SaveAtomically(encoder.Save, fileName, cancellationToken);

        private static void SaveAtomically(Action<Stream> save, string fileName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                    save(fileStream);
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
            TimeSpan heatmapElapsed = TimeSpan.Zero;
            long slotRequestedAt = 0, staQueuedAt = 0, staStartedAt = 0;
            long encodeQueuedAt = 0, encodeStartedAt = 0;
            bool usedPixelAlignedRgb48Png = false;
            bool usedHalfPrescaledRgb48 = false;
            bool usedAdaptivePng = false;
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

                slotRequestedAt = Stopwatch.GetTimestamp();
                encodeReservation = await SnapshotEncodeWorker.ReserveAsync(cancellationToken).ConfigureAwait(false);
                staQueuedAt = Stopwatch.GetTimestamp();
                PreparedSnapshot prepared = await SnapshotStaWorker.RunAsync(
                    () =>
                    {
                        staStartedAt = Stopwatch.GetTimestamp();
                        return PrepareSnapshotExports(
                            snapshot, options, queueStopwatch,
                            ref materializeElapsed, ref renderElapsed, ref heatmapElapsed, cancellationToken);
                    },
                    cancellationToken).ConfigureAwait(false);
                usedPixelAlignedRgb48Png = prepared.CanUseAdaptivePng && options.RenderedOptions.ScaleDivisor == 1;
                usedHalfPrescaledRgb48 = prepared.UsedHalfPrescaledRgb48;
                encodeQueuedAt = Stopwatch.GetTimestamp();
                Stopwatch encodeWriteStopwatch = Stopwatch.StartNew();
                try
                {
                    await encodeReservation.RunAsync(
                        () =>
                        {
                            encodeStartedAt = Stopwatch.GetTimestamp();
                            SavePreparedSnapshotExports(prepared, options, ref usedAdaptivePng, cancellationToken);
                        },
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
                long finishedAt = Stopwatch.GetTimestamp();
                encodeReservation?.Dispose();
                snapshot.Dispose();
                LogExportTiming(
                    options.DiagnosticContext,
                    outcome,
                    queueStopwatch.Elapsed,
                    materializeElapsed,
                    renderElapsed,
                    encodeWriteElapsed,
                    heatmapElapsed,
                    totalStopwatch.Elapsed,
                    GetQueueElapsed(slotRequestedAt, staQueuedAt, finishedAt),
                    GetQueueElapsed(staQueuedAt, staStartedAt, finishedAt),
                    GetQueueElapsed(encodeQueuedAt, encodeStartedAt, finishedAt),
                    usedPixelAlignedRgb48Png,
                    usedHalfPrescaledRgb48,
                    usedAdaptivePng);
            }
        }

        private static TimeSpan GetQueueElapsed(long queuedAt, long startedAt, long finishedAt) =>
            queuedAt == 0 ? TimeSpan.Zero : Stopwatch.GetElapsedTime(queuedAt, startedAt == 0 ? finishedAt : startedAt);

        private static PreparedSnapshot PrepareSnapshotExports(
            ImageViewSnapshot snapshot,
            ImageViewSnapshotExportOptions options,
            Stopwatch queueStopwatch,
            ref TimeSpan materializeElapsed,
            ref TimeSpan renderElapsed,
            ref TimeSpan heatmapElapsed,
            CancellationToken cancellationToken)
        {
            queueStopwatch.Stop();
            cancellationToken.ThrowIfCancellationRequested();
            Stopwatch materializeStopwatch = Stopwatch.StartNew();
            BitmapSource? source;
            bool usedOpaqueRgb48Source;
            try
            {
                source = MaterializeSnapshotSource(snapshot, options, out usedOpaqueRgb48Source);
            }
            finally
            {
                materializeStopwatch.Stop();
                materializeElapsed = materializeStopwatch.Elapsed;
            }

            BitmapSource? rendered = null;
            bool canUseAdaptivePng = false;
            bool usedHalfPrescaledRgb48 = false;
            if (!string.IsNullOrWhiteSpace(options.RenderedFileName))
            {
                BitmapSource? background = source;
                if (options.RenderedOptions.UseHeatmap)
                {
                    Stopwatch heatmapStopwatch = Stopwatch.StartNew();
                    try
                    {
                        if (source == null)
                            throw new InvalidOperationException("Heatmap rendering requires base image pixels.");
                        (int width, int height) = GetSnapshotOutputSize(snapshot, options.RenderedOptions.ScaleDivisor);
                        background = SnapshotHeatmap.Create(source, width, height, snapshot.DpiX, snapshot.DpiY, cancellationToken);
                    }
                    finally { heatmapElapsed = heatmapStopwatch.Elapsed; }
                }

                if (options.RenderedOptions.UseHeatmap && snapshot.Scene.Children.Count == 0)
                {
                    rendered = background;
                }
                else
                {
                    Stopwatch renderStopwatch = Stopwatch.StartNew();
                    ImageDrawing? scratchDrawing = null;
                    try
                    {
                        canUseAdaptivePng = background != null
                            && (usedOpaqueRgb48Source || CanUseAdaptiveRgb48Png(snapshot, background, options.RenderedOptions));
                        bool usePixelAlignedRgb48Png = canUseAdaptivePng && options.RenderedOptions.ScaleDivisor == 1;
                        // An odd side is rounded up by the export, so its source-to-output ratio is not exactly 2:1.
                        usedHalfPrescaledRgb48 = canUseAdaptivePng && options.RenderedOptions.ScaleDivisor == 2
                            && ((snapshot.PixelWidth | snapshot.PixelHeight) & 1) == 0;
                        if (usedHalfPrescaledRgb48)
                            background = new TransformedBitmap(background!, new ScaleTransform(0.5, 0.5));
                        DrawingGroup scene = ComposeSnapshotScene(snapshot, background, usePixelAlignedRgb48Png || usedHalfPrescaledRgb48, out scratchDrawing);
                        rendered = RenderSnapshot(snapshot, scene, options.RenderedOptions, cancellationToken);
                    }
                    finally
                    {
                        if (scratchDrawing != null)
                        {
                            scratchDrawing.ImageSource = null;
                            if (rendered != null && background is WriteableBitmap scratch)
                                SnapshotStaWorker.ReturnOpaqueBackground(scratch);
                        }
                        renderElapsed = renderStopwatch.Elapsed;
                    }
                }
            }

            return new PreparedSnapshot(
                rendered,
                string.IsNullOrWhiteSpace(options.SourceFileName) ? null : source,
                snapshot.RenderedParametersJson, canUseAdaptivePng, usedHalfPrescaledRgb48);
        }

        private static void SavePreparedSnapshotExports(
            PreparedSnapshot prepared,
            ImageViewSnapshotExportOptions options,
            ref bool usedAdaptivePng,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(options.RenderedFileName))
            {
                BitmapSource rendered = prepared.Rendered
                    ?? throw new InvalidOperationException("The rendered snapshot was not prepared.");
                usedAdaptivePng = SaveSnapshot(rendered, options.RenderedFileName, options.RenderedOptions,
                    cancellationToken, prepared.ParametersJson, prepared.CanUseAdaptivePng);
            }

            if (!string.IsNullOrWhiteSpace(options.SourceFileName))
            {
                BitmapSource source = prepared.Source
                    ?? throw new InvalidOperationException("The source snapshot was not prepared.");
                SaveSourceSnapshot(source, options.SourceFileName, options.SourceOptions, cancellationToken);
            }
        }

        private static BitmapSource? MaterializeSnapshotSource(
            ImageViewSnapshot snapshot,
            ImageViewSnapshotExportOptions options,
            out bool usedOpaqueRgb48Source)
        {
            usedOpaqueRgb48Source = false;
            using SnapshotImageBufferLease? buffer = snapshot.TakeImageBuffer();
            if (buffer == null)
                return snapshot.FrozenSource;

            ImageViewSnapshotSaveOptions rendered = options.RenderedOptions;
            if (!string.IsNullOrWhiteSpace(options.RenderedFileName)
                && string.IsNullOrWhiteSpace(options.SourceFileName)
                && rendered.Format == ImageViewSnapshotFormat.Png && rendered.ScaleDivisor == 1 && !rendered.UseHeatmap
                && IsPixelAligned(snapshot))
            {
                // In a 1:1 render, WPF takes the high byte of each RGB48 channel before compositing.
                BitmapSource? opaque = buffer.TryCreateOpaqueRgb48Bitmap(snapshot.PixelWidth, snapshot.PixelHeight, snapshot.DpiX, snapshot.DpiY);
                if (opaque != null)
                {
                    usedOpaqueRgb48Source = true;
                    return opaque;
                }
            }

            return buffer.ToFrozenBitmap(snapshot.DpiX, snapshot.DpiY);
        }

        private static DrawingGroup ComposeSnapshotScene(
            ImageViewSnapshot snapshot,
            BitmapSource? source,
            bool baseImageIsPixelAligned,
            out ImageDrawing? scratchDrawing)
        {
            scratchDrawing = null;
            if (source == null)
                return snapshot.Scene;

            ImageDrawing image = new(source, new Rect(snapshot.SceneSize));
            if (source is WriteableBitmap && !source.IsFrozen)
                scratchDrawing = image;
            Drawing background = image;
            if (baseImageIsPixelAligned)
            {
                // The base image has a 1:1 device-pixel mapping. Keep the sampling
                // option on its group so overlays still use their original rendering.
                DrawingGroup imageGroup = new();
                RenderOptions.SetBitmapScalingMode(imageGroup, BitmapScalingMode.NearestNeighbor);
                imageGroup.Children.Add(background);
                background = imageGroup;
            }

            DrawingGroup composedScene = new();
            composedScene.Children.Add(background);
            composedScene.Children.Add(snapshot.Scene);
            if (scratchDrawing == null)
                composedScene.Freeze();
            return composedScene;
        }

        private static bool CanUseAdaptiveRgb48Png(
            ImageViewSnapshot snapshot,
            BitmapSource source,
            ImageViewSnapshotSaveOptions options) =>
            options.Format == ImageViewSnapshotFormat.Png && options.ScaleDivisor is 1 or 2 && !options.UseHeatmap
            && source.Format == PixelFormats.Rgb48
            && source.PixelWidth == snapshot.PixelWidth && source.PixelHeight == snapshot.PixelHeight
            && IsPixelAligned(snapshot);

        private static bool IsPixelAligned(ImageViewSnapshot snapshot) =>
            snapshot.SceneSize.Width == snapshot.PixelWidth * 96.0 / snapshot.DpiX
            && snapshot.SceneSize.Height == snapshot.PixelHeight * 96.0 / snapshot.DpiY;

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
                context.PushTransform(new ScaleTransform(
                    outputWidth * 96.0 / snapshot.DpiX / snapshot.SceneSize.Width,
                    outputHeight * 96.0 / snapshot.DpiY / snapshot.SceneSize.Height));
                context.DrawDrawing(scene);
                context.Pop();
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
            TimeSpan heatmap,
            TimeSpan total,
            TimeSpan encodeSlotWait,
            TimeSpan staWait,
            TimeSpan encodeDispatchWait,
            bool usedPixelAlignedRgb48Png,
            bool usedHalfPrescaledRgb48,
            bool usedAdaptivePng)
        {
            if (!log.IsInfoEnabled)
                return;

            string context = string.IsNullOrWhiteSpace(diagnosticContext)
                ? "Task=ImageSnapshotExport"
                : diagnosticContext.Trim();
            log.Info(string.Format(
                CultureInfo.InvariantCulture,
                "ImageSnapshotExportTiming {0} Status={1} QueueMs={2:F3} MaterializeMs={3:F3} RenderMs={4:F3} EncodeWriteMs={5:F3} TotalMs={6:F3} EncodeConcurrency={7} HeatmapMs={8:F3}"
                + " EncodeSlotWaitMs={9:F3} StaWaitMs={10:F3} EncodeDispatchWaitMs={11:F3} PixelAlignedRgb48Png={12} AdaptivePng={13} HalfPrescaledRgb48={14}",
                context,
                outcome,
                queue.TotalMilliseconds,
                materialize.TotalMilliseconds,
                render.TotalMilliseconds,
                encodeWrite.TotalMilliseconds,
                total.TotalMilliseconds,
                SnapshotEncodeWorker.MaxConcurrency,
                heatmap.TotalMilliseconds,
                encodeSlotWait.TotalMilliseconds,
                staWait.TotalMilliseconds,
                encodeDispatchWait.TotalMilliseconds,
                usedPixelAlignedRgb48Png,
                usedAdaptivePng,
                usedHalfPrescaledRgb48));
        }

        private sealed record PreparedSnapshot(
            BitmapSource? Rendered, BitmapSource? Source, string? ParametersJson, bool CanUseAdaptivePng, bool UsedHalfPrescaledRgb48);

    }
}
