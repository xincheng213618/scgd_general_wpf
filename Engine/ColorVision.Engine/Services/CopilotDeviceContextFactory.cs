using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace ColorVision.Engine.Services
{
    internal static class CopilotDeviceContextFactory
    {
        private const int MaxConfigProperties = 60;

        public static CopilotBusinessContextBundle Capture(DeviceService device)
        {
            ArgumentNullException.ThrowIfNull(device);

            var sourceId = $"device-service:{device.SysResourceModel?.Id}:{device.Code}";
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

        private static IReadOnlyList<CopilotContextProperty> BuildRuntimeProperties(DeviceService device)
        {
            return new[]
            {
                new CopilotContextProperty { Name = "IsAlive", Value = device.IsAlive ? "true" : "false" },
                new CopilotContextProperty { Name = "LastAliveTime", Value = device.LastAliveTime == default ? string.Empty : device.LastAliveTime.ToString("O", CultureInfo.InvariantCulture) },
                new CopilotContextProperty { Name = "HeartbeatTime", Value = device.HeartbeatTime.ToString(CultureInfo.InvariantCulture) },
                new CopilotContextProperty { Name = "ServiceType", Value = device.ServiceTypes.ToString() },
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
                    properties.Add(new CopilotContextProperty
                    {
                        Name = property.DisplayName ?? property.Name,
                        Value = Convert.ToString(property.GetValue(configuration), CultureInfo.InvariantCulture) ?? string.Empty,
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
