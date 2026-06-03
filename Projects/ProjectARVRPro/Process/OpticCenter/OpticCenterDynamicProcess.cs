using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.BinocularFusion;
using ColorVision.Engine.Templates.Jsons.FindCross;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Text;

namespace ProjectARVRPro.Process.OpticCenter
{
    public class OpticCenterDynamicProcess : ProcessBase<OpticCenterDynamicProcessConfig>
    {
        public override bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Log;
            OpticCenterRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<OpticCenterRecipeConfig>();
            OpticCenterDynamicViewTestResult testResult = new OpticCenterDynamicViewTestResult();
            OpticCenterTestResult opticCenterResult = testResult.OpticCenterTestResult;

            try
            {
                log?.Info("光轴校准(Dynamic)");

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                {
                    string? fileUrl = values[0].FileUrl;
                    if (!string.IsNullOrWhiteSpace(fileUrl))
                        ctx.Result.FileName = fileUrl;
                }

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    // 允许旧版的计算方案
                    if (master.ImgFileType == ViewResultAlgType.ARVR_BinocularFusion)
                    {
                        log?.Info("use ARVR_BinocularFusion");
                        List<BinocularFusionModel> algResultModels = BinocularFusionDao.Instance.GetAllByPid(master.Id);
                        if (algResultModels.Count >= 1)
                        {
                            BinocularFusionModel binocularFusionModel = algResultModels[0];
                            opticCenterResult.OptCenterXTilt = Build("OptCenterXTilt", recipeConfig.OptCenterXTilt.Apply(binocularFusionModel.XDegree), recipeConfig.OptCenterXTilt.Min, recipeConfig.OptCenterXTilt.Max);
                            opticCenterResult.OptCenterYTilt = Build("OptCenterYTilt", recipeConfig.OptCenterYTilt.Apply(binocularFusionModel.YDegree), recipeConfig.OptCenterYTilt.Min, recipeConfig.OptCenterYTilt.Max);
                            opticCenterResult.OptCenterRotation = Build("OptCenterRotation", recipeConfig.OptCenterRotation.Apply(binocularFusionModel.ZDegree), recipeConfig.OptCenterRotation.Min, recipeConfig.OptCenterRotation.Max);
                            UpdateResult(ctx, opticCenterResult.OptCenterXTilt, opticCenterResult.OptCenterYTilt, opticCenterResult.OptCenterRotation);
                        }
                    }

                    if (master.ImgFileType == ViewResultAlgType.FindCross)
                    {
                        var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                        if (details.Count == 1)
                        {
                            var find = new FindCrossDetailViewReslut(details[0]);
                            var crossResults = find.FindCrossResult?.result;
                            if (crossResults == null || crossResults.Count == 0)
                                continue;

                            var crossResult = crossResults[0];
                            if (master.TName == "optCenter")
                            {
                                crossResult.tilt.tilt_x = recipeConfig.OptCenterXTilt.Apply(crossResult.tilt.tilt_x);
                                crossResult.tilt.tilt_y = recipeConfig.OptCenterYTilt.Apply(crossResult.tilt.tilt_y);
                                crossResult.rotationAngle = recipeConfig.OptCenterRotation.Apply(crossResult.rotationAngle);
                                opticCenterResult.OptCenterXTilt = Build("OptCenterXTilt", crossResult.tilt.tilt_x, recipeConfig.OptCenterXTilt.Min, recipeConfig.OptCenterXTilt.Max);
                                opticCenterResult.OptCenterYTilt = Build("OptCenterYTilt", crossResult.tilt.tilt_y, recipeConfig.OptCenterYTilt.Min, recipeConfig.OptCenterYTilt.Max);
                                opticCenterResult.OptCenterRotation = Build("OptCenterRotation", crossResult.rotationAngle, recipeConfig.OptCenterRotation.Min, recipeConfig.OptCenterRotation.Max);
                                UpdateResult(ctx, opticCenterResult.OptCenterXTilt, opticCenterResult.OptCenterYTilt, opticCenterResult.OptCenterRotation);
                            }
                            else if (master.TName == "ImageCenter")
                            {
                                crossResult.tilt.tilt_x = recipeConfig.ImageCenterXTilt.Apply(crossResult.tilt.tilt_x);
                                crossResult.tilt.tilt_y = recipeConfig.ImageCenterYTilt.Apply(crossResult.tilt.tilt_y);
                                crossResult.rotationAngle = recipeConfig.ImageCenterRotation.Apply(crossResult.rotationAngle);
                                opticCenterResult.ImageCenterXTilt = Build("ImageCenterXTilt", crossResult.tilt.tilt_x, recipeConfig.ImageCenterXTilt.Min, recipeConfig.ImageCenterXTilt.Max);
                                opticCenterResult.ImageCenterYTilt = Build("ImageCenterYTilt", crossResult.tilt.tilt_y, recipeConfig.ImageCenterYTilt.Min, recipeConfig.ImageCenterYTilt.Max);
                                opticCenterResult.ImageCenterRotation = Build("ImageCenterRotation", crossResult.rotationAngle, recipeConfig.ImageCenterRotation.Min, recipeConfig.ImageCenterRotation.Max);
                                UpdateResult(ctx, opticCenterResult.ImageCenterXTilt, opticCenterResult.ImageCenterYTilt, opticCenterResult.ImageCenterRotation);
                            }
                        }
                    }
                }

                testResult.Items = CollectItems(opticCenterResult);
                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);

                string outputName = GetOutputName();
                ctx.ObjectiveTestResult.DynamicTestResults[outputName] = testResult.Items;
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
        }

        public override string GenText(IProcessExecutionContext ctx)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{GetOutputName()} 画面结果");

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return sb.ToString();

            OpticCenterDynamicTestResult? testResult = JsonConvert.DeserializeObject<OpticCenterDynamicTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return sb.ToString();

            sb.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");
            foreach (var item in testResult.Items)
            {
                sb.AppendLine($"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{item.TestResult}");
            }

            return sb.ToString();
        }

        public override IRecipeConfig GetRecipeConfig()
        {
            return RecipeManager.GetInstance().RecipeConfig.GetRequiredService<OpticCenterRecipeConfig>();
        }

        private ObjectiveTestItem Build(string name, double value, double low, double up) => new ObjectiveTestItem
        {
            Name = name,
            LowLimit = low,
            UpLimit = up,
            Value = value,
            TestValue = value.ToString(Config.ShowConfig),
            Unit = Config.Unit
        };

        private static void UpdateResult(IProcessExecutionContext ctx, params ObjectiveTestItem[] items)
        {
            foreach (var item in items)
            {
                if (item != null)
                    ctx.Result.Result &= item.TestResult;
            }
        }

        private static ObservableCollection<ObjectiveTestItem> CollectItems(OpticCenterTestResult result)
        {
            ObservableCollection<ObjectiveTestItem> items = new ObservableCollection<ObjectiveTestItem>();
            AddIfNotNull(items, result.OptCenterXTilt);
            AddIfNotNull(items, result.OptCenterYTilt);
            AddIfNotNull(items, result.OptCenterRotation);
            AddIfNotNull(items, result.ImageCenterXTilt);
            AddIfNotNull(items, result.ImageCenterYTilt);
            AddIfNotNull(items, result.ImageCenterRotation);
            return items;
        }

        private static void AddIfNotNull(ObservableCollection<ObjectiveTestItem> items, ObjectiveTestItem? item)
        {
            if (item != null)
                items.Add(item);
        }

        private string GetOutputName()
        {
            return string.IsNullOrWhiteSpace(Config.Name) ? "Optical_Center" : Config.Name.Trim();
        }
    }
}
