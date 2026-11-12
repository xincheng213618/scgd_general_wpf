namespace ProjectARVRPro.Process;

/// <summary>Optional LUX reply implemented by a process with custom output.</summary>
public interface ILuxFlowResponseProvider
{
    /// <summary>Overrides the prefix for completion, missing results and failure; null keeps the standard prefix.</summary>
    string? GetResponsePrefix(LuxFlowResponseContext context) => null;

    /// <summary>Returns null when this process does not customize the current command.</summary>
    LuxFlowResponse? BuildCompletedResponse(LuxFlowResponseContext context);
}

/// <summary>The accepted command and its completed flow result, without cumulative test results.</summary>
public sealed record LuxFlowResponseContext(string MachineNo, string SocketCode, string SerialNumber, ProjectARVRReuslt? Result);

/// <summary>A complete wire reply and an optional diagnostic for missing or invalid measurements.</summary>
public sealed record LuxFlowResponse(string Text, string? Error = null);
