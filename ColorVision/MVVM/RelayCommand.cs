#pragma warning disable CS8604
using System;
using System.Windows.Input;

namespace ColorVision.MVVM
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
}
