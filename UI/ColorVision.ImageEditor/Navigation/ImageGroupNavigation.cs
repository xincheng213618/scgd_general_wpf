using System;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.Navigation
{
    /// <summary>Owns image-group selection and follow behavior without depending on a view or Dispatcher.</summary>
    internal sealed class ImageGroupNavigation
    {
        private const int MinimumIndexedPaths = 64;
        private readonly List<ImageViewImageItem> _items = new();
        private Dictionary<string, int>? _indexByPath;
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
            if (images == null) return false;
            List<ImageViewImageItem> items = new();
            Dictionary<string, int> indexByPath = new(StringComparer.OrdinalIgnoreCase);
            foreach (ImageViewImageItem image in images)
            {
                if (string.IsNullOrWhiteSpace(image.FilePath) || !indexByPath.TryAdd(image.FilePath, items.Count)) continue;
                items.Add(image);
            }
            if (items.Count == 0) return false;

            _items.EnsureCapacity(items.Count);
            _items.Clear();
            _items.AddRange(items);
            _indexByPath = items.Count >= MinimumIndexedPaths ? indexByPath : null;
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

            ImageViewImageItem item = new(filePath);
            int index = _items.Count;
            _items.EnsureCapacity(index + 1);
            if (_indexByPath == null && index == MinimumIndexedPaths - 1)
            {
                Dictionary<string, int> indexByPath = new(MinimumIndexedPaths, StringComparer.OrdinalIgnoreCase);
                for (int existing = 0; existing < index; existing++)
                    indexByPath.Add(_items[existing].FilePath, existing);
                indexByPath.Add(filePath, index);
                _indexByPath = indexByPath;
            }
            else _indexByPath?.Add(filePath, index);
            _items.Add(item);
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
            _indexByPath = null;
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

            ImageViewImageItem item = new(filePath);
            _items.EnsureCapacity(1);
            _items.Clear();
            _items.Add(item);
            _indexByPath = null;
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

        private int FindIndex(string filePath)
        {
            if (_indexByPath != null)
                return _indexByPath.TryGetValue(filePath, out int index) ? index : -1;
            for (int i = 0; i < _items.Count; i++)
                if (string.Equals(_items[i].FilePath, filePath, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }
}
