using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    /// <summary>
    /// Records node timing and MQTT request/response details for flow hosts that
    /// execute <see cref="FlowEngineControl"/> directly.
    /// </summary>
    public sealed class FlowNodeExecutionRecorder : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowNodeExecutionRecorder));

        private sealed record PendingNodeExecution(string WriteKey, FlowNodeRecord Record, FlowNodeMessage? Message);

        private readonly Func<FlowNodeRecord, FlowNodeMessage?, Task<int>> _insertExecution;
        private readonly Func<FlowNodeRecord, FlowNodeMessage?, Task> _updateExecution;
        private readonly Func<TimeSpan?, bool> _flushPendingWrites;
        private readonly Action<Task> _trackPendingWrite;
        private readonly object _attachmentSync = new object();
        private readonly object _lifecycleSync = new object();
        private readonly object _writeSync = new object();
        private readonly List<CVCommonNode> _attachedNodes = new List<CVCommonNode>();
        private readonly Dictionary<string, Queue<PendingNodeExecution>> _pendingExecutions = new();
        private readonly Dictionary<string, Task> _writeTasks = new();

        private string? _activeSerialNumber;
        private int _activeBatchId;
        private volatile bool _disposed;

        public FlowNodeExecutionRecorder()
            : this(
                static (record, message) => FlowNodeRecordDataBaseHelper.InsertNodeExecutionAsync(record, message, preserveTimingOnMessageFailure: true),
                static (record, message) => FlowNodeRecordDataBaseHelper.UpdateNodeExecutionAsync(record, message, preserveTimingOnMessageFailure: true),
                FlowNodeRecordDataBaseHelper.FlushPendingWrites,
                FlowNodeRecordDataBaseHelper.TrackPendingWrite)
        {
        }

        internal FlowNodeExecutionRecorder(
            Func<FlowNodeRecord, FlowNodeMessage?, Task<int>> insertExecution,
            Func<FlowNodeRecord, FlowNodeMessage?, Task> updateExecution,
            Func<TimeSpan?, bool> flushPendingWrites,
            Action<Task> trackPendingWrite)
        {
            _insertExecution = insertExecution ?? throw new ArgumentNullException(nameof(insertExecution));
            _updateExecution = updateExecution ?? throw new ArgumentNullException(nameof(updateExecution));
            _flushPendingWrites = flushPendingWrites ?? throw new ArgumentNullException(nameof(flushPendingWrites));
            _trackPendingWrite = trackPendingWrite ?? throw new ArgumentNullException(nameof(trackPendingWrite));
        }

        public void AttachNodes(IEnumerable<CVCommonNode> nodes)
        {
            ArgumentNullException.ThrowIfNull(nodes);

            lock (_attachmentSync)
            {
                if (_disposed)
                    return;

                DetachNodesCore();
                foreach (CVCommonNode node in nodes.Distinct())
                {
                    node.nodeRunEvent -= NodeRunEvent;
                    node.nodeEndEvent -= NodeEndEvent;
                    node.nodeRunEvent += NodeRunEvent;
                    node.nodeEndEvent += NodeEndEvent;
                    _attachedNodes.Add(node);
                }
            }
        }

        public void DetachNodes()
        {
            lock (_attachmentSync)
                DetachNodesCore();
        }

        public void StartRun(int batchId, string serialNumber)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchId);
            if (string.IsNullOrWhiteSpace(serialNumber))
                throw new ArgumentException("A flow serial number is required.", nameof(serialNumber));

            lock (_lifecycleSync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (!string.IsNullOrWhiteSpace(_activeSerialNumber))
                    throw new InvalidOperationException($"Flow diagnostics are already recording {_activeSerialNumber}.");

                _activeBatchId = batchId;
                _activeSerialNumber = serialNumber;
            }
        }

        public bool IsRecording(string? serialNumber = null)
        {
            lock (_lifecycleSync)
            {
                return !string.IsNullOrWhiteSpace(_activeSerialNumber)
                    && (string.IsNullOrWhiteSpace(serialNumber)
                        || string.Equals(_activeSerialNumber, serialNumber, StringComparison.Ordinal));
            }
        }

        public async Task<bool> CompleteRunAsync(
            string? serialNumber = null,
            TimeSpan? flushTimeout = null)
        {
            Task[] queuedWrites;
            lock (_lifecycleSync)
            {
                if (string.IsNullOrWhiteSpace(_activeSerialNumber)
                    || (serialNumber != null && !string.Equals(_activeSerialNumber, serialNumber, StringComparison.Ordinal)))
                    return false;

                // The host can still close its recording after an exception clears
                // its current result or switches the product serial number.
                serialNumber = _activeSerialNumber;
                _activeSerialNumber = null;
                _activeBatchId = 0;
                FinalizePendingExecutions(DateTime.Now);
                lock (_writeSync)
                    queuedWrites = _writeTasks.Values.ToArray();
            }

            await WaitForQueuedWritesAsync(queuedWrites).ConfigureAwait(false);

            bool flushed = await Task.Run(
                () => _flushPendingWrites(flushTimeout ?? TimeSpan.FromSeconds(5)))
                .ConfigureAwait(false);
            if (!flushed)
                log.Warn($"流程节点统计落库超时 => serialNumber={serialNumber}");

            return flushed;
        }

        private void NodeRunEvent(object sender, FlowEngineNodeRunEventArgs e)
        {
            try
            {
                lock (_lifecycleSync)
                {
                    if (sender is not CVCommonNode node || e == null || !CanRecord(e.SerialNumber))
                        return;

                    string writeKey = GetNodeExecutionKey(node, e.SendMsgId, e.SerialNumber);
                    string nodeName = node.OnGetDrawTitle();
                    var record = new FlowNodeRecord
                    {
                        BatchId = _activeBatchId,
                        SerialNumber = e.SerialNumber,
                        NodeId = node.NodeID,
                        NodeName = nodeName,
                        NodeType = node.NodeType,
                        StartTime = DateTime.Now,
                    };
                    FlowNodeMessage? message = string.IsNullOrEmpty(e.SendMsgId)
                        ? null
                        : new FlowNodeMessage
                        {
                            BatchId = record.BatchId,
                            SerialNumber = e.SerialNumber,
                            NodeId = node.NodeID,
                            NodeName = nodeName,
                            MsgId = e.SendMsgId,
                            EventName = e.SendEventName,
                            SendTopic = e.SendTopic,
                            SendPayload = e.SendPayload,
                            SendTime = DateTime.Now,
                            State = FlowMessageState.Sent,
                        };

                    if (!_pendingExecutions.TryGetValue(writeKey, out var pending))
                        _pendingExecutions.Add(writeKey, pending = new Queue<PendingNodeExecution>());
                    pending.Enqueue(new PendingNodeExecution(writeKey, record, message));
                    QueueWrite(writeKey, () => _insertExecution(record, message));
                }
            }
            catch (Exception ex)
            {
                log.Error("记录流程节点启动统计失败。", ex);
            }
        }

        private void NodeEndEvent(object sender, FlowEngineNodeEndEventArgs e)
        {
            try
            {
                lock (_lifecycleSync)
                {
                    if (sender is not CVCommonNode node || e == null || !CanRecord(e.SerialNumber))
                        return;

                    string writeKey = GetNodeExecutionKey(node, e.RecvMsgId, e.SerialNumber);
                    PendingNodeExecution? execution = TakePendingExecution(writeKey, node, e.SerialNumber);
                    if (execution == null)
                        return;

                    DateTime endTime = DateTime.Now;
                    if (execution.Message is FlowNodeMessage message)
                    {
                        message.RecvTime = endTime;
                        if (!string.IsNullOrEmpty(e.RecvMsgId))
                        {
                            message.RecvTopic = e.RecvTopic;
                            message.RecvPayload = e.RecvPayload;
                            message.StatusCode = e.RecvStatusCode;
                            message.StatusMessage = e.RecvStatusMessage;
                            message.State = e.RecvStatusCode == 0 ? FlowMessageState.Success : FlowMessageState.Fail;
                        }
                        else
                        {
                            message.State = FlowMessageState.Timeout;
                        }
                    }
                    CompleteExecution(execution, endTime);
                }
            }
            catch (Exception ex)
            {
                log.Error("记录流程节点结束统计失败。", ex);
            }
        }

        private bool CanRecord(string? serialNumber) => !_disposed
            && !string.IsNullOrWhiteSpace(serialNumber)
            && string.Equals(serialNumber, _activeSerialNumber, StringComparison.Ordinal);

        private PendingNodeExecution? TakePendingExecution(string writeKey, CVCommonNode node, string serialNumber)
        {
            if (!_pendingExecutions.TryGetValue(writeKey, out var pending))
            {
                string prefix = $"{serialNumber}|{node.NodeID}|";
                foreach (var (key, queue) in _pendingExecutions)
                {
                    if (key.StartsWith(prefix, StringComparison.Ordinal)
                        && (pending == null || queue.Peek().Record.StartTime < pending.Peek().Record.StartTime))
                    {
                        writeKey = key;
                        pending = queue;
                    }
                }
                if (pending == null)
                    return null;
            }

            PendingNodeExecution execution = pending.Dequeue();
            if (pending.Count == 0)
                _pendingExecutions.Remove(writeKey);
            return execution;
        }

        private void CompleteExecution(PendingNodeExecution execution, DateTime endTime)
        {
            execution.Record.EndTime = endTime;
            execution.Record.ElapsedMs = Math.Max(
                0,
                (long)(endTime - execution.Record.StartTime).TotalMilliseconds);
            QueueWrite(execution.WriteKey, () => execution.Record.Id > 0
                ? _updateExecution(execution.Record, execution.Message is { Id: > 0 } message ? message : null)
                : Task.CompletedTask);
        }

        private void FinalizePendingExecutions(DateTime completedTime)
        {
            foreach (Queue<PendingNodeExecution> queue in _pendingExecutions.Values)
            {
                foreach (PendingNodeExecution execution in queue)
                {
                    if (execution.Message is FlowNodeMessage message)
                    {
                        message.RecvTime ??= completedTime;
                        message.State = FlowMessageState.Timeout;
                    }
                    CompleteExecution(execution, completedTime);
                }
            }
            _pendingExecutions.Clear();
        }

        private void QueueWrite(string writeKey, Func<Task> write)
        {
            lock (_writeSync)
            {
                Task nextWrite = _writeTasks.TryGetValue(writeKey, out Task? previous)
                    ? previous.ContinueWith(
                        _ => write(),
                        CancellationToken.None,
                        TaskContinuationOptions.None,
                        TaskScheduler.Default).Unwrap()
                    : Task.Run(write);
                _writeTasks[writeKey] = nextWrite;
                _trackPendingWrite(nextWrite);
                _ = nextWrite.ContinueWith(
                    _ =>
                    {
                        lock (_writeSync)
                        {
                            if (_writeTasks.TryGetValue(writeKey, out Task? current) && ReferenceEquals(current, nextWrite))
                                _writeTasks.Remove(writeKey);
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        private static async Task WaitForQueuedWritesAsync(Task[] pendingWrites)
        {
            if (pendingWrites.Length == 0)
                return;

            try
            {
                await Task.WhenAll(pendingWrites).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Warn("等待流程节点统计写入失败。", ex);
            }
        }

        private static string GetNodeExecutionKey(
            CVCommonNode node,
            string? messageId,
            string? serialNumber)
        {
            return $"{serialNumber}|{node.NodeID}|{messageId}";
        }

        private void DetachNodesCore()
        {
            foreach (CVCommonNode node in _attachedNodes)
            {
                node.nodeRunEvent -= NodeRunEvent;
                node.nodeEndEvent -= NodeEndEvent;
            }
            _attachedNodes.Clear();
        }

        public void Dispose()
        {
            string? activeSerialNumber;
            lock (_lifecycleSync)
            {
                if (_disposed)
                    return;
                _disposed = true;
                activeSerialNumber = _activeSerialNumber;
            }
            if (!string.IsNullOrWhiteSpace(activeSerialNumber))
            {
                try
                {
                    CompleteRunAsync(
                        activeSerialNumber,
                        flushTimeout: TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    log.Warn("关闭流程节点统计记录器时落库失败。", ex);
                }
            }

            lock (_attachmentSync)
                DetachNodesCore();
        }
    }
}
