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

    public readonly record struct RealtimePseudoColorRequest(int Generation, PseudoColorFrameRequest FrameRequest);

    public interface IRealtimePseudoColorService
    {
        bool IsEnabled { get; }
        bool TryCreateRequest(out RealtimePseudoColorRequest request, int? channelOverride = null);
        void ApplyProcessedImage(RealtimePseudoColorRequest request, HImage pseudoImage);
    }
}
