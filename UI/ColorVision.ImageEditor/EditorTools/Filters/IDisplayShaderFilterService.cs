using System;

namespace ColorVision.ImageEditor.EditorTools.Filters
{
    public interface IDisplayShaderFilterService
    {
        DisplayShaderFilterState State { get; }
        event EventHandler? StateChanged;
        void AttachPersistence(DisplayShaderFilterState state, Action? saveAction);
        void OpenSettingsWindow();
        void Save();
    }
}
