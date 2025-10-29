using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2; // MTFDetailViewReslut
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Media;

namespace ProjectLUX.Process.MTFHV
{
    public class MTFHVProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            MTFHVRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<MTFHVRecipeConfig>();
            MTFHVFixConfig fixConfig = ctx.FixConfig.GetRequiredService<MTFHVFixConfig>();
            MTFHVViewTestResult testResult = new MTFHVViewTestResult();


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
                            //switch (mtf.name)
                            //{
                            //    case "Center_0F":
                            //        mtf.horizontalAverage *= fixConfig.MTF0F_Center_H1;
                            //        mtf.verticalAverage *= fixConfig.MTF_HV_V_Center_0F;
                            //        testResult.MTF0F_Center_H1 = Build("MTF0F_Center_H1", mtf.horizontalAverage, recipeConfig.MTF0F_Center_H1.Min, recipeConfig.MTF0F_Center_H1.Max);
                            //        testResult.MTF_HV_V_Center_0F = Build("MTF_HV_V_Center_0F", mtf.verticalAverage, recipeConfig.MTF_HV_V_Center_0F.Min, recipeConfig.MTF_HV_V_Center_0F.Max);
                            //        ctx.Result.Result &= testResult.MTF0F_Center_H1.TestResult;
                            //        ctx.Result.Result &= testResult.MTF_HV_V_Center_0F.TestResult;
                            //        break;
                            //    case "LeftUp_0.4F":
                            //        mtf.horizontalAverage *= fixConfig.MTF_HV_H_LeftUp_0_4F;
                            //        mtf.verticalAverage *= fixConfig.MTF_HV_V_LeftUp_0_4F;
                            //        testResult.MTF_HV_H_LeftUp_0_4F = Build("MTF_HV_H_LeftUp_0_4F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_LeftUp_0_4F.Min, recipeConfig.MTF_HV_H_LeftUp_0_4F.Max);
                            //        testResult.MTF_HV_V_LeftUp_0_4F = Build("MTF_HV_V_LeftUp_0_4F", mtf.verticalAverage, recipeConfig.MTF_HV_V_LeftUp_0_4F.Min, recipeConfig.MTF_HV_V_LeftUp_0_4F.Max);
                            //        ctx.Result.Result &= testResult.MTF_HV_H_LeftUp_0_4F.TestResult;
                            //        ctx.Result.Result &= testResult.MTF_HV_V_LeftUp_0_4F.TestResult;
                            //        break;
                            //    case "RightUp_0.4F":
                            //        mtf.horizontalAverage *= fixConfig.MTF_HV_H_RightUp_0_4F;
                            //        mtf.verticalAverage *= fixConfig.MTF_HV_V_RightUp_0_4F;
                            //        testResult.MTF_HV_H_RightUp_0_4F = Build("MTF_HV_H_RightUp_0_4F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_RightUp_0_4F.Min, recipeConfig.MTF_HV_H_RightUp_0_4F.Max);
                            //        testResult.MTF_HV_V_RightUp_0_4F = Build("MTF_HV_V_RightUp_0_4F", mtf.verticalAverage, recipeConfig.MTF_HV_V_RightUp_0_4F.Min, recipeConfig.MTF_HV_V_RightUp_0_4F.Max);
                            //        ctx.Result.Result &= testResult.MTF_HV_H_RightUp_0_4F.TestResult;
                            //        ctx.Result.Result &= testResult.MTF_HV_V_RightUp_0_4F.TestResult;
                            //        break;
                            //    case "LeftDown_0.4F":
                            //        mtf.horizontalAverage *= fixConfig.MTF_HV_H_LeftDown_0_4F;
                            //        mtf.verticalAverage *= fixConfig.MTF_HV_V_LeftDown_0_4F;
                            //        testResult.MTF_HV_H_LeftDown_0_4F = Build("MTF_HV_H_LeftDown_0_4F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_LeftDown_0_4F.Min, recipeConfig.MTF_HV_H_LeftDown_0_4F.Max);
                            //        testResult.MTF_HV_V_LeftDown_0_4F = Build("MTF_HV_V_LeftDown_0_4F", mtf.verticalAverage, recipeConfig.MTF_HV_V_LeftDown_0_4F.Min, recipeConfig.MTF_HV_V_LeftDown_0_4F.Max);
                            //        ctx.Result.Result &= testResult.MTF_HV_H_LeftDown_0_4F.TestResult;
                            //        ctx.Result.Result &= testResult.MTF_HV_V_LeftDown_0_4F.TestResult;
                            //        break;
                            //    case "RightDown_0.4F":
                            //        mtf.horizontalAverage *= fixConfig.MTF_HV_H_RightDown_0_4F;
                            //        mtf.verticalAverage *= fixConfig.MTF_HV_V_RightDown_0_4F;
                            //        testResult.MTF_HV_H_RightDown_0_4F = Build("MTF_HV_H_RightDown_0_4F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_RightDown_0_4F.Min, recipeConfig.MTF_HV_H_RightDown_0_4F.Max);
                            //        testResult.MTF_HV_V_RightDown_0_4F = Build("MTF_HV_V_RightDown_0_4F", mtf.verticalAverage, recipeConfig.MTF_HV_V_RightDown_0_4F.Min, recipeConfig.MTF_HV_V_RightDown_0_4F.Max);
                            //        ctx.Result.Result &= testResult.MTF_HV_H_RightDown_0_4F.TestResult;
                            //        ctx.Result.Result &= testResult.MTF_HV_V_RightDown_0_4F.TestResult;
                            //        break;
                            //    case "LeftUp_0.8F":
                            //        mtf.horizontalAverage *= fixConfig.MTF_HV_H_LeftUp_0_8F;
                            //        mtf.verticalAverage *= fixConfig.MTF_HV_V_LeftUp_0_8F;
                            //        testResult.MTF_HV_H_LeftUp_0_8F = Build("MTF_HV_H_LeftUp_0_8F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_LeftUp_0_8F.Min, recipeConfig.MTF_HV_H_LeftUp_0_8F.Max);
                            //        testResult.MTF_HV_V_LeftUp_0_8F = Build("MTF_HV_V_LeftUp_0_8F", mtf.verticalAverage, recipeConfig.MTF_HV_V_LeftUp_0_8F.Min, recipeConfig.MTF_HV_V_LeftUp_0_8F.Max);
                            //        ctx.Result.Result &= testResult.MTF_HV_H_LeftUp_0_8F.TestResult;
                            //        ctx.Result.Result &= testResult.MTF_HV_V_LeftUp_0_8F.TestResult;
                            //        break;
                            //    case "RightUp_0.8F":
                            //        mtf.horizontalAverage *= fixConfig.MTF_HV_H_RightUp_0_8F;
                            //        mtf.verticalAverage *= fixConfig.MTF_HV_V_RightUp_0_8F;
                            //        testResult.MTF_HV_H_RightUp_0_8F = Build("MTF_HV_H_RightUp_0_8F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_RightUp_0_8F.Min, recipeConfig.MTF_HV_H_RightUp_0_8F.Max);
                            //        testResult.MTF_HV_V_RightUp_0_8F = Build("MTF_HV_V_RightUp_0_8F", mtf.verticalAverage, recipeConfig.MTF_HV_V_RightUp_0_8F.Min, recipeConfig.MTF_HV_V_RightUp_0_8F.Max);
                            //        ctx.Result.Result &= testResult.MTF_HV_H_RightUp_0_8F.TestResult;
                            //        ctx.Result.Result &= testResult.MTF_HV_V_RightUp_0_8F.TestResult;
                            //        break;
                            //    case "LeftDown_0.8F":
                            //        mtf.horizontalAverage *= fixConfig.MTF_HV_H_LeftDown_0_8F;
                            //        mtf.verticalAverage *= fixConfig.MTF_HV_V_LeftDown_0_8F;
                            //        testResult.MTF_HV_H_LeftDown_0_8F = Build("MTF_HV_H_LeftDown_0_8F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_LeftDown_0_8F.Min, recipeConfig.MTF_HV_H_LeftDown_0_8F.Max);
                            //        testResult.MTF_HV_V_LeftDown_0_8F = Build("MTF_HV_V_LeftDown_0_8F", mtf.verticalAverage, recipeConfig.MTF_HV_V_LeftDown_0_8F.Min, recipeConfig.MTF_HV_V_LeftDown_0_8F.Max);
                            //        ctx.Result.Result &= testResult.MTF_HV_H_LeftDown_0_8F.TestResult;
                            //        ctx.Result.Result &= testResult.MTF_HV_V_LeftDown_0_8F.TestResult;
                            //        break;
                            //    case "RightDown_0.8F":
                            //        mtf.horizontalAverage *= fixConfig.MTF_HV_H_RightDown_0_8F;
                            //        mtf.verticalAverage *= fixConfig.MTF_HV_V_RightDown_0_8F;
                            //        testResult.MTF_HV_H_RightDown_0_8F = Build("MTF_HV_H_RightDown_0_8F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_RightDown_0_8F.Min, recipeConfig.MTF_HV_H_RightDown_0_8F.Max);
                            //        testResult.MTF_HV_V_RightDown_0_8F = Build("MTF_HV_V_RightDown_0_8F", mtf.verticalAverage, recipeConfig.MTF_HV_V_RightDown_0_8F.Min, recipeConfig.MTF_HV_V_RightDown_0_8F.Max);
                            //        ctx.Result.Result &= testResult.MTF_HV_H_RightDown_0_8F.TestResult;
                            //        ctx.Result.Result &= testResult.MTF_HV_V_RightDown_0_8F.TestResult;
                            //        break;
                            //}
                        }
                        testResult.MTFDetailViewReslut = mtfDetail;
                    }
                }


                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.MTFHVTestResult = JsonConvert.DeserializeObject<MTFHVTestResult>(ctx.Result.ViewResultJson) ?? new MTFHVTestResult();
                return true;
            }
            catch (Exception ex)
            {
                log?.Error(ex);
                return false;
            }
        }


        public void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            MTFHVViewTestResult testResult = JsonConvert.DeserializeObject<MTFHVViewTestResult>(ctx.Result.ViewResultJson);
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
                    Rectangle.Attribute.Text = item.name + "_" + item.id;
                    Rectangle.Attribute.Msg = item.mtfValue.ToString();
                    Rectangle.Render();
                    ctx.ImageView.AddVisual(Rectangle);
                }
            }
        }

        public string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"MTFHV 画面结果" + Environment.NewLine;


            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return outtext;
            MTFHVViewTestResult testResult = JsonConvert.DeserializeObject<MTFHVViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return outtext;

            outtext += $"name,horizontalAverage,verticalAverage,Average," + Environment.NewLine;

            if (testResult.MTFDetailViewReslut.MTFResult != null)
            {
                foreach (var item in testResult.MTFDetailViewReslut.MTFResult.resultChild)
                {
                    outtext += $"{item.name},{item.horizontalAverage},{item.verticalAverage},{item.Average}" + Environment.NewLine;
                }
            }
            return outtext;
        }
    }
}
