using OpenCvSharp;
using System;

namespace ColorVision.ImageEditor.Algorithms
{
    internal static class ImageAlgorithmApplier
    {
        public static void Apply(ImageProcessingContext image, Action<Mat> apply)
        {
            ImageAlgorithmPreviewSession session = ImageAlgorithmPreviewSession.Start(image);
            try
            {
                session.Apply(apply);
                session.Commit();
            }
            catch
            {
                session.Cancel();
                throw;
            }
        }
    }
}
