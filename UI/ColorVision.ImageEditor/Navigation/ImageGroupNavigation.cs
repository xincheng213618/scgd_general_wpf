using System;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.Navigation
{
    /// <summary>Owns image-group selection and follow behavior without depending on a view or Dispatcher.</summary>
    internal sealed class ImageGroupNavigation
    {
        private readonly List<ImageViewImageItem> _items = new();
        private readonly Action<string> _openImage;
        private bool _userPinned;

        internal ImageGroupNavigation(Action<string> openImage) => _openImage = openImage;

        internal IReadOnlyList<ImageViewImageItem> Items => _items;

        internal int SelectedIndex { get; private set; } = -1;

        internal bool AutoFollow { get; set; } = true;

        internal event EventHandler? Changed;

        internal event EventHandler<ImageViewImageChangedEventArgs>? SelectedImageChanged;

        // An empty group requests the host's full Clear operation. The host clears navigation
        // after document invalidation, preserving the ordering of its revision callbacks.
        internal bool TryOpenGroup(IEnumerable<ImageViewImageItem>? images, int selectedIndex)
        {
            List<ImageViewImageItem> items = Normalize(images);
            if (items.Count == 0) return false;

            _items.Clear();
            _items.AddRange(items);
            SelectedIndex = Math.Clamp(selectedIndex, 0, _items.Count - 1);
            _userPinned = false;
            OpenItem(SelectedIndex, false);
            return true;
        }

        internal void Append(string? filePath, bool open)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;
            int existingIndex = FindIndex(filePath);
            if (existingIndex >= 0)
            {
                if (open) Select(existingIndex);
                return;
            }

            _items.Add(new ImageViewImageItem(filePath));
            if (SelectedIndex < 0) SelectedIndex = 0;

            if (open && AutoFollow && !_userPinned) OpenItem(_items.Count - 1, false);
            else Changed?.Invoke(this, EventArgs.Empty);
        }

        internal void Select(int index)
        {
            if (_items.Count == 0) return;
            _userPinned = index < _items.Count - 1;
            OpenItem(index, true);
        }

        internal void Clear()
        {
            _items.Clear();
            SelectedIndex = -1;
            _userPinned = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        internal void SetSingle(string filePath)
        {
            if (_items.Count == 1 && SelectedIndex == 0
                && string.Equals(_items[0].FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                Changed?.Invoke(this, EventArgs.Empty);
                return;
            }

            _items.Clear();
            _items.Add(new ImageViewImageItem(filePath));
            SelectedIndex = 0;
            _userPinned = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void OpenItem(int index, bool userInitiated)
        {
            if (_items.Count == 0) return;
            SelectedIndex = Math.Clamp(index, 0, _items.Count - 1);
            ImageViewImageItem item = _items[SelectedIndex];
            Changed?.Invoke(this, EventArgs.Empty);
            _openImage(item.FilePath);
            SelectedImageChanged?.Invoke(this, new ImageViewImageChangedEventArgs(item, SelectedIndex, _items.Count, userInitiated));
        }

        private static List<ImageViewImageItem> Normalize(IEnumerable<ImageViewImageItem>? images)
        {
            List<ImageViewImageItem> items = new();
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            if (images == null) return items;

            foreach (ImageViewImageItem image in images)
            {
                if (string.IsNullOrWhiteSpace(image.FilePath) || !seen.Add(image.FilePath)) continue;
                items.Add(image);
            }
            return items;
        }

        private int FindIndex(string filePath)
        {
            for (int i = 0; i < _items.Count; i++)
                if (string.Equals(_items[i].FilePath, filePath, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }
}
