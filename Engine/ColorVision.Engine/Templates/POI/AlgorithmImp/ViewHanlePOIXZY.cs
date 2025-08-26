#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Common.MVVM;
using ColorVision.Engine.Media;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.POI.Image;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using log4net;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    public class ViewHanlePOIXZY : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleRealPOI));
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.POI_XYZ ,ViewResultAlgType.LEDStripDetection };
        
        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string fileName = Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var PoiResultCIExyuvDatas = result.ViewResults.ToSpecificViewResults<PoiResultCIExyuvData>();
            PoiResultCIExyuvData.SaveCsv(PoiResultCIExyuvDatas, fileName);
        }
        public override void Load(AlgorithmView view, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                int id = 0;
                foreach (var item in POIPointResultModels)
                {
                    PoiResultCIExyuvData poiResultCIExyuvData = new(item) { Id = id++ };
                    result.ViewResults.Add(poiResultCIExyuvData);
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmPoi), ImageFilePath = result.FilePath })) });
            }
        }
        public override void Handle(AlgorithmView view, ViewResultAlg result)
        {

            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();


            List<string> header = new List<string> { "Id", Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Shape, Properties.Resources.Size, "CCT", "Wave", "X", "Y", "Z", "u'", "v", "x", "y", "Validate" };
            if (result.ResultType == ViewResultAlgType.LEDStripDetection)
            {
                header = new List<string> { "Id", Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Shape };
            }

            List<string> bdHeader = new List<string> { "Id", "Name", "PixelPos", "Shapes", "PixelSize", "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y", "POIPointResultModel.ValidateResult" };


            if (result.ViewResults.Count <= 4000)
            {
                foreach (var poiResultCIExyuvData in result.ViewResults.ToSpecificViewResults<PoiResultCIExyuvData>())
                {
                    var item = poiResultCIExyuvData.Point;
                    switch (item.PointType)
                    {
                        case POIPointTypes.Circle:
                            DVCircleText Circle = new DVCircleText();
                            Circle.Attribute.Center = new Point(item.PixelX, item.PixelY);
                            Circle.Attribute.Radius = item.Radius;
                            Circle.Attribute.Brush = Brushes.Transparent;
                            Circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                            Circle.Attribute.Id = item.Id ?? -1;
                            Circle.Attribute.Text = item.Name;
                            Circle.Attribute.Msg = PoiImageViewComponent.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);
                            Circle.Render();
                            view.ImageView.AddVisual(Circle);
                            break;
                        case POIPointTypes.Rect:
                            DVRectangleText Rectangle = new DVRectangleText();
                            Rectangle.Attribute.Rect = new Rect(item.PixelX - item.Width / 2, item.PixelY - item.Height / 2, item.Width, item.Height);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                            Rectangle.Attribute.Id = item.Id ?? -1;
                            Rectangle.Attribute.Text = item.Name;
                            Rectangle.Attribute.Msg = PoiImageViewComponent.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);
                            Rectangle.Render();
                            view.ImageView.AddVisual(Rectangle);
                            break;
                        default:
                            break;
                    }
                }
            }
            else
            {
                log.Info($"result.ViewResults.Count:{result.ViewResults.Count}");
            }


            if (view.listViewSide.View is GridView gridView)
            {
                view.listViewSide.ItemsSource = null;
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.listViewSide.ItemsSource = result.ViewResults;
            }
        }
    }
}
