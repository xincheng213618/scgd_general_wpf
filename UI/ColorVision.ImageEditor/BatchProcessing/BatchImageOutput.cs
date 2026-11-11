using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ColorVision.ImageEditor.BatchProcessing
{
    public static class BatchImageOutput
    {
        internal static HashSet<string> ReserveSourcePaths(IEnumerable<BatchImageItem> items)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (BatchImageItem item in items)
            {
                try
                {
                    paths.Add(Path.GetFullPath(item.FilePath));
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    // The processing loop reports invalid paths as individual file failures.
                }
            }
            return paths;
        }

        public static string ResolveExtension(string sourcePath, BatchOutputFormat format)
        {
            if (format == BatchOutputFormat.SameAsSource)
            {
                string sourceExtension = Path.GetExtension(sourcePath).ToLowerInvariant();
                return sourceExtension is ".cvraw" or ".cvcie" ? ".tiff" : sourceExtension;
            }

            return format switch
            {
                BatchOutputFormat.Png => ".png",
                BatchOutputFormat.Jpeg => ".jpg",
                BatchOutputFormat.Bmp => ".bmp",
                BatchOutputFormat.Tiff => ".tiff",
                BatchOutputFormat.WebP => ".webp",
                _ => throw new ArgumentOutOfRangeException(nameof(format)),
            };
        }

        public static string CreateOutputPath(
            BatchImageItem item,
            string? outputDirectory,
            string suffix,
            BatchOutputFormat format,
            bool preserveFolderStructure,
            bool avoidOverwrite,
            ISet<string> reservedPaths)
            => CreateOutputPath(item, outputDirectory, suffix, format, preserveFolderStructure, avoidOverwrite,
                reservedPaths, nextUnreservedNumbers: null);

        internal static string CreateOutputPath(
            BatchImageItem item,
            string? outputDirectory,
            string suffix,
            BatchOutputFormat format,
            bool preserveFolderStructure,
            bool avoidOverwrite,
            ISet<string> reservedPaths,
            Dictionary<string, int>? nextUnreservedNumbers)
        {
            string directory = ResolveOutputDirectory(item, outputDirectory, preserveFolderStructure);
            string extension = ResolveExtension(item.FilePath, format);
            string baseName = Path.GetFileNameWithoutExtension(item.FilePath) + suffix;
            string outputPath = Path.Combine(directory, baseName + extension);
            string fullOutputPath = Path.GetFullPath(outputPath);

            if (fullOutputPath.Equals(Path.GetFullPath(item.FilePath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(Properties.Resources.BatchCannotOverwriteSource);
            }

            if (IsReserved() || avoidOverwrite && File.Exists(fullOutputPath))
            {
                string unnumberedPath = fullOutputPath;
                int nextUnreserved = nextUnreservedNumbers?.GetValueOrDefault(unnumberedPath, 2) ?? 2;
                for (int index = nextUnreserved; ; index++)
                {
                    outputPath = Path.Combine(directory, $"{baseName}_{index}{extension}");
                    fullOutputPath = Path.GetFullPath(outputPath);
                    bool reserved = IsReserved();
                    bool existsOnDisk = !reserved && avoidOverwrite && File.Exists(fullOutputPath);
                    // Only batch reservations are permanent. An existing file can disappear between items.
                    if (index == nextUnreserved && !existsOnDisk)
                        nextUnreserved++;
                    if (!reserved && !existsOnDisk)
                        break;
                }
                if (nextUnreserved > 2 && nextUnreservedNumbers != null)
                    nextUnreservedNumbers[unnumberedPath] = nextUnreserved;
            }
            reservedPaths.Add(fullOutputPath);

            return outputPath;

            bool IsReserved() => reservedPaths.Contains(fullOutputPath) || reservedPaths.Contains(outputPath);
        }

        public static void Save(Mat image, string filePath, bool overwrite = true)
            => EncodeAndPublish(image, filePath, overwrite, validateOutputPath: null, resolveCollision: null, CancellationToken.None);

        internal static void EncodeAndPublish(Mat image, string filePath, bool overwrite, Action<string>? validateOutputPath,
            Func<string>? resolveCollision, CancellationToken cancellationToken)
        {
            ValidateDestination();
            string fullPath = Path.GetFullPath(filePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string extension = Path.GetExtension(fullPath).ToLowerInvariant();
            string temporaryPath = Path.Combine(
                directory ?? Environment.CurrentDirectory,
                $".__colorvision_{Guid.NewGuid():N}.tmp{extension}");
            bool supportsHighDepth = extension is ".png" or ".tif" or ".tiff";
            bool supportsDepth = image.Depth() == MatType.CV_8U
                || supportsHighDepth && image.Depth() == MatType.CV_16U
                || extension is ".tif" or ".tiff" && (image.Depth() == MatType.CV_32F || image.Depth() == MatType.CV_64F);
            Mat writable = image;
            Mat? convertedDepth = null;
            Mat? convertedChannels = null;
            try
            {
                if (!supportsDepth)
                {
                    convertedDepth = new Mat();
                    Cv2.Normalize(image, convertedDepth, 0, supportsHighDepth ? ushort.MaxValue : byte.MaxValue, NormTypes.MinMax);
                    convertedDepth.ConvertTo(convertedDepth, supportsHighDepth ? MatType.CV_16U : MatType.CV_8U);
                    writable = convertedDepth;
                }

                if (extension is ".jpg" or ".jpeg" && writable.Channels() == 4)
                {
                    convertedChannels = new Mat();
                    Cv2.CvtColor(writable, convertedChannels, ColorConversionCodes.BGRA2BGR);
                    writable = convertedChannels;
                }

                if (!Cv2.ImWrite(temporaryPath, writable))
                {
                    throw new IOException(string.Format(Properties.Resources.BatchImageSaveFailed, fullPath));
                }

                while (true)
                {
                    ValidateDestination();
                    try
                    {
                        File.Move(temporaryPath, fullPath, overwrite);
                        return;
                    }
                    catch (IOException) when (!overwrite && resolveCollision != null && File.Exists(fullPath))
                    {
                        filePath = resolveCollision();
                        fullPath = Path.GetFullPath(filePath);
                    }
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                }

                convertedChannels?.Dispose();
                convertedDepth?.Dispose();
            }

            void ValidateDestination()
            {
                cancellationToken.ThrowIfCancellationRequested();
                validateOutputPath?.Invoke(filePath);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        /// <summary>Resolves the destination layout without creating directories or files.</summary>
        public static string ResolveOutputDirectory(BatchImageItem item, string? outputDirectory, bool preserveFolderStructure)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                return Path.GetDirectoryName(item.FilePath) ?? Environment.CurrentDirectory;
            }

            string directory = Path.GetFullPath(outputDirectory);
            if (!preserveFolderStructure || string.IsNullOrWhiteSpace(item.SourceRoot))
            {
                return directory;
            }

            string? sourceDirectory = Path.GetDirectoryName(item.FilePath);
            if (string.IsNullOrWhiteSpace(sourceDirectory))
            {
                return directory;
            }

            string relative = Path.GetRelativePath(item.SourceRoot, sourceDirectory);
            return relative == "." ? directory : Path.Combine(directory, relative);
        }

    }
}
