using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.FileIO;
using Newtonsoft.Json;
using OpenCvSharp;
using SqlSugar;
using SqlSugar.Extensions;
using log4net;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectARVRPro.Process.AOI
{
    public class AOIProcess : ProcessBase<AoIProcessConfig>
    {
        public override IRecipeConfig GetRecipeConfig() => Config.RecipeConfig;

        private static readonly Regex FindDotsArrayFailureRegex = new(@"findDotsArray\s+return\s+fail(?:e)?d\s*[\(（]\s*(?<code>-?\d+)\s*[\)）]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Dictionary<int, (string Name, string Message)> CVOledErrors = new Dictionary<int, (string Name, string Message)>
        {
            [0] = ("CVLED_SUCCESS", "成功"),
            [1] = ("CVLED_PARAM_E", "参数错误"),
            [2] = ("CVLED_INPUT_E", "输入错误"),
            [3] = ("CVLED_SCRREN_NOT_SUPPORT", "屏幕类型不支持(预留)"),
            [4] = ("CVLED_INIT_E", "初始化错误"),
            [5] = ("SAVE_E", "保存文件错误"),
            [6] = ("OUT_OF_BOUNDRY", "越界"),
            [7] = ("ALGORITHM_E", "算法错误"),
            [8] = ("MORIE_E", "摩尔纹"),
            [9] = ("PARTICLE_E", "灰尘检测错误"),
            [10] = ("EXT_LIGHT_E", "侧光点亮异常"),
            [11] = ("FOCUS_E", "清晰度异常/聚焦异常"),
            [12] = ("AAROT_E", "AA区平转"),
            [13] = ("PERPENDICULAR_E", "垂直不佳"),
            [14] = ("PATTERN_E", "显示画面错误"),
            [15] = ("DONGLE_E", "加密狗缺失"),
            [16] = ("BLACKSCREEN_E", "黑屏错误(屏幕未点亮)"),
            [17] = ("VH_LINE_E", "横竖线缺陷"),
            [18] = ("CONSISTANT_STAIN_E", "固定位置缺陷"),
            [19] = ("MURA_E", "Mura缺陷"),
            [20] = ("PARTICLE_WARN", "灰尘检测警告(非致命)"),
            [21] = ("POSITION_QUALITY_E", "定位质量差"),
            [22] = ("CVLED_BUILD_E", "提取/重建错误"),
            [23] = ("FILE_NOT_FOUND_E", "文件不存在(预留)"),
            [24] = ("FILE_FORMAT_E", "文件格式错误(预留)"),
            [25] = ("JSON_PARSE_E", "JSON解析错误(预留)"),
            [26] = ("IMAGE_FORMAT_E", "图片格式不支持(预留)")
        };

        private static string? TryFormatFailureMessage(string message)
        {
            Match match = FindDotsArrayFailureRegex.Match(message);
            if (!match.Success || !int.TryParse(match.Groups["code"].Value, out int oledErrorCode))
                return null;

            if (!CVOledErrors.TryGetValue(oledErrorCode, out var oledError))
                oledError = ("UNKNOWN_CVOLED_ERROR", "未知CVOLED错误码");

            string detail = $"检测失败: {message}; 错误码: {oledErrorCode}, 枚举: {oledError.Name}, 错误信息: {oledError.Message}";
            return detail;
        }

        public override async Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Log;
            AoiViewTestResult testResult = new AoiViewTestResult();

            try
            {
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                {
                    string? firstFileUrl = values[0].FileUrl;
                    if (!string.IsNullOrWhiteSpace(firstFileUrl))
                    {
                        ctx.Result.FileName = firstFileUrl;
                    }
                }

                string exportDir = GetExportDirectory(ctx.Batch.Name ?? ctx.Batch.Id.ToString());
                if (Config.ExportOriginalImage)
                {
                    ExportOriginalImages(values, exportDir, Config.ExportOriginalAsTif, log);
                }

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);


                foreach (var master in masters)
                {

                    if (master.ImgFileType == ViewResultAlgType.ImageConvert)
                    {
                        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                        var list = db.Queryable<AlgResultImageModel>().Where(it => it.Pid == master.Id).ToList();

                        foreach (var imageModel in list)
                        {
                            if (File.Exists(imageModel.FileName))
                            {
                                log.Info("正在复制 " + imageModel.FileName);
                                string destFile = Path.Combine(exportDir, Path.GetFileName(imageModel.FileName));
                                File.Copy(imageModel.FileName, destFile, true);
                            }
                        }
                    }
                    if (master.ImgFileType == ViewResultAlgType.OLED_CombineQuaterImages)
                    {
                        try
                        {
                            log.Info("正在复制 " + master.ResultImagFile);
                            string imgPath = Path.Combine(exportDir, Path.GetFileName(master.ResultImagFile));
                            File.Copy(master.ResultImagFile, imgPath, true);
                        }
                        catch(Exception ex)
                        {
                            log.Error(ex);
                        }
                       
                    }

                    if (Config.ExportCieFile && master.ImgFileType == ViewResultAlgType.OLED_RebuildPixelsMem)
                    {
                        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                        var list = db.Queryable<AlgResultPoiCieFileModel>().Where(it => it.Pid == master.Id).ToList();

                        foreach (var ciefile in list)
                        {
                            if (File.Exists(ciefile.FileUrl))
                            {
                                log.Info("正在复制 " + ciefile.FileUrl);
                                string destFile = Path.Combine(exportDir, Path.GetFileName(ciefile.FileUrl));
                                File.Copy(ciefile.FileUrl, destFile, true);
                            }

                        }
                    }
                }






                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);

                // Add to ObjectiveTestResult via dynamic dictionary
                if (!ctx.ObjectiveTestResult.DynamicTestResults.TryGetValue(Config.Name, out var dynamicItems))
                {
                    dynamicItems = new ObservableCollection<ObjectiveTestItem>();
                    ctx.ObjectiveTestResult.DynamicTestResults[Config.Name] = dynamicItems;
                }
                ctx.ObjectiveTestResult.DynamicTestResults[Config.Name].Clear();

                foreach (var item in testResult.Items)
                {
                    dynamicItems.Add(item);
                }

                return true;
            }
            catch (Exception ex)
            {
                log?.Error(ex);
                return false;
            }
        }

        public override async Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
        {
            if (ctx?.Result == null || ctx.ObjectiveTestResult == null) return false;

            string rawMessage = ctx.Result.Msg;
            string detail = TryFormatFailureMessage(rawMessage) ?? (string.IsNullOrWhiteSpace(rawMessage) ? "AOI流程失败" : rawMessage);
            AoiViewTestResult testResult = new AoiViewTestResult();
            testResult.Items.Add(new ObjectiveTestItem
            {
                Name = "AOIFailure",
                TestValue = detail,
                Value = 0,
                LowLimit = 1,
                UpLimit = 1,
                Unit = string.Empty
            });

            if (ctx.Batch?.Id > 0)
            {
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                string? firstFileUrl = values.FirstOrDefault()?.FileUrl;
                if (!string.IsNullOrWhiteSpace(firstFileUrl))
                    ctx.Result.FileName = firstFileUrl;
            }

            ctx.Result.Result = false;
            ctx.Result.Msg = detail;
            ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
            ctx.ObjectiveTestResult.TotalResult = false;
            ctx.ObjectiveTestResult.Msg = detail;

            if (!ctx.ObjectiveTestResult.DynamicTestResults.TryGetValue(Config.Name, out var dynamicItems))
            {
                dynamicItems = new ObservableCollection<ObjectiveTestItem>();
                ctx.ObjectiveTestResult.DynamicTestResults[Config.Name] = dynamicItems;
            }

            dynamicItems.Clear();
            foreach (var item in testResult.Items)
                dynamicItems.Add(item);

            return true;
        }

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            AoiViewTestResult testResult = JsonConvert.DeserializeObject<AoiViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

        }

        public override void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, System.Windows.Media.Brush foreground, double fontSize)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{Config.Name} 画面结果");

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) { AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return; }

            AoiTestResult testResult = JsonConvert.DeserializeObject<AoiTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) { AppendPlainText(paragraph, sb.ToString(), foreground, fontSize); return; }

            sb.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");

            foreach (var item in testResult.Items)
            {
                sb.AppendLine($"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{item.TestResult}");
            }

            AppendPlainText(paragraph, sb.ToString(), foreground, fontSize);
        }

        private static string GetExportDirectory(string batchName)
        {
            string dir = Path.Combine(ViewResultManager.GetInstance().Config.CsvSavePath, batchName);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        private static void ExportOriginalImages(IEnumerable<MeasureResultImgModel> values, string exportDir, bool exportOriginalAsTif, ILog log)
        {
            foreach (var value in values)
            {
                try
                {
                    ExportOriginalImage(value.FileUrl, exportDir, exportOriginalAsTif, log);
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }
        }

        private static void ExportOriginalImage(string? fileUrl, string exportDir, bool exportOriginalAsTif, ILog log)
        {
            if (string.IsNullOrWhiteSpace(fileUrl) || !File.Exists(fileUrl))
            {
                return;
            }

            if (exportOriginalAsTif && IsCvRawFile(fileUrl))
            {
                ExportCvRawAsTif(fileUrl, exportDir, log);
                return;
            }

            string destFile = Path.Combine(exportDir, Path.GetFileName(fileUrl));
            if (PathsEqual(fileUrl, destFile))
            {
                return;
            }

            log.Info("正在复制原图 " + fileUrl);
            File.Copy(fileUrl, destFile, true);
        }

        private static void ExportCvRawAsTif(string fileUrl, string exportDir, ILog log)
        {
            using var cvFile = CVFileUtil.OpenLocalCVFile(fileUrl);
            var src = cvFile.ToMat();
            if (src == null || src.Empty())
            {
                src?.Dispose();
                log.Warn("原图转TIF失败，已跳过 " + fileUrl);
                return;
            }

            using (src)
            {
                string destFile = Path.Combine(exportDir, Path.GetFileNameWithoutExtension(fileUrl) + ".tif");
                log.Info("正在输出TIF原图 " + destFile);
                src.SaveImage(destFile, new ImageEncodingParam(ImwriteFlags.TiffCompression, 1));
            }
        }

        private static bool IsCvRawFile(string fileUrl)
        {
            return string.Equals(Path.GetExtension(fileUrl), ".cvraw", StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
    }
}
