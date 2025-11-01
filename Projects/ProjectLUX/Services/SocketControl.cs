using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Templates.Flow;
using ColorVision.SocketProtocol;
using Dm.util;
using log4net;
using ProjectLUX.PluginConfig;
using ProjectLUX.Process.VID;
using SQLitePCL;
using SqlSugar;
using System.IO;
using System.Net.Sockets;
using System.Windows;

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

                    if (SummaryManager.GetInstance().Summary.MachineNO == "H02")
                    {
                        if (lastTwo == "00")
                        {
                            log.Info("VID虚像距握手");
                            return string.Join(",", strings) + ";";
                        }
                        else if (lastTwo == "01")
                        {
                            log.Info("VID虚像距执行");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"B_{sn}.csv");
                            var rows = new List<string> { "Test_Screen,Test_item,Test_Value,unit,lower_limit,upper_limit,Test_Result" };
                            VIDTestResult vIDTestResult = new VIDTestResult();
                            DeviceCamera deviceCamera = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().FirstOrDefault();
                            MsgRecord msgRecord = deviceCamera.DService.GetPosition();
                            msgRecord.MsgRecordStateChanged += (e) =>
                            {
                                if (e == MsgRecordState.Success)
                                {
                                    if (msgRecord.MsgReturn.EventName == "GetPosition")
                                    {
                                        vIDTestResult.VID.Value = msgRecord.MsgReturn.Data.VidPos;
                                    }

                                    VIDFixConfig fixConfig = FixManager.GetInstance().FixConfig.GetRequiredService<VIDFixConfig>();
                                    VIDRecipeConfig recipeConfig = RecipeManager.GetInstance().RecipeConfig.GetRequiredService<VIDRecipeConfig>();

                                    vIDTestResult.VID.Value = vIDTestResult.VID.Value * fixConfig.VID;
                                    vIDTestResult.VID.TestValue = vIDTestResult.VID.Value.ToString();
                                    vIDTestResult.VID.LowLimit = recipeConfig.VID.Min;
                                    vIDTestResult.VID.UpLimit = recipeConfig.VID.Max;
                                    ObjectiveTestResultCsvExporter.CollectRows(vIDTestResult, "2pixel_linePair", rows);
                                    File.WriteAllLines(path, rows);
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
                                }
                            };

                            return string.Join(",", strings) + ";";
                        }
                    }
                    else if (SummaryManager.GetInstance().Summary.MachineNO == "H03AR")
                    {
                        if (ProjectWindowInstance.WindowInstance == null) return string.Join(",", strings) + ";";

                        ProjectWindowInstance.WindowInstance.ReturnCode = string.Join(",", strings) + ";";
                        if (lastTwo == "00")
                        {
                            log.Info("拍图窗口握手");
                            ProjectWindowInstance.WindowInstance.InitTest(sn);
                        }
                        else if (lastTwo == "02")
                        {
                            log.Info("oc测试 ");
                            ProjectWindowInstance.WindowInstance.RunTemplate(0, "Optical_Center_Calibrate");
                            return null;
                        }
                        else if (lastTwo == "03")
                        {
                            log.Info("测试图例1 White 51 ");
                            ProjectWindowInstance.WindowInstance.RunTemplate(1, "White51_Test");
                            return null;
                        }
                        else if (lastTwo == "04")
                        {
                            log.Info("测试图例1 White Fov ");
                            ProjectWindowInstance.WindowInstance.RunTemplate(2, "White255_Test");
                            return null;
                        }
                        else if (lastTwo == "05")
                        {
                            log.Info("测试图例3 Chessboard");
                            ProjectWindowInstance.WindowInstance.RunTemplate(3, "Chessboard_ANSI_Test");
                            return null;
                        }
                        else if (lastTwo == "06")
                        {
                            log.Info("测试图例7 MTF-4pixel-o.6f");
                            ProjectWindowInstance.WindowInstance.RunTemplate(4, "MTF_HV_Test");
                            return null;
                        }
                        else if (lastTwo == "07")
                        {
                            log.Info("测试图例8 Distortion");
                            ProjectWindowInstance.WindowInstance.RunTemplate(5, "Distortion_Test");
                            return null;
                        }
                        else if (lastTwo == "08")
                        {
                            log.Info("测试图例8 Optic");
                            ProjectWindowInstance.WindowInstance.RunTemplate(6, "OpticCenter_Test");
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
                        }
                        else if (lastTwo == "02")
                        {
                            log.Info("测试图例1 White Fov ");
                            ProjectWindowInstance.WindowInstance.RunTemplate(0, "White255_FOV_VR");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"C_{sn}.csv");
                            ObjectiveTestResult TestResult = new ObjectiveTestResult();
                            TestResult.W255TestResult = new Process.W255.W255TestResult();
                            TestResult.RedTestResult = new Process.Red.RedTestResult();
                            TestResult.BlueTestResult = new Process.Blue.BlueTestResult();
                            TestResult.GreenTestResult = new Process.Green.GreenTestResult();
                            TestResult.MTFHVTestResult = new Process.MTFHV.MTFHVTestResult();
                            TestResult.DistortionTestResult = new Process.Distortion.DistortionTestResult();
                            TestResult.ChessboardTestResult = new Process.Chessboard.ChessboardTestResult();
                            TestResult.OpticCenterTestResult = new Process.OpticCenter.OpticCenterTestResult();

                            ObjectiveTestResultCsvExporter.ExportToCsv(TestResult, path);
                            return null;
                        }
                        else if (lastTwo == "03")
                        {
                            log.Info("测试图例2 White Corrdinate");
                            ProjectWindowInstance.WindowInstance.RunTemplate(1, "Black255_VR_Test");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"C_{sn}.csv");
                            ObjectiveTestResult TestResult = new ObjectiveTestResult();
                            TestResult.W255TestResult = new Process.W255.W255TestResult();
                            TestResult.RedTestResult = new Process.Red.RedTestResult();
                            TestResult.BlueTestResult = new Process.Blue.BlueTestResult();
                            TestResult.GreenTestResult = new Process.Green.GreenTestResult();
                            TestResult.MTFHVTestResult = new Process.MTFHV.MTFHVTestResult();
                            TestResult.DistortionTestResult = new Process.Distortion.DistortionTestResult();
                            TestResult.ChessboardTestResult = new Process.Chessboard.ChessboardTestResult();
                            TestResult.OpticCenterTestResult = new Process.OpticCenter.OpticCenterTestResult();

                            ObjectiveTestResultCsvExporter.ExportToCsv(TestResult, path); return null;
                        }
                        else if (lastTwo == "04")
                        {
                            log.Info("测试图例3 Red");
                            ProjectWindowInstance.WindowInstance.RunTemplate(2, "Red255_VR_Test");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"C_{sn}.csv");
                            ObjectiveTestResult TestResult = new ObjectiveTestResult();
                            TestResult.W255TestResult = new Process.W255.W255TestResult();
                            TestResult.RedTestResult = new Process.Red.RedTestResult();
                            TestResult.BlueTestResult = new Process.Blue.BlueTestResult();
                            TestResult.GreenTestResult = new Process.Green.GreenTestResult();
                            TestResult.MTFHVTestResult = new Process.MTFHV.MTFHVTestResult();
                            TestResult.DistortionTestResult = new Process.Distortion.DistortionTestResult();
                            TestResult.ChessboardTestResult = new Process.Chessboard.ChessboardTestResult();
                            TestResult.OpticCenterTestResult = new Process.OpticCenter.OpticCenterTestResult();

                            ObjectiveTestResultCsvExporter.ExportToCsv(TestResult, path); return null;
                        }
                        else if (lastTwo == "05")
                        {
                            log.Info("测试图例4 Green");
                            ProjectWindowInstance.WindowInstance.RunTemplate(3, "Green255_VR_Test");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"C_{sn}.csv");
                            ObjectiveTestResult TestResult = new ObjectiveTestResult();
                            TestResult.W255TestResult = new Process.W255.W255TestResult();
                            TestResult.RedTestResult = new Process.Red.RedTestResult();
                            TestResult.BlueTestResult = new Process.Blue.BlueTestResult();
                            TestResult.GreenTestResult = new Process.Green.GreenTestResult();
                            TestResult.MTFHVTestResult = new Process.MTFHV.MTFHVTestResult();
                            TestResult.DistortionTestResult = new Process.Distortion.DistortionTestResult();
                            TestResult.ChessboardTestResult = new Process.Chessboard.ChessboardTestResult();
                            TestResult.OpticCenterTestResult = new Process.OpticCenter.OpticCenterTestResult();

                            ObjectiveTestResultCsvExporter.ExportToCsv(TestResult, path); return null;
                        }
                        else if (lastTwo == "06")
                        {
                            log.Info("测试图例5 Blue");
                            ProjectWindowInstance.WindowInstance.RunTemplate(4, "Blue255_VR_Test");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"C_{sn}.csv");
                            ObjectiveTestResult TestResult = new ObjectiveTestResult();
                            TestResult.W255TestResult = new Process.W255.W255TestResult();
                            TestResult.RedTestResult = new Process.Red.RedTestResult();
                            TestResult.BlueTestResult = new Process.Blue.BlueTestResult();
                            TestResult.GreenTestResult = new Process.Green.GreenTestResult();
                            TestResult.MTFHVTestResult = new Process.MTFHV.MTFHVTestResult();
                            TestResult.DistortionTestResult = new Process.Distortion.DistortionTestResult();
                            TestResult.ChessboardTestResult = new Process.Chessboard.ChessboardTestResult();
                            TestResult.OpticCenterTestResult = new Process.OpticCenter.OpticCenterTestResult();

                            ObjectiveTestResultCsvExporter.ExportToCsv(TestResult, path); return null;
                        }
                        else if (lastTwo == "07")
                        {
                            log.Info("测试图例6 Chessboard");
                            ProjectWindowInstance.WindowInstance.RunTemplate(5, "Chessboard7*7_VR_Test");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"C_{sn}.csv");
                            ObjectiveTestResult TestResult = new ObjectiveTestResult();
                            TestResult.W255TestResult = new Process.W255.W255TestResult();
                            TestResult.RedTestResult = new Process.Red.RedTestResult();
                            TestResult.BlueTestResult = new Process.Blue.BlueTestResult();
                            TestResult.GreenTestResult = new Process.Green.GreenTestResult();
                            TestResult.MTFHVTestResult = new Process.MTFHV.MTFHVTestResult();
                            TestResult.DistortionTestResult = new Process.Distortion.DistortionTestResult();
                            TestResult.ChessboardTestResult = new Process.Chessboard.ChessboardTestResult();
                            TestResult.OpticCenterTestResult = new Process.OpticCenter.OpticCenterTestResult();

                            ObjectiveTestResultCsvExporter.ExportToCsv(TestResult, path); return null;
                        }
                        else if (lastTwo == "08")
                        {
                            log.Info("测试图例7 MTF-4pixel-o.6f");
                            ProjectWindowInstance.WindowInstance.RunTemplate(6, "MTF_HV_VR_Test");

                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"C_{sn}.csv");
                            ObjectiveTestResult TestResult = new ObjectiveTestResult();
                            TestResult.W255TestResult = new Process.W255.W255TestResult();
                            TestResult.RedTestResult = new Process.Red.RedTestResult();
                            TestResult.BlueTestResult = new Process.Blue.BlueTestResult();
                            TestResult.GreenTestResult = new Process.Green.GreenTestResult();
                            TestResult.MTFHVTestResult = new Process.MTFHV.MTFHVTestResult();
                            TestResult.DistortionTestResult = new Process.Distortion.DistortionTestResult();
                            TestResult.ChessboardTestResult = new Process.Chessboard.ChessboardTestResult();
                            TestResult.OpticCenterTestResult = new Process.OpticCenter.OpticCenterTestResult();

                            ObjectiveTestResultCsvExporter.ExportToCsv(TestResult, path); return null;
                        }
                        else if (lastTwo == "09")
                        {
                            log.Info("测试图例8 Distortion");
                            ProjectWindowInstance.WindowInstance.RunTemplate(7, "Distortion_VR_Test");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"C_{sn}.csv");
                            ObjectiveTestResult TestResult = new ObjectiveTestResult();
                            TestResult.W255TestResult = new Process.W255.W255TestResult();
                            TestResult.RedTestResult = new Process.Red.RedTestResult();
                            TestResult.BlueTestResult = new Process.Blue.BlueTestResult();
                            TestResult.GreenTestResult = new Process.Green.GreenTestResult();
                            TestResult.MTFHVTestResult = new Process.MTFHV.MTFHVTestResult();
                            TestResult.DistortionTestResult = new Process.Distortion.DistortionTestResult();
                            TestResult.ChessboardTestResult = new Process.Chessboard.ChessboardTestResult();
                            TestResult.OpticCenterTestResult = new Process.OpticCenter.OpticCenterTestResult();
                            ObjectiveTestResultCsvExporter.ExportToCsv(TestResult, path); return null;
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
