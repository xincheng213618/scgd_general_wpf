using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Batch.IVL
{
    /// <summary>
    /// Configuration for IVLSprectrumProcess
    /// </summary>
    public class IVLSprectrumProcessConfig : ViewModelBase
    {
        [DisplayName("保存CSV")]
        [Description("是否将光谱数据保存到CSV文件")]
        public bool SaveToCsv { get => _SaveToCsv; set { _SaveToCsv = value; OnPropertyChanged(); } }
        private bool _SaveToCsv = true;

        [DisplayName("显示图表")]
        [Description("是否显示I-Lv曲线图表窗口")]
        public bool ShowPlot { get => _ShowPlot; set { _ShowPlot = value; OnPropertyChanged(); } }
        private bool _ShowPlot = true;
    }

    [BatchProcess("IvlSpectralProcessing", "ProcessAndExportSpectrumDataFromIvlBatchOnly")]
    public class IVLSprectrumProcess : BatchProcessBase<IVLSprectrumProcessConfig>
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(IVLProcess));

        public override bool Process(IBatchContext ctx)
        {
            if (ctx?.Batch == null) return false;
            if (ctx?.Batch.FlowStatus != FlowStatus.Completed) return false;
            var batchConfig = ctx.Config;

            string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            IVLViewTestResult testResult = new IVLViewTestResult();
            try
            {

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
                var list = DB.Queryable<SpectumResultEntity>().Where(x => x.BatchId == ctx.Batch.Id).ToList();

                DB.Dispose();
                ObservableCollection<ViewResultSpectrum> ViewResults = new ObservableCollection<ViewResultSpectrum>();
                if (list.Count == 0)
                {
                    log.Info("找不到光谱数据");
                    if (Config.SaveToCsv)
                    {
                        if (!Directory.Exists(batchConfig.SavePath))
                        {
                            Directory.CreateDirectory(batchConfig.SavePath);
                        }
                        string sprectrumfilePath = Path.Combine(batchConfig.SavePath, $"SP_IVL_{timeStr}.csv");
                        ViewResults.SaveToCsv(sprectrumfilePath);
                    }
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
                            viewResultSpectrum.V = SMUResultModel.VResult ?? 0;
                            viewResultSpectrum.I = SMUResultModel.IResult ?? 0;
                        }
                        else
                        {
                            viewResultSpectrum.V = float.NaN;
                            viewResultSpectrum.I = float.NaN;
                        }
                        i++;
                        ViewResults.Add(viewResultSpectrum);
                    }
                    
                    if (Config.SaveToCsv)
                    {
                        if (!Directory.Exists(batchConfig.SavePath))
                        {
                            Directory.CreateDirectory(batchConfig.SavePath);
                        }
                        string sprectrumfilePath = Path.Combine(batchConfig.SavePath, $"SP_IVL_{timeStr}.csv");
                        ViewResults.SaveToCsv(sprectrumfilePath);
                    }
                }
                
                // Show I-Lv curve plot window
                if (Config.ShowPlot && testResult.SMUResultModels.Count > 0 && ViewResults.Count > 0)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            // Create an empty list for POI data since this process only has spectrum data
                            var plotWindow = new ILvPlotWindow(testResult.SMUResultModels, new List<PoiResultCIExyuvData>(), ViewResults);
                            plotWindow.Show();
                        }
                        catch (Exception ex)
                        {
                            log.Error("Failed to open I-Lv plot window", ex);
                        }
                    });
                }

                var DeviceSMUs = ServiceManager.GetInstance().DeviceServices.OfType<DeviceSMU>().ToList();
                if (DeviceSMUs.Count > 0)
                {
                    DeviceSMUs[0].DisplayConfig.V = null;
                    DeviceSMUs[0].DisplayConfig.I = null;
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
