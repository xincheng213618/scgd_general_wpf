using ColorVision.Common.Algorithms;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.FindLightArea;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using ProjectARVRPro.Recipe;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ProjectARVRPro.Process.RGB.FieldOfView
{
    public class FieldOfViewProcess : ProcessBase<FieldOfViewProcessConfig>
    {
        public override IRecipeConfig GetRecipeConfig() => Config.RecipeConfig;

        public override Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null || ctx.ObjectiveTestResult == null)
                return Task.FromResult(false);

            try
            {
                var testResult = new FieldOfViewViewTestResult();
                var images = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                string? fileUrl = images.FirstOrDefault()?.FileUrl;
                if (!string.IsNullOrWhiteSpace(fileUrl))
                    ctx.Result.FileName = fileUrl;

                foreach (var master in AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id))
                {
                    if (master.ImgFileType == ViewResultAlgType.FindLightArea)
                    {
                        testResult.AlgResultLightAreaModels = AlgResultLightAreaDao.Instance.GetAllByPid(master.Id) ?? new();
                    }
                    else if (master.ImgFileType == ViewResultAlgType.FOV)
                    {
                        ReadFieldOfViewResult(ctx, master, testResult);
                    }
                }

                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                var objectiveResult = JsonConvert.DeserializeObject<FieldOfViewTestResult>(ctx.Result.ViewResultJson) ?? new();
                ctx.ObjectiveTestResult.SetFieldOfViewResult(Config.GetOutputKey(), objectiveResult);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                ctx.Log?.Error(ex);
                return Task.FromResult(false);
            }
        }

        private void ReadFieldOfViewResult(IProcessExecutionContext ctx, AlgResultMasterModel master, FieldOfViewTestResult testResult)
        {
            var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
            if (details.Count != 1)
                return;

            var view = new DFovView(details[0]);
            var recipe = Config.RecipeConfig;
            double diagonal = recipe.DiagonalFieldOfViewAngle.Apply(view.Result.result.D_Fov);
            double horizontal = recipe.HorizontalFieldOfViewAngle.Apply(view.Result.result.ClolorVisionH_Fov);
            double vertical = recipe.VerticalFieldOfViewAngle.Apply(view.Result.result.ClolorVisionV_Fov);

            testResult.DiagonalFieldOfViewAngle = CreateItem("Diagonal_Field_of_View_Angle", diagonal, recipe.DiagonalFieldOfViewAngle);
            testResult.HorizontalFieldOfViewAngle = CreateItem("Horizontal_Field_Of_View_Angle", horizontal, recipe.HorizontalFieldOfViewAngle);
            testResult.VerticalFieldOfViewAngle = CreateItem("Vertical_Field of_View_Angle", vertical, recipe.VerticalFieldOfViewAngle);

            ctx.Result.Result &= testResult.DiagonalFieldOfViewAngle.TestResult;
            ctx.Result.Result &= testResult.HorizontalFieldOfViewAngle.TestResult;
            ctx.Result.Result &= testResult.VerticalFieldOfViewAngle.TestResult;
        }

        private static ObjectiveTestItem CreateItem(string name, double value, RecipeBase recipe)
        {
            return new ObjectiveTestItem
            {
                Name = name,
                Value = value,
                TestValue = value.ToString("F4"),
                Unit = "degree",
                LowLimit = recipe.Min,
                UpLimit = recipe.Max
            };
        }

        public override void Render(IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson))
                return;

            var testResult = JsonConvert.DeserializeObject<FieldOfViewViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null || testResult.AlgResultLightAreaModels.Count == 0)
                return;

            var points = testResult.AlgResultLightAreaModels
                .Select(item => new Point((int)item.PosX, (int)item.PosY))
                .ToList();
            var polygon = new DVPolygon();
            foreach (Point point in GrahamScan.ComputeConvexHull(points))
                polygon.Attribute.Points.Add(point);

            polygon.Attribute.Brush = Brushes.Transparent;
            polygon.Attribute.Pen = new Pen(Brushes.Blue, 1);
            polygon.Attribute.Id = -1;
            polygon.IsComple = true;
            polygon.Render();
            ctx.ImageView.AddVisual(polygon);
        }

        public override void GenText(IProcessExecutionContext ctx, Paragraph paragraph, Brush foreground, double fontSize)
        {
            var output = new StringBuilder().AppendLine($"视场角测试 ({Config.GetOutputKey()})");
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson))
            {
                AppendPlainText(paragraph, output.ToString(), foreground, fontSize);
                return;
            }

            var testResult = JsonConvert.DeserializeObject<FieldOfViewViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult != null)
            {
                AppendResult(output, testResult.HorizontalFieldOfViewAngle);
                AppendResult(output, testResult.VerticalFieldOfViewAngle);
                AppendResult(output, testResult.DiagonalFieldOfViewAngle);
            }

            AppendPlainText(paragraph, output.ToString(), foreground, fontSize);
        }

        private static void AppendResult(StringBuilder output, ObjectiveTestItem item)
        {
            if (string.IsNullOrWhiteSpace(item?.Name))
                return;

            output.AppendLine($"{item.Name}:{item.TestValue}{item.Unit} LowLimit:{item.LowLimit} UpLimit:{item.UpLimit},Result:{(item.TestResult ? "PASS" : "Fail")}");
        }
    }
}
