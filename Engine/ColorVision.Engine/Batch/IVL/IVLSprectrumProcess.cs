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
                var list = MySqlControl.GetInstance().DB.Queryable<SpectumResultModel>().Where(x => x.BatchId == ctx.Batch.Id).ToList();
                ObservableCollection<ViewResultSpectrum> ViewResults = new ObservableCollection<ViewResultSpectrum>();
                if (list.Count == 0)
                {
                    log.Info("找不到光谱仪的数据");
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
