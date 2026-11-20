#pragma warning disable CA1725,CS8602,CS8604

using ColorVision.Common.MVVM;
using ColorVision.Engine.Services;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Solution.Editor.AvalonEditor;
using log4net;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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
        private static readonly object ExportLock = new();

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.PoiAnalysis };
        public override bool CanHandle1(ViewResultAlg result)
        {
            if (result.Version != "1.0") return false;
            return base.CanHandle1(result);
        }


        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            var details = result.ViewResults?.ToSpecificViewResults<PoiAnalysisDetailViewReslut>();
            if (details?.Count != 1 || details[0].PoiAnalysisResult?.result is not { } measurement)
                throw new InvalidDataException($"POI 分析结果 {result.Id} 缺少可导出的明细。");

            Directory.CreateDirectory(selectedPath);
            string fileName = Path.Combine(selectedPath, Tool.SanitizeFileName($"{result.ResultType}_{result.Batch}.csv"));
            lock (ExportLock)
            {
                using FileStream file = new(fileName, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(file, Encoding.UTF8);
                using var csv = new CsvHelper.CsvWriter(writer, CultureInfo.InvariantCulture, leaveOpen: true);
                if (file.Length == 0)
                {
                    csv.WriteField("Content");
                    csv.WriteField("Value");
                    csv.NextRecord();
                }

                csv.WriteField(measurement.Content);
                csv.WriteField(measurement.Value.ToString(CultureInfo.CurrentCulture));
                csv.NextRecord();
            }
        }


        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                if (!result.TryReadPersistedDetails(DeatilCommonDao.Instance, out var detailCommonModels)) return;
                PoiAnalysisDetailViewReslut? mtfresult = detailCommonModels.Count == 1 ? new(detailCommonModels[0]) : null;
                if (mtfresult != null && mtfresult.PoiAnalysisResult?.result == null) return;
                result.ViewResults = new ObservableCollection<IViewResult>();
                if (mtfresult != null)
                {
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


                    result.ContextMenu.Items.Add(new MenuItem() { Header = string.Format(Properties.Resources.SelectResultSetFormat, "2.0"), Command = SelectrelayCommand });
                    result.ContextMenu.Items.Add(new MenuItem() { Header = string.Format(Properties.Resources.OpenResultSetFormat, "2.0"), Command = OpenrelayCommand });
                }
            }
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            OpenSourceImage(ctx, result);

            if (result.ViewResults?.Count == 1)
            {

                List<string> header = new() { "Content", "Value" };
                List<string> bdHeader = new() { "PoiAnalysisResult.result.Content", "PoiAnalysisResult.result.Value", };

                if (ctx.ListView.View is GridView gridView)
                {
                    ctx.LeftGridViewColumnVisibilitys.Clear();
                    gridView.Columns.Clear();
                    for (int i = 0; i < header.Count; i++)
                        gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                    ctx.ListView.ItemsSource = result.ViewResults;
                }
            }
            else
            {

            }



        }



    }
}
