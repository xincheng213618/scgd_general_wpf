using ColorVision.Common.Utilities;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Windows;

namespace ColorVision.ToolPlugins.UpdateRelay;

internal static class UpdateRelayForwarding
{
    internal const string StartupArgument = "--update-relay";
    private const string PortProxyKey = @"SYSTEM\CurrentControlSet\Services\PortProxy\v4tov4\tcp";
    private const string ListenAddress = "0.0.0.0";
    private const string IpHelperService = "iphlpsvc";
    private const string FirewallRule = "name=ColorVision Update Relay";
    private static string Port => UpdateRelayConfiguration.DefaultPort.ToString(CultureInfo.InvariantCulture);

    internal static int RunCommand(string[] args)
    {
        if (args.Length != 2 || args[1] is not ("enable" or "disable"))
            return 2;

        bool enable = args[1] == "enable";
        try
        {
            Configure(enable);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, DisplayText.Get(enable ? "开启更新转发失败" : "移除更新转发失败"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return 1;
        }
    }

    internal static bool IsConfigured() => FindForward(rejectConflicts: false);

    internal static void Configure(bool enable)
    {
        if (!Tool.IsAdministrator())
            throw new InvalidOperationException("配置更新转发需要 Windows 管理员权限。");

        if (!enable)
        {
            if (FindForward(rejectConflicts: true)) RemoveForward();
            RemoveFirewallRule();
            return;
        }

        string host = UpdateRelayConfiguration.OfficialHost;
        string hostsPath = Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts");
        if (File.Exists(hostsPath) && File.ReadLines(hostsPath).Any(line => MapsHost(line, host)))
            throw new InvalidOperationException($"本机 hosts 已配置 {host} 的映射。请先移除该映射，使跳板机直连更新服务器。");

        FindForward(rejectConflicts: true);
        RequireSuccess("配置 IP Helper 服务", RunTool("sc.exe", "config", IpHelperService, "start=", "auto"));
        using (var service = new ServiceController(IpHelperService))
        {
            if (service.Status != ServiceControllerStatus.Running)
            {
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
            }
        }

        bool addedForward = !FindForward(rejectConflicts: true);
        if (addedForward)
        {
            RequireSuccess("添加端口转发", RunTool("netsh.exe", "interface", "portproxy", "add", "v4tov4",
                $"listenaddress={ListenAddress}", $"listenport={Port}", $"connectaddress={host}", $"connectport={Port}"));
        }

        try
        {
            RemoveFirewallRule();
            RequireSuccess("添加防火墙规则", RunTool("netsh.exe", "advfirewall", "firewall", "add", "rule",
                FirewallRule, "dir=in", "action=allow", "protocol=TCP", $"localport={Port}", "profile=any"));
        }
        catch (Exception ex)
        {
            if (addedForward && IsConfigured())
            {
                try { RemoveForward(); }
                catch (Exception rollbackError)
                {
                    throw new InvalidOperationException($"{ex.Message}{Environment.NewLine}无法还原端口转发：{rollbackError.Message}", ex);
                }
            }
            throw;
        }
    }

    private static bool MapsHost(string line, string host)
    {
        int comment = line.IndexOf('#');
        string active = comment < 0 ? line : line[..comment];
        return active.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).Skip(1).Contains(host, StringComparer.OrdinalIgnoreCase);
    }

    private static bool FindForward(bool rejectConflicts)
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(PortProxyKey);
        if (key == null) return false;

        string listen = $"{ListenAddress}/{Port}";
        string target = $"{UpdateRelayConfiguration.OfficialHost}/{Port}";
        bool configured = false;
        foreach (string name in key.GetValueNames())
        {
            if (!name.EndsWith($"/{Port}", StringComparison.Ordinal)) continue;

            string? value = key.GetValue(name) as string;
            if (name == listen && string.Equals(value, target, StringComparison.OrdinalIgnoreCase))
                configured = true;
            else if (rejectConflicts)
                throw new InvalidOperationException($"端口 {Port} 有其他转发规则：{name} → {value}。请由管理员确认原规则后再操作。");
        }
        return configured;
    }

    private static void RemoveForward() => RequireSuccess("删除端口转发", RunTool("netsh.exe", "interface", "portproxy",
        "delete", "v4tov4", $"listenport={Port}", $"listenaddress={ListenAddress}"));

    private static void RemoveFirewallRule()
    {
        if (RunTool("netsh.exe", "advfirewall", "firewall", "show", "rule", FirewallRule).ExitCode == 0)
            RequireSuccess("删除防火墙规则", RunTool("netsh.exe", "advfirewall", "firewall", "delete", "rule", FirewallRule));
    }

    private static void RequireSuccess(string action, (int ExitCode, string Output) result)
    {
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{action}失败（退出码 {result.ExitCode}）。{Environment.NewLine}{result.Output}");
    }

    private static (int ExitCode, string Output) RunTool(string fileName, params string[] arguments)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding encoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, fileName))
        {
            WorkingDirectory = Environment.SystemDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = encoding,
            StandardErrorEncoding = encoding
        };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);

        using Process process = Process.Start(info) ?? throw new InvalidOperationException($"无法启动 {fileName}。");
        var error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, (output + Environment.NewLine + error.GetAwaiter().GetResult()).Trim());
    }
}
