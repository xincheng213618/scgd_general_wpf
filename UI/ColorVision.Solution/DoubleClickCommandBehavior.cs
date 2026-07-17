using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution
{
    public static class DoubleClickCommandBehavior
    {
        public static readonly DependencyProperty DoubleClickCommandProperty =
            DependencyProperty.RegisterAttached(
                "DoubleClickCommand",
                typeof(ICommand),
                typeof(DoubleClickCommandBehavior),
                new UIPropertyMetadata(DoubleClickCommandChanged));

        public static void SetDoubleClickCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(DoubleClickCommandProperty, value);
        }

        public static ICommand GetDoubleClickCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(DoubleClickCommandProperty);
        }

        private static void DoubleClickCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is Control control)
            {
                if (e.OldValue != null)
                    control.MouseDoubleClick -= OnMouseDoubleClick;
                if (e.NewValue != null)
                    control.MouseDoubleClick += OnMouseDoubleClick;
            }
        }

        private static void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem treeviewItem && treeviewItem.IsSelected == false)
                return;
            if (sender is not Control control
                || control.GetValue(DoubleClickCommandProperty) is not ICommand command)
            {
                return;
            }

            if (command is RoutedCommand routedCommand)
            {
                if (!routedCommand.CanExecute(null, control))
                    return;
                routedCommand.Execute(null, control);
            }
            else
            {
                if (!command.CanExecute(null))
                    return;
                command.Execute(null);
            }
            e.Handled = true;
        }
    }
}
