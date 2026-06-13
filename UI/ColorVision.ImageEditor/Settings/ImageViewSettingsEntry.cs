using System;

namespace ColorVision.ImageEditor.Settings
{
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
    }
}
