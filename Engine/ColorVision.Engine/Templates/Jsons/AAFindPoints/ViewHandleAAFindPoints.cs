#pragma warning disable CS8602

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.ImageEditor.Draw;
using ColorVision.Solution.Editor.AvalonEditor;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Jsons.AAFindPoints
{
    public class PointLocation
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("x")]
        public int X { get; set; }
        [JsonProperty("y")]
        public int Y { get; set; }
    }

    public class PointsData
    {
        [JsonProperty("location")]
        public List<PointLocation> Location { get; set; } = new List<PointLocation>();
        [JsonProperty("nummber_x")]
        public int NumberX { get; set; }
        [JsonProperty("nummber_y")]
        public int NumberY { get; set; }
    }

    public class Corner
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("x")]
        public int X { get; set; }
        [JsonProperty("y")]
        public int Y { get; set; }
    }

    public class AAFindPoint
    {
        [JsonProperty(nameof(Points))]
        public PointsData? Points { get; set; }
        [JsonProperty("corner")]
        public List<Corner> Corner { get; set; } = new List<Corner>();
    }


    public class AAFindPointsViewReslut : IViewResult
    {
        public DetailCommonModel DetailCommonModel { get; set; }

        public AAFindPointsViewReslut()
        {

        }

        public AAFindPointsViewReslut(DetailCommonModel detailCommonModel)
        {
            DetailCommonModel = detailCommonModel;

            var restfile = JsonConvert.DeserializeObject<ResultFile>(detailCommonModel.ResultJson);
            ResultFileName = restfile?.ResultFileName;

            if (File.Exists(ResultFileName))
            {
                AAFindPoint = JsonConvert.DeserializeObject<AAFindPoint>(File.ReadAllText(ResultFileName));
            }

        }
        public string? ResultFileName { get; set; }

        public AAFindPoint? AAFindPoint { get; set; }
    }


    public class ViewHandleAAFindPoints : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleAAFindPoints));

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.ARVR_AAFindPoints };
        public override bool CanHandle1(ViewResultAlg result)
        {
            if (result.Version != "AA") return false;
            return base.CanHandle1(result);
        }


        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string fileName = System.IO.Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            //var ViewResults = result.ViewResults.ToSpecificViewResults<ViewHandleAAFindPoints>();

            //var csvBuilder = new StringBuilder();

            //if (ViewResults.Count == 1)
            //{
            //    string filePath = selectedPath + "//" + mtfresult.Batch + mtfresult.ResultType + ".json";
            //    File.AppendAllText(filePath, blackMuraViews[0].Result, Encoding.UTF8);
            //}
        }


        public override void Load(IViewImageA view, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(result.Id);
                if (detailCommonModels.Count == 1)
                {
                    AAFindPointsViewReslut mtfresult = new AAFindPointsViewReslut(detailCommonModels[0]);
                    result.ViewResults.Add(mtfresult);

                    RelayCommand SelectrelayCommand = new RelayCommand(a =>
                    {
                        PlatformHelper.OpenFolderAndSelectFile(mtfresult.ResultFileName);

                    }, a => !string.IsNullOrEmpty(mtfresult.ResultFileName) && File.Exists(mtfresult.ResultFileName));

                    RelayCommand OpenrelayCommand = new RelayCommand(a =>
                    {
                        if (mtfresult.ResultFileName != null)
                        {
                            AvalonEditWindow avalonEditWindow = new AvalonEditWindow(mtfresult.ResultFileName) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                            avalonEditWindow.ShowDialog();
                        }
                    }, a => !string.IsNullOrEmpty(mtfresult.ResultFileName) && File.Exists(mtfresult.ResultFileName));


                    result.ContextMenu.Items.Add(new MenuItem() { Header = "选中2.0结果集", Command = SelectrelayCommand });
                    result.ContextMenu.Items.Add(new MenuItem() { Header = "打开2.0结果集", Command = OpenrelayCommand });
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmAAFindPoints), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(IViewImageA view, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults.FirstOrDefault() is AAFindPointsViewReslut viewResult)
            {

                DVDatumPolygon Polygon = new() { IsComple = true };
                Polygon.Attribute.Pen = new Pen(Brushes.Blue, 1 / view.ImageView.Zoombox1.ContentMatrix.M11);
                Polygon.Attribute.Brush = Brushes.Transparent;

                foreach (var item in viewResult.AAFindPoint.Corner)
                {
                    Polygon.Attribute.Points.Add(new Point() { X =item.X,Y= item.Y});
                }
                Polygon.IsComple = true;
                Polygon.Render();
                view.ImageView.AddVisual(Polygon);

                List<string> header = new() { "ID", "X", "Y" };
                List<string> bdHeader = new() { "Id", "X", "Y" };
                if (view.ListView.View is GridView gridView)
                {
                    view.LeftGridViewColumnVisibilitys.Clear();
                    gridView.Columns.Clear();
                    for (int i = 0; i < header.Count; i++)
                        gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });

                    view.ListView.ItemsSource = new ObservableCollection<Corner>(viewResult.AAFindPoint.Corner);
                }
            }
            else
            {
                if (view.ListView.View is GridView gridView)
                {
                    gridView.Columns.Clear();
                }
                view.ListView.ItemsSource = null;
            }
        }
    }
}