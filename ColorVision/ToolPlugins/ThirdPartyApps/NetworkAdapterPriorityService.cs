using ColorVision.Common.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ToolPlugins.ThirdPartyApps
{
    internal sealed class NetworkAdapterInfo
    {
        public int InterfaceIndex { get; set; }
        public string InterfaceAlias { get; set; } = string.Empty;
        public string ConnectionState { get; set; } = string.Empty;
        public int InterfaceMetric { get; set; }
        public string AutomaticMetric { get; set; } = string.Empty;
        public string IPv4Address { get; set; } = string.Empty;
        public string DefaultGateway { get; set; } = string.Empty;
        public int? RouteMetric { get; set; }

        public bool IsConnected => ConnectionState.Equals("Connected", StringComparison.OrdinalIgnoreCase);
        public string ConnectionStateText => IsConnected ? "已连接" : "未连接";
        public string AutomaticMetricText => AutomaticMetric.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ? "是" : "否";
        public string RouteMetricText => RouteMetric?.ToString(CultureInfo.InvariantCulture) ?? "-";
        public string EffectiveMetricText => RouteMetric.HasValue
            ? (RouteMetric.Value + InterfaceMetric).ToString(CultureInfo.InvariantCulture)
            : "-";
    }

    internal static class NetworkAdapterPriorityService
    {
        internal const int PreferredMetric = 5;

        private const string ReadAdaptersScript = """
            $ErrorActionPreference = 'Stop'
            $ProgressPreference = 'SilentlyContinue'
            [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

            $configurations = @(Get-NetIPConfiguration -ErrorAction SilentlyContinue)
            $defaultRoutes = @(Get-NetRoute -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue)
            $result = @(Get-NetIPInterface -AddressFamily IPv4 -ErrorAction Stop | ForEach-Object {
                $interface = $_
                $configuration = $configurations | Where-Object InterfaceIndex -eq $interface.InterfaceIndex | Select-Object -First 1
                $defaultRoute = $defaultRoutes |
                    Where-Object InterfaceIndex -eq $interface.InterfaceIndex |
                    Sort-Object RouteMetric |
                    Select-Object -First 1

                [pscustomobject]@{
                    InterfaceIndex = [int]$interface.InterfaceIndex
                    InterfaceAlias = [string]$interface.InterfaceAlias
                    ConnectionState = [string]$interface.ConnectionState
                    InterfaceMetric = [int]$interface.InterfaceMetric
                    AutomaticMetric = [string]$interface.AutomaticMetric
                    IPv4Address = [string](@($configuration.IPv4Address | ForEach-Object IPAddress) -join ', ')
                    DefaultGateway = [string](@($configuration.IPv4DefaultGateway | ForEach-Object NextHop) -join ', ')
                    RouteMetric = if ($null -ne $defaultRoute) { [int]$defaultRoute.RouteMetric } else { $null }
                }
            })

            [Console]::Out.Write((ConvertTo-Json -InputObject $result -Depth 3 -Compress))
            """;

        public static async Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default)
        {
            ProcessResult result = await RunPowerShellAsync(ReadAdaptersScript, requireAdministrator: false, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(CreatePowerShellError("读取 IPv4 网卡失败", result));

            return ParseAdapters(result.Output);
        }

        public static Task SetPreferredAsync(int interfaceIndex, CancellationToken cancellationToken = default)
        {
            return RunMetricCommandAsync(BuildSetPreferredScript(interfaceIndex), "设置网卡优先级失败", cancellationToken);
        }

        public static Task RestoreAutomaticMetricAsync(int interfaceIndex, CancellationToken cancellationToken = default)
        {
            return RunMetricCommandAsync(BuildRestoreAutomaticMetricScript(interfaceIndex), "恢复自动 Metric 失败", cancellationToken);
        }

        internal static IReadOnlyList<NetworkAdapterInfo> ParseAdapters(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<NetworkAdapterInfo>();

            List<NetworkAdapterInfo>? adapters = JsonSerializer.Deserialize<List<NetworkAdapterInfo>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return (adapters ?? new List<NetworkAdapterInfo>())
                .OrderBy(adapter => adapter.IsConnected ? 0 : 1)
                .ThenBy(adapter => adapter.RouteMetric.HasValue ? 0 : 1)
                .ThenBy(adapter => adapter.RouteMetric.HasValue ? adapter.RouteMetric.Value + adapter.InterfaceMetric : int.MaxValue)
                .ThenBy(adapter => adapter.InterfaceMetric)
                .ThenBy(adapter => adapter.InterfaceAlias, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        internal static string BuildSetPreferredScript(int interfaceIndex)
        {
            ValidateInterfaceIndex(interfaceIndex);
            return $$"""
                $ErrorActionPreference = 'Stop'
                Set-NetIPInterface -AddressFamily IPv4 -InterfaceIndex {{interfaceIndex}} -AutomaticMetric Disabled -InterfaceMetric {{PreferredMetric}} -ErrorAction Stop
                """;
        }

        internal static string BuildRestoreAutomaticMetricScript(int interfaceIndex)
        {
            ValidateInterfaceIndex(interfaceIndex);
            return $$"""
                $ErrorActionPreference = 'Stop'
                Set-NetIPInterface -AddressFamily IPv4 -InterfaceIndex {{interfaceIndex}} -AutomaticMetric Enabled -ErrorAction Stop
                """;
        }

        private static async Task RunMetricCommandAsync(string script, string errorPrefix, CancellationToken cancellationToken)
        {
            ProcessResult result = await RunPowerShellAsync(script, requireAdministrator: true, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(CreatePowerShellError(errorPrefix, result));
        }

        private static async Task<ProcessResult> RunPowerShellAsync(
            string script,
            bool requireAdministrator,
            CancellationToken cancellationToken)
        {
            bool needsElevation = requireAdministrator && !Tool.IsAdministrator();
            string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            ProcessStartInfo startInfo = new()
            {
                FileName = GetPowerShellPath(),
                Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}",
                UseShellExecute = needsElevation,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = !needsElevation,
                RedirectStandardError = !needsElevation,
            };

            if (needsElevation)
            {
                startInfo.Verb = "runas";
            }
            else
            {
                startInfo.StandardOutputEncoding = Encoding.UTF8;
                startInfo.StandardErrorEncoding = Encoding.UTF8;
            }

            try
            {
                using Process? process = Process.Start(startInfo);
                if (process == null)
                    throw new InvalidOperationException("无法启动 Windows PowerShell。");

                if (needsElevation)
                {
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    return new ProcessResult(process.ExitCode, string.Empty, string.Empty);
                }

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                return new ProcessResult(
                    process.ExitCode,
                    await outputTask.ConfigureAwait(false),
                    await errorTask.ConfigureAwait(false));
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                throw new OperationCanceledException("已取消管理员授权。", ex, cancellationToken);
            }
        }

        private static string GetPowerShellPath()
        {
            string systemPowerShell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            return File.Exists(systemPowerShell) ? systemPowerShell : "powershell.exe";
        }

        private static string CreatePowerShellError(string prefix, ProcessResult result)
        {
            string detail = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
            return string.IsNullOrWhiteSpace(detail)
                ? $"{prefix}（PowerShell 退出代码 {result.ExitCode}）。"
                : $"{prefix}：{detail.Trim()}";
        }

        private static void ValidateInterfaceIndex(int interfaceIndex)
        {
            if (interfaceIndex <= 0)
                throw new ArgumentOutOfRangeException(nameof(interfaceIndex));
        }

        private readonly record struct ProcessResult(int ExitCode, string Output, string Error);
    }
}
