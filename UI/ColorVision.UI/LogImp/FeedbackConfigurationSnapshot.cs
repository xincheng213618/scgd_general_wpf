using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;

namespace ColorVision.UI.LogImp
{
    /// <summary>Collects current, redacted JSON snapshots from paths explicitly supplied by their owners.</summary>
    public static class FeedbackConfigurationSnapshot
    {
        private const string RedactedValue = "[REDACTED]";
        private static readonly ILog log = LogManager.GetLogger(typeof(FeedbackConfigurationSnapshot));

        public static IReadOnlyList<(string EntryPath, string FilePath)> CollectFiles(IEnumerable<(string EntryPath, string FilePath)> sources)
        {
            var files = new List<(string, string)>();
            foreach (var (entryPath, sourcePath) in sources)
            {
                if (!File.Exists(sourcePath)) continue;
                string tempPath = Path.Combine(Path.GetTempPath(), $"ColorVision_Config_{Guid.NewGuid():N}.json");
                try
                {
                    JToken source;
                    using (var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                        source = ReadContainer(reader);

                    JToken snapshot = CreateRedactedSnapshot(source);
                    File.WriteAllText(tempPath, snapshot.ToString(Formatting.Indented), new UTF8Encoding(false));
                    files.Add((entryPath, tempPath));
                }
                catch (Exception ex)
                {
                    // Parser messages can quote input values. Include only the error type, never the raw JSON.
                    log.Debug($"Configuration snapshot failed for {Path.GetFileName(sourcePath)}: {ex.GetType().Name}");
                    File.WriteAllText(tempPath,
                        $"未能生成配置快照：{Path.GetFileName(sourcePath)}{Environment.NewLine}错误类型：{ex.GetType().Name}{Environment.NewLine}原始文件未加入反馈包。",
                        new UTF8Encoding(false));
                    files.Add(($"{entryPath}.collection-error.txt", tempPath));
                }
            }
            return files;
        }

        internal static JToken CreateRedactedSnapshot(JToken source)
        {
            ArgumentNullException.ThrowIfNull(source);
            JToken snapshot = source.DeepClone();
            RedactSensitiveValues(snapshot);
            return snapshot;
        }

        private static JToken ReadContainer(TextReader reader)
        {
            using var jsonReader = new JsonTextReader(reader) { DateParseHandling = DateParseHandling.None };
            JToken source = JToken.Load(jsonReader);
            if (source is not JObject and not JArray || jsonReader.Read())
                throw new JsonReaderException("Expected one JSON object or array.");
            return source;
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
            }
            else if (token is JArray array)
            {
                foreach (JToken item in array) RedactSensitiveValues(item);
            }
            else if (token is JValue { Type: JTokenType.String } value)
            {
                string text = value.Value<string>() ?? string.Empty;
                string trimmed = text.TrimStart();
                bool isJsonField = value.Parent is JProperty property && property.Name.EndsWith("Json", StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrWhiteSpace(text) || (!isJsonField && !trimmed.StartsWith('{') && !trimmed.StartsWith('[')))
                    return;
                try
                {
                    using var reader = new StringReader(text);
                    JToken nested = ReadContainer(reader);
                    RedactSensitiveValues(nested);
                    // ConfigJson remains a JSON string, preserving the configuration's original shape.
                    value.Value = nested.ToString(Formatting.None);
                }
                catch (JsonException)
                {
                    if (isJsonField) value.Value = RedactedValue;
                }
            }
        }

        private static bool IsSensitivePropertyName(string propertyName)
        {
            int separator = propertyName.LastIndexOf('.');
            if (separator >= 0 && IsSensitivePropertyName(propertyName[(separator + 1)..]))
                return true;
            string normalized = new(propertyName.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
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
