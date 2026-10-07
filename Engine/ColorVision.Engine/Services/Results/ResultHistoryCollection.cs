using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Engine.Services.Results
{
    // Queries load fully; the next live insertion applies the usual limit to the same list.
    internal sealed class ResultHistoryCollection<T>(Func<T, int> getId, int maxCount) : ObservableCollection<T> where T : class
    {
        private readonly SortedDictionary<int, T> _byId = new();
        private Collection<T>? _queryResults;
        private int _maxCount = Math.Max(1, maxCount);

        public IList<T> QueryResults => _queryResults ??= new QueryResultList(this);

        public int MaxCount
        {
            get => _maxCount;
            set
            {
                _maxCount = Math.Max(1, value);
                while (Count > _maxCount) RemoveOldest();
            }
        }

        public bool ContainsId(int id) => _byId.ContainsKey(id);

        protected override void InsertItem(int index, T item)
        {
            CheckReentrancy();
            int id = getId(item);
            if (_byId.ContainsKey(id)) return;
            while (Count >= MaxCount)
            {
                int oldestId = _byId.First().Key;
                // An old notification must not evict a newer record already in the bounded history.
                if (Count == MaxCount && id < oldestId) return;
                int removedIndex = IndexOf(_byId[oldestId]);
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

        private void RemoveOldest() => Remove(_byId.First().Value);

        private void InsertQueryResult(int index, T item)
        {
            CheckReentrancy();
            int id = getId(item);
            if (_byId.ContainsKey(id)) return;
            _byId.Add(id, item);
            base.InsertItem(index, item);
        }

        // The existing query API writes through IList<T>; this adapter preserves its loading contract.
        private sealed class QueryResultList(ResultHistoryCollection<T> history) : Collection<T>(history)
        {
            protected override void InsertItem(int index, T item) => history.InsertQueryResult(index, item);
        }
    }
}
