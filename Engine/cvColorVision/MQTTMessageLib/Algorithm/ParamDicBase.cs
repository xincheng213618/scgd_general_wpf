using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MQTTMessageLib.Algorithm;

public class ParamDicBase
{
	public string Version { get; private set; }

	[JsonIgnore]
	public string JsonConfig { get; private set; }

	[JsonIgnore]
	public Dictionary<string, string> Parameters { get; private set; }

	public virtual string ToJsonCfg()
	{
		return JsonConfig;
	}

	public bool IsValid()
	{
		if (string.IsNullOrEmpty(JsonConfig))
		{
			if (Parameters != null)
			{
				return Parameters.Count > 0;
			}
			return false;
		}
		return true;
	}

	public void AddParameters(Dictionary<string, string> parameters)
	{
		Parameters = parameters;
		Version = string.Empty;
		if (parameters != null && parameters.Count > 0)
		{
			AddParameters(JsonConvert.SerializeObject(this, new StringEnumConverter()));
		}
	}

	public void AddParameters(string jsonConfig)
	{
		JsonConfig = jsonConfig;
		Version = string.Empty;
	}

	public void AddParameters(Dictionary<string, string> parameters, string jsonConfig, string version)
	{
		Parameters = parameters;
		JsonConfig = jsonConfig;
		Version = version;
		if (parameters != null && parameters.Count > 0 && string.IsNullOrEmpty(jsonConfig))
		{
			JsonConfig = JsonConvert.SerializeObject(this, new StringEnumConverter());
		}
	}

	protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
	{
		storage = value;
		T val;
		object obj;
		if (Parameters.TryGetValue(propertyName, out var value2))
		{
			ref T reference = ref value;
			val = default(T);
			if (val == null)
			{
				val = reference;
				reference = ref val;
				if (val == null)
				{
					obj = null;
					goto IL_0048;
				}
			}
			obj = reference.ToString();
			goto IL_0048;
		}
		Dictionary<string, string> parameters = Parameters;
		ref T reference2 = ref value;
		val = default(T);
		object value3;
		if (val == null)
		{
			val = reference2;
			reference2 = ref val;
			if (val == null)
			{
				value3 = null;
				goto IL_0083;
			}
		}
		value3 = reference2.ToString();
		goto IL_0083;
		IL_0083:
		parameters.Add(propertyName, (string)value3);
		goto IL_0088;
		IL_0048:
		value2 = (string)obj;
		goto IL_0088;
		IL_0088:
		return true;
	}

	protected void SetProperty<T>(T value, [CallerMemberName] string propertyName = "")
	{
		T val;
		object obj;
		if (Parameters.TryGetValue(propertyName, out var value2))
		{
			ref T reference = ref value;
			val = default(T);
			if (val == null)
			{
				val = reference;
				reference = ref val;
				if (val == null)
				{
					obj = null;
					goto IL_0041;
				}
			}
			obj = reference.ToString();
			goto IL_0041;
		}
		Dictionary<string, string> parameters = Parameters;
		ref T reference2 = ref value;
		val = default(T);
		object value3;
		if (val == null)
		{
			val = reference2;
			reference2 = ref val;
			if (val == null)
			{
				value3 = null;
				goto IL_007b;
			}
		}
		value3 = reference2.ToString();
		goto IL_007b;
		IL_0041:
		value2 = (string)obj;
		return;
		IL_007b:
		parameters.Add(propertyName, (string)value3);
	}

	public T GetValue<T>(T storage, [CallerMemberName] string propertyName = "")
	{
		string text = "";
		if (Parameters != null && Parameters.TryGetValue(propertyName, out var value))
		{
			text = value;
			if (typeof(T) == typeof(int))
			{
				if (string.IsNullOrEmpty(text))
				{
					text = "0";
				}
				return (T)(object)Convert.ToInt32(text);
			}
			if (typeof(T) == typeof(uint))
			{
				if (string.IsNullOrEmpty(text))
				{
					text = "0";
				}
				return (T)(object)Convert.ToUInt32(text);
			}
			if (typeof(T) == typeof(long))
			{
				if (string.IsNullOrEmpty(text))
				{
					text = "0";
				}
				return (T)(object)Convert.ToInt64(text);
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
			if (typeof(T) == typeof(double[]))
			{
				if (string.IsNullOrEmpty(text))
				{
					text = "";
				}
				return (T)(object)StringToDoubleArray(text, new char[1] { ',' });
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

	public static double[] StringToDoubleArray(string input, char[] separator)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return Array.Empty<double>();
		}
		string[] array = input.Split(separator, StringSplitOptions.RemoveEmptyEntries);
		double[] array2 = new double[array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			if (!double.TryParse(array[i].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out array2[i]))
			{
				return Array.Empty<double>();
			}
		}
		return array2;
	}

	protected void CreateEmptyParams()
	{
		Parameters = new Dictionary<string, string>();
	}
}
