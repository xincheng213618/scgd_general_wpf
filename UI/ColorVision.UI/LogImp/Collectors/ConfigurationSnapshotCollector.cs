using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;

namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// Adds a redacted copy of the active application configuration to feedback packages.
    /// </summary>
    public sealed class ConfigurationSnapshotCollector : IFeedbackLogCollector
    {
        private const string RedactedValue = "[REDACTED]";
        private static readonly ILog log = LogManager.GetLogger(typeof(ConfigurationSnapshotCollector));

        public string Name => "Configuration Snapshot";
        public string Description => "Active ColorVision configuration with credentials and tokens redacted";
        public int Order => 15;

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
        {
            string? tempPath = null;
            try
            {
                string configPath = ConfigHandler.GetInstance().ConfigFilePath;
                if (!File.Exists(configPath))
                    return [];

                JObject source;
                using (var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    source = JObject.Load(jsonReader);
                    if (jsonReader.Read())
                        throw new JsonReaderException("Additional content was found after the configuration JSON object.");
                }

                JObject snapshot = CreateRedactedSnapshot(source);
                tempPath = Path.Combine(Path.GetTempPath(), $"ColorVision_Config_{Guid.NewGuid():N}.json");
                File.WriteAllText(tempPath, snapshot.ToString(Formatting.Indented), new UTF8Encoding(false));
                return [("Config/ColorVisionConfig.json", tempPath)];
            }
            catch (Exception ex)
            {
                log.Debug($"ConfigurationSnapshotCollector failed: {ex.Message}");
                if (tempPath != null && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
                return [];
            }
        }

        internal static JObject CreateRedactedSnapshot(JObject source)
        {
            ArgumentNullException.ThrowIfNull(source);
            var snapshot = (JObject)source.DeepClone();
            RedactSensitiveValues(snapshot);
            return snapshot;
        }

        private static void RedactSensitiveValues(JToken token)
        {
            if (token is JObject obj)
            {
                foreach (JProperty property in obj.Properties().ToArray())
                {
                    if (IsSensitivePropertyName(property.Name))
                        property.Value = RedactedValue;
                    else
                        RedactSensitiveValues(property.Value);
                }
                return;
            }

            if (token is JArray array)
            {
                foreach (JToken item in array)
                    RedactSensitiveValues(item);
            }
        }

        private static bool IsSensitivePropertyName(string propertyName)
        {
            string normalized = new(propertyName
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());

            return normalized is "password" or "passwd" or "pwd" or "token" or "credential" or "credentials" or "connectionstring" or "authorization" or "authentication"
                || normalized.EndsWith("password", StringComparison.Ordinal)
                || normalized.EndsWith("passwd", StringComparison.Ordinal)
                || normalized.EndsWith("pwd", StringComparison.Ordinal)
                || normalized.EndsWith("token", StringComparison.Ordinal)
                || normalized.EndsWith("secret", StringComparison.Ordinal)
                || normalized.EndsWith("apikey", StringComparison.Ordinal)
                || normalized.EndsWith("accesskey", StringComparison.Ordinal)
                || normalized.EndsWith("privatekey", StringComparison.Ordinal)
                || normalized.EndsWith("connectionstring", StringComparison.Ordinal)
                || normalized.EndsWith("credential", StringComparison.Ordinal)
                || normalized.EndsWith("credentials", StringComparison.Ordinal)
                || normalized.EndsWith("cookie", StringComparison.Ordinal);
        }
    }
}
