using ColorVision.SocketProtocol;
using log4net;
using ProjectARVRPro.PluginConfig;
using System.Net.Sockets;
using System.Windows;

namespace ProjectARVRPro.Services;

public sealed class MeasureVidSocket : ISocketJsonHandler
{
    private static readonly ILog log = LogManager.GetLogger(typeof(MeasureVidSocket));
    private readonly Func<Task<(bool Busy, ObjectiveTestItem? Result)>> _measure;

    public MeasureVidSocket() : this(MeasureOnUiThreadAsync) { }

    internal MeasureVidSocket(Func<Task<(bool Busy, ObjectiveTestItem? Result)>> measure) => _measure = measure;

    public string EventName => "MeasureVID";

    public SocketResponse Handle(NetworkStream stream, SocketRequest request)
    {
        var response = new SocketResponse
        {
            Version = request.Version ?? "1.0",
            MsgID = request.MsgID,
            EventName = EventName,
            SerialNumber = request.SerialNumber
        };
        if (string.IsNullOrWhiteSpace(request.SerialNumber))
        {
            response.Code = -1;
            response.Msg = "SerialNumber is required";
            return response;
        }

        try
        {
            // Dispatch waits on the client worker. SocketManager sends this single reply on that connection.
            var (busy, result) = _measure().GetAwaiter().GetResult();
            if (busy)
            {
                response.Code = -4;
                response.Msg = "ARVR test is busy";
            }
            else if (result == null)
            {
                response.Code = -2;
                response.Msg = "VID measurement failed";
            }
            else
            {
                response.Msg = "OK";
                response.Data = new { VID = result.Value, Pass = result.TestResult };
            }
        }
        catch (OperationCanceledException)
        {
            response.Code = -2;
            response.Msg = "VID measurement canceled";
        }
        catch (Exception ex)
        {
            log.Error("JSON VID 测量失败", ex);
            response.Code = -2;
            response.Msg = ex.Message;
        }

        return response;
    }

    private static Task<(bool Busy, ObjectiveTestItem? Result)> MeasureOnUiThreadAsync() =>
        Application.Current.Dispatcher.InvokeAsync(() => ProjectARVRWindowHost.GetOrCreate().MeasureVidForJsonAsync()).Task.Unwrap();
}
