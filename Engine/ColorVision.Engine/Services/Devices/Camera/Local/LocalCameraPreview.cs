using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Realtime;
using FlowEngineLib.Algorithm;
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
            int stride = checked(metadata.Width * metadata.Channels * (metadata.SourceBpp / 8));
            if (metadata.SourceBpp is not (8 or 16) || metadata.Channels is not (1 or 3)
                || raw.Length != checked(stride * metadata.Height))
                throw new InvalidOperationException("本地预览的 RAW 图像格式或长度无效。");
            // Preserve the existing local window's 0/2/1 channel mapping; use packed source rows.
            if (metadata.Channels == 3 && metadata.SourceBpp == 16)
                for (int i = 0; i < raw.Length; i += 6)
                {
                    (raw[i + 2], raw[i + 4]) = (raw[i + 4], raw[i + 2]);
                    (raw[i + 3], raw[i + 5]) = (raw[i + 5], raw[i + 3]);
                }
            BitmapSource bitmap = BitmapSource.Create(metadata.Width, metadata.Height, 96, 96,
                RealtimeFramePresenter.GetPixelFormat(metadata.Channels, metadata.SourceBpp), null, raw, stride);
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
