using Conoscope.Core;
using OpenCvSharp;

namespace Conoscope.Processing.Preprocess
{
    public sealed record ImageFilterOptions(
        ImageFilterType FilterType,
        int KernelSize,
        double Sigma,
        int BilateralD,
        double SigmaColor,
        double SigmaSpace);

    internal static class ImageFilterProcessor
    {
        public static Mat Apply(Mat src, ImageFilterOptions options)
        {
            Mat dst = new Mat();
            Mat workMat = src;

            switch (options.FilterType)
            {
                case ImageFilterType.LowPass:
                    Cv2.Blur(workMat, dst, new Size(options.KernelSize, options.KernelSize));
                    break;
                case ImageFilterType.MovingAverage:
                    Cv2.BoxFilter(workMat, dst, workMat.Type(), new Size(options.KernelSize, options.KernelSize));
                    break;
                case ImageFilterType.Gaussian:
                    Cv2.GaussianBlur(workMat, dst, new Size(options.KernelSize, options.KernelSize), options.Sigma);
                    break;
                case ImageFilterType.Median:
                    if (src.Depth() == MatType.CV_32F)
                    {
                        Cv2.MedianBlur(workMat, dst, options.KernelSize);
                    }
                    else
                    {
                        using Mat floatMat = new Mat();
                        workMat.ConvertTo(floatMat, MatType.CV_32FC1);
                        Cv2.MedianBlur(floatMat, dst, options.KernelSize);
                        Mat result = new Mat();
                        dst.ConvertTo(result, src.Type());
                        dst.Dispose();
                        dst = result;
                    }
                    break;
                case ImageFilterType.Bilateral:
                    if (src.Depth() == MatType.CV_32F)
                    {
                        Cv2.BilateralFilter(workMat, dst, options.BilateralD, options.SigmaColor, options.SigmaSpace);
                    }
                    else
                    {
                        using Mat floatMat = new Mat();
                        workMat.ConvertTo(floatMat, MatType.CV_32FC1);
                        Cv2.BilateralFilter(floatMat, dst, options.BilateralD, options.SigmaColor, options.SigmaSpace);
                        Mat result = new Mat();
                        dst.ConvertTo(result, src.Type());
                        dst.Dispose();
                        dst = result;
                    }
                    break;
                default:
                    return src.Clone();
            }

            return dst;
        }
    }
}