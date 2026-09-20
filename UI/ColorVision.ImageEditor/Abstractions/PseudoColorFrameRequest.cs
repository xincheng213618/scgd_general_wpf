using ColorVision.Core;

namespace ColorVision.ImageEditor.Abstractions
{
    public readonly record struct PseudoColorFrameRequest(
        uint Min,
        uint Max,
        ColormapTypes ColormapTypes,
        int Channel,
        bool IsAutoRangeEnabled,
        uint DataMin,
        uint DataMax)
    {
        public bool HasValidAutoRange => IsAutoRangeEnabled && DataMin < DataMax;
    }
}
