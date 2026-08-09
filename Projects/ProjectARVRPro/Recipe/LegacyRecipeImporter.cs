using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity;
using System.IO;

namespace ProjectARVRPro.Recipe
{
    internal sealed class LegacyRecipeImportResult
    {
        public int SourceCount { get; set; }
        public Dictionary<Type, IRecipeConfig> SharedConfigs { get; } = new();
        public Dictionary<string, LuminanceChromaticityRecipeConfig> LuminanceConfigs { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> UnsupportedTypeNames { get; } = new();
    }

    internal static class LegacyRecipeImporter
    {
        private static readonly Dictionary<string, string> LegacyLuminanceKeys = new(StringComparer.Ordinal)
        {
            ["ProjectARVRPro.Process.RGB.Red.RedRecipeConfig"] = "Red",
            ["ProjectARVRPro.Process.RGB.Green.GreenRecipeConfig"] = "Green",
            ["ProjectARVRPro.Process.RGB.Blue.BlueRecipeConfig"] = "Blue",
            ["ProjectARVRPro.Process.W25.W25RecipeConfig"] = "White"
        };

        private static readonly Dictionary<string, Type> CurrentRecipeTypes = typeof(RecipeConfig).Assembly
            .GetTypes()
            .Where(type => typeof(IRecipeConfig).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract && type.FullName != null)
            .ToDictionary(type => type.FullName!, StringComparer.Ordinal);

        public static bool TryReadFile(string filePath, out LegacyRecipeImportResult result, out string errorMessage)
        {
            result = new LegacyRecipeImportResult();
            errorMessage = string.Empty;

            try
            {
                return TryParse(File.ReadAllText(filePath), out result, out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool TryParse(string json, out LegacyRecipeImportResult result, out string errorMessage)
        {
            result = new LegacyRecipeImportResult();
            errorMessage = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    errorMessage = "Recipe 文件为空。";
                    return false;
                }

                if (JToken.Parse(json) is not JObject root || root[nameof(RecipeConfig.Configs)] is not JObject configsObject)
                {
                    errorMessage = "不是旧版 ARVRRecipe.json 格式，缺少 Configs 清单。";
                    return false;
                }

                foreach (JProperty property in configsObject.Properties().Where(property => property.Name != "$type"))
                {
                    result.SourceCount++;
                    string legacyTypeName = GetTypeFullName(property);
                    if (string.IsNullOrWhiteSpace(legacyTypeName))
                    {
                        result.UnsupportedTypeNames.Add(property.Name);
                        continue;
                    }

                    if (LegacyLuminanceKeys.TryGetValue(legacyTypeName, out string? outputKey))
                    {
                        if (!TryConvertLuminanceConfig(property.Value, out var config, out errorMessage))
                        {
                            errorMessage = $"迁移 {legacyTypeName} 失败: {errorMessage}";
                            return false;
                        }

                        result.LuminanceConfigs[outputKey] = config;
                        continue;
                    }

                    if (!CurrentRecipeTypes.TryGetValue(legacyTypeName, out Type? targetType))
                    {
                        result.UnsupportedTypeNames.Add(legacyTypeName);
                        continue;
                    }

                    if (!TryConvertConfig(property.Value, targetType, out var importedConfig, out errorMessage))
                    {
                        errorMessage = $"迁移 {legacyTypeName} 失败: {errorMessage}";
                        return false;
                    }

                    result.SharedConfigs[targetType] = importedConfig;
                }

                if (result.SourceCount == 0)
                {
                    errorMessage = "旧版 Recipe 清单中没有配置项。";
                    return false;
                }

                if (result.SharedConfigs.Count == 0 && result.LuminanceConfigs.Count == 0)
                {
                    errorMessage = "旧版 Recipe 清单中没有当前版本可迁移的配置项。";
                    return false;
                }

                return true;
            }
            catch (JsonException ex)
            {
                errorMessage = $"JSON 格式错误: {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static LuminanceChromaticityRecipeConfig CloneLuminanceConfig(LuminanceChromaticityRecipeConfig source)
        {
            return JsonConvert.DeserializeObject<LuminanceChromaticityRecipeConfig>(JsonConvert.SerializeObject(source)) ?? new();
        }

        private static string GetTypeFullName(JProperty property)
        {
            string assemblyQualifiedName = property.Name;
            if (property.Value is JObject valueObject && valueObject["$type"]?.Value<string>() is string metadataTypeName)
                assemblyQualifiedName = metadataTypeName;

            int separatorIndex = assemblyQualifiedName.IndexOf(',');
            return (separatorIndex >= 0 ? assemblyQualifiedName[..separatorIndex] : assemblyQualifiedName).Trim();
        }

        private static bool TryConvertLuminanceConfig(JToken source, out LuminanceChromaticityRecipeConfig config, out string errorMessage)
        {
            JToken normalized = CloneWithoutTypeMetadata(source);
            if (normalized is JObject configObject
                && configObject["CenterLuminance"] == null
                && configObject["CenterLunimance"] != null)
            {
                configObject["CenterLuminance"] = configObject["CenterLunimance"]!.DeepClone();
                configObject.Remove("CenterLunimance");
            }

            if (TryConvertConfig(normalized, typeof(LuminanceChromaticityRecipeConfig), out var converted, out errorMessage))
            {
                config = (LuminanceChromaticityRecipeConfig)converted;
                return true;
            }

            config = new LuminanceChromaticityRecipeConfig();
            return false;
        }

        private static bool TryConvertConfig(JToken source, Type targetType, out IRecipeConfig config, out string errorMessage)
        {
            try
            {
                JToken normalized = CloneWithoutTypeMetadata(source);
                if (normalized is JObject normalizedObject && !normalizedObject.Properties().Any())
                {
                    config = null!;
                    errorMessage = $"{targetType.FullName} 的配置内容为空。";
                    return false;
                }

                var serializer = JsonSerializer.Create(new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore
                });

                if (normalized.ToObject(targetType, serializer) is not IRecipeConfig converted)
                {
                    config = null!;
                    errorMessage = $"无法创建 {targetType.FullName}。";
                    return false;
                }

                config = converted;
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                config = null!;
                errorMessage = ex.Message;
                return false;
            }
        }

        private static JToken CloneWithoutTypeMetadata(JToken source)
        {
            JToken clone = source.DeepClone();
            RemoveTypeMetadata(clone);
            return clone;
        }

        private static void RemoveTypeMetadata(JToken token)
        {
            if (token is JObject objectToken)
            {
                objectToken.Property("$type")?.Remove();
                foreach (JProperty property in objectToken.Properties().ToList())
                    RemoveTypeMetadata(property.Value);
            }
            else if (token is JArray arrayToken)
            {
                foreach (JToken item in arrayToken)
                    RemoveTypeMetadata(item);
            }
        }
    }
}
