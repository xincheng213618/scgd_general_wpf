using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2; // MTFDetailViewReslut
using ColorVision.ImageEditor.Draw;
using Dm.util;
using Newtonsoft.Json;
using ProjectARVRPro.Fix;
using SqlSugar;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.MTFHV058
{
    public class MTFHV058Process : ProcessBase<MTFHV058ProcessConfig>
    {
        public override bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            MTFHV058RecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<MTFHV058RecipeConfig>();
            MTFHV058FixConfig fixConfig = ctx.FixConfig.GetRequiredService<MTFHV058FixConfig>();
            MTFHV058ViewTestResult testResult = new MTFHV058ViewTestResult();

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
                                testResult.MTF_HV_H_Center_0F.Value *= fixConfig.MTF058_H_Center_0F;
                                testResult.MTF_HV_H_Center_0F.TestValue = testResult.MTF_HV_H_Center_0F.Value.ToString();
                                testResult.MTF_HV_H_Center_0F.LowLimit = recipeConfig.MTF058_H_Center_0F.Min;
                                testResult.MTF_HV_H_Center_0F.UpLimit = recipeConfig.MTF058_H_Center_0F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_Center_0F.TestResult;

                                testResult.MTF_HV_V_Center_0F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_Center_0F.Value *= fixConfig.MTF058_V_Center_0F;
                                testResult.MTF_HV_V_Center_0F.TestValue = testResult.MTF_HV_V_Center_0F.Value.ToString();
                                testResult.MTF_HV_V_Center_0F.LowLimit = recipeConfig.MTF058_V_Center_0F.Min;
                                testResult.MTF_HV_V_Center_0F.UpLimit = recipeConfig.MTF058_V_Center_0F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_Center_0F.TestResult;
                            }
                            else if (mtf.name == Config.Key_LeftUp_0_5F)
                            {
                                testResult.MTF_HV_H_LeftUp_0_5F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_LeftUp_0_5F.Value *= fixConfig.MTF_HV_H_LeftUp_0_5F;
                                testResult.MTF_HV_H_LeftUp_0_5F.TestValue = testResult.MTF_HV_H_LeftUp_0_5F.Value.ToString();
                                testResult.MTF_HV_H_LeftUp_0_5F.LowLimit = recipeConfig.MTF_HV_H_LeftUp_0_5F.Min;
                                testResult.MTF_HV_H_LeftUp_0_5F.UpLimit = recipeConfig.MTF_HV_H_LeftUp_0_5F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_LeftUp_0_5F.TestResult;

                                testResult.MTF_HV_V_LeftUp_0_5F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_LeftUp_0_5F.Value *= fixConfig.MTF_HV_V_LeftUp_0_5F;
                                testResult.MTF_HV_V_LeftUp_0_5F.TestValue = testResult.MTF_HV_V_LeftUp_0_5F.Value.ToString();
                                testResult.MTF_HV_V_LeftUp_0_5F.LowLimit = recipeConfig.MTF_HV_V_LeftUp_0_5F.Min;
                                testResult.MTF_HV_V_LeftUp_0_5F.UpLimit = recipeConfig.MTF_HV_V_LeftUp_0_5F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_LeftUp_0_5F.TestResult;
                            }
                            else if (mtf.name == Config.Key_LeftDown_0_5F)
                            {
                                testResult.MTF_HV_H_LeftDown_0_5F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_LeftDown_0_5F.Value *= fixConfig.MTF_HV_H_LeftDown_0_5F;
                                testResult.MTF_HV_H_LeftDown_0_5F.TestValue = testResult.MTF_HV_H_LeftDown_0_5F.Value.ToString();
                                testResult.MTF_HV_H_LeftDown_0_5F.LowLimit = recipeConfig.MTF_HV_H_LeftDown_0_5F.Min;
                                testResult.MTF_HV_H_LeftDown_0_5F.UpLimit = recipeConfig.MTF_HV_H_LeftDown_0_5F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_LeftDown_0_5F.TestResult;

                                testResult.MTF_HV_V_LeftDown_0_5F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_LeftDown_0_5F.Value *= fixConfig.MTF_HV_V_LeftDown_0_5F;
                                testResult.MTF_HV_V_LeftDown_0_5F.TestValue = testResult.MTF_HV_V_LeftDown_0_5F.Value.ToString();
                                testResult.MTF_HV_V_LeftDown_0_5F.LowLimit = recipeConfig.MTF_HV_V_LeftDown_0_5F.Min;
                                testResult.MTF_HV_V_LeftDown_0_5F.UpLimit = recipeConfig.MTF_HV_V_LeftDown_0_5F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_LeftDown_0_5F.TestResult;
                            }
                            else if (mtf.name == Config.Key_RightDown_0_5F)
                            {
                                testResult.MTF_HV_H_RightDown_0_5F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_RightDown_0_5F.Value *= fixConfig.MTF_HV_H_RightDown_0_5F;
                                testResult.MTF_HV_H_RightDown_0_5F.TestValue = testResult.MTF_HV_H_RightDown_0_5F.Value.ToString();
                                testResult.MTF_HV_H_RightDown_0_5F.LowLimit = recipeConfig.MTF_HV_H_RightDown_0_5F.Min;
                                testResult.MTF_HV_H_RightDown_0_5F.UpLimit = recipeConfig.MTF_HV_H_RightDown_0_5F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_RightDown_0_5F.TestResult;

                                testResult.MTF_HV_V_RightDown_0_5F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_RightDown_0_5F.Value *= fixConfig.MTF_HV_V_RightDown_0_5F;
                                testResult.MTF_HV_V_RightDown_0_5F.TestValue = testResult.MTF_HV_V_RightDown_0_5F.Value.ToString();
                                testResult.MTF_HV_V_RightDown_0_5F.LowLimit = recipeConfig.MTF_HV_V_RightDown_0_5F.Min;
                                testResult.MTF_HV_V_RightDown_0_5F.UpLimit = recipeConfig.MTF_HV_V_RightDown_0_5F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_RightDown_0_5F.TestResult;
                            }
                            else if (mtf.name == Config.Key_RightUp_0_5F)
                            {
                                testResult.MTF_HV_H_RightUp_0_5F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_RightUp_0_5F.Value *= fixConfig.MTF_HV_H_RightUp_0_5F;
                                testResult.MTF_HV_H_RightUp_0_5F.TestValue = testResult.MTF_HV_H_RightUp_0_5F.Value.ToString();
                                testResult.MTF_HV_H_RightUp_0_5F.LowLimit = recipeConfig.MTF_HV_H_RightUp_0_5F.Min;
                                testResult.MTF_HV_H_RightUp_0_5F.UpLimit = recipeConfig.MTF_HV_H_RightUp_0_5F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_RightUp_0_5F.TestResult;

                                testResult.MTF_HV_V_RightUp_0_5F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_RightUp_0_5F.Value *= fixConfig.MTF_HV_V_RightUp_0_5F;
                                testResult.MTF_HV_V_RightUp_0_5F.TestValue = testResult.MTF_HV_V_RightUp_0_5F.Value.ToString();
                                testResult.MTF_HV_V_RightUp_0_5F.LowLimit = recipeConfig.MTF_HV_V_RightUp_0_5F.Min;
                                testResult.MTF_HV_V_RightUp_0_5F.UpLimit = recipeConfig.MTF_HV_V_RightUp_0_5F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_RightUp_0_5F.TestResult;
                            }
                            else if (mtf.name == Config.Key_LeftUp_0_8F)
                            {
                                testResult.MTF_HV_H_LeftUp_0_8F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_LeftUp_0_8F.Value *= fixConfig.MTF058_H_LeftUp_0_8F;
                                testResult.MTF_HV_H_LeftUp_0_8F.TestValue = testResult.MTF_HV_H_LeftUp_0_8F.Value.ToString();
                                testResult.MTF_HV_H_LeftUp_0_8F.LowLimit = recipeConfig.MTF058_H_LeftUp_0_8F.Min;
                                testResult.MTF_HV_H_LeftUp_0_8F.UpLimit = recipeConfig.MTF058_H_LeftUp_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_LeftUp_0_8F.TestResult;

                                testResult.MTF_HV_V_LeftUp_0_8F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_LeftUp_0_8F.Value *= fixConfig.MTF058_V_LeftUp_0_8F;
                                testResult.MTF_HV_V_LeftUp_0_8F.TestValue = testResult.MTF_HV_V_LeftUp_0_8F.Value.ToString();
                                testResult.MTF_HV_V_LeftUp_0_8F.LowLimit = recipeConfig.MTF058_V_LeftUp_0_8F.Min;
                                testResult.MTF_HV_V_LeftUp_0_8F.UpLimit = recipeConfig.MTF058_V_LeftUp_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_LeftUp_0_8F.TestResult;
                            }
                            else if (mtf.name == Config.Key_LeftDown_0_8F)
                            {
                                testResult.MTF_HV_H_LeftDown_0_8F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_LeftDown_0_8F.Value *= fixConfig.MTF058_H_LeftDown_0_8F;
                                testResult.MTF_HV_H_LeftDown_0_8F.TestValue = testResult.MTF_HV_H_LeftDown_0_8F.Value.ToString();
                                testResult.MTF_HV_H_LeftDown_0_8F.LowLimit = recipeConfig.MTF058_H_LeftDown_0_8F.Min;
                                testResult.MTF_HV_H_LeftDown_0_8F.UpLimit = recipeConfig.MTF058_H_LeftDown_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_LeftDown_0_8F.TestResult;

                                testResult.MTF_HV_V_LeftDown_0_8F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_LeftDown_0_8F.Value *= fixConfig.MTF058_V_LeftDown_0_8F;
                                testResult.MTF_HV_V_LeftDown_0_8F.TestValue = testResult.MTF_HV_V_LeftDown_0_8F.Value.ToString();
                                testResult.MTF_HV_V_LeftDown_0_8F.LowLimit = recipeConfig.MTF058_V_LeftDown_0_8F.Min;
                                testResult.MTF_HV_V_LeftDown_0_8F.UpLimit = recipeConfig.MTF058_V_LeftDown_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_LeftDown_0_8F.TestResult;
                            }
                            else if (mtf.name == Config.Key_RightDown_0_8F)
                            {
                                testResult.MTF_HV_H_RightDown_0_8F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_RightDown_0_8F.Value *= fixConfig.MTF058_H_RightDown_0_8F;
                                testResult.MTF_HV_H_RightDown_0_8F.TestValue = testResult.MTF_HV_H_RightDown_0_8F.Value.ToString();
                                testResult.MTF_HV_H_RightDown_0_8F.LowLimit = recipeConfig.MTF058_H_RightDown_0_8F.Min;
                                testResult.MTF_HV_H_RightDown_0_8F.UpLimit = recipeConfig.MTF058_H_RightDown_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_RightDown_0_8F.TestResult;

                                testResult.MTF_HV_V_RightDown_0_8F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_RightDown_0_8F.Value *= fixConfig.MTF058_V_RightDown_0_8F;
                                testResult.MTF_HV_V_RightDown_0_8F.TestValue = testResult.MTF_HV_V_RightDown_0_8F.Value.ToString();
                                testResult.MTF_HV_V_RightDown_0_8F.LowLimit = recipeConfig.MTF058_V_RightDown_0_8F.Min;
                                testResult.MTF_HV_V_RightDown_0_8F.UpLimit = recipeConfig.MTF058_V_RightDown_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_RightDown_0_8F.TestResult;
                            }
                            else if (mtf.name == Config.Key_RightUp_0_8F)
                            {
                                testResult.MTF_HV_H_RightUp_0_8F.Value = mtf.horizontalAverage;
                                testResult.MTF_HV_H_RightUp_0_8F.Value *= fixConfig.MTF058_H_RightUp_0_8F;
                                testResult.MTF_HV_H_RightUp_0_8F.TestValue = testResult.MTF_HV_H_RightUp_0_8F.Value.ToString();
                                testResult.MTF_HV_H_RightUp_0_8F.LowLimit = recipeConfig.MTF058_H_RightUp_0_8F.Min;
                                testResult.MTF_HV_H_RightUp_0_8F.UpLimit = recipeConfig.MTF058_H_RightUp_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_H_RightUp_0_8F.TestResult;

                                testResult.MTF_HV_V_RightUp_0_8F.Value = mtf.verticalAverage;
                                testResult.MTF_HV_V_RightUp_0_8F.Value *= fixConfig.MTF058_V_RightUp_0_8F;
                                testResult.MTF_HV_V_RightUp_0_8F.TestValue = testResult.MTF_HV_V_RightUp_0_8F.Value.ToString();
                                testResult.MTF_HV_V_RightUp_0_8F.LowLimit = recipeConfig.MTF058_V_RightUp_0_8F.Min;
                                testResult.MTF_HV_V_RightUp_0_8F.UpLimit = recipeConfig.MTF058_V_RightUp_0_8F.Max;
                                ctx.Result.Result &= testResult.MTF_HV_V_RightUp_0_8F.TestResult;
                            }
                        }
                        testResult.MTFDetailViewReslut = mtfDetail;
                    }
                }


                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.MTFHV058TestResults.Add(JsonConvert.DeserializeObject<MTFHV058TestResult>(ctx.Result.ViewResultJson) ?? new MTFHV058TestResult());
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
            MTFHV058ViewTestResult testResult = JsonConvert.DeserializeObject<MTFHV058ViewTestResult>(ctx.Result.ViewResultJson);
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
            sb.AppendLine("MTFHV058 画面结果");

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return sb.ToString();

            // 反序列化为 MTFHV058TestResult，这是包含所有 ObjectiveTestItem 属性的基类
            MTFHV058TestResult testResult = JsonConvert.DeserializeObject<MTFHV058TestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return sb.ToString();

            sb.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");

            // 使用反射获取所有类型为 ObjectiveTestItem 的公共属性
            var properties = typeof(MTFHV058TestResult).GetProperties(BindingFlags.Public | BindingFlags.Instance)
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
            return RecipeManager.GetInstance().RecipeConfig.GetRequiredService<MTFHV058RecipeConfig>();
        }

        public override IFixConfig GetFixConfig()
        {
            return FixManager.GetInstance().FixConfig.GetRequiredService<MTFHV058FixConfig>();
        }
    }
}
