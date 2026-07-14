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
            using CVCIEFile file = CVFileUtil.OpenLocalCVFile(filePath);
            using Mat? source = file.ToMat();
            if (source == null || source.Empty())
            {
                throw new InvalidOperationException($"无法读取 ColorVision 图像：{filePath}");
            }

            // ToMat may reference CVCIEFile.Data, so detach before disposing the file.
            return source.Clone();
        }
    }
}
