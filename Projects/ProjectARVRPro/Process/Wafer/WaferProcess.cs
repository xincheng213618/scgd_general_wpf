using ColorVision.Database;
using ColorVision.Engine; // AlgResultMasterDao, MeasureImgResultDao, DeatilCommonDao
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.Engine.Templates.Jsons; // DetailCommonModel
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis; // PoiAnalysisDetailViewReslut
using ColorVision.Engine.Templates.POI.AlgorithmImp; // PoiPointResultModel
using ColorVision.ImageEditor.Draw;
using CVCommCore.CVAlgorithm;
using Dm.util;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ProjectARVRPro.Process.Wafer
{
    public class WaferProcess : IProcess
    {
        public bool Execute(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null) return false;
            var log = ctx.Logger;
            WaferViewTestResult testResult = new WaferViewTestResult();

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

                        foreach (var item in poiPoints)
                        {
                            testResult.PoixyuvDatas.add(new PoiResultCIExyuvData(item));
                        }
                    }
                }
                foreach (var item in MySqlControl.GetInstance().DB.Queryable<SMUResultModel>().Where(x=>x.Batchid == ctx.Batch.Id).ToList())
                {
                    testResult.SMUResultModels.Add(item);
                }
                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);

                string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filePath = Path.Combine(ViewResultManager.GetInstance().Config.CsvSavePath, $"Wafer_{timeStr}.csv");
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
                log?.Error(ex);
                return false;
            }
        }

        public void Render (IProcessExecutionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;
            WaferViewTestResult testResult = JsonConvert.DeserializeObject<WaferViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;
        }

        public string GenText(IProcessExecutionContext ctx)
        {
            var result = ctx.Result;
            string outtext = string.Empty;
            outtext += $"Wafer" + Environment.NewLine;

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return outtext;
            WaferViewTestResult testResult = JsonConvert.DeserializeObject<WaferViewTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return outtext;
            foreach (var item in testResult.PoixyuvDatas)
            {
                outtext += $"X:{item.X.ToString("F2")} Y:{item.Y.ToString("F2")} Z:{item.Z.ToString("F2")} x:{item.x.ToString("F2")} y:{item.y.ToString("F2")} u:{item.u.ToString("F2")} v:{item.v.ToString("F2")} cct:{item.CCT.ToString("F2")} wave:{item.Wave.ToString("F2")}{Environment.NewLine}";
            }
            return outtext;
        }
    }
}
