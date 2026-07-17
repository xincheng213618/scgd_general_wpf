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

        public static CopilotBusinessContextBundle Capture(DeviceService device)
        {
            ArgumentNullException.ThrowIfNull(device);

            var sourceId = GetSourceId(device);
            var snapshot = new CopilotDeviceContextSnapshot
            {
                SourceId = sourceId,
                Title = $"Device service · {FirstNonEmpty(device.Name, device.Code, device.GetType().Name)}",
                ServiceName = device.Name ?? string.Empty,
                ServiceCode = device.Code ?? string.Empty,
                ServiceType = device.ServiceTypes.ToString(),
                DeviceStatus = device.IsAlive ? "Online" : "Offline",
                IsAlive = device.IsAlive ? "yes" : "no",
                LastAliveTime = device.LastAliveTime == default ? string.Empty : device.LastAliveTime.ToString("O", CultureInfo.InvariantCulture),
                HeartbeatTime = device.HeartbeatTime.ToString(CultureInfo.InvariantCulture),
                SendTopic = device.SendTopic ?? string.Empty,
                SubscribeTopic = device.SubscribeTopic ?? string.Empty,
                RuntimeProperties = BuildRuntimeProperties(device),
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

            var snapshot = new CopilotDeviceFleetContextSnapshot
            {
                SourceId = FleetSourceId,
                TotalDevices = currentDevices.Length,
                OnlineDevices = currentDevices.Count(device => device.IsAlive),
                OfflineDevices = currentDevices.Count(device => !device.IsAlive),
                Devices = currentDevices.Take(MaxConfigProperties).Select(device => new CopilotDeviceHealthContextSnapshot
                {
                    ServiceName = device.Name ?? string.Empty,
                    ServiceCode = device.Code ?? string.Empty,
                    ServiceType = device.ServiceTypes.ToString(),
                    IsAlive = device.IsAlive,
                    OperationalStatus = device.GetMQTTService()?.DeviceStatus.ToString() ?? string.Empty,
                    LastAliveTime = device.LastAliveTime == default
                        ? string.Empty
                        : device.LastAliveTime.ToString("O", CultureInfo.InvariantCulture),
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

        private static CopilotContextProperty[] BuildRuntimeProperties(DeviceService device)
        {
            return new[]
            {
                new CopilotContextProperty { Name = "IsAlive", Value = device.IsAlive ? "true" : "false" },
                new CopilotContextProperty { Name = "LastAliveTime", Value = device.LastAliveTime == default ? string.Empty : device.LastAliveTime.ToString("O", CultureInfo.InvariantCulture) },
                new CopilotContextProperty { Name = "HeartbeatTime", Value = device.HeartbeatTime.ToString(CultureInfo.InvariantCulture) },
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
