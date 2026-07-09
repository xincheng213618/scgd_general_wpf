#pragma warning disable CA1507,CA1725,CA1859,CS8601,CS8602,CS8604

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.ImageEditor.Draw;
using ColorVision.Solution.Editor.AvalonEditor;
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

namespace ColorVision.Engine.Templates.Jsons.DetectScreenDefects
{
    public class DetectScreenDefectItem
    {
        public int Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("width")]
        public double Width { get; set; }

        [JsonProperty("height")]
        public double Height { get; set; }

        [JsonProperty("area")]
        public double Area { get; set; }

        [JsonProperty("contrast")]
        public double? Contrast { get; set; }

        [JsonProperty("mean_value")]
        public double? MeanValue { get; set; }

        [JsonProperty("local_mean")]
        public double? LocalMean { get; set; }
    }

    public class DetectScreenDefectsResult
    {
        [JsonProperty("AvgBrightness")]
        public double? AvgBrightness { get; set; }

        [JsonProperty("DefectCount")]
        public int DefectCount { get; set; }

        [JsonProperty("Defects")]
        public List<DetectScreenDefectItem> Defects { get; set; } = new();

        [JsonProperty("GradeLevel")]
        public string GradeLevel { get; set; }

        [JsonProperty("TimeStamp")]
        public string TimeStamp { get; set; }
    }

    public class DetectScreenDefectsDetailViewResult : IViewResult
    {
        public DetailCommonModel DetailCommonModel { get; set; }
        public string? ResultFileName { get; set; }
        public DetectScreenDefectsResult? DetectResult { get; set; }

        public DetectScreenDefectsDetailViewResult()
        {
        }

        public DetectScreenDefectsDetailViewResult(DetailCommonModel detailCommonModel)
        {
            DetailCommonModel = detailCommonModel;
            var restfile = JsonConvert.DeserializeObject<ResultFile>(detailCommonModel.ResultJson);
            ResultFileName = restfile?.ResultFileName;

            if (!string.IsNullOrWhiteSpace(ResultFileName) && File.Exists(ResultFileName))
            {
                DetectResult = JsonConvert.DeserializeObject<DetectScreenDefectsResult>(File.ReadAllText(ResultFileName));
            }
            else
            {
                DetectResult = JsonConvert.DeserializeObject<DetectScreenDefectsResult>(detailCommonModel.ResultJson);
            }

            if (DetectResult?.Defects == null)
                return;

            for (int i = 0; i < DetectResult.Defects.Count; i++)
                DetectResult.Defects[i].Id = i + 1;
        }
    }

    public class ViewHandleDetectScreenDefects : IResultHandleBase
    {
        public override string Name => "屏幕缺陷检测";
        public override List<ViewResultAlgType> CanHandle { get; } = new() { ViewResultAlgType.ARVR_DetectScreenDefects };

        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults != null)
                return;

            result.ViewResults = new ObservableCollection<IViewResult>();
            List<DetailCommonModel> detailCommonModels = DeatilCommonDao.Instance.GetAllByPid(result.Id);
            if (detailCommonModels.Count == 0)
                return;

            var detectResult = new DetectScreenDefectsDetailViewResult(detailCommonModels[0]);
            result.ViewResults.Add(detectResult);

            RelayCommand selectCommand = new RelayCommand(a =>
            {
                PlatformHelper.OpenFolderAndSelectFile(detectResult.ResultFileName);
            }, a => File.Exists(detectResult.ResultFileName));

            RelayCommand openCommand = new RelayCommand(a =>
            {
                AvalonEditWindow avalonEditWindow = new AvalonEditWindow(detectResult.ResultFileName)
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                avalonEditWindow.ShowDialog();
            }, a => File.Exists(detectResult.ResultFileName));

            result.ContextMenu.Items.Add(new MenuItem() { Header = "选中屏幕缺陷检测结果", Command = selectCommand });
            result.ContextMenu.Items.Add(new MenuItem() { Header = "打开屏幕缺陷检测结果", Command = openCommand });
            result.ContextMenu.Items.Add(new MenuItem() { Header = "调试", Command = new RelayCommand(a => DisplayAlgorithmManager.GetInstance().SetType(new DisplayAlgorithmParam() { Type = typeof(AlgorithmDetectScreenDefects), ImageFilePath = result.FilePath })) });
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults?.Count != 1 || result.ViewResults[0] is not DetectScreenDefectsDetailViewResult detectView)
                return;

            var defects = detectView.DetectResult?.Defects ?? new List<DetectScreenDefectItem>();
            foreach (var item in defects)
                DrawDefect(ctx, item);

            if (ctx.ListView.View is GridView gridView)
            {
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                AddColumn(gridView, "id", nameof(DetectScreenDefectItem.Id));
                AddColumn(gridView, "type", nameof(DetectScreenDefectItem.Type));
                AddColumn(gridView, "x", nameof(DetectScreenDefectItem.X));
                AddColumn(gridView, "y", nameof(DetectScreenDefectItem.Y));
                AddColumn(gridView, "width", nameof(DetectScreenDefectItem.Width));
                AddColumn(gridView, "height", nameof(DetectScreenDefectItem.Height));
                AddColumn(gridView, "area", nameof(DetectScreenDefectItem.Area));
                AddColumn(gridView, "contrast", nameof(DetectScreenDefectItem.Contrast));
                AddColumn(gridView, "mean_value", nameof(DetectScreenDefectItem.MeanValue));
                AddColumn(gridView, "local_mean", nameof(DetectScreenDefectItem.LocalMean));
                ctx.ListView.ItemsSource = defects;
            }
        }

        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            var viewResults = result.ViewResults.ToSpecificViewResults<DetectScreenDefectsDetailViewResult>();
            if (viewResults.Count != 1)
                return;

            var detectResult = viewResults[0].DetectResult;
            if (detectResult == null)
                return;

            string filePath = Path.Combine(selectedPath, $"{result.Batch}{result.ResultType}.csv");
            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine($"GradeLevel,{detectResult.GradeLevel}");
            csvBuilder.AppendLine($"DefectCount,{detectResult.DefectCount}");
            csvBuilder.AppendLine($"AvgBrightness,{RenderConfig.FormatNumber(detectResult.AvgBrightness)}");
            csvBuilder.AppendLine($"TimeStamp,{detectResult.TimeStamp}");
            csvBuilder.AppendLine("id,type,x,y,width,height,area,contrast,mean_value,local_mean");
            foreach (var item in detectResult.Defects)
            {
                csvBuilder.AppendLine($"{item.Id},{item.Type},{item.X},{item.Y},{item.Width},{item.Height},{item.Area},{RenderConfig.FormatNumber(item.Contrast)},{RenderConfig.FormatNumber(item.MeanValue)},{RenderConfig.FormatNumber(item.LocalMean)}");
            }
            File.AppendAllText(filePath, csvBuilder.ToString(), Encoding.UTF8);
        }

        private void DrawDefect(ViewResultContext ctx, DetectScreenDefectItem item)
        {
            DVRectangleText rectangle = new();
            rectangle.Attribute.Rect = new Rect(item.X, item.Y, item.Width, item.Height);
            rectangle.Attribute.Brush = Brushes.Transparent;
            rectangle.Attribute.Pen = new Pen(GetDefectBrush(item.Type), RenderConfig.PenThickness);
            rectangle.Attribute.Id = item.Id;
            rectangle.Attribute.Text = item.Id.ToString();
            rectangle.Attribute.FontSize = RenderConfig.FontSize;
            rectangle.Attribute.Msg =
                $"type:{item.Type}{Environment.NewLine}" +
                $"area:{RenderConfig.FormatNumber(item.Area)}{Environment.NewLine}" +
                $"contrast:{RenderConfig.FormatNumber(item.Contrast)}{Environment.NewLine}" +
                $"mean:{RenderConfig.FormatNumber(item.MeanValue)}{Environment.NewLine}" +
                $"local:{RenderConfig.FormatNumber(item.LocalMean)}";
            rectangle.Render();
            ctx.ImageView.AddVisual(rectangle);
        }

        private static Brush GetDefectBrush(string? defectType)
        {
            return string.Equals(defectType, "line", System.StringComparison.OrdinalIgnoreCase)
                ? Brushes.OrangeRed
                : Brushes.Red;
        }

        private static void AddColumn(GridView gridView, string header, string bindingPath)
        {
            gridView.Columns.Add(new GridViewColumn() { Header = header, DisplayMemberBinding = new Binding(bindingPath) });
        }
    }
}
