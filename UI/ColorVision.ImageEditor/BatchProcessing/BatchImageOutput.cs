using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace ColorVision.ImageEditor.BatchProcessing
{
    public static class BatchImageOutput
    {
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
        {
            string directory = ResolveOutputDirectory(item, outputDirectory, preserveFolderStructure);
            string extension = ResolveExtension(item.FilePath, format);
            string baseName = Path.GetFileNameWithoutExtension(item.FilePath) + suffix;
            string outputPath = Path.Combine(directory, baseName + extension);

            if (Path.GetFullPath(outputPath).Equals(Path.GetFullPath(item.FilePath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("输出文件不能覆盖源文件，请设置输出后缀或输出目录。");
            }

            int index = 2;
            while (reservedPaths.Contains(outputPath) || avoidOverwrite && File.Exists(outputPath))
            {
                outputPath = Path.Combine(directory, $"{baseName}_{index++}{extension}");
            }
            reservedPaths.Add(outputPath);

            return outputPath;
        }

        public static void Save(Mat image, string filePath)
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            using Mat writable = PrepareForEncoder(image, extension);
            if (!Cv2.ImWrite(filePath, writable))
            {
                throw new IOException($"保存图像失败：{filePath}");
            }
        }

        private static string ResolveOutputDirectory(BatchImageItem item, string? outputDirectory, bool preserveFolderStructure)
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

        private static Mat PrepareForEncoder(Mat image, string extension)
        {
            bool supportsHighDepth = extension is ".png" or ".tif" or ".tiff";
            if (image.Depth() == MatType.CV_8U)
            {
                return PrepareChannels(image, extension);
            }

            if (supportsHighDepth && image.Depth() == MatType.CV_16U)
            {
                return PrepareChannels(image, extension);
            }

            if (extension is ".tif" or ".tiff" && (image.Depth() == MatType.CV_32F || image.Depth() == MatType.CV_64F))
            {
                return PrepareChannels(image, extension);
            }

            Mat normalized = new();
            Cv2.Normalize(image, normalized, 0, supportsHighDepth ? ushort.MaxValue : byte.MaxValue, NormTypes.MinMax);
            Mat converted = new();
            normalized.ConvertTo(converted, supportsHighDepth ? MatType.CV_16U : MatType.CV_8U);
            normalized.Dispose();
            Mat prepared = PrepareChannels(converted, extension);
            converted.Dispose();
            return prepared;
        }

        private static Mat PrepareChannels(Mat image, string extension)
        {
            if (extension is not ".jpg" and not ".jpeg" || image.Channels() != 4)
            {
                return image.Clone();
            }

            Mat result = new();
            Cv2.CvtColor(image, result, ColorConversionCodes.BGRA2BGR);
            return result;
        }
    }
}
