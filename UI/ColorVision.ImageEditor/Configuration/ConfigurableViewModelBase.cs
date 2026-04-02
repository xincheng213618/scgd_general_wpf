using ColorVision.Common.MVVM;
using System;
using System.ComponentModel;
using System.Windows.Input;

namespace ColorVision.ImageEditor.Configuration
{
    /// <summary>
    /// 支持配置和命令管理的视图模型基类
    /// </summary>
    public abstract class ConfigurableViewModelBase : ViewModelBase
    {
        private ICommandManager _commandManager;
        private IEditorConfiguration _configuration;

        /// <summary>
        /// 命令管理器
        /// </summary>
        public ICommandManager CommandManager
        {
            get
            {
                if (_commandManager == null)
                {
                    // 尝试从服务定位器获取
                    if (ServiceLocator.Instance.TryGetService<ICommandManager>(out var cm))
                    {
                        _commandManager = cm;
                    }
                    else
                    {
                        // 创建默认实例
                        _commandManager = new CommandManager();
                    }
                }
                return _commandManager;
            }
            protected set => _commandManager = value;
        }

        /// <summary>
        /// 配置
        /// </summary>
        public IEditorConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    if (ServiceLocator.Instance.TryGetService<IEditorConfiguration>(out var config))
                    {
                        _configuration = config;
                    }
                }
                return _configuration;
            }
            protected set => _configuration = value;
        }

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public bool CanUndo => CommandManager?.CanUndo ?? false;

        /// <summary>
        /// 是否可以重做
        /// </summary>
        public bool CanRedo => CommandManager?.CanRedo ?? false;

        /// <summary>
        /// Undo 命令
        /// </summary>
        public System.Windows.Input.ICommand UndoCommand { get; }

        /// <summary>
        /// Redo 命令
        /// </summary>
        public System.Windows.Input.ICommand RedoCommand { get; }

        /// <summary>
        /// 开始事务命令
        /// </summary>
        public System.Windows.Input.ICommand BeginTransactionCommand { get; }

        /// <summary>
        /// 提交事务命令
        /// </summary>
        public System.Windows.Input.ICommand CommitTransactionCommand { get; }

        protected ConfigurableViewModelBase()
        {
            UndoCommand = new RelayCommand(() => Undo(), () => CanUndo);
            RedoCommand = new RelayCommand(() => Redo(), () => CanRedo);
            BeginTransactionCommand = new RelayCommand<string>(name => BeginTransaction(name));
            CommitTransactionCommand = new RelayCommand(CommitTransaction, () => IsInTransaction);

            // 监听命令管理器事件
            if (CommandManager != null)
            {
                CommandManager.CommandExecuted += OnCommandExecuted;
                CommandManager.CommandUndone += OnCommandExecuted;
                CommandManager.CommandRedone += OnCommandExecuted;
            }
        }

        /// <summary>
        /// 执行命令（自动加入Undo栈）
        /// </summary>
        protected void ExecuteCommand(ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            CommandManager?.Execute(command);
            UpdateCommandStates();
        }

        /// <summary>
        /// 撤销
        /// </summary>
        protected void Undo()
        {
            CommandManager?.Undo();
            UpdateCommandStates();
        }

        /// <summary>
        /// 重做
        /// </summary>
        protected void Redo()
        {
            CommandManager?.Redo();
            UpdateCommandStates();
        }

        /// <summary>
        /// 开始事务
        /// </summary>
        protected void BeginTransaction(string name = null)
        {
            if (CommandManager is ITransactionalCommandManager transactional)
            {
                transactional.BeginTransaction(name ?? "批量操作");
                UpdateCommandStates();
            }
        }

        /// <summary>
        /// 提交事务
        /// </summary>
        protected void CommitTransaction()
        {
            if (CommandManager is ITransactionalCommandManager transactional)
            {
                transactional.CommitTransaction();
                UpdateCommandStates();
            }
        }

        /// <summary>
        /// 回滚事务
        /// </summary>
        protected void RollbackTransaction()
        {
            if (CommandManager is ITransactionalCommandManager transactional)
            {
                transactional.RollbackTransaction();
                UpdateCommandStates();
            }
        }

        /// <summary>
        /// 是否在事务中
        /// </summary>
        public bool IsInTransaction =>
            (CommandManager as ITransactionalCommandManager)?.IsInTransaction ?? false;

        /// <summary>
        /// 创建属性变更命令并执行
        /// </summary>
        protected void SetPropertyWithCommand<T>(
            ref T field,
            T value,
            string propertyName,
            Func<T> getter,
            Action<T> setter)
        {
            if (Equals(field, value))
                return;

            // 创建属性变更命令
            var command = new PropertyChangeCommand<ConfigurableViewModelBase, T>(
                this, propertyName, getter, setter, value);

            // 通过命令管理器执行
            ExecuteCommand(command);
        }

        /// <summary>
        /// 更新命令状态
        /// </summary>
        protected virtual void UpdateCommandStates()
        {
            ((RelayCommand)UndoCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RedoCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CommitTransactionCommand).RaiseCanExecuteChanged();

            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(IsInTransaction));
        }

        /// <summary>
        /// 命令执行事件处理
        /// </summary>
        private void OnCommandExecuted(object sender, CommandExecutedEventArgs e)
        {
            UpdateCommandStates();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public virtual void Dispose()
        {
            if (CommandManager != null)
            {
                CommandManager.CommandExecuted -= OnCommandExecuted;
                CommandManager.CommandUndone -= OnCommandExecuted;
                CommandManager.CommandRedone -= OnCommandExecuted;
            }
        }
    }

    /// <summary>
    /// RelayCommand 实现 - 使用 WPF 的 ICommand 接口
    /// </summary>
    public class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object parameter)
        {
            _execute();
        }

        public event EventHandler CanExecuteChanged
        {
            add { System.Windows.Input.CommandManager.RequerySuggested += value; }
            remove { System.Windows.Input.CommandManager.RequerySuggested -= value; }
        }

        public void RaiseCanExecuteChanged()
        {
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// 带参数的 RelayCommand - 使用 WPF 的 ICommand 接口
    /// </summary>
    public class RelayCommand<T> : System.Windows.Input.ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (parameter is T typedParam)
            {
                return _canExecute?.Invoke(typedParam) ?? true;
            }
            return _canExecute?.Invoke(default) ?? true;
        }

        public void Execute(object parameter)
        {
            if (parameter is T typedParam)
            {
                _execute(typedParam);
            }
            else if (parameter == null && default(T) == null)
            {
                _execute(default);
            }
        }

        public event EventHandler CanExecuteChanged
        {
            add { System.Windows.Input.CommandManager.RequerySuggested += value; }
            remove { System.Windows.Input.CommandManager.RequerySuggested -= value; }
        }

        public void RaiseCanExecuteChanged()
        {
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }
}
