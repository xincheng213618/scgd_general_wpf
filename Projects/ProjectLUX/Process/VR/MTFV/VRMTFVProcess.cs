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
                        foreach (var mtf in mtfDetail.MTFResult.result)
                        {
                            ObjectiveTestItem objectiveTestItem = new ObjectiveTestItem();
                            objectiveTestItem.TestValue = mtf.mtfValue.HasValue ? mtf.mtfValue.Value.ToString("F2") : "N/A";
                            objectiveTestItem.Name = mtf.name;
                            objectiveTestItem.Value = mtf.mtfValue ?? 0;
                            testResult.ObjectiveTestItems.Add(objectiveTestItem);
                        }
                        testResult.MTFDetailViewReslut = mtfDetail;
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
                    Rectangle.Attribute.Text = item.name + "_" + item.id;
                    Rectangle.Attribute.Msg = item.mtfValue.ToString();
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
