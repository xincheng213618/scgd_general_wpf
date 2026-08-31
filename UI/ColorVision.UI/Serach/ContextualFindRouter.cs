using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ColorVision.UI.Serach;

/// <summary>Routes Find to the focused content without simulating keys or searching another document.</summary>
public static class ContextualFindRouter
{
    public static readonly DependencyProperty LocalFindCommandProperty = DependencyProperty.RegisterAttached(
        "LocalFindCommand", typeof(ICommand), typeof(ContextualFindRouter), new PropertyMetadata(null));

    public static ICommand? GetLocalFindCommand(DependencyObject element) => (ICommand?)element.GetValue(LocalFindCommandProperty);
    public static void SetLocalFindCommand(DependencyObject element, ICommand? value) => element.SetValue(LocalFindCommandProperty, value);

    /// <returns>True when local content owns Find, including when that local action is unavailable.</returns>
    public static bool TryFind(IInputElement? focused, DependencyObject scope)
    {
        if (!IsWithin(focused, scope)) return false;
        if (ApplicationCommands.Find.CanExecute(null, focused))
        {
            ApplicationCommands.Find.Execute(null, focused);
            return true;
        }

        bool textInput = false;
        for (DependencyObject? current = focused as DependencyObject; current != null; current = GetParent(current))
        {
            if (GetLocalFindCommand(current) is { } localCommand)
            {
                if (localCommand is RoutedCommand routed)
                {
                    if (routed.CanExecute(null, focused)) routed.Execute(null, focused);
                }
                else if (localCommand.CanExecute(null)) localCommand.Execute(null);
                return true;
            }

            CommandBindingCollection? bindings = current switch
            {
                UIElement element => element.CommandBindings,
                ContentElement element => element.CommandBindings,
                _ => null
            };
            if (bindings?.Cast<CommandBinding>().Any(binding => binding.Command == ApplicationCommands.Find) == true)
                return true;

            // Embedded native editors and text entry keep their own semantics even when
            // they do not expose WPF Find. The explicit command-palette action remains available.
            textInput |= current is TextBoxBase or PasswordBox or HwndHost;
            if (ReferenceEquals(current, scope)) break;
        }
        return textInput;
    }

    public static bool IsWithin(IInputElement? element, DependencyObject scope)
    {
        for (DependencyObject? current = element as DependencyObject; current != null; current = GetParent(current))
            if (ReferenceEquals(current, scope)) return true;
        return false;
    }

    private static DependencyObject? GetParent(DependencyObject element)
    {
        if (element is ContentElement content)
            return ContentOperations.GetParent(content) ?? (content as FrameworkContentElement)?.Parent;
        DependencyObject? visualParent = element is Visual or Visual3D ? VisualTreeHelper.GetParent(element) : null;
        return visualParent ?? (element as FrameworkElement)?.Parent ?? LogicalTreeHelper.GetParent(element);
    }
}
