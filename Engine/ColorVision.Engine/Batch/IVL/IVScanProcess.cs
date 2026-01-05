using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Views;
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
using System.Windows.Xps;

namespace ColorVision.Engine.Batch.IVL
{
    /// <summary>
    /// Configuration for IVScanProcess
    /// </summary>
    public class IVScanProcessConfig : ViewModelBase
    {
        [DisplayName("保存CSV")]
        [Description("是否将IV扫描数据保存到CSV文件")]
        public bool SaveToCsv { get => _SaveToCsv; set { _SaveToCsv = value; OnPropertyChanged(); } }
        private bool _SaveToCsv = true;

        [DisplayName("显示图表")]
        [Description("是否显示IV曲线图表窗口")]
        public bool ShowPlot { get => _ShowPlot; set { _ShowPlot = value; OnPropertyChanged(); } }
        private bool _ShowPlot = true;


        public int ZIndex { get => _ZIndex; set { _ZIndex = value; OnPropertyChanged(); } }
        private int _ZIndex = -1;
    }

    /// <summary>
    /// IV Scan batch process - standalone IV scanning that saves data to CSV and displays IV chart.
    /// </summary>
    [BatchProcess("IVScanProcessing", "ProcessIVScanDataAndExportToCSV")]
    public class IVScanProcess : BatchProcessBase<IVScanProcessConfig>
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(IVScanProcess));

        public override bool Process(IBatchContext ctx)
        {
            if (ctx?.Batch == null) return false;
            if (ctx?.Batch.FlowStatus != FlowStatus.Completed) return false;

            var batchConfig = ctx.Config;

            try
            {
                List<SMUResultModel> smuScanResults;
                using (var db = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = MySqlControl.GetConnectionString(),
                    DbType = SqlSugar.DbType.MySql,
                    IsAutoCloseConnection = true
                }))
                {
                    // Query SMU scan data by batch ID
                    smuScanResults = db.Queryable<SMUResultModel>()
                        .Where(x => x.BatchId == ctx.Batch.Id && x.ZIndex == Config.ZIndex)
                        .OrderBy(x => x.CreateDate)
                        .ToList();
                }

                if (smuScanResults.Count == 0)
                {
                    log.Info("No SMU scan data found for batch");
                    return true;
                }

                // Build collection of ViewResultSMU for processing and display
                ObservableCollection<ViewResultSMU> viewResults = new ObservableCollection<ViewResultSMU>();
                foreach (var scanModel in smuScanResults)
                {
                    ViewResultSMU viewResult = new ViewResultSMU(scanModel);
                    viewResults.Add(viewResult);
                }

                // Save to CSV if enabled
                if (Config.SaveToCsv)
                {
                    string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    
                    // Ensure save directory exists
                    if (!Directory.Exists(batchConfig.SavePath))
                    {
                        Directory.CreateDirectory(batchConfig.SavePath);
                    }

                    string csvFilePath = Path.Combine(batchConfig.SavePath, $"IV_Scan_{timeStr}.csv");
                    SaveIVScanToCsv(viewResults,ctx.FlowName , csvFilePath);
                    log.Info($"IV Scan data saved to: {csvFilePath}");
                }

                // Show IV curve plot window if enabled
                if (Config.ShowPlot && viewResults.Count > 0)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var plotWindow = new IVPlotWindow(viewResults);
                            plotWindow.Show();
                        }
                        catch (Exception ex)
                        {
                            log.Error("Failed to open IV plot window", ex);
                        }
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                log.Error("IV Scan processing failed", ex);
                return false;
            }
        }

        private void SaveIVScanToCsv(ObservableCollection<ViewResultSMU> viewResults, string Recipe,string csvFilePath)
        {
            var csvBuilder = new StringBuilder();

            // Write header
            csvBuilder.AppendLine("Time,Index,Voltage(V),Current(A),ScanId,SourceMeterType,Recipe,Channel,SrcBegin,SrcEnd,Temperature");

            foreach (var result in viewResults)
            {
                string measurementTypeStr = result.MeasurementType == MeasurementType.Voltage ? "Voltage" : "Current";
                string timeStr = result.CreateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";

                for (int i = 0; i < result.SMUDatas.Count; i++)
                {
                    csvBuilder.AppendLine($"{timeStr},{i + 1},{result.SMUDatas[i].Voltage},{result.SMUDatas[i].Current},{result.Id},{measurementTypeStr},{Recipe},{result.ChannelType},{result.SMUDatas[i]}{result.LimitStart},{result.LimitEnd}");
                }
            }

            File.WriteAllText(csvFilePath, csvBuilder.ToString(), Encoding.UTF8);
        }
    }
}
