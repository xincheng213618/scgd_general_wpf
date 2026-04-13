using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using ProjectARVRPro.Fix;
using SqlSugar;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.MTFV
{
    /// <summary>
    /// 旧版MTFV解析 - 使用 MTFResult.result + mtfValue
    /// </summary>
    public class MTFVProcess : ProcessBase<MTFVProcessConfig>
    {
        public override bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            MTFVRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<MTFVRecipeConfig>();
            MTFVFixConfig fixConfig = ctx.FixConfig.GetRequiredService<MTFVFixConfig>();
            MTFVViewTestResult testResult = new MTFVViewTestResult();

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
                        var mtfDetail = new MTFDetailViewReslut(details[0]);
                        foreach (var mtf in mtfDetail.MTFResult.result)
                        {
                            if (mtf.name == Config.Key_Center_0F)
                            {
                                testResult.MTF_V_Center_0F.Value = mtf.mtfValue ?? 0;
                                testResult.MTF_V_Center_0F.Value *= fixConfig.MTF_V_Center_0F;
                                testResult.MTF_V_Center_0F.TestValue = testResult.MTF_V_Center_0F.Value.ToString();
                                testResult.MTF_V_Center_0F.LowLimit = recipeConfig.MTF_V_Center_0F.Min;
                                testResult.MTF_V_Center_0F.UpLimit = recipeConfig.MTF_V_Center_0F.Max;
                                ctx.Result.Result &= testResult.MTF_V_Center_0F.TestResult;
                            }
                            else if (mtf.name == Config.Key_LeftUp_0_5F)
                            {
                                testResult.MTF_V_LeftUp_0_5F.Value = mtf.mtfValue ?? 0;
                                testResult.MTF_V_LeftUp_0_5F.Value *= fixConfig.MTF_V_LeftUp_0_5F;
                                testResult.MTF_V_LeftUp_0_5F.TestValue = testResult.MTF_V_LeftUp_0_5F.Value.ToString();
                                testResult.MTF_V_LeftUp_0_5F.LowLimit = recipeConfig.MTF_V_LeftUp_0_5F.Min;
                                testResult.MTF_V_LeftUp_0_5F.UpLimit = recipeConfig.MTF_V_LeftUp_0_5F.Max;
                                ctx.Result.Result &= testResult.MTF_V_LeftUp_0_5F.TestResult;
                            }
                            else if (mtf.name == Config.Key_RightUp_0_5F)
                            {
                                testResult.MTF_V_RightUp_0_5F.Value = mtf.mtfValue ?? 0;
                                testResult.MTF_V_RightUp_0_5F.Value *= fixConfig.MTF_V_RightUp_0_5F;
                                testResult.MTF_V_RightUp_0_5F.TestValue = testResult.MTF_V_RightUp_0_5F.Value.ToString();
                                testResult.MTF_V_RightUp_0_5F.LowLimit = recipeConfig.MTF_V_RightUp_0_5F.Min;
                                testResult.MTF_V_RightUp_0_5F.UpLimit = recipeConfig.MTF_V_RightUp_0_5F.Max;
                                ctx.Result.Result &= testResult.MTF_V_RightUp_0_5F.TestResult;
                            }
                            else if (mtf.name == Config.Key_LeftDown_0_5F)
                            {
                                testResult.MTF_V_LeftDown_0_5F.Value = mtf.mtfValue ?? 0;
                                testResult.MTF_V_LeftDown_0_5F.Value *= fixConfig.MTF_V_LeftDown_0_5F;
                                testResult.MTF_V_LeftDown_0_5F.TestValue = testResult.MTF_V_LeftDown_0_5F.Value.ToString();
                                testResult.MTF_V_LeftDown_0_5F.LowLimit = recipeConfig.MTF_V_LeftDown_0_5F.Min;
                                testResult.MTF_V_LeftDown_0_5F.UpLimit = recipeConfig.MTF_V_LeftDown_0_5F.Max;
                                ctx.Result.Result &= testResult.MTF_V_LeftDown_0_5F.TestResult;
                            }
                            else if (mtf.name == Config.Key_RightDown_0_5F)
                            {
                                testResult.MTF_V_RightDown_0_5F.Value = mtf.mtfValue ?? 0;
                                testResult.MTF_V_RightDown_0_5F.Value *= fixConfig.MTF_V_RightDown_0_5F;
                                testResult.MTF_V_RightDown_0_5F.TestValue = testResult.MTF_V_RightDown_0_5F.Value.ToString();
                                testResult.MTF_V_RightDown_0_5F.LowLimit = recipeConfig.MTF_V_RightDown_0_5F.Min;
                                testResult.MTF_V_RightDown_0_5F.UpLimit = recipeConfig.MTF_V_RightDown_0_5F.Max;
                                ctx.Result.Result &= testResult.MTF_V_RightDown_0_5F.TestResult;
                            }
                            else if (mtf.name == Config.Key_LeftUp_0_8F)
                            {
                                testResult.MTF_V_LeftUp_0_8F.Value = mtf.mtfValue ?? 0;
                                testResult.MTF_V_LeftUp_0_8F.Value *= fixConfig.MTF_V_LeftUp_0_8F;
                                testResult.MTF_V_LeftUp_0_8F.TestValue = testResult.MTF_V_LeftUp_0_8F.Value.ToString();
                                testResult.MTF_V_LeftUp_0_8F.LowLimit = recipeConfig.MTF_V_LeftUp_0_8F.Min;
                                testResult.MTF_V_LeftUp_0_8F.UpLimit = recipeConfig.MTF_V_LeftUp_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_V_LeftUp_0_8F.TestResult;
                            }
                            else if (mtf.name == Config.Key_RightUp_0_8F)
                            {
                                testResult.MTF_V_RightUp_0_8F.Value = mtf.mtfValue ?? 0;
                                testResult.MTF_V_RightUp_0_8F.Value *= fixConfig.MTF_V_RightUp_0_8F;
                                testResult.MTF_V_RightUp_0_8F.TestValue = testResult.MTF_V_RightUp_0_8F.Value.ToString();
                                testResult.MTF_V_RightUp_0_8F.LowLimit = recipeConfig.MTF_V_RightUp_0_8F.Min;
                                testResult.MTF_V_RightUp_0_8F.UpLimit = recipeConfig.MTF_V_RightUp_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_V_RightUp_0_8F.TestResult;
                            }
                            else if (mtf.name == Config.Key_LeftDown_0_8F)
                            {
                                testResult.MTF_V_LeftDown_0_8F.Value = mtf.mtfValue ?? 0;
                                testResult.MTF_V_LeftDown_0_8F.Value *= fixConfig.MTF_V_LeftDown_0_8F;
                                testResult.MTF_V_LeftDown_0_8F.TestValue = testResult.MTF_V_LeftDown_0_8F.Value.ToString();
                                testResult.MTF_V_LeftDown_0_8F.LowLimit = recipeConfig.MTF_V_LeftDown_0_8F.Min;
                                testResult.MTF_V_LeftDown_0_8F.UpLimit = recipeConfig.MTF_V_LeftDown_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_V_LeftDown_0_8F.TestResult;
                            }
                            else if (mtf.name == Config.Key_RightDown_0_8F)
                            {
                                testResult.MTF_V_RightDown_0_8F.Value = mtf.mtfValue ?? 0;
                                testResult.MTF_V_RightDown_0_8F.Value *= fixConfig.MTF_V_RightDown_0_8F;
                                testResult.MTF_V_RightDown_0_8F.TestValue = testResult.MTF_V_RightDown_0_8F.Value.ToString();
                                testResult.MTF_V_RightDown_0_8F.LowLimit = recipeConfig.MTF_V_RightDown_0_8F.Min;
                                testResult.MTF_V_RightDown_0_8F.UpLimit = recipeConfig.MTF_V_RightDown_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_V_RightDown_0_8F.TestResult;
                            }
                        }
                        testResult.MTFDetailViewReslut = mtfDetail;
                    }
                }

                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);

                // 存入DynamicTestResults
                var items = new ObservableCollection<ObjectiveTestItem>
                {
                    testResult.MTF_V_Center_0F,
                    testResult.MTF_V_LeftUp_0_5F,
                    testResult.MTF_V_RightUp_0_5F,
                    testResult.MTF_V_LeftDown_0_5F,
                    testResult.MTF_V_RightDown_0_5F,
                    testResult.MTF_V_LeftUp_0_8F,
                    testResult.MTF_V_RightUp_0_8F,
                    testResult.MTF_V_LeftDown_0_8F,
                    testResult.MTF_V_RightDown_0_8F
                };
                ctx.ObjectiveTestResult.DynamicTestResults["MTFV"] = items;

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
            MTFVViewTestResult testResult = JsonConvert.DeserializeObject<MTFVViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            int id = 0;
            if (testResult.MTFDetailViewReslut?.MTFResult?.result != null && testResult.MTFDetailViewReslut.MTFResult.result.Count != 0)
            {
                foreach (var item in testResult.MTFDetailViewReslut.MTFResult.result)
                {
                    id++;
                    DVRectangleText Rectangle = new();
                    Rectangle.Attribute.Rect = new Rect(item.x, item.y, item.w, item.h);
                    Rectangle.Attribute.Brush = Brushes.Transparent;
                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                    Rectangle.Attribute.Id = id;
                    Rectangle.Attribute.Msg = item.mtfValue?.ToString(Config.ShowConfig);
                    Rectangle.Render();
                    ctx.ImageView.AddVisual(Rectangle);
                }
            }
        }

        public override string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("MTFV(旧版) 画面结果");

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return sb.ToString();

            MTFVTestResult testResult = JsonConvert.DeserializeObject<MTFVTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return sb.ToString();

            sb.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");

            var properties = typeof(MTFVTestResult).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(ObjectiveTestItem));
            foreach (var prop in properties)
            {
                if (prop.GetValue(testResult) is ObjectiveTestItem item)
                {
                    sb.AppendLine($"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{item.TestResult}");
                }
            }

            return sb.ToString();
        }

        public override IRecipeConfig GetRecipeConfig()
        {
            return RecipeManager.GetInstance().RecipeConfig.GetRequiredService<MTFVRecipeConfig>();
        }

        public override IFixConfig GetFixConfig()
        {
            return FixManager.GetInstance().FixConfig.GetRequiredService<MTFVFixConfig>();
        }
    }
}
