using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{


    public record class InvertContextMenu(EditorContext EditorContext) : IIEditorToolContextMenu
    {

        public void InvertImag()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (EditorContext.ImageView.ImageShow.Source is WriteableBitmap writeableBitmap)
                {
                    HImage _hImageCache = writeableBitmap.ToHImage();

                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    Task.Run(() =>
                    {
                        int ret = OpenCVMediaHelper.M_InvertImage(_hImageCache, out HImage hImageProcessed);
                        _hImageCache.Dispose();
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            if (ret == 0)
                            {
                                if (!HImageExtension.UpdateWriteableBitmap(EditorContext.ImageView.ViewBitmapSource, hImageProcessed))
                                {
                                    double DpiX = EditorContext.Config.GetProperties<double>("DpiX");
                                    double DpiY = EditorContext.Config.GetProperties<double>("DpiY");
                                    var image = hImageProcessed.ToWriteableBitmap(DpiX, DpiY);
                                    hImageProcessed.Dispose();

                                    EditorContext.ImageView.ViewBitmapSource = image;
                                }
                                EditorContext.ImageView.ImageShow.Source = EditorContext.ImageView.ViewBitmapSource;
                                stopwatch.Stop();
                            }
                        });
                    });
                }
            });
        }
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var MenuItemMetadatas = new  List<MenuItemMetadata>();

            RelayCommand relayCommand = new RelayCommand((o) =>
            {
                InvertImag();
            });
            MenuItemMetadatas.Add(new MenuItemMetadata() {GuidId = "Algorithms", Order = 303, Header = "算法" });
            MenuItemMetadatas.Add(new MenuItemMetadata() {OwnerGuid = "Algorithms", GuidId = "InvertImage", Order = 303, Header = ColorVision.ImageEditor.Properties.Resources.Invert, Command = relayCommand});
            return MenuItemMetadatas;
        }
    }


}
