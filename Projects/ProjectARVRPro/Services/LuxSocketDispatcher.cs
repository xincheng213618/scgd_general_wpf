using ColorVision.Engine.FlowProcessing;
using ColorVision.SocketProtocol;
using log4net;
using ProjectARVRPro.PluginConfig;
using ProjectARVRPro.Process;
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

    internal string ResponsePrefix => $"{MachineNo}{SocketCode},{SerialNumber},00;";
    internal string MeasurementResponse(object? value) => ResponsePrefix + value;
    internal string FailureResponse(string? eventName, string? parameters, string? responsePrefix = null) =>
        (responsePrefix ?? ResponsePrefix) + $"FlowFailed:{eventName},{parameters};";
}

/// <summary>Keeps the command's response prefix and permits one terminal reply.</summary>
internal sealed class LuxCommandSession(LuxRequest request, Action<string> send)
{
    private static readonly ILog log = LogManager.GetLogger(typeof(LuxCommandSession));
    private int _completed;
    internal LuxRequest Request { get; } = request;

    internal void CompleteFlow(IProcess? process, ProjectARVRReuslt? result, string? eventName, string? parameters)
    {
        var context = new LuxFlowResponseContext(Request.MachineNo, Request.SocketCode, Request.SerialNumber, result);
        var provider = process as ILuxFlowResponseProvider;
        string responsePrefix = Request.ResponsePrefix;
        if (provider != null)
        {
            try { responsePrefix = provider.GetResponsePrefix(context) ?? responsePrefix; }
            catch (Exception ex) { log.Error($"LUX 回包前缀生成失败，SN={Request.SerialNumber}", ex); }
        }
        if (eventName != "Completed")
        {
            Complete(Request.FailureResponse(eventName, parameters, responsePrefix));
            return;
        }

        string response = responsePrefix;
        if (provider != null)
        {
            if (result is not { FlowStatus: FlowStatus.Completed, BatchId: > 0 }
                || !string.Equals(result.SN, Request.SerialNumber, StringComparison.Ordinal)
                || !string.Equals(result.ProcessTypeFullName, provider.GetType().FullName, StringComparison.Ordinal))
            {
                log.Warn($"LUX 回包未使用结果：本次流程尚未完成或结果归属不匹配，SN={Request.SerialNumber}, BatchId={result?.BatchId}");
            }
            else
            {
                try
                {
                    LuxFlowResponse? custom = provider.BuildCompletedResponse(context);
                    if (custom != null)
                    {
                        response = custom.Text;
                        if (custom.Error != null)
                            log.Warn($"LUX 自定义回包缺值，SN={Request.SerialNumber}, BatchId={result.BatchId}: {custom.Error}");
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"LUX 自定义回包生成失败，SN={Request.SerialNumber}, BatchId={result.BatchId}", ex);
                }
            }
        }
        Complete(response);
    }

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
