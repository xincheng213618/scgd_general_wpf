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

namespace ColorVision.Engine.Templates.Jsons.SFR2
{
    public class SFR2Curve
    {
        public List<double> domainSamplingData { get; set; }
        public List<double> frequency { get; set; }
        public int id { get; set; }
    }

    public class SFR2ResultItem
    {
        public string name { get; set; }
        public List<SFR2Curve> data { get; set; }
    }

    public class SFR2ResultFile
    {
        public List<SFR2ResultItem> result { get; set; }
    }

    // 用于列表展示的汇总行（从每条曲线选一个代表频点）
    public class SFR2CurveSummary
    {
        public string name { get; set; }
        public int id { get; set; }
        public double frequency { get; set; }
        public double value { get; set; }
    }

    public class SFRDetailViewReslut : IViewResult
    {
        public DetailCommonModel DetailCommonModel { get; set; }

        public SFRDetailViewReslut() { }

        public SFRDetailViewReslut(DetailCommonModel detailCommonModel)
        {
            DetailCommonModel = detailCommonModel;
            var restfile = JsonConvert.DeserializeObject<ResultFile>(detailCommonModel.ResultJson);
            ResultFileName = restfile?.ResultFileName;

            if (File.Exists(ResultFileName))
            {
                var json = File.ReadAllText(ResultFileName);
                SFR2Result = JsonConvert.DeserializeObject<SFR2ResultFile>(json);
            }
        }

        public string? ResultFileName { get; set; }
        public SFR2ResultFile? SFR2Result { get; set; }
        public bool HasResult => SFR2Result?.result != null && SFR2Result.result.Count > 0;
    }

    public class ViewHandleSFR2 : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleSFR2));

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.SFR };
        public override bool CanHandle1(ViewResultAlg result)
        {
            if (result.Version != "2.0") return false;
            return base.CanHandle1(result);
        }

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            string filePath = selectedPath + "//" + result.Batch + result.ResultType + ".csv";

            var detailResults = result.ViewResults.ToSpecificViewResults<SFRDetailViewReslut>();
            var csvBuilder = new StringBuilder();

            if (detailResults.Count == 1)
            {
                var viewRes = detailResults[0];
                csvBuilder.AppendLine("name,id,frequency,value");

                if (viewRes.HasResult)
                {
                    foreach (var point in viewRes.SFR2Result.result)
                    {
                        if (point?.data == null) continue;
                        foreach (var curve in point.data)
                        {
                            if (curve?.frequency == null || curve?.domainSamplingData == null) continue;
                            int n = System.Math.Min(curve.frequency.Count, curve.domainSamplingData.Count);
                            for (int i = 0; i < n; i++)
                            {
                                csvBuilder.AppendLine($"{point.name},{curve.id},{curve.frequency[i]},{curve.domainSamplingData[i]}");
                            }
                        }
                    }
                }

                File.AppendAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
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
                    SFRDetailViewReslut sfrresult = new SFRDetailViewReslut(detailCommonModels[0]);
                    result.ViewResults.Add(sfrresult);

                    RelayCommand SelectrelayCommand = new RelayCommand(a =>
                    {
                        PlatformHelper.OpenFolderAndSelectFile(sfrresult.ResultFileName);
                    }, a => File.Exists(sfrresult.ResultFileName));

                    RelayCommand OpenrelayCommand = new RelayCommand(a =>
                    {
                        AvalonEditWindow avalonEditWindow = new AvalonEditWindow(sfrresult.ResultFileName) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                        avalonEditWindow.ShowDialog();
                    }, a => File.Exists(sfrresult.ResultFileName));

                    result.ContextMenu.Items.Add(new MenuItem() { Header = "选中2.0结果集", Command = SelectrelayCommand });
                    result.ContextMenu.Items.Add(new MenuItem() { Header = "打开2.0结果集", Command = OpenrelayCommand });

                    // 仅新版，无位置信息，不提供 POI 导出菜单
                }

                result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmSFR2), ImageFilePath = result.FilePath })) });
            }
        }

        public override void Handle(IViewImageA view, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults.Count == 1 && result.ViewResults[0] is SFRDetailViewReslut sfrDetailViewReslut)
            {
                // 新版结果无位置信息，不绘制矩形
                // 在列表中展示每条曲线在接近 0.5 的频点的值（可按需调整 target）
                if (view.ListView.View is GridView gridView)
                {
                    view.LeftGridViewColumnVisibilitys.Clear();
                    gridView.Columns.Clear();

                    var rows = new List<SFR2CurveSummary>();
                    if (sfrDetailViewReslut.HasResult)
                    {
                        foreach (var p in sfrDetailViewReslut.SFR2Result.result)
                        {
                            if (p?.data == null) continue;
                            foreach (var curve in p.data)
                            {
                                if (curve?.frequency == null || curve?.domainSamplingData == null) continue;
                                if (curve.frequency.Count == 0 || curve.domainSamplingData.Count == 0) continue;

                                double target = 0.5; // 选择接近 0.5 的频点
                                int idx = 0;
                                double minDiff = double.MaxValue;
                                int n = System.Math.Min(curve.frequency.Count, curve.domainSamplingData.Count);
                                for (int i = 0; i < n; i++)
                                {
                                    double diff = System.Math.Abs(curve.frequency[i] - target);
                                    if (diff < minDiff)
                                    {
                                        minDiff = diff;
                                        idx = i;
                                    }
                                }

                                rows.Add(new SFR2CurveSummary
                                {
                                    name = p.name,
                                    id = curve.id,
                                    frequency = curve.frequency[idx],
                                    value = curve.domainSamplingData[idx]
                                });
                            }
                        }
                    }

                    var header = new List<string> { "name", "id", "frequency", "value" };
                    var bdHeader = new List<string> { "name", "id", "frequency", "value" };
                    for (int i = 0; i < header.Count; i++)
                        gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });

                    view.ListView.ItemsSource = rows;
                }
            }
        }
    }
}
