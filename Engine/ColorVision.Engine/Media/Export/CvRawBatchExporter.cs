using ColorVision.FileIO;
using log4net;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Media
{
    public enum CvRawExportStatus { Exported, Skipped, Failed }

    public sealed record CvRawExportFileResult(string SourcePath, string OutputPath, CvRawExportStatus Status, string? Error = null);

    public sealed record CvRawExportProgress(int Total, int Completed, int Exported, int Skipped, int Failed, string CurrentFile);

    public sealed record CvRawExportResult(IReadOnlyList<CvRawExportFileResult> Files, int Total, bool Cancelled)
    {
        public int Exported => Files.Count(file => file.Status == CvRawExportStatus.Exported);
        public int Skipped => Files.Count(file => file.Status == CvRawExportStatus.Skipped);
        public int Failed => Files.Count(file => file.Status == CvRawExportStatus.Failed);
    }

    public static class CvRawBatchExporter
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CvRawBatchExporter));
        public static int DefaultParallelism => Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

        // Run on a worker thread: enumeration and native encoding both perform blocking I/O.
        public static CvRawExportResult ExportFolder(string folder, int parallelism,
            Action<CvRawExportProgress>? reportProgress = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folder);
            ArgumentOutOfRangeException.ThrowIfLessThan(parallelism, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(parallelism, 32);
            string root = Path.GetFullPath(folder);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"文件夹不存在：{root}");

            List<string> files = new();
            try
            {
                foreach (string path in Directory.EnumerateFiles(root, "*.cvraw", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    MatchCasing = MatchCasing.CaseInsensitive,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    IgnoreInaccessible = false
                }))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    files.Add(path);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new CvRawExportResult([], files.Count, true);
            }

            List<CvRawExportFileResult> results = new(files.Count);
            object progressLock = new();
            int exported = 0, skipped = 0, failed = 0;
            reportProgress?.Invoke(new(files.Count, 0, 0, 0, 0, string.Empty));
            try
            {
                Parallel.ForEach(files, new ParallelOptions
                {
                    MaxDegreeOfParallelism = parallelism,
                    CancellationToken = cancellationToken
                }, sourcePath =>
                {
                    CvRawExportFileResult result = ExportFile(sourcePath, cancellationToken);
                    lock (progressLock)
                    {
                        results.Add(result);
                        if (result.Status == CvRawExportStatus.Exported) exported++;
                        else if (result.Status == CvRawExportStatus.Skipped) skipped++;
                        else failed++;
                        reportProgress?.Invoke(new(files.Count, results.Count, exported, skipped, failed, sourcePath));
                    }
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }

            return new CvRawExportResult(results.ToArray(), files.Count, cancellationToken.IsCancellationRequested);
        }

        private static CvRawExportFileResult ExportFile(string sourcePath, CancellationToken cancellationToken)
        {
            string outputPath = Path.ChangeExtension(sourcePath, ".tif");
            string? temporaryPath = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(outputPath)) return new(sourcePath, outputPath, CvRawExportStatus.Skipped);

                bool loaded = CVFileUtil.ReadCVRaw(sourcePath, out CVCIEFile raw);
                using (raw)
                {
                    raw.FileExtType = CVType.Raw; // Header inference predates case-insensitive extensions.
                    if (!loaded)
                        throw new InvalidDataException("无法读取 CVRAW 文件头或图像数据。");
                    cancellationToken.ThrowIfCancellationRequested();

                    // Preserve source samples; ToMat() is a display conversion for floating-point RAW.
                    using Mat image = VExportCIE.CreateMatFromCVCIEFile(raw);
                    temporaryPath = Path.Combine(Path.GetDirectoryName(outputPath)!, $".__cvraw_export_{Guid.NewGuid():N}.tif");
                    if (!Cv2.ImWrite(temporaryPath, image, [new ImageEncodingParam(ImwriteFlags.TiffCompression, VExportCIE.DefaultTiffCompression)]))
                        throw new IOException("TIFF 图像写入失败。");
                    TiffImageDescriptionWriter.Write(temporaryPath, VExportCIE.CreateTiffParameters(raw, "Src").ToJson());
                }

                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    // Publish only a complete TIFF; a concurrent exporter must never replace an existing file.
                    File.Move(temporaryPath, outputPath, overwrite: false);
                }
                catch (IOException) when (File.Exists(outputPath))
                {
                    return new(sourcePath, outputPath, CvRawExportStatus.Skipped);
                }
                return new(sourcePath, outputPath, CvRawExportStatus.Exported);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                Log.Warn($"CVRAW batch export failed: {sourcePath}", ex);
                return new(sourcePath, outputPath, CvRawExportStatus.Failed, ex.Message);
            }
            finally
            {
                if (temporaryPath != null)
                {
                    try { File.Delete(temporaryPath); }
                    catch (Exception ex) { Log.Warn($"Unable to remove temporary TIFF: {temporaryPath}", ex); }
                }
            }
        }
    }
}
