using ColorVision.Engine.Templates.POI.AlgorithmImp;
using log4net;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.Engine.FlowProcessing.PostProcess.Poi
{
    [PostProcess("POI处理", "处理POI批次数据并导出CIE数据")]
    public class PoiPostProcessor : IPostProcessor
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PoiPostProcessor));

        public bool Process(PostProcessContext ctx)
        {
            if (ctx?.Batch == null) return false;
            if (ctx?.Batch.FlowStatus != FlowStatus.Completed) return false;

            var config = ctx.Config;
            try
            {
                using var db = ctx.CreateBatchDatabaseClient();
                var masters = db.Queryable<AlgResultMasterModel>().Where(item => item.BatchId == ctx.Batch.Id).ToList();
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        var poiPoints = db.Queryable<PoiPointResultModel>().Where(item => item.Pid == master.Id).ToList();

                        ObservableCollection<PoiResultCIExyuvData> PoiResultCIExyuvDatas = new ObservableCollection<PoiResultCIExyuvData>();

                        foreach (var item in poiPoints)
                        {
                            PoiResultCIExyuvDatas.Add(new PoiResultCIExyuvData(item));
                        }

                        string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string filePath = Path.Combine(config.SavePath, $"Poi_{master.Id}_{timeStr}.csv");

                        PoiResultCIExyuvDatas.SaveCsv(filePath);
                    }
                    if (master.ImgFileType == ViewResultAlgType.POI_Y)
                    {
                        var poiPoints = db.Queryable<PoiPointResultModel>().Where(item => item.Pid == master.Id).ToList();

                        ObservableCollection<PoiResultCIEYData> PoiResultCIEYDatas = new ObservableCollection<PoiResultCIEYData>();

                        foreach (var item in poiPoints)
                        {
                            PoiResultCIEYDatas.Add(new PoiResultCIEYData(item));
                        }
                        string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string filePath = Path.Combine(config.SavePath, $"PoiY_{master.Id}_{timeStr}.csv");
                        PoiResultCIEYData.SaveCsv(PoiResultCIEYDatas,filePath);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                log.Error("POI 后处理读取批次结果或导出失败。", ex);
                return false;
            }
        }
    }
}
