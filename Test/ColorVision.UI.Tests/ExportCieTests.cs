using ColorVision.Engine.Media;
using ColorVision.FileIO;
using ColorVision.Solution.Mru;
using OpenCvSharp;
using System.Collections.Specialized;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class ExportCieTests
{
    [Fact]
    public void RememberExportLocationPreservesTheBoundSavePathDuringListRefresh()
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-{Guid.NewGuid():N}");
        string firstPath = Path.Combine(root, "first");
        string selectedPath = Path.Combine(root, "selected");
        string sourcePath = Path.Combine(root, "sample.cvraw");
        Directory.CreateDirectory(firstPath);
        Directory.CreateDirectory(selectedPath);
        try
        {
            WriteRawFixture(sourcePath, rows: 2, cols: 3, channels: 3);
            var store = new MemoryMruPathStore(
            [
                new MruPathEntry(firstPath, DateTimeOffset.UtcNow.AddMinutes(-1)),
            ]);
            var viewModel = new VExportCIE(sourcePath, new MruPathService(store));
            bool listWasCleared = false;
            viewModel.RecentImageSaveList.CollectionChanged += (_, args) =>
            {
                if (args.Action != NotifyCollectionChangedAction.Reset)
                    return;
                listWasCleared = true;
                viewModel.SavePath = string.Empty;
            };

            viewModel.RememberExportLocation(selectedPath);

            Assert.True(listWasCleared);
            Assert.Equal(Path.GetFullPath(selectedPath), viewModel.SavePath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TiffExportCreatesTheSelectedDirectoryAndUsesCompressedDefaults()
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(root, "sample.cvraw");
        string outputPath = Path.Combine(root, "new-output");
        Directory.CreateDirectory(root);
        try
        {
            byte[] sourceData = WriteRawFixture(sourcePath, rows: 256, cols: 256, channels: 3);
            var store = new MemoryMruPathStore(
            [
                new MruPathEntry(root, DateTimeOffset.UtcNow),
            ]);
            var viewModel = new VExportCIE(sourcePath, new MruPathService(store))
            {
                SavePath = outputPath,
                Name = "compressed",
                ExportImageFormat = ImageFormat.Tiff,
            };

            Assert.Equal(VExportCIE.DefaultTiffCompression, viewModel.TiffCompression);
            Assert.Equal(VExportCIE.DefaultPngCompressionLevel, viewModel.PngCompressionLevel);
            Assert.Equal(VExportCIE.DefaultJpegQuality, viewModel.JpegQuality);
            Assert.Equal(new[] { 5, 8 }, viewModel.TiffCompressionOptions.Values);
            viewModel.ExportImageFormat = ImageFormat.Png;
            viewModel.Compression = 7;
            Assert.Equal(7, viewModel.PngCompressionLevel);
            viewModel.ExportImageFormat = ImageFormat.Jpeg;
            viewModel.Compression = 80;
            Assert.Equal(80, viewModel.JpegQuality);
            viewModel.ExportImageFormat = ImageFormat.Tiff;

            VExportCIE.SaveToTifOrThrow(viewModel);

            string exportedPath = Path.Combine(outputPath, "compressedSrc.tiff");
            Assert.True(File.Exists(exportedPath));
            Assert.True(new FileInfo(exportedPath).Length < sourceData.Length);
            using Mat exported = Cv2.ImRead(exportedPath, ImreadModes.Unchanged);
            Assert.Equal(256, exported.Rows);
            Assert.Equal(256, exported.Cols);
            Assert.Equal(3, exported.Channels());
            Assert.Equal(MatType.CV_16U, exported.Depth());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExportPolicyDistinguishesCvRawAndCvcie()
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-policy-{Guid.NewGuid():N}");
        string rawPath = Path.Combine(root, "sample.cvraw");
        string ciePath = Path.Combine(root, "sample.cvcie");
        Directory.CreateDirectory(root);
        try
        {
            WriteRawFixture(rawPath, rows: 2, cols: 3, channels: 3);
            WriteCieFixture(ciePath, rows: 2, cols: 3, channels: 3);
            var store = new MemoryMruPathStore([]);

            var raw = new VExportCIE(rawPath, new MruPathService(store));
            Assert.Equal("CVRAW", raw.FileKindName);
            Assert.Equal(3, raw.AvailableImageFormats.Count);
            Assert.Contains(raw.AvailableImageFormats.Values, format => format.Guid == ImageFormat.Tiff.Guid);
            Assert.Contains(raw.AvailableImageFormats.Values, format => format.Guid == ImageFormat.Png.Guid);
            Assert.Contains(raw.AvailableImageFormats.Values, format => format.Guid == ImageFormat.Jpeg.Guid);
            Assert.DoesNotContain(raw.AvailableImageFormats.Values, format => format.Guid == ImageFormat.Bmp.Guid);

            var cie = new VExportCIE(ciePath, new MruPathService(store));
            Assert.Equal("CVCIE", cie.FileKindName);
            Assert.Single(cie.AvailableImageFormats);
            Assert.Equal(ImageFormat.Tiff.Guid, cie.AvailableImageFormats.Single().Value.Guid);
            Assert.True(cie.IsCieThreeChannel);
            Assert.True(cie.IsExportChannelX);
            Assert.True(cie.IsExportChannelY);
            Assert.True(cie.IsExportChannelZ);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InteractiveExportNormalizesCompressionValuesThatAreNotShownInTheUi()
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-interactive-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(root, "sample.cvraw");
        Directory.CreateDirectory(root);
        try
        {
            WriteRawFixture(sourcePath, rows: 2, cols: 3, channels: 3);
            var viewModel = new VExportCIE(sourcePath, new MruPathService(new MemoryMruPathStore([])));

            viewModel.TiffCompression = 1;
            viewModel.PrepareForInteractiveExport();
            Assert.Equal(VExportCIE.DefaultTiffCompression, viewModel.TiffCompression);

            viewModel.TiffCompression = VExportCIE.ZipTiffCompression;
            viewModel.PrepareForInteractiveExport();
            Assert.Equal(VExportCIE.ZipTiffCompression, viewModel.TiffCompression);

            viewModel.ExportImageFormat = ImageFormat.Png;
            viewModel.PngCompressionLevel = 7;
            viewModel.PrepareForInteractiveExport();
            Assert.Equal(VExportCIE.AutomaticPngCompressionLevel, viewModel.PngCompressionLevel);

            viewModel.ExportImageFormat = ImageFormat.Jpeg;
            viewModel.JpegQuality = 80;
            viewModel.PrepareForInteractiveExport();
            Assert.Equal(VExportCIE.DefaultJpegQuality, viewModel.JpegQuality);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("png", VExportCIE.AutomaticPngCompressionLevel, ".png")]
    [InlineData("tiff", VExportCIE.DefaultTiffCompression, ".tiff")]
    [InlineData("tiff", VExportCIE.ZipTiffCompression, ".tiff")]
    public void CvRaw16BitLosslessExportPreservesDepthAndPixels(string format, int compression, string extension)
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-depth-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(root, "sample.cvraw");
        Directory.CreateDirectory(root);
        try
        {
            byte[] sourceData = WritePatternedRawFixture(sourcePath, rows: 5, cols: 7, channels: 3);
            var viewModel = new VExportCIE(sourcePath, new MruPathService(new MemoryMruPathStore([])))
            {
                SavePath = root,
                Name = "preserved",
                ExportImageFormat = format == "png" ? ImageFormat.Png : ImageFormat.Tiff,
            };
            viewModel.Compression = compression;

            VExportCIE.SaveToTifOrThrow(viewModel);

            string exportedPath = Path.Combine(root, "preservedSrc" + extension);
            using Mat exported = Cv2.ImRead(exportedPath, ImreadModes.Unchanged);
            Assert.Equal(MatType.CV_16U, exported.Depth());
            Assert.Equal(3, exported.Channels());
            Assert.Equal(5, exported.Rows);
            Assert.Equal(7, exported.Cols);
            Assert.True(exported.IsContinuous());
            byte[] actual = new byte[sourceData.Length];
            Marshal.Copy(exported.Data, actual, 0, actual.Length);
            Assert.Equal(sourceData, actual);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Cvcie32BitExportWritesOnlyTheSelectedFloatChannel()
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-cvcie-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(root, "sample.cvcie");
        Directory.CreateDirectory(root);
        try
        {
            const int rows = 3;
            const int cols = 4;
            float[] sourceValues = WriteCieFixture(sourcePath, rows, cols, channels: 3);
            var viewModel = new VExportCIE(sourcePath, new MruPathService(new MemoryMruPathStore([])))
            {
                SavePath = root,
                Name = "channels",
                ExportImageFormat = ImageFormat.Tiff,
                IsExportSrc = false,
                IsExportChannelX = false,
                IsExportChannelY = true,
                IsExportChannelZ = false,
            };

            VExportCIE.SaveToTifOrThrow(viewModel);

            string yPath = Path.Combine(root, "channels_Y.tiff");
            Assert.True(File.Exists(yPath));
            Assert.False(File.Exists(Path.Combine(root, "channels_X.tiff")));
            Assert.False(File.Exists(Path.Combine(root, "channels_Z.tiff")));
            using Mat exported = Cv2.ImRead(yPath, ImreadModes.Unchanged);
            Assert.Equal(MatType.CV_32F, exported.Depth());
            Assert.Equal(1, exported.Channels());
            Assert.True(exported.IsContinuous());
            float[] actual = new float[rows * cols];
            Marshal.Copy(exported.Data, actual, 0, actual.Length);
            Assert.Equal(sourceValues.Skip(rows * cols).Take(rows * cols), actual);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CvcieResolvesARelativeAssociatedSourceBeforeOfferingAndExportingIt()
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-associated-{Guid.NewGuid():N}");
        string rawPath = Path.Combine(root, "renamed-source.cvraw");
        string ciePath = Path.Combine(root, "sample.cvcie");
        Directory.CreateDirectory(root);
        try
        {
            byte[] sourceData = WritePatternedRawFixture(rawPath, rows: 3, cols: 4, channels: 3);
            WriteCieFixture(ciePath, rows: 3, cols: 4, channels: 3, srcFileName: Path.GetFileName(rawPath));
            var viewModel = new VExportCIE(ciePath, new MruPathService(new MemoryMruPathStore([])))
            {
                SavePath = root,
                Name = "associated",
                IsExportSrc = true,
                IsExportChannelX = false,
                IsExportChannelY = false,
                IsExportChannelZ = false,
            };

            Assert.True(viewModel.IsCanExportSrc);
            VExportCIE.SaveToTifOrThrow(viewModel);

            using Mat exported = Cv2.ImRead(Path.Combine(root, "associated_Src.tiff"), ImreadModes.Unchanged);
            Assert.Equal(MatType.CV_16U, exported.Depth());
            Assert.Equal(3, exported.Channels());
            Assert.True(exported.IsContinuous());
            byte[] actual = new byte[sourceData.Length];
            Marshal.Copy(exported.Data, actual, 0, actual.Length);
            Assert.Equal(sourceData, actual);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CvRaw16BitJpegExportProducesAnEightBitImageAtTheFixedDefaultQuality()
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-jpeg-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(root, "sample.cvraw");
        Directory.CreateDirectory(root);
        try
        {
            WritePatternedRawFixture(sourcePath, rows: 5, cols: 7, channels: 3);
            var viewModel = new VExportCIE(sourcePath, new MruPathService(new MemoryMruPathStore([])))
            {
                SavePath = root,
                Name = "jpeg",
                ExportImageFormat = ImageFormat.Jpeg,
            };

            Assert.Equal(100, viewModel.JpegQuality);
            VExportCIE.SaveToTifOrThrow(viewModel);

            using Mat exported = Cv2.ImRead(Path.Combine(root, "jpegSrc.jpg"), ImreadModes.Unchanged);
            Assert.Equal(MatType.CV_8U, exported.Depth());
            Assert.Equal(3, exported.Channels());
            Assert.Equal(5, exported.Rows);
            Assert.Equal(7, exported.Cols);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExportWindowAppliesTheFileSpecificFormatAndChannelPolicy()
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-window-{Guid.NewGuid():N}");
        string rawPath = Path.Combine(root, "sample.cvraw");
        string ciePath = Path.Combine(root, "sample.cvcie");
        Directory.CreateDirectory(root);
        try
        {
            WriteRawFixture(rawPath, rows: 2, cols: 3, channels: 3);
            WriteCieFixture(ciePath, rows: 2, cols: 3, channels: 3);

            WpfTestHost.Invoke(() =>
            {
                EnsureExportWindowTestResources();
                ExportCVCIE? rawWindow = null;
                ExportCVCIE? cieWindow = null;
                try
                {
                    var rawViewModel = new VExportCIE(rawPath, new MruPathService(new MemoryMruPathStore([])));
                    rawWindow = new ExportCVCIE(rawViewModel);
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                    Assert.Equal(Visibility.Collapsed, Assert.IsType<GroupBox>(rawWindow.FindName("CvcieChannelGroup")).Visibility);
                    Assert.Equal(3, Assert.IsType<HandyControl.Controls.ComboBox>(rawWindow.FindName("ExportImageFormatComboBox")).Items.Count);
                    var compressionSelector = Assert.IsType<HandyControl.Controls.ComboBox>(rawWindow.FindName("TiffCompressionComboBox"));
                    Assert.Equal(2, compressionSelector.Items.Count);
                    Assert.Equal(Visibility.Visible, compressionSelector.Visibility);
                    rawViewModel.ExportImageFormat = ImageFormat.Png;
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                    Assert.Equal(Visibility.Collapsed, compressionSelector.Visibility);
                    rawViewModel.ExportImageFormat = ImageFormat.Jpeg;
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                    Assert.Equal(Visibility.Collapsed, compressionSelector.Visibility);

                    cieWindow = new ExportCVCIE(new VExportCIE(ciePath, new MruPathService(new MemoryMruPathStore([]))));
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                    Assert.Equal(Visibility.Visible, Assert.IsType<GroupBox>(cieWindow.FindName("CvcieChannelGroup")).Visibility);
                    Assert.Single(Assert.IsType<HandyControl.Controls.ComboBox>(cieWindow.FindName("ExportImageFormatComboBox")).Items);
                }
                finally
                {
                    cieWindow?.Close();
                    rawWindow?.Close();
                }
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static byte[] WriteRawFixture(string filePath, int rows, int cols, int channels)
    {
        byte[] data = new byte[rows * cols * channels * sizeof(ushort)];
        using CVCIEFile file = new()
        {
            Version = 1,
            FileExtType = CVType.Raw,
            Rows = rows,
            Cols = cols,
            Bpp = 16,
            Channels = channels,
            Gain = 1,
            Exp = Enumerable.Repeat(1f, channels).ToArray(),
            Data = data,
        };
        Assert.True(CVFileUtil.WriteCIEFile(filePath, file));
        return data;
    }

    private static byte[] WritePatternedRawFixture(string filePath, int rows, int cols, int channels)
    {
        ushort[] values = new ushort[rows * cols * channels];
        ushort[] pattern = [0, 1, 255, 256, 1024, 32768, 65535];
        for (int i = 0; i < values.Length; i++)
            values[i] = pattern[i % pattern.Length];
        byte[] data = new byte[values.Length * sizeof(ushort)];
        Buffer.BlockCopy(values, 0, data, 0, data.Length);
        WriteCieFile(filePath, CVType.Raw, rows, cols, bpp: 16, channels, data);
        return data;
    }

    private static float[] WriteCieFixture(string filePath, int rows, int cols, int channels, string? srcFileName = null)
    {
        float[] values = Enumerable.Range(0, rows * cols * channels).Select(index => index + 0.25f).ToArray();
        byte[] data = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, data, 0, data.Length);
        WriteCieFile(filePath, CVType.CIE, rows, cols, bpp: 32, channels, data, srcFileName);
        return values;
    }

    private static void WriteCieFile(string filePath, CVType fileType, int rows, int cols, int bpp, int channels, byte[] data, string? srcFileName = null)
    {
        using CVCIEFile file = new()
        {
            Version = 1,
            FileExtType = fileType,
            Rows = rows,
            Cols = cols,
            Bpp = bpp,
            Channels = channels,
            Gain = 1,
            Exp = Enumerable.Repeat(1f, channels).ToArray(),
            SrcFileName = srcFileName,
            Data = data,
        };
        Assert.True(CVFileUtil.WriteCIEFile(filePath, file));
    }

    private static void EnsureExportWindowTestResources()
    {
        Application application = Application.Current!;
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        application.Resources["GlobalTextBrush"] = Brushes.Black;
        application.Resources["GlobalBackground"] = Brushes.White;
        application.Resources["SecondaryRegionBrush"] = Brushes.WhiteSmoke;
        application.Resources["BorderBrush"] = Brushes.Gray;
        application.Resources["SecondaryTextBrush"] = Brushes.DimGray;
        application.Resources["PrimaryBrush"] = Brushes.DodgerBlue;
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    private sealed class MemoryMruPathStore(IEnumerable<MruPathEntry> entries) : IMruPathStore
    {
        private readonly IReadOnlyList<MruPathEntry> _entries = entries.ToList();

        public IReadOnlyList<MruPathEntry> Load() => _entries;

        public void Save(IReadOnlyList<MruPathEntry> entries)
        {
        }
    }
}
