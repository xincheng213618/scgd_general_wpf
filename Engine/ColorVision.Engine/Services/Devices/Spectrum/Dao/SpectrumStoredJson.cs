using Newtonsoft.Json.Linq;

namespace ColorVision.Engine.Services.Devices.Spectrum.Dao;

internal static class SpectrumStoredJson
{
    // String-valued JSON columns were historically serialized again by the ORM.
    // Accept those existing rows without changing the stored spectrum or rewriting the database.
    internal static T? Read<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        JToken token = JToken.Parse(json);
        while (token.Type == JTokenType.String)
        {
            string? nested = token.Value<string>();
            if (string.IsNullOrWhiteSpace(nested)) return default;
            token = JToken.Parse(nested);
        }
        return token.Type == JTokenType.Null ? default : token.ToObject<T>();
    }
}
