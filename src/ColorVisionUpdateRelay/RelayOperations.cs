using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ColorVisionUpdateRelay
{
    public sealed class PortProxyEntry
    {
        public string ListenAddress;
        public int ListenPort;
        public string ConnectAddress;
        public int ConnectPort;
        public override string ToString() => ListenAddress + ":" + ListenPort + " → " + ConnectAddress + ":" + ConnectPort;
    }

    /// <summary>
    /// Windows-side work for both roles. The client role maps the update host to the relay in hosts;
    /// the relay role forwards the same port to the real server with the built-in portproxy (IP Helper service).
    /// </summary>
    public static class RelayOperations
    {
        public const string FirewallRuleName = "ColorVision Update Relay";
        private const string IpHelperService = "iphlpsvc";
        private const string PortProxyKey = @"SYSTEM\CurrentControlSet\Services\PortProxy\v4tov4\tcp";

        public static List<string> GetLocalIPv4()
        {
            var result = new List<string>();
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback || nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                foreach (UnicastIPAddressInformation address in nic.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string ip = address.Address.ToString();
                    if (ip.StartsWith("169.254.", StringComparison.Ordinal)) continue;
                    result.Add(ip + "    " + nic.Name);
                }
            }
            return result;
        }

        public static bool IsValidRelayAddress(string text)
        {
            return IPAddress.TryParse(text, out IPAddress ip) && ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip) && !ip.Equals(IPAddress.Any)
                && text.Count(c => c == '.') == 3;
        }

        // ---------- relay role ----------

        public static List<PortProxyEntry> GetPortProxies()
        {
            var result = new List<PortProxyEntry>();
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(PortProxyKey))
            {
                if (key == null) return result;
                foreach (string name in key.GetValueNames())
                {
                    string value = key.GetValue(name) as string;
                    if (!TrySplitEndpoint(name, out string listenAddress, out int listenPort) || !TrySplitEndpoint(value, out string connectAddress, out int connectPort)) continue;
                    result.Add(new PortProxyEntry { ListenAddress = listenAddress, ListenPort = listenPort, ConnectAddress = connectAddress, ConnectPort = connectPort });
                }
            }
            return result;
        }

        public static PortProxyEntry FindForward(int port) => GetPortProxies().FirstOrDefault(e => e.ListenPort == port);

        public static bool FirewallRuleExists() => RunTool("netsh.exe", "advfirewall firewall show rule name=\"" + FirewallRuleName + "\"", null) == 0;

        public static string DescribeIpHelper()
        {
            try
            {
                using (var service = new ServiceController(IpHelperService))
                    return (service.Status == ServiceControllerStatus.Running ? "运行中" : "未运行（" + service.Status + "）") + "，启动类型 " + service.StartType;
            }
            catch (Exception ex) { return "无法读取：" + ex.Message; }
        }

        public static void EnableForward(string serverHost, int port, Action<string> log)
        {
            log("确保 IP Helper 服务（端口转发依赖它）自动启动并运行……");
            RunTool("sc.exe", "config " + IpHelperService + " start= auto", log);
            using (var service = new ServiceController(IpHelperService))
            {
                if (service.Status != ServiceControllerStatus.Running)
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
                }
            }

            PortProxyEntry existing = FindForward(port);
            if (existing != null)
            {
                log("先删除原有端口转发：" + existing);
                RunTool("netsh.exe", "interface portproxy delete v4tov4 listenport=" + port + " listenaddress=" + existing.ListenAddress, log);
            }
            log("添加端口转发 0.0.0.0:" + port + " → " + serverHost + ":" + port);
            Require(RunTool("netsh.exe", "interface portproxy add v4tov4 listenaddress=0.0.0.0 listenport=" + port + " connectaddress=" + serverHost + " connectport=" + port, log), "添加端口转发");

            log("放行入站防火墙 TCP " + port);
            RunTool("netsh.exe", "advfirewall firewall delete rule name=\"" + FirewallRuleName + "\"", null);
            Require(RunTool("netsh.exe", "advfirewall firewall add rule name=\"" + FirewallRuleName + "\" dir=in action=allow protocol=TCP localport=" + port + " profile=any", log), "添加防火墙规则");
        }

        public static void DisableForward(int port, Action<string> log)
        {
            PortProxyEntry existing = FindForward(port);
            if (existing == null) log("没有找到端口 " + port + " 的转发。");
            else
            {
                log("删除端口转发：" + existing);
                Require(RunTool("netsh.exe", "interface portproxy delete v4tov4 listenport=" + port + " listenaddress=" + existing.ListenAddress, log), "删除端口转发");
            }
            if (FirewallRuleExists())
            {
                log("删除防火墙规则 " + FirewallRuleName);
                RunTool("netsh.exe", "advfirewall firewall delete rule name=\"" + FirewallRuleName + "\"", log);
            }
            log("IP Helper 服务保持原状（其他功能也可能依赖它）。");
        }

        /// <summary>A relay whose own hosts points the update host at a relay would forward to itself.</summary>
        public static string CheckRelayLoop(string serverHost)
        {
            List<string> lines = HostsEditor.Read(HostsEditor.HostsPath, out Encoding _);
            string mapped = HostsEditor.FindRelay(lines, serverHost);
            if (mapped != null) return "本机 hosts 已把 " + serverHost + " 指向 " + mapped + "（客户机设置）。跳板机必须直连服务器，请先在“客户机”页移除设置。";
            if (lines.Any(l => HostsEditor.MapsHost(l, serverHost))) return "本机 hosts 中有手动添加的 " + serverHost + " 映射，跳板机必须直连服务器，请先删除该行。";
            return null;
        }

        // ---------- client role ----------

        public static string GetClientRelay(string serverHost) => HostsEditor.FindRelay(HostsEditor.Read(HostsEditor.HostsPath, out Encoding _), serverHost);

        public static void SetClientRelay(string serverHost, string relayAddress, Action<string> log)
        {
            string path = HostsEditor.HostsPath;
            List<string> lines = HostsEditor.Read(path, out Encoding encoding);
            int conflicts = HostsEditor.Remove(lines, serverHost).Count(l => HostsEditor.MapsHost(l, serverHost));
            if (conflicts > 0) log("hosts 中已有 " + conflicts + " 行手动映射 " + serverHost + "，已注释停用，移除设置时会恢复。");
            string backup = HostsEditor.Write(path, HostsEditor.Apply(lines, serverHost, relayAddress), encoding);
            log("已写入 hosts：" + relayAddress + "  " + serverHost + "（原文件备份：" + backup + "）");
            FlushDns(log);
        }

        public static void RemoveClientRelay(string serverHost, Action<string> log)
        {
            string path = HostsEditor.HostsPath;
            List<string> lines = HostsEditor.Read(path, out Encoding encoding);
            List<string> updated = HostsEditor.Remove(lines, serverHost);
            if (updated.SequenceEqual(lines)) { log("hosts 中没有本工具写入的 " + serverHost + " 设置。"); return; }
            string backup = HostsEditor.Write(path, updated, encoding);
            log("已从 hosts 移除跳板设置（原文件备份：" + backup + "）");
            FlushDns(log);
        }

        private static void FlushDns(Action<string> log) => RunTool("ipconfig.exe", "/flushdns", log);

        // ---------- checks ----------

        public static bool CanConnect(string host, int port, Action<string> log)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    IAsyncResult pending = client.BeginConnect(host, port, null, null);
                    if (!pending.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5))) { log("连接 " + host + ":" + port + " 超时。"); return false; }
                    client.EndConnect(pending);
                    log("可以连接 " + host + ":" + port + "。");
                    return true;
                }
            }
            catch (Exception ex) { log("无法连接 " + host + ":" + port + "：" + ex.Message); return false; }
        }

        public static string DescribeResolution(string host)
        {
            try { return string.Join(", ", Dns.GetHostAddresses(host).Select(a => a.ToString())); }
            catch (Exception ex) { return "解析失败：" + ex.Message; }
        }

        /// <summary>
        /// Queries the same latest-version API the application uses, without the system proxy (the application's default),
        /// sending the real server name as Host even when connecting to an address.
        /// </summary>
        public static bool QueryLatestVersion(string connectHost, string serverHost, int port, Action<string> log)
        {
            string url = "http://" + connectHost + ":" + port + "/api/app/latest-version";
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Proxy = null;
                request.Host = serverHost + ":" + port;
                request.KeepAlive = false;
                request.Timeout = 10000;
                request.ReadWriteTimeout = 10000;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    string body = reader.ReadToEnd().Trim();
                    Match match = Regex.Match(body, "\"version\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    string version = match.Success ? match.Groups[1].Value : (body.Length > 80 ? body.Substring(0, 80) + "…" : body);
                    log("更新服务器响应正常，最新版本：" + version);
                    return true;
                }
            }
            catch (Exception ex) { log("访问 " + url + " 失败：" + ex.Message); return false; }
        }

        // ---------- helpers ----------

        private static bool TrySplitEndpoint(string text, out string address, out int port)
        {
            address = null; port = 0;
            if (string.IsNullOrEmpty(text)) return false;
            int slash = text.LastIndexOf('/');
            if (slash <= 0 || !int.TryParse(text.Substring(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out port)) return false;
            address = text.Substring(0, slash);
            return true;
        }

        private static void Require(int exitCode, string action)
        {
            if (exitCode != 0) throw new InvalidOperationException(action + "失败（退出码 " + exitCode + "），详见上方输出。");
        }

        private static int RunTool(string fileName, string arguments, Action<string> log)
        {
            var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, fileName), arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Encoding console = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
            info.StandardOutputEncoding = console;
            info.StandardErrorEncoding = console;
            using (Process process = Process.Start(info))
            {
                var error = process.StandardError.ReadToEndAsync();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (log != null)
                {
                    foreach (string line in (output + "\n" + error.Result).Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0))
                        log("  " + line);
                }
                return process.ExitCode;
            }
        }
    }
}
