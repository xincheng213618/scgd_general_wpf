#pragma warning disable CS8625,CS8603,CA1822,CS8604,CS8601,CS8601
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Common.Adorners.ListViewAdorners
{
    public class ListViewDragDropManager<T> where T : class
    {
        #region Data

        bool canInitiateDrag;
        DragAdorner dragAdorner;
        double dragAdornerOpacity;
        int indexToSelect;
        bool isDragInProgress;
        T itemUnderDragCursor;
        ListView listView;
        Point ptMouseDown;
        bool showDragAdorner;

        #endregion // Data

        #region Constructors

        /// <summary>
        /// Initializes a new instance of ListViewDragManager.
        /// </summary>
        public ListViewDragDropManager()
        {
            this.canInitiateDrag = false;
            this.dragAdornerOpacity = 0.7;
            this.indexToSelect = -1;
            this.showDragAdorner = true;
        }

        public EventHandler<List<T>> EventHandler { get; set; }

        /// <summary>
        /// Initializes a new instance of ListViewDragManager.
        /// </summary>
        /// <param name="listView"></param>
        public ListViewDragDropManager(ListView listView)
            : this()
        {
            this.ListView = listView;
        }

        /// <summary>
        /// Initializes a new instance of ListViewDragManager.
        /// </summary>
        /// <param name="listView"></param>
        /// <param name="dragAdornerOpacity"></param>
        public ListViewDragDropManager(ListView listView, double dragAdornerOpacity)
            : this(listView)
        {
            this.DragAdornerOpacity = dragAdornerOpacity;
        }

        /// <summary>
        /// Initializes a new instance of ListViewDragManager.
        /// </summary>
        /// <param name="listView"></param>
        /// <param name="showDragAdorner"></param>
        public ListViewDragDropManager(ListView listView, bool showDragAdorner)
            : this(listView)
        {
            this.ShowDragAdorner = showDragAdorner;
        }

        #endregion // Constructors

        #region Public Interface

        #region DragAdornerOpacity

        /// <summary>
        /// Gets/sets the opacity of the drag adorner.  This property has no
        /// effect if ShowDragAdorner is false. The default value is 0.7
        /// </summary>
        public double DragAdornerOpacity
        {
            get { return this.dragAdornerOpacity; }
            set
            {
                if (this.IsDragInProgress)
                    throw new InvalidOperationException("Cannot set the DragAdornerOpacity property during a drag operation.");

                if (value < 0.0 || value > 1.0)
                    throw new ArgumentOutOfRangeException("DragAdornerOpacity", value, "Must be between 0 and 1.");

                this.dragAdornerOpacity = value;
            }
        }

        #endregion // DragAdornerOpacity

        #region IsDragInProgress

        /// <summary>
        /// Returns true if there is currently a drag operation being managed.
        /// </summary>
        public bool IsDragInProgress
        {
            get { return this.isDragInProgress; }
            private set { this.isDragInProgress = value; }
        }

        /// <summary>
        /// Gets or sets optional minimal distance mouse pointer has to be dragged horizontally before it is considered a drag operation.
        /// If value is not set, <see cref="SystemParameters.MinimumHorizontalDragDistance"/> is used.
        /// Set to <see cref="Double.PositiveInfinity"/> to prevent horizontal drag
        /// (which does not make sense with standard ListView's vertical layout).
        /// </summary>
        public double? MinimumHorizontalDragDistance { get; set; }
        /// <summary>
        /// Gets or sets optional minimal distance mouse pointer has to be dragged vertically before it is considered a drag operation.
        /// If value is not set, <see cref="SystemParameters.MinimumVerticalDragDistance"/> is used.
        /// Set to <see cref="Double.PositiveInfinity"/> to prevent vertical drag.
        /// </summary>
        public double? MinimumVerticalDragDistance { get; set; }

        /// <summary>
        /// Gets or sets precondition for dragging. If set to <c>null</c> (default value) drag is always allowed.
        /// Provided <see cref="Point"/> is the mouse position relative to the list view.
        /// </summary>
        public Func<Point, bool> DraggingPrecondition { get; set; }

        #endregion // IsDragInProgress

        #region ListView

        /// <summary>
        /// Gets/sets the ListView whose dragging is managed.  This property
        /// can be set to null, to prevent drag management from occuring.  If
        /// the ListView's AllowDrop property is false, it will be set to true.
        /// </summary>
        public ListView ListView
        {
            get { return listView; }
            set
            {
                if (this.IsDragInProgress)
                    throw new InvalidOperationException("Cannot set the ListView property during a drag operation.");

                if (this.listView != null)
                {
                    #region Unhook Events

                    this.listView.PreviewMouseLeftButtonDown -= listView_PreviewMouseLeftButtonDown;
                    this.listView.PreviewMouseMove -= listView_PreviewMouseMove;
                    this.listView.DragOver -= listView_DragOver;
                    this.listView.DragLeave -= listView_DragLeave;
                    this.listView.DragEnter -= listView_DragEnter;
                    this.listView.Drop -= listView_Drop;

                    #endregion // Unhook Events
                }

                this.listView = value;

                if (this.listView != null)
                {
                    if (!this.listView.AllowDrop)
                        this.listView.AllowDrop = true;

                    #region Hook Events

                    this.listView.PreviewMouseLeftButtonDown += listView_PreviewMouseLeftButtonDown;
                    this.listView.PreviewMouseMove += listView_PreviewMouseMove;
                    this.listView.DragOver += listView_DragOver;
                    this.listView.DragLeave += listView_DragLeave;
                    this.listView.DragEnter += listView_DragEnter;
                    this.listView.Drop += listView_Drop;

                    #endregion // Hook Events
                }
            }
        }

        #endregion // ListView

        #region ProcessDrop [event]

        /// <summary>
        /// Raised when a drop occurs.  By default the dropped item will be moved
        /// to the target index.  Handle this event if relocating the dropped item
        /// requires custom behavior.  Note, if this event is handled the default
        /// item dropping logic will not occur.
        /// </summary>
        public event EventHandler<ProcessDropEventArgs<T>> ProcessDrop;

        #endregion // ProcessDrop [event]

        #region ShowDragAdorner

        /// <summary>
        /// Gets/sets whether a visual representation of the ListViewItem being dragged
        /// follows the mouse cursor during a drag operation.  The default value is true.
        /// </summary>
        public bool ShowDragAdorner
        {
            get { return this.showDragAdorner; }
            set
            {
                if (this.IsDragInProgress)
                    throw new InvalidOperationException("Cannot set the ShowDragAdorner property during a drag operation.");

                this.showDragAdorner = value;
            }
        }

        #endregion // ShowDragAdorner

        #endregion // Public Interface

        #region Event Handling Methods

        #region listView_PreviewMouseLeftButtonDown

        void listView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (this.IsMouseOverScrollbar)
            {
                // 4/13/2007 - Set the flag to false when cursor is over scrollbar.
                this.canInitiateDrag = false;
                return;
            }

            Point position = e.GetPosition(this.listView);
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(listView, position);

            if (hitTestResult != null)
            {
                // Find the ListViewItem that was clicked
                ListViewItem item = FindAncestor<ListViewItem>(hitTestResult.VisualHit);

                if (item != null)
                {
                    this.canInitiateDrag = true;
                    this.ptMouseDown = position;
                    this.indexToSelect = listView.ItemContainerGenerator.IndexFromContainer(item);
                }
                else
                {
                    this.canInitiateDrag = false;
                    this.ptMouseDown = new Point(-10000, -10000);
                    this.indexToSelect = -1;
                }
            }
            else
            {
                this.canInitiateDrag = false;
                this.ptMouseDown = new Point(-10000, -10000);
                this.indexToSelect = -1;
            }
        }
        private static T1 FindAncestor<T1>(DependencyObject current) where T1 : DependencyObject
        {
            while (current != null)
            {
                if (current is T1)
                {
                    return (T1)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        #endregion // listView_PreviewMouseLeftButtonDown

        #region listView_PreviewMouseMove

        void listView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!this.CanStartDragOperation)
                return;

            // Select the item the user clicked on.
            if (this.listView.SelectedIndex != this.indexToSelect)
                this.listView.SelectedIndex = this.indexToSelect;

            // If the item at the selected index is null, there's nothing
            // we can do, so just return;
            if (this.listView.SelectedItem == null)
                return;

            ListViewItem itemToDrag = this.GetListViewItem(this.listView.SelectedIndex);
            if (itemToDrag == null)
                return;

            AdornerLayer adornerLayer = this.ShowDragAdornerResolved ? this.InitializeAdornerLayer(itemToDrag) : null;

            this.InitializeDragOperation(itemToDrag);
            this.PerformDragOperation();
            this.FinishDragOperation(itemToDrag, adornerLayer);
        }

        #endregion // listView_PreviewMouseMove

        #region listView_DragOver

        void listView_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;

            if (this.ShowDragAdornerResolved)
                this.UpdateDragAdornerLocation();

            // Update the item which is known to be currently under the drag cursor.
            int index = this.IndexUnderDragCursor;

            this.ItemUnderDragCursor = index < 0 ? null : this.ListView.Items[index] as T;
        }

        #endregion // listView_DragOver

        #region listView_DragLeave

        void listView_DragLeave(object sender, DragEventArgs e)
        {
            if (!this.IsMouseOver(this.listView))
            {
                if (this.ItemUnderDragCursor != null)
                    this.ItemUnderDragCursor = null;

                if (this.dragAdorner != null)
                    this.dragAdorner.Visibility = Visibility.Collapsed;
            }
        }

        #endregion // listView_DragLeave

        #region listView_DragEnter

        void listView_DragEnter(object sender, DragEventArgs e)
        {
            if (this.dragAdorner != null && this.dragAdorner.Visibility != Visibility.Visible)
            {
                // Update the location of the adorner and then show it.
                this.UpdateDragAdornerLocation();
                this.dragAdorner.Visibility = Visibility.Visible;
            }
        }

        #endregion // listView_DragEnter

        #region listView_Drop

        void listView_Drop(object sender, DragEventArgs e)
        {
            T data = ItemUnderDragCursor;

            if (this.ItemUnderDragCursor != null)
                this.ItemUnderDragCursor = null;

            e.Effects = DragDropEffects.None;

            //if (!e.Data.GetDataPresent(typeof(T)))
            //    return;

            // Get the data object which was dropped.
            //T data = e.Data.GetData(typeof(T)) as T;

            if (data == null)
                return;

            // Get the ObservableCollection<T> which contains the dropped data object.
            ObservableCollection<T> itemsSource = this.listView.ItemsSource as ObservableCollection<T>;
            if (itemsSource == null)
#pragma warning disable CA2201 // 不要引发保留的异常类型
                throw new Exception(
                    "A ListView managed by ListViewDragManager must have its ItemsSource set to an ObservableCollection<ItemType>.");
#pragma warning restore CA2201 // 不要引发保留的异常类型

            int oldIndex = indexToSelect;

            EventHandler.Invoke(this, new List<T> { data, itemsSource[indexToSelect] });

            int newIndex = itemsSource.IndexOf(data);
            if (newIndex < 0)
            {
                // The drag started somewhere else, and our ListView is empty
                // so make the new item the first in the list.
                if (itemsSource.Count == 0)
                    newIndex = 0;

                // The drag started somewhere else, but our ListView has items
                // so make the new item the last in the list.
                else if (oldIndex < 0)
                    newIndex = itemsSource.Count;

                // The user is trying to drop an item from our ListView into
                // our ListView, but the mouse is not over an item, so don't
                // let them drop it.
                else
                    return;
            }

            // Dropping an item back onto itself is not considered an actual 'drop'.
            if (oldIndex == newIndex)
                return;

            if (this.ProcessDrop != null)
            {
                // Let the client code process the drop.
                ProcessDropEventArgs<T> args = new ProcessDropEventArgs<T>(itemsSource, data, oldIndex, newIndex, e.AllowedEffects);
                this.ProcessDrop(this, args);
                e.Effects = args.Effects;
            }
            else
            {
                // Move the dragged data object from it's original index to the
                // new index (according to where the mouse cursor is).  If it was
                // not previously in the ListBox, then insert the item.
                if (oldIndex > -1)
                    itemsSource.Move(oldIndex, newIndex);
                else
                    itemsSource.Insert(newIndex, data);

                // Set the Effects property so that the call to DoDragDrop will return 'Move'.
                e.Effects = DragDropEffects.Move;
            }
        }

        #endregion // listView_Drop

        #endregion // Event Handling Methods

        #region Private Helpers

        #region CanStartDragOperation

        bool CanStartDragOperation
        {
            get
            {
                if (Mouse.LeftButton != MouseButtonState.Pressed)
                    return false;

                if (!this.canInitiateDrag)
                    return false;

                if (this.indexToSelect == -1)
                    return false;

                if (false.Equals(this.DraggingPrecondition?.Invoke(this.ptMouseDown)))
                    return false;

                return this.HasCursorLeftDragThreshold;
            }
        }

        #endregion // CanStartDragOperation

        #region FinishDragOperation

        void FinishDragOperation(ListViewItem draggedItem, AdornerLayer adornerLayer)
        {
            // Let the ListViewItem know that it is not being dragged anymore.
            ListViewItemDragState.SetIsBeingDragged(draggedItem, false);

            this.IsDragInProgress = false;

            if (this.ItemUnderDragCursor != null)
                this.ItemUnderDragCursor = null;

            // Remove the drag adorner from the adorner layer.
            if (adornerLayer != null)
            {
                adornerLayer.Remove(this.dragAdorner);
                this.dragAdorner = null;
            }
        }

        #endregion // FinishDragOperation

        #region GetListViewItem

        ListViewItem GetListViewItem(int index)
        {
            if (this.listView.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
                return null;

            return this.listView.ItemContainerGenerator.ContainerFromIndex(index) as ListViewItem;
        }

        ListViewItem GetListViewItem(T dataItem)
        {
            if (this.listView.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
                return null;

            return this.listView.ItemContainerGenerator.ContainerFromItem(dataItem) as ListViewItem;
        }

        #endregion // GetListViewItem

        #region HasCursorLeftDragThreshold

        bool HasCursorLeftDragThreshold
        {
            get
            {
                if (this.indexToSelect < 0)
                    return false;

                ListViewItem item = this.GetListViewItem(this.indexToSelect);
                Rect bounds = VisualTreeHelper.GetDescendantBounds(item);
                Point ptInItem = this.listView.TranslatePoint(this.ptMouseDown, item);

                // In case the cursor is at the very top or bottom of the ListViewItem
                // we want to make the vertical threshold very small so that dragging
                // over an adjacent item does not select it.
                double topOffset = Math.Abs(ptInItem.Y);
                double btmOffset = Math.Abs(bounds.Height - ptInItem.Y);
                double vertOffset = Math.Min(topOffset, btmOffset);

                double width = (this.MinimumHorizontalDragDistance ?? SystemParameters.MinimumHorizontalDragDistance) * 2;
                double height = Math.Min(this.MinimumVerticalDragDistance ?? SystemParameters.MinimumVerticalDragDistance, vertOffset) * 2;
                Size szThreshold = new Size(width, height);

                Rect rect = new Rect(this.ptMouseDown, szThreshold);
                rect.Offset(szThreshold.Width / -2, szThreshold.Height / -2);
                Point ptInListView = MouseUtilities.GetMousePosition(this.listView);
                return !rect.Contains(ptInListView);
            }
        }

        #endregion // HasCursorLeftDragThreshold

        #region IndexUnderDragCursor

        /// <summary>
        /// Returns the index of the ListViewItem underneath the
        /// drag cursor, or -1 if the cursor is not over an item.
        /// </summary>
        int IndexUnderDragCursor
        {
            get
            {
                int index = -1;
                for (int i = 0; i < this.listView.Items.Count; ++i)
                {
                    ListViewItem item = this.GetListViewItem(i);
                    if (item == null)
                    {
                        continue;
                    }
                    if (this.IsMouseOver(item))
                    {
                        index = i;
                        break;
                    }
                }
                return index;
            }
        }

        #endregion // IndexUnderDragCursor

        #region InitializeAdornerLayer

        AdornerLayer InitializeAdornerLayer(ListViewItem itemToDrag)
        {
            // Create a brush which will paint the ListViewItem onto
            // a visual in the adorner layer.
            VisualBrush brush = new VisualBrush(itemToDrag);

            // Create an element which displays the source item while it is dragged.
            this.dragAdorner = new DragAdorner(this.listView, itemToDrag.RenderSize, brush);

            // Set the drag adorner's opacity.
            this.dragAdorner.Opacity = this.DragAdornerOpacity;

            AdornerLayer layer = AdornerLayer.GetAdornerLayer(this.listView);
            layer.Add(dragAdorner);

            // Save the location of the cursor when the left mouse button was pressed.
            this.ptMouseDown = MouseUtilities.GetMousePosition(this.listView);

            return layer;
        }

        #endregion // InitializeAdornerLayer

        #region InitializeDragOperation

        void InitializeDragOperation(ListViewItem itemToDrag)
        {
            // Set some flags used during the drag operation.
            this.IsDragInProgress = true;
            this.canInitiateDrag = false;

            // Let the ListViewItem know that it is being dragged.
            ListViewItemDragState.SetIsBeingDragged(itemToDrag, true);
        }

        #endregion // InitializeDragOperation

        #region IsMouseOver

        bool IsMouseOver(Visual target)
        {
            // We need to use MouseUtilities to figure out the cursor
            // coordinates because, during a drag-drop operation, the WPF
            // mechanisms for getting the coordinates behave strangely.

            Rect bounds = VisualTreeHelper.GetDescendantBounds(target);
            Point mousePos = MouseUtilities.GetMousePosition(target);
            return bounds.Contains(mousePos);
        }

        #endregion // IsMouseOver

        #region IsMouseOverScrollbar

        /// <summary>
        /// Returns true if the mouse cursor is over a scrollbar in the ListView.
        /// </summary>
        bool IsMouseOverScrollbar
        {
            get
            {
                Point ptMouse = MouseUtilities.GetMousePosition(this.listView);
                HitTestResult res = VisualTreeHelper.HitTest(this.listView, ptMouse);
                if (res == null)
                    return false;

                DependencyObject depObj = res.VisualHit;
                while (depObj != null)
                {
                    if (depObj is ScrollBar)
                        return true;

                    // VisualTreeHelper works with objects of type Visual or Visual3D.
                    // If the current object is not derived from Visual or Visual3D,
                    // then use the LogicalTreeHelper to find the parent element.
                    if (depObj is Visual || depObj is System.Windows.Media.Media3D.Visual3D)
                        depObj = VisualTreeHelper.GetParent(depObj);
                    else
                        depObj = LogicalTreeHelper.GetParent(depObj);
                }

                return false;
            }
        }

        #endregion // IsMouseOverScrollbar

        #region ItemUnderDragCursor

        T ItemUnderDragCursor
        {
            get { return this.itemUnderDragCursor; }
            set
            {
                if (this.itemUnderDragCursor == value)
                    return;

                // The first pass handles the previous item under the cursor.
                // The second pass handles the new one.
                for (int i = 0; i < 2; ++i)
                {
                    if (i == 1)
                        this.itemUnderDragCursor = value;

                    if (this.itemUnderDragCursor != null)
                    {
                        ListViewItem listViewItem = this.GetListViewItem(this.itemUnderDragCursor);
                        if (listViewItem != null)
                            ListViewItemDragState.SetIsUnderDragCursor(listViewItem, i == 1);
                    }
                }
            }
        }

        #endregion // ItemUnderDragCursor

        #region PerformDragOperation

        void PerformDragOperation()
        {
            T selectedItem = this.listView.SelectedItem as T;
            DragDropEffects allowedEffects = DragDropEffects.Move | DragDropEffects.Move | DragDropEffects.Link;
            if (DragDrop.DoDragDrop(this.listView, selectedItem, allowedEffects) != DragDropEffects.None)
            {
                // The item was dropped into a new location,
                // so make it the new selected item.
                this.listView.SelectedItem = selectedItem;
            }
        }

        #endregion // PerformDragOperation

        #region ShowDragAdornerResolved

        bool ShowDragAdornerResolved
        {
            get { return this.ShowDragAdorner && this.DragAdornerOpacity > 0.0; }
        }

        #endregion // ShowDragAdornerResolved

        #region UpdateDragAdornerLocation

        void UpdateDragAdornerLocation()
        {
            if (this.dragAdorner != null)
            {
                Point ptCursor = MouseUtilities.GetMousePosition(this.ListView);

                double left = ptCursor.X - this.ptMouseDown.X;

                // 4/13/2007 - Made the top offset relative to the item being dragged.
                ListViewItem itemBeingDragged = this.GetListViewItem(this.indexToSelect);
                Point itemLoc = itemBeingDragged.TranslatePoint(new Point(0, 0), this.ListView);
                double top = itemLoc.Y + ptCursor.Y - this.ptMouseDown.Y;

                this.dragAdorner.SetOffsets(left, top);
            }
        }

        #endregion // UpdateDragAdornerLocation

        #endregion // Private Helpers
    }
}