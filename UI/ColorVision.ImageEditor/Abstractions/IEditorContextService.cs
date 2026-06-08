namespace ColorVision.ImageEditor.Abstractions
{
    public interface IEditorContextService
    {
    }

    public interface IPseudoColorService : IEditorContextService
    {
        bool IsEnabled { get; }
        void ConfigureForImage();
        void RefreshPreview();
        void RequestRender(int throttleDelayMs = 0);
        void Invalidate();
        void Reset();
        void RestoreSource();
    }
}
