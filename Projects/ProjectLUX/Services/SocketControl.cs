using ColorVision.Engine.Templates.Flow;
using ColorVision.SocketProtocol;
using Dm.util;
using log4net;
using ProjectLUX.PluginConfig;
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
            var list = request.split(",");

            if (list.Length == 2)
            {
                string code = list[0];
                string sn = list[1];
                sn = sn.TrimEnd(';');

                if (code.startsWith("T00"))
                {
                    // 取 code 的最后两位（如果 code 长度足够）
                    string lastTwo = code.Length >= 2 ? code.Substring(code.Length - 2, 2) : code;

                    if (!Directory.Exists(ProjectLUXConfig.Instance.ResultSavePath))
                    {
                        Directory.CreateDirectory(ProjectLUXConfig.Instance.ResultSavePath);
                    }

                    if (SummaryManager.GetInstance().Summary.MachineNO == "H02")
                    {
                        if (lastTwo == "00")
                        {
                            log.Info("VID虚像距握手");
                        }
                        else if (lastTwo == "01")
                        {
                            log.Info("VID虚像距执行");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"{sn}.csv");
                            File.WriteAllText(path, request);
                        }
                    }
                    else
                    {

                        if (lastTwo == "00")
                        {
                            log.Info("拍图窗口握手");
                        }
                        else if (lastTwo == "02")
                        {
                            log.Info("测试图例1 White Fov ");
                            ProjectWindowInstance.WindowInstance.RunTemplate(0, "White255_FOV_VR");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"{sn}.csv");
                            File.AppendAllText(path, request);
                        }
                        else if (lastTwo == "03")
                        {
                            log.Info("测试图例2 White Corrdinate");
                            ProjectWindowInstance.WindowInstance.RunTemplate(1, "White255_Test");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"{sn}.csv");
                            File.AppendAllText(path, request);
                        }
                        else if (lastTwo == "04")
                        {
                            log.Info("测试图例3 Red");
                            ProjectWindowInstance.WindowInstance.RunTemplate(2, "Red255_VR_Test");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"{sn}.csv");
                            File.AppendAllText(path, request);
                        }
                        else if (lastTwo == "05")
                        {
                            log.Info("测试图例4 Green");
                            ProjectWindowInstance.WindowInstance.RunTemplate(3, "Green255_VR_Test");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"{sn}.csv");
                            File.AppendAllText(path, request);
                        }
                        else if (lastTwo == "06")
                        {
                            log.Info("测试图例5 Blue");
                            ProjectWindowInstance.WindowInstance.RunTemplate(4, "Blue255_VR_Test");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"{sn}.csv");
                            File.AppendAllText(path, request);
                        }
                        else if (lastTwo == "07")
                        {
                            log.Info("测试图例6 Chessboard");
                            ProjectWindowInstance.WindowInstance.RunTemplate(5, "Chessboard7*7_VR_Test");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"{sn}.csv");
                            File.AppendAllText(path, request);
                        }
                        else if (lastTwo == "08")
                        {
                            log.Info("测试图例7 MTF-4pixel-o.6f");
                            ProjectWindowInstance.WindowInstance.RunTemplate(6, "MTF_HV_VR_Test");

                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"{sn}.csv");
                            File.AppendAllText(path, request);
                        }
                        else if (lastTwo == "09")
                        {
                            log.Info("测试图例8 Distortion");
                            ProjectWindowInstance.WindowInstance.RunTemplate(7, "Distortion_VR_Test");
                            string path = Path.Combine(ProjectLUXConfig.Instance.ResultSavePath, $"{sn}.csv");
                            File.AppendAllText(path, request);
                        }
                    }
                        // 拼接到 H030 上
                    string h030x = SummaryManager.GetInstance().Summary.MachineNO + lastTwo;

                    List<string> strings = new List<string>();
                    strings.Add(h030x);
                    strings.Add(sn);
                    strings.Add("00");

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
