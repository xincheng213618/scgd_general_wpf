using ColorVision.ImageEditor.Settings;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace ColorVision.ImageEditor;

public partial class ImageView
{
    private const string ProfileSectionName = "ImageEditor.ViewProfiles";
    private static readonly ILog ProfileLog = LogManager.GetLogger(typeof(ImageView));
    private string? _configurationCode;

    /// <summary>Stable profile key supplied by the host. An empty key uses the global tool settings.</summary>
    public string? ConfigurationCode
    {
        get => _configurationCode;
        set
        {
            Dispatcher.VerifyAccess();
            string? code = NormalizeCode(value);
            if (string.Equals(_configurationCode, code, StringComparison.Ordinal))
                return;

            _configurationCode = code;
            ConfigurationCodeChanged?.Invoke(this, EventArgs.Empty);
            OnPropertyChanged();
        }
    }

    public event EventHandler? ConfigurationCodeChanged;

    internal T ReadProfileOptions<T>(string toolId, Func<T> createDefaults) where T : class
    {
        JObject? section = null;
        if (ConfigService.Instance is ConfigHandler handler)
        {
            try
            {
                section = handler.ReadOwnedSection(ProfileSectionName);
            }
            catch (Exception ex)
            {
                ProfileLog.Warn("ImageView profiles could not be read.", ex);
            }
        }

        JObject? profile = ConfigurationCode is { } code ? FindProfile(section, code) : null;
        return ReadOptions<T>(profile?[toolId], toolId)
            ?? ReadOptions<T>(FindProfile(section, code: null)?[toolId], toolId)
            ?? createDefaults();
    }

    private static T? ReadOptions<T>(JToken? token, string toolId) where T : class
    {
        if (token == null || token.Type == JTokenType.Null)
            return null;

        try
        {
            return token.ToObject<T>();
        }
        catch (JsonException ex)
        {
            ProfileLog.Warn($"ImageView options '{toolId}' could not be read.", ex);
            return null;
        }
    }

    internal bool TrySaveProfileOptions<T>(string toolId, T value, out string error) where T : class
    {
        string? code = ConfigurationCode;
        return TryUpdateProfile(section =>
        {
            JObject profile = GetOrCreateProfile(section, code);
            JToken options = JToken.FromObject(value);
            if (JToken.DeepEquals(profile[toolId], options))
                return false;

            profile[toolId] = options;
            return true;
        }, out error);
    }

    internal bool TryResetProfileOptions(string toolId, out string error)
    {
        string? code = ConfigurationCode;
        return TryUpdateProfile(section => FindProfile(section, code)?.Remove(toolId) == true, out error);
    }

    private static bool TryUpdateProfile(Func<JObject, bool> update, out string error)
    {
        if (ConfigService.Instance is ConfigHandler handler)
            return handler.TryUpdateOwnedSection(ProfileSectionName, update, out error);

        error = SettingsText.SessionOnly;
        return false;
    }

    private static JObject GetOrCreateProfile(JObject section, string? code) => code == null
        ? GetOrCreateObject(section, "Global")
        : GetOrCreateObject(GetOrCreateObject(section, "Profiles"), code);

    private static JObject? FindProfile(JObject? section, string? code) => code == null
        ? section?["Global"] as JObject
        : (section?["Profiles"] as JObject)?[code] as JObject;

    private static JObject GetOrCreateObject(JObject parent, string key)
    {
        if (parent[key] is JObject value)
            return value;

        if (parent.ContainsKey(key))
            throw new JsonSerializationException($"ImageView profile entry '{key}' is not a JSON object.");

        var created = new JObject();
        parent.Add(key, created);
        return created;
    }

    private static string? NormalizeCode(string? code) => string.IsNullOrWhiteSpace(code) ? null : code.Trim();
}
