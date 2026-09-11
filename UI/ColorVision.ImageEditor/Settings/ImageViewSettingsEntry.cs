using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.ImageEditor.Settings
{
    public enum ImageSettingsScope { Extension, CurrentView, CurrentImage, Application, Defaults, SourceProfile }

    public sealed record ImageSettingsAction(string Title, Action Execute)
    {
        public object? SavedSource { get; init; }
    }

    public sealed class ImageViewSettingsEntry
    {
        public ImageViewSettingsEntry(string group, string title, object source, Action? save = null)
        {
            Group = group;
            Title = title;
            Source = source;
            Save = save;
        }

        public string Group { get; }

        public string Title { get; }

        public object Source { get; }

        public Action? Save { get; }

        public string Id { get; init; } = string.Empty;
        public string OwnerId { get; init; } = "Extension";
        public string CategoryId { get; init; } = string.Empty;
        public ImageSettingsScope Scope { get; init; } = ImageSettingsScope.Extension;
        public string Description { get; init; } = string.Empty;
        public int Order { get; init; }
        public bool IsReadOnly { get; init; }
        public IReadOnlyList<string>? PropertyNames { get; init; }
        public Func<FrameworkElement>? CreateView { get; init; }
        public IReadOnlyList<ImageSettingsAction> Actions { get; init; } = Array.Empty<ImageSettingsAction>();

        internal string PageId => string.IsNullOrWhiteSpace(CategoryId) ? Group : CategoryId;
        internal string StableId => string.IsNullOrWhiteSpace(Id) ? $"{Source.GetType().FullName}:{Group}:{Title}" : Id;
    }
}
