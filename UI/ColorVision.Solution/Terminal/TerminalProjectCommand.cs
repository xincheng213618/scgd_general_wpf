using System.IO;
using System.IO.Compression;
using System.Text;

namespace ColorVision.Solution.Terminal
{
    internal static class TerminalProjectCommand
    {
        private const string CmdOptions = "/D /S /V:OFF /C ";
        private const int CmdMaxCommandLineLength = 8191;
        private static readonly char[] Newlines = ['\r', '\n'];
        private static readonly string CmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        private static readonly string PowerShellPath = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

        public static bool CanRun(string shell, string command, string workingDirectory) =>
            !string.IsNullOrWhiteSpace(command) &&
            !string.IsNullOrWhiteSpace(workingDirectory) &&
            !command.Contains('\0') && !workingDirectory.Contains('\0') &&
            Path.IsPathFullyQualified(workingDirectory) &&
            (shell != "cmd" || IsSupportedCmdCommand(command, workingDirectory));

        private static bool IsSupportedCmdCommand(string command, string workingDirectory) =>
            command.Length <= CmdMaxCommandLineLength - CmdPath.Length - CmdOptions.Length - 5 &&
            command.IndexOfAny(Newlines) < 0 &&
            Path.GetPathRoot(workingDirectory) is { Length: 3 } root && root[1] == ':';

        public static string CreateCommandLine(string shell, string command)
        {
            var (executable, arguments) = GetInvocation(shell, command);
            return $"\"{executable}\" {arguments}";
        }

        public static string CreateBatchCommandLine(string shell, IReadOnlyList<TerminalCommandRequest> commands)
        {
            var script = new StringBuilder("$ErrorActionPreference = 'Stop'\n");
            script.AppendLine("$info = New-Object System.Diagnostics.ProcessStartInfo");
            script.AppendLine("$info.UseShellExecute = $false");
            foreach (var command in commands)
            {
                var (executable, arguments) = GetInvocation(shell, command.Command);
                script.AppendLine($"Write-Host '==== {Literal(command.DisplayName)} ===='");
                script.AppendLine($"$info.FileName = '{Literal(executable)}'");
                script.AppendLine($"$info.Arguments = '{Literal(arguments)}'");
                script.AppendLine($"$info.WorkingDirectory = '{Literal(command.WorkingDirectory)}'");
                script.AppendLine("$process = [System.Diagnostics.Process]::Start($info)");
                script.AppendLine("try { $process.WaitForExit(); if ($process.ExitCode -ne 0) { exit $process.ExitCode } } finally { $process.Dispose() }");
            }

            script.AppendLine("exit 0");
            return $"\"{PowerShellPath}\" {EncodePowerShell(script.ToString())}";
        }

        private static (string Executable, string Arguments) GetInvocation(string shell, string command)
        {
            if (shell == "cmd")
                return (CmdPath, $"{CmdOptions}\"{command}\"");

            // Capture $? inside the same script as the command, before running any other
            // statement. Windows PowerShell can reset it across a script block boundary.
            string script = "$ErrorActionPreference = 'Stop'\nSet-Location -LiteralPath ([Environment]::CurrentDirectory)\n" +
                "$global:LASTEXITCODE = 0\n[int[]]$cvExitCode = @(0)\n& {\n" + command + "\n" +
                "if (-not $?) { $cvExitCode[0] = if ($LASTEXITCODE -ne 0) { $LASTEXITCODE } else { 1 } }\n" +
                "} | Out-Default\nexit $cvExitCode[0]";
            return (PowerShellPath, EncodePowerShell(script));
        }

        private static string EncodePowerShell(string script)
        {
            byte[] scriptBytes = Encoding.Unicode.GetBytes(script);
            string encoded = Convert.ToBase64String(scriptBytes);
            if (encoded.Length <= 30_000)
                return "-NoLogo -NoProfile -EncodedCommand " + encoded;

            // Multi-project plans repeat the same runner. Keep the launch compact enough
            // for CreateProcessW without needing an on-disk command payload.
            using var data = new MemoryStream();
            using (var gzip = new GZipStream(data, CompressionLevel.Fastest, leaveOpen: true))
                gzip.Write(scriptBytes);

            string payload = Convert.ToBase64String(data.ToArray());
            return "-NoLogo -NoProfile -Command \"" +
                $"$data = [System.IO.MemoryStream]::new([Convert]::FromBase64String('{payload}')); " +
                "$gzip = [System.IO.Compression.GZipStream]::new($data, [System.IO.Compression.CompressionMode]::Decompress); " +
                "$reader = [System.IO.StreamReader]::new($gzip, [Text.Encoding]::Unicode); " +
                "try { $script = [ScriptBlock]::Create($reader.ReadToEnd()) } finally { $reader.Dispose() }; & $script\"";
        }

        private static string Literal(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    }
}
