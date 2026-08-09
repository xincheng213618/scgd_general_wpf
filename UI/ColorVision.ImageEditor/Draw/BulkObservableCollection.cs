using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Draw
{
    internal sealed class BulkObservableCollection<T> : ObservableCollection<T>
    {
        private const int PerItemNotificationLimit = 32;

        public void AddRange(IReadOnlyList<T> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            if (items.Count <= PerItemNotificationLimit)
            {
                foreach (T item in items)
                {
                    Add(item);
                }
                return;
            }

            foreach (T item in items)
            {
                Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
