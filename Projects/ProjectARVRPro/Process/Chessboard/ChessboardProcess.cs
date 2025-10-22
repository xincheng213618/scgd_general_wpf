using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.PoiAnalysis; // PoiAnalysisDetailViewReslut
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using ProjectARVRPro.Process.MTFHV;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.Chessboard
{
    public class ChessboardProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            ChessboardRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<ChessboardRecipeConfig>();
            ChessboardFixConfig fixConfig = ctx.FixConfig.GetRequiredService<ChessboardFixConfig>();

            try
            {
                log?.Info("处理 Chessboard 流程结果");

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        ctx.Result.ViewReslutCheckerboard.PoiResultCIExyuvDatas = new ObservableCollection<PoiResultCIExyuvData>();
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        int id = 0;
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            ctx.Result.ViewReslutCheckerboard.PoiResultCIExyuvDatas.Add(poi);
                        }
                    }

                    if (master.ImgFileType == ViewResultAlgType.PoiAnalysis && master.TName.Contains("Chessboard_Contrast"))
                    {
                        var details = DeatilCommonDao.Instance.GetAllByPid(master.Id);
                        if (details.Count == 1)
                        {
                            var view = new PoiAnalysisDetailViewReslut(details[0]);
                            view.PoiAnalysisResult.result.Value *= fixConfig.ChessboardContrast;
                            var contrast = new ObjectiveTestItem
                            {
                                Name = "Chessboard_Contrast",
                                LowLimit = recipeConfig.ChessboardContras.Min,
                                UpLimit = recipeConfig.ChessboardContras.Max,
                                Value = view.PoiAnalysisResult.result.Value,
                                TestValue = view.PoiAnalysisResult.result.Value.ToString("F3")
                            };
                            ctx.ObjectiveTestResult.ChessboardContrast = contrast;
                            ctx.Result.ViewReslutCheckerboard.ChessboardContrast = contrast;
                            ctx.Result.Result &= contrast.TestResult;
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

        public void Render(IProcessExecutionContext ctx)
        {
            foreach (var poiResultCIExyuvData in ctx.Result.ViewReslutCheckerboard.PoiResultCIExyuvDatas)
            {
                var item = poiResultCIExyuvData.Point;
                switch (item.PointType)
                {
                    case POIPointTypes.Circle:
                        DVCircleText Circle = new DVCircleText();
                        Circle.Attribute.Center = new Point(item.PixelX, item.PixelY);
                        Circle.Attribute.Radius = item.Radius;
                        Circle.Attribute.Brush = Brushes.Transparent;
                        Circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        Circle.Attribute.Id = item.Id ?? -1;
                        Circle.Attribute.Text = item.Name;
                        Circle.Attribute.Msg = CVRawOpen.FormatMessage("Y:@Y:F2", poiResultCIExyuvData);
                        Circle.Render();
                        ctx.ImageView.AddVisual(Circle);
                        break;
                    case POIPointTypes.Rect:
                        DVRectangleText Rectangle = new DVRectangleText();
                        Rectangle.Attribute.Rect = new Rect(item.PixelX - item.Width / 2, item.PixelY - item.Height / 2, item.Width, item.Height);
                        Rectangle.Attribute.Brush = Brushes.Transparent;
                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        Rectangle.Attribute.Id = item.Id ?? -1;
                        Rectangle.Attribute.Text = item.Name;
                        Rectangle.Attribute.Msg = CVRawOpen.FormatMessage("Y:@Y:F2", poiResultCIExyuvData);
                        Rectangle.Render();
                        ctx.ImageView.AddVisual(Rectangle);
                        break;
                    default:
                        break;
                }
            }
        }

        public string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"棋盘格 测试项：" + Environment.NewLine;

            if (result.ViewReslutCheckerboard.PoiResultCIExyuvDatas != null)
            {
                foreach (var item in result.ViewReslutCheckerboard.PoiResultCIExyuvDatas)
                {
                    outtext += $"{item.Name}  Y:{item.Y.ToString("F2")}{Environment.NewLine}";
                }
            }
            outtext += $"ChessboardContrast:{result.ViewReslutCheckerboard.ChessboardContrast.TestValue} LowLimit:{result.ViewReslutCheckerboard.ChessboardContrast.LowLimit}  UpLimit:{result.ViewReslutCheckerboard.ChessboardContrast.UpLimit},Rsult{(result.ViewReslutCheckerboard.ChessboardContrast.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            return outtext;
        }
    }
}
