#pragma warning disable CA1031
using log4net;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Batch.PreProcess
{
    public enum PreProcessScriptType
    {
        Auto,
        Python,
        Cmd,
        PowerShell,
        Executable
    }

    public class ScriptPreProcessConfig : PreProcessConfigBase
    {
        [Display(Name = "PreProcess_ScriptType", Description = "PreProcess_ScriptTypeDesc", GroupName = "PreProcess_ScriptGroup", ResourceType = typeof(Properties.Resources))]
        public PreProcessScriptType ScriptType { get => _ScriptType; set { _ScriptType = value; OnPropertyChanged(); } }
        private PreProcessScriptType _ScriptType = PreProcessScriptType.Auto;

        [Display(Name = "PreProcess_ScriptFile", Description = "PreProcess_ScriptFileDesc", GroupName = "PreProcess_ScriptGroup", ResourceType = typeof(Properties.Resources))]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        public string ScriptFile { get => _ScriptFile; set { _ScriptFile = value ?? string.Empty; OnPropertyChanged(); } }
        private string _ScriptFile = string.Empty;

        [Display(Name = "PreProcess_CommandText", Description = "PreProcess_CommandTextDesc", GroupName = "PreProcess_ScriptGroup", ResourceType = typeof(Properties.Resources))]
        public string CommandText { get => _CommandText; set { _CommandText = value ?? string.Empty; OnPropertyChanged(); } }
        private string _CommandText = string.Empty;

        [Display(Name = "PreProcess_Arguments", Description = "PreProcess_ArgumentsDesc", GroupName = "PreProcess_ScriptGroup", ResourceType = typeof(Properties.Resources))]
        public string Arguments { get => _Arguments; set { _Arguments = value ?? string.Empty; OnPropertyChanged(); } }
        private string _Arguments = string.Empty;

        [Display(Name = "PreProcess_WorkingDirectory", Description = "PreProcess_WorkingDirectoryDesc", GroupName = "PreProcess_ScriptGroup", ResourceType = typeof(Properties.Resources))]
        [PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
        public string WorkingDirectory { get => _WorkingDirectory; set { _WorkingDirectory = value ?? string.Empty; OnPropertyChanged(); } }
        private string _WorkingDirectory = string.Empty;

        [Display(Name = "PreProcess_PythonExecutable", Description = "PreProcess_PythonExecutableDesc", GroupName = "PreProcess_ScriptGroup", ResourceType = typeof(Properties.Resources))]
        public string PythonExecutable { get => _PythonExecutable; set { _PythonExecutable = value ?? string.Empty; OnPropertyChanged(); } }
        private string _PythonExecutable = "python";

        [Display(Name = "PreProcess_TimeoutMs", Description = "PreProcess_TimeoutMsDesc", GroupName = "PreProcess_ExecutionGroup", ResourceType = typeof(Properties.Resources))]
        public int TimeoutMs { get => _TimeoutMs; set { _TimeoutMs = value; OnPropertyChanged(); } }
        private int _TimeoutMs = 60000;

        [Display(Name = "PreProcess_SuccessExitCodes", Description = "PreProcess_SuccessExitCodesDesc", GroupName = "PreProcess_ExecutionGroup", ResourceType = typeof(Properties.Resources))]
        public string SuccessExitCodes { get => _SuccessExitCodes; set { _SuccessExitCodes = value ?? string.Empty; OnPropertyChanged(); } }
        private string _SuccessExitCodes = "0";

        [Display(Name = "PreProcess_StopFlowOnFailure", Description = "PreProcess_StopFlowOnFailureDesc", GroupName = "PreProcess_ExecutionGroup", ResourceType = typeof(Properties.Resources))]
        public bool StopFlowOnFailure { get => _StopFlowOnFailure; set { _StopFlowOnFailure = value; OnPropertyChanged(); } }
        private bool _StopFlowOnFailure = true;

        [Display(Name = "PreProcess_MaxOutputChars", Description = "PreProcess_MaxOutputCharsDesc", GroupName = "PreProcess_ExecutionGroup", ResourceType = typeof(Properties.Resources))]
        public int MaxOutputChars { get => _MaxOutputChars; set { _MaxOutputChars = Math.Max(0, value); OnPropertyChanged(); } }
        private int _MaxOutputChars = 8000;
    }

    [PreProcess("PreProcess_ScriptName", "PreProcess_ScriptDesc", ResourceType = typeof(Properties.Resources))]
    public class ScriptPreProcess : PreProcessBase<ScriptPreProcessConfig>
    {
        private const string DefaultCmdExecutable = "cmd.exe";
        private const string DefaultPowerShellExecutable = "powershell.exe";
        private static readonly ILog log = LogManager.GetLogger(typeof(ScriptPreProcess));
        private static readonly char[] SuccessExitCodeSeparators = new[] { ',', ';', ' ', '\t', '\r', '\n' };

        public override async Task<bool> PreProcess(IPreProcessContext ctx)
        {
            if (!TryBuildProcessStartInfo(ctx, out ProcessStartInfo startInfo, out string displayCommand, out string errorMessage))
            {
                return HandleFailure(errorMessage);
            }

            log.Info($"ScriptPreProcess: 执行 {displayCommand}");
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                using Process process = new() { StartInfo = startInfo };
                if (!process.Start())
                {
                    return HandleFailure($"ScriptPreProcess: {Properties.Resources.CommandLineScript_StartFailed}");
                }

                Task<string> stdoutTask = ReadCappedAsync(process.StandardOutput, Config.MaxOutputChars);
                Task<string> stderrTask = ReadCappedAsync(process.StandardError, Config.MaxOutputChars);
                bool timedOut = false;

                try
                {
                    if (Config.TimeoutMs > 0)
                    {
                        using CancellationTokenSource cts = new(Config.TimeoutMs);
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

                string stdout = await stdoutTask.ConfigureAwait(false);
                string stderr = await stderrTask.ConfigureAwait(false);
                stopwatch.Stop();

                LogOutput(stdout, stderr);

                if (timedOut)
                {
                    return HandleFailure($"ScriptPreProcess: {string.Format(Properties.Resources.CommandLineScript_Timeout, Config.TimeoutMs)}");
                }

                if (IsSuccessExitCode(process.ExitCode))
                {
                    log.Info($"ScriptPreProcess: 执行完成，ExitCode={process.ExitCode}，耗时 {stopwatch.ElapsedMilliseconds} ms");
                    return true;
                }

                string detail = FirstNonEmptyLine(stderr) ?? FirstNonEmptyLine(stdout) ?? string.Empty;
                string message = string.IsNullOrWhiteSpace(detail)
                    ? $"ScriptPreProcess: {string.Format(Properties.Resources.CommandLineScript_ExecutionFailed, process.ExitCode)}"
                    : $"ScriptPreProcess: {string.Format(Properties.Resources.CommandLineScript_ExecutionFailedWithDetail, process.ExitCode, TrimMessage(detail, 240))}";
                return HandleFailure(message);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return HandleFailure($"ScriptPreProcess: {string.Format(Properties.Resources.CommandLineScript_ExecutionException, ex.Message)}");
            }
        }

        private bool TryBuildProcessStartInfo(IPreProcessContext ctx, out ProcessStartInfo startInfo, out string displayCommand, out string errorMessage)
        {
            startInfo = null!;
            displayCommand = string.Empty;
            errorMessage = string.Empty;

            string workingDirectoryText = ExpandPlaceholders(Config.WorkingDirectory, ctx);
            string workingDirectory = ResolveDirectory(workingDirectoryText);
            string scriptFile = ResolveScriptFile(ExpandPlaceholders(Config.ScriptFile, ctx), workingDirectory);
            string commandText = ExpandPlaceholders(Config.CommandText, ctx);
            string arguments = ExpandPlaceholders(Config.Arguments, ctx);

            if (string.IsNullOrWhiteSpace(scriptFile) && string.IsNullOrWhiteSpace(commandText))
            {
                errorMessage = $"ScriptPreProcess: {Properties.Resources.CommandLineScript_FileAndCommandEmpty}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                workingDirectory = ResolveDefaultWorkingDirectory(scriptFile);
            }

            if (!Directory.Exists(workingDirectory))
            {
                errorMessage = $"ScriptPreProcess: {string.Format(Properties.Resources.CommandLineScript_WorkingDirectoryNotFound, workingDirectory)}";
                return false;
            }

            PreProcessScriptType scriptType = ResolveScriptType(scriptFile);
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

            ApplyProcessCommand(startInfo, scriptType, scriptFile, commandText, arguments);
            displayCommand = BuildDisplayCommand(startInfo);
            return true;
        }

        private void ApplyProcessCommand(ProcessStartInfo startInfo, PreProcessScriptType scriptType, string scriptFile, string commandText, string arguments)
        {
            switch (scriptType)
            {
                case PreProcessScriptType.Python:
                    startInfo.FileName = ResolveExecutable(Config.PythonExecutable, "python");
                    startInfo.Arguments = string.IsNullOrWhiteSpace(scriptFile)
                        ? $"-c {QuoteArgument(commandText)} {arguments}".TrimEnd()
                        : $"{QuoteArgument(scriptFile)} {arguments}".TrimEnd();
                    break;
                case PreProcessScriptType.PowerShell:
                    startInfo.FileName = DefaultPowerShellExecutable;
                    startInfo.Arguments = string.IsNullOrWhiteSpace(scriptFile)
                        ? $"-NoProfile -ExecutionPolicy Bypass -Command {QuoteArgument(commandText)}"
                        : $"-NoProfile -ExecutionPolicy Bypass -File {QuoteArgument(scriptFile)} {arguments}".TrimEnd();
                    break;
                case PreProcessScriptType.Executable:
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
                case PreProcessScriptType.Cmd:
                default:
                    startInfo.FileName = DefaultCmdExecutable;
                    startInfo.Arguments = string.IsNullOrWhiteSpace(scriptFile)
                        ? $"/d /c {commandText}"
                        : BuildCmdScriptArguments(scriptFile, arguments);
                    break;
            }
        }

        private PreProcessScriptType ResolveScriptType(string scriptFile)
        {
            if (Config.ScriptType != PreProcessScriptType.Auto)
            {
                return Config.ScriptType;
            }

            if (string.IsNullOrWhiteSpace(scriptFile))
            {
                return PreProcessScriptType.Cmd;
            }

            string extension = Path.GetExtension(scriptFile).ToLowerInvariant();
            return extension switch
            {
                ".py" or ".pyw" => PreProcessScriptType.Python,
                ".cmd" or ".bat" => PreProcessScriptType.Cmd,
                ".ps1" => PreProcessScriptType.PowerShell,
                _ => PreProcessScriptType.Executable
            };
        }

        private bool IsSuccessExitCode(int exitCode)
        {
            if (string.IsNullOrWhiteSpace(Config.SuccessExitCodes))
            {
                return exitCode == 0;
            }

            string[] parts = Config.SuccessExitCodes.Split(SuccessExitCodeSeparators, StringSplitOptions.RemoveEmptyEntries);
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

        private bool HandleFailure(string message)
        {
            if (Config.StopFlowOnFailure)
            {
                log.Warn(message);
                return false;
            }

            log.Warn($"{message}. The flow is configured to continue after a failure.");
            return true;
        }

        private static void LogOutput(string stdout, string stderr)
        {
            if (!string.IsNullOrWhiteSpace(stdout))
            {
                log.Info($"ScriptPreProcess stdout: {TrimMessage(stdout.Trim(), 2000)}");
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                log.Warn($"ScriptPreProcess stderr: {TrimMessage(stderr.Trim(), 2000)}");
            }
        }

        private static string ExpandPlaceholders(string value, IPreProcessContext ctx)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string flowName = ctx.FlowName ?? string.Empty;
            string serialNumber = ctx.SerialNumber ?? string.Empty;
            return value
                .Replace("{FlowName}", flowName, StringComparison.OrdinalIgnoreCase)
                .Replace("{SerialNumber}", serialNumber, StringComparison.OrdinalIgnoreCase)
                .Replace("{SN}", serialNumber, StringComparison.OrdinalIgnoreCase);
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

        private static bool ValidateScriptFile(PreProcessScriptType scriptType, string scriptFile, string workingDirectory, out string errorMessage)
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

            if (scriptType == PreProcessScriptType.Executable && !LooksLikePath(scriptFile))
            {
                return true;
            }

            errorMessage = $"ScriptPreProcess: {string.Format(Properties.Resources.CommandLineScript_FileNotFound, scriptFile)}";
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
