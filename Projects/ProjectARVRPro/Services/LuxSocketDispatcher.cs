using ColorVision.SocketProtocol;
using ProjectARVRPro.PluginConfig;
using ProjectARVRPro.Process.OpticCenter;
using System.Net.Sockets;
using System.Text;
using System.Windows;

namespace ProjectARVRPro.Services;

/// <summary>The original LUX wire format, independent of Pro's JSON events and batch Code.</summary>
internal sealed record LuxRequest(string MachineNo, string SocketCode, string SerialNumber)
{
    internal static LuxRequest? Parse(string? text, string machineNo, out string? error)
    {
        string payload = text?.Trim().TrimEnd(';') ?? string.Empty;
        int comma = payload.IndexOf(',');
        string code = comma >= 0 ? payload[..comma] : payload;
        string sn = comma >= 0 ? payload[(comma + 1)..] : string.Empty;
        error = string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(sn) ? "No SN"
            : !code.StartsWith("T00", StringComparison.Ordinal) ? $"No right Code {code}" : null;
        return error == null ? new(machineNo, code[^2..], sn) : null;
    }

    internal bool IsOpticCenter => MachineNo == "H03AR" && SocketCode == "02";
    internal string ResponsePrefix => IsOpticCenter ? $"{MachineNo}{SocketCode},{SerialNumber}" : $"{MachineNo}{SocketCode},{SerialNumber},00;";
    internal string MeasurementResponse(object? value) => ResponsePrefix + value;
    internal string FailureResponse(string? eventName, string? parameters) => ResponsePrefix + $"FlowFailed:{eventName},{parameters};";
    internal string CompletedResponse(OpticCenterTestResult? center) => IsOpticCenter
        && center is { OptCenterRotation: not null, OptCenterXTilt: not null, OptCenterYTilt: not null }
        ? ResponsePrefix + $",{center.OptCenterRotation.Value},{center.OptCenterXTilt.Value},{center.OptCenterYTilt.Value},00;"
        : ResponsePrefix;
}

/// <summary>Keeps the command's response prefix and permits one terminal reply.</summary>
internal sealed class LuxCommandSession(LuxRequest request, Action<string> send)
{
    private int _completed;
    internal LuxRequest Request { get; } = request;
    internal void Complete(string response)
    {
        if (Interlocked.Exchange(ref _completed, 1) == 0) send(response);
    }
}

public sealed class LuxSocketDispatcher : ISocketTextDispatcher
{
    private readonly Func<string> _machineNo;
    private readonly Func<NetworkStream, LuxRequest, string?> _handle;

    public LuxSocketDispatcher() : this(() => ProjectARVRProConfig.Instance.LuxMachineNo, DispatchToWindow) { }

    internal LuxSocketDispatcher(Func<string> machineNo, Func<NetworkStream, LuxRequest, string?> handle)
    {
        _machineNo = machineNo;
        _handle = handle;
    }

    public string? Handle(NetworkStream stream, string request)
    {
        LuxRequest? command = LuxRequest.Parse(request, _machineNo(), out string? error);
        if (command == null) return error;
        return _handle(stream, command);
    }

    private static string? DispatchToWindow(NetworkStream stream, LuxRequest command) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (command.SocketCode == "00" && ProjectWindowInstance.WindowInstance == null)
                return command.ResponsePrefix;
            ARVRWindow window = command.SocketCode is "00" or "01" or "31"
                ? ProjectARVRWindowHost.GetOrCreate() : ProjectARVRWindowHost.ShowOrActivate();
            return window.HandleLuxRequest(stream, new LuxCommandSession(command, response =>
            {
                SocketMessageManager.GetInstance().AddMessage(new SocketMessage
                {
                    Direction = SocketMessageDirection.Sent, Content = response, MessageTime = DateTime.Now
                });
                SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(response));
            }));
        });
}
