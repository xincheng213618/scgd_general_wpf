using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using Dm.util;
using Newtonsoft.Json;
using ProjectARVRPro.Fix;
using ProjectARVRPro.Process.W255;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.Black
{
    public class BlackProcess : ProcessBase<BlackProcessConfig>
    {
        public override bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            BlackRecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<BlackRecipeConfig>();
            BlackFixConfig fixConfig = ctx.FixConfig.GetRequiredService<BlackFixConfig>();
            BlackViewTestResult testResult = new BlackViewTestResult();

            try
            {
                log?.Info("开始 Black 流程");

                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        ctx.Result.FileName = master.ImgFile;

                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        int id = 0;
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            testResult.ViewPoixyuvDatas.Add(poi);
                            testResult.PoixyuvDatas.Add(new PoixyuvData() { Id = poi.Id, Name = poi.Name, X = poi.X, Y = poi.Y, Z = poi.Z, x = poi.x, y = poi.y, u = poi.u, v = poi.v, CCT = poi.CCT, Wave = poi.Wave });
                        }
                        // 需要白画面亮度才能计算对比度
                        if (ctx.ObjectiveTestResult.W255TestResult != null && ctx.ObjectiveTestResult.W255TestResult.CenterLunimance != null)
                        {
                            double contrast = ctx.ObjectiveTestResult.W255TestResult.CenterLunimance.Value / testResult.ViewPoixyuvDatas[0].Y;
                            contrast *= fixConfig.FOFOContrast;
                            testResult.FOFOContrast.LowLimit = recipeConfig.FOFOContrast.Min;
                            testResult.FOFOContrast.UpLimit = recipeConfig.FOFOContrast.Max;
                            testResult.FOFOContrast.Value = contrast;
                            testResult.FOFOContrast.TestValue = contrast.ToString("F2");

                            ctx.Result.Result &= testResult.FOFOContrast.TestResult;
                        }
                        else
                        {
                            log?.Info("计算对比度前需要白画面亮度");
                        }
                    }
                }

                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.BlackTestResult = JsonConvert.DeserializeObject<BlackTestResult>(ctx.Result.ViewResultJson) ?? new BlackTestResult();

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
            BlackViewTestResult testResult = JsonConvert.DeserializeObject<BlackViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;


            foreach (var poiResultCIExyuvData in testResult.ViewPoixyuvDatas)
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

        public override string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;

            outtext += $"黑画面" + Environment.NewLine;
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return string.Empty;
            BlackViewTestResult testResult = JsonConvert.DeserializeObject<BlackViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return string.Empty;

            outtext += $"FOFOContrast:{testResult.FOFOContrast.TestValue}  LowLimit:{testResult.FOFOContrast.LowLimit} UpLimit:{testResult.FOFOContrast.UpLimit},Rsult{(testResult.FOFOContrast.TestResult ? "PASS" : "Fail")}{Environment.NewLine}";
            return outtext;
        }

        public override IRecipeConfig GetRecipeConfig()
        {
            return RecipeManager.GetInstance().RecipeConfig.GetRequiredService<BlackRecipeConfig>();
        }

        public override IFixConfig GetFixConfig()
        {
            return FixManager.GetInstance().FixConfig.GetRequiredService<BlackFixConfig>();
        }
    }
}
