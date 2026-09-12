using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using SharpSevenZip;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    internal sealed record CalibrationExportFile(string SourcePath, string EntryPath);

    internal sealed record CalibrationExportText(string EntryPath, string Content);

    internal sealed record CalibrationExportPlan(
        IReadOnlyList<CalibrationExportFile> Files,
        IReadOnlyList<CalibrationExportText> TextEntries)
    {
        public int EntryCount => Files.Count + TextEntries.Count;
    }

    internal sealed record CalibrationExportProgress(int Percent, string EntryPath);

    internal static class CalibrationExportArchive
    {
        public static void CreateOrReplace(
            string destinationPath,
            CalibrationExportPlan plan,
            IProgress<CalibrationExportProgress>? progress = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
            ArgumentNullException.ThrowIfNull(plan);

            string fullDestinationPath = Path.GetFullPath(destinationPath);
            string destinationDirectory = Path.GetDirectoryName(fullDestinationPath)
                ?? throw new InvalidOperationException("无法确定导出目录。");
            string temporaryArchivePath = Path.Combine(
                destinationDirectory,
                $".{Path.GetFileName(fullDestinationPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                List<Stream> ownedStreams = new(plan.EntryCount);
                try
                {
                    Dictionary<string, StreamWithAttributes> entries = new(StringComparer.OrdinalIgnoreCase);
                    foreach (CalibrationExportFile file in plan.Files)
                    {
                        FileStream stream = new(
                            file.SourcePath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            1024 * 1024,
                            FileOptions.SequentialScan);
                        ownedStreams.Add(stream);
                        entries.Add(NormalizeEntryPath(file.EntryPath), new StreamWithAttributes(stream));
                    }

                    foreach (CalibrationExportText textEntry in plan.TextEntries)
                    {
                        MemoryStream stream = new(Encoding.UTF8.GetBytes(textEntry.Content), writable: false);
                        ownedStreams.Add(stream);
                        entries.Add(NormalizeEntryPath(textEntry.EntryPath), new StreamWithAttributes(stream));
                    }

                    string currentEntry = string.Empty;
                    SharpSevenZipCompressor compressor = new()
                    {
                        ArchiveFormat = OutArchiveFormat.Zip,
                        CompressionLevel = SharpSevenZip.CompressionLevel.Fast,
                        CompressionMethod = CompressionMethod.Deflate,
                        CompressionMode = SharpSevenZip.CompressionMode.Create,
                        DirectoryStructure = true
                    };
                    compressor.CustomParameters.Add("mt", "on");
                    compressor.FileCompressionStarted += (_, args) =>
                    {
                        currentEntry = args.FileName;
                        progress?.Report(new CalibrationExportProgress(args.PercentDone, currentEntry));
                    };
                    compressor.Compressing += (_, args) =>
                    {
                        progress?.Report(new CalibrationExportProgress(args.PercentDone, currentEntry));
                    };

                    progress?.Report(new CalibrationExportProgress(0, string.Empty));
                    using (FileStream destination = new(temporaryArchivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        compressor.CompressStreamDictionary(entries, destination);
                    }
                    progress?.Report(new CalibrationExportProgress(100, currentEntry));
                }
                finally
                {
                    foreach (Stream stream in ownedStreams)
                    {
                        stream.Dispose();
                    }
                }

                File.Move(temporaryArchivePath, fullDestinationPath, true);
            }
            finally
            {
                if (File.Exists(temporaryArchivePath))
                {
                    File.Delete(temporaryArchivePath);
                }
            }
        }

        public static void ExtractToDirectory(
            string archivePath,
            string destinationDirectory,
            IProgress<CalibrationExportProgress>? progress = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

            string fullDestinationDirectory = Path.GetFullPath(destinationDirectory);
            Directory.CreateDirectory(fullDestinationDirectory);
            ValidateZipEntries(archivePath, fullDestinationDirectory);

            string currentEntry = string.Empty;
            using SharpSevenZipExtractor extractor = new(archivePath);
            extractor.FileExtractionStarted += (_, args) =>
            {
                currentEntry = args.FileInfo.FileName;
                progress?.Report(new CalibrationExportProgress(args.PercentDone, currentEntry));
            };
            extractor.Extracting += (_, args) =>
            {
                progress?.Report(new CalibrationExportProgress(args.PercentDone, currentEntry));
            };

            progress?.Report(new CalibrationExportProgress(0, string.Empty));
            extractor.ExtractArchive(fullDestinationDirectory);
            progress?.Report(new CalibrationExportProgress(100, currentEntry));
        }

        private static void ValidateZipEntries(string archivePath, string destinationDirectory)
        {
            string destinationPrefix = Path.TrimEndingDirectorySeparator(destinationDirectory) + Path.DirectorySeparatorChar;
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string relativePath = entry.FullName
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                string destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, relativePath));
                if (!destinationPath.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase)
                    || relativePath.Split(Path.DirectorySeparatorChar).Any(segment => segment.Contains(':')))
                {
                    throw new IOException($"压缩包包含不安全的路径: {entry.FullName}");
                }
            }
        }

        private static string NormalizeEntryPath(string entryPath) => entryPath.Replace('\\', '/');
    }
}
