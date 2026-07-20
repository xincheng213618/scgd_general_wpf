using System;
using System.Collections.Concurrent;
using System.Threading;

namespace FlowEngineLib.Base;

/// <summary>
/// Holds process-local resources that must never be serialized with a flow message.
/// A single instance is shared by all copies of the same <see cref="CVStartCFC"/>.
/// </summary>
public sealed class FlowRuntimeResources : IDisposable
{
	private readonly ConcurrentDictionary<string, object> resources = new(StringComparer.Ordinal);
	private int disposed;

	public bool IsDisposed => Volatile.Read(ref disposed) != 0;

	public void Set(string key, object value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentNullException.ThrowIfNull(value);

		if (IsDisposed)
		{
			DisposeValue(value);
			throw new ObjectDisposedException(nameof(FlowRuntimeResources));
		}

		while (true)
		{
			if (!resources.TryGetValue(key, out object previous))
			{
				if (resources.TryAdd(key, value))
				{
					break;
				}
				continue;
			}

			if (!resources.TryUpdate(key, value, previous))
			{
				continue;
			}

			if (!ReferenceEquals(previous, value))
			{
				DisposeValue(previous);
			}
			break;
		}

		if (IsDisposed)
		{
			if (resources.TryRemove(key, out object removed))
			{
				DisposeValue(removed);
			}
			throw new ObjectDisposedException(nameof(FlowRuntimeResources));
		}
	}

	public bool TryGet<T>(string key, out T value) where T : class
	{
		value = null;
		return !IsDisposed
			&& resources.TryGetValue(key, out object resource)
			&& (value = resource as T) != null;
	}

	public bool Remove(string key)
	{
		if (!resources.TryRemove(key, out object value))
		{
			return false;
		}

		DisposeValue(value);
		return true;
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref disposed, 1) != 0)
		{
			return;
		}

		foreach (var item in resources)
		{
			if (resources.TryRemove(item.Key, out object value))
			{
				DisposeValue(value);
			}
		}
	}

	private static void DisposeValue(object value)
	{
		if (value is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}
}
