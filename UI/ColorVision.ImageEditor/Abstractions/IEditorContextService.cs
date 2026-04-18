using ColorVision.Core;

namespace ColorVision.ImageEditor.Abstractions
{
    public interface IEditorContextService
    {
    }

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

    public interface IPseudoColorService : IEditorContextService
    {
        bool IsEnabled { get; }
        void ConfigureForImage();
        void RefreshPreview();
        void RequestRender(int throttleDelayMs = 0);
        void Invalidate();
        bool TryCreateRequest(out PseudoColorFrameRequest request, int? channelOverride = null);
        void ApplyProcessedImage(HImage pseudoImage);
        void RestoreSource();
    }
}