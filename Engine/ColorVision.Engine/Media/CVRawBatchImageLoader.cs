using ColorVision.FileIO;
using ColorVision.ImageEditor.BatchProcessing;
using OpenCvSharp;
using System;
using System.Collections.Generic;

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
            Mat? source = file.ToMat();
            if (source != null && !source.Empty())
            {
                // An associated CIE can still arrive as three complete integer planes.
                // Retain only the displayed plane after this source is released.
                if (file.FileExtType == CVType.CIE && file.Channels == 3 && file.Bpp is 8 or 16)
                {
                    using (source) return source.Clone();
                }
                // Array-backed Mats hold their own pin. Disposing the file only clears its reference.
                return source;
            }
            source?.Dispose();
            throw new InvalidOperationException($"无法读取 ColorVision 图像：{filePath}");
        }
    }
}
