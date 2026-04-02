using System;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.Configuration
{
    /// <summary>
    /// 命令接口 - Undo/Redo 的基础
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// 命令唯一标识
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// 命令名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 执行时间戳
        /// </summary>
        DateTime Timestamp { get; }

        /// <summary>
        /// 执行命令
        /// </summary>
        void Execute();

        /// <summary>
        /// 撤销命令
        /// </summary>
        void Undo();

        /// <summary>
        /// 重做命令（默认调用Execute，可重写优化）
        /// </summary>
        void Redo();
    }

    /// <summary>
    /// 差分命令接口 - 支持差分存储
    /// </summary>
    public interface IDeltaCommand : ICommand
    {
        /// <summary>
        /// 获取差分数据
        /// </summary>
        IDeltaData GetDeltaData();

        /// <summary>
        /// 从差分数据恢复
        /// </summary>
        void ApplyDelta(IDeltaData deltaData);
    }

    /// <summary>
    /// 差分数据接口
    /// </summary>
    public interface IDeltaData
    {
        /// <summary>
        /// 目标对象标识
        /// </summary>
        string TargetId { get; }

        /// <summary>
        /// 属性名称
        /// </summary>
        string PropertyName { get; }

        /// <summary>
        /// 旧值
        /// </summary>
        object OldValue { get; }

        /// <summary>
        /// 新值
        /// </summary>
        object NewValue { get; }

        /// <summary>
        /// 序列化为字节数组（用于存储）
        /// </summary>
        byte[] Serialize();

        /// <summary>
        /// 从字节数组反序列化
        /// </summary>
        void Deserialize(byte[] data);
    }

    /// <summary>
    /// 命令管理器接口
    /// </summary>
    public interface ICommandManager
    {
        /// <summary>
        /// 执行命令
        /// </summary>
        void Execute(ICommand command);

        /// <summary>
        /// 撤销
        /// </summary>
        void Undo();

        /// <summary>
        /// 重做
        /// </summary>
        void Redo();

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// 是否可以重做
        /// </summary>
        bool CanRedo { get; }

        /// <summary>
        /// 撤销栈深度
        /// </summary>
        int UndoStackDepth { get; }

        /// <summary>
        /// 重做栈深度
        /// </summary>
        int RedoStackDepth { get; }

        /// <summary>
        /// 最大历史记录数
        /// </summary>
        int MaxHistorySize { get; set; }

        /// <summary>
        /// 清空历史
        /// </summary>
        void ClearHistory();

        /// <summary>
        /// 获取撤销历史
        /// </summary>
        IEnumerable<ICommand> GetUndoHistory();

        /// <summary>
        /// 获取重做历史
        /// </summary>
        IEnumerable<ICommand> GetRedoHistory();

        /// <summary>
        /// 撤销到指定命令
        /// </summary>
        void UndoTo(Guid commandId);

        /// <summary>
        /// 重做指定命令
        /// </summary>
        void RedoTo(Guid commandId);

        /// <summary>
        /// 命令执行事件
        /// </summary>
        event EventHandler<CommandExecutedEventArgs> CommandExecuted;

        /// <summary>
        /// 命令撤销事件
        /// </summary>
        event EventHandler<CommandExecutedEventArgs> CommandUndone;

        /// <summary>
        /// 命令重做事件
        /// </summary>
        event EventHandler<CommandExecutedEventArgs> CommandRedone;
    }

    /// <summary>
    /// 命令执行事件参数
    /// </summary>
    public class CommandExecutedEventArgs : EventArgs
    {
        public ICommand Command { get; }
        public CommandExecutionType ExecutionType { get; }

        public CommandExecutedEventArgs(ICommand command, CommandExecutionType executionType)
        {
            Command = command;
            ExecutionType = executionType;
        }
    }

    /// <summary>
    /// 命令执行类型
    /// </summary>
    public enum CommandExecutionType
    {
        Execute,
        Undo,
        Redo
    }

    /// <summary>
    /// 支持事务的命令管理器
    /// </summary>
    public interface ITransactionalCommandManager : ICommandManager
    {
        /// <summary>
        /// 开始事务
        /// </summary>
        void BeginTransaction(string transactionName);

        /// <summary>
        /// 提交事务
        /// </summary>
        void CommitTransaction();

        /// <summary>
        /// 回滚事务
        /// </summary>
        void RollbackTransaction();

        /// <summary>
        /// 是否在事务中
        /// </summary>
        bool IsInTransaction { get; }

        /// <summary>
        /// 当前事务名称
        /// </summary>
        string CurrentTransactionName { get; }
    }
}
