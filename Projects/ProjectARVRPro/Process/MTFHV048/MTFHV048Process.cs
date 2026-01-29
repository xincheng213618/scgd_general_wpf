using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2; // MTFDetailViewReslut
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using ProjectARVRPro.Fix;
using SqlSugar;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.MTFHV048
{
    public class MTFHV048Process : ProcessBase<MTFHV048ProcessConfig>
    {
        public override bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            MTFHV048RecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<MTFHV048RecipeConfig>();
            MTFHV048FixConfig fixConfig = ctx.FixConfig.GetRequiredService<MTFHV048FixConfig>();
            MTFHV048ViewTestResult testResult = new MTFHV048ViewTestResult();

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
                        foreach (var mtf in mtfDetail.MTFResult.resultChild)
                        {
                            if (mtf.name == Config.Key_Center_0F)
                            {
                                testResult.MTF_HV_H_Center_0F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_Center_0F.Value *= fixConfig.MTF048_H_Center_0F;
                                testResult.MTF_HV_H_Center_0F.TestValue = testResult.MTF_HV_H_Center_0F.Value.ToString();
                                testResult.MTF_HV_H_Center_0F.LowLimit = recipeConfig.MTF048_H_Center_0F.Min;
                                testResult.MTF_HV_H_Center_0F.UpLimit = recipeConfig.MTF048_H_Center_0F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_Center_0F.TestResult;

                                testResult.MTF_HV_V_Center_0F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_Center_0F.Value *= fixConfig.MTF048_V_Center_0F;
                                testResult.MTF_HV_V_Center_0F.TestValue = testResult.MTF_HV_V_Center_0F.Value.ToString();
                                testResult.MTF_HV_V_Center_0F.LowLimit = recipeConfig.MTF048_V_Center_0F.Min;
                                testResult.MTF_HV_V_Center_0F.UpLimit = recipeConfig.MTF048_V_Center_0F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_Center_0F.TestResult;
                            }
                            else if (mtf.name == Config.Key_LeftUp_0_4F)
                            {
                                testResult.MTF_HV_H_LeftUp_0_4F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_LeftUp_0_4F.Value *= fixConfig.MTF_HV_H_LeftUp_0_4F;
                                testResult.MTF_HV_H_LeftUp_0_4F.TestValue = testResult.MTF_HV_H_LeftUp_0_4F.Value.ToString();
                                testResult.MTF_HV_H_LeftUp_0_4F.LowLimit = recipeConfig.MTF_HV_H_LeftUp_0_4F.Min;
                                testResult.MTF_HV_H_LeftUp_0_4F.UpLimit = recipeConfig.MTF_HV_H_LeftUp_0_4F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_LeftUp_0_4F.TestResult;

                                testResult.MTF_HV_V_LeftUp_0_4F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_LeftUp_0_4F.Value *= fixConfig.MTF_HV_V_LeftUp_0_4F;
                                testResult.MTF_HV_V_LeftUp_0_4F.TestValue = testResult.MTF_HV_V_LeftUp_0_4F.Value.ToString();
                                testResult.MTF_HV_V_LeftUp_0_4F.LowLimit = recipeConfig.MTF_HV_V_LeftUp_0_4F.Min;
                                testResult.MTF_HV_V_LeftUp_0_4F.UpLimit = recipeConfig.MTF_HV_V_LeftUp_0_4F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_LeftUp_0_4F.TestResult;
                            }
                            else if (mtf.name == Config.Key_LeftDown_0_4F)
                            {
                                testResult.MTF_HV_H_LeftDown_0_4F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_LeftDown_0_4F.Value *= fixConfig.MTF_HV_H_LeftDown_0_4F;
                                testResult.MTF_HV_H_LeftDown_0_4F.TestValue = testResult.MTF_HV_H_LeftDown_0_4F.Value.ToString();
                                testResult.MTF_HV_H_LeftDown_0_4F.LowLimit = recipeConfig.MTF_HV_H_LeftDown_0_4F.Min;
                                testResult.MTF_HV_H_LeftDown_0_4F.UpLimit = recipeConfig.MTF_HV_H_LeftDown_0_4F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_LeftDown_0_4F.TestResult;

                                testResult.MTF_HV_V_LeftDown_0_4F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_LeftDown_0_4F.Value *= fixConfig.MTF_HV_V_LeftDown_0_4F;
                                testResult.MTF_HV_V_LeftDown_0_4F.TestValue = testResult.MTF_HV_V_LeftDown_0_4F.Value.ToString();
                                testResult.MTF_HV_V_LeftDown_0_4F.LowLimit = recipeConfig.MTF_HV_V_LeftDown_0_4F.Min;
                                testResult.MTF_HV_V_LeftDown_0_4F.UpLimit = recipeConfig.MTF_HV_V_LeftDown_0_4F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_LeftDown_0_4F.TestResult;
                            }
                            else if (mtf.name == Config.Key_RightDown_0_4F)
                            {
                                testResult.MTF_HV_H_RightDown_0_4F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_RightDown_0_4F.Value *= fixConfig.MTF_HV_H_RightDown_0_4F;
                                testResult.MTF_HV_H_RightDown_0_4F.TestValue = testResult.MTF_HV_H_RightDown_0_4F.Value.ToString();
                                testResult.MTF_HV_H_RightDown_0_4F.LowLimit = recipeConfig.MTF_HV_H_RightDown_0_4F.Min;
                                testResult.MTF_HV_H_RightDown_0_4F.UpLimit = recipeConfig.MTF_HV_H_RightDown_0_4F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_RightDown_0_4F.TestResult;

                                testResult.MTF_HV_V_RightDown_0_4F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_RightDown_0_4F.Value *= fixConfig.MTF_HV_V_RightDown_0_4F;
                                testResult.MTF_HV_V_RightDown_0_4F.TestValue = testResult.MTF_HV_V_RightDown_0_4F.Value.ToString();
                                testResult.MTF_HV_V_RightDown_0_4F.LowLimit = recipeConfig.MTF_HV_V_RightDown_0_4F.Min;
                                testResult.MTF_HV_V_RightDown_0_4F.UpLimit = recipeConfig.MTF_HV_V_RightDown_0_4F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_RightDown_0_4F.TestResult;
                            }
                            else if (mtf.name == Config.Key_RightUp_0_4F)
                            {
                                testResult.MTF_HV_H_RightUp_0_4F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_RightUp_0_4F.Value *= fixConfig.MTF_HV_H_RightUp_0_4F;
                                testResult.MTF_HV_H_RightUp_0_4F.TestValue = testResult.MTF_HV_H_RightUp_0_4F.Value.ToString();
                                testResult.MTF_HV_H_RightUp_0_4F.LowLimit = recipeConfig.MTF_HV_H_RightUp_0_4F.Min;
                                testResult.MTF_HV_H_RightUp_0_4F.UpLimit = recipeConfig.MTF_HV_H_RightUp_0_4F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_RightUp_0_4F.TestResult;

                                testResult.MTF_HV_V_RightUp_0_4F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_RightUp_0_4F.Value *= fixConfig.MTF_HV_V_RightUp_0_4F;
                                testResult.MTF_HV_V_RightUp_0_4F.TestValue = testResult.MTF_HV_V_RightUp_0_4F.Value.ToString();
                                testResult.MTF_HV_V_RightUp_0_4F.LowLimit = recipeConfig.MTF_HV_V_RightUp_0_4F.Min;
                                testResult.MTF_HV_V_RightUp_0_4F.UpLimit = recipeConfig.MTF_HV_V_RightUp_0_4F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_RightUp_0_4F.TestResult;
                            }
                            else if (mtf.name == Config.Key_LeftUp_0_8F)
                            {
                                testResult.MTF_HV_H_LeftUp_0_8F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_LeftUp_0_8F.Value *= fixConfig.MTF048_H_LeftUp_0_8F;
                                testResult.MTF_HV_H_LeftUp_0_8F.TestValue = testResult.MTF_HV_H_LeftUp_0_8F.Value.ToString();
                                testResult.MTF_HV_H_LeftUp_0_8F.LowLimit = recipeConfig.MTF048_H_LeftUp_0_8F.Min;
                                testResult.MTF_HV_H_LeftUp_0_8F.UpLimit = recipeConfig.MTF048_H_LeftUp_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_LeftUp_0_8F.TestResult;

                                testResult.MTF_HV_V_LeftUp_0_8F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_LeftUp_0_8F.Value *= fixConfig.MTF048_V_LeftUp_0_8F;
                                testResult.MTF_HV_V_LeftUp_0_8F.TestValue = testResult.MTF_HV_V_LeftUp_0_8F.Value.ToString();
                                testResult.MTF_HV_V_LeftUp_0_8F.LowLimit = recipeConfig.MTF048_V_LeftUp_0_8F.Min;
                                testResult.MTF_HV_V_LeftUp_0_8F.UpLimit = recipeConfig.MTF048_V_LeftUp_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_LeftUp_0_8F.TestResult;
                            }
                            else if (mtf.name == Config.Key_LeftDown_0_8F)
                            {
                                testResult.MTF_HV_H_LeftDown_0_8F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_LeftDown_0_8F.Value *= fixConfig.MTF048_H_LeftDown_0_8F;
                                testResult.MTF_HV_H_LeftDown_0_8F.TestValue = testResult.MTF_HV_H_LeftDown_0_8F.Value.ToString();
                                testResult.MTF_HV_H_LeftDown_0_8F.LowLimit = recipeConfig.MTF048_H_LeftDown_0_8F.Min;
                                testResult.MTF_HV_H_LeftDown_0_8F.UpLimit = recipeConfig.MTF048_H_LeftDown_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_LeftDown_0_8F.TestResult;

                                testResult.MTF_HV_V_LeftDown_0_8F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_LeftDown_0_8F.Value *= fixConfig.MTF048_V_LeftDown_0_8F;
                                testResult.MTF_HV_V_LeftDown_0_8F.TestValue = testResult.MTF_HV_V_LeftDown_0_8F.Value.ToString();
                                testResult.MTF_HV_V_LeftDown_0_8F.LowLimit = recipeConfig.MTF048_V_LeftDown_0_8F.Min;
                                testResult.MTF_HV_V_LeftDown_0_8F.UpLimit = recipeConfig.MTF048_V_LeftDown_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_LeftDown_0_8F.TestResult;
                            }
                            else if (mtf.name == Config.Key_RightDown_0_8F)
                            {
                                testResult.MTF_HV_H_RightDown_0_8F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_RightDown_0_8F.Value *= fixConfig.MTF048_H_RightDown_0_8F;
                                testResult.MTF_HV_H_RightDown_0_8F.TestValue = testResult.MTF_HV_H_RightDown_0_8F.Value.ToString();
                                testResult.MTF_HV_H_RightDown_0_8F.LowLimit = recipeConfig.MTF048_H_RightDown_0_8F.Min;
                                testResult.MTF_HV_H_RightDown_0_8F.UpLimit = recipeConfig.MTF048_H_RightDown_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_RightDown_0_8F.TestResult;

                                testResult.MTF_HV_V_RightDown_0_8F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_RightDown_0_8F.Value *= fixConfig.MTF048_V_RightDown_0_8F;
                                testResult.MTF_HV_V_RightDown_0_8F.TestValue = testResult.MTF_HV_V_RightDown_0_8F.Value.ToString();
                                testResult.MTF_HV_V_RightDown_0_8F.LowLimit = recipeConfig.MTF048_V_RightDown_0_8F.Min;
                                testResult.MTF_HV_V_RightDown_0_8F.UpLimit = recipeConfig.MTF048_V_RightDown_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_RightDown_0_8F.TestResult;
                            }
                            else if (mtf.name == Config.Key_RightUp_0_8F)
                            {
                                testResult.MTF_HV_H_RightUp_0_8F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_RightUp_0_8F.Value *= fixConfig.MTF048_H_RightUp_0_8F;
                                testResult.MTF_HV_H_RightUp_0_8F.TestValue = testResult.MTF_HV_H_RightUp_0_8F.Value.ToString();
                                testResult.MTF_HV_H_RightUp_0_8F.LowLimit = recipeConfig.MTF048_H_RightUp_0_8F.Min;
                                testResult.MTF_HV_H_RightUp_0_8F.UpLimit = recipeConfig.MTF048_H_RightUp_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_RightUp_0_8F.TestResult;

                                testResult.MTF_HV_V_RightUp_0_8F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_RightUp_0_8F.Value *= fixConfig.MTF048_V_RightUp_0_8F;
                                testResult.MTF_HV_V_RightUp_0_8F.TestValue = testResult.MTF_HV_V_RightUp_0_8F.Value.ToString();
                                testResult.MTF_HV_V_RightUp_0_8F.LowLimit = recipeConfig.MTF048_V_RightUp_0_8F.Min;
                                testResult.MTF_HV_V_RightUp_0_8F.UpLimit = recipeConfig.MTF048_V_RightUp_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_RightUp_0_8F.TestResult;
                            }
                        }
                        testResult.MTFDetailViewReslut = mtfDetail;
                    }
                }


                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.MTFHV048TestResults.Add(JsonConvert.DeserializeObject<MTFHV048TestResult>(ctx.Result.ViewResultJson) ?? new MTFHV048TestResult());
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
            MTFHV048ViewTestResult testResult = JsonConvert.DeserializeObject<MTFHV048ViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            int id = 0;
            if (testResult.MTFDetailViewReslut.MTFResult.result.Count != 0)
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
            sb.AppendLine("MTFHV048 画面结果");

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return sb.ToString();

            // 反序列化为 MTFHV048TestResult，这是包含所有 ObjectiveTestItem 属性的基类
            MTFHV048TestResult testResult = JsonConvert.DeserializeObject<MTFHV048TestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return sb.ToString();

            sb.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");

            // 使用反射获取所有类型为 ObjectiveTestItem 的公共属性
            var properties = typeof(MTFHV048TestResult).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(ObjectiveTestItem));
            foreach (var prop in properties)
            {
                // 获取属性值，它是一个 ObjectiveTestItem 对象
                if (prop.GetValue(testResult) is ObjectiveTestItem item)
                {
                    // 格式化输出每个测试项的详细信息
                    sb.AppendLine($"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{item.TestResult}");
                }
            }

            return sb.ToString();
        }

        public override IRecipeConfig GetRecipeConfig()
        {
            return RecipeManager.GetInstance().RecipeConfig.GetRequiredService<MTFHV048RecipeConfig>();
        }

        public override IFixConfig GetFixConfig()
        {
            return FixManager.GetInstance().FixConfig.GetRequiredService<MTFHV048FixConfig>();
        }
    }
}
