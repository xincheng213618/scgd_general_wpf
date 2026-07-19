using MQTTMessageLib;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.RC
{
    /// <summary>
    /// Retains the newest RC snapshots received before ServiceManager is ready.
    /// Taking a batch is atomic so an update cannot be partially consumed.
    /// </summary>
    internal sealed class PendingServiceUpdateBuffer
    {
        private readonly object _lock = new();
        private Dictionary<CVServiceType, List<MQTTNodeService>>? _services;
        private List<MQTTNodeServiceStatus>? _statuses;

        public void StoreServices(Dictionary<CVServiceType, List<MQTTNodeService>> services)
        {
            lock (_lock)
            {
                _services = services;
            }
        }

        public void StoreStatuses(List<MQTTNodeServiceStatus> statuses)
        {
            lock (_lock)
            {
                _statuses = statuses;
            }
        }

        public PendingServiceUpdates Take()
        {
            lock (_lock)
            {
                PendingServiceUpdates updates = new(_services, _statuses);
                _services = null;
                _statuses = null;
                return updates;
            }
        }
    }

    internal readonly record struct PendingServiceUpdates(
        Dictionary<CVServiceType, List<MQTTNodeService>>? Services,
        List<MQTTNodeServiceStatus>? Statuses);
}
