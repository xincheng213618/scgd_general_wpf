using ColorVision.Experimental.CVLicenseHook;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ColorVision.Engine.Diagnostics;

/// <summary>
/// 对 dev / x64 两个服务进程同时接管 licenseClientDLL.dll!LicenseValidate。
///
/// 只保留这两个目标（不再包含 ColorVisionServiceHost），一次「一键放行」把两者都打上补丁，
/// 策略恒为 TRUE（放行），不再提供任何会造成拒绝 / 限制测量的开关。
/// Hook 实例由静态表持有，窗口关闭后放行状态仍然保留；服务进程重启后自动失效。
/// </summary>
public static class ServiceLicenseBypass
{
    /// <summary>只处理 dev / x64；与跨进程引擎共用同一份清单，顺序与诊断窗口卡片一致。</summary>
    public static IReadOnlyList<string> TargetNames => RemoteLicenseHook.TargetProcessNames;

    public sealed class ProcessState
    {
        public int Id { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string Bypass { get; set; } = "未放行";
        public long Hits { get; set; }
    }

    public sealed class TargetState
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<ProcessState> Instances { get; } = new();
        public string Status { get; set; } = "检查中";
        public string BypassStatus { get; set; } = "未放行";
        public string DiagnosticsText { get; set; } = "尚未执行只读诊断，点「重新诊断」可刷新。";
        public string EmptyDetails { get; set; } = string.Empty;

        public bool IsRunning => Instances.Count != 0;
        public long Hits => Instances.Sum(x => x.Hits);
        public string DetailTitle => Label + " 进程详情";
    }

    private sealed class Entry
    {
        public string Key = string.Empty;
        public string Name = string.Empty;
        public int Pid;
        public RemoteLicenseHook? Hook;
        public RemoteModuleDiagnostics? Diagnostics;
    }

    private static readonly object Gate = new object();
    private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

    // ------------------------------------------------------------------
    // 环境
    // ------------------------------------------------------------------

    public static bool IsElevated => RemoteLicenseHook.IsElevated;

    public static bool ExperimentalEnabled
    {
        get
        {
            try { return ExperimentalFeaturesConfig.Instance.IsEnabled; }
            catch { return false; }
        }
    }

    public static bool TryEnablePrivilege(out string message)
        => RemoteLicenseHook.TryEnableSeDebugPrivilege(out message);

    public static string ProbeAccess(int pid) => RemoteLicenseHook.ProbeAccess(pid);

    public static bool HasAnyInstalled
    {
        get { lock (Gate) return Entries.Values.Any(x => x.Hook?.IsInstalled == true); }
    }

    public static long TotalHits
    {
        get
        {
            lock (Gate)
            {
                long total = 0;
                foreach (Entry e in Entries.Values)
                {
                    if (e.Hook?.IsInstalled != true) continue;
                    try { total += e.Hook.GetHits(); } catch { }
                }
                return total;
            }
        }
    }

    // ------------------------------------------------------------------
    // 只读诊断 / 一键放行 / 恢复
    // ------------------------------------------------------------------

