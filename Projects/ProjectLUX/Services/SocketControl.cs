using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.SocketProtocol;
using Dm.util;
using log4net;
using ProjectLUX.PluginConfig;
using ProjectLUX.Process.VID;
using SqlSugar;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace ProjectLUX.Services
{
    public class SocketControl
    {
        public static SocketControl Current { get; set; } = new SocketControl();
        public NetworkStream Stream { get; set; }
    }

    public class TestSocket:ISocketTextDispatcher
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TestSocket));

        public string Handle(NetworkStream stream, string request)
        {
            SocketControl.Current.Stream = stream;
            var list = request.split(",");

            if (list.Length == 2)
            {
                string code = list[0];
                string sn = list[1];
                sn = sn.TrimEnd(';');

                ProjectLUXConfig.Instance.SN = sn;

                if (code.startsWith("T00"))
                {
                    // 取 code 的最后两位（如果 code 长度足够）
                    string lastTwo = code.Length >= 2 ? code.Substring(code.Length - 2, 2) : code;
                    // 拼接到 H030 上
                    string h030x = SummaryManager.GetInstance().Summary.MachineNO + lastTwo;

                    List<string> strings = new List<string>();
                    strings.Add(h030x);
                    strings.Add(sn);
                    strings.Add("00");

                    ProjectLUXConfig.Instance.SN = sn;

                    if (lastTwo == "31")
                    {
                        log.Info("光谱测量执行");

                        if (!Directory.Exists(ProjectLUXConfig.Instance.ResultSavePath))
                            Directory.CreateDirectory(ProjectLUXConfig.Instance.ResultSavePath);

                        string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"D_{sn}.csv");
                        DeviceSpectrum deviceSprectrm = ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>().FirstOrDefault();
                        MsgRecord msgRecord = deviceSprectrm.DService.GetData();

                        //MsgRecord msgRecord = deviceSprectrm.DService.GetPosition();
                        msgRecord.MsgRecordStateChanged += (s, e) =>
                        {
                            log.Info("msgRecord");

                            if (e == MsgRecordState.Success)
                            { 
                                var msg =  msgRecord.MsgReturn;
                                ViewResultSpectrum viewResultSpectrum = null;
                                if (msg != null && msg.Data != null && msg?.Data?.MasterId != null && msg?.Data?.MasterId > 0)
                                {
                                    int masterId = msg.Data?.MasterId;

                                    var DB = new SqlSugarClient(new ConnectionConfig
                                    {
                                        ConnectionString = MySqlControl.GetConnectionString(),
                                        DbType = SqlSugar.DbType.MySql,
                                        IsAutoCloseConnection = true
                                    });
                                    SpectumResultEntity model = DB.Queryable<SpectumResultEntity>().Where(x => x.Id == masterId).First();
                                    DB.Dispose();
                                    log.Info($"GetData MasterId:{masterId} ");
                                    if (model != null)
                                    {
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            try
                                            {
                                                viewResultSpectrum = new ViewResultSpectrum(model);
                                            }
                                            catch (Exception ex)
                                            {
                                                log.Error(ex);
                                            }
                                        });
                                    }
                                }

                                if (viewResultSpectrum != null)
                                {
                                    SpectrumCsvExportHelper.ExportLuminousFluxMode(path, new[] { viewResultSpectrum });
                                }
                                stream.Write(Encoding.UTF8.GetBytes(string.Join(",", strings) + $";{viewResultSpectrum?.LuminousFlux}"));
                            }
                            else
                            {
                                stream.Write(Encoding.UTF8.GetBytes(string.Join(",", strings) + ";0"));
                            }
                        };
                        return null;
                    }

                    if (lastTwo == "01")
                    {
                        log.Info("VID虚像距执行");
                        string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"B_{sn}.csv");
                        var rows = new List<string> { "Test_Screen,Test_item,Test_Value,unit,lower_limit,upper_limit,Test_Result" };
                        VIDTestResult vIDTestResult = new VIDTestResult();
                        DeviceCamera deviceCamera = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().FirstOrDefault();
                        DisplayCameraConfig displayCameraConfig = DisplayConfigManager.Instance.GetDisplayConfig<DisplayCameraConfig>(deviceCamera.Config.Code);

                        //这俩值还不一样，看看后面怎么优化一下
                        MsgRecord msgRecord = deviceCamera.DService.AutoFocus(TemplateAutoFocus.Params[displayCameraConfig.AutoFocusTemplateIndex].Value);

                        //MsgRecord msgRecord = deviceSprectrm.DService.GetPosition();
                        msgRecord.MsgRecordStateChanged += (s, e) =>
                        {
                            log.Info("msgRecord");

                            if (e == MsgRecordState.Success)
                            {
                                if (msgRecord.MsgReturn.EventName == "GetPosition")
                                {
                                    vIDTestResult.VID.Value = msgRecord.MsgReturn.Data.VidPos;
                                }
                                if (msgRecord.MsgReturn.EventName == "AutoFocus")
                                {
                                    vIDTestResult.VID.Value = msgRecord.MsgReturn.Data.VidPosition;
                                }
                                VIDFixConfig fixConfig = FixManager.GetInstance().FixConfig.GetRequiredService<VIDFixConfig>();
                                VIDRecipeConfig recipeConfig = RecipeManager.GetInstance().RecipeConfig.GetRequiredService<VIDRecipeConfig>();

                                vIDTestResult.VID.Value = vIDTestResult.VID.Value * fixConfig.VID;
                                vIDTestResult.VID.TestValue = vIDTestResult.VID.Value.ToString();
                                vIDTestResult.VID.LowLimit = recipeConfig.VID.Min;
                                vIDTestResult.VID.UpLimit = recipeConfig.VID.Max;
                                ObjectiveTestResultCsvExporter.CollectRows(vIDTestResult, "2pixel_linePair", rows);
                                File.WriteAllLines(path, rows);
                                stream.Write(Encoding.UTF8.GetBytes(string.Join(",", strings) + $";{vIDTestResult.VID.Value}"));
                            }
                            else
                            {
                                VIDFixConfig fixConfig = FixManager.GetInstance().FixConfig.GetRequiredService<VIDFixConfig>();
                                VIDRecipeConfig recipeConfig = RecipeManager.GetInstance().RecipeConfig.GetRequiredService<VIDRecipeConfig>();

                                vIDTestResult.VID.Value = vIDTestResult.VID.Value * fixConfig.VID;
                                vIDTestResult.VID.TestValue = vIDTestResult.VID.Value.ToString();
                                vIDTestResult.VID.LowLimit = recipeConfig.VID.Min;
                                vIDTestResult.VID.UpLimit = recipeConfig.VID.Max;
                                ObjectiveTestResultCsvExporter.CollectRows(vIDTestResult, "2pixel_linePair", rows);
                                File.WriteAllLines(path, rows);
                                stream.Write(Encoding.UTF8.GetBytes(string.Join(",", strings) + ";0"));
                            }
                        };
                        return null;
                    }
                    
                    
                    if (SummaryManager.GetInstance().Summary.MachineNO == "H03AR")
                    {
                        if (ProjectWindowInstance.WindowInstance == null) return string.Join(",", strings) + ";";

                        ProjectWindowInstance.WindowInstance.ReturnCode = string.Join(",", strings) + ";";
                        if (lastTwo == "")
                        {
                            log.Info("拍图窗口握手");
                            ProjectWindowInstance.WindowInstance.InitTest(sn);
                        }
                        else if (lastTwo == "02")
                        {
                            log.Info("oc测试 ");
                            strings.RemoveAt(2);
                            ProjectWindowInstance.WindowInstance.ReturnCode = string.Join(",", strings);
                            ProjectWindowInstance.WindowInstance.RunTemplateBySocketCode(lastTwo);
                            return null;
                        }
                        else
                        {
                            ProjectWindowInstance.WindowInstance.RunTemplateBySocketCode(lastTwo);
                            return null;
                        }
                    }
                    else
                    {
                        if (ProjectWindowInstance.WindowInstance == null) return string.Join(",", strings) + ";";

                        ProjectWindowInstance.WindowInstance.ReturnCode = string.Join(",", strings) + ";";
                        if (lastTwo == "00")
                        {

                            log.Info("拍图窗口握手");
                            ProjectWindowInstance.WindowInstance.InitTest(sn);
                        }
                        else
                        {
                            ProjectWindowInstance.WindowInstance.RunTemplateBySocketCode(lastTwo);
                            return null;
                        }
                    }
                    return string.Join(",", strings) + ";";
                }
                else
                {
                    return $"No right Code {code}";
                }
            }
            else
            {
                return "No SN";
            }
        }
    }
}
