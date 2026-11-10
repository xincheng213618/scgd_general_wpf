using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using FlowEngineLib.Algorithm;
using OpenCvSharp;
using System;
using System.Windows;
using System.Windows.Media.Imaging;
using ColorVision.ImageEditor.Tif;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    // Copy before a flow hands its mutable buffers downstream. The view retains only the latest snapshot.
    internal sealed class LocalCameraPreview
    {
        internal required WriteableBitmap Bitmap { get; init; }
        internal required byte[] CieData { get; init; }
        internal required LocalFrameMetadata Metadata { get; init; }
        internal required float[] Exposure { get; init; }
        internal required ColorVisionTiffParameters Parameters { get; init; }

        public static LocalCameraPreview Create(LocalFlowFrame frame)
        {
            using LocalFlowFrameLease lease = frame.Acquire();
            LocalFrameMetadata metadata = lease.Metadata;
            // Borrow RAW only during the synchronous copy into an independently owned display bitmap.
            CVImageFlipMode displayFlip = frame.IsRawFlipApplied ? CVImageFlipMode.None : metadata.FlipMode;
            WriteableBitmap bitmap = CreateRawBitmap(lease.RawPointer, lease.RawLength, metadata.SourceBpp, metadata.Channels,
                metadata.Width, metadata.Height, displayFlip);
            bitmap.Freeze();
            return new LocalCameraPreview
            {
                Bitmap = bitmap, CieData = lease.CopyCieToArray(), Metadata = metadata,
                Exposure = (float[])metadata.Exposure.Clone(),
                Parameters = CameraFileMetadata.CreateParameters(metadata, lease.ColorCalibration, lease.CalibrationProvenance)
            };
        }

        internal static unsafe WriteableBitmap CreateRawBitmap(byte[] raw, int bpp, int channels, int width, int height)
        {
            fixed (byte* pointer = raw)
                return CreateRawBitmap((IntPtr)pointer, raw.Length, bpp, channels, width, height);
        }

        private static WriteableBitmap CreateRawBitmap(IntPtr raw, int length, int bpp, int channels, int width, int height,
            CVImageFlipMode displayFlip = CVImageFlipMode.None)
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
            if (width <= 0 || height <= 0 || raw == IntPtr.Zero || length != checked(stride * height))
                throw new InvalidOperationException("本地预览的 RAW 图像格式或长度无效。");
            // Use the CVRAW decoder's BGR-to-WPF conversion, including BGR16 -> Rgb48.
            // The converter owns the display copy; neither source samples nor their order are changed.
            using Mat mat = Mat.FromPixelData(height, width, matType, raw, stride);
            WriteableBitmap bitmap = mat.CreateDisplayBitmap();
            if (displayFlip == CVImageFlipMode.None) return bitmap;

            // Orient the private display buffer without modifying the frame or allocating a second bitmap.
            bitmap.Lock();
            try
            {
                using Mat display = Mat.FromPixelData(height, width, matType, bitmap.BackBuffer, bitmap.BackBufferStride);
                Cv2.Flip(display, display, LocalFrameMirrorService.ToOpenCvFlipMode(displayFlip));
                bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally { bitmap.Unlock(); }
            return bitmap;
        }

        public void Show(ImageView view, bool copyBitmap = true)
        {
            view.ReleaseImageContent();
            view.IEditorToolFactory.ApplyImageOpenTools(null);
            view.SetLayerController(null);
            view.Config.ClearProperties();
            view.OpenImage(copyBitmap ? new WriteableBitmap(Bitmap) : Bitmap);
            if (CieData.Length > 0 && view.IEditorToolFactory.IImageOpens.TryGetValue(".cvcie", out var opener) && opener is CVRawOpen cvRaw)
            {
                int cieChannels = CieData.Length / checked(Metadata.Width * Metadata.Height * sizeof(float));
                cvRaw.AttachLiveCvcie(view, (uint)Metadata.Width, (uint)Metadata.Height, (uint)Metadata.CieBpp,
                    (uint)cieChannels, CieData, (float[])Exposure.Clone());
            }
            view.SetImageParameterRecord(Parameters);
        }
    }
}
