using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using Newtonsoft.Json;
using SqlSugar;
using SqlSugar.Extensions;
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
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);


                foreach (var master in masters)
                {

                    if (master.ImgFileType == ViewResultAlgType.ImageConvert)
                    {
                        string Dir = Path.Combine(ViewResultManager.GetInstance().Config.CsvSavePath, ctx.Batch.Name);
                        if (!Directory.Exists(Dir))
                        {
                            Directory.CreateDirectory(Dir);
                        }

                        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                        var list = db.Queryable<AlgResultImageModel>().Where(it => it.Pid == master.Id).ToList();

                        foreach (var imageModel in list)
                        {
                            if (File.Exists(imageModel.FileName))
                            {
                                log.Info("正在复制 " + imageModel.FileName);
                                string destFile = Path.Combine(Dir, Path.GetFileName(imageModel.FileName));
                                File.Copy(imageModel.FileName, destFile, true);
                            }
                        }
                    }
                    if (master.ImgFileType == ViewResultAlgType.OLED_CombineQuaterImages)
                    {
                        try
                        {
                            log.Info("正在复制 " + master.ResultImagFile);
                            string Dir = Path.Combine(ViewResultManager.GetInstance().Config.CsvSavePath, ctx.Batch.Name);
                            if (!Directory.Exists(Dir))
                            {
                                Directory.CreateDirectory(Dir);
                            }
                            string imgPath = Path.Combine(Dir, Path.GetFileName(master.ResultImagFile));
                            File.Copy(master.ResultImagFile, imgPath, true);
                        }
                        catch(Exception ex)
                        {
                            log.Error(ex);
                        }
                       
                    }

                    if (master.ImgFileType == ViewResultAlgType.OLED_RebuildPixelsMem)
                    {
                        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                        var list = db.Queryable<AlgResultPoiCieFileModel>().Where(it => it.Pid == master.Id).ToList();

                        foreach (var ciefile in list)
                        {
                            if (File.Exists(ciefile.FileUrl))
                            {
                                log.Info("正在复制 " + ciefile.FileUrl);
                                string Dir = Path.Combine(ViewResultManager.GetInstance().Config.CsvSavePath, ctx.Batch.Name);
                                if (!Directory.Exists(Dir))
                                {
                                    Directory.CreateDirectory(Dir);
                                }
                                string destFile = Path.Combine(Dir, Path.GetFileName(ciefile.FileUrl));
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
    }
}
