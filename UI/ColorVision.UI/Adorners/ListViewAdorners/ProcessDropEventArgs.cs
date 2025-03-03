// Copyright (C) Josh Smith - January 2007
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Common.Adorners.ListViewAdorners
{
    /// <summary>
    /// Event arguments used by the ListViewDragDropManager.ProcessDrop event.
    /// </summary>
    /// <typeparam name="T">The type of data object being dropped.</typeparam>
    public class ProcessDropEventArgs<T> : EventArgs where T : class
    {
        #region Data

        ObservableCollection<T> itemsSource;
        T dataItem;
        int oldIndex;
        int newIndex;
        DragDropEffects allowedEffects = DragDropEffects.None;
        DragDropEffects effects = DragDropEffects.None;

        #endregion // Data

        #region Constructor

        internal ProcessDropEventArgs(
            ObservableCollection<T> itemsSource,
            T dataItem,
            int oldIndex,
            int newIndex,
            DragDropEffects allowedEffects)
        {
            this.itemsSource = itemsSource;
            this.dataItem = dataItem;
            this.oldIndex = oldIndex;
            this.newIndex = newIndex;
            this.allowedEffects = allowedEffects;
        }

        #endregion // Constructor

        #region Public Properties

        /// <summary>
        /// The items source of the ListView where the drop occurred.
        /// </summary>
        public ObservableCollection<T> ItemsSource
        {
            get { return this.itemsSource; }
        }

        /// <summary>
        /// The data object which was dropped.
        /// </summary>
        public T DataItem
        {
            get { return this.dataItem; }
        }

        /// <summary>
        /// The current index of the data item being dropped, in the ItemsSource collection.
        /// </summary>
        public int OldIndex
        {
            get { return this.oldIndex; }
        }

        /// <summary>
        /// The target index of the data item being dropped, in the ItemsSource collection.
        /// </summary>
        public int NewIndex
        {
            get { return this.newIndex; }
        }

        /// <summary>
        /// The drag drop effects allowed to be performed.
        /// </summary>
        public DragDropEffects AllowedEffects
        {
            get { return allowedEffects; }
        }

        /// <summary>
        /// The drag drop effect(s) performed on the dropped item.
        /// </summary>
        public DragDropEffects Effects
        {
            get { return effects; }
            set { effects = value; }
        }

        #endregion // Public Properties
    }
}