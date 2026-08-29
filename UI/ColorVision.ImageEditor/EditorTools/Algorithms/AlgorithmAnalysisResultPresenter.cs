using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    internal interface IAlgorithmAnalysisResultPresenter
    {
        void Present(AlgorithmResult result, string title);
    }

    internal sealed class DelegateAlgorithmAnalysisResultPresenter(Action<AlgorithmResult, string> present)
        : IAlgorithmAnalysisResultPresenter
    {
        public void Present(AlgorithmResult result, string title) => present(result, title);
    }

    internal static class AlgorithmAnalysisPresentationBudget
    {
        public const int MaximumImages = 8;
        public const int MaximumImageBytes = 16 * 1024 * 1024;
        public const int MaximumTotalImageBytes = 32 * 1024 * 1024;
        public const int MaximumPreviewEdge = 1600;
        public const int MaximumPreviewPixels = 1_048_576;
        public const int MaximumJsonPreviewCharacters = 32_768;
        public const int MaximumArtifactSummaries = 64;
    }

    internal sealed class AlgorithmAnalysisImagePresentation : IDisposable
    {
        private readonly object _sync = new();
        private readonly AlgorithmImageBufferLease _image;
        private readonly bool _previewAvailable;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly int _width;
        private readonly int _height;
        private readonly int _stride;
        private readonly double _dpiX;
        private readonly double _dpiY;
        private Task<BitmapSource>? _previewTask;
        private BitmapSource? _bitmap;
        private bool _disposed;

        public AlgorithmAnalysisImagePresentation(AlgorithmImageArtifact artifact, bool retainPixels)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            Name = artifact.Name;
            Role = artifact.Role;
            Format = artifact.Image.Format;
            _width = artifact.Image.Width;
            _height = artifact.Image.Height;
            _stride = artifact.Image.Stride;
            _dpiX = artifact.Image.DpiX;
            _dpiY = artifact.Image.DpiY;
            _image = artifact.Image.AcquireReadOnlyLease();
            _previewAvailable = retainPixels;
            DisplayRangePolicy = artifact.Image.Format.IsFloatingPoint()
                ? "finite-global-minmax; nan/-inf=0; +inf=255"
                : "native";
            PreferredExtension = artifact.Image.Format.IsFloatingPoint()
                ? ".png"
                : artifact.Image.Format.BitsPerChannel() > 8 ? ".tiff" : ".png";
            SuggestedFileName = SanitizeFileName($"{artifact.Name}-{artifact.Role}{PreferredExtension}");
            Diagnostic = retainPixels
                ? "选择图像标签时在后台按预算生成预览。"
                : $"图像超出自动预览预算（单图最多 {AlgorithmAnalysisPresentationBudget.MaximumImageBytes / (1024 * 1024)} MiB，全部图像最多 {AlgorithmAnalysisPresentationBudget.MaximumTotalImageBytes / (1024 * 1024)} MiB）；仍可显式导出原始分辨率图像。";
        }

        public string Name { get; }

        public string Role { get; }

        public AlgorithmImageFormat Format { get; }

        public BitmapSource Bitmap
            => GetPreviewAsync(CancellationToken.None).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException(Diagnostic);

        public bool IsBitmapMaterialized
        {
            get
            {
                lock (_sync) return _bitmap != null;
            }
        }

        public bool IsPreviewAvailable => _previewAvailable && !_disposed;

        public bool CanExport => !_disposed;

        public string PreferredExtension { get; }

        public string DisplayRangePolicy { get; private set; }

        public string SuggestedFileName { get; }

        public string Diagnostic { get; }

        public bool TryGetPreview(out BitmapSource? bitmap)
        {
            if (!IsPreviewAvailable)
            {
                bitmap = null;
                return false;
            }
            bitmap = Bitmap;
            return true;
        }

        public async Task<BitmapSource?> GetPreviewAsync(CancellationToken cancellationToken)
        {
            if (!IsPreviewAvailable) return null;
            Task<BitmapSource> task;
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_bitmap != null) return _bitmap;
                task = _previewTask ??= Task.Run(CreatePreview, _lifetime.Token);
            }
            try
            {
                return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_previewTask, task) && !_disposed) _previewTask = null;
                }
                throw;
            }
        }

        public string Export(string path, bool overwrite = false)
            => ExportAsync(path, overwrite, CancellationToken.None).GetAwaiter().GetResult();

        public Task<string> ExportAsync(string path, bool overwrite, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            lock (_sync) ObjectDisposedException.ThrowIf(_disposed, this);
            return Task.Run(() => ExportCore(path, overwrite, cancellationToken), cancellationToken);
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
            }
            _lifetime.Cancel();
            _lifetime.Dispose();
            _image.Dispose();
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        }

        private BitmapSource CreatePreview()
        {
            (BitmapSource bitmap, string policy) = CreateDisplayBitmap(fullResolution: false, _lifetime.Token);
            if (bitmap.CanFreeze) bitmap.Freeze();
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _bitmap = bitmap;
                DisplayRangePolicy = policy;
            }
            return bitmap;
        }

        private string ExportCore(string path, bool overwrite, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath)
                ?? throw new ArgumentException("The export path has no directory.", nameof(path));
            Directory.CreateDirectory(directory);
            if (!overwrite && File.Exists(fullPath)) throw new IOException($"The export target already exists: {fullPath}");
            BitmapEncoder encoder = Path.GetExtension(fullPath).ToLowerInvariant() switch
            {
                ".tif" or ".tiff" => new TiffBitmapEncoder(),
                ".png" => new PngBitmapEncoder(),
                _ when PreferredExtension == ".tiff" => new TiffBitmapEncoder(),
                _ => new PngBitmapEncoder(),
            };
            (BitmapSource bitmap, string policy) = CreateDisplayBitmap(fullResolution: true, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (bitmap.CanFreeze) bitmap.Freeze();
            DisplayRangePolicy = policy;
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    encoder.Save(stream);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, fullPath, overwrite);
                return fullPath;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch { }
            }
        }

        private (BitmapSource Bitmap, string Policy) CreateDisplayBitmap(bool fullResolution, CancellationToken cancellationToken)
        {
            ReadOnlyMemory<byte> pixels = _image.Data;
            (int targetWidth, int targetHeight) = fullResolution
                ? (_width, _height)
                : CalculatePreviewDimensions(_width, _height);
            if (!Format.IsFloatingPoint())
            {
                if (fullResolution)
                    return (CreateNativeFullResolutionBitmap(pixels, cancellationToken), "native; original resolution");
                int bytesPerPixel = Format.BytesPerPixel();
                int nativeTargetStride = checked(targetWidth * bytesPerPixel);
                byte[] nativeTarget = SamplePixels(pixels.Span, _width, _height, _stride, targetWidth, targetHeight, bytesPerPixel, cancellationToken);
                using AlgorithmImageBuffer sampled = new(targetWidth, targetHeight, nativeTargetStride, Format, nativeTarget, _dpiX, _dpiY);
                return (ImageAlgorithmInputFactory.ToWriteableBitmap(sampled), "native; nearest-neighbor bounded preview");
            }

            int channels = Format.Channels();
            int colorChannels = Format == AlgorithmImageFormat.Bgra128Float ? 3 : channels;
            ReadOnlySpan<byte> source = pixels.Span;
            double minimum = double.PositiveInfinity;
            double maximum = double.NegativeInfinity;
            for (int y = 0; y < _height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int row = y * _stride;
                for (int x = 0; x < _width; x++)
                {
                    int pixel = row + x * channels * sizeof(float);
                    for (int channel = 0; channel < colorChannels; channel++)
                    {
                        float value = MemoryMarshal.Read<float>(source.Slice(pixel + channel * sizeof(float), sizeof(float)));
                        if (!float.IsFinite(value)) continue;
                        minimum = Math.Min(minimum, value);
                        maximum = Math.Max(maximum, value);
                    }
                }
            }

            bool hasRange = double.IsFinite(minimum) && double.IsFinite(maximum) && maximum > minimum;
            int targetStride = checked(targetWidth * channels);
            byte[] target = new byte[checked(targetStride * targetHeight)];
            for (int y = 0; y < targetHeight; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int sourceY = Math.Min(_height - 1, checked((int)((long)y * _height / targetHeight)));
                int sourceRow = sourceY * _stride;
                int targetRow = y * targetStride;
                for (int x = 0; x < targetWidth; x++)
                {
                    int sourceX = Math.Min(_width - 1, checked((int)((long)x * _width / targetWidth)));
                    int sourcePixel = sourceRow + sourceX * channels * sizeof(float);
                    int targetPixel = targetRow + x * channels;
                    for (int channel = 0; channel < colorChannels; channel++)
                    {
                        float value = MemoryMarshal.Read<float>(source.Slice(sourcePixel + channel * sizeof(float), sizeof(float)));
                        double normalized = value switch
                        {
                            float.NaN => 0,
                            float.PositiveInfinity => 1,
                            float.NegativeInfinity => 0,
                            _ when hasRange => (value - minimum) / (maximum - minimum),
                            _ => Math.Clamp(value, 0, 1),
                        };
                        target[targetPixel + channel] = ToDisplayByte(normalized);
                    }
                    if (channels == 4)
                    {
                        float alpha = MemoryMarshal.Read<float>(source.Slice(sourcePixel + 3 * sizeof(float), sizeof(float)));
                        double normalizedAlpha = alpha switch
                        {
                            float.NaN => 0,
                            float.PositiveInfinity => 1,
                            float.NegativeInfinity => 0,
                            _ => Math.Clamp(alpha, 0, 1),
                        };
                        target[targetPixel + 3] = ToDisplayByte(normalizedAlpha);
                    }
                }
            }

            PixelFormat pixelFormat = channels switch
            {
                1 => PixelFormats.Gray8,
                3 => PixelFormats.Bgr24,
                4 => PixelFormats.Bgra32,
                _ => throw new ArgumentOutOfRangeException(nameof(Format)),
            };
            WriteableBitmap bitmap = new(targetWidth, targetHeight, _dpiX, _dpiY, pixelFormat, null);
            bitmap.WritePixels(new Int32Rect(0, 0, targetWidth, targetHeight), target, targetStride, 0);
            string policy = hasRange
                ? "finite-global-minmax; nan/-inf=0; +inf=255"
                : "finite-unit-clamp; nan/-inf=0; +inf=255";
            return (bitmap, policy);
        }

        private BitmapSource CreateNativeFullResolutionBitmap(ReadOnlyMemory<byte> pixels, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PixelFormat pixelFormat = Format switch
            {
                AlgorithmImageFormat.Gray8 => PixelFormats.Gray8,
                AlgorithmImageFormat.Gray16 => PixelFormats.Gray16,
                AlgorithmImageFormat.Bgr24 => PixelFormats.Bgr24,
                AlgorithmImageFormat.Bgra32 => PixelFormats.Bgra32,
                AlgorithmImageFormat.Bgr48 => PixelFormats.Rgb48,
                AlgorithmImageFormat.Bgra64 => PixelFormats.Rgba64,
                _ => throw new ArgumentOutOfRangeException(nameof(Format)),
            };
            byte[] source;
            if (Format is AlgorithmImageFormat.Bgr48 or AlgorithmImageFormat.Bgra64)
            {
                source = pixels.ToArray();
                int channels = Format.Channels();
                for (int y = 0; y < _height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int row = y * _stride;
                    for (int x = 0; x < _width; x++)
                    {
                        int pixel = row + x * channels * sizeof(ushort);
                        (source[pixel], source[pixel + 4]) = (source[pixel + 4], source[pixel]);
                        (source[pixel + 1], source[pixel + 5]) = (source[pixel + 5], source[pixel + 1]);
                    }
                }
            }
            else if (MemoryMarshal.TryGetArray(pixels, out ArraySegment<byte> segment)
                && segment.Offset == 0
                && segment.Array != null)
            {
                source = segment.Array;
            }
            else
            {
                source = pixels.ToArray();
            }
            return BitmapSource.Create(_width, _height, _dpiX, _dpiY, pixelFormat, null, source, _stride);
        }

        private static (int Width, int Height) CalculatePreviewDimensions(int width, int height)
        {
            double scale = Math.Min(
                1,
                Math.Min(
                    AlgorithmAnalysisPresentationBudget.MaximumPreviewEdge / (double)Math.Max(width, height),
                    Math.Sqrt(AlgorithmAnalysisPresentationBudget.MaximumPreviewPixels / (double)checked((long)width * height))));
            return (
                Math.Max(1, (int)Math.Floor(width * scale)),
                Math.Max(1, (int)Math.Floor(height * scale)));
        }

        private static byte[] SamplePixels(
            ReadOnlySpan<byte> source,
            int sourceWidth,
            int sourceHeight,
            int sourceStride,
            int targetWidth,
            int targetHeight,
            int bytesPerPixel,
            CancellationToken cancellationToken)
        {
            int targetStride = checked(targetWidth * bytesPerPixel);
            byte[] target = new byte[checked(targetStride * targetHeight)];
            for (int y = 0; y < targetHeight; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int sourceY = Math.Min(sourceHeight - 1, checked((int)((long)y * sourceHeight / targetHeight)));
                for (int x = 0; x < targetWidth; x++)
                {
                    int sourceX = Math.Min(sourceWidth - 1, checked((int)((long)x * sourceWidth / targetWidth)));
                    source.Slice(
                        checked(sourceY * sourceStride + sourceX * bytesPerPixel),
                        bytesPerPixel).CopyTo(target.AsSpan(checked(y * targetStride + x * bytesPerPixel), bytesPerPixel));
                }
            }
            return target;
        }

        private static byte ToDisplayByte(double value)
            => (byte)Math.Clamp(Math.Round(value * byte.MaxValue, MidpointRounding.AwayFromZero), byte.MinValue, byte.MaxValue);
    }

    internal sealed class AlgorithmAnalysisResultPresentation : IDisposable
    {
        private readonly string _jsonSummary;
        private readonly CancellationTokenSource _lifetime = new();
        private int _disposed;

        public AlgorithmAnalysisResultPresentation(AlgorithmResult result, string title)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Title = string.IsNullOrWhiteSpace(title) ? "算法结果" : title;
            AlgorithmImageArtifact[] imageArtifacts = result.Artifacts.OfType<AlgorithmImageArtifact>().ToArray();
            List<AlgorithmAnalysisImagePresentation> images = new();
            long retainedBytes = 0;
            try
            {
                foreach (AlgorithmImageArtifact artifact in imageArtifacts.Take(AlgorithmAnalysisPresentationBudget.MaximumImages))
                {
                    int byteCount = artifact.Image.Data.Length;
                    bool retain = byteCount <= AlgorithmAnalysisPresentationBudget.MaximumImageBytes
                        && retainedBytes <= AlgorithmAnalysisPresentationBudget.MaximumTotalImageBytes - byteCount;
                    if (retain) retainedBytes += byteCount;
                    images.Add(new AlgorithmAnalysisImagePresentation(artifact, retain));
                }
            }
            catch
            {
                foreach (AlgorithmAnalysisImagePresentation image in images) image.Dispose();
                _lifetime.Dispose();
                throw;
            }
            Images = images.AsReadOnly();
            OmittedImageCount = Math.Max(0, imageArtifacts.Length - images.Count);
            _jsonSummary = BuildJsonSummary(result, OmittedImageCount);
        }

        public AlgorithmResult Result { get; }

        public string Title { get; }

        public IReadOnlyList<AlgorithmAnalysisImagePresentation> Images { get; }

        public int OmittedImageCount { get; }

        public string Json => _jsonSummary;

        public CancellationToken LifetimeToken => _lifetime.Token;

        public string ExportJson(string path, bool overwrite = false)
            => AlgorithmResultExporter.ExportJson(Result, path, overwrite);

        public Task<string> ExportJsonAsync(
            string path,
            bool overwrite,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress = null)
            => AlgorithmResultExporter.ExportJsonAsync(Result, path, overwrite, cancellationToken, progress);

        public IReadOnlyList<string> ExportCsv(string path, bool overwrite = false)
            => AlgorithmResultExporter.ExportCsvBundle(Result, path, overwrite);

        public Task<IReadOnlyList<string>> ExportCsvAsync(
            string path,
            bool overwrite,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress = null)
            => AlgorithmResultExporter.ExportCsvBundleAsync(Result, path, overwrite, cancellationToken, progress);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _lifetime.Cancel();
            foreach (AlgorithmAnalysisImagePresentation image in Images) image.Dispose();
            _lifetime.Dispose();
        }

        private static string BuildJsonSummary(AlgorithmResult result, int omittedImageCount)
        {
            object[] artifacts = result.Artifacts.Take(AlgorithmAnalysisPresentationBudget.MaximumArtifactSummaries)
                .Select(artifact => new
                {
                    Name = Clip(artifact.Name),
                    Type = artifact.GetType().Name,
                    Role = Clip((artifact as AlgorithmImageArtifact)?.Role),
                    Image = artifact is AlgorithmImageArtifact image
                        ? new { image.Image.Width, image.Image.Height, Format = image.Image.Format.ToString(), image.Image.Stride }
                        : null,
                    Count = artifact switch
                    {
                        AlgorithmMeasurementArtifact measurement => measurement.Measurements.Count,
                        AlgorithmTableArtifact table => table.Rows.Count,
                        AlgorithmGeometryArtifact geometry => geometry.Geometries.Count,
                        _ => (int?)null,
                    },
                })
                .ToArray();
            string summary = JsonSerializer.Serialize(new
            {
                result.InvocationId,
                AlgorithmId = Clip(result.AlgorithmId.Value),
                AlgorithmVersion = result.AlgorithmVersion.ToString(),
                Status = result.Status.ToString(),
                ArtifactCount = result.Artifacts.Count,
                OmittedArtifactCount = Math.Max(0, result.Artifacts.Count - artifacts.Length),
                OmittedImageCount = omittedImageCount,
                Failures = result.Failures.Take(32).Select(failure => new
                {
                    Code = Clip(failure.Code),
                    Message = Clip(failure.Message),
                    Path = Clip(failure.Path),
                    DetailCount = failure.Details?.Count ?? 0,
                }),
                Diagnostics = new
                {
                    ProviderId = Clip(result.Diagnostics.ProviderId),
                    result.Diagnostics.ProviderKind,
                    result.Diagnostics.StartedAt,
                    result.Diagnostics.Duration,
                    Messages = result.Diagnostics.Messages.Take(32).Select(message => new
                    {
                        Code = Clip(message.Code),
                        Message = Clip(message.Message),
                        Severity = Clip(message.Severity),
                        DataCount = message.Data?.Count ?? 0,
                    }),
                },
                Artifacts = artifacts,
                Note = "这是有界摘要；使用“导出 JSON/CSV”生成完整结果。",
            }, new JsonSerializerOptions(AlgorithmJson.Options) { WriteIndented = true });
            if (summary.Length <= AlgorithmAnalysisPresentationBudget.MaximumJsonPreviewCharacters) return summary;
            const string suffix = "\n... JSON 摘要已按展示预算截断；完整内容请显式导出。";
            return summary[..(AlgorithmAnalysisPresentationBudget.MaximumJsonPreviewCharacters - suffix.Length)] + suffix;
        }

        private static string? Clip(string? value)
            => value == null || value.Length <= 256 ? value : value[..253] + "...";
    }

    internal sealed class DefaultAlgorithmAnalysisResultPresenter : IAlgorithmAnalysisResultPresenter
    {
        public static DefaultAlgorithmAnalysisResultPresenter Instance { get; } = new();

        internal static AlgorithmAnalysisResultPresentation CreatePresentation(AlgorithmResult result, string title)
            => new(result, title);

        internal static FrameworkElement CreateContent(AlgorithmAnalysisResultPresentation presentation)
        {
            ArgumentNullException.ThrowIfNull(presentation);
            DockPanel root = new();
            StackPanel buttons = new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8),
                DataContext = presentation,
            };
            Button exportCsv = new() { Content = "导出 CSV", MinWidth = 96, Margin = new Thickness(0, 0, 8, 0), DataContext = presentation };
            Button exportJson = new() { Content = "导出 JSON", MinWidth = 96, Margin = new Thickness(0, 0, 8, 0), DataContext = presentation };
            ProgressBar exportProgress = new() { Width = 120, Height = 16, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 0, 8, 0), Visibility = Visibility.Collapsed };
            TextBlock exportStatus = new() { MinWidth = 120, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Visibility = Visibility.Collapsed };
            Button cancelExport = new() { Content = "取消导出", MinWidth = 88, Margin = new Thickness(0, 0, 8, 0), IsEnabled = false };
            Button close = new() { Content = "关闭", MinWidth = 88, IsCancel = true };
            buttons.Children.Add(exportStatus);
            buttons.Children.Add(exportProgress);
            buttons.Children.Add(cancelExport);
            buttons.Children.Add(exportCsv);
            buttons.Children.Add(exportJson);
            buttons.Children.Add(close);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);

            TabControl tabs = new() { Margin = new Thickness(8, 0, 8, 8), DataContext = presentation };
            foreach (AlgorithmAnalysisImagePresentation imagePresentation in presentation.Images)
            {
                Grid imagePanel = new();
                imagePanel.RowDefinitions.Add(new RowDefinition());
                imagePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Image image = new()
                {
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(4),
                    DataContext = imagePresentation,
                };
                TextBlock diagnostic = new()
                {
                    Text = imagePresentation.Diagnostic,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(16),
                    DataContext = imagePresentation,
                };
                Button exportImage = new()
                {
                    Content = imagePresentation.PreferredExtension == ".png" ? "导出 PNG" : "导出 TIFF",
                    MinWidth = 96,
                    Margin = new Thickness(4),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    DataContext = imagePresentation,
                    IsEnabled = imagePresentation.CanExport,
                };
                Grid.SetRow(exportImage, 1);
                imagePanel.Children.Add(image);
                imagePanel.Children.Add(diagnostic);
                imagePanel.Children.Add(exportImage);
                tabs.Items.Add(new TabItem
                {
                    Header = $"{imagePresentation.Name} ({imagePresentation.Role})",
                    Content = imagePanel,
                    DataContext = imagePresentation,
                });
                exportImage.Click += async (_, _) => await ExportImageAsync(imagePresentation, exportImage, presentation.LifetimeToken);
            }

            tabs.SelectionChanged += async (_, _) => await MaterializeSelectedImageAsync(tabs, presentation.LifetimeToken);

            TextBox text = new()
            {
                Text = presentation.Json,
                IsReadOnly = true,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                DataContext = presentation,
            };
            tabs.Items.Add(new TabItem { Header = "JSON / 结构化结果", Content = text, DataContext = presentation });
            root.Children.Add(tabs);
            if (tabs.Items.Count > 0 && tabs.SelectedIndex < 0) tabs.SelectedIndex = 0;
            _ = MaterializeSelectedImageAsync(tabs, presentation.LifetimeToken);

            CancellationTokenSource? activeExport = null;
            cancelExport.Click += (_, _) => activeExport?.Cancel();
            exportJson.Click += async (_, _) =>
            {
                Window? owner = Window.GetWindow(exportJson);
                SaveFileDialog dialog = new() { Filter = "JSON 文件 (*.json)|*.json", FileName = "algorithm-result.json", AddExtension = true, OverwritePrompt = false };
                if (ShowDialog(dialog, owner) != true) return;
                await RunExportAsync(
                    async (token, progress) => await presentation.ExportJsonAsync(dialog.FileName, overwrite: false, token, progress),
                    owner);
            };
            exportCsv.Click += async (_, _) =>
            {
                Window? owner = Window.GetWindow(exportCsv);
                SaveFileDialog dialog = new() { Filter = "CSV 文件 (*.csv)|*.csv", FileName = "algorithm-result.csv", AddExtension = true, OverwritePrompt = false };
                if (ShowDialog(dialog, owner) != true) return;
                await RunExportAsync(
                    async (token, progress) => await presentation.ExportCsvAsync(dialog.FileName, overwrite: false, token, progress),
                    owner);
            };
            close.Click += (_, _) => Window.GetWindow(close)?.Close();
            return root;

            async Task RunExportAsync(
                Func<CancellationToken, IProgress<AlgorithmProgress>, Task> export,
                Window? owner)
            {
                if (activeExport != null) return;
                activeExport = CancellationTokenSource.CreateLinkedTokenSource(presentation.LifetimeToken);
                exportJson.IsEnabled = false;
                exportCsv.IsEnabled = false;
                cancelExport.IsEnabled = true;
                exportProgress.Value = 0;
                exportProgress.Visibility = Visibility.Visible;
                exportStatus.Text = "准备导出…";
                exportStatus.Visibility = Visibility.Visible;
                Progress<AlgorithmProgress> progress = new(value =>
                {
                    exportProgress.Value = Math.Clamp(value.Fraction, 0, 1);
                    exportStatus.Text = string.IsNullOrWhiteSpace(value.Message) ? value.Stage : value.Message;
                });
                try
                {
                    await export(activeExport.Token, progress);
                    if (owner == null) MessageBox.Show("导出完成。", "算法结果", MessageBoxButton.OK, MessageBoxImage.Information);
                    else MessageBox.Show(owner, "导出完成。", owner.Title, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (OperationCanceledException)
                {
                    exportStatus.Text = "导出已取消。";
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    if (owner == null) MessageBox.Show(exception.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    else MessageBox.Show(owner, exception.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    activeExport.Dispose();
                    activeExport = null;
                    exportJson.IsEnabled = true;
                    exportCsv.IsEnabled = true;
                    cancelExport.IsEnabled = false;
                }
            }
        }

        private static async Task MaterializeSelectedImageAsync(TabControl tabs, CancellationToken cancellationToken)
        {
            if (tabs.SelectedItem is not TabItem { Content: Grid panel, DataContext: AlgorithmAnalysisImagePresentation presentation }) return;
            Image? image = panel.Children.OfType<Image>().SingleOrDefault();
            TextBlock? diagnostic = panel.Children.OfType<TextBlock>().SingleOrDefault();
            if (image == null || image.Source != null) return;
            if (!presentation.IsPreviewAvailable)
            {
                if (diagnostic != null) diagnostic.Visibility = Visibility.Visible;
                return;
            }
            if (diagnostic != null)
            {
                diagnostic.Text = "正在后台生成有界预览…";
                diagnostic.Visibility = Visibility.Visible;
            }
            try
            {
                BitmapSource? bitmap = await presentation.GetPreviewAsync(cancellationToken);
                if (bitmap == null) return;
                image.Source = bitmap;
                if (diagnostic != null) diagnostic.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                if (diagnostic != null)
                {
                    diagnostic.Text = $"预览生成失败：{exception.Message}";
                    diagnostic.Visibility = Visibility.Visible;
                }
            }
        }

        public void Present(AlgorithmResult result, string title)
        {
            AlgorithmAnalysisResultPresentation presentation = CreatePresentation(result, title);
            Window window = new()
            {
                Owner = Application.Current.GetActiveWindow(),
                Title = $"{presentation.Title} - 结果",
                Width = 720,
                Height = 520,
                Content = CreateContent(presentation),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            window.Closed += (_, _) => presentation.Dispose();
            try { window.ShowDialog(); }
            finally { presentation.Dispose(); }
        }

        private static async Task ExportImageAsync(
            AlgorithmAnalysisImagePresentation presentation,
            FrameworkElement source,
            CancellationToken cancellationToken)
        {
            Window? owner = Window.GetWindow(source);
            bool png = presentation.PreferredExtension == ".png";
            SaveFileDialog dialog = new()
            {
                Filter = png ? "PNG 图像 (*.png)|*.png" : "TIFF 图像 (*.tif;*.tiff)|*.tif;*.tiff",
                FileName = presentation.SuggestedFileName,
                DefaultExt = presentation.PreferredExtension,
                AddExtension = true,
                OverwritePrompt = false,
            };
            if (ShowDialog(dialog, owner) != true) return;
            source.IsEnabled = false;
            try
            {
                await TryExportAsync(owner, () => presentation.ExportAsync(dialog.FileName, overwrite: false, cancellationToken));
            }
            finally
            {
                if (presentation.CanExport) source.IsEnabled = true;
            }
        }

        private static bool? ShowDialog(SaveFileDialog dialog, Window? owner)
            => owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);

        private static async Task TryExportAsync(Window? owner, Func<Task> export)
        {
            try
            {
                await export();
                if (owner == null) MessageBox.Show("导出完成。", "算法结果", MessageBoxButton.OK, MessageBoxImage.Information);
                else MessageBox.Show(owner, "导出完成。", owner.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                if (owner == null) MessageBox.Show(exception.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
                else MessageBox.Show(owner, exception.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
