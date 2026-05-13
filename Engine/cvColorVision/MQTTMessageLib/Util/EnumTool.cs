using System;

namespace MQTTMessageLib.Util;

public class EnumTool
{
	public static T ParseEnum<T>(string value, bool ignoreCase = false)
	{
		return (T)Enum.Parse(typeof(T), value, ignoreCase);
	}
}
