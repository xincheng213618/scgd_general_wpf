using ColorVision.Database;
using ColorVision.Engine; // DAOs
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;
using ProjectARVRPro.Fix;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.W25
{
    public class W25Process : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            W25RecipeConfig recipeConfig = ctx.RecipeConfig.GetRequiredService<W25RecipeConfig>();
            W25FixConfig fixConfig = ctx.FixConfig.GetRequiredService<W25FixConfig>();
            W25ViewTestResult testResult = new W25ViewTestResult();


            try
            {
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                if (values.Count > 0)
                    ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        int id = 0;
                        testResult.ViewPoixyuvDatas.Clear();
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            poi.Y *= fixConfig.CenterLunimance;
                            poi.x *= fixConfig.CenterCIE1931ChromaticCoordinatesx;
                            poi.y *= fixConfig.CenterCIE1931ChromaticCoordinatesy;
                            poi.u *= fixConfig.CenterCIE1976ChromaticCoordinatesu;
                            poi.v *= fixConfig.CenterCIE1976ChromaticCoordinatesv;
                            testResult.ViewPoixyuvDatas.Add(poi);
                            testResult.PoixyuvDatas.Add(new PoixyuvData() { Id = poi.Id, Name = poi.Name, X = poi.X, Y = poi.Y, Z = poi.Z, x = poi.x, y = poi.y, u = poi.u, v = poi.v, CCT = poi.CCT, Wave = poi.Wave });

                            if (item.PoiName == "P_9")
                            {
                                testResult.CenterLunimance = new ObjectiveTestItem
                                {
                                    Name = "CenterLunimance",
                                    LowLimit = recipeConfig.CenterLunimance.Min,
                                    UpLimit = recipeConfig.CenterLunimance.Max,
                                    Value = poi.Y,
                                    TestValue = poi.Y.ToString("F4") + " nit"
                                };
                                testResult.CenterCIE1931ChromaticCoordinatesx = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1931ChromaticCoordinatesx",
                                    LowLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesx.Min,
                                    UpLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesx.Max,
                                    Value = poi.x,
                                    TestValue = poi.x.ToString("F4")
                                };
                                testResult.CenterCIE1931ChromaticCoordinatesy = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1931ChromaticCoordinatesy",
                                    LowLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesy.Min,
                                    UpLimit = recipeConfig.CenterCIE1931ChromaticCoordinatesy.Max,
                                    Value = poi.y,
                                    TestValue = poi.y.ToString("F4")
                                };
                                testResult.CenterCIE1976ChromaticCoordinatesu = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1976ChromaticCoordinatesu",
                                    LowLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesu.Min,
                                    UpLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesu.Max,
                                    Value = poi.u,
                                    TestValue = poi.u.ToString("F4")
                                };
                                testResult.CenterCIE1976ChromaticCoordinatesv = new ObjectiveTestItem
                                {
                                    Name = "CenterCIE1976ChromaticCoordinatesv",
                                    LowLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesv.Min,
                                    UpLimit = recipeConfig.CenterCIE1976ChromaticCoordinatesv.Max,
                                    Value = poi.v,
                                    TestValue = poi.v.ToString("F4")
                                };

                                ctx.Result.Result &= testResult.CenterLunimance.TestResult;
                                ctx.Result.Result &= testResult.CenterCIE1931ChromaticCoordinatesx.TestResult;
                                ctx.Result.Result &= testResult.CenterCIE1931ChromaticCoordinatesy.TestResult;
                                ctx.Result.Result &= testResult.CenterCIE1976ChromaticCoordinatesu.TestResult;
                                ctx.Result.Result &= testResult.CenterCIE1976ChromaticCoordinatesv.TestResult;
                            }
                        }
                    }
                }

                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.W25TestResult = JsonConvert.DeserializeObject<W25TestResult>(ctx.Result.ViewResultJson) ?? new W25TestResult();
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
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            W25ViewTestResult testResult = JsonConvert.DeserializeObject<W25ViewTestResult>(ctx.Result.ViewResultJson);
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

        public string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"W25 画面结果" + Environment.NewLine;

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return outtext;
            W25ViewTestResult testResult = JsonConvert.DeserializeObject<W25ViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return outtext;

            foreach (var item in testResult.ViewPoixyuvDatas)
            {
                outtext += $"X:{item.X.ToString("F2")} Y:{item.Y.ToString("F2")} Z:{item.Z.ToString("F2")} x:{item.x.ToString("F2")} y:{item.y.ToString("F2")} u:{item.u.ToString("F2")} v:{item.v.ToString("F2")} cct:{item.CCT.ToString("F2")} wave:{item.Wave.ToString("F2")}{Environment.NewLine}";
            }

            return outtext;
        }

        public IRecipeConfig GetRecipeConfig()
        {
            return RecipeManager.GetInstance().RecipeConfig.GetRequiredService<W25RecipeConfig>();
        }

        public IFixConfig GetFixConfig()
        {
            return FixManager.GetInstance().FixConfig.GetRequiredService<W25FixConfig>();
        }
    }
}
