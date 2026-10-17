using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ColorVision.FlowPoi.Client;

namespace ColorVision.FlowPoi.Demo
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            bool interactive = args.Length == 0;
            int exitCode;
            using (var cancellation = new CancellationTokenSource())
            {
                ConsoleCancelEventHandler cancel = (sender, e) => { e.Cancel = true; cancellation.Cancel(); };
                Console.CancelKeyPress += cancel;
                try
                {
                    Options options = interactive ? Prompt() : Parse(args);
                    if (options == null) return 0;
                    Console.WriteLine("连接 {0}:{1}，执行流程 {2}，等待完整结果（最多 {3} 秒）……",
                        options.Host, options.Port, options.FlowName ?? ("ID=" + options.FlowId), options.TimeoutSeconds);

                    var client = new FlowPoiClient(options.Host, options.Port);
                    TimeSpan timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                    FlowPoiResponse response = options.FlowId.HasValue
                        ? await client.RunByIdAsync(options.FlowId.Value, options.SerialNumber, timeout, cancellation.Token)
                        : await client.RunByNameAsync(options.FlowName, options.SerialNumber, timeout, cancellation.Token);
                    Console.WriteLine("Code={0}，{1}", response.Code, response.Msg);
                    if (response.Data != null)
                    {
                        Console.WriteLine("流程：{0}；批次：{1}；执行编号：{2}；状态：{3}；耗时：{4} ms",
                            response.Data.FlowName, response.Data.BatchId, response.Data.ExecutionSerialNumber,
                            response.Data.Status, response.Data.TotalTime);
                        foreach (FlowPoiPoint point in response.Data.Poi)
                        {
                            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                "{0} / {1}: X={2:G9}, Y={3:G9}, Z={4:G9}, x={5:G9}, y={6:G9}, u={7:G9}, v={8:G9}, CCT={9:G9}, Wave={10:G9}",
                                point.AlgorithmName, point.Name, point.X, point.Y, point.Z,
                                point.ChromaticityX, point.ChromaticityY, point.U, point.V, point.CCT, point.Wave));
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(options.Output))
                    {
                        string outputPath = Path.GetFullPath(options.Output);
                        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                        File.WriteAllText(outputPath, response.ResponseJson, new UTF8Encoding(false));
                        Console.WriteLine("完整响应已保存：" + outputPath);
                    }
                    exitCode = response.IsSuccess ? 0 : 2;
                }
                catch (OperationCanceledException)
                {
                    Console.Error.WriteLine("已取消客户端等待。主程序中的流程可能仍在执行，请在主程序中确认或停止。");
                    exitCode = 3;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    exitCode = 1;
                }
                finally
                {
                    Console.CancelKeyPress -= cancel;
                }
            }
            if (interactive && !Console.IsInputRedirected)
            {
                Console.WriteLine("按任意键退出。");
                Console.ReadKey(true);
            }
            return exitCode;
        }

        private static Options Prompt()
        {
            Console.WriteLine("ColorVision 流程 POI 调用示例（.NET Framework 4.8 / x64）");
            Console.WriteLine("请先在主程序中启用 Socket JSON 服务、连接 MySQL，并确认待执行流程。");
            var options = new Options();
            options.Host = Read("主程序 IP", "127.0.0.1");
            options.Port = int.Parse(Read("Socket 端口", "6666"), CultureInfo.InvariantCulture);
            string mode = Read("按名称调用输入 name，按模板 ID 调用输入 id", "name");
            if (string.Equals(mode, "id", StringComparison.OrdinalIgnoreCase))
                options.FlowId = int.Parse(Read("流程模板 ID（不是列表位置）", null), CultureInfo.InvariantCulture);
            else if (string.Equals(mode, "name", StringComparison.OrdinalIgnoreCase))
                options.FlowName = Read("流程名称", null);
            else
                throw new ArgumentException("调用方式必须是 name 或 id。");
            options.TimeoutSeconds = int.Parse(Read("等待超时秒数", "600"), CultureInfo.InvariantCulture);
            options.SerialNumber = Read("请求关联编号（可留空）", string.Empty);
            options.Output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results",
                "flow-poi-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".json");
            return options;
        }

        private static string Read(string label, string defaultValue)
        {
            Console.Write(label + (string.IsNullOrEmpty(defaultValue) ? "" : " [" + defaultValue + "]") + ": ");
            string input = Console.ReadLine();
            if (input == null) throw new EndOfStreamException("控制台输入已结束。");
            return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
        }

        private static Options Parse(string[] args)
        {
            var options = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--help" || args[i] == "-h")
                {
                    Console.WriteLine("FlowPoiClientDemo.exe --host IP --port 6666 --name \"流程名称\" [--timeout 600] [--sn 编号] [--output result.json]");
                    Console.WriteLine("或将 --name 替换为 --id 模板ID。无参数启动进入交互模式。");
                    return null;
                }
                if (i + 1 >= args.Length) throw new ArgumentException("参数缺少值：" + args[i]);
                string key = args[i];
                string value = args[++i];
                switch (key)
                {
                    case "--host": options.Host = value; break;
                    case "--port": options.Port = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "--name": options.FlowName = value; break;
                    case "--id": options.FlowId = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "--timeout": options.TimeoutSeconds = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "--sn": options.SerialNumber = value; break;
                    case "--output": options.Output = value; break;
                    default: throw new ArgumentException("未知参数：" + key);
                }
            }
            bool hasName = !string.IsNullOrWhiteSpace(options.FlowName);
            if (hasName == options.FlowId.HasValue)
                throw new ArgumentException("--name 与 --id 必须且只能填写一项。使用 --help 查看示例。");
            return options;
        }

        private sealed class Options
        {
            public string Host = "127.0.0.1";
            public int Port = 6666;
            public string FlowName;
            public int? FlowId;
            public int TimeoutSeconds = 600;
            public string SerialNumber;
            public string Output;
        }
    }
}
