#pragma warning disable CA1725,CS8604

using ColorVision.Common.MVVM;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Results;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using log4net;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
            var views = result.ViewResults?.ToSpecificViewResults<GhostView>();
            if (views?.Count != 1 || string.IsNullOrWhiteSpace(views[0].Result))
                throw new InvalidDataException($"鬼影结果 {result.Id} 缺少可导出的明细 JSON。");

            string json = views[0].Result;
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"鬼影结果 {result.Id} 的明细必须是一个 JSON 对象。");

            AlgorithmSideDataFiles.Write(result, selectedPath, $"{result.Batch}{result.ResultType}.json",
                path => File.WriteAllText(path, json, Encoding.UTF8));
        }


        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                if (!result.TryReadPersistedDetails(DeatilCommonDao.Instance, out var detailCommonModels)) return;
                GhostView? ghostresult = detailCommonModels.Count == 1 ? new GhostView(detailCommonModels[0]) : null;
                if (ghostresult != null && string.IsNullOrWhiteSpace(ghostresult.Result)) return;
                result.ViewResults = new ObservableCollection<IViewResult>();
                if (ghostresult != null)
                {
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


                    result.ContextMenu.Items.Add(new MenuItem() { Header = string.Format(Properties.Resources.SelectResultSetFormat, "2.0"), Command = SelectrelayCommand });
                    result.ContextMenu.Items.Add(new MenuItem() { Header = string.Format(Properties.Resources.OpenResultSetFormat, "2.0"), Command = OpenrelayCommand });
                }
                result.ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Debug, Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmGhost2), ImageFilePath = result.FilePath })) });

            }
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            OpenSourceImage(ctx, result);
            log.Info(result.FilePath);

            List<string> header = new() { "Analysis", "Bright", "Ghost" };
            List<string> bdHeader = new() { "GhostReslut.Analysis", "GhostReslut.Bright", "GhostReslut.Ghost" };

            if (ctx.ListView.View is GridView gridView)
            {
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                ctx.ListView.ItemsSource = result.ViewResults;
            }
        }



    }
}
