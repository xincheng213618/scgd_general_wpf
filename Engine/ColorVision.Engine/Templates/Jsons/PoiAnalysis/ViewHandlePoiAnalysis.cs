#pragma warning disable CS8602

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Abstractions;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.Jsons.PoiAnalysis
{
    public class PoiAnalysisItem
    {
        public string Content { get; set; }
        public double Value { get; set; }
    }

    public class PoiAnalysisResult
    {
        public PoiAnalysisItem result { get; set; }
    }


    public class PoiAnalysisDetailViewReslut : IViewResult
    {
        public DetailCommonModel DetailCommonModel { get; set; }

        public PoiAnalysisDetailViewReslut()
        {

        }

        public PoiAnalysisDetailViewReslut(DetailCommonModel detailCommonModel)
        {
            DetailCommonModel = detailCommonModel;

            var restfile = JsonConvert.DeserializeObject<ResultFile>(detailCommonModel.ResultJson);
            ResultFileName = restfile?.ResultFileName;

            if (File.Exists(ResultFileName))
            {
                PoiAnalysisResult = JsonConvert.DeserializeObject<PoiAnalysisResult>(File.ReadAllText(ResultFileName));
            }

        }
        public string? ResultFileName { get; set; }

        public PoiAnalysisResult? PoiAnalysisResult { get; set; }
    }


    public class ViewHandlePoiAnalysis : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandlePoiAnalysis));

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.PoiAnalysis };
        public override bool CanHandle1(ViewResultAlg result)
        {
            if (result.Version != "1.0") return false;
            return base.CanHandle1(result);
        }


        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string fileName = System.IO.Path.Combine(selectedPath, $"{result.ResultType}_{result.Batch}.csv");
            var ViewResults = result.ViewResults.ToSpecificViewResults<PoiAnalysisDetailViewReslut>();

            var csvBuilder = new StringBuilder();

            if (ViewResults.Count == 1)
            {
                var PoiAnalysisResult = ViewResults[0].PoiAnalysisResult;
                csvBuilder.AppendLine("Content,Value");
                csvBuilder.AppendLine(string.Join( PoiAnalysisResult.result.Content,PoiAnalysisResult.result.Value));

                File.AppendAllText(fileName, csvBuilder.ToString(), Encoding.UTF8);
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
                    PoiAnalysisDetailViewReslut mtfresult = new PoiAnalysisDetailViewReslut(detailCommonModels[0]);
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
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmPoiAnalysis), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(AlgorithmView view, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults.Count == 1)
            {

                List<string> header = new() { "Content", "Value" };
                List<string> bdHeader = new() { "PoiAnalysisResult.result.Content", "PoiAnalysisResult.result.Value", };

                if (view.listViewSide.View is GridView gridView)
                {
                    view.LeftGridViewColumnVisibilitys.Clear();
                    gridView.Columns.Clear();
                    for (int i = 0; i < header.Count; i++)
                        gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                    view.listViewSide.ItemsSource = result.ViewResults;
                }
            }
            else
            {

            }



        }



    }
}
