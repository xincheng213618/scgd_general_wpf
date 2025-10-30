using System;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;

namespace ST.Library.UI;

public static class Lang
{
	private static readonly ResourceManager _resourceManager;

	private static CultureInfo _currentCulture;

	static Lang()
	{
		_resourceManager = new ResourceManager("ST.Library.UI.Properties.Resources", Assembly.GetExecutingAssembly());
		AutoDetectSystemLanguage();
	}

	private static void AutoDetectSystemLanguage()
	{
		_currentCulture = CultureInfo.CurrentUICulture ?? CultureInfo.CurrentCulture;
		Console.WriteLine("自动检测到系统语言: " + _currentCulture.Name);
	}

	public static CultureInfo GetCurrentCulture()
	{
		return _currentCulture;
	}

	public static void SetLanguage(string cultureName)
	{
		try
		{
			_currentCulture = new CultureInfo(cultureName);
			Thread.CurrentThread.CurrentCulture = _currentCulture;
			Thread.CurrentThread.CurrentUICulture = _currentCulture;
		}
		catch (CultureNotFoundException)
		{
			Console.WriteLine("不支持的语言: " + cultureName);
		}
	}

	public static string Get(string key)
	{
		try
		{
			return _resourceManager.GetString(key, _currentCulture) ?? ("[" + key + "]");
		}
		catch
		{
			return "[" + key + "]";
		}
	}

	public static string Get(string key, params object[] args)
	{
		string text = Get(key);
		if (text == null)
		{
			return "[" + key + "]";
		}
		return string.Format(_currentCulture, text, args);
	}

	public static string[] GetSupportedLanguages()
	{
		return new string[2] { "en-US", "zh-CN" };
	}
}
