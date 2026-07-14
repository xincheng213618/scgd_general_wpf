using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;

namespace ST.Library.UI;

public static class Lang
{
	private static readonly ResourceManager _resourceManager = new ResourceManager("ST.Library.UI.Properties.Resources", Assembly.GetExecutingAssembly());
	private static readonly List<ResourceManager> _externalManagers = new();

	public static void RegisterResourceManager(ResourceManager manager)
	{
		if (manager != null && !_externalManagers.Contains(manager))
		{
			_externalManagers.Add(manager);
		}
	}

	public static string Get(string key)
	{
		return GetOrDefault(key, "[" + key + "]");
	}

	public static string GetOrDefault(string key, string fallback)
	{
		try
		{
			string value = _resourceManager.GetString(key);
			if (value != null) return value;
		}
		catch { }

		for (int i = _externalManagers.Count - 1; i >= 0; i--)
		{
			try
			{
				string value = _externalManagers[i].GetString(key);
				if (value != null) return value;
			}
			catch { }
		}

		return fallback;
	}

}
