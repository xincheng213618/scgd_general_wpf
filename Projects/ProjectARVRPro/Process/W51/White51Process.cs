using ColorVision.Common.Algorithms;
using ColorVision.Database;
using ColorVision.Engine; // AlgResultMasterDao, MeasureImgResultDao
using ColorVision.Engine.Templates.FindLightArea;
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.FOV2; // DFovView
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.W51
{
    /// <summary>
    /// Extracted processing logic for White51 (W51) test type.
    /// </summary>
    public class White51Process : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx == null || ctx.Batch == null || ctx.Result == null)
                return false;
            var log = ctx.Logger;
            W51RecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<W51RecipeConfig>();
            W51FixConfig fixConfig = ctx.FixConfig.GetRequiredService<W51FixConfig>();


            try
            {
                log?.Info("处理 White51 流程结果");
                // 图像
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                {
                    ctx.Result.FileName = values[0].FileUrl;
                }

                var algResultMasterlists = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                log?.Info($"AlgResultMasterlists count {algResultMasterlists.Count}");
                foreach (var algResultMaster in algResultMasterlists)
                {
                    if (algResultMaster.ImgFileType == ViewResultAlgType.FindLightArea)
                    {
                        ctx.Result.ViewReslutW51.AlgResultLightAreaModels = AlgResultLightAreaDao.Instance.GetAllByPid(algResultMaster.Id);
                    }

                    if (algResultMaster.ImgFileType == ViewResultAlgType.FOV)
                    {
                        var algResultModels = DeatilCommonDao.Instance.GetAllByPid(algResultMaster.Id);
                        if (algResultModels.Count == 1)
                        {
                            DFovView view1 = new DFovView(algResultModels[0]);

                            view1.Result.result.D_Fov = view1.Result.result.D_Fov * fixConfig.W51DiagonalFieldOfViewAngle;
                            view1.Result.result.ClolorVisionH_Fov = view1.Result.result.ClolorVisionH_Fov * fixConfig.W51HorizontalFieldOfViewAngle;
                            view1.Result.result.ClolorVisionV_Fov = view1.Result.result.ClolorVisionV_Fov * fixConfig.W51VerticalFieldOfViewAngle;

                            ctx.Result.ViewResultWhite.DFovView = view1;
                            ctx.ObjectiveTestResult.W51DiagonalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "DiagonalFieldOfViewAngle",
                                LowLimit = recipeConfig.DiagonalFieldOfViewAngleMin,
                                UpLimit = recipeConfig.DiagonalFieldOfViewAngleMax,
                                Value = view1.Result.result.D_Fov,
                                TestValue = view1.Result.result.D_Fov.ToString("F3")
                            };

                            ctx.ObjectiveTestResult.W51HorizontalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "HorizontalFieldOfViewAngle",
                                LowLimit = recipeConfig.HorizontalFieldOfViewAngleMin,
                                UpLimit = recipeConfig.HorizontalFieldOfViewAngleMax,
                                Value = view1.Result.result.ClolorVisionH_Fov,
                                TestValue = view1.Result.result.ClolorVisionH_Fov.ToString("F3")
                            };
                            ctx.ObjectiveTestResult.W51VerticalFieldOfViewAngle = new ObjectiveTestItem()
                            {
                                Name = "VerticalFieldOfViewAngle",
                                LowLimit = recipeConfig.VerticalFieldOfViewAngleMin,
                                UpLimit = recipeConfig.VerticalFieldOfViewAngleMax,
                                Value = view1.Result.result.ClolorVisionV_Fov,
                                TestValue = view1.Result.result.ClolorVisionV_Fov.ToString("F3")
                            };
                            ctx.Result.ViewReslutW51.DiagonalFieldOfViewAngle = ctx.ObjectiveTestResult.W51DiagonalFieldOfViewAngle;
                            ctx.Result.ViewReslutW51.HorizontalFieldOfViewAngle = ctx.ObjectiveTestResult.W51HorizontalFieldOfViewAngle;
                            ctx.Result.ViewReslutW51.VerticalFieldOfViewAngle = ctx.ObjectiveTestResult.W51VerticalFieldOfViewAngle;

                            ctx.Result.Result = ctx.Result.Result && ctx.ObjectiveTestResult.W51DiagonalFieldOfViewAngle.TestResult;
                            ctx.Result.Result = ctx.Result.Result && ctx.ObjectiveTestResult.W51HorizontalFieldOfViewAngle.TestResult;
                            ctx.Result.Result = ctx.Result.Result && ctx.ObjectiveTestResult.W51VerticalFieldOfViewAngle.TestResult;
                        }

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

        public void Render(IProcessExecutionContext processExecutionContext)
        {
            var ctx = processExecutionContext;
            DVPolygon polygon = new DVPolygon();
            List<System.Windows.Point> point1s = new List<System.Windows.Point>();
            foreach (var item in ctx.Result.ViewReslutW51.AlgResultLightAreaModels)
            {
                point1s.Add(new System.Windows.Point((int)item.PosX, (int)item.PosY));
            }
            foreach (var item in GrahamScan.ComputeConvexHull(point1s))
            {
                polygon.Attribute.Points.Add(new Point(item.X, item.Y));
            }
            polygon.Attribute.Brush = Brushes.Transparent;
            polygon.Attribute.Pen = new Pen(Brushes.Blue, 1);
            polygon.Attribute.Id = -1;
            polygon.IsComple = true;
            polygon.Render();
            ctx.ImageView.AddVisual(polygon);
        }



        public string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"白画面绿图W51 测试项：自动AA区域定位算法+FOV算法" + Environment.NewLine;

            outtext += $"发光区角点：" + Environment.NewLine;
            if (result.ViewReslutW51.AlgResultLightAreaModels != null)
            {
                foreach (var item in result.ViewReslutW51.AlgResultLightAreaModels)
                {
                    outtext += $"{item.PosX},{item.PosY}" + Environment.NewLine;
                }
            }

            outtext += $"DiagonalFieldOfViewAngle:{result.ViewReslutW51.DiagonalFieldOfViewAngle.TestValue}  LowLimit:{result.ViewReslutW51.DiagonalFieldOfViewAngle.LowLimit} UpLimit:{result.ViewReslutW51.DiagonalFieldOfViewAngle.UpLimit},Rsult{(result.ViewReslutW51.DiagonalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"HorizontalFieldOfViewAngle:{result.ViewReslutW51.HorizontalFieldOfViewAngle.TestValue} LowLimit:{result.ViewReslutW51.HorizontalFieldOfViewAngle.LowLimit} UpLimit:{result.ViewReslutW51.HorizontalFieldOfViewAngle.UpLimit} ,Rsult{(result.ViewReslutW51.HorizontalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            outtext += $"VerticalFieldOfViewAngle:{result.ViewReslutW51.VerticalFieldOfViewAngle.TestValue} LowLimit:{result.ViewReslutW51.VerticalFieldOfViewAngle.LowLimit} UpLimit:{result.ViewReslutW51.VerticalFieldOfViewAngle.UpLimit},Rsult{(result.ViewReslutW51.VerticalFieldOfViewAngle.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            return outtext;
        }
    }
}
