using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Themes;

/// <summary>Mirrors the owning selector's alignment without a transient ancestor binding.</summary>
public static class ComboBoxItemAlignment
{
    public static readonly DependencyProperty SyncWithOwnerProperty = DependencyProperty.RegisterAttached(
        "SyncWithOwner",
        typeof(bool),
        typeof(ComboBoxItemAlignment),
        new PropertyMetadata(false, OnSyncWithOwnerChanged));

    private static readonly DependencyProperty BoundOwnerProperty = DependencyProperty.RegisterAttached(
        "BoundOwner",
        typeof(ItemsControl),
        typeof(ComboBoxItemAlignment));

    public static bool GetSyncWithOwner(DependencyObject element) => (bool)element.GetValue(SyncWithOwnerProperty);

    public static void SetSyncWithOwner(DependencyObject element, bool value) => element.SetValue(SyncWithOwnerProperty, value);

    private static void OnSyncWithOwnerChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not ComboBoxItem item)
            return;

        item.Loaded -= Item_Loaded;
        item.Unloaded -= Item_Unloaded;
        ClearOwnerBindings(item);

        if ((bool)e.NewValue)
        {
            item.Loaded += Item_Loaded;
            item.Unloaded += Item_Unloaded;
            if (item.IsLoaded)
                BindToOwner(item);
        }
    }

    private static void Item_Loaded(object sender, RoutedEventArgs e) => BindToOwner((ComboBoxItem)sender);

    private static void Item_Unloaded(object sender, RoutedEventArgs e) => ClearOwnerBindings((ComboBoxItem)sender);

    private static void BindToOwner(ComboBoxItem item)
    {
        ClearOwnerBindings(item);
        ItemsControl? owner = ItemsControl.ItemsControlFromItemContainer(item);
        if (owner == null)
            return;

        bool bound = BindIfUnset(item, Control.HorizontalContentAlignmentProperty, owner, nameof(Control.HorizontalContentAlignment));
        bound |= BindIfUnset(item, Control.VerticalContentAlignmentProperty, owner, nameof(Control.VerticalContentAlignment));
        if (bound)
            item.SetValue(BoundOwnerProperty, owner);
    }

    private static bool BindIfUnset(ComboBoxItem item, DependencyProperty property, ItemsControl owner, string path)
    {
        if (item.ReadLocalValue(property) != DependencyProperty.UnsetValue)
            return false;

        BindingOperations.SetBinding(item, property, new Binding(path)
        {
            Source = owner,
            Mode = BindingMode.OneWay,
        });
        return true;
    }

    private static void ClearOwnerBindings(ComboBoxItem item)
    {
        if (item.GetValue(BoundOwnerProperty) is not ItemsControl owner)
            return;

        ClearIfOwned(item, Control.HorizontalContentAlignmentProperty, owner);
        ClearIfOwned(item, Control.VerticalContentAlignmentProperty, owner);
        item.ClearValue(BoundOwnerProperty);
    }

    private static void ClearIfOwned(ComboBoxItem item, DependencyProperty property, ItemsControl owner)
    {
        BindingExpression? expression = BindingOperations.GetBindingExpression(item, property);
        if (ReferenceEquals(expression?.ParentBinding.Source, owner))
            BindingOperations.ClearBinding(item, property);
    }
}
