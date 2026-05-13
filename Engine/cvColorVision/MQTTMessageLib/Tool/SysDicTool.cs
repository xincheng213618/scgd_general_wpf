using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MQTTMessageLib.Tool;

public static class SysDicTool
{
	public static T GetValue<T>(Dictionary<string, KeyValuePair<string, string>> parameters, bool first, T storage, [CallerMemberName] string propertyName = "")
	{
		string text = "";
		if (parameters.TryGetValue(propertyName, out var value))
		{
			text = ((!first) ? value.Value : value.Key);
			if (typeof(T) == typeof(int))
			{
				if (string.IsNullOrEmpty(text))
				{
					text = "0";
				}
				return (T)(object)int.Parse(text);
			}
			if (typeof(T) == typeof(string))
			{
				return (T)(object)text;
			}
			if (typeof(T) == typeof(bool))
			{
				if (string.IsNullOrEmpty(text))
				{
					text = "False";
				}
				return (T)(object)bool.Parse(text);
			}
			if (typeof(T) == typeof(float))
			{
				if (string.IsNullOrEmpty(text))
				{
					text = "0.0";
				}
				return (T)(object)float.Parse(text);
			}
			if (typeof(T) == typeof(double))
			{
				if (string.IsNullOrEmpty(text))
				{
					text = "0.0";
				}
				return (T)(object)double.Parse(text);
			}
			if (typeof(T).IsEnum)
			{
				try
				{
					return (T)Enum.Parse(typeof(T), text);
				}
				catch (Exception)
				{
				}
			}
		}
		return default(T);
	}
}
