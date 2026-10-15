#pragma warning disable CA1725,CS8602

using ColorVision.Common.MVVM;
using ColorVision.Common.Algorithms;
using ColorVision.Engine.Services.Results.LightArea;

using ColorVision.Engine.Services;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.ImageEditor.Draw;
using ColorVision.Solution.Editor.AvalonEditor;
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

        [JsonIgnore]
        public List<AlgResultLightAreaModel> LightAreaModels => AAFindPoint?.Corner?.Select(corner => new AlgResultLightAreaModel
        {
            Id = corner.Id,
            Pid = DetailCommonModel?.PId ?? 0,
            PosX = corner.X,
            PosY = corner.Y
        }).ToList() ?? new List<AlgResultLightAreaModel>();
    }


    public class ViewHandleAAFindPoints : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleAAFindPoints));

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.ARVR_AAFindPoints, ViewResultAlgType.FindLightArea };
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


        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ResultType == ViewResultAlgType.FindLightArea)
            {
                result.ViewResults ??= new ObservableCollection<IViewResult>(AlgResultLightAreaDao.Instance.GetAllByPid(result.Id));
                return;
            }

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


                    result.ContextMenu.Items.Add(new MenuItem() { Header = string.Format(Properties.Resources.SelectResultSetFormat, "2.0"), Command = SelectrelayCommand });
                    result.ContextMenu.Items.Add(new MenuItem() { Header = string.Format(Properties.Resources.OpenResultSetFormat, "2.0"), Command = OpenrelayCommand });
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Debug, Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmAAFindPoints), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            OpenSourceImage(ctx, result);

            if (result.ResultType == ViewResultAlgType.FindLightArea)
            {
                HandlePersistedCorners(ctx, result);
                return;
            }

            if (result.ViewResults.FirstOrDefault() is AAFindPointsViewReslut viewResult && viewResult.AAFindPoint != null)
            {
                List<AlgResultLightAreaModel> corners = viewResult.LightAreaModels;

                DVDatumPolygon Polygon = new() { IsComple = true };
                Polygon.Attribute.Pen = new Pen(Brushes.Blue, 1 / ctx.ImageView.Zoombox1.ContentMatrix.M11);

                foreach (var item in corners)
                {
                    Polygon.Attribute.Points.Add(new Point(item.PosX, item.PosY));
                }
                Polygon.IsComple = true;
                Polygon.Render();
                ctx.ImageView.AddVisual(Polygon);

                List<string> header = new() { "ID", "X", "Y" };
                List<string> bdHeader = new() { nameof(AlgResultLightAreaModel.Id), nameof(AlgResultLightAreaModel.PosX), nameof(AlgResultLightAreaModel.PosY) };
                if (ctx.ListView.View is GridView gridView)
                {
                    ctx.LeftGridViewColumnVisibilitys.Clear();
                    gridView.Columns.Clear();
                    for (int i = 0; i < header.Count; i++)
                        gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });

                    ctx.ListView.ItemsSource = new ObservableCollection<AlgResultLightAreaModel>(corners);
                }
            }
            else
            {
                if (ctx.ListView.View is GridView gridView)
                {
                    gridView.Columns.Clear();
                }
                ctx.ListView.ItemsSource = null;
            }
        }

        private static void HandlePersistedCorners(ViewResultContext ctx, ViewResultAlg result)
        {
            var corners = result.ViewResults.ToSpecificViewResults<AlgResultLightAreaModel>();
            if (corners.Count > 0)
            {
                DVPolygon polygon = new() { IsComple = true };
                List<Point> points = corners.Select(item => new Point((int)item.PosX, (int)item.PosY)).ToList();
                foreach (Point point in GrahamScan.ComputeConvexHull(points))
                    polygon.Attribute.Points.Add(point);
                polygon.Attribute.Brush = Brushes.Transparent;
                polygon.Attribute.Pen = new Pen(Brushes.Blue, 1);
                polygon.Attribute.Id = -1;
                polygon.Render();
                ctx.ImageView.AddVisual(polygon);
            }

            if (ctx.ListView.View is GridView gridView)
            {
                ctx.ListView.ItemsSource = null;
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                gridView.Columns.Add(new GridViewColumn { Header = "PosX", DisplayMemberBinding = new Binding(nameof(AlgResultLightAreaModel.PosX)) });
                gridView.Columns.Add(new GridViewColumn { Header = "PosY", DisplayMemberBinding = new Binding(nameof(AlgResultLightAreaModel.PosY)) });
                ctx.ListView.ItemsSource = corners;
            }
        }
    }
}
