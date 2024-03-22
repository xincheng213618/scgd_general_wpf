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
        private readonly Action<object> Execute;
        private readonly Predicate<object> CanExecute;

        //Func<object,bool> =>Predicate<object> ss
        public RelayCommand(Action<object> execute)
        {
            this.Execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.CanExecute = a => true;
        }
        public RelayCommand(Action<object> execute, Predicate<object> canExecute)
        {
            this.Execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.CanExecute = canExecute;
        }

        bool ICommand.CanExecute(object? parameter) => CanExecute is null || CanExecute(parameter);

        event EventHandler? ICommand.CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        void ICommand.Execute(object? parameter) => Execute(parameter);

        public void RaiseExecute(object parameter) => Execute(parameter);
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
            return this._canExecute == null || this._canExecute(parameter);
        }

        public void Execute(T? parameter)
        {
            this._action(parameter);
        }

        public bool CanExecute(object? parameter)
        {
            if (TryGetCommandArg(parameter, out T? res))
            {
                return this.CanExecute(res);
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
