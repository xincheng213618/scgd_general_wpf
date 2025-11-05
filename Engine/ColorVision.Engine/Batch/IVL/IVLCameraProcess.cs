using ColorVision.Database;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;

namespace ColorVision.Engine.Batch.IVL
{

    [BatchProcess("IVL相机处理", "仅处理IVL批次中的Camera数据并导出")]
    public class IVLCameraProcess : IBatchProcess
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(IVLProcess));

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

                var DB = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = MySqlControl.GetConnectionString(),
                    DbType = SqlSugar.DbType.MySql,
                    IsAutoCloseConnection = true
                });
                foreach (var item in DB.Queryable<SMUResultModel>().Where(x => x.Batchid == ctx.Batch.Id).ToList())
                {
                    testResult.SMUResultModels.Add(item);
                }
                DB.Dispose();

                string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filePath = Path.Combine(config.SavePath, $"Camera_IVL_{timeStr}.csv");
                var rows = new List<string> { "Time,Meas_id,POI_id,Voltage(V),Current(mA),Lv(cd/m2),X,Y,Z,cx,cy,u',v',CCT(K),Dominant Wavelength" };

                string DateTimeNow = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                for (int i = 0; i < testResult.PoixyuvDatas.Count;i++)
                {
                    if (testResult.SMUResultModels.Count > i)
                    {
                        var SMUResultModel = testResult.SMUResultModels[i];
                        rows.Add($"{DateTimeNow},{i},{testResult.PoixyuvDatas[i].Id},{SMUResultModel.VResult},{SMUResultModel.IResult},{testResult.PoixyuvDatas[i].Y},{testResult.PoixyuvDatas[i].X},{testResult.PoixyuvDatas[i].Y},{testResult.PoixyuvDatas[i].Z},{testResult.PoixyuvDatas[i].x},{testResult.PoixyuvDatas[i].y},{testResult.PoixyuvDatas[i].u},{testResult.PoixyuvDatas[i].v},{testResult.PoixyuvDatas[i].CCT},{testResult.PoixyuvDatas[i].Wave}");
                    }
                    else
                    {
                        rows.Add($"{DateTimeNow},{i},{testResult.PoixyuvDatas[i].Id},,,{testResult.PoixyuvDatas[i].Y},{testResult.PoixyuvDatas[i].X},{testResult.PoixyuvDatas[i].Y},{testResult.PoixyuvDatas[i].Z},{testResult.PoixyuvDatas[i].x},{testResult.PoixyuvDatas[i].y},{testResult.PoixyuvDatas[i].u},{testResult.PoixyuvDatas[i].v},{testResult.PoixyuvDatas[i].CCT},{testResult.PoixyuvDatas[i].Wave}");

                    }
                }
                File.WriteAllLines(filePath, rows);
                //ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                return true;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return false;
            }
        }
    }
}
