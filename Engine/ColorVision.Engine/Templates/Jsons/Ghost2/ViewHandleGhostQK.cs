#pragma warning disable CS8602

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.UI;
using log4net;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.Jsons.Ghost2
{

    public class ViewHandleGhostQK : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleGhostQK));

        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>() { AlgorithmResultType.Ghost};
        public override bool CanHandle1(AlgorithmResult result)
        {
            if (result.Version != "2.0") return false;
            return base.CanHandle1(result);
        }


        public override void SideSave(AlgorithmResult result, string selectedPath)
        {
            var blackMuraViews = result.ViewResults.ToSpecificViewResults<GhostView>();
            var csvBuilder = new StringBuilder();
            if (blackMuraViews.Count == 1)
            {
                string filePath = selectedPath + "//" + result.Batch + result.ResultType + ".json";
                File.AppendAllText(filePath, blackMuraViews[0].Result, Encoding.UTF8);
            }
        }


        public override void Load(AlgorithmView view, AlgorithmResult result)
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
            }
        }

        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {
            view.ImageView.ImageShow.Clear();


            void OpenSource()
            {
                view.ImageView.ImageShow.Clear();
                foreach (var item in result.ViewResults)
                {
                    if (item is GhostView blackMuraModel)
                    {
                        if (File.Exists(result.FilePath))
                            view.ImageView.OpenImage(result.FilePath);
                        log.Info(result.FilePath);
                    }
                }
            }

            Load(view, result);
            OpenSource();

            List<string> header = new() { "Analysis", "Bright", "Ghost" };
            List<string> bdHeader = new() { "GhostReslut.Analysis", "GhostReslut.Bright", "GhostReslut.Ghost" };

            if (view.listViewSide.View is GridView gridView)
            {
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.listViewSide.ItemsSource = result.ViewResults;
            }
        }



    }
}
