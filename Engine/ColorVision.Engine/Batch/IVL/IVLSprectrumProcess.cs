using ColorVision.Database;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using log4net;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.Engine.Batch.IVL
{
    [BatchProcess("IVL光谱处理", "仅处理IVL批次中的Spectrum数据并导出")]
    public class IVLSprectrumProcess : IBatchProcess
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(IVLProcess));

        public bool Process(IBatchContext ctx)
        {
            if (ctx?.Batch == null) return false;
            var config = ctx.Config;

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

                foreach (var item in DB.Queryable<SMUResultModel>().Where(x => x.Batchid == ctx.Batch.Id).ToList())
                {
                    testResult.SMUResultModels.Add(item);
                }
                var list = DB.Queryable<SpectumResultModel>().Where(x => x.BatchId == ctx.Batch.Id).ToList();

                DB.Dispose();
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
                        i++;
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
