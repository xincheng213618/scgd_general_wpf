using ColorVision.UI;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services
{
    public sealed class CopilotDeviceContextProvider : ICopilotContextProvider
    {
        private static readonly string[] DeviceIntentTerms =
        [
            "device", "device service", "camera", "spectrum", "sensor", "motor", "calibration", "smu",
            "设备", "设备服务", "相机", "光谱", "传感器", "电机", "标定", "校准", "在线", "离线", "心跳",
        ];
        private readonly Func<string, CancellationToken, Task<CopilotBusinessContextBundle?>> _contextProvider;
        private readonly Func<bool> _isActive;
        private readonly Func<bool> _isCurrentSurface;

        public CopilotDeviceContextProvider(
            Func<string, CancellationToken, Task<CopilotBusinessContextBundle?>> contextProvider,
            Func<bool>? isActive = null,
            Func<bool>? isCurrentSurface = null)
        {
            _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
            _isActive = isActive ?? (() => true);
            _isCurrentSurface = isCurrentSurface ?? (() => false);
        }

        public int Order => 25;

        public bool CanProvide(CopilotContextScope scope)
        {
            return _isActive() && (scope == CopilotContextScope.Agent || scope == CopilotContextScope.Diagnose);
        }

        public async Task<CopilotContextItem?> CaptureAsync(CopilotContextRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            if (!_isActive() || !ShouldCapture(request))
                return null;

            var bundle = await _contextProvider(request.UserText ?? string.Empty, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!_isActive() || bundle?.Items == null)
                return null;

            return bundle.Items.FirstOrDefault(item => item != null
                && (!string.IsNullOrWhiteSpace(item.Content) || !string.IsNullOrWhiteSpace(item.Summary)));
        }

        public static CopilotDeviceContextProvider Create(ServiceManager manager)
        {
            ArgumentNullException.ThrowIfNull(manager);
            return new CopilotDeviceContextProvider(
                (userText, cancellationToken) => CaptureManagerContextAsync(manager, userText, cancellationToken),
                () => ReferenceEquals(ServiceManager.Current, manager),
                manager.HasCurrentCopilotDeviceSurface);
        }

        private bool ShouldCapture(CopilotContextRequest request)
        {
            if (request.Scope == CopilotContextScope.Diagnose || _isCurrentSurface())
                return true;

            var userText = request.UserText ?? string.Empty;
            return DeviceIntentTerms.Any(term => userText.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task<CopilotBusinessContextBundle?> CaptureManagerContextAsync(
            ServiceManager manager,
            string userText,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                return await dispatcher.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return manager.CaptureCopilotDeviceContext(userText);
                });
            }

            return manager.CaptureCopilotDeviceContext(userText);
        }
    }

    public static class CopilotDeviceAgentExtension
    {
        public const string SourceId = "device-services";

        public static IDisposable Register(
            CopilotAgentExtensionRegistry registry,
            ICopilotContextProvider contextProvider,
            string? sourceVersion = null)
        {
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentNullException.ThrowIfNull(contextProvider);
            return registry.Register(new CopilotAgentExtensionRegistration
            {
                SourceId = SourceId,
                SourceName = "Device Services",
                SourceVersion = sourceVersion ?? string.Empty,
                ContextProviders = [contextProvider],
            });
        }
    }
}
