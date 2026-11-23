using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using log4net;
using MQTTnet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace ColorVision.Engine.Services
{
    internal sealed record MqttServiceRoute(MQTTControl Transport, string SendTopic, string ReplyTopic, string DeviceCode, string ServiceToken,
        MqttClientOptions? ConnectionOptions)
    {
        internal bool HasSameDestination(MqttServiceRoute current) => ReferenceEquals(Transport, current.Transport)
            && ReferenceEquals(ConnectionOptions, current.ConnectionOptions)
            && string.Equals(DeviceCode, current.DeviceCode, StringComparison.Ordinal)
            && string.Equals(SendTopic, current.SendTopic, StringComparison.Ordinal)
            && string.Equals(ReplyTopic, current.ReplyTopic, StringComparison.Ordinal);
    }

    public class MQTTServiceBase : ViewModelBase, IDisposable
    {
        private const int PendingResponseCode = 102;
        internal static readonly ILog log = LogManager.GetLogger(typeof(MQTTServiceBase));
        private readonly object _locker = new();
        private readonly Dictionary<string, PendingRequest> _pendingRequests = new(StringComparer.Ordinal);
        private readonly MQTTControl _subscriptionTransport;
        private volatile int _pinnedReplyCount;
        private volatile bool _disposed;

        public MQTTControl MQTTControl { get; set; }

        public virtual DeviceStatusType DeviceStatus { get; set; }


        public MQTTServiceBase() : this(MQTTControl.GetInstance())
        {
        }

        internal MQTTServiceBase(MQTTControl mqttControl)
        {
            MQTTControl = _subscriptionTransport = mqttControl;
            _subscriptionTransport.ApplicationMessageReceivedAsync += Processing;
        }

        public void SubscribeCache() => MQTTControl.SubscribeCache(SubscribeTopic);


        private Task Processing(MqttApplicationMessageReceivedEventArgs arg)
        {
            string topic = arg.ApplicationMessage.Topic;
            bool currentTopic = string.Equals(topic, SubscribeTopic, StringComparison.Ordinal);
            if (currentTopic || HasPinnedReplyTopic(topic))
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
                try
                {
                    MsgReturn json = JsonConvert.DeserializeObject<MsgReturn>(Msg);
                    if (json == null)
                        return Task.CompletedTask;

                    PendingRequest? request = TakeResponseRequest(json, topic, currentTopic, out bool pinnedMessage);
                    if (request != null)
                    {
                        MsgReturn response = request.PinnedRoute is { } pinned && !IsMqttRouteUsable(pinned)
                            ? CreateRetiredRouteResponse(request.Record) : json;
                        request.Record.CompleteWithResponse(response);
                    }
                    else if (currentTopic && !pinnedMessage && json.Code != PendingResponseCode)
                    {
                        log.Debug($"Received MQTT response but no pending MsgRecord matched. Topic:{topic}, MsgID:{json.MsgID}, EventName:{json.EventName}, DeviceCode:{json.DeviceCode}");
                    }
                    if (currentTopic && !pinnedMessage && string.Equals(topic, SubscribeTopic, StringComparison.Ordinal))
                        MsgReturnReceived?.Invoke(json);
                }
                catch (Exception ex)
                {
                    if (log.IsErrorEnabled)
                        log.Error(ex);
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
        }

        private bool HasPinnedReplyTopic(string topic)
        {
            if (_pinnedReplyCount == 0) return false;
            lock (_locker)
                return _pendingRequests.Values.Any(request => string.Equals(request.PinnedRoute?.ReplyTopic, topic, StringComparison.Ordinal));
        }

        public MsgReturnHandler? MsgReturnReceived { get; set; }
        public virtual string SubscribeTopic { get; set; }
        public virtual string SendTopic { get; set; }
        public virtual string DeviceCode { get; set; }
        public string ServiceName { get; set; }
        public virtual string ServiceToken { get; set; }

        /// <summary>
        /// 这里修改成可以继承的
        /// </summary>
        /// <param name="msg"></param>
        internal virtual MsgRecord PublishAsyncClient(MsgSend msg, double timeout = 30000) =>
            PublishCore(msg, CaptureMqttRoute(), timeout, pinReply: false);

        internal MqttServiceRoute CaptureMqttRoute(bool pinConnection = false)
        {
            var transport = MQTTControl;
            return new(transport, SendTopic, SubscribeTopic, DeviceCode, ServiceToken, pinConnection ? transport.MQTTClient.Options : null);
        }

        internal bool IsMqttRouteUsable(MqttServiceRoute route) => !_disposed && ReferenceEquals(MQTTControl, route.Transport)
            && ReferenceEquals(_subscriptionTransport, route.Transport) && route.Transport.MQTTClient.IsConnected
            && route.ConnectionOptions != null && ReferenceEquals(route.Transport.MQTTClient.Options, route.ConnectionOptions)
            && !string.IsNullOrWhiteSpace(route.SendTopic) && !string.IsNullOrWhiteSpace(route.ReplyTopic);

        private static MsgReturn CreateRetiredRouteResponse(MsgRecord record) => new()
        {
            MsgID = record.MsgID,
            DeviceCode = record.MsgSend.DeviceCode,
            EventName = record.MsgSend.EventName,
            Code = -1,
            Message = "原设备服务的 MQTT 路由或命令已失效。"
        };

        internal MsgRecord PublishOnMqttRoute(MsgSend msg, MqttServiceRoute route, double timeout = 30000,
            Func<bool>? canDispatch = null, object? dispatchLock = null, Func<MsgReturn, bool>? acceptsReply = null,
            Action<MsgRecord>? beforeDispatch = null) =>
            PublishCore(msg, route, timeout, pinReply: true, canDispatch, dispatchLock, acceptsReply, beforeDispatch);

        private MsgRecord PublishCore(MsgSend msg, MqttServiceRoute route, double timeout, bool pinReply,
            Func<bool>? canDispatch = null, object? dispatchLock = null, Func<MsgReturn, bool>? acceptsReply = null,
            Action<MsgRecord>? beforeDispatch = null)
        {
            msg.MsgID ??= Guid.NewGuid().ToString();
            msg.DeviceCode ??= route.DeviceCode;
            msg.Token ??= route.ServiceToken;
            msg.ServiceName ??= route.SendTopic;

            string json = JsonConvert.SerializeObject(msg, Formatting.None);

            string messageId = msg.MsgID;
            MsgRecord msgRecord = new(msg.CompleteWithoutDispatcher) { SendTopic = route.SendTopic, SubscribeTopic = route.ReplyTopic, MsgID = messageId, SendTime = DateTime.Now, MsgSend = msg, MsgRecordState = MsgRecordState.Sended };
            beforeDispatch?.Invoke(msgRecord);
            PendingRequest pending;
            lock (_locker)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (pinReply && !IsMqttRouteUsable(route)) throw new InvalidOperationException("原设备服务的 MQTT 路由已不可用。");
                var timer = new Timer(timeout) { AutoReset = false };
                pending = new PendingRequest(msgRecord, timer, pinReply ? route : null, acceptsReply);
                timer.Elapsed += (_, _) =>
                {
                    if (TakePendingRequest(messageId, pending) != null)
                        msgRecord.MsgRecordState = MsgRecordState.Timeout;
                };
                try
                {
                    _pendingRequests.Add(messageId, pending);
                    if (pinReply) _pinnedReplyCount++;
                    timer.Start();
                }
                catch
                {
                    if (_pendingRequests.TryGetValue(messageId, out var registered) && ReferenceEquals(registered, pending))
                    {
                        RemovePendingRequest(messageId, pending);
                    }
                    timer.Dispose();
                    throw;
                }
            }

            Task.Run(() =>
            {
                MsgRecordDataBaseHelper.Insert(msgRecord);
            });

            if (pinReply)
                _ = DispatchPinnedAsync(messageId, pending, json, canDispatch, dispatchLock);
            else
                _ = route.Transport.PublishAsyncClient(route.SendTopic, json, false);
            return msgRecord;
        }

        private async Task DispatchPinnedAsync(string messageId, PendingRequest pending, string json,
            Func<bool>? canDispatch, object? dispatchLock)
        {
            MqttServiceRoute route = pending.PinnedRoute!;
            bool StillPending()
            {
                if (canDispatch?.Invoke() == false) return false;
                lock (_locker)
                    return IsMqttRouteUsable(route) && _pendingRequests.TryGetValue(messageId, out var current) && ReferenceEquals(current, pending);
            }

            try
            {
                if (await route.Transport.TryPublishOnConnectionAsync(route.ConnectionOptions!, route.SendTopic, json, StillPending, dispatchLock).ConfigureAwait(false))
                    return;
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to publish pinned MQTT command. Topic:{route.SendTopic}, MsgID:{messageId}", ex);
            }

            if (TakePendingRequest(messageId, pending) != null)
                pending.Record.CompleteWithResponse(CreateRetiredRouteResponse(pending.Record));
        }

        private PendingRequest? TakeResponseRequest(MsgReturn response, string topic, bool currentTopic, out bool pinnedMessage)
        {
            pinnedMessage = false;
            PendingRequest? pending;
            lock (_locker)
            {
                if (response.MsgID == null || !_pendingRequests.TryGetValue(response.MsgID, out pending))
                    return null;
                pinnedMessage = pending.PinnedRoute != null;
                if (response.Code == PendingResponseCode || (pinnedMessage
                    ? !string.Equals(pending.PinnedRoute!.ReplyTopic, topic, StringComparison.Ordinal) : !currentTopic))
                    return null;
                if (pending.AcceptsReply?.Invoke(response) == false)
                    return null;
                RemovePendingRequest(response.MsgID, pending);
            }
            pending.Timer.Dispose();
            return pending;
        }

        private PendingRequest? TakePendingRequest(string messageId, PendingRequest expected)
        {
            lock (_locker)
            {
                if (!_pendingRequests.TryGetValue(messageId, out var pending) || !ReferenceEquals(expected, pending))
                    return null;
                RemovePendingRequest(messageId, pending);
            }

            // Only the winner publishes a terminal state, and WPF callbacks run outside the lock.
            expected.Timer.Dispose();
            return expected;
        }

        private void RemovePendingRequest(string messageId, PendingRequest pending)
        {
            _pendingRequests.Remove(messageId);
            if (pending.PinnedRoute != null) _pinnedReplyCount--;
        }

        public virtual void Dispose()
        {
            List<PendingRequest> abandoned;
            lock (_locker)
            {
                if (_disposed)
                    return;
                _disposed = true;
                abandoned = new(_pendingRequests.Values);
                _pendingRequests.Clear();
                _pinnedReplyCount = 0;
            }

            _subscriptionTransport.ApplicationMessageReceivedAsync -= Processing;
            foreach (var pending in abandoned)
            {
                pending.Timer.Dispose();
                MsgRecord record = pending.Record;
                record.FailOnServiceDisposal(new MsgReturn
                {
                    MsgID = record.MsgID,
                    EventName = record.MsgSend.EventName,
                    DeviceCode = record.MsgSend.DeviceCode,
                    Code = -1,
                    Message = EngineLocalization.Get("设备服务已释放，命令结果待确认。"),
                });
            }

            GC.SuppressFinalize(this);
        }

        private sealed record PendingRequest(MsgRecord Record, Timer Timer, MqttServiceRoute? PinnedRoute, Func<MsgReturn, bool>? AcceptsReply);
    }
}