    /// <summary>对两个服务的全部实例做只读 PE 诊断，结果缓存到静态表。</summary>
    public static IReadOnlyList<string> InspectAll()
    {
        List<string> log = new List<string>();
        foreach (string name in TargetNames)
        {
            foreach (int pid in FindPids(name))
            {
                string key = Key(name, pid);
                try
                {
                    RemoteModuleDiagnostics d = RemoteLicenseHook.Inspect(pid);
                    lock (Gate)
                    {
                        Entry e = GetOrAdd(key, name, pid);
                        e.Diagnostics = d;
                    }
                    log.Add($"{Label(name)} (PID {pid}) 只读诊断：{d.Status}"
                            + (d.CanInstall ? "；入口特征匹配，可以放行。" : "；不可放行。"));
                }
                catch (Exception ex)
                {
                    log.Add($"{Label(name)} (PID {pid}) 诊断失败：{ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        if (log.Count == 0) log.Add("未发现 dev / x64 服务进程，无法执行诊断。");
        return log;
    }

    /// <summary>
    /// 一键放行：对 dev 与 x64 两个服务的全部实例同时安装 hook，策略恒为 TRUE。
    /// 单个进程失败不影响其它进程；已安装的先恢复再重装，保证状态干净。
    /// </summary>
    public static IReadOnlyList<string> InstallAll()
    {
        List<string> log = new List<string>();

        if (!IsElevated)
        {
            log.Add("已拒绝对话：主程序不是管理员，无法打开 SYSTEM 服务进程。请以管理员身份重启 ColorVision。");
            return log;
        }
        if (!ExperimentalEnabled)
        {
            log.Add("已拒绝对话：实验功能开关未开启。");
            return log;
        }
        TryEnablePrivilege(out string privilege);
        log.Add("权限：" + privilege);

        foreach (string name in TargetNames)
        {
            int[] pids = FindPids(name);
            if (pids.Length == 0)
            {
                log.Add($"{Label(name)}：未运行，跳过。");
                continue;
            }

            foreach (int pid in pids)
            {
                string key = Key(name, pid);
                try
                {
                    RemoteModuleDiagnostics d = RemoteLicenseHook.Inspect(pid);
                    if (!d.CanInstall)
                    {
                        log.Add($"{Label(name)} (PID {pid}) 放行被拒绝：{d.Status}");
                        lock (Gate) { GetOrAdd(key, name, pid).Diagnostics = d; }
                        continue;
                    }

                    lock (Gate)
                    {
                        Entry e = GetOrAdd(key, name, pid);
                        if (e.Hook != null)
                        {
                            try { e.Hook.Uninstall(); } catch { }
                            try { e.Hook.Dispose(); } catch { }
                            e.Hook = null;
                        }
                        e.Diagnostics = d;
                    }

                    RemoteLicenseHook hook = RemoteLicenseHook.Install(pid);
                    hook.SetPolicy(RemoteLicenseHook.CV_ERR_SUCCESS);   // TRUE：默认放行，无拒绝策略
                    lock (Gate) { GetOrAdd(key, name, pid).Hook = hook; }

                    log.Add($"{Label(name)} (PID {pid}) 已放行：LicenseValidate VA 0x{hook.ValiLicAddress.ToInt64():X}"
                            + "，返回值 1 (TRUE)。");
                }
                catch (Exception ex)
                {
                    log.Add($"{Label(name)} (PID {pid}) 放行失败：{ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        int installed;
        lock (Gate) installed = Entries.Values.Count(x => x.Hook?.IsInstalled == true);
        log.Add(installed > 0
            ? $"一键放行完成：{installed} 个进程已生效。心跳命中数持续增长即为证据；测量请求不再被 Unauthorized/401 拦截。"
            : "一键放行未产生任何生效实例，请查看上面的失败原因。");
        return log;
    }

    /// <summary>恢复两个进程 LicenseValidate 的原始 17 字节。</summary>
    public static IReadOnlyList<string> RestoreAll()
    {
        List<string> log = new List<string>();
        List<Entry> snapshot;
        lock (Gate) snapshot = Entries.Values.ToList();

        foreach (Entry e in snapshot)
        {
            if (e.Hook == null) continue;
            try
            {
                bool clean = e.Hook.Uninstall();
                log.Add(clean
                    ? $"{Label(e.Name)} (PID {e.Pid}) 已恢复 LicenseValidate 原始 17 字节。"
                    : $"{Label(e.Name)} (PID {e.Pid}) 恢复未干净完成：已保留远程 stub 以避免目标进程跳到已释放地址，请重启服务彻底清除。");
            }
            catch (Exception ex)
            {
                log.Add($"{Label(e.Name)} (PID {e.Pid}) 恢复失败：{ex.Message}");
            }
            finally
            {
                try { e.Hook!.Dispose(); } catch { }
                e.Hook = null;
            }
        }

        lock (Gate) Entries.Clear();
        if (log.Count == 0) log.Add("当前没有已安装的放行补丁。");
        else log.Add("已恢复原校验；服务重新按自身许可证逻辑判定。");
        return log;
    }

    /// <summary>
    /// 每秒调用：检测目标进程退出并清理；确保策略始终为 TRUE（避免任何外部改动造成拒绝）。
    /// 只返回需要写日志的事件，正常同步不产生日志。
    /// </summary>
    public static IReadOnlyList<string> Tick()
    {
        List<string> log = new List<string>();
        List<Entry> snapshot;
        lock (Gate) snapshot = Entries.Values.ToList();

        foreach (Entry e in snapshot)
        {
            if (e.Hook == null) continue;
            bool alive;
            try { using Process p = Process.GetProcessById(e.Pid); alive = !p.HasExited; }
            catch { alive = false; }

            if (!alive)
            {
                log.Add($"{Label(e.Name)} (PID {e.Pid}) 已退出，放行状态自动清除；服务重启后需重新点「一键放行」。");
                try { e.Hook.Dispose(); } catch { }
                e.Hook = null;
                lock (Gate) Entries.Remove(e.Key);
                continue;
            }

            if (e.Hook.IsInstalled)
            {
                try
                {
                    if (e.Hook.GetPolicy() != RemoteLicenseHook.CV_ERR_SUCCESS)
                    {
                        e.Hook.SetPolicy(RemoteLicenseHook.CV_ERR_SUCCESS);
                        log.Add($"{Label(e.Name)} (PID {e.Pid}) 策略被外部改动，已重新同步为默认放行。");
                    }
                }
                catch (Exception ex)
                {
                    log.Add($"{Label(e.Name)} (PID {e.Pid}) 运行时同步失败：{ex.Message}");
                }
            }
        }
        return log;
    }

    // ------------------------------------------------------------------
    // UI 数据
    // ------------------------------------------------------------------

    /// <summary>轻量快照：不读 PE，只读进程列表与已缓存的诊断 / hook 状态。</summary>
    public static IReadOnlyList<TargetState> Snapshot()
    {
        List<TargetState> states = new List<TargetState>();
        List<Entry> entries;
        lock (Gate) entries = Entries.Values.ToList();

        foreach (string name in TargetNames)
        {
            TargetState state = new TargetState { Name = name, Label = Label(name) };
            int installed = 0;

            foreach (int pid in FindPids(name))
            {
                Entry? e = entries.FirstOrDefault(x => x.Key == Key(name, pid));
                RemoteLicenseHook? hook = e?.Hook;
                bool on = hook?.IsInstalled == true;
                long hits = 0;
                if (on) { try { hits = hook!.GetHits(); } catch { } }
                if (on) installed++;

                state.Instances.Add(new ProcessState
                {
                    Id = pid,
                    StartTime = ReadStartTime(pid),
                    Bypass = on ? "放行中" : "未放行",
                    Hits = hits
                });
            }

            state.Status = state.Instances.Count == 0
                ? "未运行"
                : state.Instances.Count == 1 ? "运行中" : $"运行中 · {state.Instances.Count} 个";
            state.EmptyDetails = state.Instances.Count == 0
                ? "没有找到运行中的此版本服务。"
                : string.Empty;
            state.BypassStatus = installed > 0 ? $"放行中 · {installed}/{state.Instances.Count} 实例" : "未放行";

            RemoteModuleDiagnostics? d = entries.FirstOrDefault(x => x.Name == name)?.Diagnostics;
            if (d != null) state.DiagnosticsText = RenderDiagnostics(d);
            states.Add(state);
        }
        return states;
    }

    public static string RenderDiagnostics(RemoteModuleDiagnostics d)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"进程        : {d.ProcessName} (PID {d.Pid})");
        if (string.IsNullOrEmpty(d.ModulePath))
        {
            sb.AppendLine();
            sb.AppendLine(d.Status);
            return sb.ToString();
        }
        sb.AppendLine($"架构        : {(d.Is64Bit ? "x64" : "x86 (不适用)")}");
        sb.AppendLine($"模块        : {d.ModulePath}");
        sb.AppendLine($"模块基址    : 0x{d.ModuleBase.ToInt64():X}");
        sb.AppendLine($"文件大小    : {d.FileSizeBytes / 1024} KB    映像大小: 0x{d.ModuleSize:X}");
        sb.AppendLine($"PE 构建时间 : {d.BuildTimestampUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"命名导出数  : {d.NamedExportCount}");
        sb.AppendLine($"LicenseValidate: ordinal {d.ValiLicOrdinal}   RVA 0x{d.ValiLicRva:X}   VA 0x{d.ValiLicAddress.ToInt64():X}");
        sb.AppendLine($"入口 17 字节: {d.EntryHex}");
        sb.AppendLine($"特征匹配    : {(d.EntryMatches ? "是" : "否")}");
        sb.AppendLine();
        sb.AppendLine($"状态        : {d.Status}");
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // 内部工具
    // ------------------------------------------------------------------

    private static string Label(string name)
        => name.EndsWith("_dev", StringComparison.OrdinalIgnoreCase) ? "dev" : "x64";

    private static string Key(string name, int pid) => name + "#" + pid;

    private static Entry GetOrAdd(string key, string name, int pid)
    {
        if (!Entries.TryGetValue(key, out Entry? e))
        {
            e = new Entry { Key = key, Name = name, Pid = pid };
            Entries[key] = e;
        }
        return e;
    }

    private static int[] FindPids(string name)
    {
        try
        {
            return Process.GetProcessesByName(name).Select(p =>
            {
                int id = p.Id;
                p.Dispose();
                return id;
            }).OrderBy(x => x).ToArray();
        }
        catch { return Array.Empty<int>(); }
    }

    private static string ReadStartTime(int pid)
    {
        try
        {
            using Process p = Process.GetProcessById(pid);
            return p.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch (System.ComponentModel.Win32Exception) { return "不可读取"; }
        catch (InvalidOperationException) { return "进程已退出"; }
        catch { return "不可读取"; }
    }
}
