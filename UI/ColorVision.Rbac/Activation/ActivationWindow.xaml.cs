using ColorVision.Themes;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Rbac.Activation;

public partial class ActivationWindow : Window
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DispatcherTimer _heartbeat = new() { Interval = TimeSpan.FromMinutes(1) };
    private ActivationClient? _client;
    private ActivationState? _state;
    private bool _busy;
    private bool _closed;

    public ActivationWindow()
    {
        InitializeComponent();
        this.ApplyCaption();
        Render();
        ContentRendered += LoadOnce;
        _heartbeat.Tick += async (_, _) =>
        {
            if (_state?.EnrollmentMode != null)
                await RefreshAsync();
        };
    }

    private async void LoadOnce(object? sender, EventArgs e)
    {
        ContentRendered -= LoadOnce;
        await RefreshAsync();
        if (!_closed)
            _heartbeat.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _heartbeat.Stop();
        _lifetime.Cancel();
        _lifetime.Dispose();
        base.OnClosed(e);
    }

    private ActivationMode SelectedMode => OfflineMode.IsChecked == true ? ActivationMode.Offline : ActivationMode.Online;

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync(renewOffline: true);

    private Task RefreshAsync(bool renewOffline = false) => RunAsync(async (client, token) =>
    {
        var state = await client.RefreshAsync(token);
        if (!state.IsServerAvailable || state.LicenseExpiresAt <= DateTimeOffset.UtcNow)
            return state;
        bool needsOfflineRenewal = state.EnrollmentMode == ActivationMode.Offline &&
            (renewOffline || state.OfflineExpiresAt == null || state.OfflineExpiresAt <= DateTimeOffset.UtcNow.AddHours(1));
        if (state.EnrollmentMode != ActivationMode.Online && !needsOfflineRenewal)
            return state;
        try
        {
            return await client.RenewAsync(token);
        }
        catch (ActivationException ex)
        {
            throw new RefreshException(state, ex);
        }
    });

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        var mode = SelectedMode;
        await RunAsync((client, token) => client.ApplyAsync(mode, token));
    }

    private async void Activate_Click(object sender, RoutedEventArgs e)
    {
        var code = LicenseInput.Password.Trim();
        if (code.Length == 0)
        {
            ShowOperation("请输入许可证。");
            LicenseInput.Focus();
            return;
        }
        var mode = SelectedMode;
        await RunAsync((client, token) => client.ActivateAsync(code, mode, token));
    }

    private async void Release_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _state is not { EnrollmentMode: not null } state)
            return;
        _heartbeat.Stop();
        try
        {
            if (MessageBox.Show(this, DisplayText.Get("释放后，本机需要联网重新激活。是否继续？"), Title,
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
            await RunAsync(async (client, token) =>
            {
                try
                {
                    return await client.ReleaseAsync(token);
                }
                catch (ActivationException ex) when (ex.Code is "server_unavailable" or "invalid_response" or "local_storage_error")
                {
                    throw new RefreshException(state with { Devices = [], OfflineExpiresAt = null, IsServerAvailable = false }, ex);
                }
            }, "已释放本机。");
        }
        finally
        {
            if (!_closed)
                _heartbeat.Start();
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_state?.LicenseCode is not { Length: > 0 } code)
            return;
        try
        {
            Clipboard.SetText(code);
            ShowOperation("许可证已复制。持有许可证的人可以占用可用设备名额。");
        }
        catch (ExternalException)
        {
            ShowOperation("剪贴板暂不可用，请重试。");
        }
    }

    private async Task RunAsync(Func<ActivationClient, CancellationToken, Task<ActivationState>> operation, string? success = null)
    {
        if (_busy || _closed)
            return;
        _busy = true;
        Render();
        var token = _lifetime.Token;
        try
        {
            var result = await operation(_client ??= ActivationClient.CreateDefault(), token);
            if (_closed)
                return;
            _state = result;
            if (result.EnrollmentMode != null)
                LicenseInput.Clear();
            ShowOperation(success);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (RefreshException ex) when (!_closed)
        {
            _state = ex.State;
            ShowOperation(ErrorText(ex.Failure.Code));
        }
        catch (ActivationException ex) when (!_closed)
        {
            ShowOperation(ErrorText(ex.Code));
        }
        catch (Exception ex)
        {
            if (!_closed)
            {
                log4net.LogManager.GetLogger(typeof(ActivationWindow)).Warn("Activation dialog operation failed: " + ex.GetType().Name);
                ShowOperation("激活请求未完成，请重试。");
            }
        }
        finally
        {
            _busy = false;
            if (!_closed)
                Render();
        }
    }

    private void Render()
    {
        var state = _state;
        var now = DateTimeOffset.UtcNow;
        var devices = state?.Devices.Where(device => device.ExpiresAt > now).ToArray() ?? [];
        var current = devices.FirstOrDefault(device => device.DeviceId == state?.CurrentDeviceId);
        var enrollment = state?.EnrollmentMode;
        bool expired = state?.LicenseExpiresAt <= now;
        bool offlineValid = state?.OfflineExpiresAt > now;
        string status = expired ? "许可证已到期"
            : offlineValid ? "本机已激活 · TPM 离线"
            : current?.Mode == ActivationMode.Online ? "本机已激活 · 在线租约"
            : enrollment != null ? "本机待联网续期"
            : "本机未激活";
        LocalStatus.Text = DisplayText.Get(status);
        LicenseDetails.Text = state?.LicenseExpiresAt is { } licenseEnd
            ? DisplayText.Format($"许可证到期 {FormatTime(licenseEnd)}") : string.Empty;
        if (offlineValid && state?.OfflineExpiresAt is { } offlineEnd)
            LicenseDetails.Text += "  ·  " + DisplayText.Format($"离线凭据到期 {FormatTime(offlineEnd)}");
        if (LicenseDetails.Text.Length == 0 && _busy)
            LicenseDetails.Text = DisplayText.Get("正在读取激活状态…");
        LicenseDetails.Visibility = LicenseDetails.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        Actions.IsEnabled = !_busy;
        EnrollmentCard.IsEnabled = !_busy;
        EnrollmentCard.Visibility = enrollment == null || expired ? Visibility.Visible : Visibility.Collapsed;
        CopyButton.Visibility = state?.LicenseCode is { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        ReleaseButton.Visibility = enrollment != null ? Visibility.Visible : Visibility.Collapsed;
        DevicesTitle.Text = state is { IsServerAvailable: true, DeviceLimit: > 0 }
            ? DisplayText.Format($"使用中的设备（{devices.Length} / {state.DeviceLimit}）")
            : DisplayText.Get("使用中的设备");
        DevicesHint.Text = state?.UnavailableReason is { } reason ? DisplayText.Get(ErrorText(reason))
            : devices.Length == 0 ? DisplayText.Get("暂无使用中的设备。") : string.Empty;
        DevicesHint.Visibility = DevicesHint.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        DeviceList.ItemsSource = devices.Select(device => new DeviceRow(
            device.DeviceId == state?.CurrentDeviceId ? DisplayText.Format($"{device.DeviceName}（本机）") : device.DeviceName,
            DisplayText.Format($"{DisplayText.Get(device.Mode == ActivationMode.Offline ? "TPM 离线" : "在线租约")} · 到期 {FormatTime(device.ExpiresAt)}"))).ToArray();
    }

    private void ShowOperation(string? key)
    {
        OperationStatus.Text = key is null ? string.Empty : DisplayText.Get(key);
        OperationStatus.Visibility = key is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private static string FormatTime(DateTimeOffset value) => value.ToLocalTime().ToString("G", CultureInfo.CurrentCulture);

    private static string ErrorText(string code) => code switch
    {
        "tpm_unavailable" => "本机无法创建 TPM 密钥。可选择共享电脑（在线）。",
        "device_key_unavailable" => "本机密钥不可用，请检查 Windows 账户或 TPM 状态。",
        "server_unavailable" => "当前无法连接激活服务器，设备列表可能仅显示本机的离线凭据。",
        "invalid_server_url" => "激活服务器地址无效。",
        "http_public_key_required" => "HTTP 激活需要预置服务器公钥，请联系维护人员。",
        "invalid_http_public_key" => "激活服务器公钥无效，请联系维护人员。",
        "local_storage_error" => "无法读取或保存本机激活记录。",
        "offline_clock_invalid" => "本机时间异常，请校准后联网刷新。",
        "not_enrolled" => "本机没有可续期的激活记录。",
        "invalid_license" => "许可证无效，请核对后重试。",
        "license_expired" => "许可证已到期。",
        "device_limit" => "设备名额已满，请先在其他设备上释放，或等待租约到期。",
        "release_required" => "请先释放本机的现有激活。",
        "rate_limited" => "请求过于频繁，请稍后重试。",
        "activation_busy" => "另一个 ColorVision 窗口正在处理激活，请稍后重试。",
        "invalid_response" => "服务器返回的激活数据无法验证。",
        _ => "激活请求未完成，请重试。",
    };

    private sealed record DeviceRow(string Name, string Details);

    private sealed class RefreshException(ActivationState state, ActivationException failure) : Exception(failure.Message, failure)
    {
        public ActivationState State { get; } = state;
        public ActivationException Failure { get; } = failure;
    }
}
