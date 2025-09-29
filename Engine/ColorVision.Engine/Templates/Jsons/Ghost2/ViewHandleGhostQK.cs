#pragma warning disable CS8602

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
using log4net;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ColorVision.Engine.Services;
using ColorVision.Solution.Editor.AvalonEditor;

namespace ColorVision.Engine.Templates.Jsons.Ghost2
{

    public class ViewHandleGhostQK : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleGhostQK));

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.Ghost};
        public override bool CanHandle1(ViewResultAlg result)
        {
            if (result.Version != "2.0") return false;
            return base.CanHandle1(result);
        }


        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            var blackMuraViews = result.ViewResults.ToSpecificViewResults<GhostView>();
            var csvBuilder = new StringBuilder();
            if (blackMuraViews.Count == 1)
            {
                string filePath = selectedPath + "//" + result.Batch + result.ResultType + ".json";
                File.AppendAllText(filePath, blackMuraViews[0].Result, Encoding.UTF8);
            }
        }


        public override void Load(IViewImageA view, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();

                List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(result.Id);
                if (detailCommonModels.Count == 1)
                {
                    GhostView ghostresult = new GhostView(detailCommonModels[0]);
                    result.ViewResults.Add(ghostresult);

                    RelayCommand SelectrelayCommand = new RelayCommand(a =>
                    {
                        PlatformHelper.OpenFolderAndSelectFile(ghostresult.ResultFileName);

                    }, a => File.Exists(ghostresult.ResultFileName));

                    RelayCommand OpenrelayCommand = new RelayCommand(a =>
                    {
                        AvalonEditWindow avalonEditWindow = new AvalonEditWindow(ghostresult.ResultFileName) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                        avalonEditWindow.ShowDialog();
                    }, a => File.Exists(ghostresult.ResultFileName));


                    result.ContextMenu.Items.Add(new MenuItem() { Header = "选中2.0结果集", Command = SelectrelayCommand });
                    result.ContextMenu.Items.Add(new MenuItem() { Header = "打开2.0结果集", Command = OpenrelayCommand });
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmGhost2), ImageFilePath = result.FilePath })) });

            }
        }

        public override void Handle(IViewImageA view, ViewResultAlg result)
        {
            foreach (var item in result.ViewResults)
            {
                if (item is GhostView blackMuraModel)
                {
                    if (File.Exists(result.FilePath))
                        view.ImageView.OpenImage(result.FilePath);
                    log.Info(result.FilePath);
                }
            }

            List<string> header = new() { "Analysis", "Bright", "Ghost" };
            List<string> bdHeader = new() { "GhostReslut.Analysis", "GhostReslut.Bright", "GhostReslut.Ghost" };

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
