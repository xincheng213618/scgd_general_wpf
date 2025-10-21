using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.Black
{
    public class BlackProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            BlackRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<BlackRecipeConfig>();

            try
            {
                log?.Info("处理 Black 流程结果");

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        ctx.Result.ViewResultBlack.PoiResultCIExyuvDatas = new List<PoiResultCIExyuvData>();
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        int id = 0;
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            ctx.Result.ViewResultBlack.PoiResultCIExyuvDatas.Add(poi);
                        }
                        // 需要白画面的亮度才能计算对比度
                        if (ctx.Result.ViewResultWhite != null && ctx.Result.ViewResultWhite.PoiResultCIExyuvDatas != null && ctx.Result.ViewResultWhite.PoiResultCIExyuvDatas.Count == 9 && ctx.Result.ViewResultBlack.PoiResultCIExyuvDatas.Count == 1)
                        {
                            double contrast = ctx.Result.ViewResultWhite.PoiResultCIExyuvDatas[5].Y / ctx.Result.ViewResultBlack.PoiResultCIExyuvDatas[0].Y;
                            contrast *= ctx.ObjectiveTestResultFix.FOFOContrast;
                            var fofo = new ObjectiveTestItem
                            {
                                Name = "FOFOContrast",
                                LowLimit = recipeConfig.FOFOContrastMin,
                                UpLimit = recipeConfig.FOFOContrastMax,
                                Value = contrast,
                                TestValue = contrast.ToString("F2")
                            };
                            ctx.ObjectiveTestResult.FOFOContrast = fofo;
                            ctx.Result.ViewResultBlack.FOFOContrast = fofo;
                            ctx.Result.Result &= fofo.TestResult;
                        }
                        else
                        {
                            log?.Info("计算对比度前需要白画面数据");
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
            foreach (var poiResultCIExyuvData in ctx.Result.ViewResultBlack.PoiResultCIExyuvDatas)
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
                        Circle.Attribute.Msg = CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);
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
                        Rectangle.Attribute.Msg = CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);
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

            outtext += $"黑画面 测试项结果)" + Environment.NewLine;
            if (result.ViewResultBlack.PoiResultCIExyuvDatas != null)
            {
                foreach (var item in result.ViewResultBlack.PoiResultCIExyuvDatas)
                {
                    outtext += $"{item.Name}  X:{item.X.ToString("F2")} Y:{item.Y.ToString("F2")} Z:{item.Z.ToString("F2")} x:{item.x.ToString("F2")} y:{item.y.ToString("F2")} u:{item.u.ToString("F2")} v:{item.v.ToString("F2")} cct:{item.CCT.ToString("F2")} wave:{item.Wave.ToString("F2")}{Environment.NewLine}";
                }
            }
            if(result.ViewResultBlack.FOFOContrast != null)
            {
                outtext += $"FOFOContrast:{result.ViewResultBlack.FOFOContrast.TestValue}  LowLimit:{result.ViewResultBlack.FOFOContrast.LowLimit} UpLimit:{result.ViewResultBlack.FOFOContrast.UpLimit},Rsult{(result.ViewResultBlack.FOFOContrast.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            }
            else
            {
                outtext += $"FOFOContrast: N/A (缺少白画面数据，无法计算对比度)" + Environment.NewLine;
            }
            return outtext;
        }

    }
}
