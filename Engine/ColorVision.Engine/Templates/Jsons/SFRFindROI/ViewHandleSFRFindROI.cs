#pragma warning disable CS8602,CS8601,CS8629

using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Jsons.SFRFindROI
{
    public class ViewHandleSFRFindROI : IResultHandleBase
    {
        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.ARVR_SFR_FindROI };

        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            string fileName = System.IO.Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var ViewResults = result.ViewResults.ToSpecificViewResults<PoiPointResultModel>();

            var csvBuilder = new StringBuilder();
            List<string> properties = new() { "Id", "Name", "位置", "大小", "形状"};
            csvBuilder.AppendLine(string.Join(",", properties));

            foreach (var item in ViewResults)
            {
                List<string> values = new()
                {
                    item.Id.ToString() ?? string.Empty,
                    item.PoiName?? string.Empty,
                    $"{item.PoiX}|{item.PoiY}",
                    $"{item.PoiWidth}|{item.PoiHeight}",
                    item.PoiType.ToString(),
                };
                csvBuilder.AppendLine(string.Join(",", values));
            }


            File.WriteAllText(fileName, csvBuilder.ToString(), Encoding.UTF8);
        }

        public override void Load(AlgorithmResult result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>(PoiPointResultDao.Instance.GetAllByPid(result.Id));
            }
        }

        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {
            view.ImageView.ImageShow.Clear();
            if (result.ResultCode != 0)
            {
                if (File.Exists(result.FilePath))
                    view.ImageView.OpenImage(result.FilePath);
                return;
            }
            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            Load(result);

            view.ImageView.ImageShow.Clear();

            foreach (var item in result.ViewResults)
            {
                if (item is PoiPointResultModel poiResultData)
                {
                    switch (poiResultData.PoiType)
                    {
                        case POIPointTypes.Circle:
                            DVCircleText Circle = new();
                            Circle.Attribute.Center = new Point((double)poiResultData.PoiX, (double)poiResultData.PoiY);
                            Circle.Attribute.Radius = (double)poiResultData.PoiHeight / 2;
                            Circle.Attribute.Brush = Brushes.Transparent;
                            Circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                            Circle.Attribute.Id = poiResultData.Id;
                            Circle.Attribute.Text = poiResultData.PoiName;
                            Circle.Render();
                            view.ImageView.AddVisual(Circle);
                            break;
                        case POIPointTypes.Rect:
                            DVRectangleText Rectangle = new();
                            Rectangle.Attribute.Rect = new Rect((double)poiResultData.PoiX - (double)poiResultData.PoiWidth / 2, (double)poiResultData.PoiY - (double)poiResultData.PoiHeight / 2, (double)poiResultData.PoiWidth, (double)poiResultData.PoiHeight);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                            Rectangle.Attribute.Id = poiResultData.Id;
                            Rectangle.Attribute.Text = poiResultData.PoiName;
                            Rectangle.Render();
                            view.ImageView.AddVisual(Rectangle);
                            break;
                        default:
                            break;
                    }
                }
            }


            List<string> header;
            List<string> bdHeader;
            header = new() { "Name", "PoiX", "PoiY", "PoiWidth", "PoiHeight", "形状"};
            bdHeader = new() { "PoiName", "PoiX", "PoiY", "PoiWidth", "PoiHeight", "PoiType" };

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
