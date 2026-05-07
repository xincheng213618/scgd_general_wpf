using ColorVision.Core;
using OpenCvSharp.WpfExtensions;
using System;
using System.Windows.Media.Imaging;

namespace Conoscope.Core
{
    public sealed class ConoscopePseudoColorRenderResult
    {
        public ConoscopePseudoColorRenderResult(WriteableBitmap bitmap, ExportChannel channel, double minValue, double maxValue)
        {
            Bitmap = bitmap;
            Channel = channel;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public WriteableBitmap Bitmap { get; }
        public ExportChannel Channel { get; }
        public double MinValue { get; }
        public double MaxValue { get; }
    }

    public static class ConoscopePseudoColorRenderer
    {
        public static ConoscopePseudoColorRenderResult Render(
            OpenCvSharp.Mat xMat,
            OpenCvSharp.Mat yMat,
            OpenCvSharp.Mat zMat,
            ExportChannel channel,
            ColormapTypes colormap,
            Func<OpenCvSharp.Mat> createColorDifferenceMat)
        {
            using OpenCvSharp.Mat channelMat = CreateDisplayChannelMat(xMat, yMat, zMat, channel, createColorDifferenceMat);
            using OpenCvSharp.Mat normalized = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat gray8 = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat pseudoColor = new OpenCvSharp.Mat();

            OpenCvSharp.Cv2.MinMaxLoc(channelMat, out double minValue, out double maxValue);
            OpenCvSharp.Cv2.Normalize(channelMat, normalized, 0, 255, OpenCvSharp.NormTypes.MinMax);
            normalized.ConvertTo(gray8, OpenCvSharp.MatType.CV_8UC1);
            OpenCvSharp.Cv2.ApplyColorMap(gray8, pseudoColor, ResolveOpenCvColormap(colormap));

            WriteableBitmap bitmap = pseudoColor.ToWriteableBitmap();
            bitmap.Freeze();

            return new ConoscopePseudoColorRenderResult(bitmap, channel, minValue, maxValue);
        }

        private static OpenCvSharp.Mat CreateDisplayChannelMat(
            OpenCvSharp.Mat xMat,
            OpenCvSharp.Mat yMat,
            OpenCvSharp.Mat zMat,
            ExportChannel channel,
            Func<OpenCvSharp.Mat> createColorDifferenceMat)
        {
            return channel == ExportChannel.ColorDifference
                ? createColorDifferenceMat()
                : ConoscopeColorimetry.CreateChannelMat(xMat, yMat, zMat, channel);
        }

        private static OpenCvSharp.ColormapTypes ResolveOpenCvColormap(ColormapTypes colormapType)
        {
            int value = (int)colormapType;
            return Enum.IsDefined(typeof(OpenCvSharp.ColormapTypes), value)
                ? (OpenCvSharp.ColormapTypes)value
                : OpenCvSharp.ColormapTypes.Jet;
        }
    }
}
