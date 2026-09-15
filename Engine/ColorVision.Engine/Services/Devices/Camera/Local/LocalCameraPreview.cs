using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using FlowEngineLib.Algorithm;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    // Copy before a flow hands its mutable buffers downstream. The view retains only the latest snapshot.
    internal sealed class LocalCameraPreview
    {
        internal required BitmapSource Bitmap { get; init; }
        internal required byte[] CieData { get; init; }
        internal required LocalFrameMetadata Metadata { get; init; }
        internal required float[] Exposure { get; init; }

        public static LocalCameraPreview Create(LocalFlowFrame frame)
        {
            using LocalFlowFrameLease lease = frame.Acquire();
            LocalFrameMetadata metadata = lease.Metadata;
            byte[] raw = lease.CopyRawToArray();
            BitmapSource bitmap = CreateRawBitmap(raw, metadata.SourceBpp, metadata.Channels, metadata.Width, metadata.Height);
            if (!frame.IsRawFlipApplied && metadata.FlipMode != CVImageFlipMode.None)
            {
                LocalFrameMirrorService.ValidateFlipMode(metadata.FlipMode);
                bool flipX = metadata.FlipMode is CVImageFlipMode.Y or CVImageFlipMode.XY;
                bool flipY = metadata.FlipMode is CVImageFlipMode.X or CVImageFlipMode.XY;
                bitmap = new TransformedBitmap(bitmap, new ScaleTransform(flipX ? -1 : 1, flipY ? -1 : 1));
            }
            bitmap.Freeze();
            return new LocalCameraPreview { Bitmap = bitmap, CieData = lease.CopyCieToArray(), Metadata = metadata, Exposure = (float[])metadata.Exposure.Clone() };
        }

        internal static WriteableBitmap CreateRawBitmap(byte[] raw, int bpp, int channels, int width, int height)
        {
            MatType matType = (bpp, channels) switch
            {
                (8, 1) => MatType.CV_8UC1,
                (8, 3) => MatType.CV_8UC3,
                (16, 1) => MatType.CV_16UC1,
                (16, 3) => MatType.CV_16UC3,
                _ => throw new InvalidOperationException("本地预览的 RAW 图像格式无效。")
            };
            int stride = checked(width * channels * (bpp / 8));
            if (width <= 0 || height <= 0 || raw.Length != checked(stride * height))
                throw new InvalidOperationException("本地预览的 RAW 图像格式或长度无效。");
            // Use the CVRAW decoder's BGR-to-WPF conversion, including BGR16 -> Rgb48.
            // The converter owns the display copy; neither source samples nor their order are changed.
            using Mat mat = Mat.FromPixelData(height, width, matType, raw, stride);
            return mat.ToWriteableBitmap();
        }

        public void Show(ImageView view)
        {
            view.EditorContext.IImageOpen = null;
            view.IEditorToolFactory.ApplyImageOpenTools(null);
            view.SetLayerController(null);
            view.Config.ClearProperties();
            view.OpenImage(new WriteableBitmap(Bitmap));
            if (CieData.Length > 0 && view.IEditorToolFactory.IImageOpens.TryGetValue(".cvcie", out var opener) && opener is CVRawOpen cvRaw)
            {
                int cieChannels = CieData.Length / checked(Metadata.Width * Metadata.Height * sizeof(float));
                cvRaw.AttachLiveCvcie(view, (uint)Metadata.Width, (uint)Metadata.Height, (uint)Metadata.CieBpp,
                    (uint)cieChannels, CieData, (float[])Exposure.Clone());
            }
        }
    }
}
