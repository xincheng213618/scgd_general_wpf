#pragma warning disable CS8602

using ColorVision.Common.MVVM;
using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.ImageEditor.Draw;
using log4net;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Jsons.BlackMura
{


    public class FloatPoint
    {
        public double X { get; set; }
        public double Y { get; set; }

         public Point ToPoint()
        {
            return new Point(X, Y);
        }
    }



    public class ViewHandleBlackMura : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleBlackMura));

        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.BlackMura_Caculate};

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
            var ViewResults = result.ViewResults.ToSpecificViewResults<BlackMuraModel>();
            var csvBuilder = new StringBuilder();

            List<BlackMuraView> blackMuraViews = new List<BlackMuraView>();
            foreach (var item in ViewResults)
            {
                blackMuraViews.Add(new BlackMuraView(item));
            }

            List<string> header = new List<string>();
            var properties = typeof(BlackMuraView).GetProperties();

            // 递归构建头部
            foreach (var prop in properties)
            {
                var columnName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
                if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                {
                    var nestedProperties = prop.PropertyType.GetProperties();
                    foreach (var nestedProp in nestedProperties)
                    {
                        var nestedColumnName = $"{nestedProp.Name}";
                        header.Add(nestedColumnName);
                    }
                }
                else
                {
                    header.Add(columnName);
                }
            }

            csvBuilder.AppendLine(string.Join(",", header));

            foreach (var item in blackMuraViews)
            {
                List<string> content = new List<string>();
                foreach (var prop in properties)
                {
                    var value = prop.GetValue(item);
                    if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                    {
                        var nestedProperties = prop.PropertyType.GetProperties();
                        foreach (var nestedProp in nestedProperties)
                        {
                            var nestedValue = nestedProp.GetValue(value);
                            content.Add(EscapeCsvField(nestedValue?.ToString() ?? string.Empty));
                        }
                    }
                    else
                    {
                        content.Add(EscapeCsvField(value?.ToString() ?? string.Empty));
                    }
                }
                csvBuilder.AppendLine(string.Join(",", content));
            }

            File.WriteAllText(selectedPath + "//" + result.Batch + ".csv", csvBuilder.ToString(), Encoding.UTF8);

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

            void OpenSource()
            {
                view.ImageView.ImageShow.Clear();
                foreach (var item in result.ViewResults)
                {
                    if (item is BlackMuraModel blackMuraModel)
                    {
                        if (File.Exists(result.FilePath))
                            view.ImageView.OpenImage(result.FilePath);
                        log.Info(result.FilePath);

                        if (!string.IsNullOrEmpty(blackMuraModel.AreaJsonVal))
                        {
                            List<FloatPoint> floatPoints = JsonConvert.DeserializeObject<List<FloatPoint>>(blackMuraModel.AreaJsonVal);
                            log.Info(floatPoints.Count);
                            DVPolygon dVPolygon = new DVPolygon();
                            dVPolygon.Attribute.Brush = Brushes.Transparent;
                            dVPolygon.Attribute.Pen = new Pen(Brushes.Red, 1);
                            foreach (var item1 in floatPoints)
                            {
                                dVPolygon.Points.Add(item1.ToPoint());
                            }
                            dVPolygon.IsComple = true;
                            view.ImageView.AddVisual(dVPolygon);
                            log.Info(dVPolygon);
                        }

                    }
                }
            }


            void OpenAA()
            {
                view.ImageView.ImageShow.Clear();
                foreach (var item in result.ViewResults)
                {
                    if (item is BlackMuraModel blackMuraModel)
                    {
                        Outputfile outputfile = JsonConvert.DeserializeObject<Outputfile>(blackMuraModel.OutputFile);
                        if (File.Exists(outputfile.AAPath))
                            view.ImageView.OpenImage(outputfile.AAPath);


                        ResultJson lvDetails = JsonConvert.DeserializeObject<ResultJson>(blackMuraModel.ResultJson);
                        DVCircleText maxcirle = new();
                        maxcirle.Attribute.Center = new System.Windows.Point(lvDetails.MaxPtX, lvDetails.MaxPtY);
                        maxcirle.Attribute.Radius = lvDetails.Nle;
                        maxcirle.Attribute.Brush = Brushes.Transparent;
                        maxcirle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        maxcirle.Attribute.Id = -1;
                        maxcirle.Attribute.Text = string.Empty;
                        maxcirle.Attribute.Msg = $"Max({lvDetails.MaxPtX},{lvDetails.MaxPtY}) Lv:{lvDetails.LvMax}";
                        maxcirle.Render();
                        view.ImageView.AddVisual(maxcirle);

                        DVCircleText mincirle = new();
                        mincirle.Attribute.Center = new System.Windows.Point(lvDetails.MinPtX, lvDetails.MinPtY);
                        mincirle.Attribute.Radius = lvDetails.Nle;
                        mincirle.Attribute.Brush = Brushes.Transparent;
                        mincirle.Attribute.Pen = new Pen(Brushes.Yellow, 1);
                        mincirle.Attribute.Id = -1;
                        mincirle.Attribute.Text = string.Empty;
                        mincirle.Attribute.Msg = $"Min({lvDetails.MinPtX},{lvDetails.MinPtY}) Lv:{lvDetails.LvMin}";
                        mincirle.Render();
                        view.ImageView.AddVisual(mincirle);
                    }
                }
            }


            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<BlackMuraModel> AlgResultModels = BlackMuraDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultModels)
                {
                    result.ViewResults.Add(item);
                    var ContextMenu = result.ContextMenu;
                    RelayCommand relayCommand = new RelayCommand(a => OpenSource());
                    ContextMenu.Items.Add(new MenuItem() { Header = "切分示意图", Command = relayCommand });
                    RelayCommand relayCommand1 = new RelayCommand(a => OpenAA());
                    ContextMenu.Items.Add(new MenuItem() { Header = "AA区", Command = relayCommand1 });
                }
            }

            OpenSource();


            List<string> header = new() { "ResultJson", "UniformityJson", "OutputFile", "AreaJsonVal" };
            List<string> bdHeader = new() { "ResultJson", "UniformityJson", "OutputFile", "AreaJsonVal" };

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
