﻿using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace ColorVision.Common.Collections
{
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        private bool updatesEnabled = true;
        private bool collectionChanged;
        private readonly object _syncRoot = new object();

        public void SuspendUpdate()
        {
            lock (_syncRoot)
            {
                updatesEnabled = false;
            }
        }

        public void ResumeUpdate()
        {
            lock (_syncRoot)
            {
                updatesEnabled = true;
                if (collectionChanged)
                {
                    collectionChanged = false;
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            lock (_syncRoot)
            {
                if (updatesEnabled)
                {
                    base.OnCollectionChanged(e);
                }
                else
                {
                    collectionChanged = true;
                }
            }
        }

        public void AddRange(IEnumerable<T> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            lock (_syncRoot)
            {
                SuspendUpdate();
                foreach (var item in items)
                {
                    Items.Add(item);
                }
                ResumeUpdate();
            }
        }
    }
}