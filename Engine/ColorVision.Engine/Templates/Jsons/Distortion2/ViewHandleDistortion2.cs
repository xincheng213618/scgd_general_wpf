#pragma warning disable CA1725

using ColorVision.Common.MVVM;using ColorVision.Engine.Services;
using ColorVision.Database;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using ColorVision.UI.Extension;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Templates.Jsons.Distortion2
{

    public class ViewHandleDistortion2 : IResultHandleBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ViewHandleDistortion2));
        private const string OverlayTag = "Engine.DistortionResult";
        private sealed class RenderState { public long Generation; }
        private static readonly ConditionalWeakTable<ImageView, RenderState> RenderStates = new();

        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>() { ViewResultAlgType.Distortion};
        public override bool CanHandle1(ViewResultAlg result)
        {
            if (result.Version != "2.0") return false;
            return base.CanHandle1(result);
        }
        public override void SideSave(ViewResultAlg result, string selectedPath)
        {
            // 添加日期时间戳到文件名（只到天）
            string dateStamp = DateTime.Now.ToString("yyyyMMdd");
            string fileName = System.IO.Path.Combine(selectedPath, $"{result.ResultType}_{dateStamp}.csv");

            var sb = new StringBuilder();

            // 检查文件是否存在以及是否已有标题
            bool fileExists = File.Exists(fileName);
            bool needHeader = !fileExists;

            if (fileExists)
            {
                // 读取文件检查是否已有标题
                var lines = File.ReadAllLines(fileName, Encoding.UTF8);
                if (lines.Length == 0 || !lines[0].Contains("OpticDistortion_OpticRatio"))
                {
                    needHeader = true;
                }
            }

            // 只在需要时写入标题
            if (needHeader)
            {
                sb.AppendLine("WriteTime,OpticDistortion_OpticRatio,OpticDistortion_T,OpticDistortion_Message,OpticDistortion_MaxErrPoint_X,OpticDistortion_MaxErrPoint_Y,Point9Distortion_TopRatio,Point9Distortion_BottomRatio,Point9Distortion_LeftRatio,Point9Distortion_RightRatio,Point9Distortion_KeyStoneHoriRatio,Point9Distortion_KeyStoneVercRatio,Point9Distortion_Message,TVDistortion_HorizontalRatio,TVDistortion_VerticalRatio,TVDistortion_Message");
            }

            // 获取当前时间用于记录
            string writeTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (var res in result.ViewResults.ToSpecificViewResults<Distortion2View>())
            {
                sb.AppendLine(string.Join(",",
                    writeTime,
                    res.DistortionReslut.OpticDistortion?.OpticRatio.ToString() ?? "",
                    GetOpticTText(res),
                    EscapeCsv(res.DistortionReslut.OpticDistortion?.Message),
                    res.DistortionReslut.OpticDistortion?.MaxErrPoint?.X.ToString() ?? "",
                    res.DistortionReslut.OpticDistortion?.MaxErrPoint?.Y.ToString() ?? "",
                    res.DistortionReslut.Point9Distortion?.TopRatio.ToString() ?? "",
                    res.DistortionReslut.Point9Distortion?.BottomRatio.ToString() ?? "",
                    res.DistortionReslut.Point9Distortion?.LeftRatio.ToString() ?? "",
                    res.DistortionReslut.Point9Distortion?.RightRatio.ToString() ?? "",
                    res.DistortionReslut.Point9Distortion?.KeyStoneHoriRatio.ToString() ?? "",
                    res.DistortionReslut.Point9Distortion?.KeyStoneVercRatio.ToString() ?? "",
                    EscapeCsv(res.DistortionReslut.Point9Distortion?.Message),
                    res.DistortionReslut.TVDistortion?.HorizontalRatio.ToString() ?? "",
                    res.DistortionReslut.TVDistortion?.VerticalRatio.ToString() ?? "",
                    EscapeCsv(res.DistortionReslut.TVDistortion?.Message)
                ));
            }
            File.AppendAllText(fileName, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        public override void Load(ViewResultContext ctx, ViewResultAlg result)
        {
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<DetailCommonModel> AlgResultModels = DeatilCommonDao.Instance.GetAllByPid(result.Id);
                foreach (var item in AlgResultModels)
                {
                    Distortion2View blackMuraView = new Distortion2View(item);
                    result.ViewResults.Add(blackMuraView);
                }
            }
        }

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            OpenSourceImage(ctx, result);
            log.Info(result.FilePath);

            List<string> header = new() { "Point9Distortion", "TVDistortion", "OpticDistortion" };
            List<string> bdHeader = new() { "DistortionReslut.Point9Distortion", "DistortionReslut.TVDistortion", "DistortionReslut.OpticDistortion" };

            if (ctx.ListView.View is GridView gridView)
            {
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
            }
            ctx.ListView.ItemsSource = result.ViewResults;

            ctx.SideTextBox.Visibility = System.Windows.Visibility.Visible;
            ctx.SideTextBox.Text = BuildResultText(result);
            RenderState state = RenderStates.GetOrCreateValue(ctx.ImageView);
            _ = RenderWhenReadyAsync(ctx, result, state, ++state.Generation);
        }

        private static async Task RenderWhenReadyAsync(ViewResultContext ctx, ViewResultAlg result, RenderState state, long generation)
        {
            var rows = result.ViewResults;
            string path = result.FilePath;
            try
            {
                Task sourceReady = ctx.ImageView.ImageOpenCompletion;
                await sourceReady;
                if (state.Generation != generation || !ReferenceEquals(ctx.ListView.ItemsSource, rows) || result.FilePath != path
                    || !ReferenceEquals(ctx.ImageView.ImageOpenCompletion, sourceReady)) return;
                if (File.Exists(path) && !string.Equals(ctx.ImageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath), path, StringComparison.OrdinalIgnoreCase)) return;
                var draw = ctx.ImageView.EditorContext.DrawEditorContext;
                BitmapSource? bitmap = ctx.ImageView.ViewBitmapSource as BitmapSource;
                AlgorithmResultOverlay.ClearTagged(draw, OverlayTag);
                foreach (Distortion2View item in rows.ToSpecificViewResults<Distortion2View>())
                {
                    var measured = item.DistortionReslut?.TVDistortion?.FinalPoints;
                    if (measured == null) continue;
                    (int gridRows, int gridColumns) = GetGridShape(item, measured.Count);
                    var points = measured.Select(point => new System.Windows.Point(point.X, point.Y)).ToArray();
                    bool Visible(System.Windows.Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y) && point.X >= 0 && point.Y >= 0
                        && (bitmap == null || point.X < bitmap.PixelWidth && point.Y < bitmap.PixelHeight);
                    if (points.All(Visible)) GridDistortionOverlayRenderer.AppendLines(draw, points, gridRows, gridColumns, OverlayTag);
                    for (int index = 0; index < measured.Count; index++)
                    {
                        var point = measured[index];
                        if (!Visible(points[index])) continue;
                        AlgorithmResultOverlay.AddLabel(draw, points[index], $"P{index + 1}\nX:{point.X:F1}\nY:{point.Y:F1}", Brushes.Red, OverlayTag);
                    }
                }
            }
            catch (Exception ex) { log.Warn("畸变结果叠图失败。", ex); }
        }

        internal static (int Rows, int Columns) GetGridShape(Distortion2View item, int count)
        {
            if (!string.IsNullOrWhiteSpace(item.Result))
            {
                try
                {
                    JObject? grid = JObject.Parse(item.Result).GetValue("GridDistortion", StringComparison.OrdinalIgnoreCase) as JObject;
                    JToken? rows = grid?.GetValue("ExpectedRows", StringComparison.OrdinalIgnoreCase);
                    JToken? columns = grid?.GetValue("ExpectedCols", StringComparison.OrdinalIgnoreCase);
                    if (rows?.Type == JTokenType.Integer && columns?.Type == JTokenType.Integer
                        && long.TryParse(rows.ToString(), out long r) && long.TryParse(columns.ToString(), out long c)
                        && r is >= 3 and <= 15 && c is >= 3 and <= 15 && r * c == count)
                        return ((int)r, (int)c);
                }
                catch (JsonException) { }
            }
            // Legacy nine-point results have a known row-major layout; do not guess larger grids.
            return count == 9 ? (3, 3) : (0, 0);
        }

        internal static string BuildResultText(ViewResultAlg result)
        {
            var results = result.ViewResults?.ToSpecificViewResults<Distortion2View>();
            if (results?.Count > 0 && results[0].DistortionReslut != null)
            {
                DistortionReslut value = results[0].DistortionReslut;
                StringBuilder text = new("畸变结果\n");
                string metricUnit = "";
                try
                {
                    if (JObject.Parse(results[0].Result ?? "{}").Value<string>("Units") == "percent") metricUnit = "%";
                }
                catch (JsonException) { }
                if (metricUnit.Length == 0) text.AppendLine("原始测量值，单位以原结果为准。");
                void Metric(string name, double? number, string? unit = null)
                {
                    if (number.HasValue && double.IsFinite(number.Value)) text.AppendLine($"{name}：{number.Value:F5} {unit ?? metricUnit}");
                }
                Metric("水平 TV", value.TVDistortion?.HorizontalRatio);
                Metric("垂直 TV", value.TVDistortion?.VerticalRatio);
                Metric("光学畸变", value.OpticDistortion?.OpticRatio);
                Metric("上畸变", value.Point9Distortion?.TopRatio);
                Metric("下畸变", value.Point9Distortion?.BottomRatio);
                Metric("左畸变", value.Point9Distortion?.LeftRatio);
                Metric("右畸变", value.Point9Distortion?.RightRatio);
                Metric("水平梯形", value.Point9Distortion?.KeyStoneHoriRatio);
                Metric("垂直梯形", value.Point9Distortion?.KeyStoneVercRatio);
                Metric("最大倾角", value.Analysis?.Geometry?.MaximumTiltDegrees, "°");
                Metric("最大边长差", value.Analysis?.Geometry?.MaximumEdgeLengthDifferencePercent);
                if (!string.IsNullOrWhiteSpace(value.OpticDistortion?.Message)) text.AppendLine(value.OpticDistortion.Message);
                text.AppendLine("\n原始结果：");
                text.Append(string.IsNullOrWhiteSpace(results[0].Result) ? value.ToJsonN() : results[0].Result);
                return text.ToString();
            }
            return $"{result.ResultDesc}{Environment.NewLine}{result.AlgResultMasterModel?.Params}".Trim();
        }

        internal static string GetOpticTText(Distortion2View result)
        {
            if (result.DistortionReslut?.OpticDistortion == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(result.Result))
            {
                try
                {
                    JObject raw = JObject.Parse(result.Result);
                    if (raw.Value<string>("Algorithm") == "GridDistortionV2"
                        && raw["Optic_Distortion"] is JObject optic && optic.Property("t") == null)
                        return string.Empty;
                }
                catch (JsonException)
                {
                    // Preserve legacy exports when only the typed result is available.
                }
            }
            return result.DistortionReslut.OpticDistortion.T.ToString();
        }

    }
}
