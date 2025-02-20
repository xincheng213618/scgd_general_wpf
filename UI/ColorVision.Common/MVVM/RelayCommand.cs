#pragma warning disable CS8604
using System;
using System.Windows.Input;

namespace ColorVision.Common.MVVM
{
    /// <summary>
    ///  Implements the <see cref="ICommand"/> interface
    /// </summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object> execute;
        private readonly Predicate<object> canExecute;

        //Func<object,bool> =>Predicate<object> ss
        public RelayCommand(Action<object> execute)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            canExecute = a => true;
        }
        public RelayCommand(Action<object> execute, Predicate<object> canExecute)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => canExecute is null || canExecute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        public void Execute(object? parameter) => execute(parameter);

        public void RaiseExecute(object parameter) => execute(parameter);
    }


    public class RelayCommand<T> : ICommand 
    {
        Action<T?> _action;
        Func<T?, bool> _canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(T? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(T? parameter)
        {
            _action(parameter);
        }

        public bool CanExecute(object? parameter)
        {
            if (TryGetCommandArg(parameter, out T? res))
            {
                return CanExecute(res);
            }
            return false;
        }

        public void Execute(object? parameter)
        {
            if (TryGetCommandArg(parameter, out T? res))
            {
                Execute(res);
            }
        }

        public RelayCommand(Action<T?> action, Func<T?, bool> canExecute)
        {
            _action = action;
            _canExecute = canExecute;
        }

        private static bool TryGetCommandArg(object? parameter, out T? arg)
        {
            if (parameter is null && default(T) is null)
            {
                arg = default(T);
                return true;
            }

            if (parameter is T argument)
            {
                arg = argument;
                return true;
            }

            arg = default(T);
            return false;
        }
    }




}
