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
using System.Text;

namespace ColorVision.Engine.Batch.IVL
{
    public static class IVLViewResultSpectrumExt
    {
        public static void SaveToCsv(this ObservableCollection<ViewResultSpectrum> ViewResultSpectrums, string csv)
        {
            var csvBuilder = new StringBuilder();

            List<string> properties = new();
            properties.Add("Time");
            properties.Add("Meas_id");
            properties.Add("Voltage/V");
            properties.Add("Current/mA");
            properties.Add("Lv(cd/m2)");
            properties.Add("IP");
            properties.Add("BlueLight");
            properties.Add("cx");
            properties.Add("cy");
            properties.Add("u'");
            properties.Add("v'");
            properties.Add("CCT(K)");
            properties.Add("Dominant Wavelength(nm)");
            properties.Add("Saturation(%)");
            properties.Add("Peak Wavelength(nm)");
            properties.Add("CRI/Ra");
            properties.Add("FWHM");

            for (int i = 380; i <= 780; i++)
            {
                properties.Add(i.ToString());
            }
            // д����ͷ
            for (int i = 0; i < properties.Count; i++)
            {
                // �������
                csvBuilder.Append(properties[i]);

                // ����������һ�У�����Ӷ���
                if (i < properties.Count - 1)
                    csvBuilder.Append(',');
            }
            // ��ӻ��з�
            csvBuilder.AppendLine();
            foreach (var result in ViewResultSpectrums)
            {
                csvBuilder.Append(result.CreateTime + ",");
                csvBuilder.Append(result.Id + ",");
                csvBuilder.Append(result.V + ",");
                csvBuilder.Append(result.I + ",");
                csvBuilder.Append(result.Lv + ",");
                csvBuilder.Append(result.IP + ",");
                csvBuilder.Append(result.Blue + ",");
                csvBuilder.Append(result.fx + ",");
                csvBuilder.Append(result.fy + ",");
                csvBuilder.Append(result.fu + ",");
                csvBuilder.Append(result.fv + ",");
                csvBuilder.Append(result.fCCT + ",");
                csvBuilder.Append(result.fLd + ",");
                csvBuilder.Append(result.fPur + ",");
                csvBuilder.Append(result.fLp + ",");
                csvBuilder.Append(result.fRa + ",");
                csvBuilder.Append(result.fHW + ",");


                for (int i = 0; i < result.SpectralDatas.Count; i++)
                {
                    csvBuilder.Append(result.SpectralDatas[i].AbsoluteSpectrum);
                    csvBuilder.Append(',');
                }
                csvBuilder.AppendLine();
            }
            File.WriteAllText(csv, csvBuilder.ToString(), Encoding.UTF8);

        }
    }

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

                var list = MySqlControl.GetInstance().DB.Queryable<SpectumResultModel>().Where(x => x.BatchId == ctx.Batch.Id).ToList();
                ObservableCollection<ViewResultSpectrum> ViewResults = new ObservableCollection<ViewResultSpectrum>();
                if (list.Count == 0)
                {
                    log.Info("�Ҳ��������ǵ�����");
                    string sprectrumfilePath = Path.Combine(config.SavePath, $"SP_IVL_{timeStr}.csv");
                    ViewResults.SaveToCsv(sprectrumfilePath);
                }
                else
                {
                    int i = 0;
                    foreach (var item in list)
                    {
                        ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(item);
                        if (testResult.SMUResultModels.Count > i)
                        {
                            var SMUResultModel = testResult.SMUResultModels[i];
                            viewResultSpectrum.V = SMUResultModel.VResult;
                            viewResultSpectrum.I = SMUResultModel.IResult;
                        }
                        else
                        {
                            viewResultSpectrum.V = float.NaN;
                            viewResultSpectrum.I = float.NaN;
                        }
 
                        ViewResults.Add(viewResultSpectrum);
                    }
                    string sprectrumfilePath = Path.Combine(config.SavePath, $"SP_IVL_{timeStr}.csv");
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
