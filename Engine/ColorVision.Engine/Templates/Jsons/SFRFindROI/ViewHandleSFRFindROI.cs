#pragma warning disable CS8602,CS8601,CS8629
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ColorVision.Engine.Services;
using ColorVision.ImageEditor;

namespace ColorVision.Engine.Templates.Jsons.SFRFindROI
{
    public class Point1
    {
        public double X { get; set; }
        public double Y { get; set; }

        public override string ToString()
        {
            return $"X:{X},Y:{Y}";
        }
    }

    public class SFRFindROIValue
    {
        [JsonProperty("angle")]
        public double Angle { get; set; }
        [JsonProperty("center")]
        public Point1 Center { get; set; }
    }


    public class ViewSFRFindROI : IViewResult
    {

        public ViewSFRFindROI(PoiPointResultModel poiPointResultModel)
        {
            Id = poiPointResultModel.Id;
            Pid = poiPointResultModel.Pid;
            PoiId = poiPointResultModel.PoiId;
            PoiName = poiPointResultModel.PoiName;
            PoiType = poiPointResultModel.PoiType;
            PoiX = poiPointResultModel.PoiX;
            PoiY = poiPointResultModel.PoiY;
            PoiWidth = poiPointResultModel.PoiWidth;
            PoiHeight = poiPointResultModel.PoiHeight;
            Value = poiPointResultModel.Value == null ? new SFRFindROIValue() : JsonConvert.DeserializeObject<SFRFindROIValue>(poiPointResultModel.Value);
        }

        public int Id { get; set; }
        public int? Pid { get; set; }
        public int? PoiId { get; set; }
        public string? PoiName { get; set; }
        public POIPointTypes PoiType { get; set; }
        public int? PoiX { get; set; }
        public int? PoiY { get; set; }
        public int? PoiWidth { get; set; }
        public int? PoiHeight { get; set; }

        public SFRFindROIValue Value { get; set; }


    }

    public class ViewHandleSFRFindROI : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.ARVR_SFR_FindROI };

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string fileName = System.IO.Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var ViewResults = result.ViewResults.ToSpecificViewResults<ViewSFRFindROI>();

            var csvBuilder = new StringBuilder();
            List<string> properties = new() { "Id", "Name", "位置", "大小", "形状","Angle","Center"};
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
                    item.Value.Angle.ToString(),
                    $"{item.Value.Center.X}|{item.Value.Center.Y}",
                };
                csvBuilder.AppendLine(string.Join(",", values));
            }


            File.WriteAllText(fileName, csvBuilder.ToString(), Encoding.UTF8);
        }

        public override void Load(IViewImageA view, ViewResultAlg result)
        {
            if (result.ViewResults ==null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                foreach (var item in PoiPointResultDao.Instance.GetAllByPid(result.Id))
                    result.ViewResults.Add(new ViewSFRFindROI(item));

                void ExportToPoi()
                {
                    int old1 = TemplatePoi.Params.Count;
                    TemplatePoi templatePoi1 = new TemplatePoi();
                    templatePoi1.ImportTemp = new PoiParam() { Name = templatePoi1.NewCreateFileName("poi") };
                    templatePoi1.ImportTemp.Height = 400;
                    templatePoi1.ImportTemp.Width = 300;
                    templatePoi1.ImportTemp.PoiConfig.BackgroundFilePath = result.FilePath;
                    foreach (var item in result.ViewResults.ToSpecificViewResults<ViewSFRFindROI>())
                    {
                        PoiPoint poiPoint = new PoiPoint()
                        {
                            Name = item.PoiName,
                            PixX = (double)item.PoiX,
                            PixY = (double)item.PoiY,
                            PixHeight = (double)item.PoiHeight,
                            PixWidth = (double)item.PoiWidth,
                            PointType = (GraphicTypes)item.PoiType,
                            Id = -1
                        };
                        templatePoi1.ImportTemp.PoiPoints.Add(poiPoint);
                    }


                    templatePoi1.OpenCreate();
                    int next1 = TemplatePoi.Params.Count;
                    if (next1 == old1 + 1)
                    {
                        new EditPoiParam(TemplatePoi.Params[next1 - 1].Value).ShowDialog();
                    }
                }
                RelayCommand ExportToPoiCommand = new RelayCommand(a => ExportToPoi());
                result.ContextMenu.Items.Add(new MenuItem() { Header = "创建到POI", Command = ExportToPoiCommand });
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmSFRFindROI), ImageFilePath = result.FilePath })) });
            }

        }

        public override void Handle(IViewImageA view, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            foreach (var item in result.ViewResults)
            {
                if (item is ViewSFRFindROI poiResultData)
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
                            Circle.Attribute.Msg = $"Angle:{poiResultData.Value.Angle}{Environment.NewLine}";
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
                            Rectangle.Attribute.Msg = $"Angle:{poiResultData.Value.Angle}{Environment.NewLine}";
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
            header = new() { "Name", "PoiX", "PoiY", "PoiWidth", "PoiHeight", "形状" ,"angle","Center"};
            bdHeader = new() { "PoiName", "PoiX", "PoiY", "PoiWidth", "PoiHeight", "PoiType", "Value.Angle", "Value.Center" };

            if (view.ListView.View is GridView gridView)
            {
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.ListView.ItemsSource = result.ViewResults;
            }
        }



        }
}
