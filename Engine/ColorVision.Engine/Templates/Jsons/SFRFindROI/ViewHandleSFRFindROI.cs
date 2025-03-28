#pragma warning disable CS8602

using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.Ghost;
using ColorVision.Engine.Templates.MTF;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
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

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',' ) || field.Contains('"') || field.Contains('\n'))
            {
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }
            return field;
        }

        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            string fileName = System.IO.Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var ViewResults = result.ViewResults.ToSpecificViewResults<ViewResultMTF>();

            var csvBuilder = new StringBuilder();
            List<string> properties = new() { "Id", "Name", "位置", "大小", "形状", "MTF" };
            csvBuilder.AppendLine(string.Join(",", properties));

            foreach (var item in ViewResults)
            {
                List<string> values = new()
        {
            item.Point.Id.ToString() ?? string.Empty,
            item.Name,
            $"{item.Point.PixelX}|{item.Point.PixelY}",
            $"{item.Point.Width}|{item.Point.Height}",
            item.Shapes.ToString(),
            item.Articulation.ToString()
        };
                csvBuilder.AppendLine(string.Join(",", values));
            }

            // Statistical calculations
            var maxValues = new Dictionary<string, double>();
            var minValues = new Dictionary<string, double>();
            var sumValues = new Dictionary<string, double>();
            var maxNames = new Dictionary<string, string>();
            var minNames = new Dictionary<string, string>();
            var count = ViewResults.Count;

            foreach (var property in properties.Skip(4)) // Assuming the first few properties are non-numeric
            {
                maxValues[property] = double.MinValue;
                minValues[property] = double.MaxValue;
                sumValues[property] = 0.0;
                maxNames[property] = string.Empty;
                minNames[property] = string.Empty;

                foreach (var item in ViewResults)
                {
                    if (typeof(ViewResultMTF).GetProperty(property)?.GetValue(item) is double value)
                    {
                        if (value > maxValues[property])
                        {
                            maxValues[property] = value;
                            maxNames[property] = item.Name ?? item.Point.Id.ToString();
                        }
                        if (value < minValues[property])
                        {
                            minValues[property] = value;
                            minNames[property] = item.Name ?? item.Point.Id.ToString();
                        }
                        sumValues[property] += value;
                    }
                }
            }

            var meanValues = sumValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / count);
            var varianceValues = new Dictionary<string, double>();

            foreach (var property in properties.Skip(4))
            {
                varianceValues[property] = 0.0;
                foreach (var item in ViewResults)
                {
                    if (typeof(ViewResultMTF).GetProperty(property)?.GetValue(item) is double value)
                    {
                        varianceValues[property] += Math.Pow(value - meanValues[property], 2);
                    }
                }
                varianceValues[property] /= count;
            }

            csvBuilder.AppendLine("\n统计信息");
            csvBuilder.AppendLine("属性,最大值,最大值所在名称,最小值,最小值所在名称,平均值,方差,均匀性");

            foreach (var property in properties.Skip(4))
            {
                double uniformity = (maxValues[property] != 0) ? (minValues[property] / maxValues[property]) * 100 : 0;

                List<string> stats = new()
        {
            property,
            maxValues[property].ToString(CultureInfo.InvariantCulture),
            maxNames[property],
            minValues[property].ToString(CultureInfo.InvariantCulture),
            minNames[property],
            meanValues[property].ToString(CultureInfo.InvariantCulture),
            varianceValues[property].ToString(CultureInfo.InvariantCulture),
            uniformity.ToString("F2", CultureInfo.InvariantCulture)
        };
                csvBuilder.AppendLine(string.Join(",", stats));
            }

            File.WriteAllText(fileName, csvBuilder.ToString(), Encoding.UTF8);

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

            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<PoiPointResultModel> AlgResultMTFModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultMTFModels)
                {
                    ViewResultMTF mTFResultData = new(item);
                    result.ViewResults.Add(mTFResultData);
                }
            }

            view.ImageView.ImageShow.Clear();

            foreach (var item in result.ViewResults)
            {
                if (item is ViewResultMTF poiResultData)
                {
                    switch (poiResultData.Point.PointType)
                    {
                        case POIPointTypes.Circle:
                            DVCircleText Circle = new();
                            Circle.Attribute.Center = new Point(poiResultData.Point.PixelX, poiResultData.Point.PixelY);
                            Circle.Attribute.Radius = poiResultData.Point.Height / 2;
                            Circle.Attribute.Brush = Brushes.Transparent;
                            Circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                            Circle.Attribute.Id = poiResultData.Id;
                            Circle.Attribute.Text = poiResultData.Name;
                            Circle.Attribute.Msg = poiResultData.Articulation.ToString();
                            Circle.Render();
                            view.ImageView.AddVisual(Circle);
                            break;
                        case POIPointTypes.Rect:
                            DVRectangleText Rectangle = new();
                            Rectangle.Attribute.Rect = new Rect(poiResultData.Point.PixelX - poiResultData.Point.Width / 2, poiResultData.Point.PixelY - poiResultData.Point.Height / 2, poiResultData.Point.Width, poiResultData.Point.Height);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                            Rectangle.Attribute.Id = poiResultData.Id;
                            Rectangle.Attribute.Text = poiResultData.Name;
                            Rectangle.Attribute.Msg = poiResultData.Articulation.ToString();
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
            header = new() { "Name", "位置", "大小", "形状", "MTF" };
            bdHeader = new() { "Name", "PixelPos", "PixelSize", "Shapes", "Articulation" };


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
