using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotSettingsViewModel
    {
        public RelayCommand DetectLocalCodexCommand { get; private set; } = null!;
        public RelayCommand LoginLocalCodexCommand { get; private set; } = null!;
        public RelayCommand AddLocalCodexCommand { get; private set; } = null!;
        private bool _isCheckingLocalCodex;
        private bool _hasLocalCodex;
        private bool _isLocalCodexSignedIn;
        private string _localCodexStatus = "正在检测本机 Codex…";
        private IReadOnlyList<string> _localCodexModels = Array.Empty<string>();
        private readonly Func<IReadOnlyList<string>> _findLocalCodexCandidates;
        private readonly Func<IReadOnlyList<string>?, CancellationToken, Task<ICopilotCodexAppServer>> _connectLocalCodex;

        public bool IsCheckingLocalCodex { get => _isCheckingLocalCodex; private set { SetProperty(ref _isCheckingLocalCodex, value); CommandManager.InvalidateRequerySuggested(); } }
        public bool HasLocalCodex { get => _hasLocalCodex; private set => SetProperty(ref _hasLocalCodex, value); }
        public bool IsLocalCodexSignedIn { get => _isLocalCodexSignedIn; private set { SetProperty(ref _isLocalCodexSignedIn, value); CommandManager.InvalidateRequerySuggested(); } }
        public string LocalCodexStatus { get => _localCodexStatus; private set => SetProperty(ref _localCodexStatus, value); }
        public IReadOnlyList<string> LocalCodexModels => _localCodexModels;

        private void InitializeLocalCodexCommands()
        {
            DetectLocalCodexCommand = new RelayCommand(_ => RunUiOperation(DetectLocalCodexAsync, "检测 Codex"), _ => !_disposed && !IsCheckingLocalCodex);
            LoginLocalCodexCommand = new RelayCommand(_ => RunUiOperation(LoginLocalCodexAsync, "登录 Codex"), _ => !_disposed && HasLocalCodex && !IsCheckingLocalCodex && !IsLocalCodexSignedIn);
            AddLocalCodexCommand = new RelayCommand(_ => AddLocalCodexProfile(), _ => !_disposed && HasLocalCodex && IsLocalCodexSignedIn && !IsCheckingLocalCodex);
        }

        internal async Task DetectLocalCodexAsync()
        {
            if (_disposed || IsCheckingLocalCodex) return;
            IsCheckingLocalCodex = true;
            ClearLocalCodexAccount();
            LocalCodexStatus = "正在检测本机 Codex…";
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                var candidates = await Task.Run(_findLocalCodexCandidates, timeout.Token);
                timeout.Token.ThrowIfCancellationRequested();
                HasLocalCodex = candidates.Count > 0;
                if (!HasLocalCodex) { LocalCodexStatus = "未检测到 Codex，可继续配置 API Key。"; return; }
                await using var server = await _connectLocalCodex(candidates, timeout.Token);
                timeout.Token.ThrowIfCancellationRequested();
                await RefreshLocalCodexAccountAsync(server, timeout.Token, useInitialSnapshot: true);
            }
            catch (OperationCanceledException) { if (!_disposed) LocalCodexStatus = "检测超时，请启动或更新 Codex 后重新检测。"; }
            catch (Exception ex) { if (!_disposed) LocalCodexStatus = SanitizeError(ex.Message); }
            finally { IsCheckingLocalCodex = false; }
        }

        private void ClearLocalCodexAccount()
        {
            IsLocalCodexSignedIn = false;
            _localCodexModels = Array.Empty<string>();
            OnPropertyChanged(nameof(LocalCodexModels));
            OnPropertyChanged(nameof(AvailableModelPresets));
        }

        internal async Task RefreshLocalCodexAccountAsync(ICopilotCodexAppServer server, CancellationToken token, bool useInitialSnapshot)
        {
            token.ThrowIfCancellationRequested();
            var initialSignedIn = useInitialSnapshot ? server.InitialSignedIn : null;
            bool signedIn;
            if (initialSignedIn.HasValue)
                signedIn = initialSignedIn.Value;
            else
            {
                var account = await server.RequestAsync("account/read", new { refreshToken = false }, token);
                token.ThrowIfCancellationRequested();
                signedIn = account["account"] != null;
            }
            token.ThrowIfCancellationRequested();
            IReadOnlyList<string> availableModels = Array.Empty<string>();
            if (signedIn)
            {
                var models = await server.RequestAsync("model/list", new { limit = 100 }, token);
                token.ThrowIfCancellationRequested();
                if (models["data"] is System.Text.Json.Nodes.JsonArray data)
                    availableModels = data.Select(m => m?["model"]?.GetValue<string>() ?? string.Empty).Where(m => m.Length > 0).Distinct().ToArray();
            }
            _localCodexModels = availableModels;
            IsLocalCodexSignedIn = signedIn;
            LocalCodexStatus = signedIn ? "已检测到 Codex，已登录。可使用本机账户，无需 API Key。" : "已检测到 Codex，登录后即可使用。";
            OnPropertyChanged(nameof(LocalCodexModels));
            OnPropertyChanged(nameof(AvailableModelPresets));
        }

        private async Task LoginLocalCodexAsync()
        {
            if (_disposed || IsCheckingLocalCodex) return;
            IsCheckingLocalCodex = true;
            ClearLocalCodexAccount();
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
                timeout.CancelAfter(TimeSpan.FromMinutes(5));
                await using var server = await _connectLocalCodex(null, timeout.Token);
                timeout.Token.ThrowIfCancellationRequested();
                var login = await server.RequestAsync("account/login/start", new { type = "chatgpt" }, timeout.Token);
                timeout.Token.ThrowIfCancellationRequested();
                var url = login["authUrl"]?.GetValue<string>();
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
                    || !(uri.Host == "auth.openai.com" || uri.Host == "chatgpt.com" || uri.Host.EndsWith(".openai.com", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Codex 未返回有效的官方登录地址，请在 Codex 桌面版中登录。");
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                LocalCodexStatus = "请在浏览器完成 Codex 登录…";
                while (true)
                {
                    var message = await server.ReadAsync(timeout.Token);
                    timeout.Token.ThrowIfCancellationRequested();
                    if (message["method"]?.GetValue<string>() != "account/login/completed") continue;
                    if (message["params"]?["success"]?.GetValue<bool>() != true)
                        throw new InvalidOperationException("Codex 登录未完成，请重试或在桌面版中登录。");
                    break;
                }
                await RefreshLocalCodexAccountAsync(server, timeout.Token, useInitialSnapshot: false);
            }
            catch (OperationCanceledException) { if (!_disposed) LocalCodexStatus = "登录已超时，请重新登录。"; }
            catch (Exception ex) { if (!_disposed) LocalCodexStatus = SanitizeError(ex.Message); }
            finally { IsCheckingLocalCodex = false; }
        }

        internal void AddLocalCodexProfile()
        {
            if (!HasLocalCodex || !IsLocalCodexSignedIn || IsCheckingLocalCodex) return;
            var profile = Profiles.FirstOrDefault(p => p.IsLocalCodex);
            if (profile == null)
            {
                profile = new CopilotProfileConfig
                {
                    Name = "本机 Codex", VendorType = CopilotVendorType.OpenAI,
                    ProviderType = CopilotProviderType.LocalCodex, BaseUrl = string.Empty, ApiKey = string.Empty,
                    Model = string.Empty, SupportsImageInput = true,
                };
                Profiles.Add(profile);
            }
            SelectedProfile = profile;
            MarkSettingsPending("已选择本机 Codex，点击 Apply 或 Save 后用于聊天。");
        }
    }
}
