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

namespace ProjectARVRPro.Process.AOI
{
    public class AOIProcess : ProcessBase<AoIProcessConfig>
    {
        public override IRecipeConfig GetRecipeConfig() => Config.RecipeConfig;


        public override bool Execute(IProcessExecutionContext ctx)
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

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            AoiViewTestResult testResult = JsonConvert.DeserializeObject<AoiViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

        }

        public override string GenText(IProcessExecutionContext ctx)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{Config.Name} 画面结果");

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return sb.ToString();

            AoiTestResult testResult = JsonConvert.DeserializeObject<AoiTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return sb.ToString();

            sb.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");

            foreach (var item in testResult.Items)
            {
                sb.AppendLine($"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{item.TestResult}");
            }

            return sb.ToString();
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
