#pragma warning disable CS8601,CS8625
using ColorVision.Database;
using log4net;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Messages
{

    [SugarTable("MsgRecord")]
    public class MsgRecord : ViewEntity 
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MsgRecord));
        private readonly TaskCompletionSource<MsgRecordState>? _workerCompletion;
        private Task<MsgRecordState>? _workerResult;
        private TaskCompletionSource<MsgRecordState>? _legacyNotificationCompletion;
        private int _legacyNotifiedTerminalState;
        public event EventHandler<MsgRecordState> MsgRecordStateChanged;
        public event EventHandler<MsgReturn> MsgSucessed;
        internal event Action<MsgRecordState>? TerminalStateCommitted;

        public MsgRecord() { }

        internal MsgRecord(bool completeWithoutDispatcher)
        {
            if (completeWithoutDispatcher)
            {
                _workerCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _workerResult = _workerCompletion.Task;
            }
        }

        [SugarColumn(IsIgnore = true)]
        [JsonIgnore]
        internal Task<MsgRecordState>? WorkerCompletion => _workerResult;

        [SugarColumn(IsIgnore = true)]
        [JsonIgnore]
        internal Task<MsgRecordState> LegacyNotificationCompletion
        {
            get
            {
                var completion = LazyInitializer.EnsureInitialized(ref _legacyNotificationCompletion,
                    static () => new(TaskCreationOptions.RunContinuationsAsynchronously));
                var state = (MsgRecordState)Volatile.Read(ref _legacyNotifiedTerminalState);
                if (IsTerminal(state)) completion.TrySetResult(state);
                return completion.Task;
            }
        }

        // The sender attaches native/service ownership cleanup before returning the record to its waiter.
        internal void BeforeWorkerCompletion(Action<MsgRecordState> cleanup)
        {
            _workerResult = CompleteAsync(_workerResult!);

            async Task<MsgRecordState> CompleteAsync(Task<MsgRecordState> result)
            {
                MsgRecordState state = await result.ConfigureAwait(false);
                try { cleanup(state); }
                catch (Exception ex) { Log.Warn("Device command bookkeeping failed.", ex); }
                return state;
            }
        }

        [SugarColumn(ColumnName = "SubscribeTopic",IsNullable =true)]
        public string SubscribeTopic { get; set; }
        [SugarColumn(ColumnName = "SendTopic", IsNullable = true)]
        public string SendTopic { get; set; }

        [SugarColumn(ColumnName = "MsgID", IsNullable = true)]
        public string MsgID { get; set; }

        [SugarColumn(ColumnName = "SendTime", IsNullable = true)]
        public DateTime SendTime { get => _SendTime; set { _SendTime = value; OnPropertyChanged(); } }
        private DateTime _SendTime;

        [SugarColumn(ColumnName = "ReciveTime", IsNullable = true)]
        public DateTime ReciveTime { get => _ReciveTime; set { _ReciveTime = value; NotifyPropertyChanged(nameof(ReciveTime)); } }
        private DateTime _ReciveTime;

        [SugarColumn(IsIgnore = true)]
        public MsgSend MsgSend { get; set; }

        [SugarColumn(ColumnName = "MsgSendJson", IsNullable = true)]
        public string MsgSendJson { get => JsonConvert.SerializeObject(MsgSend); set { if (!string.IsNullOrEmpty(value)) MsgSend = JsonConvert.DeserializeObject<MsgSend>(value); } }
       
        [SugarColumn(IsIgnore = true)]
        public MsgReturn MsgReturn { get; set; }

        [SugarColumn(ColumnName = "MsgReturnJson", IsNullable = true)]
        public string MsgReturnJson { get => JsonConvert.SerializeObject(MsgReturn); set { if (!string.IsNullOrEmpty(value)) MsgReturn = JsonConvert.DeserializeObject<MsgReturn>(value); } }

        [SugarColumn(ColumnName = "MsgRecordState", IsNullable = true)]
        public MsgRecordState MsgRecordState
        {
            get => _MsgRecordState; set
            {
                CommitState(value);
                if (_workerCompletion != null)
                {
                    if (IsTerminal(value))
                        PublishDeferredState(value, notifyReceiveTime: value != MsgRecordState.Timeout);
                    return;
                }
                NotifyPropertyChanged(nameof(MsgRecordState));

                Application.Current.Dispatcher.Invoke(() => NotifyStateChanged(_MsgRecordState));
                NotifyStateProperties(value);
            }
        }

        private volatile MsgRecordState _MsgRecordState = MsgRecordState.Initial;

        private void CommitState(MsgRecordState state)
        {
            _MsgRecordState = state;
            if (!IsTerminal(state)) return;
            foreach (Action<MsgRecordState> handler in TerminalStateCommitted?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try { handler(state); }
                catch (Exception ex) { Log.Warn("Device command terminal observer failed.", ex); }
            }
        }

        internal static bool IsTerminal(MsgRecordState state) =>
            state is MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout;

        private void CompleteLegacyNotification(MsgRecordState state)
        {
            // Some legacy device owners release their command lease inside MsgRecordStateChanged.
            if (_workerCompletion == null && IsTerminal(state)
                && Interlocked.CompareExchange(ref _legacyNotifiedTerminalState, (int)state, (int)MsgRecordState.Initial) == (int)MsgRecordState.Initial)
                Volatile.Read(ref _legacyNotificationCompletion)?.TrySetResult(state);
        }

        private void NotifyStateChanged(MsgRecordState state)
        {
            foreach (EventHandler<MsgRecordState> handler in MsgRecordStateChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try { handler(this, state); }
                catch (Exception ex) { Log.Warn("Device command completion subscriber failed.", ex); }
            }
            CompleteLegacyNotification(state);
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            try { OnPropertyChanged(propertyName); }
            catch (Exception ex) { Log.Warn("Device command property notification failed.", ex); }
        }

        internal void CompleteWithResponse(MsgReturn response)
        {
            if (_workerCompletion != null)
                _ReciveTime = DateTime.Now;
            else
                ReciveTime = DateTime.Now;
            MsgReturn = response;
            MsgRecordState = response.Code == 0 ? MsgRecordState.Success : MsgRecordState.Fail;
        }

        internal void FailOnServiceDisposal(MsgReturn failure)
        {
            MsgReturn = failure;
            CommitState(MsgRecordState.Fail);
            PublishDeferredState(MsgRecordState.Fail, notifyReceiveTime: false);
        }

        private void PublishDeferredState(MsgRecordState state, bool notifyReceiveTime)
        {
            void Notify()
            {
                NotifyStateChanged(state);
                if (notifyReceiveTime) NotifyPropertyChanged(nameof(ReciveTime));
                NotifyPropertyChanged(nameof(MsgRecordState));
                NotifyStateProperties(state);
            }

            // Calibration and disposal may complete while the UI is waiting for the worker.
            if (Application.Current?.Dispatcher is { } dispatcher)
            {
                _ = dispatcher.InvokeAsync(Notify).Task.ContinueWith(_ => Notify(), CancellationToken.None,
                    TaskContinuationOptions.OnlyOnCanceled, TaskScheduler.Default);
            }
            else
            {
                _ = Task.Run(Notify);
            }

            _workerCompletion?.TrySetResult(state);
        }

        private void NotifyStateProperties(MsgRecordState state)
        {
            if (state is MsgRecordState.Success or MsgRecordState.Fail)
            {
                NotifyPropertyChanged(nameof(IsRecive));
                NotifyPropertyChanged(nameof(MsgReturn));
                if (state == MsgRecordState.Success)
                {
                    try { Application.Current.Dispatcher.Invoke(() => MsgSucessed?.Invoke(this, MsgReturn)); }
                    catch (Exception ex) { Log.Warn("Device command success notification failed.", ex); }
                }
                else
                    MsgSucessed = null;
            }
            else if (state == MsgRecordState.Timeout)
                NotifyPropertyChanged(nameof(IsTimeout));
            else
                NotifyPropertyChanged(nameof(MsgReturn));

            NotifyPropertyChanged(nameof(IsSend));
        }

        [SugarColumn(IsIgnore = true)]
        public bool IsSend { get => MsgRecordState == MsgRecordState.Sended; }

        [SugarColumn(IsIgnore = true)]
        [JsonIgnore]
        public bool IsRecive { get => MsgRecordState == MsgRecordState.Success || MsgRecordState == MsgRecordState.Fail; }

        [SugarColumn(IsIgnore = true)]
        [JsonIgnore]
        public bool IsTimeout { get => MsgRecordState == MsgRecordState.Timeout; }

        public DateTime UpdateTime { get; set; } = DateTime.Now;

        public DateTime CreateTime { get; set; } = DateTime.Now;

    }



}
