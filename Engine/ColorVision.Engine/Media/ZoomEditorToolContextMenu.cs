using ColorVision.Common.MVVM;
using ColorVision.Engine.Media;
using ColorVision.ImageEditor.EditorTools.Rotate;
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

            if (context.ImageView.ImageShow.Source is WriteableBitmap writeableBitmap)
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
                MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Flip", GuidId = "XY", Order = 4, Header = "XY", Command = XYCommand });
            }






            return MenuItemMetadatas;
        }
    }


}
