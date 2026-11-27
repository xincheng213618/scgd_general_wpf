using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ColorVision.UI.LogImp.Controls
{
    internal sealed class BulkObservableCollection<T> : ObservableCollection<T>
    {
        private const int PerItemNotificationLimit = 32;

        public void ResetWith(IEnumerable<T> items)
        {
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            RaiseReset();
        }

        public void AddRange(IReadOnlyList<T> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            if (items.Count <= PerItemNotificationLimit)
            {
                foreach (var item in items)
                {
                    Add(item);
                }

                return;
            }

            foreach (var item in items)
            {
                Items.Add(item);
            }

            RaiseReset();
        }

        public void InsertRangeAtStart(IReadOnlyList<T> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            if (items.Count <= PerItemNotificationLimit)
            {
                for (var i = items.Count - 1; i >= 0; i--)
                {
                    Insert(0, items[i]);
                }

                return;
            }

            for (var i = items.Count - 1; i >= 0; i--)
            {
                Items.Insert(0, items[i]);
            }

            RaiseReset();
        }

        public void RemoveFirst(int count)
        {
            if (count <= 0)
            {
                return;
            }

            if (count <= PerItemNotificationLimit)
            {
                for (var i = 0; i < count; i++)
                {
                    RemoveAt(0);
                }

                return;
            }

            for (var i = 0; i < count; i++)
            {
                Items.RemoveAt(0);
            }

            RaiseReset();
        }

        public void RemoveLast(int count)
        {
            if (count <= 0)
            {
                return;
            }

            if (count <= PerItemNotificationLimit)
            {
                for (var i = 0; i < count; i++)
                {
                    RemoveAt(Count - 1);
                }

                return;
            }

            for (var i = 0; i < count; i++)
            {
                Items.RemoveAt(Items.Count - 1);
            }

            RaiseReset();
        }

        public void UpdateEnds(IReadOnlyList<T> incoming, int removeCount, bool prepend)
        {
            if (removeCount <= PerItemNotificationLimit - incoming.Count)
            {
                if (prepend)
                {
                    RemoveLast(removeCount);
                    InsertRangeAtStart(incoming);
                }
                else
                {
                    RemoveFirst(removeCount);
                    AddRange(incoming);
                }
                return;
            }

            int removeIndex = prepend ? Items.Count - removeCount : 0;
            if (Items is List<T> list)
            {
                list.RemoveRange(removeIndex, removeCount);
                list.InsertRange(prepend ? 0 : list.Count, incoming);
            }
            else
            {
                for (int i = 0; i < removeCount; i++) Items.RemoveAt(removeIndex);
                int insertIndex = prepend ? 0 : Items.Count;
                foreach (T item in incoming) Items.Insert(insertIndex++, item);
            }
            RaiseReset();
        }

        private void RaiseReset()
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
