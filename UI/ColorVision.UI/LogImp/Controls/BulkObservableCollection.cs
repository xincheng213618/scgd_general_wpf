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

        private void RaiseReset()
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
