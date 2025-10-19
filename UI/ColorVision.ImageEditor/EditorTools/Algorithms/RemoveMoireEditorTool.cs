using ColorVision.Common.MVVM;
using ColorVision.Core;
using log4net;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    public record class RemoveMoireEditorTool(EditorContext EditorContext) : IEditorTool
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(RemoveMoireEditorTool));

        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;
        public string? GuidId => "RemoveMoire";
        public int Order { get; set; } = 25;

        public object Icon { get; set; } = IEditorToolFactory.TryFindResource("DrawingImageMax");

        public ICommand? Command { get; set; } = new RelayCommand(_ =>
        {
            var imageView = EditorContext.ImageView;
            if (imageView == null) return;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (imageView.HImageCache == null) return;
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                log.Info($"RemoveMoire");
                Task.Run(() =>
                {
                    int ret = OpenCVMediaHelper.M_RemoveMoire((HImage)imageView.HImageCache, out HImage hImageProcessed);
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (ret == 0)
                        {
                            if (!HImageExtension.UpdateWriteableBitmap(imageView.FunctionImage, hImageProcessed))
                            {
                                double DpiX = imageView.Config.GetProperties<double>("DpiX");
                                double DpiY = imageView.Config.GetProperties<double>("DpiY");
                                var image = hImageProcessed.ToWriteableBitmap(DpiX, DpiY);

                                hImageProcessed.Dispose();

                                imageView.FunctionImage = image;
                            }
                            imageView.ImageShow.Source = imageView.FunctionImage;
                            stopwatch.Stop();
                            log.Info($"RemoveMoire {stopwatch.Elapsed}");
                        }
                    });
                });
            });
        });
    }
}
