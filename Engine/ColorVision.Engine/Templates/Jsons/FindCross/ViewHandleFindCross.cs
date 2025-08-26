#pragma warning disable

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Abstractions;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using log4net;
using MQTTMessageLib.Algorithm;
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
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.Jsons.FindCross
{
    // Add a new class for the FindCross result structure
    public class FindCrossItem
    {
        public Center center { get; set; }
        public double rotationAngle { get; set; }
        public Tilt tilt { get; set; }

        public string name { get; set; }

        public double x { get; set; }
        public double y { get; set; }
        public double w { get; set; }
        public double h { get; set; }
    }

    public class Center
    {
        public int x { get; set; }
        public int y { get; set; }
    }

    public class Tilt
    {
        public double tilt_x { get; set; }
        public double tilt_y { get; set; }
    }

    public class FindCrossResult
    {
        public List<FindCrossItem> result { get; set; }
    }


    public class FindCrossDetailViewReslut : IViewResult
    {
        public DetailCommonModel DetailCommonModel { get; set; }

        public FindCrossDetailViewReslut()
        {

        }
        public FindCrossDetailViewReslut(DetailCommonModel detailCommonModel)
        {
            DetailCommonModel = detailCommonModel;

            var restfile = JsonConvert.DeserializeObject<ResultFile>(detailCommonModel.ResultJson);
            ResultFileName = restfile?.ResultFileName;

            if (File.Exists(ResultFileName))
            {
                // Try to parse as FindCrossResult first, fall back to MTFResult for legacy support
                string fileText = File.ReadAllText(ResultFileName);
                try
                {
                    FindCrossResult = JsonConvert.DeserializeObject<FindCrossResult>(fileText);
                }
                catch
                {
                    FindCrossResult = null;
                }
            }
        }
        public string? ResultFileName { get; set; }

        public FindCrossResult? FindCrossResult { get; set; }
    }


    public class ViewHandleFindCross : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleFindCross));

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.FindCross };
        public override bool CanHandle1(ViewResultAlg result)
        {
            if (result.Version != "1.0") return false;
            return base.CanHandle1(result);
        }

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string filePath = selectedPath + "//" + result.Batch + result.ResultType + ".csv";

            var MTFDetailViewResluts = result.ViewResults.ToSpecificViewResults<FindCrossDetailViewReslut>();
            var csvBuilder = new StringBuilder();
            // For FindCross type, modify header and content
            if (MTFDetailViewResluts.Count == 1)
            {
                var findCross = MTFDetailViewResluts[0].FindCrossResult?.result;
                if (findCross != null)
                {
                    int id = 0;
                    csvBuilder.AppendLine($"id,name,x,y,w,h,center_x,center_y,rotationAngle,tilt_tilt_x,tilt_tilt_y");
                    foreach (var item in findCross)
                    {
                        id++;
                        csvBuilder.AppendLine($"{id},{item.name},{item.x},{item.y},{item.w},{item.h},{item.center.x},{item.center.y},{item.rotationAngle},{item.tilt.tilt_x},{item.tilt.tilt_y}");
                    }
                    File.AppendAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
                    return;
                }
            }
        }

        public override void Load(AlgorithmView view, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(result.Id);
                if (detailCommonModels.Count == 1)
                {
                    FindCrossDetailViewReslut mtfresult = new FindCrossDetailViewReslut(detailCommonModels[0]);
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

                    result.ContextMenu.Items.Add(new MenuItem() { Header = "选中1.0结果集", Command = SelectrelayCommand });
                    result.ContextMenu.Items.Add(new MenuItem() { Header = "打开1.0结果集", Command = OpenrelayCommand });
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmFindCross), ImageFilePath = result.FilePath })) });

            }
        }

        public override void Handle(AlgorithmView view, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults.Count == 1)
            {
                if (result.ViewResults[0] is FindCrossDetailViewReslut mTFDetailViewReslut)
                {
                    // Show FindCrossResult if available
                    if (mTFDetailViewReslut.FindCrossResult != null && mTFDetailViewReslut.FindCrossResult.result != null)
                    {
                        var header = new List<string> { "id","name", "x", "y", "w", "h", "center_x", "center_y", "rotationAngle", "tilt_tilt_x", "tilt_tilt_y" };
                        // For binding, you may want to use a value converter or custom object, or expose computed properties
                        if (view.listViewSide.View is GridView gridView)
                        {
                            view.LeftGridViewColumnVisibilitys.Clear();
                            gridView.Columns.Clear();
                            foreach (var h in header)
                                gridView.Columns.Add(new GridViewColumn() { Header = h, DisplayMemberBinding = new Binding(h) });

                            int id = 0;
                            foreach (var item in mTFDetailViewReslut.FindCrossResult.result)
                            {
                                id++;
                                DVCircleText cricle = new DVCircleText();
                                cricle.Attribute.Center = new Point(item.center.x,item.center.y);
                                cricle.Attribute.Radius = 10;
                                cricle.Attribute.Brush = Brushes.Red;
                                cricle.Attribute.Pen = new Pen(Brushes.Red, 10);
                                cricle.Attribute.Id = id;
                                cricle.Attribute.Text = id.ToString();
                                cricle.Attribute.Msg =  $"({item.center.x},{item.center.y}){Environment.NewLine}xtilt:{item.tilt.tilt_x}{Environment.NewLine}ytilt:{item.tilt.tilt_y}{Environment.NewLine}rotation:{item.rotationAngle}"  ;
                                cricle.Render();
                                view.ImageView.AddVisual(cricle);
                            }

                            // Prepare a flat list for binding
                            var flatList = new List<dynamic>();
                            int id1 = 0;
                            foreach (var item in mTFDetailViewReslut.FindCrossResult.result)
                            {
                                id1++;
                                flatList.Add(new
                                {
                                    id =id1,
                                    name =item.name,
                                    x = item.x,
                                    y = item.y,
                                    w =item.w,
                                    h =item.h,
                                    center_x = item.center.x,
                                    center_y = item.center.y,
                                    rotationAngle = item.rotationAngle,
                                    tilt_tilt_x = item.tilt.tilt_x,
                                    tilt_tilt_y = item.tilt.tilt_y
                                });
                            }
                            view.listViewSide.ItemsSource = flatList;
                        }
                    }
                }
            }
        }
    }
}