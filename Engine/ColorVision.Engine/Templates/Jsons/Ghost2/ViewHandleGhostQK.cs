#pragma warning disable CS8602

using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.Jsons.Distortion2;
using log4net;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
                List<DetailCommonModel> AlgResultModels = DeatilCommonDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultModels)
                {
                    GhostView blackMuraView = new GhostView(item);
                    result.ViewResults.Add(blackMuraView);
                }
            }
        }

        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {
            view.ImageView.ImageShow.Clear();
            if (result.ResultCode != 0)
            {
                if (File.Exists(result.FilePath))
                    view.ImageView.OpenImage(result.FilePath);
                return;
            }

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

            List<string> header = new() { "LvAvg", "LvMax", "LvMin", "Uniformity(%)", "ZaRelMax", "AreaJsonVal" };
            List<string> bdHeader = new() { "ResultJson.LvAvg", "ResultJson.LvMax", "ResultJson.LvMin", "ResultJson.Uniformity", "ResultJson.ZaRelMax", "AreaJsonVal" };

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
