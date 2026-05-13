using OpenCvSharp;

namespace Conoscope.Processing.Preprocess
{
    internal static class XyzClampProcessor
    {
        public static int ClampNonPositive(Mat mat, float lowerBound)
        {
            using Mat mask = new Mat();
            Cv2.Compare(mat, Scalar.All(0), mask, CmpTypes.LE);
            int count = Cv2.CountNonZero(mask);
            if (count > 0)
            {
                mat.SetTo(Scalar.All(lowerBound), mask);
            }

            return count;
        }
    }
}