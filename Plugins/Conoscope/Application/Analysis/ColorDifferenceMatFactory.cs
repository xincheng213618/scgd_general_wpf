using Conoscope.Core;
using OpenCvSharp;
using System;

namespace Conoscope.ApplicationServices.Analysis
{
    internal static class ColorDifferenceMatFactory
    {
        public static Mat Create(Mat xMat, Mat yMat, Mat zMat, ConoscopeUvReference reference)
        {
            return ConoscopeColorimetry.CreateColorDifferenceMat(xMat, yMat, zMat, reference.U, reference.V);
        }

        public static Mat Create(Mat xMat, Mat yMat, Mat zMat, Mat referenceUMat, Mat referenceVMat)
        {
            return ConoscopeColorimetry.CreateColorDifferenceMat(xMat, yMat, zMat, referenceUMat, referenceVMat);
        }

        public static double GetValue(double x, double y, double z, ConoscopeUvReference reference)
        {
            return ConoscopeColorimetry.CalculateColorDifference(x, y, z, reference.U, reference.V);
        }

        public static double GetValue(int ix, int iy, double x, double y, double z, Mat referenceUMat, Mat referenceVMat)
        {
            ArgumentNullException.ThrowIfNull(referenceUMat);
            ArgumentNullException.ThrowIfNull(referenceVMat);

            int sampleX = ConoscopeNumericHelper.ClampToInt(ix, 0, referenceUMat.Width - 1);
            int sampleY = ConoscopeNumericHelper.ClampToInt(iy, 0, referenceUMat.Height - 1);
            double referenceU = referenceUMat.At<float>(sampleY, sampleX);
            double referenceV = referenceVMat.At<float>(sampleY, sampleX);
            return ConoscopeColorimetry.CalculateColorDifference(x, y, z, referenceU, referenceV);
        }
    }
}