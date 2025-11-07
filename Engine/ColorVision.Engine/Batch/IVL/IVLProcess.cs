using ColorVision.Database;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
            //properties.Add("CRI/Ra");
            properties.Add("FWHM");

            for (int i = 380; i <= 780; i++)
            {
                properties.Add(i.ToString());
            }
            for (int i = 0; i < properties.Count; i++)
            {
                csvBuilder.Append(properties[i]);

                if (i < properties.Count - 1)
                    csvBuilder.Append(',');
            }
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
                //csvBuilder.Append(result.fRa + ",");
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

    [BatchProcess("IVL完整处理", "处理IVL批次数据，包含Camera和Spectrum数据的导出")]
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
                int cout = 0;
                foreach (var master in masters)
                {
                    if (master.ImgFileType == ViewResultAlgType.POI_XYZ)
                    {
                        var poiPoints = PoiPointResultDao.Instance.GetAllByPid(master.Id);
                        cout = poiPoints.Count;
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
                foreach (var item in DB.Queryable<SMUResultModel>().Where(x => x.BatchId == ctx.Batch.Id).ToList())
                {
                    testResult.SMUResultModels.Add(item);
                }
                DB.Dispose();
                string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filePath = Path.Combine(config.SavePath, $"Camera_IVL_{timeStr}.csv");
                var rows = new List<string> { "Time,Meas_id,PoiName,Voltage(V),Current(mA),Lv(cd/m2),X,Y,Z,cx,cy,u',v',CCT(K),Dominant Wavelength" };

                string DateTimeNow = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                int z = 0;
                for (int i = 0; i < testResult.PoixyuvDatas.Count; i++)
                {
                    z = i / cout;
                    if (testResult.SMUResultModels.Count > z)
                    {
                        var SMUResultModel = testResult.SMUResultModels[z];
                        rows.Add($"{DateTimeNow},{i},{testResult.PoixyuvDatas[i].POIPointResultModel.PoiName},{SMUResultModel.VResult},{SMUResultModel.IResult},{testResult.PoixyuvDatas[i].Y},{testResult.PoixyuvDatas[i].X},{testResult.PoixyuvDatas[i].Y},{testResult.PoixyuvDatas[i].Z},{testResult.PoixyuvDatas[i].x},{testResult.PoixyuvDatas[i].y},{testResult.PoixyuvDatas[i].u},{testResult.PoixyuvDatas[i].v},{testResult.PoixyuvDatas[i].CCT},{testResult.PoixyuvDatas[i].Wave}");
                    }
                    else
                    {
                        rows.Add($"{DateTimeNow},{i},{testResult.PoixyuvDatas[i].POIPointResultModel.PoiName},,,{testResult.PoixyuvDatas[i].Y},{testResult.PoixyuvDatas[i].X},{testResult.PoixyuvDatas[i].Y},{testResult.PoixyuvDatas[i].Z},{testResult.PoixyuvDatas[i].x},{testResult.PoixyuvDatas[i].y},{testResult.PoixyuvDatas[i].u},{testResult.PoixyuvDatas[i].v},{testResult.PoixyuvDatas[i].CCT},{testResult.PoixyuvDatas[i].Wave}");

                    }
                }
                File.WriteAllLines(filePath, rows);


                var DB1 = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = MySqlControl.GetConnectionString(),
                    DbType = SqlSugar.DbType.MySql,
                    IsAutoCloseConnection = true
                });

                var list = DB1.Queryable<SpectumResultModel>().Where(x => x.BatchId == ctx.Batch.Id).ToList();
                DB1.Dispose();
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
                            viewResultSpectrum.V = SMUResultModel.VResult ??0;
                            viewResultSpectrum.I = SMUResultModel.IResult ??0;
                        }
                        else
                        {
                            viewResultSpectrum.V = float.NaN;
                            viewResultSpectrum.I = float.NaN;
                        }
                        i++;
                        ViewResults.Add(viewResultSpectrum);
                    }
                    string sprectrumfilePath = Path.Combine(config.SavePath, $"SP_IVL_{timeStr}.csv");
                    ViewResults.SaveToCsv(sprectrumfilePath);
                }
                
                // Show I-Lv curve plot window
                if (testResult.SMUResultModels.Count > 0 && (testResult.PoixyuvDatas.Count > 0 || ViewResults.Count > 0))
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                          
                            var plotWindow = new ILvPlotWindow(testResult.SMUResultModels, testResult.PoixyuvDatas, ViewResults.ToList());
                            plotWindow.Show();
                        }
                        catch (Exception ex)
                        {
                            log.Error("Failed to open I-Lv plot window", ex);
                        }
                    });
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
