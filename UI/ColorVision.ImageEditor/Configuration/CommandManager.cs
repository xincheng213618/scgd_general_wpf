using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.ImageEditor.Configuration
{
    /// <summary>
    /// 命令管理器实现 - 支持 Undo/Redo 和事务
    /// </summary>
    public class CommandManager : ITransactionalCommandManager
    {
        private readonly Stack<ICommand> _undoStack = new Stack<ICommand>();
        private readonly Stack<ICommand> _redoStack = new Stack<ICommand>();
        private readonly Stack<Transaction> _transactionStack = new Stack<Transaction>();

        public int MaxHistorySize { get; set; } = 100;
        public int UndoStackDepth => _undoStack.Count;
        public int RedoStackDepth => _redoStack.Count;
        public bool CanUndo => _undoStack.Count > 0 && !IsInTransaction;
        public bool CanRedo => _redoStack.Count > 0 && !IsInTransaction;

        public bool IsInTransaction => _transactionStack.Count > 0;
        public string CurrentTransactionName => _transactionStack.Count > 0 ? _transactionStack.Peek().Name : null;

        public event EventHandler<CommandExecutedEventArgs> CommandExecuted;
        public event EventHandler<CommandExecutedEventArgs> CommandUndone;
        public event EventHandler<CommandExecutedEventArgs> CommandRedone;

        public void Execute(ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            // 执行命令
            command.Execute();

            if (IsInTransaction)
            {
                // 在事务中，添加到当前事务
                _transactionStack.Peek().Commands.Add(command);
            }
            else
            {
                // 不在事务中，正常处理
                _undoStack.Push(command);
                _redoStack.Clear(); // 新命令会清空重做栈

                // 限制历史记录大小
                TrimHistoryIfNeeded();

                CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(command, CommandExecutionType.Execute));
            }
        }

        public void Undo()
        {
            if (!CanUndo)
                return;

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);

            CommandUndone?.Invoke(this, new CommandExecutedEventArgs(command, CommandExecutionType.Undo));
        }

        public void Redo()
        {
            if (!CanRedo)
                return;

            var command = _redoStack.Pop();
            command.Redo();
            _undoStack.Push(command);

            CommandRedone?.Invoke(this, new CommandExecutedEventArgs(command, CommandExecutionType.Redo));
        }

        public void UndoTo(Guid commandId)
        {
            if (!CanUndo)
                return;

            var commandsToUndo = new List<ICommand>();
            var tempStack = new Stack<ICommand>();

            // 找到目标命令
            while (_undoStack.Count > 0)
            {
                var cmd = _undoStack.Pop();
                tempStack.Push(cmd);
                commandsToUndo.Add(cmd);

                if (cmd.Id == commandId)
                    break;
            }

            // 恢复未匹配的命令到撤销栈
            while (tempStack.Count > 0 && tempStack.Peek().Id != commandId)
            {
                _undoStack.Push(tempStack.Pop());
            }

            // 执行撤销（从后往前）
            for (int i = commandsToUndo.Count - 1; i >= 0; i--)
            {
                commandsToUndo[i].Undo();
                _redoStack.Push(commandsToUndo[i]);
                CommandUndone?.Invoke(this, new CommandExecutedEventArgs(commandsToUndo[i], CommandExecutionType.Undo));
            }
        }

        public void RedoTo(Guid commandId)
        {
            if (!CanRedo)
                return;

            var commandsToRedo = new List<ICommand>();

            // 找到目标命令
            while (_redoStack.Count > 0)
            {
                var cmd = _redoStack.Pop();
                commandsToRedo.Add(cmd);

                cmd.Redo();
                _undoStack.Push(cmd);
                CommandRedone?.Invoke(this, new CommandExecutedEventArgs(cmd, CommandExecutionType.Redo));

                if (cmd.Id == commandId)
                    break;
            }
        }

        public void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        public IEnumerable<ICommand> GetUndoHistory()
        {
            return _undoStack.ToArray().Reverse();
        }

        public IEnumerable<ICommand> GetRedoHistory()
        {
            return _redoStack.ToArray().Reverse();
        }

        public void BeginTransaction(string transactionName)
        {
            _transactionStack.Push(new Transaction(transactionName));
        }

        public void CommitTransaction()
        {
            if (!IsInTransaction)
                throw new InvalidOperationException("当前不在事务中");

            var transaction = _transactionStack.Pop();

            if (transaction.Commands.Count == 0)
                return;

            if (transaction.Commands.Count == 1)
            {
                // 单个命令直接处理
                _undoStack.Push(transaction.Commands[0]);
            }
            else
            {
                // 多个命令包装为复合命令
                var composite = new CompositeCommand(transaction.Name, transaction.Commands);
                _undoStack.Push(composite);
            }

            _redoStack.Clear();
            TrimHistoryIfNeeded();

            CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(
                transaction.Commands.Last(), CommandExecutionType.Execute));
        }

        public void RollbackTransaction()
        {
            if (!IsInTransaction)
                throw new InvalidOperationException("当前不在事务中");

            var transaction = _transactionStack.Pop();

            // 撤销事务中所有命令（从后往前）
            for (int i = transaction.Commands.Count - 1; i >= 0; i--)
            {
                transaction.Commands[i].Undo();
            }
        }

        private void TrimHistoryIfNeeded()
        {
            while (_undoStack.Count > MaxHistorySize)
            {
                // 将栈转换为列表，移除最早的命令，再转回栈
                var tempList = _undoStack.ToList();
                tempList.RemoveAt(tempList.Count - 1);
                _undoStack.Clear();
                for (int i = tempList.Count - 1; i >= 0; i--)
                {
                    _undoStack.Push(tempList[i]);
                }
            }
        }

        /// <summary>
        /// 事务内部类
        /// </summary>
        private class Transaction
        {
            public string Name { get; }
            public List<ICommand> Commands { get; }

            public Transaction(string name)
            {
                Name = name ?? "Transaction";
                Commands = new List<ICommand>();
            }
        }
    }

    /// <summary>
    /// 复合命令 - 用于事务
    /// </summary>
    public class CompositeCommand : ICommand
    {
        private readonly List<ICommand> _commands;

        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; }
        public DateTime Timestamp { get; }

        public CompositeCommand(string name, List<ICommand> commands)
        {
            Name = name ?? "复合命令";
            _commands = commands ?? new List<ICommand>();
            Timestamp = commands.FirstOrDefault()?.Timestamp ?? DateTime.Now;
        }

        public void Execute()
        {
            foreach (var cmd in _commands)
            {
                cmd.Execute();
            }
        }

        public void Undo()
        {
            // 从后往前撤销
            for (int i = _commands.Count - 1; i >= 0; i--)
            {
                _commands[i].Undo();
            }
        }

        public void Redo()
        {
            foreach (var cmd in _commands)
            {
                cmd.Redo();
            }
        }
    }
}
