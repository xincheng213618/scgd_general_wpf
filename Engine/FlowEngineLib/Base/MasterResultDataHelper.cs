using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace FlowEngineLib.Base;

internal static class MasterResultDataHelper
{
	public static bool TryRead(object data, string nodeType, out string masterValue, out int masterId, out int masterResultType)
	{
		masterValue = null;
		masterId = -1;
		masterResultType = GetDefaultMasterResultType(nodeType);

		if (data == null)
		{
			return false;
		}

		bool hasMasterValue = TryGetValue(data, "MasterValue", out masterValue);
		bool hasMasterId = TryGetValue(data, "MasterId", out masterId);
		bool hasMasterResultType = TryGetValue(data, "MasterResultType", out masterResultType);
		if (!hasMasterResultType)
		{
			hasMasterResultType = TryGetValue(data, "ResultType", out masterResultType);
		}

		return hasMasterValue || hasMasterId || hasMasterResultType;
	}

	private static int GetDefaultMasterResultType(string nodeType)
	{
		return nodeType switch
		{
			"SMU" => 200,
			"Spectrum" => 300,
			_ => -1
		};
	}

	private static bool TryGetValue<T>(object source, string memberName, out T value)
	{
		value = default;
		if (!TryGetRawValue(source, memberName, out object rawValue))
		{
			return false;
		}

		return TryConvert(rawValue, out value);
	}

	private static bool TryGetRawValue(object source, string memberName, out object value)
	{
		value = null;
		if (source is JObject jObject)
		{
			return TryGetTokenValue(jObject[memberName], out value);
		}

		if (source is JToken token)
		{
			return token.Type == JTokenType.Object && TryGetTokenValue(token[memberName], out value);
		}

		if (source is IDictionary<string, object> dictionary && dictionary.TryGetValue(memberName, out value))
		{
			return value != null;
		}

		var property = source.GetType().GetProperty(memberName);
		if (property == null)
		{
			return false;
		}

		value = property.GetValue(source);
		return value != null;
	}

	private static bool TryGetTokenValue(JToken token, out object value)
	{
		value = null;
		if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
		{
			return false;
		}

		if (token is JValue jValue)
		{
			value = jValue.Value;
			return value != null;
		}

		value = token;
		return true;
	}

	private static bool TryConvert<T>(object rawValue, out T value)
	{
		value = default;
		if (rawValue == null)
		{
			return false;
		}

		if (rawValue is T typedValue)
		{
			value = typedValue;
			return true;
		}

		if (rawValue is JToken token)
		{
			try
			{
				T tokenValue = token.ToObject<T>();
				if (tokenValue == null)
				{
					return false;
				}

				value = tokenValue;
				return true;
			}
			catch
			{
				return false;
			}
		}

		try
		{
			Type targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
			object convertedValue = targetType == typeof(string)
				? Convert.ToString(rawValue)
				: Convert.ChangeType(rawValue, targetType);
			if (convertedValue == null)
			{
				return false;
			}

			value = (T)convertedValue;
			return true;
		}
		catch
		{
			return false;
		}
	}
}