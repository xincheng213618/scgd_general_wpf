#pragma warning disable CA1031
using FlowEngineLib;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Templates.Flow.Nodes
{
    public enum CommandLineScriptType
    {
        Auto,
        Python,
        Cmd,
        PowerShell,
        Executable
    }

    internal sealed class CommandLineScriptResultData
    {
        public string? Command { get; set; }

        public string? WorkingDirectory { get; set; }

        public int? ExitCode { get; set; }

        public bool TimedOut { get; set; }

        public int TotalTime { get; set; }

        public string? StandardOutput { get; set; }

        public string? StandardError { get; set; }
    }

    [STNode("Flow_CustomNodes", "CommandLineScript_NodeName")]
    public class CommandLineScriptNode : CVBaseServerNode
    {
        private const string LocalTopic = "LOCAL";
        private const string DefaultPythonExecutable = "python";
        private const string DefaultCmdExecutable = "cmd.exe";
        private const string DefaultPowerShellExecutable = "powershell.exe";
        private const int DefaultMaxOutputChars = 16000;
        private static readonly char[] SuccessExitCodeSeparators = new[] { ',', ';', ' ', '\t', '\r', '\n' };

        private CommandLineScriptType _ScriptType;
        private string _ScriptFile;
        private string _CommandText;
        private string _Arguments;
        private string _WorkingDirectory;
        private string _PythonExecutable;
        private string _SuccessExitCodes;
        private int _MaxOutputChars;

        [Category("PreProcess_ScriptGroup")]
        [STNodeProperty("PreProcess_ScriptType", "PreProcess_ScriptTypeDesc", true)]
        public CommandLineScriptType ScriptType
        {
            get => _ScriptType;
            set => _ScriptType = value;
        }

        [Category("PreProcess_ScriptGroup")]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        [STNodeProperty("PreProcess_ScriptFile", "PreProcess_ScriptFileDesc", true)]
        public string ScriptFile
        {
            get => _ScriptFile;
            set => _ScriptFile = value ?? string.Empty;
        }

        [Category("PreProcess_ScriptGroup")]
        [STNodeProperty("PreProcess_CommandText", "PreProcess_CommandTextDesc", true)]
        public string CommandText
        {
            get => _CommandText;
            set => _CommandText = value ?? string.Empty;
        }

        [Category("PreProcess_ScriptGroup")]
        [STNodeProperty("PreProcess_Arguments", "CommandLineScript_ArgumentsDesc", true)]
        public string Arguments
        {
            get => _Arguments;
            set => _Arguments = value ?? string.Empty;
        }

        [Category("PreProcess_ScriptGroup")]
        [PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
        [STNodeProperty("PreProcess_WorkingDirectory", "PreProcess_WorkingDirectoryDesc", true)]
        public string WorkingDirectory
        {
            get => _WorkingDirectory;
            set => _WorkingDirectory = value ?? string.Empty;
        }

        [Category("PreProcess_ScriptGroup")]
        [STNodeProperty("PreProcess_PythonExecutable", "PreProcess_PythonExecutableDesc", true)]
        public string PythonExecutable
        {
            get => _PythonExecutable;
            set => _PythonExecutable = value ?? string.Empty;
        }

        [Category("PreProcess_ScriptGroup")]
        [STNodeProperty("PreProcess_SuccessExitCodes", "PreProcess_SuccessExitCodesDesc", true)]
        public string SuccessExitCodes
        {
            get => _SuccessExitCodes;
            set => _SuccessExitCodes = value ?? string.Empty;
        }

        [Category("PreProcess_ScriptGroup")]
        [STNodeProperty("PreProcess_MaxOutputChars", "CommandLineScript_MaxOutputCharsDesc", true)]
        public int MaxOutputChars
        {
            get => _MaxOutputChars;
            set => _MaxOutputChars = Math.Max(0, value);
        }

        public CommandLineScriptNode()
            : base(Properties.Resources.CommandLineScript_NodeName, "CommandLine", "SVR.CommandLine.Default", "DEV.CommandLine.Default")
        {
            operatorCode = "Execute";
            _MaxTime = 60000;
            _ScriptType = CommandLineScriptType.Auto;
            _ScriptFile = string.Empty;
            _CommandText = string.Empty;
            _Arguments = string.Empty;
            _WorkingDirectory = string.Empty;
            _PythonExecutable = DefaultPythonExecutable;
            _SuccessExitCodes = "0";
            _MaxOutputChars = DefaultMaxOutputChars;
        }

        protected override void m_in_start_DataTransfer(object sender, STNodeOptionEventArgs e)
        {
            if (e.Status != ConnectionStatus.Connected || !HasData(e))
            {
                m_op_end.TransferData(e.TargetOption.Data);
                return;
            }

            if (e.TargetOption.Data is not CVStartCFC start)
            {
                m_op_end.TransferData(e.TargetOption.Data);
                return;
            }

            start.NormalizeStopStatus();
            if (!start.IsRunning)
            {
                m_op_end.TransferData(start);
                return;
            }

            CVTransAction trans = new(start);
            m_trans_action.AddOrUpdate(start.SerialNumber, trans, (_, _) => trans);
            nodeRunEvent?.Invoke(this, new FlowEngineNodeRunEventArgs
            {
                SendTopic = LocalTopic,
                SendMsgId = start.SerialNumber,
                SendEventName = operatorCode,
                SendPayload = BuildRunPayload(start)
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    await FinishCommandLineNodeAsync(trans).ConfigureAwait(false);
                }
                finally
                {
                    m_trans_action.TryRemove(trans.trans_action.SerialNumber, out _);
                }
            });
        }

        private async Task FinishCommandLineNodeAsync(CVTransAction trans)
        {
            CVStartCFC action = trans.trans_action;
            if (!TryBuildProcessStartInfo(action, out ProcessStartInfo startInfo, out string displayCommand, out string errorMessage))
            {
                FailCommandLineNode(trans, errorMessage, null, -1);
                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            CommandLineScriptResultData resultData = new()
            {
                Command = displayCommand,
                WorkingDirectory = startInfo.WorkingDirectory
            };

            try
            {
                using Process process = new()
                {
                    StartInfo = startInfo
                };

                if (!process.Start())
                {
                    FailCommandLineNode(trans, Properties.Resources.CommandLineScript_StartFailed, resultData, -1);
                    return;
                }

                Task<string> stdoutTask = ReadCappedAsync(process.StandardOutput, MaxOutputChars);
                Task<string> stderrTask = ReadCappedAsync(process.StandardError, MaxOutputChars);
                bool timedOut = false;

                try
                {
                    if (GetMaxDelay() > 0)
                    {
                        using CancellationTokenSource cts = new(GetMaxDelay());
                        await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await process.WaitForExitAsync().ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    timedOut = true;
                    KillProcessTree(process);
                }

                resultData.StandardOutput = await stdoutTask.ConfigureAwait(false);
                resultData.StandardError = await stderrTask.ConfigureAwait(false);
                stopwatch.Stop();
                resultData.TotalTime = ToIntMilliseconds(stopwatch.ElapsedMilliseconds);

                if (timedOut)
                {
                    resultData.TimedOut = true;
                    SetResultData(action, resultData);
                    FailCommandLineNode(trans, string.Format(Properties.Resources.CommandLineScript_Timeout, GetMaxDelay()), resultData, -2);
                    return;
                }

                resultData.ExitCode = process.ExitCode;
                SetResultData(action, resultData);

                if (IsSuccessExitCode(process.ExitCode))
                {
                    CVServerResponse response = new(action.SerialNumber, ActionStatusEnum.Finish, $"ExitCode={process.ExitCode}", operatorCode, resultData);
                    svrRecvResp = response;
                    action.AddResult(GetLocalNodeName(), response, trans.startTime);
                    TransferEnd(trans, response, 0);
                    return;
                }

                string message = BuildExitCodeFailureMessage(process.ExitCode, resultData);
                FailCommandLineNode(trans, message, resultData, -1);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                resultData.TotalTime = ToIntMilliseconds(stopwatch.ElapsedMilliseconds);
                SetResultData(action, resultData);
                FailCommandLineNode(trans, string.Format(Properties.Resources.CommandLineScript_ExecutionException, ex.Message), resultData, -1);
            }
        }

        private bool TryBuildProcessStartInfo(CVStartCFC start, out ProcessStartInfo startInfo, out string displayCommand, out string errorMessage)
        {
            startInfo = null!;
            displayCommand = string.Empty;
            errorMessage = string.Empty;

            string workingDirectoryText = ExpandPlaceholders(_WorkingDirectory, start);
            string workingDirectory = ResolveDirectory(workingDirectoryText);
            string scriptFile = ResolveScriptFile(ExpandPlaceholders(_ScriptFile, start), workingDirectory);
            string commandText = ExpandPlaceholders(_CommandText, start);
            string arguments = ExpandPlaceholders(_Arguments, start);

            if (string.IsNullOrWhiteSpace(scriptFile) && string.IsNullOrWhiteSpace(commandText))
            {
                errorMessage = Properties.Resources.CommandLineScript_FileAndCommandEmpty;
                return false;
            }

            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                workingDirectory = ResolveDefaultWorkingDirectory(scriptFile);
            }

            if (!Directory.Exists(workingDirectory))
            {
                errorMessage = string.Format(Properties.Resources.CommandLineScript_WorkingDirectoryNotFound, workingDirectory);
                return false;
            }

            CommandLineScriptType scriptType = ResolveScriptType(scriptFile);
            if (!ValidateScriptFile(scriptType, scriptFile, workingDirectory, out errorMessage))
            {
                return false;
            }

            startInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            ApplyProcessCommand(startInfo, scriptType, scriptFile, commandText, arguments, start);
            displayCommand = BuildDisplayCommand(startInfo);
            return true;
        }

        private void ApplyProcessCommand(ProcessStartInfo startInfo, CommandLineScriptType scriptType, string scriptFile, string commandText, string arguments, CVStartCFC start)
        {
            switch (scriptType)
            {
                case CommandLineScriptType.Python:
                    startInfo.FileName = ResolveExecutable(ExpandPlaceholders(_PythonExecutable, start), DefaultPythonExecutable);
                    startInfo.Arguments = string.IsNullOrWhiteSpace(scriptFile)
                        ? $"-c {QuoteArgument(commandText)} {arguments}".TrimEnd()
                        : $"{QuoteArgument(scriptFile)} {arguments}".TrimEnd();
                    break;
                case CommandLineScriptType.PowerShell:
                    startInfo.FileName = DefaultPowerShellExecutable;
                    startInfo.Arguments = string.IsNullOrWhiteSpace(scriptFile)
                        ? $"-NoProfile -ExecutionPolicy Bypass -Command {QuoteArgument(commandText)}"
                        : $"-NoProfile -ExecutionPolicy Bypass -File {QuoteArgument(scriptFile)} {arguments}".TrimEnd();
                    break;
                case CommandLineScriptType.Executable:
                    if (string.IsNullOrWhiteSpace(scriptFile))
                    {
                        startInfo.FileName = DefaultCmdExecutable;
                        startInfo.Arguments = $"/d /c {commandText}";
                    }
                    else
                    {
                        startInfo.FileName = scriptFile;
                        startInfo.Arguments = arguments;
                    }
                    break;
                case CommandLineScriptType.Cmd:
                default:
                    startInfo.FileName = DefaultCmdExecutable;
                    startInfo.Arguments = string.IsNullOrWhiteSpace(scriptFile)
                        ? $"/d /c {commandText}"
                        : BuildCmdScriptArguments(scriptFile, arguments);
                    break;
            }
        }

        private CommandLineScriptType ResolveScriptType(string scriptFile)
        {
            if (_ScriptType != CommandLineScriptType.Auto)
            {
                return _ScriptType;
            }

            if (string.IsNullOrWhiteSpace(scriptFile))
            {
                return CommandLineScriptType.Cmd;
            }

            string extension = Path.GetExtension(scriptFile).ToLowerInvariant();
            return extension switch
            {
                ".py" or ".pyw" => CommandLineScriptType.Python,
                ".cmd" or ".bat" => CommandLineScriptType.Cmd,
                ".ps1" => CommandLineScriptType.PowerShell,
                _ => CommandLineScriptType.Executable
            };
        }

        private static bool ValidateScriptFile(CommandLineScriptType scriptType, string scriptFile, string workingDirectory, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(scriptFile))
            {
                return true;
            }

            bool exists = File.Exists(scriptFile) || (!Path.IsPathRooted(scriptFile) && File.Exists(Path.Combine(workingDirectory, scriptFile)));
            if (exists)
            {
                return true;
            }

            if (scriptType == CommandLineScriptType.Executable && !LooksLikePath(scriptFile))
            {
                return true;
            }

            errorMessage = string.Format(Properties.Resources.CommandLineScript_FileNotFound, scriptFile);
            return false;
        }

        private static async Task<string> ReadCappedAsync(StreamReader reader, int maxChars)
        {
            char[] buffer = new char[4096];
            StringBuilder builder = new();
            bool truncated = false;
            int read;

            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                if (maxChars <= 0)
                {
                    truncated = true;
                    continue;
                }

                int remaining = maxChars - builder.Length;
                if (remaining > 0)
                {
                    int count = Math.Min(remaining, read);
                    builder.Append(buffer, 0, count);
                }

                if (read > remaining)
                {
                    truncated = true;
                }
            }

            if (truncated && maxChars > 0 && builder.Length < maxChars)
            {
                string suffix = Environment.NewLine + "... output truncated ...";
                int remaining = maxChars - builder.Length;
                builder.Append(suffix, 0, Math.Min(remaining, suffix.Length));
            }

            return builder.ToString();
        }

        private bool IsSuccessExitCode(int exitCode)
        {
            if (string.IsNullOrWhiteSpace(_SuccessExitCodes))
            {
                return exitCode == 0;
            }

            string[] parts = _SuccessExitCodes.Split(SuccessExitCodeSeparators, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawPart in parts)
            {
                string part = rawPart.Trim();
                int dashIndex = part.IndexOf('-', StringComparison.Ordinal);
                if (dashIndex > 0
                    && int.TryParse(part.AsSpan(0, dashIndex), NumberStyles.Integer, CultureInfo.InvariantCulture, out int start)
                    && int.TryParse(part.AsSpan(dashIndex + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int end))
                {
                    if (start > end)
                    {
                        (start, end) = (end, start);
                    }

                    if (exitCode >= start && exitCode <= end)
                    {
                        return true;
                    }
                }
                else if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int code) && exitCode == code)
                {
                    return true;
                }
            }

            return false;
        }

        private void FailCommandLineNode(CVTransAction trans, string message, CommandLineScriptResultData? resultData, int statusCode)
        {
            CVStartCFC action = trans.trans_action;
            if (resultData != null)
            {
                SetResultData(action, resultData);
            }

            if (statusCode == -2)
            {
                trans.NodeOverTime(GetLocalNodeName());
            }
            else
            {
                trans.NodeFailed(message, GetLocalNodeName());
            }

            CVServerResponse response = new(action.SerialNumber, ActionStatusEnum.Failed, message, operatorCode, resultData);
            svrRecvResp = response;
            TransferEnd(trans, response, statusCode);
        }

        private void TransferEnd(CVTransAction trans, CVServerResponse response, int statusCode)
        {
            nodeEndEvent?.Invoke(this, new FlowEngineNodeEndEventArgs
            {
                RecvTopic = LocalTopic,
                RecvMsgId = response.Id,
                RecvEventName = response.EventName,
                RecvStatusCode = statusCode,
                RecvStatusMessage = response.Message,
                RecvPayload = response.Data != null ? JsonConvert.SerializeObject(response.Data) : null
            });
            m_op_end.TransferData(trans.trans_action);
        }

        private void SetResultData(CVStartCFC action, CommandLineScriptResultData resultData)
        {
            if (action.Data == null)
            {
                return;
            }

            string key = $"{GetLocalNodeName()}.Result";
            action.Data[key] = resultData;
        }

        private string BuildRunPayload(CVStartCFC start)
        {
            return JsonConvert.SerializeObject(new
            {
                ServiceName = NodeName,
                DeviceCode,
                EventName = operatorCode,
                start.SerialNumber,
                ScriptType,
                ScriptFile = ExpandPlaceholders(_ScriptFile, start),
                CommandText = ExpandPlaceholders(_CommandText, start),
                Arguments = ExpandPlaceholders(_Arguments, start),
                WorkingDirectory = ExpandPlaceholders(_WorkingDirectory, start)
            });
        }

        private string GetLocalNodeName()
        {
            return $"{base.Title}.{NodeName}";
        }

        private string ExpandPlaceholders(string value, CVStartCFC start)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string serialNumber = start.SerialNumber ?? string.Empty;
            return value
                .Replace("{SerialNumber}", serialNumber, StringComparison.OrdinalIgnoreCase)
                .Replace("{BatchName}", serialNumber, StringComparison.OrdinalIgnoreCase)
                .Replace("{NodeName}", NodeName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{NodeID}", NodeID ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{ZIndex}", ZIndex.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return string.Empty;
            }

            directory = TrimMatchingQuotes(directory.Trim());
            try
            {
                return Path.IsPathRooted(directory)
                    ? Path.GetFullPath(directory)
                    : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, directory));
            }
            catch
            {
                return directory;
            }
        }

        private static string ResolveDefaultWorkingDirectory(string scriptFile)
        {
            if (!string.IsNullOrWhiteSpace(scriptFile) && File.Exists(scriptFile))
            {
                string? directory = Path.GetDirectoryName(scriptFile);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    return directory;
                }
            }

            return Environment.CurrentDirectory;
        }

        private static string ResolveScriptFile(string scriptFile, string workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(scriptFile))
            {
                return string.Empty;
            }

            scriptFile = TrimMatchingQuotes(scriptFile.Trim());
            try
            {
                if (Path.IsPathRooted(scriptFile) || LooksLikePath(scriptFile))
                {
                    string baseDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory;
                    return Path.IsPathRooted(scriptFile)
                        ? Path.GetFullPath(scriptFile)
                        : Path.GetFullPath(Path.Combine(baseDirectory, scriptFile));
                }

                string candidate = Path.Combine(string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory, scriptFile);
                return File.Exists(candidate) ? Path.GetFullPath(candidate) : scriptFile;
            }
            catch
            {
                return scriptFile;
            }
        }

        private static string ResolveExecutable(string executable, string fallback)
        {
            if (string.IsNullOrWhiteSpace(executable))
            {
                return fallback;
            }

            executable = TrimMatchingQuotes(executable.Trim());
            if (!LooksLikePath(executable) && !Path.IsPathRooted(executable))
            {
                return executable;
            }

            try
            {
                return Path.IsPathRooted(executable)
                    ? Path.GetFullPath(executable)
                    : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, executable));
            }
            catch
            {
                return executable;
            }
        }

        private static bool LooksLikePath(string value)
        {
            return value.Contains(Path.DirectorySeparatorChar)
                || value.Contains(Path.AltDirectorySeparatorChar)
                || value.Contains(':');
        }

        private static string TrimMatchingQuotes(string value)
        {
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                return value.Substring(1, value.Length - 2);
            }

            return value;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }

        private static string BuildCmdScriptArguments(string scriptFile, string arguments)
        {
            string invocation = string.IsNullOrWhiteSpace(arguments)
                ? QuoteArgument(scriptFile)
                : $"{QuoteArgument(scriptFile)} {arguments}";
            return $"/d /c \"{invocation}\"";
        }

        private static string BuildDisplayCommand(ProcessStartInfo startInfo)
        {
            if (string.IsNullOrWhiteSpace(startInfo.Arguments))
            {
                return startInfo.FileName;
            }

            return $"{startInfo.FileName} {startInfo.Arguments}";
        }

        private static string BuildExitCodeFailureMessage(int exitCode, CommandLineScriptResultData resultData)
        {
            string detail = FirstNonEmptyLine(resultData.StandardError) ?? FirstNonEmptyLine(resultData.StandardOutput) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(detail))
            {
                return string.Format(Properties.Resources.CommandLineScript_ExecutionFailed, exitCode);
            }

            return string.Format(Properties.Resources.CommandLineScript_ExecutionFailedWithDetail, exitCode, TrimMessage(detail, 240));
        }

        private static string? FirstNonEmptyLine(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            using StringReader reader = new(text);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line.Trim();
                }
            }

            return null;
        }

        private static string TrimMessage(string message, int maxLength)
        {
            if (message.Length <= maxLength)
            {
                return message;
            }

            return string.Concat(message.AsSpan(0, maxLength), "...");
        }

        private static int ToIntMilliseconds(long milliseconds)
        {
            return milliseconds > int.MaxValue ? int.MaxValue : (int)milliseconds;
        }

        private static void KillProcessTree(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best effort: the process may have already exited between timeout and kill.
            }
        }
    }
}
