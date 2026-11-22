using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.Engine.Diagnostics;

/// <summary>
/// 服务进程诊断 + 许可证统一放行（实验功能）。
///
/// 只处理 dev / x64 两个服务；打开窗口即自动诊断；点「一键放行」一次对两个进程同时生效。
/// 默认放行且没有拒绝 / 限制测量的策略选项；关闭窗口后放行状态由静态管理器保留，
/// 随时可以回来点「恢复原校验」。服务进程重启后内存补丁自动消失。
/// </summary>
public partial class ServiceProcessWindow : Window
{
    private const string DefaultSelection = "CVMainWindowsService_x64";

    private readonly DispatcherTimer _timer;
    private bool _seDebugEnabled;
    private bool _busy;
    private bool _suppressSelection;
    private long _lastHits = -1;
    private bool _hitLogged;

    public ServiceProcessWindow()
    {
        InitializeComponent();
        ApplySnapshot(ServiceLicenseBypass.Snapshot());
        Services.SelectedValue = DefaultSelection;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (ServiceLicenseBypass.IsElevated)
        {
            bool ok = ServiceLicenseBypass.TryEnablePrivilege(out string message);
            _seDebugEnabled |= ok;
            Log("权限：" + message);
        }
        else
        {
            Log("当前主程序不是管理员，无法打开 SYSTEM 服务进程；请以管理员身份重新启动 ColorVision。");
        }

        RefreshEnvironment();
        await InspectAsync();   // 打开即诊断，不需要手动点按钮
    }

    // ------------------------------------------------------------------
    // 快照 / 状态
    // ------------------------------------------------------------------

    private void ApplySnapshot(IReadOnlyList<ServiceLicenseBypass.TargetState> states)
    {
        string selected = Services.SelectedValue as string ?? DefaultSelection;
        _suppressSelection = true;
        try
        {
            Services.ItemsSource = states;
            Services.SelectedValue = selected;
        }
        finally { _suppressSelection = false; }
        UpdateDiagnosticsBox();
        RefreshEnvironment();
    }

    private void UpdateDiagnosticsBox()
    {
        if (DiagnosticsBox == null) return;
        DiagnosticsBox.Text = (Services.SelectedItem as ServiceLicenseBypass.TargetState)?.DiagnosticsText
                              ?? "选中一个服务卡片以查看只读诊断结果。";
    }

    private void RefreshEnvironment()
    {
        bool elevated = ServiceLicenseBypass.IsElevated;
        bool experimental = ServiceLicenseBypass.ExperimentalEnabled;
        bool installed = ServiceLicenseBypass.HasAnyInstalled;

        var states = Services.ItemsSource as IEnumerable<ServiceLicenseBypass.TargetState>;
        int firstPid = states?.Where(x => x.IsRunning).SelectMany(x => x.Instances).Select(x => x.Id).FirstOrDefault() ?? 0;
        string access = firstPid > 0 ? ServiceLicenseBypass.ProbeAccess(firstPid) : "未发现服务进程";
        bool accessOk = access.StartsWith("可读写", StringComparison.Ordinal);

        string verdict;
        if (!elevated) verdict = "不可用：主程序不是管理员";
        else if (!experimental) verdict = "不可用：实验功能开关未开启";
        else if (!_seDebugEnabled) verdict = "待启用 SeDebugPrivilege";
        else if (!accessOk) verdict = "不可用：" + access;
        else verdict = installed ? "权限就绪，放行已生效" : "权限就绪，可一键放行";

        StatusLine.Text = "实验功能开关: " + (experimental ? "开启" : "关闭")
                        + "    |    管理员: " + (elevated ? "是" : "否")
                        + "    |    目标: dev + x64"
                        + "    |    " + verdict;
        PrivilegeLine.Text = "SeDebugPrivilege: " + (_seDebugEnabled ? "已启用" : "未启用")
                           + "    |    目标进程访问: " + access
                           + "    |    状态: " + (installed ? "默认放行中" : "未放行")
                           + Environment.NewLine
                           + (accessOk
                                ? "心跳命中数持续增长即为生效证据；服务重启后内存补丁自动消失。"
                                : "若显示「拒绝访问 (Win32 5)」，请以管理员身份重启 ColorVision 主程序。");

        HitSummary.Text = installed
            ? $"放行状态：已生效（累计命中 {ServiceLicenseBypass.TotalHits} 次）"
            : "放行状态：未放行";

        InstallButton.IsEnabled = !_busy && elevated && experimental && _seDebugEnabled && accessOk && !installed;
        RestoreButton.IsEnabled = !_busy && installed;
        InspectButton.IsEnabled = !_busy;
        RefreshButton.IsEnabled = !_busy;
    }

    // ------------------------------------------------------------------
    // 按钮
    // ------------------------------------------------------------------

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        ApplySnapshot(ServiceLicenseBypass.Snapshot());
        Log("已刷新 dev / x64 服务进程状态。");
    }

    private async void Inspect_Click(object sender, RoutedEventArgs e) => await InspectAsync();

    private async Task InspectAsync()
    {
        if (_busy) return;
        _busy = true;
        RefreshEnvironment();
        try
        {
            IReadOnlyList<string> log = await Task.Run(ServiceLicenseBypass.InspectAll);
            foreach (string line in log) Log(line);
        }
        catch (Exception ex)
        {
            Log("诊断异常：" + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            _busy = false;
            ApplySnapshot(ServiceLicenseBypass.Snapshot());
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        RefreshEnvironment();
        Log("一键放行：开始对 dev 与 x64 两个服务进程安装补丁…");
        try
        {
            IReadOnlyList<string> log = await Task.Run(ServiceLicenseBypass.InstallAll);
            foreach (string line in log) Log(line);
            _lastHits = -1;
            _hitLogged = false;
        }
        catch (Exception ex)
        {
            Log("一键放行异常：" + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            _busy = false;
            ApplySnapshot(ServiceLicenseBypass.Snapshot());
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        RefreshEnvironment();
        try
        {
            IReadOnlyList<string> log = await Task.Run(ServiceLicenseBypass.RestoreAll);
            foreach (string line in log) Log(line);
            _lastHits = -1;
            _hitLogged = false;
        }
        catch (Exception ex)
        {
            Log("恢复异常：" + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            _busy = false;
            ApplySnapshot(ServiceLicenseBypass.Snapshot());
        }
    }

    // ------------------------------------------------------------------
    // 定时刷新
    // ------------------------------------------------------------------

    private void Timer_Tick(object? sender, EventArgs e)
    {
        foreach (string line in ServiceLicenseBypass.Tick()) Log(line);

        long hits = ServiceLicenseBypass.TotalHits;
        if (hits != _lastHits)
        {
            _lastHits = hits;
            RefreshEnvironment();
            if (hits > 0 && !_hitLogged)
            {
                _hitLogged = true;
                Log("已捕获服务心跳对 LicenseValidate 的调用，放行生效（累计命中 " + hits + " 次）。");
            }
        }

        ApplySnapshot(ServiceLicenseBypass.Snapshot());
    }

    // ------------------------------------------------------------------
    // 其它交互
    // ------------------------------------------------------------------

    private void Services_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection) return;
        UpdateDiagnosticsBox();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
        // 默认保留放行状态：静态管理器持有 Hook，重新打开窗口仍可点「恢复原校验」。
    }

    private void Log(string message)
    {
        if (LogBox == null) return;
        LogBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
        LogBox.ScrollToEnd();
    }
}
