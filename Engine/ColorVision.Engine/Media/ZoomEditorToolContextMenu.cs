using ColorVision.Common.MVVM;
using ColorVision.Engine.Media;
using ColorVision.Engine.Properties;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools
{
    public record class RotateEditorToolContextMenu(EditorContext context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var MenuItemMetadatas = new  List<MenuItemMetadata>();

            if (context.DrawCanvas.Source is WriteableBitmap writeableBitmap)
            {
                RelayCommand XCommand = new(o =>
                {
                    writeableBitmap.Lock();
                    OpenCvSharp.MatType matType = writeableBitmap.Format.GetPixelFormat();
                    using var srcMat = OpenCvSharp.Mat.FromPixelData(writeableBitmap.PixelHeight, writeableBitmap.PixelWidth, matType, writeableBitmap.BackBuffer, writeableBitmap.BackBufferStride);
                    OpenCvSharp.Cv2.Flip(srcMat, srcMat, OpenCvSharp.FlipMode.X);
                    writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
                    writeableBitmap.Unlock();
                });
                RelayCommand YCommand = new(o =>
                {
                    writeableBitmap.Lock();
                    OpenCvSharp.MatType matType = writeableBitmap.Format.GetPixelFormat();
                    using var srcMat = OpenCvSharp.Mat.FromPixelData(writeableBitmap.PixelHeight, writeableBitmap.PixelWidth, matType, writeableBitmap.BackBuffer, writeableBitmap.BackBufferStride);
                    OpenCvSharp.Cv2.Flip(srcMat, srcMat, OpenCvSharp.FlipMode.Y);
                    writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
                    writeableBitmap.Unlock();
                });
                RelayCommand XYCommand = new(o =>
                {
                    writeableBitmap.Lock();
                    OpenCvSharp.MatType matType = writeableBitmap.Format.GetPixelFormat();
                    using var srcMat = OpenCvSharp.Mat.FromPixelData(writeableBitmap.PixelHeight, writeableBitmap.PixelWidth, matType, writeableBitmap.BackBuffer, writeableBitmap.BackBufferStride);
                    OpenCvSharp.Cv2.Flip(srcMat, srcMat, OpenCvSharp.FlipMode.XY);
                    writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
                    writeableBitmap.Unlock();
                });

                MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Flip", Order = 101, Header = "Flip", Icon = MenuItemIcon.TryFindResource("DIRotate") });
                MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Flip", GuidId = "X", Order = 3, Header = "X", Command = XCommand});
                MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Flip", GuidId = "Y", Order = 4, Header = "Y", Command = YCommand });
                MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Flip", GuidId = "XY", Order = 5, Header = "XY", Command = XYCommand });

                // === Image Processing (图像处理) ===
                MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "ImageProcessing", Order = 102, Header = ColorVision.Engine.Properties.Resources.ImageProcessing });

                // Threshold (二值化)
                RelayCommand thresholdCommand = new(o =>
                {
                    var dlg = new ThresholdDialog(writeableBitmap) { Owner = Application.Current.GetActiveWindow() };
                    dlg.ShowDialog();
                });
                MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "ImageProcessing", GuidId = "Threshold", Order = 1, Header = ColorVision.Engine.Properties.Resources.ThresholdProcessing, Command = thresholdCommand });

                // Erode (腐蚀)
                RelayCommand erodeCommand = new(o =>
                {
                    var dlg = new MorphologyDialog(writeableBitmap, 0) { Owner = Application.Current.GetActiveWindow() };
                    dlg.ShowDialog();
                });
                MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "ImageProcessing", GuidId = "Erode", Order = 2, Header = ColorVision.Engine.Properties.Resources.Erode, Command = erodeCommand });

                // Dilate (膨胀)
                RelayCommand dilateCommand = new(o =>
                {
                    var dlg = new MorphologyDialog(writeableBitmap, 1) { Owner = Application.Current.GetActiveWindow() };
                    dlg.ShowDialog();
                });
                MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "ImageProcessing", GuidId = "Dilate", Order = 3, Header = ColorVision.Engine.Properties.Resources.Dilate, Command = dilateCommand });

                // MorphologyEx (形态学操作)
                RelayCommand morphologyExCommand = new(o =>
                {
                    var dlg = new MorphologyDialog(writeableBitmap, 2) { Owner = Application.Current.GetActiveWindow() };
                    dlg.ShowDialog();
                });
                MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "ImageProcessing", GuidId = "MorphologyEx", Order = 4, Header = ColorVision.Engine.Properties.Resources.MorphologyEx, Command = morphologyExCommand });

                // === Filter/Denoise (滤波/去噪) ===
                MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "FilterDenoise", Order = 103, Header = ColorVision.Engine.Properties.Resources.FilterDenoise });

                // GaussianBlur (高斯滤波)
                RelayCommand gaussianCommand = new(o =>
                {
                    var dlg = new FilterDialog(writeableBitmap, 0) { Owner = Application.Current.GetActiveWindow() };
                    dlg.ShowDialog();
                });
                MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "FilterDenoise", GuidId = "GaussianBlur", Order = 1, Header = ColorVision.Engine.Properties.Resources.GaussianBlur, Command = gaussianCommand });

                // MedianBlur (中值滤波)
                RelayCommand medianCommand = new(o =>
                {
                    var dlg = new FilterDialog(writeableBitmap, 1) { Owner = Application.Current.GetActiveWindow() };
                    dlg.ShowDialog();
                });
                MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "FilterDenoise", GuidId = "MedianBlur", Order = 2, Header = ColorVision.Engine.Properties.Resources.MedianFilter, Command = medianCommand });

                // BilateralFilter (双边滤波/去噪)
                RelayCommand bilateralCommand = new(o =>
                {
                    var dlg = new FilterDialog(writeableBitmap, 2) { Owner = Application.Current.GetActiveWindow() };
                    dlg.ShowDialog();
                });
                MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "FilterDenoise", GuidId = "BilateralFilter", Order = 3, Header = ColorVision.Engine.Properties.Resources.BilateralFilter, Command = bilateralCommand });

                // Blur (均值滤波)
                RelayCommand blurCommand = new(o =>
                {
                    var dlg = new FilterDialog(writeableBitmap, 3) { Owner = Application.Current.GetActiveWindow() };
                    dlg.ShowDialog();
                });
                MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "FilterDenoise", GuidId = "Blur", Order = 4, Header = ColorVision.Engine.Properties.Resources.Blur, Command = blurCommand });
            }



            return MenuItemMetadatas;
        }
    }


}
