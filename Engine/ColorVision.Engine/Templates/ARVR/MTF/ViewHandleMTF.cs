#pragma warning disable CA1725,CS8601
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.MTF
{
    public class ViewHandleMTF : IResultHandleBase
    {
        public override string Name => "MTF(ARVR)";

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.MTF };

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string fileName = System.IO.Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var ViewResults = result.ViewResults.ToSpecificViewResults<ViewResultMTF>();

            var csvBuilder = new StringBuilder();
            List<string> properties = new() { "Id", Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Size, Properties.Resources.Shape, "MTF" };
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

            csvBuilder.AppendLine($"\n{Properties.Resources.Statistics}");
            csvBuilder.AppendLine(Properties.Resources.MtfStatisticsCsvHeader);

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



        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<PoiPointResultModel> AlgResultMTFModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultMTFModels)
                {
                    ViewResultMTF mTFResultData = new(item);
                    result.ViewResults.Add(mTFResultData);
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Debug, Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmMTF), ImageFilePath = result.FilePath })) });

            }
        }


        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);

            foreach (ViewResultMTF poiResultData in result.ViewResults.OfType<ViewResultMTF>())
                PoiOverlayRenderer.Add(ctx.ImageView, poiResultData.Point, FormatNumber(poiResultData.Articulation));


            List<string> header;
            List<string> bdHeader;
            header = new() { Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Size, Properties.Resources.Shape, "MTF" };
            bdHeader = new() { "Name", "PixelPos", "PixelSize", "Shapes", "Articulation" };


            if (ctx.ListView.View is GridView gridView)
            {
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                ctx.ListView.ItemsSource = result.ViewResults;
            }
        }


    }

}
