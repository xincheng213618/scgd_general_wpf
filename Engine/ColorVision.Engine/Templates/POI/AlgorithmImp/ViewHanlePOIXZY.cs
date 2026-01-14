using ColorVision.Common.MVVM;
using ColorVision.Engine.Media;
using ColorVision.Database;
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
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.POI_XYZ};
        
        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string fileName = Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var PoiResultCIExyuvDatas = result.ViewResults.ToSpecificViewResults<PoiResultCIExyuvData>();
            PoiResultCIExyuvDatas.SaveCsv(fileName);
        }
        public override void Load(ViewResultContext ctx, ViewResultAlg result)
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
        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {

            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);

            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();


            List<string> header = new List<string> { "Id", Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Shape, Properties.Resources.Size, "CCT", "Wave", "X", "Y", "Z", "u'", "v", "x", "y" };
            if (result.ResultType == ViewResultAlgType.LEDStripDetection)
            {
                header = new List<string> { "Id", Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Shape };
            }

            List<string> bdHeader = new List<string> { "Id", "Name", "PixelPos", "Shapes", "PixelSize", "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y" };


            if (result.ViewResults.Count <= 4000)
            {
                foreach (var poiResultCIExyuvData in result.ViewResults.ToSpecificViewResults<PoiResultCIExyuvData>())
                {
                    var item = poiResultCIExyuvData.Point;
                    switch (item.PointType)
                    {
                        case POIPointTypes.Circle:
                            CircleTextProperties circleTextProperties = new CircleTextProperties();
                            circleTextProperties.Center = new Point(item.PixelX, item.PixelY);
                            circleTextProperties.Radius = item.Radius;
                            circleTextProperties.Brush = Brushes.Transparent;
                            circleTextProperties.Pen = new Pen(Brushes.Red, 1);
                            circleTextProperties.Id = item.Id ?? -1;
                            circleTextProperties.Text = item.Name;
                            circleTextProperties.Msg = CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);

                            DVCircleText Circle = new DVCircleText(circleTextProperties);
                            Circle.Render();
                            ctx.ImageView.AddVisual(Circle);
                            break;
                        case POIPointTypes.Rect:
                            RectangleTextProperties rectangleTextProperties = new RectangleTextProperties();
                            rectangleTextProperties.Rect = new Rect(item.PixelX - item.Width / 2, item.PixelY - item.Height / 2, item.Width, item.Height);
                            rectangleTextProperties.Brush = Brushes.Transparent;
                            rectangleTextProperties.Pen = new Pen(Brushes.Red, 1);
                            rectangleTextProperties.Id = item.Id ?? -1;
                            rectangleTextProperties.Text = item.Name;
                            rectangleTextProperties.Msg = CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);

                            DVRectangleText Rectangle = new DVRectangleText(rectangleTextProperties);
                            Rectangle.Render();
                            ctx.ImageView.AddVisual(Rectangle);
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


            if (ctx.ListView.View is GridView gridView)
            {
                ctx.ListView.ItemsSource = null;
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                ctx.ListView.ItemsSource = result.ViewResults;
            }
        }
    }
}
