using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ColorVision.FlowPoi.Client;

namespace ColorVision.FlowPoi.Demo
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 0 || (args.Length == 1 && args[0] == "--ui"))
            {
                var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                return application.Run(new MainWindow());
            }
            return RunConsoleAsync(args).GetAwaiter().GetResult();
        }

        private static async Task<int> RunConsoleAsync(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            int exitCode;
            using (var cancellation = new CancellationTokenSource())
            {
                ConsoleCancelEventHandler cancel = (sender, e) => { e.Cancel = true; cancellation.Cancel(); };
                Console.CancelKeyPress += cancel;
                try
                {
                    Options options = Parse(args);
                    if (options == null) return 0;
                    FlowPoiResponse response;
                    if (!string.IsNullOrWhiteSpace(options.ParseFile))
                    {
                        response = FlowPoiResponse.Parse(File.ReadAllText(options.ParseFile, Encoding.UTF8));
                        Console.WriteLine("离线解析：" + Path.GetFullPath(options.ParseFile));
                    }
                    else
                    {
                        Console.WriteLine("连接 {0}:{1}，执行流程 {2}，等待完整结果（最多 {3} 秒）……",
                            options.Host, options.Port, options.FlowName ?? ("ID=" + options.FlowId), options.TimeoutSeconds);
                        var client = new FlowPoiClient(options.Host, options.Port);
                        TimeSpan timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                        response = options.FlowId.HasValue
                            ? await client.RunByIdAsync(options.FlowId.Value, options.SerialNumber, timeout, cancellation.Token)
                            : await client.RunByNameAsync(options.FlowName, options.SerialNumber, timeout, cancellation.Token);
                    }
                    Console.WriteLine("Code={0}，{1}", response.Code, response.Msg);
                    if (response.Data != null)
                    {
                        Console.WriteLine("流程：{0}；批次：{1}；执行编号：{2}；状态：{3}；耗时：{4} ms",
                            response.Data.FlowName, response.Data.BatchId, response.Data.ExecutionSerialNumber,
                            response.Data.Status, response.Data.TotalTime);
                        foreach (FlowPoiPoint point in response.Data.Poi ?? new System.Collections.Generic.List<FlowPoiPoint>())
                        {
                            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                "{0} / {1}: X={2:G9}, Y={3:G9}, Z={4:G9}, x={5:G9}, y={6:G9}, u={7:G9}, v={8:G9}, CCT={9:G9}, Wave={10:G9}",
                                point.AlgorithmName, point.Name, point.X, point.Y, point.Z,
                                point.ChromaticityX, point.ChromaticityY, point.U, point.V, point.CCT, point.Wave));
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(options.Output))
                    {
                        DemoResultFiles.SaveJson(response, options.Output);
                        Console.WriteLine("完整响应已保存：" + Path.GetFullPath(options.Output));
                    }
                    if (!string.IsNullOrWhiteSpace(options.CsvOutput))
                    {
                        DemoResultFiles.SaveCsv(response, options.CsvOutput);
                        Console.WriteLine("POI CSV 已保存：" + Path.GetFullPath(options.CsvOutput));
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
            return exitCode;
        }

        private static Options Parse(string[] args)
        {
            var options = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--help" || args[i] == "-h")
                {
                    Console.WriteLine("FlowPoiClientDemo.exe --host IP --port 6666 --name \"流程名称\" [--timeout 600] [--sn 编号] [--output result.json]");
                    Console.WriteLine("或将 --name 替换为 --id 模板ID。可用 --csv result.csv 导出点位表。");
                    Console.WriteLine("离线解析：FlowPoiClientDemo.exe --parse response.json [--output result.json] [--csv result.csv]");
                    Console.WriteLine("无参数或 --ui 启动窗口。运行环境：.NET Framework 4.8 / x64。");
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
                    case "--csv": options.CsvOutput = value; break;
                    case "--parse": options.ParseFile = value; break;
                    default: throw new ArgumentException("未知参数：" + key);
                }
            }
            bool hasName = !string.IsNullOrWhiteSpace(options.FlowName);
            if (!string.IsNullOrWhiteSpace(options.ParseFile))
            {
                if (hasName || options.FlowId.HasValue)
                    throw new ArgumentException("--parse 不能与 --name 或 --id 同时使用。");
            }
            else if (hasName == options.FlowId.HasValue)
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
            public string CsvOutput;
            public string ParseFile;
        }
    }
}
