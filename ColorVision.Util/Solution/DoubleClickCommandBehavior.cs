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
                if (e.NewValue != null)
                {
                    control.MouseDoubleClick += OnMouseDoubleClick;
                }
                else
                {
                    control.MouseDoubleClick -= OnMouseDoubleClick;
                }
            }
        }

        private static void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem treeviewItem && treeviewItem.IsSelected == false)
                return;
            if (sender is Control control && control.GetValue(DoubleClickCommandProperty) is ICommand command && command.CanExecute(null))
                command.Execute(null);
        }
    }
}
