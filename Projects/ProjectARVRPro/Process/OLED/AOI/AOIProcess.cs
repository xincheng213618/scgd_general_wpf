using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using ProjectARVRPro.Fix;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.OLED.AOI
{
    public class AOIProcess : ProcessBase<AoIProcessConfig>
    {
        public override IFixConfig GetFixConfig()=> Config.FixConfig;
        public override IRecipeConfig GetRecipeConfig() => Config.RecipeConfig;


        public override bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            AoiViewTestResult testResult = new AoiViewTestResult();

            try
            {
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters.Where(m => m.ImgFileType == ViewResultAlgType.MTF && m.version == "2.0"))
                {
                    var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                    if (details.Count == 1)
                    {
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
