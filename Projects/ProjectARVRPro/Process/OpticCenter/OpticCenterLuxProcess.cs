using Newtonsoft.Json;

namespace ProjectARVRPro.Process.OpticCenter;

/// <summary>Uses the normal optical-center calculation and adds the legacy LUX station reply.</summary>
public class OpticCenterLuxProcess : OpticCenterProcess, ILuxFlowResponseProvider
{
    public string GetResponsePrefix(LuxFlowResponseContext context) => $"{context.MachineNo}{context.SocketCode},{context.SerialNumber}";

    public LuxFlowResponse? BuildCompletedResponse(LuxFlowResponseContext context)
    {
        string prefix = GetResponsePrefix(context);
        string? json = context.Result?.ViewResultJson;
        if (string.IsNullOrWhiteSpace(json))
            return new(prefix, "本次光学中心结果快照为空");

        OpticCenterTestResult? center;
        try
        {
            center = JsonConvert.DeserializeObject<OpticCenterTestResult>(json);
        }
        catch (JsonException ex)
        {
            return new(prefix, $"本次光学中心结果快照无法解析: {ex.Message}");
        }

        if (center?.OptCenterRotation is not { } rotation
            || center.OptCenterXTilt is not { } xTilt
            || center.OptCenterYTilt is not { } yTilt
            || !double.IsFinite(rotation.Value) || !double.IsFinite(xTilt.Value) || !double.IsFinite(yTilt.Value))
        {
            return new(prefix, "本次光学中心结果缺少有效的 Rotation、TiltX 或 TiltY，请核对算法结果及 optCenter 模板名称");
        }

        return new($"{prefix},{rotation.Value},{xTilt.Value},{yTilt.Value},00;");
    }
}
