using ColorVision.Common.NativeMethods;
using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2; // MTFDetailViewReslut
using ColorVision.ImageEditor.Draw;
using Dm.util;
using Newtonsoft.Json;
using ProjectLUX.Fix;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ProjectLUX.Process.VR.MTFH
{
    public class VRMTFHProcessConfig : ProcessConfigBase
    {
        public int Size { get => _Size; set { _Size = value; OnPropertyChanged(); } }
        private int _Size = 30;

        public double Center { get => _Center; set { _Center = value; OnPropertyChanged(); } }
        private double _Center = 14.5;

        public double RA { get => _RA; set { _RA = value; OnPropertyChanged(); } }
        private double _RA = 1.0;
        public double RB { get => _RB; set { _RB = value; OnPropertyChanged(); } }
        private double _RB = 4.0;
        public double RC { get => _RC; set { _RC = value; OnPropertyChanged(); } }
        private double _RC = 9.0;
        public double RD { get => _RD; set { _RD = value; OnPropertyChanged(); } }
        private double _RD = 15.0;

    }


    public class VRMTFHProcess : ProcessBase<VRMTFHProcessConfig>
    {
        public override bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            VRMTFHRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<VRMTFHRecipeConfig>();
            VRMTFHFixConfig fixConfig = ctx.FixConfig.GetRequiredService<VRMTFHFixConfig>();
            VRMTFHViewTestResult testResult = new VRMTFHViewTestResult();
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
                                //这里需要移除掉之前的_
                                objectiveTestItem.Name = mtf.name.Replace("_","");
                                objectiveTestItem.Value = mtf.mtfValue ?? 0;
                                objectiveTestItem.LowLimit = 0;
                                objectiveTestItem.UpLimit = 0;
                                testResult.ObjectiveTestItems.Add(objectiveTestItem);
                            }
                            testResult.MTFDetailViewReslut = mtfDetail;

                            log.Info("MTF Cout:" + testResult.ObjectiveTestItems.Count);
                            if (testResult.ObjectiveTestItems.Count == Config.Size* Config.Size)
                            {
                                List<List<ObjectiveTestItem>> RectItems = new List<List<ObjectiveTestItem>>();

                                for (int i = 0; i < Config.Size; i += 1)
                                {
                                    List<ObjectiveTestItem> objectiveTestItems = new List<ObjectiveTestItem>();
                                    for (int j = 0; j < Config.Size; j++)
                                    {
                                        objectiveTestItems.Add(testResult.ObjectiveTestItems[i* Config.Size + j]);
                                    }
                                    RectItems.Add(objectiveTestItems);
                                }
                                int Size = Config.Size;
                                double center = Config.Center;

                                // 半径阈值
                                double rA = Config.RA;
                                double rB = Config.RB;
                                double rC = Config.RC;
                                double rD = Config.RD;

                                List<ObjectiveTestItem> regionA = new List<ObjectiveTestItem>();
                                List<ObjectiveTestItem> regionB = new List<ObjectiveTestItem>();
                                List<ObjectiveTestItem> regionC = new List<ObjectiveTestItem>();
                                List<ObjectiveTestItem> regionD = new List<ObjectiveTestItem>();

                                for (int row = 0; row < Size; row++)
                                {
                                    for (int col = 0; col < Size; col++)
                                    {
                                        int idx = row * Size + col;
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
                ctx.ObjectiveTestResult.VRMTFHTestResult = JsonConvert.DeserializeObject<VRMTFHTestResult>(ctx.Result.ViewResultJson) ?? new VRMTFHTestResult();

                if (Config.SaveCsv && Directory.Exists(ctx.SavePath))
                {
                    string csvPath = System.IO.Path.Combine(ctx.SavePath, $"{ctx.Result.SN}_MTFH_Result.csv");
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
            VRMTFHViewTestResult testResult = JsonConvert.DeserializeObject<VRMTFHViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            int size = Config.Size;
            double center = Config.Center;

            // 半径阈值
            double rA = Config.RA;
            double rB = Config.RB;
            double rC = Config.RC;
            double rD = Config.RD;

            int id = 0;
            var results = testResult.MTFDetailViewReslut.MTFResult.result;
            if (results.Count != 0)
            {
                // 先尝试排序，保证顺序和 Execute 中一致（如果需要），这里假设数据顺序已经是 30x30 
                // 注意：Render时如果直接遍历列表，需要确认列表是否是 900 个且顺序对应 row/col
                // 如果列表是无序的或者数量不对应，下面计算 idx 的逻辑可能会错。
                // 鉴于 Execute 中做了 Sort，这里通常也是 Sort 后的结果。
                results.Sort((x, y) => Shlwapi.CompareLogical(x.name, y.name));

                for (int i = 0; i < results.Count; i++)
                {
                    var item = results[i];
                    id++;
                    DVRectangleText Rectangle = new();
                    Rectangle.Attribute.Rect = new Rect(item.x, item.y, item.w, item.h);
                    Rectangle.Attribute.Brush = Brushes.Transparent;

                    // 默认红色
                    Brush penBrush = Brushes.Red;

                    // 如果总数是 900，我们尝试根据行列计算区域颜色
                    if (results.Count == 900)
                    {
                        int row = i / size;
                        int col = i % size;

                        // 计算到中心的距离
                        double dr = row - center;
                        double dc = col - center;
                        double dist = Math.Sqrt(dr * dr + dc * dc);

                        if (dist <= rA)
                        {
                            penBrush = Brushes.Gray;   // A 区域 灰色
                        }
                        else if (dist <= rB)
                        {
                            penBrush = Brushes.Green;  // B 区域 绿色
                        }
                        else if (dist <= rC)
                        {
                            penBrush = Brushes.Blue;   // C 区域 蓝色
                        }
                        else if (dist <= rD)
                        {
                            penBrush = Brushes.Yellow; // D 区域 黄色
                        }
                        // 超过 D 区域的默认为 Red
                    }

                    Rectangle.Attribute.Pen = new Pen(penBrush, 1);
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
            outtext += $"MTFH" + Environment.NewLine;

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return outtext;
            VRMTFHViewTestResult testResult = JsonConvert.DeserializeObject<VRMTFHViewTestResult>(ctx.Result.ViewResultJson);
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
            return RecipeManager.GetInstance().RecipeConfig.GetRequiredService<VRMTFHRecipeConfig>();
        }

        public override IFixConfig GetFixConfig()
        {
            return FixManager.GetInstance().FixConfig.GetRequiredService<VRMTFHFixConfig>();
        }
    }
}
