using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.MTF
{
    public class MTFProcess : ProcessBase<MTFProcessConfig>
    {
        public override bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            MTFViewTestResult testResult = new MTFViewTestResult();

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
                        if (mtfDetail.MTFResult?.resultChild != null)
                        {
                            foreach (var mtf in mtfDetail.MTFResult.resultChild)
                            {
                                string displayName = Config.GetDisplayName(mtf.name);

                                // Horizontal item
                                var hItem = new ObjectiveTestItem()
                                {
                                    Name = displayName + "_H",
                                    Unit = "%",
                                    Value = mtf.horizontalAverage * Config.FixH,
                                    LowLimit = Config.UnifiedRecipe.Min,
                                    UpLimit = Config.UnifiedRecipe.Max,
                                };
                                hItem.TestValue = hItem.Value.ToString();
                                ctx.Result.Result &= hItem.TestResult;
                                testResult.Items.Add(hItem);

                                // Vertical item
                                var vItem = new ObjectiveTestItem()
                                {
                                    Name = displayName + "_V",
                                    Unit = "%",
                                    Value = mtf.verticalAverage * Config.FixV,
                                    LowLimit = Config.UnifiedRecipe.Min,
                                    UpLimit = Config.UnifiedRecipe.Max,
                                };
                                vItem.TestValue = vItem.Value.ToString();
                                ctx.Result.Result &= vItem.TestResult;
                                testResult.Items.Add(vItem);
                            }
                        }
                        testResult.MTFDetailViewReslut = mtfDetail;
                    }
                }

                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);

                // Add to ObjectiveTestResult via dynamic dictionary
                if (!ctx.ObjectiveTestResult.DynamicTestResults.ContainsKey(Config.Name))
                {
                    ctx.ObjectiveTestResult.DynamicTestResults[Config.Name] = new ObservableCollection<ObjectiveTestItem>();
                }
                foreach (var item in testResult.Items)
                {
                    ctx.ObjectiveTestResult.DynamicTestResults[Config.Name].Add(item);
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
            MTFViewTestResult testResult = JsonConvert.DeserializeObject<MTFViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            int id = 0;

            if (testResult.MTFDetailViewReslut?.MTFResult?.result != null && testResult.MTFDetailViewReslut.MTFResult.result.Count != 0)
            {
                foreach (var item in testResult.MTFDetailViewReslut.MTFResult.result)
                {
                    id++;
                    DVRectangleText Rectangle = new();
                    Rectangle.Attribute.Rect = new Rect(item.x, item.y, item.w, item.h);
                    Rectangle.Attribute.Brush = Brushes.Transparent;
                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                    Rectangle.Attribute.Id = id;
                    Rectangle.Attribute.Msg = item.mtfValue?.ToString(Config.ShowConfig);
                    Rectangle.Render();
                    ctx.ImageView.AddVisual(Rectangle);
                }
            }
        }

        public override string GenText(IProcessExecutionContext ctx)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{Config.Name} 画面结果");

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return sb.ToString();

            MTFTestResult testResult = JsonConvert.DeserializeObject<MTFTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return sb.ToString();

            sb.AppendLine("Name,Value,Unit,LowLimit,UpLimit,Result");

            foreach (var item in testResult.Items)
            {
                sb.AppendLine($"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{item.TestResult}");
            }

            return sb.ToString();
        }
    }
}
