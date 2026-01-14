#pragma warning disable CS8602

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.ImageEditor.Draw;
using ColorVision.Solution.Editor.AvalonEditor;
using log4net;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Jsons.LEDStripDetectionV2
{
    /// <summary>
    /// 单个端点坐标
    /// </summary>
    public class EndPoint
    {
        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }
    }

    /// <summary>
    /// 单个检测结果条目
    /// </summary>
    public class LEDStripPointResult
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("physicalLength")]
        public double PhysicalLength { get; set; }

        [JsonProperty("pixLength")]
        public double PixLength { get; set; }

        [JsonProperty("endPoint")]
        public List<EndPoint> EndPoints { get; set; }
    }

    /// <summary>
    /// LED灯条检测V2的结果集
    /// </summary>
    public class LEDStripDetectionV2Result
    {
        [JsonProperty("result")]
        public List<LEDStripPointResult> Result { get; set; }
    }

    public class LEDStripDetailViewResult : IViewResult
    {
        public DetailCommonModel DetailCommonModel { get; set; }

        public LEDStripDetectionV2Result LEDStripResult { get; set; }
        public string ResultFileName { get; set; }

        public LEDStripDetailViewResult() { }

        public LEDStripDetailViewResult(DetailCommonModel detailCommonModel)
        {
            DetailCommonModel = detailCommonModel;

            var restfile = JsonConvert.DeserializeObject<ResultFile>(detailCommonModel.ResultJson);
            ResultFileName = restfile?.ResultFileName;

            if (File.Exists(ResultFileName))
            {
                LEDStripResult = JsonConvert.DeserializeObject<LEDStripDetectionV2Result>(File.ReadAllText(ResultFileName));
            }
        }
    }


    public class ViewHandleLEDStripDetectionV2 : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleLEDStripDetectionV2));

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.LEDStripDetection};
        public override bool CanHandle1(ViewResultAlg result)
        {
            if (result.Version != "2.0") return false;
            return base.CanHandle1(result);
        }

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string filePath = Path.Combine(selectedPath, $"{result.Batch}{result.ResultType}_LEDStripV2.csv");

            var viewResults = result.ViewResults.ToSpecificViewResults<LEDStripDetailViewResult>();
            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("name,physicalLength,pixLength,x1,y1,x2,y2");
            if (viewResults.Count == 1)
            {
                var items = viewResults[0].LEDStripResult?.Result;
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        csvBuilder.AppendLine($"{item.Name},{item.PhysicalLength},{item.PixLength},{item.EndPoints[0].X},{item.EndPoints[0].Y},{item.EndPoints[1].X},{item.EndPoints[1].Y}");
                    }
                }
                File.AppendAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
            }
        }


        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(result.Id);
                if (detailCommonModels.Count == 1)
                {
                    var ledResult = new LEDStripDetailViewResult(detailCommonModels[0]);
                    result.ViewResults.Add(ledResult);

                    RelayCommand SelectrelayCommand = new RelayCommand(a =>
                    {
                        PlatformHelper.OpenFolderAndSelectFile(ledResult.ResultFileName);
                    }, a => File.Exists(ledResult.ResultFileName));

                    RelayCommand OpenrelayCommand = new RelayCommand(a =>
                    {
                        AvalonEditWindow avalonEditWindow = new AvalonEditWindow(ledResult.ResultFileName) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                        avalonEditWindow.ShowDialog();
                    }, a => File.Exists(ledResult.ResultFileName));

                    result.ContextMenu.Items.Add(new MenuItem() { Header = "选中2.0结果集", Command = SelectrelayCommand });
                    result.ContextMenu.Items.Add(new MenuItem() { Header = "打开2.0结果集", Command = OpenrelayCommand });
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmLEDStripDetectionV2), ImageFilePath = result.FilePath })) });
            }
        }
         
        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);


            if (result.ViewResults.Count == 1)
            {
                if (result.ViewResults[0] is LEDStripDetailViewResult ledResult)
                {
                    int id = 0;
                    if (ledResult.LEDStripResult?.Result?.Count > 0)
                    {
                        foreach (var item in ledResult.LEDStripResult.Result)
                        {
                            id++;
                            // 绘制两个端点之间的直线
                            var ep1 = item.EndPoints[0];
                            var ep2 = item.EndPoints[1];

                            LineProperties lineProperties = new LineProperties();
                            lineProperties.Points.Add(new Point(ep1.X, ep1.Y));
                            lineProperties.Points.Add(new Point(ep2.X, ep2.Y));
                            lineProperties.Pen = new Pen(Brushes.Red, 1 / ctx.ImageView.Zoombox1.ContentMatrix.M11);

                            var line = new DVLine(lineProperties);
                            line.Render();
                            ctx.ImageView.AddVisual(line);
                        }
                    }

                    List<string> header = new() { "name", "physicalLength", "pixLength", "x1", "y1", "x2", "y2" };
                    if (ctx.ListView.View is GridView gridView)
                    {
                        ctx.LeftGridViewColumnVisibilitys.Clear();
                        gridView.Columns.Clear();
                        foreach (var h in header)
                            gridView.Columns.Add(new GridViewColumn() { Header = h, DisplayMemberBinding = new Binding(h) });

                        // 适配显示用的行对象
                        var displayList = new List<dynamic>();
                        foreach (var item in ledResult.LEDStripResult.Result)
                        {
                            displayList.Add(new
                            {
                                name = item.Name,
                                physicalLength = item.PhysicalLength,
                                pixLength = item.PixLength,
                                x1 = item.EndPoints[0].X,
                                y1 = item.EndPoints[0].Y,
                                x2 = item.EndPoints[1].X,
                                y2 = item.EndPoints[1].Y
                            });
                        }
                        ctx.ListView.ItemsSource = displayList;
                    }
                }
            }



        }



    }
}
