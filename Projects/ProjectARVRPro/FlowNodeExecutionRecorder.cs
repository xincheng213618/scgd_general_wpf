using ColorVision.Engine.FlowProcessing.Diagnostics;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectARVRPro
{
    /// <summary>
    /// Records node timing and MQTT request/response details for flow hosts that
    /// execute <see cref="FlowEngineControl"/> directly.
    /// </summary>
    internal sealed class FlowNodeExecutionRecorder : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowNodeExecutionRecorder));

        private sealed class PendingNodeExecution
        {
            public PendingNodeExecution(
                string writeKey,
                FlowNodeRecord record,
                FlowNodeMessage? message,
                long generation)
            {
                WriteKey = writeKey;
                Record = record;
                Message = message;
                Generation = generation;
            }

            public string WriteKey { get; }

            public FlowNodeRecord Record { get; }

            public FlowNodeMessage? Message { get; }

            public long Generation { get; }
        }

        private readonly Func<FlowNodeRecord, int> _insertRecord;
        private readonly Action<FlowNodeRecord> _updateRecord;
        private readonly Func<FlowNodeMessage, int> _insertMessage;
        private readonly Action<FlowNodeMessage> _updateMessage;
        private readonly Func<TimeSpan?, bool> _flushPendingWrites;
        private readonly Action<Task> _trackPendingWrite;
        private readonly object _attachmentSync = new object();
        private readonly object _lifecycleSync = new object();
        private readonly object _writeSync = new object();
        private readonly List<CVCommonNode> _attachedNodes = new List<CVCommonNode>();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<PendingNodeExecution>> _pendingExecutions = new ConcurrentDictionary<string, ConcurrentQueue<PendingNodeExecution>>();
        private readonly ConcurrentDictionary<string, Task> _writeTasks = new ConcurrentDictionary<string, Task>();

        private string? _activeSerialNumber;
        private int _activeBatchId;
        private long _generation;
        private bool _disposed;

        internal FlowNodeExecutionRecorder()
            : this(
                FlowNodeRecordDataBaseHelper.Insert,
                FlowNodeRecordDataBaseHelper.Update,
                FlowNodeRecordDataBaseHelper.InsertMessage,
                FlowNodeRecordDataBaseHelper.UpdateMessage,
                FlowNodeRecordDataBaseHelper.FlushPendingWrites,
                FlowNodeRecordDataBaseHelper.TrackPendingWrite)
        {
        }

        internal FlowNodeExecutionRecorder(
            Func<FlowNodeRecord, int> insertRecord,
            Action<FlowNodeRecord> updateRecord,
            Func<FlowNodeMessage, int> insertMessage,
            Action<FlowNodeMessage> updateMessage,
            Func<TimeSpan?, bool> flushPendingWrites,
            Action<Task> trackPendingWrite)
        {
            _insertRecord = insertRecord ?? throw new ArgumentNullException(nameof(insertRecord));
            _updateRecord = updateRecord ?? throw new ArgumentNullException(nameof(updateRecord));
            _insertMessage = insertMessage ?? throw new ArgumentNullException(nameof(insertMessage));
            _updateMessage = updateMessage ?? throw new ArgumentNullException(nameof(updateMessage));
            _flushPendingWrites = flushPendingWrites ?? throw new ArgumentNullException(nameof(flushPendingWrites));
            _trackPendingWrite = trackPendingWrite ?? throw new ArgumentNullException(nameof(trackPendingWrite));
        }

        public void AttachNodes(IEnumerable<CVCommonNode> nodes)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));

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
            if (batchId <= 0)
                throw new ArgumentOutOfRangeException(nameof(batchId));
            if (string.IsNullOrWhiteSpace(serialNumber))
                throw new ArgumentException("A flow serial number is required.", nameof(serialNumber));

            lock (_lifecycleSync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (!string.IsNullOrWhiteSpace(_activeSerialNumber))
                    throw new InvalidOperationException($"Flow diagnostics are already recording {_activeSerialNumber}.");

                _pendingExecutions.Clear();
                _activeBatchId = batchId;
                _activeSerialNumber = serialNumber;
                Interlocked.Increment(ref _generation);
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
            string serialNumber,
            TimeSpan? nodeEventDrainDelay = null,
            TimeSpan? flushTimeout = null)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
                return false;

            TimeSpan drainDelay = nodeEventDrainDelay.GetValueOrDefault();
            if (drainDelay > TimeSpan.Zero)
                await Task.Delay(drainDelay).ConfigureAwait(false);

            long completedGeneration;
            lock (_lifecycleSync)
            {
                if (!string.Equals(_activeSerialNumber, serialNumber, StringComparison.Ordinal))
                    return false;

                completedGeneration = Volatile.Read(ref _generation);
                _activeSerialNumber = null;
                _activeBatchId = 0;
            }

            FinalizePendingExecutions(completedGeneration, DateTime.Now);
            await WaitForQueuedWritesAsync().ConfigureAwait(false);

            bool flushed = await Task.Run(
                () => _flushPendingWrites(flushTimeout ?? TimeSpan.FromSeconds(5)))
                .ConfigureAwait(false);
            if (!flushed)
                log.Warn($"流程节点统计落库超时 => serialNumber={serialNumber}");

            RemoveCompletedGeneration(completedGeneration);
            return flushed;
        }

        private void NodeRunEvent(object sender, FlowEngineNodeRunEventArgs e)
        {
            try
            {
                if (sender is not CVCommonNode node
                    || e == null
                    || !TryGetActiveRun(e.SerialNumber, out int batchId, out long generation))
                {
                    return;
                }

                string writeKey = GetNodeExecutionKey(node, e.SendMsgId, e.SerialNumber);
                var record = new FlowNodeRecord
                {
                    BatchId = batchId,
                    SerialNumber = e.SerialNumber,
                    NodeId = node.NodeID,
                    NodeName = node.OnGetDrawTitle(),
                    NodeType = node.NodeType,
                    StartTime = DateTime.Now,
                };
                FlowNodeMessage? message = string.IsNullOrEmpty(e.SendMsgId)
                    ? null
                    : new FlowNodeMessage
                    {
                        BatchId = batchId,
                        SerialNumber = e.SerialNumber,
                        NodeId = node.NodeID,
                        NodeName = node.OnGetDrawTitle(),
                        MsgId = e.SendMsgId,
                        EventName = e.SendEventName,
                        SendTopic = e.SendTopic,
                        SendPayload = e.SendPayload,
                        SendTime = DateTime.Now,
                        State = FlowMessageState.Sent,
                    };

                var execution = new PendingNodeExecution(writeKey, record, message, generation);
                _pendingExecutions
                    .GetOrAdd(writeKey, _ => new ConcurrentQueue<PendingNodeExecution>())
                    .Enqueue(execution);
                QueueWrite(writeKey, () =>
                {
                    int insertId = _insertRecord(record);
                    if (insertId > 0 && message != null)
                    {
                        message.NodeRecordId = record.Id;
                        _insertMessage(message);
                    }
                });
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
                if (sender is not CVCommonNode node
                    || e == null
                    || !TryGetActiveRun(e.SerialNumber, out _, out long generation))
                {
                    return;
                }

                string writeKey = GetNodeExecutionKey(node, e.RecvMsgId, e.SerialNumber);
                if (!TryTakePendingExecution(writeKey, node, e.SerialNumber, generation, out PendingNodeExecution? execution))
                    return;

                DateTime endTime = DateTime.Now;
                PendingNodeExecution completedExecution = execution!;
                CompleteRecord(completedExecution, endTime);
                if (completedExecution.Message is FlowNodeMessage message)
                {
                    message.RecvTime = endTime;
                    if (!string.IsNullOrEmpty(e.RecvMsgId))
                    {
                        message.RecvTopic = e.RecvTopic;
                        message.RecvPayload = e.RecvPayload;
                        message.StatusCode = e.RecvStatusCode;
                        message.StatusMessage = e.RecvStatusMessage;
                        message.State = e.RecvStatusCode == 0
                            ? FlowMessageState.Success
                            : FlowMessageState.Fail;
                    }
                    else
                    {
                        message.State = FlowMessageState.Timeout;
                    }

                    QueueWrite(completedExecution.WriteKey, () => _updateMessage(message));
                }
            }
            catch (Exception ex)
            {
                log.Error("记录流程节点结束统计失败。", ex);
            }
        }

        private bool TryGetActiveRun(string? serialNumber, out int batchId, out long generation)
        {
            lock (_lifecycleSync)
            {
                bool matches = !_disposed
                    && !string.IsNullOrWhiteSpace(serialNumber)
                    && string.Equals(serialNumber, _activeSerialNumber, StringComparison.Ordinal);
                batchId = matches ? _activeBatchId : 0;
                generation = matches ? Volatile.Read(ref _generation) : 0;
                return matches;
            }
        }

        private bool TryTakePendingExecution(
            string writeKey,
            CVCommonNode node,
            string serialNumber,
            long generation,
            out PendingNodeExecution? execution)
        {
            if (_pendingExecutions.TryGetValue(writeKey, out ConcurrentQueue<PendingNodeExecution>? exactQueue)
                && TryTakeGeneration(exactQueue, generation, out execution))
            {
                return true;
            }

            string prefix = $"{serialNumber}|{node.NodeID}|";
            foreach (KeyValuePair<string, ConcurrentQueue<PendingNodeExecution>> item in _pendingExecutions
                         .Where(item => item.Key.StartsWith(prefix, StringComparison.Ordinal))
                         .OrderBy(item => item.Value.TryPeek(out PendingNodeExecution? pending)
                             ? pending.Record.StartTime
                             : DateTime.MaxValue))
            {
                if (TryTakeGeneration(item.Value, generation, out execution))
                    return true;
            }

            execution = null;
            return false;
        }

        private static bool TryTakeGeneration(
            ConcurrentQueue<PendingNodeExecution> queue,
            long generation,
            out PendingNodeExecution? execution)
        {
            while (queue.TryDequeue(out PendingNodeExecution? candidate))
            {
                if (candidate.Generation == generation)
                {
                    execution = candidate;
                    return true;
                }
            }

            execution = null;
            return false;
        }

        private void CompleteRecord(PendingNodeExecution execution, DateTime endTime)
        {
            execution.Record.EndTime = endTime;
            execution.Record.ElapsedMs = Math.Max(
                0,
                (long)(endTime - execution.Record.StartTime).TotalMilliseconds);
            QueueWrite(execution.WriteKey, () => _updateRecord(execution.Record));
        }

        private void FinalizePendingExecutions(long generation, DateTime completedTime)
        {
            foreach (ConcurrentQueue<PendingNodeExecution> queue in _pendingExecutions.Values)
            {
                while (queue.TryDequeue(out PendingNodeExecution? execution))
                {
                    if (execution.Generation != generation)
                        continue;

                    CompleteRecord(execution, completedTime);
                    if (execution.Message is FlowNodeMessage message)
                    {
                        message.RecvTime ??= completedTime;
                        message.State = FlowMessageState.Timeout;
                        QueueWrite(execution.WriteKey, () => _updateMessage(message));
                    }
                }
            }
        }

        private void QueueWrite(string writeKey, Action write)
        {
            lock (_writeSync)
            {
                Task nextWrite = _writeTasks.TryGetValue(writeKey, out Task? previous)
                    ? previous.ContinueWith(
                        _ => write(),
                        CancellationToken.None,
                        TaskContinuationOptions.None,
                        TaskScheduler.Default)
                    : Task.Run(write);
                _writeTasks[writeKey] = nextWrite;
                _trackPendingWrite(nextWrite);
                _ = nextWrite.ContinueWith(
                    _ =>
                    {
                        lock (_writeSync)
                        {
                            if (_writeTasks.TryGetValue(writeKey, out Task? current)
                                && ReferenceEquals(current, nextWrite))
                            {
                                _writeTasks.TryRemove(writeKey, out _);
                            }
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        private async Task WaitForQueuedWritesAsync()
        {
            Task[] pendingWrites;
            lock (_writeSync)
                pendingWrites = _writeTasks.Values.ToArray();

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

        private void RemoveCompletedGeneration(long generation)
        {
            foreach (KeyValuePair<string, ConcurrentQueue<PendingNodeExecution>> item in _pendingExecutions)
            {
                if (item.Value.IsEmpty
                    || item.Value.All(execution => execution.Generation <= generation))
                {
                    _pendingExecutions.TryRemove(item.Key, out _);
                }
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
            if (_disposed)
                return;

            string? activeSerialNumber;
            lock (_lifecycleSync)
                activeSerialNumber = _activeSerialNumber;
            if (!string.IsNullOrWhiteSpace(activeSerialNumber))
            {
                try
                {
                    CompleteRunAsync(
                        activeSerialNumber,
                        TimeSpan.Zero,
                        TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    log.Warn("关闭流程节点统计记录器时落库失败。", ex);
                }
            }

            lock (_attachmentSync)
            {
                _disposed = true;
                DetachNodesCore();
            }
            _pendingExecutions.Clear();
            _writeTasks.Clear();
        }
    }
}
