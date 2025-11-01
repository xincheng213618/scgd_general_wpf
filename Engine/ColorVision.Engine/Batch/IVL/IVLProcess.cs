using ColorVision.Database;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.Engine.Batch.IVL
{
    public class IVLProcess : IBatchProcess
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
                foreach (var item in MySqlControl.GetInstance().DB.Queryable<SMUResultModel>().Where(x=>x.Batchid == ctx.Batch.Id).ToList())
                {
                    testResult.SMUResultModels.Add(item);
                }

                string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filePath = Path.Combine(config.SavePath, $"ivl_{timeStr}.csv");
                var rows = new List<string> { "Time,id,X,Y,Z,x,y,u,v,CCT,Wave,V,I" };

                string DateTimeNow = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                for (int i = 0; i < testResult.PoixyuvDatas.Count;i++)
                {
                    if (testResult.SMUResultModels.Count > i)
                    {
                        var SMUResultModel = testResult.SMUResultModels[i];
                        rows.Add($"{DateTimeNow}{i}{testResult.PoixyuvDatas[i].X},{testResult.PoixyuvDatas[i].Y},{testResult.PoixyuvDatas[i].Z},{testResult.PoixyuvDatas[i].x},{testResult.PoixyuvDatas[i].y},{testResult.PoixyuvDatas[i].u},{testResult.PoixyuvDatas[i].v},{testResult.PoixyuvDatas[i].CCT},{testResult.PoixyuvDatas[i].Wave},{SMUResultModel.VResult},{SMUResultModel.IResult}");
                    }
                    else
                    {
                        rows.Add($"{DateTimeNow}{i}{testResult.PoixyuvDatas[i].X},{testResult.PoixyuvDatas[i].Y},{testResult.PoixyuvDatas[i].Z},{testResult.PoixyuvDatas[i].x},{testResult.PoixyuvDatas[i].y},{testResult.PoixyuvDatas[i].u},{testResult.PoixyuvDatas[i].v},{testResult.PoixyuvDatas[i].CCT},{testResult.PoixyuvDatas[i].Wave},,");

                    }
                }
                File.WriteAllLines(filePath, rows);

                var list = MySqlControl.GetInstance().DB.Queryable<SpectumResultModel>().Where(x => x.BatchId == ctx.Batch.Id).ToList();
                ObservableCollection<ViewResultSpectrum> ViewResults = new ObservableCollection<ViewResultSpectrum>();
                if (list.Count == 0)
                {
                    log.Info("找不到光谱仪的数据");
                    string sprectrumfilePath = Path.Combine(config.SavePath, $"sprectrum_{timeStr}.csv");
                    ViewResults.SaveToCsv(sprectrumfilePath);
                }
                else
                {
                    foreach (var item in list)
                    {
                        ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(item);
                        viewResultSpectrum.V = float.NaN;
                        viewResultSpectrum.I = float.NaN;
                        ViewResults.Add(viewResultSpectrum);
                    }
                    string sprectrumfilePath = Path.Combine(config.SavePath, $"sprectrum_{timeStr}.csv");
                    ViewResults.SaveToCsv(sprectrumfilePath);
                }

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
