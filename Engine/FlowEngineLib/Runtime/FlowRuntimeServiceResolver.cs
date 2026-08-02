using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FlowEngineLib.Runtime;

internal interface IFlowServiceResolver
{
	MQTTServiceInfo GetService(string serviceType, string serviceCode);
}

internal sealed class FlowRuntimeServiceCatalog
{
	private readonly Dictionary<string, Dictionary<string, MQTTServiceInfo>>
		serviceGroups;

	public static FlowRuntimeServiceCatalog Empty { get; } =
		new FlowRuntimeServiceCatalog(
			new Dictionary<string, Dictionary<string, MQTTServiceInfo>>());

	public FlowRuntimeServiceCatalog(
		Dictionary<string, Dictionary<string, MQTTServiceInfo>> serviceGroups)
	{
		this.serviceGroups = serviceGroups
			?? throw new ArgumentNullException(nameof(serviceGroups));
	}

	public bool TryGetService(
		string serviceType,
		string serviceCode,
		out MQTTServiceInfo service)
	{
		service = null;
		return serviceType != null
			&& serviceCode != null
			&& serviceGroups.TryGetValue(
				serviceType,
				out Dictionary<string, MQTTServiceInfo> services)
			&& services.TryGetValue(
				serviceCode,
				out service);
	}
}

/// <summary>
/// Provides one runtime host with an immutable MQTT service snapshot.
/// </summary>
internal sealed class FlowRuntimeServiceResolver : IFlowServiceResolver
{
	private static readonly AsyncLocal<IFlowServiceResolver>
		ambientResolver = new();

	private FlowRuntimeServiceCatalog catalog =
		FlowRuntimeServiceCatalog.Empty;

	public static IFlowServiceResolver Ambient =>
		ambientResolver.Value;

	public MQTTServiceInfo GetService(string serviceType, string serviceCode)
	{
		FlowRuntimeServiceCatalog snapshot =
			Volatile.Read(ref catalog);
		return snapshot.TryGetService(
			serviceType,
			serviceCode,
			out MQTTServiceInfo service)
				? service
				: null;
	}

	public FlowRuntimeServiceCatalog Replace(
		FlowRuntimeServiceCatalog nextCatalog)
	{
		ArgumentNullException.ThrowIfNull(nextCatalog);
		return Interlocked.Exchange(
			ref catalog,
			nextCatalog);
	}

	public IDisposable EnterLoadScope()
	{
		IFlowServiceResolver previousResolver =
			ambientResolver.Value;
		ambientResolver.Value = this;
		return new ResolverScope(previousResolver);
	}

	public static FlowRuntimeServiceCatalog CreateCatalog(
		IEnumerable<MQTTServiceInfo> services)
	{
		ArgumentNullException.ThrowIfNull(services);
		var nextGroups =
			new Dictionary<string, Dictionary<string, MQTTServiceInfo>>();
		foreach (MQTTServiceInfo service in services)
		{
			ArgumentNullException.ThrowIfNull(service);
			if (string.IsNullOrEmpty(service.ServiceType)
				|| string.IsNullOrEmpty(service.ServiceCode))
			{
				continue;
			}
			if (!nextGroups.TryGetValue(
				service.ServiceType,
				out Dictionary<string, MQTTServiceInfo> serviceGroup))
			{
				serviceGroup =
					new Dictionary<string, MQTTServiceInfo>();
				nextGroups.Add(
					service.ServiceType,
					serviceGroup);
			}
			if (!serviceGroup.ContainsKey(service.ServiceCode))
			{
				serviceGroup.Add(
					service.ServiceCode,
					service);
			}
		}
		return new FlowRuntimeServiceCatalog(nextGroups);
	}

	public static MQTTServiceInfo[] CreateSnapshot(
		IEnumerable<MQTTServiceInfo> services)
	{
		if (services == null)
		{
			return Array.Empty<MQTTServiceInfo>();
		}
		return services
			.Select(CloneService)
			.ToArray();
	}

	private static MQTTServiceInfo CloneService(MQTTServiceInfo service)
	{
		ArgumentNullException.ThrowIfNull(service);
		if (string.IsNullOrEmpty(service.ServiceType))
		{
			throw new ArgumentException(
				"MQTT service type cannot be empty.",
				nameof(service));
		}
		var clone = new MQTTServiceInfo
		{
			ServiceType = service.ServiceType,
			ServiceCode = service.ServiceCode,
			SubscribeTopic = service.SubscribeTopic,
			PublishTopic = service.PublishTopic,
			Token = service.Token
		};
		foreach (MQTTDeviceInfo device in service.Devices.Values)
		{
			clone.AddDevice(
				device.ID,
				device.DeviceCode);
		}
		return clone;
	}

	private sealed class ResolverScope : IDisposable
	{
		private readonly IFlowServiceResolver previousResolver;

		private bool isDisposed;

		public ResolverScope(
			IFlowServiceResolver previousResolver)
		{
			this.previousResolver = previousResolver;
		}

		public void Dispose()
		{
			if (isDisposed)
			{
				return;
			}
			isDisposed = true;
			ambientResolver.Value = previousResolver;
		}
	}
}
