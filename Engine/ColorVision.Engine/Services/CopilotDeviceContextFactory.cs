using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ColorVision.Engine.Services
{
    internal static class CopilotDeviceContextFactory
    {
        private const int MaxConfigProperties = 60;

        public const string SourceIdPrefix = "device-service:";

        public const string FleetSourceId = "device-services:fleet";

        internal readonly record struct HeartbeatObservation(bool? IsAlive)
        {
            internal bool IsKnown => IsAlive.HasValue;
            internal string DeviceStatus => IsAlive switch { true => "Online", false => "Offline", _ => "Unknown" };
            internal string StatusText => IsAlive switch { true => "yes", false => "no", _ => "unknown" };
            internal string RuntimeText => IsAlive switch { true => "true", false => "false", _ => "unknown" };
        }

        internal static HeartbeatObservation ObserveHeartbeat(DeviceService device) => new(device.ObservedIsAlive);

        public static CopilotBusinessContextBundle Capture(DeviceService device)
        {
            ArgumentNullException.ThrowIfNull(device);

            var sourceId = GetSourceId(device);
            var heartbeat = ObserveHeartbeat(device);
            var snapshot = new CopilotDeviceContextSnapshot
            {
                SourceId = sourceId,
                Title = $"Device service · {FirstNonEmpty(device.Name, device.Code, device.GetType().Name)}",
                ServiceName = device.Name ?? string.Empty,
                ServiceCode = device.Code ?? string.Empty,
                ServiceType = device.ServiceTypes.ToString(),
                DeviceStatus = heartbeat.DeviceStatus,
                IsAlive = heartbeat.StatusText,
                LastAliveTime = device.LastAliveTime == default ? string.Empty : device.LastAliveTime.ToString("O", CultureInfo.InvariantCulture),
                SendTopic = device.SendTopic ?? string.Empty,
                SubscribeTopic = device.SubscribeTopic ?? string.Empty,
                RuntimeProperties = BuildRuntimeProperties(device, heartbeat),
                ConfigProperties = BuildConfigurationProperties(device.GetConfig()),
            };

            var item = CopilotBusinessContextBuilder.BuildDeviceContextItem(snapshot);
            return CopilotBusinessContextBundle.FromItem(sourceId, item);
        }

        public static CopilotBusinessContextBundle? CaptureFleet(IEnumerable<DeviceService> devices)
        {
            ArgumentNullException.ThrowIfNull(devices);
            var currentDevices = devices.Where(device => device != null).ToArray();
            if (currentDevices.Length == 0)
                return null;
            var observations = currentDevices.Select(device => (Device: device, Heartbeat: ObserveHeartbeat(device))).ToArray();

            var snapshot = new CopilotDeviceFleetContextSnapshot
            {
                SourceId = FleetSourceId,
                TotalDevices = currentDevices.Length,
                OnlineDevices = observations.Count(item => item.Heartbeat.IsAlive == true),
                OfflineDevices = observations.Count(item => item.Heartbeat.IsAlive == false),
                UnknownDevices = observations.Count(item => !item.Heartbeat.IsKnown),
                Devices = observations.Take(MaxConfigProperties).Select(item => new CopilotDeviceHealthContextSnapshot
                {
                    ServiceName = item.Device.Name ?? string.Empty,
                    ServiceCode = item.Device.Code ?? string.Empty,
                    ServiceType = item.Device.ServiceTypes.ToString(),
                    IsAlive = item.Heartbeat.IsAlive ?? false,
                    HeartbeatObservationKnown = item.Heartbeat.IsKnown,
                    OperationalStatus = item.Device.GetMQTTService()?.DeviceStatus.ToString() ?? string.Empty,
                    LastAliveTime = item.Device.LastAliveTime == default
                        ? string.Empty
                        : item.Device.LastAliveTime.ToString("O", CultureInfo.InvariantCulture),
                }).ToArray(),
            };
            var item = CopilotBusinessContextBuilder.BuildDeviceFleetContextItem(snapshot);
            return CopilotBusinessContextBundle.FromItem(FleetSourceId, item);
        }

        public static string GetSourceId(DeviceService device)
        {
            ArgumentNullException.ThrowIfNull(device);
            var resourceId = device.SysResourceModel?.Id ?? 0;
            return resourceId > 0
                ? $"{SourceIdPrefix}{resourceId}"
                : $"{SourceIdPrefix}runtime-{RuntimeHelpers.GetHashCode(device):x}";
        }

        private static CopilotContextProperty[] BuildRuntimeProperties(DeviceService device, HeartbeatObservation heartbeat)
        {
            return new[]
            {
                new CopilotContextProperty { Name = "IsAlive", Value = heartbeat.RuntimeText },
                new CopilotContextProperty { Name = "LastAliveTime", Value = device.LastAliveTime == default ? string.Empty : device.LastAliveTime.ToString("O", CultureInfo.InvariantCulture) },
                new CopilotContextProperty { Name = "ServiceType", Value = device.ServiceTypes.ToString() },
                new CopilotContextProperty { Name = "OperationalStatus", Value = device.GetMQTTService()?.DeviceStatus.ToString() ?? string.Empty },
            };
        }

        private static IReadOnlyList<CopilotContextProperty> BuildConfigurationProperties(object? configuration)
        {
            if (configuration == null)
                return Array.Empty<CopilotContextProperty>();

            var properties = new List<CopilotContextProperty>();
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(configuration))
            {
                if (!property.IsBrowsable || !IsSimpleType(property.PropertyType))
                    continue;

                try
                {
                    var rawValue = Convert.ToString(property.GetValue(configuration), CultureInfo.InvariantCulture) ?? string.Empty;
                    properties.Add(new CopilotContextProperty
                    {
                        Name = property.DisplayName ?? property.Name,
                        // Use the CLR property name for the security decision. DisplayName can be
                        // localized and must not make SN/token/license fields look non-sensitive.
                        Value = CopilotBusinessContextBuilder.MaskIfSensitive(property.Name, rawValue),
                    });
                }
                catch
                {
                }

                if (properties.Count >= MaxConfigProperties)
                    break;
            }

            return properties;
        }

        private static bool IsSimpleType(Type type)
        {
            var source = Nullable.GetUnderlyingType(type) ?? type;
            return source.IsPrimitive
                || source.IsEnum
                || source == typeof(string)
                || source == typeof(decimal)
                || source == typeof(DateTime)
                || source == typeof(DateTimeOffset)
                || source == typeof(TimeSpan)
                || source == typeof(Guid);
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
    }
}
