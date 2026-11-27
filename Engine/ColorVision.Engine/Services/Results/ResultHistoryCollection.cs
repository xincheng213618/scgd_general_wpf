using ColorVision.Database;
using ColorVision.UI.Sorts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.Engine.Services.Results
{
    // Queries load fully; the next live insertion applies the usual limit to the same list.
    internal sealed class ResultHistoryCollection<T>(Func<T, int> getId, int maxCount) : ObservableCollection<T>, ISortableCollection where T : class
    {
        private const int BatchSortMinimumCount = 1000;
        private SortedDictionary<int, T> _byId = new();
        private IQueryResultList<T>? _queryResults;
        private int _maxCount = Math.Max(1, maxCount);

        public IQueryResultList<T> QueryResults => _queryResults ??= new QueryResultList(this);

        public int MaxCount
        {
            get => _maxCount;
            set
            {
                _maxCount = Math.Max(1, value);
                TrimToCount(_maxCount, 0);
            }
        }

        public bool ContainsId(int id) => _byId.ContainsKey(id);

        bool ISortableCollection.TrySortBy(Func<object, object?> keySelector, IComparer<object?> keyComparer)
        {
            if (Count < BatchSortMinimumCount) return false;
            CheckReentrancy();
            T[] sorted = Items.OrderBy(keySelector.Invoke, keyComparer).ToArray();
            int firstChanged = 0;
            while (firstChanged < sorted.Length && ReferenceEquals(Items[firstChanged], sorted[firstChanged]))
                firstChanged++;

            if (firstChanged == sorted.Length) return true;
            for (int i = firstChanged; i < sorted.Length; i++)
                Items[i] = sorted[i];

            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            return true;
        }

        protected override void InsertItem(int index, T item)
        {
            CheckReentrancy();
            int id = getId(item);
            if (_byId.ContainsKey(id)) return;
            if (Count > MaxCount)
                index = TrimToCount(MaxCount, index);
            if (Count == MaxCount)
            {
                var oldest = _byId.First();
                // An old notification must not evict a newer record already in the bounded history.
                if (id < oldest.Key) return;
                int removedIndex = IndexOf(oldest.Value);
                RemoveAt(removedIndex);
                if (removedIndex < index) index--;
            }
            _byId.Add(id, item);
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            CheckReentrancy();
            int id = getId(this[index]);
            _byId.Remove(id);
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, T item)
        {
            CheckReentrancy();
            int id = getId(item);
            if (_byId.TryGetValue(id, out T? existing) && !ReferenceEquals(existing, this[index]))
                throw new InvalidOperationException("A result with this ID is already in the history.");
            int oldId = getId(this[index]);
            _byId.Remove(oldId);
            _byId.Add(id, item);
            base.SetItem(index, item);
        }

        protected override void ClearItems()
        {
            CheckReentrancy();
            _byId.Clear();
            base.ClearItems();
        }

        private int TrimToCount(int limit, int insertionIndex)
        {
            int removeCount = Count - limit;
            if (removeCount <= 0) return insertionIndex;
            if (removeCount == 1)
            {
                int removedIndex = IndexOf(_byId.First().Value);
                RemoveAt(removedIndex);
                return removedIndex < insertionIndex ? insertionIndex - 1 : insertionIndex;
            }

            CheckReentrancy();
            int cutoffId = _byId.Keys.ElementAt(removeCount - 1);
            // Rebuild only when the discarded rows substantially outnumber the retained rows.
            var retainedById = removeCount >= 2L * limit ? new SortedDictionary<int, T>() : null;
            int retainedCount = 0;
            int adjustedIndex = insertionIndex;
            for (int index = 0; index < Count; index++)
            {
                T item = Items[index];
                int id = getId(item);
                if (id <= cutoffId)
                {
                    if (retainedById == null) _byId.Remove(id);
                    if (index < insertionIndex) adjustedIndex--;
                    continue;
                }
                if (retainedCount != index) Items[retainedCount] = item;
                retainedById?.Add(id, item);
                retainedCount++;
            }
            // WPF observes the finished list once instead of reprocessing every removed row.
            if (Items is List<T> list)
                list.RemoveRange(retainedCount, Count - retainedCount);
            else
                while (Count > retainedCount) Items.RemoveAt(Count - 1);
            if (retainedById != null) _byId = retainedById;
            PublishReset();
            return adjustedIndex;
        }

        private void PublishReset()
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void AddQueryResults(IEnumerable<T> rows)
        {
            CheckReentrancy();
            int initialCount = Count;
            try
            {
                foreach (T row in rows)
                    if (_byId.TryAdd(getId(row), row)) Items.Add(row);
            }
            finally
            {
                if (Count != initialCount) PublishReset();
            }
        }

        private void InsertQueryResult(int index, T item)
        {
            CheckReentrancy();
            int id = getId(item);
            if (_byId.ContainsKey(id)) return;
            _byId.Add(id, item);
            base.InsertItem(index, item);
        }

        // Query writes may exceed the live retention limit.
        private sealed class QueryResultList(ResultHistoryCollection<T> history) : Collection<T>(history), IQueryResultList<T>
        {
            public void AddRange(IEnumerable<T> rows) => history.AddQueryResults(rows);
            protected override void InsertItem(int index, T item) => history.InsertQueryResult(index, item);
        }
    }
}
