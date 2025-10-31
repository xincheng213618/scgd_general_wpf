using ColorVision.Database;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace ColorVision.Engine.Batch.IVL
{
    public class IVLProcess : IBatchProcess
    {
        public bool Process(IBatchContext ctx)
        {
            if (ctx?.Batch == null) return false;
            var config = ctx.Config;

            IVLViewTestResult testResult = new IVLViewTestResult();
            try
            {
                var values = MeasureImgResultDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                //if (values.Count > 0)
                    //ctx.Result.FileName = values[0].FileUrl;
                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);

                        foreach (var item in poiPoints)
                        {
                            testResult.PoixyuvDatas.Add(new PoiResultCIExyuvData(item));
                        }
                    }
                }
                foreach (var item in MySqlControl.GetInstance().DB.Queryable<SMUResultModel>().Where(x=>x.Batchid == ctx.Batch.Id).ToList())
                {
                    testResult.SMUResultModels.Add(item);
                }
                //ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);

                string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                string filePath = Path.Combine(config.SavePath, $"Wafer_{timeStr}.csv");

                var rows = new List<string> { "X,Y,Z,x,y,u,v,CCT,Wave,V,I" };

                for (int i = 0; i < testResult.PoixyuvDatas.Count;i++)
                {
                    rows.Add($"{testResult.PoixyuvDatas[i].X},{testResult.PoixyuvDatas[i].Y},{testResult.PoixyuvDatas[i].Z},{testResult.PoixyuvDatas[i].x},{testResult.PoixyuvDatas[i].y},{testResult.PoixyuvDatas[i].u},{testResult.PoixyuvDatas[i].v},{testResult.PoixyuvDatas[i].CCT},{testResult.PoixyuvDatas[i].Wave},{testResult.SMUResultModels[i].VResult},{testResult.SMUResultModels[i].IResult}");
                }
                File.WriteAllLines(filePath, rows);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
