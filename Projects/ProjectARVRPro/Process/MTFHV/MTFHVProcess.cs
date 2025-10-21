using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2; // MTFDetailViewReslut
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.MTFHV
{
    public class MTFHVProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            MTFHVRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<MTFHVRecipeConfig>();

            try
            {
                log?.Info("处理 MTF_HV 流程结果");

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
                            switch (mtf.name)
                            {
                                case "Center_0F":
                                    mtf.horizontalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_H_Center_0F;
                                    mtf.verticalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_V_Center_0F;
                                    ctx.ObjectiveTestResult.MTF_HV_H_Center_0F = Build("MTF_HV_H_Center_0F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_Center_0FMin, recipeConfig.MTF_HV_H_Center_0FMax);
                                    ctx.ObjectiveTestResult.MTF_HV_V_Center_0F = Build("MTF_HV_V_Center_0F", mtf.verticalAverage, recipeConfig.MTF_HV_V_Center_0FMin, recipeConfig.MTF_HV_V_Center_0FMax);
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_H_Center_0F.TestResult;
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_V_Center_0F.TestResult;
                                    break;
                                case "LeftUp_0.4F":
                                    mtf.horizontalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_H_LeftUp_0_4F;
                                    mtf.verticalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_V_LeftUp_0_4F;
                                    ctx.ObjectiveTestResult.MTF_HV_H_LeftUp_0_4F = Build("MTF_HV_H_LeftUp_0_4F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_LeftUp_0_4FMin, recipeConfig.MTF_HV_H_LeftUp_0_4FMax);
                                    ctx.ObjectiveTestResult.MTF_HV_V_LeftUp_0_4F = Build("MTF_HV_V_LeftUp_0_4F", mtf.verticalAverage, recipeConfig.MTF_HV_V_LeftUp_0_4FMin, recipeConfig.MTF_HV_V_LeftUp_0_4FMax);
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_H_LeftUp_0_4F.TestResult;
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_V_LeftUp_0_4F.TestResult;
                                    break;
                                case "RightUp_0.4F":
                                    mtf.horizontalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_H_RightUp_0_4F;
                                    mtf.verticalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_V_RightUp_0_4F;
                                    ctx.ObjectiveTestResult.MTF_HV_H_RightUp_0_4F = Build("MTF_HV_H_RightUp_0_4F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_RightUp_0_4FMin, recipeConfig.MTF_HV_H_RightUp_0_4FMax);
                                    ctx.ObjectiveTestResult.MTF_HV_V_RightUp_0_4F = Build("MTF_HV_V_RightUp_0_4F", mtf.verticalAverage, recipeConfig.MTF_HV_V_RightUp_0_4FMin, recipeConfig.MTF_HV_V_RightUp_0_4FMax);
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_H_RightUp_0_4F.TestResult;
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_V_RightUp_0_4F.TestResult;
                                    break;
                                case "LeftDown_0.4F":
                                    mtf.horizontalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_H_LeftDown_0_4F;
                                    mtf.verticalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_V_LeftDown_0_4F;
                                    ctx.ObjectiveTestResult.MTF_HV_H_LeftDown_0_4F = Build("MTF_HV_H_LeftDown_0_4F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_LeftDown_0_4FMin, recipeConfig.MTF_HV_H_LeftDown_0_4FMax);
                                    ctx.ObjectiveTestResult.MTF_HV_V_LeftDown_0_4F = Build("MTF_HV_V_LeftDown_0_4F", mtf.verticalAverage, recipeConfig.MTF_HV_V_LeftDown_0_4FMin, recipeConfig.MTF_HV_V_LeftDown_0_4FMax);
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_H_LeftDown_0_4F.TestResult;
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_V_LeftDown_0_4F.TestResult;
                                    break;
                                case "RightDown_0.4F":
                                    mtf.horizontalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_H_RightDown_0_4F;
                                    mtf.verticalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_V_RightDown_0_4F;
                                    ctx.ObjectiveTestResult.MTF_HV_H_RightDown_0_4F = Build("MTF_HV_H_RightDown_0_4F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_RightDown_0_4FMin, recipeConfig.MTF_HV_H_RightDown_0_4FMax);
                                    ctx.ObjectiveTestResult.MTF_HV_V_RightDown_0_4F = Build("MTF_HV_V_RightDown_0_4F", mtf.verticalAverage, recipeConfig.MTF_HV_V_RightDown_0_4FMin, recipeConfig.MTF_HV_V_RightDown_0_4FMax);
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_H_RightDown_0_4F.TestResult;
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_V_RightDown_0_4F.TestResult;
                                    break;
                                case "LeftUp_0.8F":
                                    mtf.horizontalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_H_LeftUp_0_8F;
                                    mtf.verticalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_V_LeftUp_0_8F;
                                    ctx.ObjectiveTestResult.MTF_HV_H_LeftUp_0_8F = Build("MTF_HV_H_LeftUp_0_8F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_LeftUp_0_8FMin, recipeConfig.MTF_HV_H_LeftUp_0_8FMax);
                                    ctx.ObjectiveTestResult.MTF_HV_V_LeftUp_0_8F = Build("MTF_HV_V_LeftUp_0_8F", mtf.verticalAverage, recipeConfig.MTF_HV_V_LeftUp_0_8FMin, recipeConfig.MTF_HV_V_LeftUp_0_8FMax);
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_H_LeftUp_0_8F.TestResult;
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_V_LeftUp_0_8F.TestResult;
                                    break;
                                case "RightUp_0.8F":
                                    mtf.horizontalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_H_RightUp_0_8F;
                                    mtf.verticalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_V_RightUp_0_8F;
                                    ctx.ObjectiveTestResult.MTF_HV_H_RightUp_0_8F = Build("MTF_HV_H_RightUp_0_8F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_RightUp_0_8FMin, recipeConfig.MTF_HV_H_RightUp_0_8FMax);
                                    ctx.ObjectiveTestResult.MTF_HV_V_RightUp_0_8F = Build("MTF_HV_V_RightUp_0_8F", mtf.verticalAverage, recipeConfig.MTF_HV_V_RightUp_0_8FMin, recipeConfig.MTF_HV_V_RightUp_0_8FMax);
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_H_RightUp_0_8F.TestResult;
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_V_RightUp_0_8F.TestResult;
                                    break;
                                case "LeftDown_0.8F":
                                    mtf.horizontalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_H_LeftDown_0_8F;
                                    mtf.verticalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_V_LeftDown_0_8F;
                                    ctx.ObjectiveTestResult.MTF_HV_H_LeftDown_0_8F = Build("MTF_HV_H_LeftDown_0_8F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_LeftDown_0_8FMin, recipeConfig.MTF_HV_H_LeftDown_0_8FMax);
                                    ctx.ObjectiveTestResult.MTF_HV_V_LeftDown_0_8F = Build("MTF_HV_V_LeftDown_0_8F", mtf.verticalAverage, recipeConfig.MTF_HV_V_LeftDown_0_8FMin, recipeConfig.MTF_HV_V_LeftDown_0_8FMax);
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_H_LeftDown_0_8F.TestResult;
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_V_LeftDown_0_8F.TestResult;
                                    break;
                                case "RightDown_0.8F":
                                    mtf.horizontalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_H_RightDown_0_8F;
                                    mtf.verticalAverage *= ctx.ObjectiveTestResultFix.MTF_HV_V_RightDown_0_8F;
                                    ctx.ObjectiveTestResult.MTF_HV_H_RightDown_0_8F = Build("MTF_HV_H_RightDown_0_8F", mtf.horizontalAverage, recipeConfig.MTF_HV_H_RightDown_0_8FMin, recipeConfig.MTF_HV_H_RightDown_0_8FMax);
                                    ctx.ObjectiveTestResult.MTF_HV_V_RightDown_0_8F = Build("MTF_HV_V_RightDown_0_8F", mtf.verticalAverage, recipeConfig.MTF_HV_V_RightDown_0_8FMin, recipeConfig.MTF_HV_V_RightDown_0_8FMax);
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_H_RightDown_0_8F.TestResult;
                                    ctx.Result.Result &= ctx.ObjectiveTestResult.MTF_HV_V_RightDown_0_8F.TestResult;
                                    break;
                            }
                        }
                        ctx.Result.ViewRelsultMTFH.MTFDetailViewReslut = mtfDetail;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                log?.Error(ex);
                return false;
            }
        }



        private ObjectiveTestItem Build(string name, double value, double low, double up) => new ObjectiveTestItem
        {
            Name = name,
            LowLimit = low,
            UpLimit = up,
            Value = value,
            TestValue = value.ToString()
        };

        public void Render(IProcessExecutionContext ctx)
        {
            int id = 0;
            if (ctx.Result.ViewRelsultMTFH.MTFDetailViewReslut.MTFResult.result.Count != 0)
            {
                foreach (var item in ctx.Result.ViewRelsultMTFH.MTFDetailViewReslut.MTFResult.result)
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
            outtext += $"水平MTF 测试项：自动AA区域定位算法+MTFHV算法" + Environment.NewLine;
            outtext += $"name,horizontalAverage,verticalAverage,Average," + Environment.NewLine;

            if (result.ViewRelsultMTFH.MTFDetailViewReslut.MTFResult != null)
            {
                foreach (var item in result.ViewRelsultMTFH.MTFDetailViewReslut.MTFResult.resultChild)
                {
                    outtext += $"{item.name},{item.horizontalAverage},{item.verticalAverage},{item.Average}" + Environment.NewLine;
                }
            }

            return outtext;
        }
    }
}
