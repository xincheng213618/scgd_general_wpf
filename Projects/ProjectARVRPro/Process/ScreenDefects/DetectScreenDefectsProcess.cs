using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.DetectScreenDefects;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.ScreenDefects
{
    public sealed class DetectScreenDefectsProcess : ProcessBase<DetectScreenDefectsProcessConfig>
    {
        public override Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null || ctx.ObjectiveTestResult == null)
                return Task.FromResult(false);

            try
            {
                SetPreviewFile(ctx);
                ScreenDefectsData result = LoadLatestResult(ctx);
                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(result, Formatting.None, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                ctx.ObjectiveTestResult.DynamicScreenDefectResults ??= new Dictionary<string, ScreenDefectsData>();
                ctx.ObjectiveTestResult.DynamicScreenDefectResults[GetOutputName()] = result;
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                ctx.Log?.Error("屏幕缺陷检测结果解析异常", ex);
                return Task.FromResult(false);
            }
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
            IEnumerable<AlgResultMasterModel> masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id)
                .Where(master => master.ImgFileType == ViewResultAlgType.ARVR_DetectScreenDefects && ShouldParse(master))
                .OrderByDescending(master => master.Id);

            foreach (AlgResultMasterModel master in masters)
            {
                if (!string.IsNullOrWhiteSpace(master.ImgFile))
                    ctx.Result.FileName = master.ImgFile;

                IEnumerable<DetailCommonModel> details = DeatilCommonDao.Instance.GetAllByPid(master.Id).OrderByDescending(detail => detail.Id);
                foreach (DetailCommonModel detail in details)
                {
                    try
                    {
                        DetectScreenDefectsResult? parsed = ScreenDefectsResultParser.Parse(detail.ResultJson, out _);
                        if (parsed != null)
                            return CreateCleanResult(parsed);
                    }
                    catch (Exception ex)
                    {
                        ctx.Log?.Warn($"屏幕缺陷检测明细解析失败，MasterId={master.Id}, DetailId={detail.Id}: {ex.Message}");
                    }
                }
            }

            return new ScreenDefectsData();
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
            var rectangle = new DVRectangleText();
            rectangle.Attribute.Rect = new Rect(defect.X, defect.Y, defect.Width, defect.Height);
            rectangle.Attribute.Brush = Brushes.Transparent;
            rectangle.Attribute.Pen = new Pen(GetDefectBrush(defect.Type), 1);
            rectangle.Attribute.Id = defect.Id;
            rectangle.Attribute.Text = defect.Id.ToString(CultureInfo.InvariantCulture);
            rectangle.Attribute.Msg =
                $"type:{defect.Type}{Environment.NewLine}" +
                $"area:{Format(defect.Area, "F4")}{Environment.NewLine}" +
                $"contrast:{Format(defect.Contrast, "F4")}{Environment.NewLine}" +
                $"mean:{Format(defect.MeanValue, "F4")}{Environment.NewLine}" +
                $"local:{Format(defect.LocalMean, "F4")}";
            rectangle.Render();
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
            string? fileName = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id).FirstOrDefault()?.FileUrl;
            if (!string.IsNullOrWhiteSpace(fileName))
                ctx.Result.FileName = fileName;
        }
    }
}
