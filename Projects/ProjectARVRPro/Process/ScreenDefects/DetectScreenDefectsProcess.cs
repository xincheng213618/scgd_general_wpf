using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.DetectScreenDefects;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.ScreenDefects
{
    public sealed class DetectScreenDefectsProcess : ProcessBase<DetectScreenDefectsProcessConfig>
    {
        internal Func<int, List<AlgResultMasterModel>>? ReadMasters { get; init; }
        internal Func<int, List<DetailCommonModel>?>? ReadDetails { get; init; }

        public override Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Result == null || ctx.ObjectiveTestResult == null)
                return Task.FromResult(false);

            try
            {
                if (ctx.Batch == null)
                    throw new InvalidDataException("缺少测量批次。");

                SetPreviewFile(ctx);
                ScreenDefectsData result = LoadLatestResult(ctx);
                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(result, Formatting.None, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                ctx.ObjectiveTestResult.DynamicScreenDefectResults ??= new Dictionary<string, ScreenDefectsData>();
                ctx.ObjectiveTestResult.DynamicScreenDefectResults[GetOutputName()] = result;

                ctx.Result.Result &= result.DefectCount == 0;
                ctx.ObjectiveTestResult.TotalResult &= ctx.Result.Result;

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                string message = $"屏幕缺陷检测解析失败 [{GetOutputName()}]：{ex.Message}";
                ctx.Log.Error(message, ex);
                Reject(ctx, message);
                return Task.FromResult(false);
            }
        }

        public override Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
        {
            if (ctx?.Result != null && ctx.ObjectiveTestResult != null)
                Reject(ctx, string.IsNullOrWhiteSpace(ctx.Result.Msg) ? "屏幕缺陷检测流程未成功完成。" : ctx.Result.Msg);
            return Task.FromResult(false);
        }

        private void Reject(IProcessExecutionContext ctx, string message)
        {
            ctx.Result.Result = false;
            ctx.Result.Msg = message;
            ctx.Result.ViewResultJson = string.Empty;
            ctx.ObjectiveTestResult.TotalResult = false;
            ctx.ObjectiveTestResult.DynamicScreenDefectResults?.Remove(GetOutputName());
        }

        public override void Render(IProcessExecutionContext ctx)
        {
            ScreenDefectsData? result = DeserializeResult(ctx.Result?.ViewResultJson);
            if (result == null)
                return;

            foreach (ScreenDefectData defect in result.Defects)
                DrawDefect(ctx, defect);
        }

        public override void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, Brush foreground, double fontSize)
        {
            ScreenDefectsData? result = DeserializeResult(ctx.Result?.ViewResultJson);
            AppendPlainText(paragraph, BuildText(GetOutputName(), result, Config.ShowConfig), foreground, fontSize);
        }

        public static ScreenDefectsData CreateCleanResult(DetectScreenDefectsResult source)
        {
            ArgumentNullException.ThrowIfNull(source);

            var result = new ScreenDefectsData
            {
                AvgBrightness = source.AvgBrightness,
                DefectCount = source.DefectCount,
                GradeLevel = NullIfWhiteSpace(source.GradeLevel),
                TimeStamp = NullIfWhiteSpace(source.TimeStamp)
            };

            List<DetectScreenDefectItem> sourceDefects = source.Defects ?? new List<DetectScreenDefectItem>();
            for (int index = 0; index < sourceDefects.Count; index++)
            {
                DetectScreenDefectItem sourceDefect = sourceDefects[index];
                result.Defects.Add(new ScreenDefectData
                {
                    Id = index + 1,
                    Type = sourceDefect.Type ?? string.Empty,
                    X = sourceDefect.X,
                    Y = sourceDefect.Y,
                    Width = sourceDefect.Width,
                    Height = sourceDefect.Height,
                    Area = sourceDefect.Area,
                    Contrast = sourceDefect.Contrast,
                    MeanValue = sourceDefect.MeanValue,
                    LocalMean = sourceDefect.LocalMean
                });
            }

            return result;
        }

        public static string BuildText(string outputName, ScreenDefectsData? result, string? numberFormat = "F4")
        {
            var text = new StringBuilder();
            text.AppendLine($"{outputName} 屏幕缺陷检测结果");
            if (result == null)
                return text.ToString();

            text.AppendLine($"AvgBrightness:{Format(result.AvgBrightness, numberFormat)}");
            text.AppendLine($"DefectCount:{result.DefectCount}");
            if (!string.IsNullOrWhiteSpace(result.GradeLevel))
                text.AppendLine($"GradeLevel:{result.GradeLevel}");
            if (!string.IsNullOrWhiteSpace(result.TimeStamp))
                text.AppendLine($"TimeStamp:{result.TimeStamp}");

            text.AppendLine("Id,Type,X,Y,Width,Height,Area,Contrast,MeanValue,LocalMean");
            foreach (ScreenDefectData defect in result.Defects)
            {
                text.AppendLine(string.Join(",",
                    defect.Id,
                    defect.Type,
                    Format(defect.X, numberFormat),
                    Format(defect.Y, numberFormat),
                    Format(defect.Width, numberFormat),
                    Format(defect.Height, numberFormat),
                    Format(defect.Area, numberFormat),
                    Format(defect.Contrast, numberFormat),
                    Format(defect.MeanValue, numberFormat),
                    Format(defect.LocalMean, numberFormat)));
            }

            return text.ToString();
        }

        private ScreenDefectsData LoadLatestResult(IProcessExecutionContext ctx)
        {
            var master = (ReadMasters?.Invoke(ctx.Batch.Id) ?? ctx.ReadMasterResults())
                .Where(master => master.ImgFileType == ViewResultAlgType.ARVR_DetectScreenDefects && ShouldParse(master))
                .MaxBy(master => master.Id)
                ?? throw new InvalidDataException($"批次 {ctx.Batch.Id} 缺少匹配的屏幕缺陷检测结果。");

            if (!string.IsNullOrWhiteSpace(master.ImgFile))
                ctx.Result.FileName = master.ImgFile;

            var details = (ReadDetails != null ? ReadDetails(master.Id) : ctx.TryReadDetails(DeatilCommonDao.Instance, master.Id))
                ?? throw new InvalidDataException($"无法读取屏幕缺陷检测结果 {master.Id} 的明细。");
            var detail = details.MaxBy(detail => detail.Id)
                ?? throw new InvalidDataException($"屏幕缺陷检测结果 {master.Id} 缺少明细。");
            try
            {
                var parsed = ScreenDefectsResultParser.Parse(detail.ResultJson, out string? file);
                if (parsed == null)
                    throw new InvalidDataException(file == null ? "结果内容为空。" : $"结果文件无内容或无法读取：{file}");
                return CreateCleanResult(parsed);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"结果 {master.Id} 明细 {detail.Id}：{ex.Message}", ex);
            }
        }

        private bool ShouldParse(AlgResultMasterModel master)
        {
            if (string.IsNullOrWhiteSpace(Config.TemplateName))
                return true;

            return !string.IsNullOrWhiteSpace(master.TName)
                && master.TName.Contains(Config.TemplateName, StringComparison.OrdinalIgnoreCase);
        }

        private string GetOutputName()
        {
            return string.IsNullOrWhiteSpace(Config.Name) ? "ScreenDefects" : Config.Name.Trim();
        }

        private static ScreenDefectsData? DeserializeResult(string? json)
        {
            return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<ScreenDefectsData>(json);
        }

        private static void DrawDefect(IProcessExecutionContext ctx, ScreenDefectData defect)
        {
            if (!ProcessExtensions.TryCreateOverlayRect(defect.X, defect.Y, defect.Width, defect.Height, out Rect overlayRect))
                return;

            DVRectangleText rectangle = new(new RectangleTextProperties
            {
                Rect = overlayRect,
                Brush = Brushes.Transparent,
                Pen = new Pen(GetDefectBrush(defect.Type), 1),
                Id = defect.Id,
                Text = defect.Id.ToString(CultureInfo.InvariantCulture),
                Msg =
                    $"type:{defect.Type}{Environment.NewLine}" +
                    $"area:{Format(defect.Area, "F4")}{Environment.NewLine}" +
                    $"contrast:{Format(defect.Contrast, "F4")}{Environment.NewLine}" +
                    $"mean:{Format(defect.MeanValue, "F4")}{Environment.NewLine}" +
                    $"local:{Format(defect.LocalMean, "F4")}",
            });
            ctx.ImageView.AddVisual(rectangle);
        }

        private static SolidColorBrush GetDefectBrush(string? defectType)
        {
            return string.Equals(defectType, "line", StringComparison.OrdinalIgnoreCase) ? Brushes.OrangeRed : Brushes.Red;
        }

        private static string Format(double? value, string? numberFormat)
        {
            return value.HasValue ? Format(value.Value, numberFormat) : string.Empty;
        }

        private static string Format(double value, string? numberFormat)
        {
            string format = string.IsNullOrWhiteSpace(numberFormat) ? "F4" : numberFormat;
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string? NullIfWhiteSpace(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static void SetPreviewFile(IProcessExecutionContext ctx)
        {
            string? fileName = ctx.GetMeasureResults().FirstOrDefault()?.FileUrl;
            if (!string.IsNullOrWhiteSpace(fileName))
                ctx.Result.FileName = fileName;
        }
    }
}
