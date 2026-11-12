using ColorVision.Engine.Media;
using ColorVision.FileIO;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Tif;
using ColorVision.Solution.Mru;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;
using OpenCvSharp;
using Newtonsoft.Json.Linq;
using System.Collections.Specialized;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class ExportCieTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CorruptOptionalRawTailStillExportsOriginalPixels(bool batch)
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-corrupt-tail-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "source.cvraw");
        bool previousCache = CVFileReadCache.IsEnabled;
        try
        {
            CVFileReadCache.IsEnabled = false;
            byte[] expected = WritePatternedRawFixture(path, rows: 2, cols: 3, channels: 1);
            new ColorCalibrationSnapshot
            {
                Width = 3, Height = 2, RawBpp = 16, Channels = 1, TransformKind = 2,
                Coefficients = [2, 0, 0, 0, 0, 0, 0, 0, 0], Exposure = [1, 1, 1]
            }.Save(path, canReplay: true);
            using (FileStream file = File.Open(path, FileMode.Open, FileAccess.ReadWrite))
            {
                file.Position = file.Length - 1;
                int checksum = file.ReadByte();
                file.Position--;
                file.WriteByte((byte)(checksum ^ 1));
            }
            Assert.Throws<InvalidDataException>(() => CVFileMetadata.Read(path));

            string output;
            if (batch)
            {
                Assert.Equal(1, CvRawBatchExporter.ExportFolder(root, 1).Exported);
                output = Path.Combine(root, "source.tif");
            }
            else
            {
                var export = new VExportCIE(path, new MruPathService(new MemoryMruPathStore([]))) { SavePath = root, Name = "export" };
                Assert.True(export.IsCanExportSrc);
                Assert.False(export.HasCieChannels);
                VExportCIE.SaveToTifOrThrow(export);
                output = Path.Combine(root, "export.tiff");
            }

            Assert.Null(ReadTiffParameters(output).Calibration);
            using Mat image = Cv2.ImRead(output, ImreadModes.Unchanged);
            Assert.Equal(MatType.CV_16UC1, image.Type());
            byte[] actual = new byte[expected.Length];
            Marshal.Copy(image.Data, actual, 0, actual.Length);
            Assert.Equal(expected, actual);
        }
        finally
        {
            CVFileReadCache.IsEnabled = previousCache;
            foreach (string file in Directory.EnumerateFiles(root)) File.Delete(file);
            Directory.Delete(root);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CalibratedRawExportKeepsOneSnapshotWhenAcquisitionChangesBetweenOutputs(bool cacheEnabled)
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-snapshot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "source.cvraw");
        bool previousCache = CVFileReadCache.IsEnabled;
        var logger = (Logger)LogManager.GetLogger(typeof(CameraFileMetadata).Assembly, typeof(CameraFileMetadata)).Logger;
        Hierarchy hierarchy = Assert.IsType<Hierarchy>(logger.Repository);
        bool previousConfigured = hierarchy.Configured;
        Level previousLevel = logger.Level;
        Exception? updateFailure = null;
        int updates = 0;
        var appender = new AcquisitionWarningAppender(() =>
        {
            if (++updates != 1) return;
            try
            {
                CameraFileMetadata.Save(path, CameraFileMetadata.AcquisitionKind, new ImageAcquisitionRecord { NdPort = 17 });
            }
            catch (Exception error) { updateFailure = error; }
        });
        try
        {
            CVFileReadCache.IsEnabled = cacheEnabled;
            byte[] raw = WritePatternedRawFixture(path, rows: 2, cols: 3, channels: 1);
            new ColorCalibrationSnapshot
            {
                Width = 3, Height = 2, RawBpp = 16, Channels = 1, TransformKind = 2,
                Coefficients = [2, 0, 0, 0, 0, 0, 0, 0, 0], Exposure = [1, 1, 1], Template = "original"
            }.Save(path, canReplay: true);
            CVFileMetadata.SetProperty(path, CameraFileMetadata.AcquisitionKind, 1, [(byte)'{']);
            var export = new VExportCIE(path, new MruPathService(new MemoryMruPathStore([]))) { SavePath = root, Name = "export" };
            hierarchy.Configured = true;
            logger.Level = Level.Warn;
            logger.AddAppender(appender);

            VExportCIE.SaveToTifOrThrow(export);

            Assert.Null(updateFailure);
            Assert.True(updates > 0);
            Assert.Equal(17, CameraFileMetadata.Read(path).Acquisition?.NdPort);
            Assert.Null(ReadTiffParameters(Path.Combine(root, "export_Src.tiff")).Acquisition);
            string channel = Path.Combine(root, "export_Y.tiff");
            Assert.Null(ReadTiffParameters(channel).Acquisition);
            using Mat image = Cv2.ImRead(channel, ImreadModes.Unchanged);
            float[] actual = new float[6];
            Marshal.Copy(image.Data, actual, 0, actual.Length);
            Assert.Equal(Enumerable.Range(0, 6).Select(pixel => 2f * BitConverter.ToUInt16(raw, pixel * sizeof(ushort))), actual);
        }
        finally
        {
            logger.RemoveAppender(appender);
            appender.Close();
            logger.Level = previousLevel;
            hierarchy.Configured = previousConfigured;
            CVFileReadCache.IsEnabled = previousCache;
            foreach (string file in Directory.EnumerateFiles(root)) File.Delete(file);
            Directory.Delete(root);
        }
    }

    private sealed class AcquisitionWarningAppender(Action update) : AppenderSkeleton
    {
        protected override void Append(LoggingEvent loggingEvent)
        {
            if (loggingEvent.RenderedMessage == $"Invalid optional image record: {CameraFileMetadata.AcquisitionKind}") update();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JpegExportRejectsMeasurementPlanesBeforeWritingAndCanRetryWithOriginalOnly(bool cieFile)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"colorvision-export-jpeg-measurement-{Guid.NewGuid():N}"));
        string rawPath = Path.Combine(root, "source.cvraw");
        string inputPath = cieFile ? Path.Combine(root, "source.cvcie") : rawPath;
        string output = Path.Combine(root, "output");
        Directory.CreateDirectory(output);
        try
        {
            WritePatternedRawFixture(rawPath, rows: 2, cols: 3, channels: 3);
            if (cieFile)
                WriteCieFixture(inputPath, rows: 2, cols: 3, channels: 1, srcFileName: Path.GetFileName(rawPath));
            else
            {
                new ColorCalibrationSnapshot
                {
                    Width = 3, Height = 2, RawBpp = 16, Channels = 3, TransformKind = 0,
                    Coefficients = [2, 0, 0, 0, -3, 0, 0, 0, 4], Exposure = [1, 1, 1], Template = "export",
                }.Save(rawPath, canReplay: true);
            }

            string sourceTarget = Path.Combine(output, "result_Src.jpg");
            string measurementTarget = Path.Combine(output, "result_Y.jpg");
            byte[] originalSource = [1, 2, 3], originalMeasurement = [4, 5, 6];
            File.WriteAllBytes(sourceTarget, originalSource);
            File.WriteAllBytes(measurementTarget, originalMeasurement);
            var export = new VExportCIE(inputPath, new MruPathService(new MemoryMruPathStore([])))
            {
                SavePath = output, Name = "result", ExportImageFormat = ImageFormat.Jpeg,
            };
            Assert.True(export.IsExportSrc);
            Assert.True(export.IsExportChannelY);
            var error = Assert.Throws<NotSupportedException>(() => VExportCIE.SaveToTifOrThrow(export));
            Assert.Contains("TIFF", error.Message);
            Assert.Equal(2, Directory.GetFiles(output).Length);
            Assert.Equal(originalSource, File.ReadAllBytes(sourceTarget));
            Assert.Equal(originalMeasurement, File.ReadAllBytes(measurementTarget));

            export.IsExportChannelY = false;
            if (!cieFile) export.IsExportChannelX = export.IsExportChannelZ = false;
            // X and Z are absent from a one-channel CVCIE even when their hidden flags are set.
            VExportCIE.SaveToTifOrThrow(export);
            using Mat originalOnly = Cv2.ImRead(Path.Combine(output, "result.jpg"), ImreadModes.Unchanged);
            Assert.Equal((2, 3, MatType.CV_8UC3), (originalOnly.Rows, originalOnly.Cols, originalOnly.Type()));
            Assert.Equal(3, Directory.GetFiles(output).Length);
        }
        finally
        {
            Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar,
                root, StringComparison.OrdinalIgnoreCase);
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(3, true, true)]
    [InlineData(3, false, true)]
    [InlineData(1, true, true)]
    [InlineData(1, false, true)]
    [InlineData(3, true, false)]
    [InlineData(3, false, true, true)]
    public void CalibratedRawExportsFloatMeasurementChannelsWithoutCreatingCvcie(int channels, bool includeSource, bool canReplay, bool memoryOnly = false)
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-calibrated-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(root, "sample.cvraw");
        string outputPath = Path.Combine(root, "output");
        Directory.CreateDirectory(root);
        bool previousCacheEnabled = CVFileReadCache.IsEnabled;
        try
        {
            if (memoryOnly) CVFileReadCache.IsEnabled = true;
            byte[] raw = WritePatternedRawFixture(sourcePath, rows: 2, cols: 3, channels, memoryOnly ? CVFileSaveMode.MemoryOnly : CVFileSaveMode.Synchronous);
            new ColorCalibrationSnapshot
            {
                Width = 3, Height = 2, RawBpp = 16, Channels = channels,
                TransformKind = channels == 1 ? 2 : 0, Coefficients = [2, 0, 0, 0, -3, 0, 0, 0, 4],
                Exposure = [1, 1, 1], Template = "export"
            }.Save(sourcePath, canReplay);
            byte[]? original = memoryOnly ? null : File.ReadAllBytes(sourcePath);
            var export = new VExportCIE(sourcePath, new MruPathService(new MemoryMruPathStore([])))
            {
                SavePath = outputPath, Name = "measurement.v1", IsExportSrc = includeSource
            };
            Assert.True(export.IsCVRaw);
            Assert.False(export.IsCVCIE);
            Assert.Equal(canReplay, export.HasCieChannels);
            Assert.Equal(canReplay && channels == 3, export.IsCieThreeChannel);
            Assert.Equal(3, export.AvailableImageFormats.Count);
            VExportCIE.SaveToTifOrThrow(export);

            int count = (includeSource ? 1 : 0) + (canReplay ? channels : 0);
            Assert.Equal(count, Directory.GetFiles(outputPath).Length);
            if (includeSource)
            {
                string path = Path.Combine(outputPath, "measurement.v1" + (count > 1 ? "_Src" : "") + ".tiff");
                using Mat source = Cv2.ImRead(path, ImreadModes.Unchanged);
                Assert.Equal(MatType.CV_16U, source.Depth());
                byte[] actual = new byte[raw.Length];
                Marshal.Copy(source.Data, actual, 0, actual.Length);
                Assert.Equal(raw, actual);
                ColorVisionTiffParameters sourceParameters = ReadTiffParameters(path);
                Assert.Equal("calibration-input", sourceParameters.PixelRole);
                Assert.NotNull(sourceParameters.Calibration);
                Assert.Equal(canReplay, sourceParameters.Calibration["CanReplay"]!.Value<bool>());
                AssertPixelBinding(path, sourceParameters);
            }
            for (int channel = 0; canReplay && channel < channels; channel++)
            {
                string name = channels == 1 ? "Y" : new[] { "X", "Y", "Z" }[channel];
                string path = Path.Combine(outputPath, "measurement.v1" + (count > 1 ? "_" + name : "") + ".tiff");
                using Mat image = Cv2.ImRead(path, ImreadModes.Unchanged);
                Assert.Equal(MatType.CV_32FC1, image.Type());
                Assert.Equal(2, image.Rows);
                Assert.Equal(3, image.Cols);
                float[] actual = new float[6];
                Marshal.Copy(image.Data, actual, 0, actual.Length);
                float scale = channels == 1 ? 2 : new[] { 2f, -3f, 4f }[channel];
                float[] expected = Enumerable.Range(0, 6).Select(pixel =>
                    scale * BitConverter.ToUInt16(raw, (pixel * channels + (channels == 1 ? 0 : 2 - channel)) * sizeof(ushort))).ToArray();
                Assert.Equal(expected, actual);
                ColorVisionTiffParameters parameters = ReadTiffParameters(path);
                Assert.Equal("CVRAW", parameters.SourceType);
                Assert.Equal(name, parameters.ExportedChannel);
                Assert.Equal(32, parameters.Bpp);
                Assert.Equal("xyz-channel", parameters.PixelRole);
                Assert.Equal(name, parameters.MeasurementChannel);
                AssertPixelBinding(path, parameters);
            }
            if (memoryOnly) Assert.False(File.Exists(sourcePath));
            else Assert.Equal(original, File.ReadAllBytes(sourcePath));
            Assert.Empty(Directory.EnumerateFiles(root, "*.cvcie", SearchOption.AllDirectories));
        }
        finally
        {
            CVFileReadCache.IsEnabled = previousCacheEnabled;
            Assert.StartsWith(Path.GetTempPath(), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase);
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("png", 1)]
    [InlineData("png", 3)]
    [InlineData("tiff", 3)]
    [InlineData("png", 3, true)]
    [InlineData("tiff", 3, true)]
    public void SourceSnapshotRoundTripRetainsBindingWithoutOriginalRaw(string format, int channels, bool planar = false)
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-replay-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string sourcePath = Path.Combine(root, "source.cvraw");
            byte[] raw = WritePatternedRawFixture(sourcePath, 2, 3, channels);
            new ColorCalibrationSnapshot
            {
                Width = 3, Height = 2, RawBpp = 16, Channels = channels,
                TransformKind = channels == 1 ? 2 : 0,
                Coefficients = [2, 0, 0, 0, -3, 0, 0, 0, 4],
                Exposure = [2, 3, 4], Template = "导出后无需模板-颜色🌈", InterleavedBgr = !planar
            }.Save(sourcePath, canReplay: true);
            var export = new VExportCIE(sourcePath, new MruPathService(new MemoryMruPathStore([])))
            {
                SavePath = root, Name = "roundtrip", IsExportSrc = true,
                IsExportChannelX = false, IsExportChannelY = false, IsExportChannelZ = false,
                ExportImageFormat = format == "png" ? ImageFormat.Png : ImageFormat.Tiff
            };
            VExportCIE.SaveToTifOrThrow(export);
            string path = Path.Combine(root, "roundtrip." + format);
            WpfTestHost.Invoke(() =>
            {
                BitmapFrame frame = ReadFrame(path);
                Assert.True(ColorVisionTiffParameters.TryRead(frame.Metadata as BitmapMetadata, out var parameters));
                Assert.NotNull(parameters);
                Assert.Equal("calibration-input", parameters.PixelRole);
                Assert.Equal(ColorVisionPixelBinding.ComputeSha256(frame), parameters.PixelSha256);
                Assert.Equal(new float[] { 2, 3, 4 }, parameters.Exposure);
                Assert.Equal("导出后无需模板-颜色🌈", parameters.Calibration!["Template"]!.Value<string>());
                File.Delete(sourcePath);
                string secondPath = Path.Combine(root, "independent." + format);
                CalibratedImageExport.Save(frame, parameters, secondPath);
                using Mat image = Cv2.ImRead(secondPath, ImreadModes.Unchanged);
                byte[] bytes = new byte[raw.Length];
                Marshal.Copy(image.Data, bytes, 0, bytes.Length);
                byte[] expected = raw;
                if (planar)
                {
                    expected = new byte[raw.Length];
                    for (int pixel = 0; pixel < 6; pixel++)
                        for (int channel = 0; channel < 3; channel++)
                            Buffer.BlockCopy(raw, ((2 - channel) * 6 + pixel) * 2, expected, (pixel * 3 + channel) * 2, 2);
                }
                Assert.Equal(expected, bytes);
                BitmapFrame reopened = ReadFrame(secondPath);
                Assert.True(ColorVisionTiffParameters.TryRead(reopened.Metadata as BitmapMetadata, out var copied));
                Assert.True(JToken.DeepEquals(parameters.Calibration, copied!.Calibration));
                Assert.Equal("calibration-input", copied.PixelRole);
                Assert.Equal(parameters.PixelSha256, copied.PixelSha256);

                // An edit with identical dimensions cannot acquire replay rights from the old metadata.
                byte[] changed = new byte[checked(frame.PixelWidth * frame.PixelHeight * frame.Format.BitsPerPixel / 8)];
                BitmapSource edit = BitmapSource.Create(frame.PixelWidth, frame.PixelHeight, 96, 96, frame.Format, null,
                    changed, checked(frame.PixelWidth * frame.Format.BitsPerPixel / 8));
                string editedPath = Path.Combine(root, "edited." + format);
                CalibratedImageExport.Save(edit, parameters, editedPath);
                BitmapFrame editedFrame = ReadFrame(editedPath);
                Assert.True(ColorVisionTiffParameters.TryRead(editedFrame.Metadata as BitmapMetadata, out var editedParameters));
                Assert.Equal("display", editedParameters!.PixelRole);
                Assert.NotNull(editedParameters.Calibration);
            });
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void BindsRgb48AcrossBlocksWithoutHashingPaddingOrChangingSource()
    {
        const int width = 1025;
        const int height = 173;
        const int rowBytes = width * 6;
        using Mat owner = new(height + 2, width + 2, MatType.CV_16UC3, Scalar.All(54321));
        using Mat image = new(owner, new OpenCvSharp.Rect(1, 1, width, height));
        Assert.False(image.IsContinuous());
        byte[] bgr = new byte[rowBytes * height];
        new Random(71).NextBytes(bgr);
        for (int y = 0; y < height; y++) Marshal.Copy(bgr, y * rowBytes, image.Ptr(y), rowBytes);
        using Mat original = owner.Clone();
        byte[] rgb = bgr.ToArray();
        for (int pixel = 0; pixel < rgb.Length; pixel += 6)
        {
            (rgb[pixel], rgb[pixel + 4]) = (rgb[pixel + 4], rgb[pixel]);
            (rgb[pixel + 1], rgb[pixel + 5]) = (rgb[pixel + 5], rgb[pixel + 1]);
        }

        ColorVisionTiffParameters parameters = new();
        CalibratedImageExport.BindPixels(parameters, image);

        Assert.Equal(Convert.ToHexString(SHA256.HashData(rgb)), parameters.PixelSha256);
        Assert.Equal(3, parameters.PixelChannels);
        Assert.Equal("Rgb48", parameters.PixelLayout);
        Assert.Equal(0d, Cv2.Norm(original, owner, NormTypes.INF));
    }

    [Fact]
    public void FloatPngExportRejectsQuantizationAndPreservesExistingDestination()
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-float-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "existing.png");
            byte[] original = [1, 2, 3, 4];
            File.WriteAllBytes(path, original);
            WpfTestHost.Invoke(() =>
            {
                BitmapSource pixels = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Gray32Float, null, new float[] { -4.5f, 3.25f }, 8);
                Assert.Throws<NotSupportedException>(() => CalibratedImageExport.Save(pixels, null, path));
            });
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Single(Directory.GetFiles(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static BitmapFrame ReadFrame(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
    }

    private static void AssertPixelBinding(string path, ColorVisionTiffParameters parameters)
        => WpfTestHost.Invoke(() => Assert.Equal(ColorVisionPixelBinding.ComputeSha256(ReadFrame(path)), parameters.PixelSha256));

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

            string exportedPath = Path.Combine(outputPath, "compressed.tiff");
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
    public void TiffExportEmbedsAndDisplaysColorVisionParameters()
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-metadata-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(root, "sample.cvraw");
        Directory.CreateDirectory(root);
        try
        {
            WriteRawFixture(sourcePath, rows: 2, cols: 3, channels: 3);
            var viewModel = new VExportCIE(sourcePath, new MruPathService(new MemoryMruPathStore([])))
            {
                SavePath = root,
                Name = "metadata",
                ExportImageFormat = ImageFormat.Tiff,
            };

            VExportCIE.SaveToTifOrThrow(viewModel);

            ColorVisionTiffParameters parameters = ReadTiffParameters(Path.Combine(root, "metadata.tiff"));
            Assert.Equal(ColorVisionTiffParameters.CurrentSchema, parameters.Schema);
            Assert.Equal("CVRAW", parameters.SourceType);
            Assert.Equal("Src", parameters.ExportedChannel);
            Assert.Equal("sample.cvraw", parameters.InputFileName);
            Assert.Null(parameters.AssociatedSourceFileName);
            Assert.Equal((uint)1, parameters.FileVersion);
            Assert.Equal(2, parameters.Rows);
            Assert.Equal(3, parameters.Cols);
            Assert.Equal(16, parameters.Bpp);
            Assert.Equal(3, parameters.SourceChannels);
            Assert.Equal(1, parameters.Gain);
            Assert.Equal([1f, 1f, 1f], parameters.Exposure);

            ImageViewConfig config = new();
            Opentif.ApplyColorVisionMetadata(config, parameters);
            Assert.Equal("CVRAW", config.GetProperties<string>(ImageViewPropertyKeys.ColorVisionSourceType));
            Assert.Equal("Src", config.GetProperties<string>(ImageViewPropertyKeys.ColorVisionExportedChannel));
            Assert.Equal(1f, config.GetProperties<float>(ImageViewPropertyKeys.ColorVisionGain));
            Assert.Equal([1f, 1f, 1f], config.GetProperties<float[]>(ImageViewPropertyKeys.ColorVisionExposure));
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

            string exportedPath = Path.Combine(root, "preserved" + extension);
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

    [Theory]
    [InlineData(32)]
    [InlineData(64)]
    public void CvcieFloatExportWritesOnlyTheSelectedChannel(int bpp)
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-cvcie-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(root, "sample.cvcie");
        Directory.CreateDirectory(root);
        try
        {
            const int rows = 3;
            const int cols = 4;
            float[] sourceValues = WriteCieFixture(sourcePath, rows, cols, channels: 3, bpp: bpp);
            var viewModel = new VExportCIE(sourcePath, new MruPathService(new MemoryMruPathStore([])))
            {
                SavePath = root,
                Name = "channels.v1",
                ExportImageFormat = ImageFormat.Tiff,
                IsExportSrc = false,
                IsExportChannelX = false,
                IsExportChannelY = true,
                IsExportChannelZ = false,
            };

            VExportCIE.SaveToTifOrThrow(viewModel);

            string yPath = Path.Combine(root, "channels.v1.tiff");
            Assert.True(File.Exists(yPath));
            Assert.Single(Directory.EnumerateFiles(root, "*.tiff"));
            using Mat exported = Cv2.ImRead(yPath, ImreadModes.Unchanged);
            Assert.Equal(bpp == 32 ? MatType.CV_32F : MatType.CV_64F, exported.Depth());
            Assert.Equal(1, exported.Channels());
            Assert.True(exported.IsContinuous());
            double[] actual = new double[rows * cols];
            if (bpp == 32)
            {
                float[] values = new float[actual.Length];
                Marshal.Copy(exported.Data, values, 0, values.Length);
                actual = values.Select(value => (double)value).ToArray();
            }
            else Marshal.Copy(exported.Data, actual, 0, actual.Length);
            Assert.Equal(sourceValues.Skip(rows * cols).Take(rows * cols).Select(value => (double)value), actual);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CvcieExportDoesNotAllocateAManagedMeasurementPlane()
    {
        string root = Directory.CreateTempSubdirectory("colorvision-export-allocation-").FullName;
        string sourcePath = Path.Combine(root, "sample.cvcie");
        string outputPath = Path.Combine(root, "measurement.tiff");
        try
        {
            const int rows = 512;
            const int cols = 512;
            const int planeBytes = rows * cols * sizeof(float);
            WriteCieFixture(sourcePath, rows, cols, channels: 1);
            var export = new VExportCIE(sourcePath, new MruPathService(new MemoryMruPathStore([])))
            {
                SavePath = root, Name = "measurement", IsExportSrc = false,
            };

            VExportCIE.SaveToTifOrThrow(export);
            long before = GC.GetAllocatedBytesForCurrentThread();
            VExportCIE.SaveToTifOrThrow(export);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(allocated < planeBytes, $"Export allocated {allocated:N0} managed bytes for a {planeBytes:N0}-byte plane.");
        }
        finally
        {
            File.Delete(outputPath);
            File.Delete(sourcePath);
            Directory.Delete(root);
        }
    }

    // Preserve both selected-plane bounds and the existing complete-payload metadata validation.
    [Theory]
    [InlineData(32, 1)]
    [InlineData(64, 1)]
    [InlineData(32, 2)]
    [InlineData(64, 2)]
    public void CvcieExportRejectsIncompletePayload(int bpp, int selectedChannel)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"colorvision-export-plane-{Guid.NewGuid():N}"));
        string sourcePath = Path.Combine(root, "partial.cvcie");
        Directory.CreateDirectory(root);
        try
        {
            WriteCieFixture(sourcePath, rows: 2, cols: 3, channels: 3, bpp: bpp);
            int offset = CVFileUtil.ReadCIEFileHeader(sourcePath, out CVCIEFile header);
            using (header)
            using (FileStream file = File.OpenWrite(sourcePath))
            using (BinaryWriter writer = new(file))
            {
                int availableBytes = 2 * 3 * (bpp / 8) * 2;
                file.Position = offset;
                writer.Write(availableBytes);
                file.SetLength(offset + sizeof(int) + availableBytes);
            }
            var export = new VExportCIE(sourcePath, new MruPathService(new MemoryMruPathStore([])))
            {
                SavePath = root, Name = "selected", IsExportSrc = false,
                IsExportChannelX = false, IsExportChannelY = selectedChannel == 1, IsExportChannelZ = selectedChannel == 2,
            };
            byte[] original = File.ReadAllBytes(sourcePath);
            Assert.Throws<InvalidDataException>(() => VExportCIE.SaveToTifOrThrow(export));
            Assert.Empty(Directory.EnumerateFiles(root, "*.tiff"));
            Assert.Equal(original, File.ReadAllBytes(sourcePath));
        }
        finally
        {
            Assert.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar, root, StringComparison.OrdinalIgnoreCase);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CvcieExportPreservesEverySuffixWhenTheNameContainsDots()
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-cvcie-suffix-{Guid.NewGuid():N}");
        string rawPath = Path.Combine(root, "source.cvraw");
        string ciePath = Path.Combine(root, "sample.cvcie");
        Directory.CreateDirectory(root);
        try
        {
            WritePatternedRawFixture(rawPath, rows: 2, cols: 3, channels: 3);
            float[] xyz = WriteCieFixture(ciePath, rows: 2, cols: 3, channels: 3, srcFileName: Path.GetFileName(rawPath));
            var viewModel = new VExportCIE(ciePath, new MruPathService(new MemoryMruPathStore([])))
            {
                SavePath = root,
                Name = "measurement.v1",
                IsExportSrc = true,
                IsExportChannelX = true,
                IsExportChannelY = true,
                IsExportChannelZ = true,
            };

            VExportCIE.SaveToTifOrThrow(viewModel);

            string[] expectedFiles =
            [
                "measurement.v1_Src.tiff",
                "measurement.v1_X.tiff",
                "measurement.v1_Y.tiff",
                "measurement.v1_Z.tiff",
            ];
            Assert.All(expectedFiles, fileName => Assert.True(File.Exists(Path.Combine(root, fileName)), fileName));
            Assert.Equal(expectedFiles.Order(), Directory.EnumerateFiles(root, "*.tiff").Select(Path.GetFileName).Order());

            ColorVisionTiffParameters sourceParameters = ReadTiffParameters(Path.Combine(root, "measurement.v1_Src.tiff"));
            Assert.Equal("CVRAW", sourceParameters.SourceType);
            Assert.Equal("Src", sourceParameters.ExportedChannel);
            Assert.Equal("source.cvraw", sourceParameters.InputFileName);

            for (int channel = 0; channel < 3; channel++)
            {
                string channelName = "XYZ"[channel].ToString();
                string path = Path.Combine(root, $"measurement.v1_{channelName}.tiff");
                ColorVisionTiffParameters parameters = ReadTiffParameters(path);
                Assert.Equal("CVCIE", parameters.SourceType);
                Assert.Equal(channelName, parameters.ExportedChannel);
                Assert.Equal("sample.cvcie", parameters.InputFileName);
                Assert.Equal("source.cvraw", parameters.AssociatedSourceFileName);
                Assert.Equal(3, parameters.SourceChannels);

                using Mat plane = Cv2.ImRead(path, ImreadModes.Unchanged);
                Assert.Equal(MatType.CV_32FC1, plane.Type());
                float[] actual = new float[6];
                Marshal.Copy(plane.Data, actual, 0, actual.Length);
                Assert.Equal(xyz.Skip(channel * actual.Length).Take(actual.Length), actual);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CvcieResolvesARelativeAssociatedSourceBeforeOfferingAndExportingIt(bool missingCiePayload)
    {
        string root = Path.Combine(Path.GetTempPath(), $"colorvision-export-associated-{Guid.NewGuid():N}");
        string rawPath = Path.Combine(root, "renamed-source.cvraw");
        string ciePath = Path.Combine(root, "sample.cvcie");
        Directory.CreateDirectory(root);
        try
        {
            byte[] sourceData = WritePatternedRawFixture(rawPath, rows: 3, cols: 4, channels: 3);
            WriteCieFixture(ciePath, rows: 3, cols: 4, channels: 3, srcFileName: Path.GetFileName(rawPath));
            if (missingCiePayload)
            {
                int offset = CVFileUtil.ReadCIEFileHeader(ciePath, out CVCIEFile header);
                using (header)
                using (FileStream file = File.OpenWrite(ciePath)) file.SetLength(offset);
            }
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

            using Mat exported = Cv2.ImRead(Path.Combine(root, "associated.tiff"), ImreadModes.Unchanged);
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

            using Mat exported = Cv2.ImRead(Path.Combine(root, "jpeg.jpg"), ImreadModes.Unchanged);
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

    private static byte[] WritePatternedRawFixture(string filePath, int rows, int cols, int channels, CVFileSaveMode saveMode = CVFileSaveMode.Synchronous)
    {
        ushort[] values = new ushort[rows * cols * channels];
        ushort[] pattern = [0, 1, 255, 256, 1024, 32768, 65535];
        for (int i = 0; i < values.Length; i++)
            values[i] = pattern[i % pattern.Length];
        byte[] data = new byte[values.Length * sizeof(ushort)];
        Buffer.BlockCopy(values, 0, data, 0, data.Length);
        WriteCieFile(filePath, CVType.Raw, rows, cols, bpp: 16, channels, data, saveMode: saveMode);
        return data;
    }

    private static float[] WriteCieFixture(string filePath, int rows, int cols, int channels, string? srcFileName = null, int bpp = 32)
    {
        float[] values = Enumerable.Range(0, rows * cols * channels).Select(index => index + 0.25f).ToArray();
        byte[] data = new byte[values.Length * (bpp / 8)];
        if (bpp == 32) Buffer.BlockCopy(values, 0, data, 0, data.Length);
        else Buffer.BlockCopy(values.Select(value => (double)value).ToArray(), 0, data, 0, data.Length);
        WriteCieFile(filePath, CVType.CIE, rows, cols, bpp, channels, data, srcFileName);
        return values;
    }

    private static void WriteCieFile(string filePath, CVType fileType, int rows, int cols, int bpp, int channels, byte[] data, string? srcFileName = null, CVFileSaveMode saveMode = CVFileSaveMode.Synchronous)
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
        Assert.True(CVFileUtil.WriteCIEFile(filePath, file, saveMode));
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

    private static ColorVisionTiffParameters ReadTiffParameters(string filePath)
    {
        return WpfTestHost.Invoke(() =>
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            TiffBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapMetadata metadata = Assert.IsType<BitmapMetadata>(Assert.Single(decoder.Frames).Metadata);
            Assert.True(ColorVisionTiffParameters.TryRead(metadata, out ColorVisionTiffParameters? parameters));
            return Assert.IsType<ColorVisionTiffParameters>(parameters);
        });
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
