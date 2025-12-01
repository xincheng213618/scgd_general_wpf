using ColorVision.Database;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Views;
using log4net;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Engine.Batch.IVL
{
    /// <summary>
    /// IV Scan batch process - standalone IV scanning that saves data to CSV and displays IV chart.
    /// </summary>
    [BatchProcess("IVScanProcessing", "ProcessIVScanDataAndExportToCSV")]
    public class IVScanProcess : IBatchProcess
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(IVScanProcess));

        public bool Process(IBatchContext ctx)
        {
            if (ctx?.Batch == null) return false;
            var config = ctx.Config;

            try
            {
                List<SmuScanModel> smuScanResults;
                using (var db = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = MySqlControl.GetConnectionString(),
                    DbType = SqlSugar.DbType.MySql,
                    IsAutoCloseConnection = true
                }))
                {
                    // Query SMU scan data by batch ID
                    smuScanResults = db.Queryable<SmuScanModel>()
                        .Where(x => x.BatchId == ctx.Batch.Id)
                        .OrderBy(x => x.CreateDate)
                        .ToList();
                }

                if (smuScanResults.Count == 0)
                {
                    log.Info("No SMU scan data found for batch");
                    return true;
                }

                string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                
                // Ensure save directory exists
                if (!Directory.Exists(config.SavePath))
                {
                    Directory.CreateDirectory(config.SavePath);
                }

                // Build collection of ViewResultSMU for processing and display
                ObservableCollection<ViewResultSMU> viewResults = new ObservableCollection<ViewResultSMU>();
                foreach (var scanModel in smuScanResults)
                {
                    ViewResultSMU viewResult = new ViewResultSMU(scanModel);
                    viewResults.Add(viewResult);
                }

                // Save to CSV
                string csvFilePath = Path.Combine(config.SavePath, $"IV_Scan_{timeStr}.csv");
                SaveIVScanToCsv(viewResults, csvFilePath);

                log.Info($"IV Scan data saved to: {csvFilePath}");

                // Show IV curve plot window
                if (viewResults.Count > 0)
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

        private void SaveIVScanToCsv(ObservableCollection<ViewResultSMU> viewResults, string csvFilePath)
        {
            var csvBuilder = new StringBuilder();

            // Write header
            csvBuilder.AppendLine("Time,ScanId,MeasurementType,SrcBegin,SrcEnd,Index,Voltage(V),Current(A)");

            foreach (var result in viewResults)
            {
                string measurementTypeStr = result.MeasurementType == MeasurementType.Voltage ? "Voltage" : "Current";
                string timeStr = result.CreateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";

                for (int i = 0; i < result.SMUDatas.Count; i++)
                {
                    csvBuilder.AppendLine($"{timeStr},{result.Id},{measurementTypeStr},{result.LimitStart},{result.LimitEnd},{i + 1},{result.SMUDatas[i].Voltage},{result.SMUDatas[i].Current}");
                }
            }

            File.WriteAllText(csvFilePath, csvBuilder.ToString(), Encoding.UTF8);
        }
    }
}
