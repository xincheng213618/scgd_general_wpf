#pragma warning disable CS8601, CS8602
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.POI
{
    public class PoiDynamicProcess : ProcessBase<PoiDynamicProcessConfig>
    {
        public override async Task<bool> Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null || ctx.ObjectiveTestResult == null) return false;

            var log = ctx.Log;
            var testResult = new PoiDynamicTestResult();
            var sourcePoixyuvDatas = new List<PoiResultCIExyuvData>();

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
                        if (File.Exists(master.ImgFile))
                        {
                            ctx.Result.FileName = master.ImgFile;
                        }
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        if (poiPoints.Count == 0) continue;
                        int id = sourcePoixyuvDatas.Count;
                        foreach (var item in poiPoints)
                        {
                            var poi = new PoiResultCIExyuvData(item) { Id = id++ };
                            sourcePoixyuvDatas.Add(poi);
                            testResult.ViewPoixyuvDatas.Add(poi);
                            testResult.PoixyuvDatas.Add(ToPoixyuvData(poi));
                        }
                    }

                }

                string outputName = GetOutputName();
                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.ObjectiveTestResult.DynamicPoixyuvDatas ??= new Dictionary<string, ObservableCollection<PoixyuvData>>();
                ctx.ObjectiveTestResult.DynamicPoixyuvDatas[outputName] = new ObservableCollection<PoixyuvData>(testResult.PoixyuvDatas);

                if (Config.SaveCsv)
                    SavePoixyuvDatas(sourcePoixyuvDatas, ctx, outputName);

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

            PoiDynamicTestResult? testResult = JsonConvert.DeserializeObject<PoiDynamicTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            foreach (var poiResultCIExyuvData in testResult.ViewPoixyuvDatas)
            {
                var item = poiResultCIExyuvData.Point;
                switch (item.PointType)
                {
                    case POIPointTypes.Circle:
                        DVCircleText circle = new DVCircleText();
                        circle.Attribute.Center = new Point(item.PixelX, item.PixelY);
                        circle.Attribute.Radius = item.Radius;
                        circle.Attribute.Brush = Brushes.Transparent;
                        circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        circle.Attribute.Id = item.Id ?? -1;
                        circle.Attribute.Text = item.Name;
                        circle.Attribute.Msg = CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);
                        circle.Render();
                        ctx.ImageView.AddVisual(circle);
                        break;
                    case POIPointTypes.Rect:
                        DVRectangleText rectangle = new DVRectangleText();
                        rectangle.Attribute.Rect = new Rect(item.PixelX - item.Width / 2, item.PixelY - item.Height / 2, item.Width, item.Height);
                        rectangle.Attribute.Brush = Brushes.Transparent;
                        rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        rectangle.Attribute.Id = item.Id ?? -1;
                        rectangle.Attribute.Text = item.Name;
                        rectangle.Attribute.Msg = CVRawOpen.FormatMessage(CVCIEShowConfig.Instance.Template, poiResultCIExyuvData);
                        rectangle.Render();
                        ctx.ImageView.AddVisual(rectangle);
                        break;
                    default:
                        break;
                }
            }
        }

        public override void GenText(IProcessExecutionContext ctx, System.Windows.Documents.Paragraph paragraph, System.Windows.Media.Brush foreground, double fontSize)
        {
            string outtext = $"{GetOutputName()} 关注点结果" + Environment.NewLine;

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) { AppendPlainText(paragraph, outtext, foreground, fontSize); return; }

            var testResult = JsonConvert.DeserializeObject<PoiDynamicTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) { AppendPlainText(paragraph, outtext, foreground, fontSize); return; }

            foreach (var item in testResult.PoixyuvDatas)
            {
                outtext += $"{item.Name} X:{Format(item.X)} Y:{Format(item.Y)} Z:{Format(item.Z)} x:{Format(item.x)} y:{Format(item.y)} u:{Format(item.u)} v:{Format(item.v)} cct:{Format(item.CCT)} wave:{Format(item.Wave)}{Environment.NewLine}";
            }

            AppendPlainText(paragraph, outtext, foreground, fontSize); return;
        }

        private string GetOutputName()
        {
            return string.IsNullOrWhiteSpace(Config.Name) ? "POI" : Config.Name.Trim();
        }

        private string Format(double value)
        {
            return value.ToString(Config.ShowConfig);
        }

        private static PoixyuvData ToPoixyuvData(PoiResultCIExyuvData poi)
        {
            return new PoixyuvData
            {
                Id = poi.Id,
                Name = poi.Name,
                CCT = poi.CCT,
                Wave = poi.Wave,
                X = poi.X,
                Y = poi.Y,
                Z = poi.Z,
                u = poi.u,
                v = poi.v,
                x = poi.x,
                y = poi.y
            };
        }

        private static void SavePoixyuvDatas(IEnumerable<PoiResultCIExyuvData>? poixyuvDatas, IProcessExecutionContext ctx, string outputName)
        {
            try
            {
                ObservableCollection<PoiResultCIExyuvData> items = new ObservableCollection<PoiResultCIExyuvData>();
                if (poixyuvDatas != null)
                {
                    foreach (var item in poixyuvDatas)
                    {
                        items.Add(item);
                    }
                }

                string exportDir = GetExportDirectory(ctx);
                string safeOutputName = SanitizeFileName(string.IsNullOrWhiteSpace(outputName) ? "POI" : outputName);
                string filePath = Path.Combine(exportDir, $"{safeOutputName}_PoixyuvDatas_{DateTime.Now:yyyyMMdd_HHmmssfff}.csv");
                items.SaveCsv(filePath);
                ctx.Log.Info($"POI PoixyuvDatas CSV saved: {filePath}");
            }
            catch (Exception ex)
            {
                ctx.Log.Error("POI PoixyuvDatas CSV save failed", ex);
            }
        }

        private static string GetExportDirectory(IProcessExecutionContext ctx)
        {
            var config = ViewResultManager.GetInstance().Config;
            string exportDir = string.IsNullOrWhiteSpace(config.CsvSavePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ARVR")
                : config.CsvSavePath;

            if (config.SaveByDate)
            {
                exportDir = Path.Combine(exportDir, DateTime.Now.ToString("yyyy-MM-dd"));
            }

            string? batchName = ctx.Batch?.Name;
            if (string.IsNullOrWhiteSpace(batchName))
                batchName = ctx.Result?.SN;
            if (string.IsNullOrWhiteSpace(batchName))
                batchName = ctx.Batch?.Id.ToString();

            batchName = SanitizeFileName(batchName);
            if (!string.IsNullOrWhiteSpace(batchName))
            {
                exportDir = Path.Combine(exportDir, batchName);
            }

            Directory.CreateDirectory(exportDir);
            return exportDir;
        }

        private static string SanitizeFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            return fileName.Trim();
        }
    }
}
