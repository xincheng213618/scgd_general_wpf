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

namespace ColorVision.Engine.Templates.Jsons.MTF2
{
    public class MTFItem
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
    public class ChildRect
    {
        public int h { get; set; }
        public int id { get; set; }
        public double? mtfValue { get; set; }
        public int w { get; set; }
        public int x { get; set; }
        public int y { get; set; }
    }

    // 对应 resultChild 每一项
    public class ResultChildItem
    {
        public string name { get; set; }
        public double Average { get; set; }
        public double horizontalAverage { get; set; }
        public double verticalAverage { get; set; }
        public List<ChildRect> childRects { get; set; }
    }

    public class MTFResult
    {
        public List<MTFItem> result { get; set; }
        public List<ResultChildItem> resultChild { get; set; }
    }


    public class MTFDetailViewReslut : IViewResult
    {
        public DetailCommonModel DetailCommonModel { get; set; }

        public MTFDetailViewReslut()
        {

        }

        public MTFDetailViewReslut(DetailCommonModel detailCommonModel)
        {
            DetailCommonModel = detailCommonModel;

            var restfile = JsonConvert.DeserializeObject<ResultFile>(detailCommonModel.ResultJson);
            ResultFileName = restfile?.ResultFileName;

            if (File.Exists(ResultFileName))
            {
                MTFResult = JsonConvert.DeserializeObject<MTFResult>(File.ReadAllText(ResultFileName));
            }

        }
        public string? ResultFileName { get; set; }
        public MTFResult? MTFResult { get; set; }
    }


    public class ViewHandleMTF2 : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleMTF2));

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.MTF};
        public override bool CanHandle1(ViewResultAlg result)
        {
            if (result.Version != "2.0") return false;
            return base.CanHandle1(result);
        }


        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string filePath = selectedPath + "//" + result.Batch + result.ResultType + ".csv";

            var MTFDetailViewResluts = result.ViewResults.ToSpecificViewResults<MTFDetailViewReslut>();
            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine($"name,x,y,w,h,mtfValue");
            if (MTFDetailViewResluts.Count == 1)
            {
                var resultChilds = MTFDetailViewResluts[0].MTFResult.resultChild;
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
                    var mtfs = MTFDetailViewResluts[0].MTFResult?.result;
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


        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(result.Id);
                if (detailCommonModels.Count == 1)
                {
                    MTFDetailViewReslut mtfresult = new MTFDetailViewReslut(detailCommonModels[0]);
                    result.ViewResults.Add(mtfresult);

                    RelayCommand SelectrelayCommand = new RelayCommand(a =>
                    {
                        PlatformHelper.OpenFolderAndSelectFile(mtfresult.ResultFileName);

                    }, a => File.Exists(mtfresult.ResultFileName));

                    RelayCommand OpenrelayCommand = new RelayCommand(a =>
                    {
                        AvalonEditWindow avalonEditWindow = new AvalonEditWindow(mtfresult.ResultFileName) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                        avalonEditWindow.ShowDialog();
                    }, a => File.Exists(mtfresult.ResultFileName));


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
                        foreach (var item in mtfresult.MTFResult.result)
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

                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmMTF2), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults.Count == 1)
            {
                if (result.ViewResults[0] is MTFDetailViewReslut mTFDetailViewReslut)
                {
                    int id = 0;
                    if (mTFDetailViewReslut.MTFResult.result.Count != 0)
                    {
                        foreach (var item in mTFDetailViewReslut.MTFResult.result)
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
                            ctx.ImageView.AddVisual(Rectangle);
                        }
                    }

                    List<string> header = new() { "name", "x","y","w","h","mtfvalue" };
                    List<string> bdHeader = new() { "name", "x", "y", "w", "h", "mtfValue" };

                    if (ctx.ListView.View is GridView gridView)
                    {
                        ctx.LeftGridViewColumnVisibilitys.Clear();
                        gridView.Columns.Clear();
                        for (int i = 0; i < header.Count; i++)
                            gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                        ctx.ListView.ItemsSource = mTFDetailViewReslut?.MTFResult?.result;
                    }
                }
            }
            else
            {

            }



        }



    }
}
