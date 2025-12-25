#pragma warning disable CS8602

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
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

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI
{
    public class OLEDAOIItem
    {
        public string name { get; set; }
        public double? mtfValue { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public int w { get; set; }
        public int h { get; set; }
        public int id { get; set; } // 新增 id 字段
    }

    // 对应 resultChild.childRects
    public class OLEDAOIChildRect
    {
        public int h { get; set; }
        public int id { get; set; }
        public double? mtfValue { get; set; }
        public int w { get; set; }
        public int x { get; set; }
        public int y { get; set; }
    }

    // 对应 resultChild 每一项
    public class OLEDAOIResultChildItem
    {
        public string name { get; set; }
        public double Average { get; set; }
        public double horizontalAverage { get; set; }
        public double verticalAverage { get; set; }
        public List<OLEDAOIChildRect> childRects { get; set; }
    }

    public class OLEDAOIResult
    {
        public List<OLEDAOIItem> result { get; set; }
        public List<OLEDAOIResultChildItem> resultChild { get; set; }
    }


    public class OLEDAOIDetailViewReslut : IViewResult
    {
        public DetailCommonModel DetailCommonModel { get; set; }

        public OLEDAOIDetailViewReslut()
        {

        }

        public OLEDAOIDetailViewReslut(DetailCommonModel detailCommonModel)
        {
            DetailCommonModel = detailCommonModel;

            var restfile = JsonConvert.DeserializeObject<ResultFile>(detailCommonModel.ResultJson);
            ResultFileName = restfile?.ResultFileName;

            if (File.Exists(ResultFileName))
            {
                OLEDAOIResult = JsonConvert.DeserializeObject<OLEDAOIResult>(File.ReadAllText(ResultFileName));
            }

        }
        public string? ResultFileName { get; set; }
        public OLEDAOIResult? OLEDAOIResult { get; set; }
    }


    public class ViewHandleOLEDAOI : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleOLEDAOI));

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.OLEDAOI};
        public override bool CanHandle1(ViewResultAlg result)
        {
            if (result.Version != "2.0") return false;
            return base.CanHandle1(result);
        }


        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string filePath = selectedPath + "//" + result.Batch + result.ResultType + ".csv";

            var OLEDAOIDetailViewResluts = result.ViewResults.ToSpecificViewResults<OLEDAOIDetailViewReslut>();
            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine($"name,x,y,w,h,mtfValue");
            if (OLEDAOIDetailViewResluts.Count == 1)
            {
                var resultChilds = OLEDAOIDetailViewResluts[0].OLEDAOIResult.resultChild;
                if (resultChilds != null)
                {

                    foreach (var child in resultChilds)
                    {
                        // child.name 这一组的 name
                        if (child.childRects != null && child.childRects.Count > 0)
                        {
                            foreach (var rect in child.childRects)
                            {
                                csvBuilder.AppendLine($"{child.name},{rect.x},{rect.y},{rect.w},{rect.h},{rect.mtfValue},{rect.id}");
                            }
                        }
                        // 如果你还需要写入平均值，可以单独加一行（如不需要可移除）
                        csvBuilder.AppendLine($"{child.name},,,,HorizontalAverage,{child.horizontalAverage}");
                        csvBuilder.AppendLine($"{child.name},,,,verticalAverage,{child.verticalAverage}");
                        csvBuilder.AppendLine($"{child.name},,,,Average,{child.Average}");

                    }
                }
                else
                {
                    var mtfs = OLEDAOIDetailViewResluts[0].OLEDAOIResult?.result;
                    if (mtfs != null)
                    {
                        foreach (var item in mtfs)
                        {
                            csvBuilder.AppendLine($"{item.name},{item.x},{item.y},{item.w},{item.h},{item.mtfValue}");
                        }
                    }
                }

                File.AppendAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
            }
        }


        public override void Load(ViewResultContext view, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(result.Id);
                if (detailCommonModels.Count == 1)
                {
                    OLEDAOIDetailViewReslut oledaoiresult = new OLEDAOIDetailViewReslut(detailCommonModels[0]);
                    result.ViewResults.Add(oledaoiresult);

                    RelayCommand SelectrelayCommand = new RelayCommand(a =>
                    {
                        PlatformHelper.OpenFolderAndSelectFile(oledaoiresult.ResultFileName);

                    }, a => File.Exists(oledaoiresult.ResultFileName));

                    RelayCommand OpenrelayCommand = new RelayCommand(a =>
                    {
                        AvalonEditWindow avalonEditWindow = new AvalonEditWindow(oledaoiresult.ResultFileName) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                        avalonEditWindow.ShowDialog();
                    }, a => File.Exists(oledaoiresult.ResultFileName));


                    result.ContextMenu.Items.Add(new MenuItem() { Header = "选中2.0结果集", Command = SelectrelayCommand });
                    result.ContextMenu.Items.Add(new MenuItem() { Header = "打开2.0结果集", Command = OpenrelayCommand });



                    void ExportToPoi()
                    {
                        int old1 = TemplatePoi.Params.Count;
                        TemplatePoi templatePoi1 = new TemplatePoi();
                        templatePoi1.ImportTemp = new PoiParam() { Name = templatePoi1.NewCreateFileName("poi") };
                        templatePoi1.ImportTemp.Height = 400;
                        templatePoi1.ImportTemp.Width = 300;
                        templatePoi1.ImportTemp.PoiConfig.BackgroundFilePath = result.FilePath;
                        foreach (var item in oledaoiresult.OLEDAOIResult.result)
                        {
                            PoiPoint poiPoint = new PoiPoint()
                            {
                                Name = item.name,
                                PixX = item.x,
                                PixY = item.y,
                                PixHeight =item.w,
                                PixWidth = item.h,
                                PointType = GraphicTypes.Rect,
                                Id = item.id
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
                }

                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmOLEDAOI), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(ViewResultContext view, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults.Count == 1)
            {
                if (result.ViewResults[0] is OLEDAOIDetailViewReslut oledaoiDetailViewReslut)
                {
                    int id = 0;
                    if (oledaoiDetailViewReslut.OLEDAOIResult.result.Count != 0)
                    {
                        foreach (var item in oledaoiDetailViewReslut.OLEDAOIResult.result)
                        {
                            id++;
                            DVRectangleText Rectangle = new();
                            Rectangle.Attribute.Rect = new Rect(item.x,item.y,item.w,item.h);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                            Rectangle.Attribute.Id = id;
                            Rectangle.Attribute.Text = item.name;
                            Rectangle.Attribute.Msg = item.mtfValue.ToString();
                            Rectangle.Render();
                            view.ImageView.AddVisual(Rectangle);
                        }
                    }

                    List<string> header = new() { "name", "x","y","w","h","mtfvalue" };
                    List<string> bdHeader = new() { "name", "x", "y", "w", "h", "mtfValue" };

                    if (view.ListView.View is GridView gridView)
                    {
                        view.LeftGridViewColumnVisibilitys.Clear();
                        gridView.Columns.Clear();
                        for (int i = 0; i < header.Count; i++)
                            gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                        view.ListView.ItemsSource = oledaoiDetailViewReslut?.OLEDAOIResult?.result;
                    }
                }
            }
            else
            {

            }



        }



    }
}
