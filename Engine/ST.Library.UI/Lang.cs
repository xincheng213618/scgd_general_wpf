using System;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;

namespace ST.Library.UI;

public static class Lang
{
	private static readonly ResourceManager _resourceManager = new ResourceManager("ST.Library.UI.Properties.Resources", Assembly.GetExecutingAssembly());

	public static string Get(string key)
	{
		try
		{
			return _resourceManager.GetString(key) ?? ("[" + key + "]");
		}
		catch
		{
			return "[" + key + "]";
		}
	}

}
