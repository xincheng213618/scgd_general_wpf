using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Templates.Flow;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Engine.Batch.Eqe
{
    public static class EqeViewResultExt
    {
        public static void SaveToCsv(this ObservableCollection<ViewResultSpectrum> viewResults, string csv)
        {
            var csvBuilder = new StringBuilder();

            List<string> properties = new();
            properties.Add("Time");
            properties.Add("Meas_id");
            properties.Add("Voltage/V");
            properties.Add("Current/mA");
            properties.Add("Lv(cd/m2)");
            properties.Add("IP");
            properties.Add("EQE");
            properties.Add("LuminousFlux(lm)");
            properties.Add("RadiantFlux(W)");
            properties.Add("LuminousEfficacy(lm/W)");
            properties.Add("cx");
            properties.Add("cy");
            properties.Add("u'");
            properties.Add("v'");
            properties.Add("CCT(K)");
            properties.Add("Dominant Wavelength(nm)");
            properties.Add("Saturation(%)");
            properties.Add("Peak Wavelength(nm)");
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

            int index = 1;

            foreach (var result in viewResults)
            {
                csvBuilder.Append(result.CreateTime + ",");
                csvBuilder.Append(index + ",");
                index++;
                csvBuilder.Append(result.V + ",");
                csvBuilder.Append(result.I + ",");
                csvBuilder.Append(result.Lv + ",");
                csvBuilder.Append(result.IP + ",");
                csvBuilder.Append(result.Eqe + ",");
                csvBuilder.Append(result.LuminousFlux + ",");
                csvBuilder.Append(result.RadiantFlux + ",");
                csvBuilder.Append(result.LuminousEfficacy + ",");
                csvBuilder.Append(result.fx + ",");
                csvBuilder.Append(result.fy + ",");
                csvBuilder.Append(result.fu + ",");
                csvBuilder.Append(result.fv + ",");
                csvBuilder.Append(result.fCCT + ",");
                csvBuilder.Append(result.fLd + ",");
                csvBuilder.Append(result.fPur + ",");
                csvBuilder.Append(result.fLp + ",");
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

    /// <summary>
    /// Configuration for EqeProcess
    /// </summary>
    public class EqeProcessConfig : ViewModelBase
    {
        [DisplayName("保存CSV")]
        [Description("是否将EQE数据保存到CSV文件")]
        public bool SaveToCsv { get => _SaveToCsv; set { _SaveToCsv = value; OnPropertyChanged(); } }
        private bool _SaveToCsv = true;

        [DisplayName("显示窗口")]
        [Description("是否将EQE结果注入光谱视图")]
        public bool ShowWindow { get => _ShowWindow; set { _ShowWindow = value; OnPropertyChanged(); } }
        private bool _ShowWindow = true;
    }

    [BatchProcess("EQE处理", "处理批次EQE数据并显示结果")]
    public class EqeProcess : BatchProcessBase<EqeProcessConfig>
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(EqeProcess));

        public override bool Process(IBatchContext ctx)
        {
            if (ctx?.Batch == null) return false;
            if (ctx?.Batch.FlowStatus != FlowStatus.Completed) return false;

            var batchConfig = ctx.Config;

            string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            try
            {
                var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

                // Query EQE results for this batch
                var eqeResults = DB.Queryable<SpectumResultEntity>()
                    .Where(x => x.BatchId == ctx.Batch.Id && x.DataType == true)
                    .ToList();

                DB.Dispose();

                ObservableCollection<ViewResultSpectrum> viewResults = new ObservableCollection<ViewResultSpectrum>();

                if (eqeResults.Count == 0)
                {
                    log.Info("找不到EQE数据");
                    if (Config.SaveToCsv)
                    {
                        if (!Directory.Exists(batchConfig.SavePath))
                        {
                            Directory.CreateDirectory(batchConfig.SavePath);
                        }
                        string eqeFilePath = Path.Combine(batchConfig.SavePath, $"EQE_{timeStr}.csv");
                        viewResults.SaveToCsv(eqeFilePath);
                    }
                }
                else
                {
                    foreach (var item in eqeResults)
                    {
                        viewResults.Add(new ViewResultSpectrum(item));
                    }

                    if (Config.SaveToCsv)
                    {
                        if (!Directory.Exists(batchConfig.SavePath))
                        {
                            Directory.CreateDirectory(batchConfig.SavePath);
                        }
                        string eqeFilePath = Path.Combine(batchConfig.SavePath, $"EQE_{timeStr}.csv");
                        viewResults.SaveToCsv(eqeFilePath);
                    }
                }

                // Inject EQE results into existing spectrum view instead of opening a dedicated EQE window.
                if (Config.ShowWindow && viewResults.Count > 0)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var deviceSpectrum = ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>().FirstOrDefault();
                            if (deviceSpectrum == null)
                            {
                                log.Info("找不到光谱设备实例，跳过EQE视图注入");
                                return;
                            }

                            deviceSpectrum.DisplayConfig.IsLuminousFluxMode = true;
                            foreach (var result in viewResults)
                            {
                                deviceSpectrum.View.AddViewResultSpectrum(result);
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error("Failed to inject EQE results into spectrum view", ex);
                        }
                    });
                }

                // Reset SMU device configuration if available
                var DeviceSMUs = ServiceManager.GetInstance().DeviceServices.OfType<DeviceSMU>().ToList();
                if (DeviceSMUs.Count > 0)
                {
                    DeviceSMUs[0].DisplayConfig.V = null;
                    DeviceSMUs[0].DisplayConfig.I = null;
                }

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
