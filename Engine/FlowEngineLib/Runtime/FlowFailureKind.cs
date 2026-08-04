namespace FlowEngineLib.Runtime;

/// <summary>
/// Diagnostic classification for a node failure.
/// </summary>
public enum FlowFailureKind
{
    Business,
    Technical,
    Timeout,
    Canceled,
    Contract
}
