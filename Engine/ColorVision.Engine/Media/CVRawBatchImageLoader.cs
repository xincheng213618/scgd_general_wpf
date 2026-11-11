using ColorVision.FileIO;
using ColorVision.ImageEditor.BatchProcessing;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace ColorVision.Engine.Media
{
    /// <summary>
    /// Adds CVRAW/CVCIE decoding to ImageEditor's format-agnostic batch processor.
    /// </summary>
    public sealed class CVRawBatchImageLoader : IBatchImageLoader
    {
        private static readonly string[] SupportedExtensions = { ".cvraw", ".cvcie" };

        public IReadOnlyCollection<string> Extensions => SupportedExtensions;

        public Mat Load(string filePath)
        {
            using CVCIEFile file = CVFileUtil.OpenLocalCVFileForDisplay(filePath);
            Mat? source = TryConvert(file);
            if (source == null && file.Data is { Length: > 0 } && !string.IsNullOrEmpty(file.SrcFileName)
                && string.Equals(Path.GetExtension(filePath), ".cvcie", StringComparison.OrdinalIgnoreCase))
            {
                using CVCIEFile embedded = CVFileUtil.OpenLocalCVFileForDisplay(filePath, followAssociatedSource: false);
                source = TryConvert(embedded);
            }
            return source ?? throw new InvalidOperationException($"无法读取 ColorVision 图像：{filePath}");
        }

        private static Mat? TryConvert(CVCIEFile file)
        {
            if (file.Data is not { Length: > 0 }) return null;
            Mat? source = file.ToMat(showErrors: false);
            if (source == null || source.Empty())
            {
                source?.Dispose();
                return null;
            }

            // An associated CIE can still arrive as three complete integer planes.
            if (file.FileExtType == CVType.CIE && file.Channels == 3 && file.Bpp is 8 or 16)
            {
                using (source) return source.Clone();
            }
            // Array-backed Mats hold their own pin. Disposing the file only clears its reference.
            return source;
        }
    }
}
