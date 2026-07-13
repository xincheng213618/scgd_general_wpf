#pragma warning disable CS8601, CS8602, CS8604
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Sensor;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ProjectARVRPro.Process.Demura
{
    public class DemuraProcess : ProcessBase<DemuraProcessConfig>
    {
        private const string ToolFolderName = "DemuraTool";
        private const string ToolExeName = "DemuraTool_x64.exe";
        private const string Prepared128FileName = "G128.csv";
        private const string Prepared255FileName = "G255.csv";
        private static readonly char[] CsvSeparators = new[] { ',', ';', '\t', ' ' };
        private static readonly HashSet<string> PreviewImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public override async Task<bool> Execute(IProcessExecutionContext ctx)
        {
            return await ExecuteCoreAsync(ctx).ConfigureAwait(false);
        }

        public override async Task<bool> ExecuteFailure(IProcessExecutionContext ctx)
        {
            string failureMessage = string.IsNullOrWhiteSpace(ctx?.Result?.Msg) ? "Demura流程失败" : ctx.Result.Msg;
            if (ctx?.Result != null)
            {
                ctx.Result.Result = false;
                ctx.Result.Msg = failureMessage;
            }

            if (ctx?.ObjectiveTestResult != null)
            {
                ctx.ObjectiveTestResult.TotalResult = false;
                ctx.ObjectiveTestResult.Msg = failureMessage;
            }

            var log = ctx?.Log;
            try
            {
                DeviceSensor? sensor = FindGeneralSensor();
                if (sensor == null)
                {
                    log?.Warn($"Demura失败处理下电失败：未找到通用传感器服务，Code={Config.GeneralSensorCode}, Category={Config.GeneralSensorCategory}");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(sensor.Config.Addr) || sensor.Config.Port <= 0)
                {
                    log?.Warn($"Demura失败处理下电失败：通用传感器PG连接配置无效：{sensor.Config.Addr}:{sensor.Config.Port}");
                    return false;
                }

                CommandExchange powerOff = await SendPowerOffCommandAsync(sensor.Config.Addr, sensor.Config.Port, log).ConfigureAwait(false);
                string powerOffMessage = powerOff.Success ? "Demura失败处理PG下电成功。" : $"Demura失败处理PG下电失败：{powerOff.Message}";
                log?.Info(powerOffMessage);
                if (ctx?.Result != null)
                    ctx.Result.Msg = string.IsNullOrWhiteSpace(ctx.Result.Msg) ? powerOffMessage : $"{ctx.Result.Msg}; {powerOffMessage}";

                return powerOff.Success;
            }
            catch (Exception ex)
            {
                log?.Error("Demura失败处理PG下电异常", ex);
                return false;
            }
        }

        private async Task<bool> ExecuteCoreAsync(IProcessExecutionContext ctx)
        {
            if (ctx?.Batch == null || ctx.Result == null || ctx.ObjectiveTestResult == null) return false;

            var log = ctx.Log;
            DemuraViewTestResult testResult = new DemuraViewTestResult();

            try
            {
                testResult.PreviewImageFile = FindPreviewImageFile(ctx.Batch.Id);
                var masters = AlgResultMasterDao.Instance.GetAllByBatchId(ctx.Batch.Id);
                var candidates = CollectImageConvertCandidates(masters, Config.ImageConvertResultType);

                ApplyCandidate(testResult.W128, FindCandidate(candidates, Config.W128Keyword, Prepared128FileName));
                ApplyCandidate(testResult.W255, FindCandidate(candidates, Config.W255Keyword, Prepared255FileName));

                bool hasRequiredCsv = testResult.W128.SourceExists && testResult.W255.SourceExists;
                if (Config.PrepareDemuraTool)
                {
                    if (hasRequiredCsv)
                    {
                        await PrepareDemuraToolAsync(testResult, log).ConfigureAwait(false);
                    }
                    else
                    {
                        testResult.Message = "未找到完整的W128/W255 CSV，已跳过DemuraTool准备。";
                    }
                }

                RebuildItems(testResult);
                UpdateObjectiveTestResult(ctx.ObjectiveTestResult, Config.Name, testResult.Items);

                ctx.Result.FileName = GetPreviewFileName(testResult);
                ctx.Result.ViewResultJson = JsonConvert.SerializeObject(testResult);
                ctx.Result.Result &= testResult.Items.All(item => item.TestResult);
                if (!ctx.Result.Result && string.IsNullOrWhiteSpace(ctx.Result.Msg))
                    ctx.Result.Msg = string.IsNullOrWhiteSpace(testResult.Message) ? "Demura CSV准备失败" : testResult.Message;

                return true;
            }
            catch (Exception ex)
            {
                log?.Error(ex);
                ctx.Result.Result = false;
                ctx.Result.Msg = ex.Message;
                return false;
            }
        }

        public override void Render(IProcessExecutionContext ctx)
        {
        }

        public override void GenText(IProcessExecutionContext ctx, Paragraph paragraph, Brush foreground, double fontSize)
        {
            AppendOutputLine(paragraph, $"{Config.Name} 画面结果", foreground, fontSize);

            if (string.IsNullOrWhiteSpace(ctx.Result.ViewResultJson)) return;

            DemuraTestResult? testResult = JsonConvert.DeserializeObject<DemuraTestResult>(ctx.Result.ViewResultJson);
            if (testResult == null) return;

            AppendCsvOutput(paragraph, "W128", testResult.W128, foreground, fontSize);
            AppendCsvOutput(paragraph, "W255", testResult.W255, foreground, fontSize);
            AppendFileLinkOutput(paragraph, "PreviewImage", testResult.PreviewImageFile, foreground, fontSize);
            AppendFileLinkOutput(paragraph, "ToolDirectory", testResult.ToolDirectory, foreground, fontSize);
            AppendFileLinkOutput(paragraph, "DemuraConfig", testResult.DemuraConfigFile, foreground, fontSize);
            AppendFileLinkOutput(paragraph, "DynamicBin", testResult.DynamicBinFile, foreground, fontSize);
            AppendFileLinkOutput(paragraph, "MergedBin", testResult.MergedBinFile, foreground, fontSize);
            if (testResult.MergedBinExists)
                AppendOutputLine(paragraph, "FlashAddress:0x00003000", foreground, fontSize);
            AppendOutputLine(paragraph, $"BurnStatus:{BuildBurnStatusText(testResult)}", foreground, fontSize);
            if (testResult.BurnEnabled)
            {
                AppendFileLinkOutput(paragraph, "BurnSource", testResult.BurnSourceFile, foreground, fontSize);
                AppendOutputLine(paragraph, $"BurnTarget:{testResult.BurnTargetFileName}", foreground, fontSize);
                AppendOutputLine(paragraph, $"BurnSensor:{testResult.BurnSensorName}({testResult.BurnSensorCode}) {testResult.BurnAddress}:{testResult.BurnPort}", foreground, fontSize);
                AppendOutputLine(paragraph, "BurnConnectionMode:TCPIP", foreground, fontSize);
                AppendOutputLine(paragraph, $"BurnCommand:{testResult.BurnCommand}", foreground, fontSize);
                if (!string.IsNullOrWhiteSpace(testResult.BurnCommandHex))
                    AppendOutputLine(paragraph, $"BurnCommandHex:{testResult.BurnCommandHex}", foreground, fontSize);
                if (!string.IsNullOrWhiteSpace(testResult.BurnResponseText))
                    AppendOutputLine(paragraph, $"BurnResponse:{testResult.BurnResponseText}", foreground, fontSize);
                if (!string.IsNullOrWhiteSpace(testResult.BurnMessage))
                    AppendOutputLine(paragraph, $"BurnMessage:{testResult.BurnMessage}", foreground, fontSize);
            }
            if (!string.IsNullOrWhiteSpace(testResult.Message))
                AppendOutputLine(paragraph, $"Message:{testResult.Message}", foreground, fontSize);

            AppendOutputLine(paragraph, "Name,Value,Unit,LowLimit,UpLimit,Result", foreground, fontSize);
            foreach (var item in testResult.Items)
            {
                AppendOutputLine(paragraph, $"{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{(item.TestResult ? "PASS" : "Fail")}", foreground, fontSize);
            }
        }

        private static void AppendCsvOutput(Paragraph paragraph, string name, DemuraCsvFileResult result, Brush foreground, double fontSize)
        {
            AppendFileLinkOutput(paragraph, $"{name}Source", result.SourceFile, foreground, fontSize);
            AppendFileLinkOutput(paragraph, $"{name}Csv", result.PreparedFile, foreground, fontSize);
            AppendOutputLine(paragraph, $"{name} ExposureTime:{result.ExposureTime:G17}", foreground, fontSize);
            AppendOutputLine(paragraph, $"{name} MasterId:{result.MasterId} Field:{result.SourceField}", foreground, fontSize);
            AppendOutputLine(paragraph, $"{name} Count:{result.ValueCount} Lines:{result.LineCount} Size:{result.FileSize} Min:{result.Min:F4} Max:{result.Max:F4} Average:{result.Average:F4}", foreground, fontSize);
            if (!string.IsNullOrWhiteSpace(result.ParseMessage))
                AppendOutputLine(paragraph, $"{name} Message:{result.ParseMessage}", foreground, fontSize);
        }

        private static void AppendFileLinkOutput(Paragraph paragraph, string name, string filePath, Brush foreground, double fontSize)
        {
            bool exists = File.Exists(filePath) || Directory.Exists(filePath);
            string displayName = string.IsNullOrWhiteSpace(filePath) ? "-" : Path.GetFileName(filePath);
            if (!string.IsNullOrWhiteSpace(filePath) && Directory.Exists(filePath))
                displayName = new DirectoryInfo(filePath).Name;

            if (paragraph.Inlines.Count > 0)
                paragraph.Inlines.Add(new LineBreak());

            paragraph.Inlines.Add(CreateOutputRun($"{name}:", foreground, fontSize));
            if (string.IsNullOrWhiteSpace(filePath))
            {
                paragraph.Inlines.Add(CreateOutputRun(displayName, foreground, fontSize));
            }
            else
            {
                var link = new Hyperlink(CreateOutputRun(displayName, Brushes.Blue, fontSize))
                {
                    CommandParameter = filePath,
                    Foreground = Brushes.Blue,
                    TextDecorations = TextDecorations.Underline
                };
                link.PreviewMouseLeftButtonDown += OutputFileLink_PreviewMouseLeftButtonDown;
                paragraph.Inlines.Add(link);
            }
            paragraph.Inlines.Add(CreateOutputRun($" Exists:{exists}", foreground, fontSize));
        }

        private static void AppendOutputLine(Paragraph paragraph, string text, Brush foreground, double fontSize)
        {
            if (paragraph.Inlines.Count > 0)
                paragraph.Inlines.Add(new LineBreak());

            paragraph.Inlines.Add(CreateOutputRun(text, foreground, fontSize));
        }

        private static Run CreateOutputRun(string text, Brush foreground, double fontSize)
        {
            return new Run(text)
            {
                Foreground = foreground,
                FontSize = fontSize
            };
        }

        private static void OutputFileLink_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount < 2 || sender is not Hyperlink link || link.CommandParameter is not string filePath)
                return;

            PlatformHelper.OpenFolderAndSelectFile(filePath);
            e.Handled = true;
        }

        private async Task PrepareDemuraToolAsync(DemuraViewTestResult testResult, ILog? log)
        {
            string? bundledToolDir = ResolveBundledToolDirectory();
            if (string.IsNullOrWhiteSpace(bundledToolDir))
            {
                testResult.Message = "未找到打包的DemuraTool_x64资源。";
                log?.Warn(testResult.Message);
                return;
            }

            string workDirectory = BuildWorkDirectory();
            log?.Info($"Demura准备工具目录: source={bundledToolDir}, work={workDirectory}");
            CopyMissingDirectoryFiles(bundledToolDir, workDirectory);

            string csvDirectory = Path.Combine(workDirectory, "csv");
            Directory.CreateDirectory(csvDirectory);

            string g128 = Path.Combine(csvDirectory, Prepared128FileName);
            string g255 = Path.Combine(csvDirectory, Prepared255FileName);
            double w128ExposureTime = ValidateExposureTime(Config.W128ExposureTime, "W128");
            double w255ExposureTime = ValidateExposureTime(Config.W255ExposureTime, "W255");
            testResult.W128.ExposureTime = w128ExposureTime;
            testResult.W255.ExposureTime = w255ExposureTime;
            WriteExposureCalibratedCsv(testResult.W128.SourceFile, g128, w128ExposureTime, "W128", log);
            WriteExposureCalibratedCsv(testResult.W255.SourceFile, g255, w255ExposureTime, "W255", log);
            testResult.W128.PreparedFile = g128;
            testResult.W255.PreparedFile = g255;

            string configFile = Path.Combine(workDirectory, "DemuraConfig.ini");
            File.WriteAllText(configFile, BuildDemuraConfig(), Encoding.UTF8);

            string exeFile = Path.Combine(workDirectory, ToolExeName);
            string staticBin = Path.Combine(workDirectory, "DemuraStatic.bin");
            string dynamicBin = Path.Combine(workDirectory, "DemuraDynamic.bin");
            string mergedBin = Path.Combine(workDirectory, "DemuraMerged.bin");

            testResult.ToolDirectory = workDirectory;
            testResult.WorkDirectory = workDirectory;
            testResult.CsvDirectory = csvDirectory;
            testResult.ToolExecutable = exeFile;
            testResult.DemuraConfigFile = configFile;
            testResult.StaticBinFile = staticBin;
            testResult.DynamicBinFile = dynamicBin;
            testResult.MergedBinFile = mergedBin;
            testResult.ToolPrepared = File.Exists(exeFile) && File.Exists(Path.Combine(workDirectory, "DemuraSaveBin.dll")) && File.Exists(Path.Combine(workDirectory, "GenDemuraCompressionLUT.dll")) && File.Exists(g128) && File.Exists(g255);

            if (Config.GenerateBinWithDll)
            {
                try
                {
                    log?.Info($"Demura开始生成bin: work={workDirectory}, g128={g128}, g255={g255}, w128Exposure={w128ExposureTime}, w255Exposure={w255ExposureTime}, input={Config.InputWidth}x{Config.InputHeight}, output={Config.OutputWidth}x{Config.OutputHeight}");
                    var binResult = DemuraBinGenerator.Generate(workDirectory, g128, g255, Config.InputWidth, Config.InputHeight, Config.OutputWidth, Config.OutputHeight, Config.BlockMode, Config.PaddingMode);
                    testResult.StaticBinFile = binResult.StaticBinFile;
                    testResult.DynamicBinFile = binResult.DynamicBinFile;
                    testResult.MergedBinFile = binResult.MergedBinFile;
                    testResult.BinGenerated = binResult.Success;
                    log?.Info($"Demura生成bin完成: success={binResult.Success}, static={testResult.StaticBinFile}, dynamic={testResult.DynamicBinFile}, merged={testResult.MergedBinFile}, message={binResult.Message}");
                    if (!binResult.Success)
                        AppendMessage(testResult, binResult.Message);
                }
                catch (Exception ex)
                {
                    testResult.BinGenerated = false;
                    log?.Error("Demura bin生成异常", ex);
                    AppendMessage(testResult, $"Demura bin生成失败：{ex.Message}");
                }
            }

            if (Config.LaunchDemuraTool && File.Exists(exeFile))
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo(exeFile) { WorkingDirectory = workDirectory, UseShellExecute = true });
                testResult.ToolLaunched = true;
            }

            testResult.MergedBinExists = File.Exists(mergedBin);

            if (Config.BurnAfterGenerate)
            {
                await BurnDemuraBinAsync(testResult, log).ConfigureAwait(false);
            }
        }

        private async Task BurnDemuraBinAsync(DemuraViewTestResult testResult, ILog? log)
        {
            testResult.BurnEnabled = true;
            testResult.BurnTargetFileName = string.IsNullOrWhiteSpace(Config.BurnTargetFileName) ? "DemuraMerged.bin" : Config.BurnTargetFileName.Trim();
            testResult.BurnSourceFile = ResolveBurnSourceFile(testResult);

            try
            {
                log?.Info($"Demura烧录开始: source={testResult.BurnSourceFile}, target={testResult.BurnTargetFileName}");
                if (!File.Exists(testResult.BurnSourceFile))
                {
                    SetBurnFailure(testResult, $"烧录源bin不存在：{testResult.BurnSourceFile}", log);
                    return;
                }

                DeviceSensor? sensor = FindGeneralSensor();
                if (sensor == null)
                {
                    SetBurnFailure(testResult, $"未找到通用传感器服务，Code={Config.GeneralSensorCode}, Category={Config.GeneralSensorCategory}", log);
                    return;
                }

                testResult.BurnSensorCode = sensor.Code;
                testResult.BurnSensorName = sensor.Name;
                testResult.BurnAddress = sensor.Config.Addr;
                testResult.BurnPort = sensor.Config.Port;

                if (string.IsNullOrWhiteSpace(sensor.Config.Addr) || sensor.Config.Port <= 0)
                {
                    SetBurnFailure(testResult, $"通用传感器PG连接配置无效：{sensor.Config.Addr}:{sensor.Config.Port}", log);
                    return;
                }

                log?.Info($"Demura烧录使用TCPIP直连PG，不关闭通用传感器服务: {sensor.Name}({sensor.Code})");
                TcpBurnResult burnResult = await SendBurnCommandAsync(sensor.Config.Addr, sensor.Config.Port, testResult.BurnSourceFile, testResult.BurnTargetFileName, log).ConfigureAwait(false);
                testResult.BurnCommand = burnResult.CommandText;
                testResult.BurnCommandHex = burnResult.CommandHex;
                testResult.BurnResponseText = burnResult.ResponseText;
                testResult.BurnResponseHex = burnResult.ResponseHex;
                testResult.BurnSucceeded = burnResult.Success;
                testResult.BurnMessage = burnResult.Message;
                if (!burnResult.Success)
                    AppendMessage(testResult, burnResult.Message);
            }
            catch (Exception ex)
            {
                testResult.BurnSucceeded = false;
                testResult.BurnMessage = $"Demura烧录异常：{ex.Message}";
                log?.Error("Demura烧录异常", ex);
                AppendMessage(testResult, testResult.BurnMessage);
            }
            finally
            {
                log?.Info($"Demura烧录结束: success={testResult.BurnSucceeded}, response={testResult.BurnResponseText}, message={testResult.BurnMessage}");
            }
        }

        private async Task<TcpBurnResult> SendBurnCommandAsync(string address, int port, string sourceFile, string targetFileName, ILog? log)
        {
            string powerOnBody = "PG,1,POWER,ON";
            string powerOnFrame = BuildLengthPrefixedBody(powerOnBody);
            byte[] powerOnBytes = BuildBurnCommandBytes(powerOnBody);
            string powerOnText = $"[02][FF]{powerOnFrame}[03]";
            string powerOnHex = ToHex(powerOnBytes);

            string pgChannel = string.IsNullOrWhiteSpace(Config.BurnPgChannel) ? "01" : Config.BurnPgChannel.Trim();
            string sendFileBody = $"PG,{pgChannel},SENDFILE,START,{Config.BurnFileIndex},{sourceFile},{targetFileName}";
            string sendFileFrame = BuildLengthPrefixedBody(sendFileBody);
            byte[] sendFileBytes = BuildBurnCommandBytes(sendFileBody);
            string sendFileText = $"[02][FF]{sendFileFrame}[03]";
            string sendFileHex = ToHex(sendFileBytes);
            string powerOffBody = "PG,1,POWER,OFF";
            string powerOffFrame = BuildLengthPrefixedBody(powerOffBody);
            byte[] powerOffBytes = BuildBurnCommandBytes(powerOffBody);
            string powerOffText = $"[02][FF]{powerOffFrame}[03]";
            string powerOffHex = ToHex(powerOffBytes);
            string commandText = $"PowerOn={powerOnText};SendFile={sendFileText};PowerOff={powerOffText}";
            string commandHex = $"PowerOn={powerOnHex};SendFile={sendFileHex};PowerOff={powerOffHex}";
            string successResponse = string.IsNullOrWhiteSpace(Config.BurnSuccessResponse) ? "SENDFILE,END,OK" : Config.BurnSuccessResponse;

            log?.Info($"Demura烧录TCP连接PG: {address}:{port}, commands={commandText}, hex={commandHex}");
            using TcpClient tcpClient = new();
            try
            {
                await tcpClient.ConnectAsync(address, port).WaitAsync(TimeSpan.FromMilliseconds(Math.Max(1000, Config.BurnTcpConnectTimeoutMs))).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return TcpBurnResult.FromFailure(commandText, commandHex, string.Empty, string.Empty, $"连接PG超时：{address}:{port}");
            }

            tcpClient.NoDelay = true;

            using NetworkStream stream = tcpClient.GetStream();
            List<CommandExchange> exchanges = new();
            CommandExchange? failure = null;
            bool powerOnAccepted = false;
            bool powerOffAttempted = false;

            try
            {
                CommandExchange powerOn = await SendCommandAndWaitAsync(stream, "PowerOn", powerOnText, powerOnBytes, "POWER,ON,END,OK", log).ConfigureAwait(false);
                exchanges.Add(powerOn);
                if (powerOn.Success)
                    powerOnAccepted = true;
                else
                    failure = powerOn;

                if (failure == null)
                {
                    CommandExchange sendFile = await SendCommandAndWaitAsync(stream, "SendFile", sendFileText, sendFileBytes, successResponse, log).ConfigureAwait(false);
                    exchanges.Add(sendFile);
                    if (!sendFile.Success)
                        failure = sendFile;
                }
            }
            catch (Exception ex)
            {
                CommandExchange exception = CommandExchange.FromFailure("BurnException", string.Empty, string.Empty, ex.Message);
                exchanges.Add(exception);
                failure ??= exception;
                log?.Error("Demura烧录指令执行异常", ex);
            }
            finally
            {
                if (powerOnAccepted)
                {
                    powerOffAttempted = true;
                    try
                    {
                        CommandExchange powerOff = await SendCommandAndWaitAsync(stream, "PowerOff", powerOffText, powerOffBytes, "POWER,OFF,END,OK", log).ConfigureAwait(false);
                        exchanges.Add(powerOff);
                        if (!powerOff.Success && failure == null)
                            failure = powerOff;
                    }
                    catch (Exception ex)
                    {
                        CommandExchange powerOff = CommandExchange.FromFailure("PowerOff", string.Empty, string.Empty, ex.Message);
                        exchanges.Add(powerOff);
                        failure ??= powerOff;
                        log?.Error("Demura烧录下电异常", ex);
                    }
                }
            }

            string responseText = BuildExchangeText(exchanges);
            string responseHex = BuildExchangeHex(exchanges);
            if (failure != null)
            {
                string powerOffSuffix = powerOnAccepted && powerOffAttempted ? "，已尝试下电" : string.Empty;
                return TcpBurnResult.FromFailure(commandText, commandHex, responseText, responseHex, $"PG指令失败({failure.Name})：{failure.Message}{powerOffSuffix}");
            }

            return TcpBurnResult.FromSuccess(commandText, commandHex, responseText, responseHex, "烧录成功，上电、烧录、下电均收到回包。");
        }

        private async Task<CommandExchange> SendPowerOffCommandAsync(string address, int port, ILog? log)
        {
            string powerOffBody = "PG,1,POWER,OFF";
            string powerOffFrame = BuildLengthPrefixedBody(powerOffBody);
            byte[] powerOffBytes = BuildBurnCommandBytes(powerOffBody);
            string powerOffText = $"[02][FF]{powerOffFrame}[03]";

            using TcpClient tcpClient = new();
            try
            {
                await tcpClient.ConnectAsync(address, port).WaitAsync(TimeSpan.FromMilliseconds(Math.Max(1000, Config.BurnTcpConnectTimeoutMs))).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return CommandExchange.FromFailure("PowerOff", string.Empty, string.Empty, $"连接PG超时：{address}:{port}");
            }

            tcpClient.NoDelay = true;
            using NetworkStream stream = tcpClient.GetStream();
            return await SendCommandAndWaitAsync(stream, "PowerOff", powerOffText, powerOffBytes, "POWER,OFF,END,OK", log).ConfigureAwait(false);
        }

        private async Task<CommandExchange> SendCommandAndWaitAsync(NetworkStream stream, string name, string commandText, byte[] commandBytes, string expectedResponse, ILog? log)
        {
            await stream.WriteAsync(commandBytes.AsMemory(0, commandBytes.Length)).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
            log?.Info($"Demura烧录指令已发送: step={name}, command={commandText}, hex={ToHex(commandBytes)}, bytes={commandBytes.Length}, expected={expectedResponse}");

            List<byte> received = new();
            byte[] buffer = new byte[1024];
            DateTime deadline = DateTime.Now.AddMilliseconds(Math.Max(1000, Config.BurnTcpResponseTimeoutMs));

            while (DateTime.Now < deadline)
            {
                TimeSpan remaining = deadline - DateTime.Now;
                int read;
                try
                {
                    read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length)).AsTask().WaitAsync(remaining).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    break;
                }

                if (read <= 0)
                    break;

                for (int i = 0; i < read; i++)
                    received.Add(buffer[i]);

                string responseText = DecodeAscii(received);
                string responseHex = ToHex(received);
                log?.Info($"Demura烧录收到PG回包片段: step={name}, text={responseText}, hex={responseHex}");
                if (responseText.Contains(expectedResponse, StringComparison.OrdinalIgnoreCase))
                    return CommandExchange.FromSuccess(name, responseText, responseHex);

                if (ContainsBurnFailureResponse(responseText))
                    return CommandExchange.FromFailure(name, responseText, responseHex, $"PG返回失败：{responseText}");
            }

            string finalText = DecodeAscii(received);
            string finalHex = ToHex(received);
            string message = string.IsNullOrWhiteSpace(finalText) ? $"等待{name}回包超时，未收到数据。" : $"等待{name}期望回包超时：{finalText}";
            return CommandExchange.FromFailure(name, finalText, finalHex, message);
        }

        private string ResolveBurnSourceFile(DemuraTestResult testResult)
        {
            string configured = Config.BurnSourceBinName?.Trim() ?? string.Empty;
            if (Path.IsPathRooted(configured))
                return configured;

            if (string.Equals(configured, Path.GetFileName(testResult.StaticBinFile), StringComparison.OrdinalIgnoreCase))
                return testResult.StaticBinFile;

            if (string.Equals(configured, Path.GetFileName(testResult.MergedBinFile), StringComparison.OrdinalIgnoreCase))
                return testResult.MergedBinFile;

            if (string.IsNullOrWhiteSpace(configured) || string.Equals(configured, Path.GetFileName(testResult.DynamicBinFile), StringComparison.OrdinalIgnoreCase))
                return testResult.DynamicBinFile;

            return string.IsNullOrWhiteSpace(testResult.WorkDirectory) ? configured : Path.Combine(testResult.WorkDirectory, configured);
        }

        private static double ValidateExposureTime(double exposureTime, string name)
        {
            if (double.IsNaN(exposureTime) || double.IsInfinity(exposureTime) || exposureTime <= 0)
                throw new InvalidOperationException($"{name}曝光时间必须大于0。");

            return exposureTime;
        }

        private static void WriteExposureCalibratedCsv(string sourceFile, string targetFile, double exposureTime, string name, ILog? log)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile) ?? ".");
            int numericCount = 0;
            using StreamReader reader = new(sourceFile, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using StreamWriter writer = new(targetFile, false, new UTF8Encoding(false));

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string calibratedLine = Regex.Replace(line, @"(?<![A-Za-z0-9_+\-.])[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?", match =>
                {
                    if (!TryParseDouble(match.Value, out double value))
                        return match.Value;

                    numericCount++;
                    return (value / exposureTime).ToString("G17", CultureInfo.InvariantCulture);
                });
                writer.WriteLine(calibratedLine);
            }

            log?.Info($"Demura {name} CSV曝光校准完成: source={sourceFile}, target={targetFile}, exposure={exposureTime}, numericCount={numericCount}");
        }

        private DeviceSensor? FindGeneralSensor()
        {
            var services = ServiceManager.GetInstance().DeviceServices.OfType<DeviceSensor>();
            DeviceSensor? sensor = null;
            if (!string.IsNullOrWhiteSpace(Config.GeneralSensorCode))
                sensor = services.FirstOrDefault(x => string.Equals(x.Code, Config.GeneralSensorCode, StringComparison.OrdinalIgnoreCase));

            if (sensor == null && !string.IsNullOrWhiteSpace(Config.GeneralSensorCategory))
                sensor = services.FirstOrDefault(x => string.Equals(x.Config.Category, Config.GeneralSensorCategory, StringComparison.OrdinalIgnoreCase));

            return sensor;
        }

        private static byte[] BuildBurnCommandBytes(string commandBody)
        {
            string framedBody = BuildLengthPrefixedBody(commandBody);
            byte[] bodyBytes = Encoding.ASCII.GetBytes(framedBody);
            byte[] bytes = new byte[bodyBytes.Length + 3];
            bytes[0] = 0x02;
            bytes[1] = 0xFF;
            Array.Copy(bodyBytes, 0, bytes, 2, bodyBytes.Length);
            bytes[^1] = 0x03;
            return bytes;
        }

        private static string BuildLengthPrefixedBody(string commandBody)
        {
            int length = Encoding.ASCII.GetByteCount(commandBody);
            return $"{length:X4}{commandBody}";
        }

        private static bool ContainsBurnFailureResponse(string responseText)
        {
            return responseText.Contains("END,NG", StringComparison.OrdinalIgnoreCase) ||
                   responseText.Contains("FAIL", StringComparison.OrdinalIgnoreCase) ||
                   responseText.Contains("ERROR", StringComparison.OrdinalIgnoreCase);
        }

        private static string DecodeAscii(IEnumerable<byte> bytes)
        {
            return Encoding.ASCII.GetString(bytes.ToArray()).Replace("\u0002", "[02]").Replace("\u0003", "[03]");
        }

        private static string ToHex(IEnumerable<byte> bytes)
        {
            StringBuilder builder = new();
            foreach (byte value in bytes)
            {
                if (builder.Length > 0)
                    builder.Append(' ');

                builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string BuildExchangeText(IEnumerable<CommandExchange> exchanges)
        {
            return string.Join(" | ", exchanges.Select(exchange => $"{exchange.Name}:{exchange.Text}"));
        }

        private static string BuildExchangeHex(IEnumerable<CommandExchange> exchanges)
        {
            return string.Join(" | ", exchanges.Select(exchange => $"{exchange.Name}:{exchange.Hex}"));
        }

        private static void SetBurnFailure(DemuraTestResult testResult, string message, ILog? log)
        {
            testResult.BurnSucceeded = false;
            testResult.BurnMessage = message;
            log?.Warn(message);
            AppendMessage(testResult, message);
        }

        private string BuildDemuraConfig()
        {
            return $"""
# Demura Tool Configuration
# Generated by ProjectARVRPro

[Size]
input_width = {Config.InputWidth}
input_height = {Config.InputHeight}
output_width = {Config.OutputWidth}
output_height = {Config.OutputHeight}

[Demura]
block_mode = {Config.BlockMode}
padding_mode = {Config.PaddingMode}

[Paths]
csv_dir = csv
csv_128 = {Prepared128FileName}
csv_255 = {Prepared255FileName}
static_bin = DemuraStatic.bin
dynamic_bin = DemuraDynamic.bin
merged_bin = DemuraMerged.bin
flash_address = 0x00003000
""";
        }

        private void RebuildItems(DemuraViewTestResult testResult)
        {
            testResult.Items.Clear();
            int expectedCount = Math.Max(0, Config.InputWidth) * Math.Max(0, Config.InputHeight);
            AddCsvItems(testResult.Items, "W128", testResult.W128, expectedCount);
            AddCsvItems(testResult.Items, "W255", testResult.W255, expectedCount);

            if (Config.PrepareDemuraTool)
            {
                AddBoolItem(testResult.Items, "DemuraToolPrepared", testResult.ToolPrepared, testResult.WorkDirectory);
                AddBoolItem(testResult.Items, "G128Prepared", File.Exists(testResult.W128.PreparedFile), testResult.W128.PreparedFile);
                AddBoolItem(testResult.Items, "G255Prepared", File.Exists(testResult.W255.PreparedFile), testResult.W255.PreparedFile);
            }

            if (Config.PrepareDemuraTool && Config.GenerateBinWithDll)
            {
                AddBoolItem(testResult.Items, "DemuraBinGenerated", testResult.BinGenerated, testResult.MergedBinFile);
                AddBoolItem(testResult.Items, "DemuraStaticBin", File.Exists(testResult.StaticBinFile), testResult.StaticBinFile);
                AddBoolItem(testResult.Items, "DemuraDynamicBin", File.Exists(testResult.DynamicBinFile), testResult.DynamicBinFile);
                AddBoolItem(testResult.Items, "DemuraMergedBin", testResult.MergedBinExists, testResult.MergedBinFile);
            }
            else if (Config.RequireMergedBin)
            {
                AddBoolItem(testResult.Items, "DemuraMergedBin", testResult.MergedBinExists, testResult.MergedBinFile);
            }

            if (Config.BurnAfterGenerate)
            {
                AddBoolItem(testResult.Items, "DemuraBurnSourceBin", File.Exists(testResult.BurnSourceFile), testResult.BurnSourceFile);
                AddBoolItem(testResult.Items, "DemuraBurnSendFile", testResult.BurnSucceeded, testResult.BurnMessage);
            }
        }

        private void AddCsvItems(ObservableCollection<ObjectiveTestItem> items, string prefix, DemuraCsvFileResult csv, int expectedCount)
        {
            AddBoolItem(items, $"{prefix}FileExists", csv.SourceExists, csv.SourceFile);
            AddItem(items, $"{prefix}FileSize", csv.FileSize, csv.FileSize.ToString(CultureInfo.InvariantCulture), "byte");
            AddItem(items, $"{prefix}LineCount", csv.LineCount, csv.LineCount.ToString(CultureInfo.InvariantCulture), "line");
            AddItem(items, $"{prefix}ExposureTime", csv.ExposureTime, csv.ExposureTime.ToString("G17", CultureInfo.InvariantCulture), "ms");

            if (Config.ValidateInputSize && expectedCount > 0)
                AddItem(items, $"{prefix}ValueCount", csv.ValueCount, csv.ValueCount.ToString(CultureInfo.InvariantCulture), "count", expectedCount, expectedCount);
            else
                AddItem(items, $"{prefix}ValueCount", csv.ValueCount, csv.ValueCount.ToString(CultureInfo.InvariantCulture), "count");

            AddItem(items, $"{prefix}Min", csv.Min, csv.Min.ToString("F4", CultureInfo.InvariantCulture));
            AddItem(items, $"{prefix}Max", csv.Max, csv.Max.ToString("F4", CultureInfo.InvariantCulture));
            AddItem(items, $"{prefix}Average", csv.Average, csv.Average.ToString("F4", CultureInfo.InvariantCulture));
        }

        private static void AddBoolItem(ObservableCollection<ObjectiveTestItem> items, string name, bool value, string testValue)
        {
            AddItem(items, name, value ? 1 : 0, string.IsNullOrWhiteSpace(testValue) ? value.ToString() : testValue, string.Empty, 1, 1);
        }

        private static void AddItem(ObservableCollection<ObjectiveTestItem> items, string name, double value, string testValue, string unit = "", double lowLimit = 0, double upLimit = 0)
        {
            items.Add(new ObjectiveTestItem
            {
                Name = name,
                Value = value,
                TestValue = testValue,
                Unit = unit,
                LowLimit = lowLimit,
                UpLimit = upLimit
            });
        }

        private static void UpdateObjectiveTestResult(ObjectiveTestResult objectiveTestResult, string outputName, ObservableCollection<ObjectiveTestItem> items)
        {
            if (!objectiveTestResult.DynamicTestResults.TryGetValue(outputName, out var dynamicItems))
            {
                dynamicItems = new ObservableCollection<ObjectiveTestItem>();
                objectiveTestResult.DynamicTestResults[outputName] = dynamicItems;
            }

            dynamicItems.Clear();
            foreach (var item in items)
                dynamicItems.Add(item);
        }

        private static void ApplyCandidate(DemuraCsvFileResult result, DemuraFileCandidate? candidate)
        {
            if (candidate == null)
            {
                result.ParseMessage = $"未找到W{result.Gray} CSV。";
                return;
            }

            result.SourceFile = candidate.FilePath;
            result.SourceField = candidate.SourceField;
            result.MasterId = candidate.MasterId;
            result.SourceExists = File.Exists(candidate.FilePath);

            if (!result.SourceExists)
            {
                result.ParseMessage = "文件不存在。";
                return;
            }

            CsvSummary summary = ReadCsvSummary(candidate.FilePath);
            result.FileSize = summary.FileSize;
            result.LineCount = summary.LineCount;
            result.ValueCount = summary.ValueCount;
            result.Min = summary.Min;
            result.Max = summary.Max;
            result.Average = summary.Average;
            result.ParseMessage = summary.Message;
        }

        private static List<DemuraFileCandidate> CollectImageConvertCandidates(List<AlgResultMasterModel> masters, int imageConvertResultType)
        {
            List<DemuraFileCandidate> candidates = new List<DemuraFileCandidate>();

            foreach (var master in masters.Where(m => (int)m.ImgFileType == imageConvertResultType).OrderByDescending(m => m.Id))
            {
                AddCandidate(candidates, master, master.ImgFile, nameof(master.ImgFile));
                AddCandidate(candidates, master, master.ResultImagFile, nameof(master.ResultImagFile));

                foreach (var image in AlgResultImageDao.Instance.GetAllByPid(master.Id))
                {
                    AddCandidate(candidates, master, image.FileName, "AlgResultImageModel.FileName");
                }
            }

            return candidates;
        }

        private static void AddCandidate(List<DemuraFileCandidate> candidates, AlgResultMasterModel master, string? rawPath, string sourceField)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return;

            string filePath = ResolveFilePath(rawPath, master);
            if (candidates.Any(c => string.Equals(c.FilePath, filePath, StringComparison.OrdinalIgnoreCase))) return;

            candidates.Add(new DemuraFileCandidate
            {
                MasterId = master.Id,
                FilePath = filePath,
                SourceField = sourceField
            });
        }

        private static string ResolveFilePath(string rawPath, AlgResultMasterModel master)
        {
            string filePath = rawPath.Trim().Trim('"');
            if (File.Exists(filePath)) return Path.GetFullPath(filePath);

            foreach (string? baseFile in new[] { master.ImgFile, master.ResultImagFile })
            {
                if (string.IsNullOrWhiteSpace(baseFile)) continue;
                string? directory = Path.GetDirectoryName(baseFile);
                if (string.IsNullOrWhiteSpace(directory)) continue;

                string combined = Path.Combine(directory, filePath);
                if (File.Exists(combined)) return Path.GetFullPath(combined);
            }

            return filePath;
        }

        private static DemuraFileCandidate? FindCandidate(List<DemuraFileCandidate> candidates, string keyword, string preparedFileName)
        {
            return candidates
                .Where(candidate => IsMatch(candidate.FilePath, keyword, preparedFileName))
                .OrderByDescending(candidate => File.Exists(candidate.FilePath))
                .ThenByDescending(candidate => candidate.MasterId)
                .FirstOrDefault();
        }

        private static bool IsMatch(string filePath, string keyword, string preparedFileName)
        {
            string fileName = Path.GetFileName(filePath);
            return Contains(fileName, keyword) || Contains(fileName, Path.GetFileNameWithoutExtension(preparedFileName));
        }

        private static bool Contains(string value, string keyword)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !string.IsNullOrWhiteSpace(keyword) &&
                   value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        private static string FindPreviewImageFile(int batchId)
        {
            var images = MeasureImgResultDao.Instance.GetAllByBatchId(batchId);
            foreach (var image in images)
            {
                if (!string.IsNullOrWhiteSpace(image.FileUrl))
                    return image.FileUrl;
            }

            return string.Empty;
        }

        private static CsvSummary ReadCsvSummary(string filePath)
        {
            CsvSummary summary = new CsvSummary { FileSize = new FileInfo(filePath).Length };
            double sum = 0;

            foreach (string line in File.ReadLines(filePath))
            {
                summary.LineCount++;
                foreach (string token in line.Split(CsvSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!TryParseDouble(token, out double value)) continue;

                    if (summary.ValueCount == 0)
                    {
                        summary.Min = value;
                        summary.Max = value;
                    }
                    else
                    {
                        summary.Min = Math.Min(summary.Min, value);
                        summary.Max = Math.Max(summary.Max, value);
                    }

                    summary.ValueCount++;
                    sum += value;
                }
            }

            if (summary.ValueCount > 0)
                summary.Average = sum / summary.ValueCount;
            else
                summary.Message = "CSV中未解析到数值。";

            return summary;
        }

        private static bool TryParseDouble(string token, out double value)
        {
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            return double.TryParse(token, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static string? ResolveBundledToolDirectory()
        {
            string? assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            foreach (string? baseDirectory in new[] { assemblyDirectory, AppContext.BaseDirectory })
            {
                if (string.IsNullOrWhiteSpace(baseDirectory)) continue;

                string candidate = Path.Combine(baseDirectory, "Tools", ToolFolderName);
                if (File.Exists(Path.Combine(candidate, ToolExeName))) return candidate;
            }

            return null;
        }

        private static string BuildWorkDirectory()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "ColorVision", "ProjectARVRPro", ToolFolderName);
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c.ToString(), "");

            return string.IsNullOrWhiteSpace(value) ? "Batch" : value;
        }

        private static void CopyMissingDirectoryFiles(string sourceDirectory, string targetDirectory)
        {
            Directory.CreateDirectory(targetDirectory);

            foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceDirectory, directory);
                Directory.CreateDirectory(Path.Combine(targetDirectory, relative));
            }

            foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceDirectory, file);
                string targetFile = Path.Combine(targetDirectory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile) ?? targetDirectory);
                if (!File.Exists(targetFile))
                    File.Copy(file, targetFile, false);
            }
        }

        private static string GetPreviewFileName(DemuraTestResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.PreviewImageFile)) return result.PreviewImageFile;

            foreach (string fileName in new[] { result.W255.SourceFile, result.W128.SourceFile })
            {
                if (File.Exists(fileName) && PreviewImageExtensions.Contains(Path.GetExtension(fileName)))
                    return fileName;
            }

            return string.Empty;
        }

        private static void AppendMessage(DemuraTestResult result, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (string.IsNullOrWhiteSpace(result.Message))
                result.Message = message;
            else
                result.Message += Environment.NewLine + message;
        }

        private static string BuildBurnStatusText(DemuraTestResult result)
        {
            if (!result.BurnEnabled)
                return "未启用自动烧录。";

            if (result.BurnSucceeded)
                return "烧录成功，TCP/IP直连PG完成。";

            return string.IsNullOrWhiteSpace(result.BurnMessage) ? "烧录失败。" : result.BurnMessage;
        }

        private static void AppendCsvText(StringBuilder sb, string name, DemuraCsvFileResult result)
        {
            AppendFileLinkText(sb, $"{name}Source", result.SourceFile);
            AppendFileLinkText(sb, $"{name}Csv", result.PreparedFile);
            sb.AppendLine($"{name} ExposureTime:{result.ExposureTime:G17}");
            sb.AppendLine($"{name} MasterId:{result.MasterId} Field:{result.SourceField}");
            sb.AppendLine($"{name} Count:{result.ValueCount} Lines:{result.LineCount} Size:{result.FileSize} Min:{result.Min:F4} Max:{result.Max:F4} Average:{result.Average:F4}");
            if (!string.IsNullOrWhiteSpace(result.ParseMessage))
                sb.AppendLine($"{name} Message:{result.ParseMessage}");
        }

        private static void AppendBinLinkText(StringBuilder sb, string name, string filePath)
        {
            AppendFileLinkText(sb, name, filePath);
        }

        private static void AppendFileLinkText(StringBuilder sb, string name, string filePath)
        {
            string displayName = string.IsNullOrWhiteSpace(filePath) ? "-" : filePath;
            bool exists = File.Exists(filePath) || Directory.Exists(filePath);
            sb.AppendLine($"{name}:{displayName} Exists:{exists}");
        }

        private sealed class CommandExchange
        {
            public string Name { get; private init; } = string.Empty;

            public bool Success { get; private init; }

            public string Text { get; private init; } = string.Empty;

            public string Hex { get; private init; } = string.Empty;

            public string Message { get; private init; } = string.Empty;

            public static CommandExchange FromSuccess(string name, string text, string hex)
            {
                return new CommandExchange { Name = name, Success = true, Text = text, Hex = hex, Message = "OK" };
            }

            public static CommandExchange FromFailure(string name, string text, string hex, string message)
            {
                return new CommandExchange { Name = name, Success = false, Text = text, Hex = hex, Message = message };
            }
        }

        private sealed class TcpBurnResult
        {
            public bool Success { get; private init; }

            public string CommandText { get; private init; } = string.Empty;

            public string CommandHex { get; private init; } = string.Empty;

            public string ResponseText { get; private init; } = string.Empty;

            public string ResponseHex { get; private init; } = string.Empty;

            public string Message { get; private init; } = string.Empty;

            public static TcpBurnResult FromSuccess(string commandText, string commandHex, string responseText, string responseHex, string message)
            {
                return new TcpBurnResult { Success = true, CommandText = commandText, CommandHex = commandHex, ResponseText = responseText, ResponseHex = responseHex, Message = message };
            }

            public static TcpBurnResult FromFailure(string commandText, string commandHex, string responseText, string responseHex, string message)
            {
                return new TcpBurnResult { Success = false, CommandText = commandText, CommandHex = commandHex, ResponseText = responseText, ResponseHex = responseHex, Message = message };
            }
        }

        private sealed class DemuraBinGenerationResult
        {
            public bool Success { get; set; }

            public string StaticBinFile { get; set; } = string.Empty;

            public string DynamicBinFile { get; set; } = string.Empty;

            public string MergedBinFile { get; set; } = string.Empty;

            public string Message { get; set; } = string.Empty;
        }

        private static class DemuraBinGenerator
        {
            private const int StaticBufferLength = 768;
            private const int DynamicBufferLength = 166320;

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.I1)]
            private delegate bool DemuraStaticSaveBinDelegate(ref StaticArray array, [Out] byte[] buffer, uint bufferLength);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.I1)]
            private delegate bool DemuraDynamicSaveBinDelegate([In] float[] data, uint length, ref DynamicParams param, [Out] byte[] buffer, uint bufferLength);

            public static DemuraBinGenerationResult Generate(string workDirectory, string csv128, string csv255, int inputWidth, int inputHeight, int outputWidth, int outputHeight, int blockMode, int paddingMode)
            {
                var result = new DemuraBinGenerationResult
                {
                    StaticBinFile = Path.Combine(workDirectory, "DemuraStatic.bin"),
                    DynamicBinFile = Path.Combine(workDirectory, "DemuraDynamic.bin"),
                    MergedBinFile = Path.Combine(workDirectory, "DemuraMerged.bin")
                };

                IntPtr lutHandle = IntPtr.Zero;
                IntPtr demuraHandle = IntPtr.Zero;

                try
                {
                    ValidateDimensions(inputWidth, inputHeight, outputWidth, outputHeight);

                    string lutDll = Path.Combine(workDirectory, "GenDemuraCompressionLUT.dll");
                    string demuraDll = Path.Combine(workDirectory, "DemuraSaveBin.dll");
                    if (!File.Exists(lutDll)) return Fail(result, "未找到GenDemuraCompressionLUT.dll。");
                    if (!File.Exists(demuraDll)) return Fail(result, "未找到DemuraSaveBin.dll。");

                    lutHandle = NativeLibrary.Load(lutDll);
                    demuraHandle = NativeLibrary.Load(demuraDll);

                    var staticSaveBin = Marshal.GetDelegateForFunctionPointer<DemuraStaticSaveBinDelegate>(NativeLibrary.GetExport(demuraHandle, "DemuraStaticSaveBin"));
                    var dynamicSaveBin = Marshal.GetDelegateForFunctionPointer<DemuraDynamicSaveBinDelegate>(NativeLibrary.GetExport(demuraHandle, "DemuraDynamicSaveBin"));

                    byte[] staticBuffer = GenerateStaticBin(staticSaveBin);
                    File.WriteAllBytes(result.StaticBinFile, staticBuffer);

                    float[] dynamicData = BuildDynamicData(csv128, csv255, inputWidth, inputHeight, outputWidth, outputHeight, paddingMode);
                    byte[] dynamicBuffer = GenerateDynamicBin(dynamicSaveBin, dynamicData, outputWidth, outputHeight, blockMode);
                    File.WriteAllBytes(result.DynamicBinFile, dynamicBuffer);

                    using (FileStream stream = File.Create(result.MergedBinFile))
                    {
                        stream.Write(staticBuffer, 0, staticBuffer.Length);
                        stream.Write(dynamicBuffer, 0, dynamicBuffer.Length);
                    }

                    result.Success = File.Exists(result.MergedBinFile);
                    return result;
                }
                catch (Exception ex)
                {
                    return Fail(result, ex.Message);
                }
                finally
                {
                    if (demuraHandle != IntPtr.Zero) NativeLibrary.Free(demuraHandle);
                    if (lutHandle != IntPtr.Zero) NativeLibrary.Free(lutHandle);
                }
            }

            private static DemuraBinGenerationResult Fail(DemuraBinGenerationResult result, string message)
            {
                result.Success = false;
                result.Message = message;
                return result;
            }

            private static void ValidateDimensions(int inputWidth, int inputHeight, int outputWidth, int outputHeight)
            {
                if (inputWidth <= 0 || inputHeight <= 0 || outputWidth <= 0 || outputHeight <= 0)
                    throw new InvalidOperationException("Demura输入/输出尺寸必须大于0。");

                if (outputWidth < inputWidth || outputHeight < inputHeight)
                    throw new InvalidOperationException("Demura输出尺寸不能小于输入尺寸。");
            }

            private static byte[] GenerateStaticBin(DemuraStaticSaveBinDelegate staticSaveBin)
            {
                var array = new StaticArray
                {
                    BurstDataPoint = BuildPoint(),
                    BurstDataSlope = BuildSlope(),
                    BurstDataScaler = Enumerable.Repeat((ushort)4095, 84).ToArray(),
                    BurstDataTYPE2 = BuildType2()
                };

                byte[] buffer = new byte[StaticBufferLength];
                if (!staticSaveBin(ref array, buffer, (uint)buffer.Length))
                    throw new InvalidOperationException("DemuraStaticSaveBin返回失败。");

                return buffer;
            }

            private static byte[] GenerateDynamicBin(DemuraDynamicSaveBinDelegate dynamicSaveBin, float[] dynamicData, int outputWidth, int outputHeight, int blockMode)
            {
                var parameters = new DynamicParams
                {
                    Width = (uint)outputWidth,
                    Height = (uint)outputHeight,
                    BlockMode = (uint)Math.Max(0, blockMode)
                };

                byte[] buffer = new byte[DynamicBufferLength];
                if (!dynamicSaveBin(dynamicData, (uint)dynamicData.Length, ref parameters, buffer, (uint)buffer.Length))
                    throw new InvalidOperationException("DemuraDynamicSaveBin返回失败。");

                return buffer;
            }

            private static float[] BuildDynamicData(string csv128, string csv255, int inputWidth, int inputHeight, int outputWidth, int outputHeight, int paddingMode)
            {
                float[] values128 = ReadCsvValues(csv128, inputWidth * inputHeight);
                float[] values255 = ReadCsvValues(csv255, inputWidth * inputHeight);
                float[] padded128 = PadData(values128, inputWidth, inputHeight, outputWidth, outputHeight, paddingMode);
                float[] padded255 = PadData(values255, inputWidth, inputHeight, outputWidth, outputHeight, paddingMode);
                float[] data = new float[padded128.Length + padded255.Length];
                Array.Copy(padded128, 0, data, 0, padded128.Length);
                Array.Copy(padded255, 0, data, padded128.Length, padded255.Length);
                return data;
            }

            private static float[] ReadCsvValues(string filePath, int expectedCount)
            {
                float[] values = new float[expectedCount];
                int index = 0;

                foreach (string line in File.ReadLines(filePath))
                {
                    if (index >= expectedCount) break;

                    foreach (string token in line.Split(CsvSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (index >= expectedCount) break;
                        if (!TryParseFloat(token, out float value)) continue;
                        values[index++] = value <= 0 ? 1.0f : value;
                    }
                }

                if (index < expectedCount)
                    throw new InvalidOperationException($"{Path.GetFileName(filePath)}数据不足，需要{expectedCount}个数值，实际解析{index}个。");

                return values;
            }

            private static bool TryParseFloat(string token, out float value)
            {
                if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    return true;

                return float.TryParse(token, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
            }

            private static float[] PadData(float[] data, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, int paddingMode)
            {
                float[] output = new float[targetWidth * targetHeight];
                for (int y = 0; y < sourceHeight; y++)
                    Array.Copy(data, y * sourceWidth, output, y * targetWidth, sourceWidth);

                float meanValue = paddingMode == 3 ? Mean(data) : 0;

                for (int y = 0; y < sourceHeight; y++)
                {
                    for (int x = sourceWidth; x < targetWidth; x++)
                    {
                        output[y * targetWidth + x] = paddingMode switch
                        {
                            1 or 2 => data[y * sourceWidth + sourceWidth - 1],
                            3 => meanValue,
                            _ => 0
                        };
                    }
                }

                for (int y = sourceHeight; y < targetHeight; y++)
                {
                    for (int x = 0; x < targetWidth; x++)
                    {
                        output[y * targetWidth + x] = paddingMode switch
                        {
                            1 or 2 => output[(sourceHeight - 1) * targetWidth + x],
                            3 => meanValue,
                            _ => 0
                        };
                    }
                }

                return output;
            }

            private static float Mean(float[] data)
            {
                double sum = 0;
                foreach (float value in data)
                    sum += value;

                return (float)(sum / data.Length);
            }

            private static ushort[] BuildPoint()
            {
                ushort[] values = new ushort[84];
                ushort[] first = new ushort[] { 112, 256, 1023, 1023, 1023, 1023, 1023 };
                ushort[] rest = new ushort[] { 112, 256, 512, 1023, 1023, 1023, 1023 };
                Array.Copy(first, 0, values, 0, first.Length);
                for (int group = 1; group < 12; group++)
                    Array.Copy(rest, 0, values, group * rest.Length, rest.Length);

                return values;
            }

            private static ushort[] BuildSlope()
            {
                ushort[] values = new ushort[96];
                ushort[] first = new ushort[] { 73, 57, 0, 0, 0, 9, 64, 64 };
                ushort[] rest = new ushort[] { 73, 57, 16, 4, 0, 9, 64, 64 };
                Array.Copy(first, 0, values, 0, first.Length);
                for (int group = 1; group < 12; group++)
                    Array.Copy(rest, 0, values, group * rest.Length, rest.Length);

                return values;
            }

            private static ushort[] BuildType2()
            {
                return new ushort[]
                {
                    1024, 0, 22,
                    1024, 372, 22,
                    1024, 745, 22,
                    1024, 1117, 22,
                    1024, 1489, 22,
                    1024, 1861, 22,
                    1024, 2234, 22,
                    1024, 2606, 22,
                    1024, 2978, 22,
                    1024, 3350, 22,
                    1024, 3723, 22,
                    1024, 4095, 0
                };
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct StaticArray
            {
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 84)]
                public ushort[] BurstDataPoint;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 96)]
                public ushort[] BurstDataSlope;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 84)]
                public ushort[] BurstDataScaler;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
                public ushort[] BurstDataTYPE2;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct DynamicParams
            {
                public uint Width;

                public uint Height;

                public uint BlockMode;
            }
        }

        private sealed class DemuraFileCandidate
        {
            public int MasterId { get; set; }

            public string FilePath { get; set; } = string.Empty;

            public string SourceField { get; set; } = string.Empty;
        }

        private sealed class CsvSummary
        {
            public long FileSize { get; set; }

            public int LineCount { get; set; }

            public int ValueCount { get; set; }

            public double Min { get; set; }

            public double Max { get; set; }

            public double Average { get; set; }

            public string Message { get; set; } = string.Empty;
        }
    }
}
