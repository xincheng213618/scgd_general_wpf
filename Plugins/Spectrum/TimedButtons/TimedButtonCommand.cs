using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Spectrum.TimedButtons
{
    internal static class SpectrumTimedButtonHost
    {
        public static FrameworkElement? GetOwner() => MainWindow.Instance;

        public static string BuildOperationKey(string actionKey)
        {
            return $"spectrum:{actionKey}";
        }
    }

    internal sealed class TimedButtonCommand : ICommand
    {
        private readonly Func<object?, Task<bool>> _executeAsync;
        private readonly Predicate<object?> _canExecute;
        private readonly Func<FrameworkElement?> _ownerProvider;
        private readonly Func<string, string> _operationKeyBuilder;
        private readonly string _actionKey;
        private readonly string? _runningText;
        private readonly Action<Exception>? _onException;

        public TimedButtonCommand(
            Func<object?, Task<bool>> executeAsync,
            Predicate<object?> canExecute,
            Func<FrameworkElement?> ownerProvider,
            Func<string, string> operationKeyBuilder,
            string actionKey,
            string? runningText = null,
            Action<Exception>? onException = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute ?? (_ => true);
            _ownerProvider = ownerProvider ?? throw new ArgumentNullException(nameof(ownerProvider));
            _operationKeyBuilder = operationKeyBuilder ?? throw new ArgumentNullException(nameof(operationKeyBuilder));
            _actionKey = actionKey ?? throw new ArgumentNullException(nameof(actionKey));
            _runningText = runningText;
            _onException = onException;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute(GetLogicalParameter(parameter));
        }

        public async void Execute(object? parameter)
        {
            (Button? button, object? logicalParameter) = ResolveParameter(parameter);
            FrameworkElement? owner = _ownerProvider();
            TimedButtonOperationScope? operationScope = null;
            bool success = false;

            try
            {
                if (owner != null && button != null)
                {
                    TimedButtonOperationRegistry operations = owner.GetTimedButtonOperations(_operationKeyBuilder);
                    operations.Register(button, _actionKey);
                    operationScope = operations.Begin(button, runningText: _runningText);
                }

                success = await _executeAsync(logicalParameter);
            }
            catch (Exception ex)
            {
                _onException?.Invoke(ex);
                success = false;
            }
            finally
            {
                operationScope?.Complete(success);
            }
        }

        private static object? GetLogicalParameter(object? parameter)
        {
            return ResolveParameter(parameter).LogicalParameter;
        }

        private static (Button? Button, object? LogicalParameter) ResolveParameter(object? parameter)
        {
            if (parameter is Button button)
            {
                return (button, button.Tag);
            }

            return (null, parameter);
        }
    }
}
