using ColorVision.Database;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.Engine.Batch.Poi
{
    [BatchProcess("POI处理", "处理POI批次数据并导出CIE xyuv数据")]
    public class IPoiProcess : IBatchProcess
    {
        public bool Process(IBatchContext ctx)
        {
            if (ctx?.Batch == null) return false;
            var config = ctx.Config;
            try
            {
                //var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                ////if (values.Count > 0)
                //    //ctx.Result.FileName = values[0].FileUrl;

                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);

                        ObservableCollection<PoiResultCIExyuvData> PoiResultCIExyuvDatas = new ObservableCollection<PoiResultCIExyuvData>();

                        foreach (var item in poiPoints)
                        {
                            PoiResultCIExyuvDatas.Add(new PoiResultCIExyuvData(item));
                        }
                        string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string filePath = Path.Combine(config.SavePath, $"Poi_{timeStr}.csv");
                        PoiResultCIExyuvDatas.SaveCsv(filePath);

                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
