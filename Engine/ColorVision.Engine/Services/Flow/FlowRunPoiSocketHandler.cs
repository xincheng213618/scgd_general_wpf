using ColorVision.SocketProtocol;
using Newtonsoft.Json;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Flow;

/// <summary>General host Socket command, discovered without a customer bundle.</summary>
public sealed class FlowRunPoiSocketHandler : ISocketJsonHandler
{
    public string EventName => "FlowRunPoi";
    private readonly Func<FlowPoiExecutionRequest, Task<FlowPoiExecutionResult>> execute;

    public FlowRunPoiSocketHandler() : this(FlowPoiExecutionService.Shared.RunAsync)
    {
    }

    internal FlowRunPoiSocketHandler(Func<FlowPoiExecutionRequest, Task<FlowPoiExecutionResult>> execute)
    {
        this.execute = execute;
    }

    public SocketResponse Handle(NetworkStream stream, SocketRequest request)
    {
        FlowPoiExecutionRequest? input;
        try
        {
            input = JsonConvert.DeserializeObject<FlowPoiExecutionRequest>(request.Params ?? string.Empty,
                new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        }
        catch (JsonException)
        {
            return Response(request, new(400, "Params 必须是包含 FlowName 或 FlowId 的 JSON 字符串。"));
        }

        if (input == null)
            return Response(request, new(400, "Params 必须包含 FlowName 或 FlowId。"));

        // SocketManager dispatches on its client worker. The service marshals
        // only flow operations onto the UI dispatcher and awaits finalization.
        return Response(request, execute(input).GetAwaiter().GetResult());
    }

    private SocketResponse Response(SocketRequest request, FlowPoiExecutionResult result) => new()
    {
        Version = request.Version,
        MsgID = request.MsgID,
        EventName = EventName,
        SerialNumber = request.SerialNumber,
        Code = result.Code,
        Msg = result.Message,
        Data = result.Data
    };
}
