#nullable enable
using System;

namespace FlowEngineLib.SMU;

/// <summary>Captures cleanup for the original session and output created by an executed local SMU command.</summary>
public interface ISmuFlowOutput
{
    SmuLocalOutputClose? CaptureCloseOutput();
}

public sealed record SmuLocalOutputClose(object Session, SMUChannelType Channel, Action Close);
