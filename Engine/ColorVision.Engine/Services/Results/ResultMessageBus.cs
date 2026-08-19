using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.Results
{
    public static class ResultRoutes
    {
        public const string Camera = "camera";
        public const string Calibration = "calibration";
        public const string Algorithm = "algorithm";
        public const string Spectrum = "spectrum";
        public const string Smu = "smu";
    }

    public static class ResultKinds
    {
        public const string Image = "image";
        public const string Algorithm = "algorithm";
        public const string Spectrum = "spectrum";
        public const string Smu = "smu";
    }

    public sealed class ResultReference
    {
        public ResultReference(int masterId, int masterResultType)
        {
            MasterId = masterId;
            MasterResultType = masterResultType;
        }

        public int MasterId { get; }
        public int MasterResultType { get; }
    }

    /// <summary>
    /// Versioned process-local result message. The persisted reference is transport-neutral;
    /// Attachment is reserved for optional process-local data such as a future image-frame handle.
    /// </summary>
    public sealed class ResultMessage
    {
        public const string CurrentProtocolVersion = "ColorVision.Result/1.0";

        internal ResultMessage(
            string route,
            string resultKind,
            string deviceCode,
            string eventName,
            string serialNumber,
            string nodeId,
            int zIndex,
            ResultReference data,
            object? attachment)
        {
            ProtocolVersion = CurrentProtocolVersion;
            MessageId = Guid.NewGuid().ToString("N");
            Route = route;
            ResultKind = resultKind;
            DeviceCode = deviceCode;
            EventName = eventName;
            SerialNumber = serialNumber;
            NodeId = nodeId;
            ZIndex = zIndex;
            Data = data;
            Attachment = attachment;
        }

        public string ProtocolVersion { get; }
        public string MessageId { get; }
        public string Route { get; }
        public string ResultKind { get; }
        public string DeviceCode { get; }
        public string EventName { get; }
        public string SerialNumber { get; }
        public string NodeId { get; }
        public int ZIndex { get; }
        public int Code => 0;
        public string Message => "ok";
        public ResultReference Data { get; }

        /// <summary>
        /// Optional process-local extension point. Persisted-result messages currently leave this null.
        /// </summary>
        public object? Attachment { get; }
    }

    /// <summary>
    /// In-process result transport. Receivers select messages by result kind and device code,
    /// then resolve the persisted record through their own DAO.
    /// </summary>
    public sealed class ResultMessageBus
    {
        private sealed class Subscription : IDisposable
        {
            private ResultMessageBus? owner;
            private readonly long id;

            public Subscription(ResultMessageBus owner, long id)
            {
                this.owner = owner;
                this.id = id;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref owner, null)?.Unsubscribe(id);
            }
        }

        private static readonly ILog log = LogManager.GetLogger(typeof(ResultMessageBus));
        private readonly object sync = new();
        private readonly Dictionary<long, Action<ResultMessage>> subscriptions = new();
        private long nextSubscriptionId;

        public static ResultMessageBus Default { get; } = new();

        public IDisposable Subscribe(Action<ResultMessage> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            long id = Interlocked.Increment(ref nextSubscriptionId);
            lock (sync)
            {
                subscriptions.Add(id, handler);
            }
            return new Subscription(this, id);
        }

        internal void PublishPersisted(
            string route,
            string resultKind,
            string deviceCode,
            string eventName,
            string serialNumber,
            string nodeId,
            int zIndex,
            int masterId,
            int masterResultType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(route);
            ArgumentException.ThrowIfNullOrWhiteSpace(resultKind);
            if (masterId <= 0) throw new ArgumentOutOfRangeException(nameof(masterId));
            Publish(new ResultMessage(
                route,
                resultKind,
                deviceCode ?? string.Empty,
                eventName ?? string.Empty,
                serialNumber ?? string.Empty,
                nodeId ?? string.Empty,
                zIndex,
                new ResultReference(masterId, masterResultType),
                attachment: null));
        }

        internal void Publish(ResultMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            Action<ResultMessage>[] handlers;
            lock (sync)
            {
                handlers = subscriptions.Values.ToArray();
            }

            foreach (Action<ResultMessage> handler in handlers)
            {
                try
                {
                    handler(message);
                }
                catch (Exception ex)
                {
                    log.Error($"结果消息处理失败：{message.Route}/{message.ResultKind}/{message.DeviceCode}", ex);
                }
            }
        }

        private void Unsubscribe(long id)
        {
            lock (sync)
            {
                subscriptions.Remove(id);
            }
        }
    }
}
