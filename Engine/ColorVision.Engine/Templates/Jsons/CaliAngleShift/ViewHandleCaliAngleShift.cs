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

namespace ColorVision.Engine.Templates.Jsons.CaliAngleShift
{
    public class CaliAngleShiftItem
    {
        public string name { get; set; }
        public double? value { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public int w { get; set; }
        public int h { get; set; }
        public int id { get; set; }
    }

    public class CaliAngleShiftResult
    {
        public List<CaliAngleShiftItem> result { get; set; }
    }


    public class CaliAngleShiftDetailViewResult : IViewResult
    {
        public DetailCommonModel DetailCommonModel { get; set; }

        public CaliAngleShiftDetailViewResult()
        {

        }

        public CaliAngleShiftDetailViewResult(DetailCommonModel detailCommonModel)
        {
            DetailCommonModel = detailCommonModel;

            var restfile = JsonConvert.DeserializeObject<ResultFile>(detailCommonModel.ResultJson);
            ResultFileName = restfile?.ResultFileName;

            if (File.Exists(ResultFileName))
            {
                CaliAngleShiftResult = JsonConvert.DeserializeObject<CaliAngleShiftResult>(File.ReadAllText(ResultFileName));
            }

        }
        public string? ResultFileName { get; set; }
        public CaliAngleShiftResult? CaliAngleShiftResult { get; set; }
    }


    public class ViewHandleCaliAngleShift : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleCaliAngleShift));

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.Calibration};
        
        public override bool CanHandle1(ViewResultAlg result)
        {
            if (result.Version != "1.0") return false;
            return base.CanHandle1(result);
        }


        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string filePath = selectedPath + "//" + result.Batch + result.ResultType + ".csv";

            var CaliAngleShiftDetailViewResults = result.ViewResults.ToSpecificViewResults<CaliAngleShiftDetailViewResult>();
            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine($"name,x,y,w,h,value");
            if (CaliAngleShiftDetailViewResults.Count == 1)
            {
                var items = CaliAngleShiftDetailViewResults[0].CaliAngleShiftResult?.result;
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        csvBuilder.AppendLine($"{item.name},{item.x},{item.y},{item.w},{item.h},{item.value}");
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
                    CaliAngleShiftDetailViewResult caliresult = new CaliAngleShiftDetailViewResult(detailCommonModels[0]);
                    result.ViewResults.Add(caliresult);

                    RelayCommand SelectrelayCommand = new RelayCommand(a =>
                    {
                        PlatformHelper.OpenFolderAndSelectFile(caliresult.ResultFileName);

                    }, a => File.Exists(caliresult.ResultFileName));

                    RelayCommand OpenrelayCommand = new RelayCommand(a =>
                    {
                        AvalonEditWindow avalonEditWindow = new AvalonEditWindow(caliresult.ResultFileName) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                        avalonEditWindow.ShowDialog();
                    }, a => File.Exists(caliresult.ResultFileName));


                    result.ContextMenu.Items.Add(new MenuItem() { Header = "选中结果集", Command = SelectrelayCommand });
                    result.ContextMenu.Items.Add(new MenuItem() { Header = "打开结果集", Command = OpenrelayCommand });



                    void ExportToPoi()
                    {
                        if (caliresult.CaliAngleShiftResult?.result == null) return;
                        
                        int old1 = TemplatePoi.Params.Count;
                        TemplatePoi templatePoi1 = new TemplatePoi();
                        templatePoi1.ImportTemp = new PoiParam() { Name = templatePoi1.NewCreateFileName("poi") };
                        templatePoi1.ImportTemp.Height = 400;
                        templatePoi1.ImportTemp.Width = 300;
                        templatePoi1.ImportTemp.PoiConfig.BackgroundFilePath = result.FilePath;
                        foreach (var item in caliresult.CaliAngleShiftResult.result)
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

                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmCaliAngleShift), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(ViewResultContext view, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults.Count == 1)
            {
                if (result.ViewResults[0] is CaliAngleShiftDetailViewResult caliAngleShiftDetailViewResult)
                {
                    int id = 0;
                    if (caliAngleShiftDetailViewResult.CaliAngleShiftResult?.result != null && caliAngleShiftDetailViewResult.CaliAngleShiftResult.result.Count != 0)
                    {
                        foreach (var item in caliAngleShiftDetailViewResult.CaliAngleShiftResult.result)
                        {
                            id++;
                            DVRectangleText Rectangle = new();
                            Rectangle.Attribute.Rect = new Rect(item.x,item.y,item.w,item.h);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                            Rectangle.Attribute.Id = id;
                            Rectangle.Attribute.Text = item.name;
                            Rectangle.Attribute.Msg = item.value?.ToString() ?? string.Empty;
                            Rectangle.Render();
                            view.ImageView.AddVisual(Rectangle);
                        }
                    }

                    List<string> header = new() { "name", "x","y","w","h","value" };
                    List<string> bdHeader = new() { "name", "x", "y", "w", "h", "value" };

                    if (view.ListView.View is GridView gridView)
                    {
                        view.LeftGridViewColumnVisibilitys.Clear();
                        gridView.Columns.Clear();
                        for (int i = 0; i < header.Count; i++)
                            gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                        view.ListView.ItemsSource = caliAngleShiftDetailViewResult?.CaliAngleShiftResult?.result;
                    }
                }
            }
            else
            {

            }



        }



    }
}
