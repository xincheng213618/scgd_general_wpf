using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2; // MTFDetailViewReslut
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using ProjectLUX.Fix;
using ProjectLUX.Process.ChessboardAR;
using ProjectLUX.Process.VR.MTFH;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ProjectLUX.Process.VR.MTFV
{
    public class VRMTFVProcessConfig : ProcessConfigBase
    {

    }

    public class VRMTFVProcess : ProcessBase<VRMTFVProcessConfig>
    {
        public override bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            VRMTFVRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<VRMTFVRecipeConfig>();
            VRMTFVFixConfig fixConfig = ctx.FixConfig.GetRequiredService<VRMTFVFixConfig>();
            VRMTFVViewTestResult testResult = new VRMTFVViewTestResult();


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
                        if (mtfDetail?.MTFResult?.result != null)
                        {
                            mtfDetail.MTFResult.result.Sort((x, y) => Shlwapi.CompareLogical(x.name, y.name));
                            foreach (var mtf in mtfDetail.MTFResult.result)
                            {
                                ObjectiveTestItem objectiveTestItem = new ObjectiveTestItem();
                                objectiveTestItem.TestValue = mtf.mtfValue.HasValue ? mtf.mtfValue.Value.ToString("F2") : "N/A";
                                objectiveTestItem.Name = mtf.name;
                                objectiveTestItem.Value = mtf.mtfValue ?? 0;
                                objectiveTestItem.LowLimit = 0;
                                objectiveTestItem.UpLimit = 0;
                                testResult.ObjectiveTestItems.Add(objectiveTestItem);
                            }
                            testResult.MTFDetailViewReslut = mtfDetail;

                            log.Info("MTF Cout:" + testResult.ObjectiveTestItems.Count);
                            if (testResult.ObjectiveTestItems.Count == 900)
                            {
                                List<List<ObjectiveTestItem>> RectItems = new List<List<ObjectiveTestItem>>();

                                for (int i = 0; i < 30; i += 1)
                                {
                                    List<ObjectiveTestItem> objectiveTestItems = new List<ObjectiveTestItem>();
                                    for (int j = 0; j < 30; j++)
                                    {
                                        objectiveTestItems.Add(testResult.ObjectiveTestItems[i * 30 + j]);
                                    }
                                    RectItems.Add(objectiveTestItems);
                                }
                                const int size = 30;
                                const double center = 14.5;

                                // 半径阈值
                                const double rA = 1.0;
                                const double rB = 2.0;
                                const double rC = 4.0;
                                const double rD = 7.5;

                                List<ObjectiveTestItem> regionA = new List<ObjectiveTestItem>();
                                List<ObjectiveTestItem> regionB = new List<ObjectiveTestItem>();
                                List<ObjectiveTestItem> regionC = new List<ObjectiveTestItem>();
                                List<ObjectiveTestItem> regionD = new List<ObjectiveTestItem>();


                                for (int row = 0; row < size; row++)
                                {
                                    for (int col = 0; col < size; col++)
                                    {
                                        int idx = row * size + col;
                                        var item = testResult.ObjectiveTestItems[idx];

                                        // 计算到中心的距离
                                        double dr = row - center;
                                        double dc = col - center;
                                        double dist = Math.Sqrt(dr * dr + dc * dc);

                                        if (dist <= rA)
                                        {
                                            regionA.Add(item);
                                        }
                                        else if (dist <= rB)
                                        {
                                            regionB.Add(item);
                                        }
                                        else if (dist <= rC)
                                        {
                                            regionC.Add(item);
                                        }
                                        else if (dist <= rD)
                                        {
                                            regionD.Add(item);
                                        }
                                    }
                                }

                                testResult.Region_A_Average.Value = regionA.Average(i => i.Value);
                                testResult.Region_A_Average.TestValue = regionA.Average(i => i.Value).ToString();
                                testResult.Region_A_Average.LowLimit = recipeConfig.Region_A_Average.Min;
                                testResult.Region_A_Average.UpLimit = recipeConfig.Region_A_Average.Max;

                                testResult.Region_A_Max.Value = regionA.Max(i => i.Value);
                                testResult.Region_A_Max.TestValue = regionA.Max(i => i.Value).ToString();
                                testResult.Region_A_Max.LowLimit = recipeConfig.Region_A_Max.Min;
                                testResult.Region_A_Max.UpLimit = recipeConfig.Region_A_Max.Max;

                                testResult.Region_A_Min.Value = regionA.Min(i => i.Value);
                                testResult.Region_A_Min.TestValue = regionA.Min(i => i.Value).ToString();
                                testResult.Region_A_Min.LowLimit = recipeConfig.Region_A_Min.Min;
                                testResult.Region_A_Min.UpLimit = recipeConfig.Region_A_Min.Max;

                                testResult.Region_B_Average.Value = regionB.Average(i => i.Value);
                                testResult.Region_B_Average.TestValue = regionB.Average(i => i.Value).ToString();
                                testResult.Region_B_Average.LowLimit = recipeConfig.Region_B_Average.Min;
                                testResult.Region_B_Average.UpLimit = recipeConfig.Region_B_Average.Max;

                                testResult.Region_B_Max.Value = regionB.Max(i => i.Value);
                                testResult.Region_B_Max.TestValue = regionB.Max(i => i.Value).ToString();
                                testResult.Region_B_Max.LowLimit = recipeConfig.Region_B_Max.Min;
                                testResult.Region_B_Max.UpLimit = recipeConfig.Region_B_Max.Max;

                                testResult.Region_B_Min.Value = regionB.Min(i => i.Value);
                                testResult.Region_B_Min.TestValue = regionB.Min(i => i.Value).ToString();
                                testResult.Region_B_Min.LowLimit = recipeConfig.Region_B_Min.Min;
                                testResult.Region_B_Min.UpLimit = recipeConfig.Region_B_Min.Max;

                                testResult.Region_C_Average.Value = regionC.Average(i => i.Value);
                                testResult.Region_C_Average.TestValue = regionC.Average(i => i.Value).ToString();
                                testResult.Region_C_Average.LowLimit = recipeConfig.Region_C_Average.Min;
                                testResult.Region_C_Average.UpLimit = recipeConfig.Region_C_Average.Max;

                                testResult.Region_C_Max.Value = regionC.Max(i => i.Value);
                                testResult.Region_C_Max.TestValue = regionC.Max(i => i.Value).ToString();
                                testResult.Region_C_Max.LowLimit = recipeConfig.Region_C_Max.Min;
                                testResult.Region_C_Max.UpLimit = recipeConfig.Region_C_Max.Max;

                                testResult.Region_C_Min.Value = regionC.Min(i => i.Value);
                                testResult.Region_C_Min.TestValue = regionC.Min(i => i.Value).ToString();
                                testResult.Region_C_Min.LowLimit = recipeConfig.Region_C_Min.Min;
                                testResult.Region_C_Min.UpLimit = recipeConfig.Region_C_Min.Max;

                                testResult.Region_D_Average.Value = regionD.Average(i => i.Value);
                                testResult.Region_D_Average.TestValue = regionD.Average(i => i.Value).ToString();
                                testResult.Region_D_Average.LowLimit = recipeConfig.Region_D_Average.Min;
                                testResult.Region_D_Average.UpLimit = recipeConfig.Region_D_Average.Max;

                                testResult.Region_D_Max.Value = regionD.Max(i => i.Value);
                                testResult.Region_D_Max.TestValue = regionD.Max(i => i.Value).ToString();
                                testResult.Region_D_Max.LowLimit = recipeConfig.Region_D_Max.Min;
                                testResult.Region_D_Max.UpLimit = recipeConfig.Region_D_Max.Max;

                                testResult.Region_D_Min.Value = regionD.Min(i => i.Value);
                                testResult.Region_D_Min.TestValue = regionD.Min(i => i.Value).ToString();
                                testResult.Region_D_Min.LowLimit = recipeConfig.Region_D_Min.Min;
                                testResult.Region_D_Min.UpLimit = recipeConfig.Region_D_Min.Max;
                            }
                        }

                    }
                }


                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.VRMTFVTestResult = JsonConvert.DeserializeObject<VRMTFVTestResult>(ctx.Result.ViewResultJson) ?? new VRMTFVTestResult();

                if (Config.SaveCsv && Directory.Exists(ctx.SavePath))
                {
                    string csvPath = System.IO.Path.Combine(ctx.SavePath, $"{ctx.Result.SN}_MTFV_Result.csv");
                    ObjectiveTestResultCsvExporter.ExportToCsv(ctx.ObjectiveTestResult, csvPath);
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
            VRMTFVViewTestResult testResult = JsonConvert.DeserializeObject<VRMTFVViewTestResult>(ctx.Result.ViewResultJson);
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
                    Rectangle.Render();
                    ctx.ImageView.AddVisual(Rectangle);
                }
            }
        }

        public override string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"MTFV" + Environment.NewLine;

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return outtext;
            VRMTFVViewTestResult testResult = JsonConvert.DeserializeObject<VRMTFVViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return outtext;

            outtext += $"name,mtfValue," + Environment.NewLine;

            if (testResult.MTFDetailViewReslut.MTFResult != null)
            {
                foreach (var item in testResult.MTFDetailViewReslut.MTFResult.result)
                {
                    outtext += $"{item.name},{item.mtfValue}" + Environment.NewLine;
                }
            }
            return outtext;
        }

        public override IRecipeConfig GetRecipeConfig()
        {
            return RecipeManager.GetInstance().RecipeConfig.GetRequiredService<VRMTFVRecipeConfig>();
        }

        public override IFixConfig GetFixConfig()
        {
            return FixManager.GetInstance().FixConfig.GetRequiredService<VRMTFVFixConfig>();
        }

    }
}
