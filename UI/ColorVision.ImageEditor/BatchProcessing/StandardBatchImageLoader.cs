using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.BatchProcessing
{
    public sealed class StandardBatchImageLoader : IBatchImageLoader
    {
        private static readonly string[] SupportedExtensions =
        {
            ".bmp", ".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff",
        };

        public IReadOnlyCollection<string> Extensions => SupportedExtensions;

        public Mat Load(string filePath)
        {
            Mat image = Cv2.ImRead(filePath, ImreadModes.Unchanged);
            if (image.Empty())
            {
                image.Dispose();
                throw new InvalidOperationException($"无法读取图像：{filePath}");
            }

            return image;
        }
    }
}
