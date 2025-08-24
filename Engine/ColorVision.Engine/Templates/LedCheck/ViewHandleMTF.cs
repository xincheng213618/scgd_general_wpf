using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.ToolPlugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Templates.LedCheck
{
    public class ViewHandleLedCheck : IResultHandleBase
    {
        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { };

        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            string fileName = Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var ViewResults = result.ViewResults.ToSpecificViewResults<ViewResultLedCheck>();

            var csvBuilder = new StringBuilder();
            List<string> properties = new() { "x", "y", "Radius" };
            csvBuilder.AppendLine(string.Join(",", properties));
            foreach (var item in ViewResults)
            {
                List<string> strings = new List<string>()
                {
                    item.Point.X.ToString(),
                    item.Point.Y.ToString(),
                    item.Radius.ToString(),
                };
                csvBuilder.AppendLine(string.Join(",", properties));
            }

            File.WriteAllText(fileName, csvBuilder.ToString(), Encoding.UTF8);
        }



        public override void Load(AlgorithmView view, AlgorithmResult result)
        {
            base.Load(view, result);
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<PoiPointResultModel> AlgResultLedcheckModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultLedcheckModels)
                {
                    ViewResultLedCheck ledResultData = new(new Point((double)item.PoiX, (double)item.PoiY), (double)item.PoiWidth / 2);
                    result.ViewResults.Add(ledResultData);
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmLedCheck), ImageFilePath = result.FilePath })) });
            }

        }


        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {

            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            var header = new List<string> { "坐标", "半径" };
            var bdHeader = new List<string> { "Point", "Radius" };

            List<Point> points = new();
            List<double> Radius = new();

            double radius = 0;
            foreach (var item in result.ViewResults)
            {
                if (item is ViewResultLedCheck viewResultLedCheck)
                {
                    points.Add(viewResultLedCheck.Point);
                    Radius.Add(viewResultLedCheck.Radius);
                    radius = viewResultLedCheck.Radius;
                }
            }
            int z = 0;
            int[] ints = new int[points.Count * 2];
            for (int i = 0; i < points.Count; i++)
            {
                ints[2 * z] = (int)points[i].X;
                ints[2 * z + 1] = (int)points[i].Y;
                z += 1;
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                BitmapImage bitmapImage = new BitmapImage(new Uri(result.FilePath));
                HImage hImage = bitmapImage.ToHImage();

                int ret = OpenCVMediaHelper.M_DrawPoiImage(hImage, out HImage hImageProcessed, (int)radius, ints, ints.Length, LedToolConfig.Instance.Thickness);
                if (ret == 0)
                {
                    if (!HImageExtension.UpdateWriteableBitmap(view.ImageView.FunctionImage, hImageProcessed))
                    {
                        var image = hImageProcessed.ToWriteableBitmap();

                        OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                        hImageProcessed.pData = IntPtr.Zero;
                        view.ImageView.FunctionImage = image;
                    }
                    view.ImageView.ImageShow.Source = view.ImageView.FunctionImage;
                }
                hImage.Dispose();
            });

            if (view.listViewSide.View is GridView gridView)
            {
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.listViewSide.ItemsSource = result.ViewResults;
            }
        }


    }

}
