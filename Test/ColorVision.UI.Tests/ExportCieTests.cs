using ColorVision.Engine.Media;
using ColorVision.FileIO;
using ColorVision.Solution.Mru;
using OpenCvSharp;
using System.Collections.Specialized;
using System.Drawing.Imaging;
using System.IO;

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

    private sealed class MemoryMruPathStore(IEnumerable<MruPathEntry> entries) : IMruPathStore
    {
        private readonly IReadOnlyList<MruPathEntry> _entries = entries.ToList();

        public IReadOnlyList<MruPathEntry> Load() => _entries;

        public void Save(IReadOnlyList<MruPathEntry> entries)
        {
        }
    }
}
