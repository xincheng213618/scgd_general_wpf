using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Serach;

internal static class SearchCommandExecutor
{
    internal static bool CanExecute(ICommand? command, object? parameter, IInputElement? target)
    {
        try
        {
            return command is RoutedCommand routed
                ? target != null && routed.CanExecute(parameter, target)
                : command?.CanExecute(parameter) == true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException) { return false; }
    }

    internal static bool TryExecute(ICommand? command, object? parameter, IInputElement? target, Func<bool>? validateTarget = null)
    {
        // Recheck after restoring focus: closing the palette can change command availability.
        if (!CanExecute(command, parameter, target)) return false;
        if (validateTarget?.Invoke() == false) return false;
        if (command is RoutedCommand routed) routed.Execute(parameter, target!);
        else command!.Execute(parameter);
        return true;
    }
}
