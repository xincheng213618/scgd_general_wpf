using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaptchaOCR
{
    public enum ModelType
    {
        LengthAndChars,
        CtcSequence
    }

    public enum CharsetType
    {
        Alphanumeric62,
        DdddBeta,
        DdddOld
    }

    public class CaptchaModelInfo
    {
        [JsonIgnore]
        internal static string PluginDirectory => System.IO.Path.GetDirectoryName(typeof(CaptchaModelInfo).Assembly.Location)
            ?? AppDomain.CurrentDomain.BaseDirectory;

        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public ModelType ModelType { get; set; } = ModelType.LengthAndChars;
        public CharsetType Charset { get; set; } = CharsetType.Alphanumeric62;
        public int InputChannels { get; set; } = 3;
        public int InputHeight { get; set; } = 60;
        public int InputWidth { get; set; } = 160;
        public bool VariableWidth { get; set; } = false;
        public int MinLength { get; set; } = 3;
        public int MaxLength { get; set; } = 6;
        public bool Enabled { get; set; } = true;

        [JsonIgnore]
        public string? ErrorMessage { get; set; }

        [JsonIgnore]
        public bool IsAvailable => Enabled && File.Exists(ResolvePath());

        public string ResolvePath()
        {
            if (System.IO.Path.IsPathRooted(Path))
                return Path;
            return System.IO.Path.Combine(PluginDirectory, Path);
        }
    }

    public class CaptchaModelCatalog
    {
        public List<CaptchaModelInfo> Models { get; set; } = new();
    }

    public static class ModelCatalogLoader
    {
        private static readonly string CatalogPath = System.IO.Path.Combine(
            CaptchaModelInfo.PluginDirectory, "models.json");

        private static readonly string UserConfigPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ColorVision", "CaptchaOCR", "last_model.json");

        public static CaptchaModelCatalog LoadCatalog()
        {
            if (File.Exists(CatalogPath))
            {
                try
                {
                    var json = File.ReadAllText(CatalogPath);
                    var catalog = JsonSerializer.Deserialize<CaptchaModelCatalog>(json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            Converters = { new JsonStringEnumConverter() }
                        });
                    if (catalog != null)
                        return catalog;
                }
                catch { }
            }

            return new CaptchaModelCatalog
            {
                Models = new List<CaptchaModelInfo>
                {
                    new()
                    {
                        Id = "default",
                        Name = "专用模型",
                        Path = "captcha_model.onnx",
                        ModelType = ModelType.LengthAndChars,
                        Charset = CharsetType.Alphanumeric62,
                        InputChannels = 3,
                        InputHeight = 60,
                        InputWidth = 160,
                        VariableWidth = false,
                        MinLength = 3,
                        MaxLength = 6
                    }
                }
            };
        }

        public static void SaveLastSelected(string modelId)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(UserConfigPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(UserConfigPath, JsonSerializer.Serialize(new { LastModelId = modelId }));
            }
            catch { }
        }

        public static string? LoadLastSelected()
        {
            try
            {
                if (File.Exists(UserConfigPath))
                {
                    var json = File.ReadAllText(UserConfigPath);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("LastModelId", out var prop))
                        return prop.GetString();
                }
            }
            catch { }
            return null;
        }

        // ---- 生成规则持久化 ----

        private static readonly string GenerationConfigPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ColorVision", "CaptchaOCR", "generation_config.json");

        public static void SaveGenerationConfig(int length, CharacterMode mode, int digitCount)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(GenerationConfigPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(GenerationConfigPath, JsonSerializer.Serialize(new
                {
                    Length = length,
                    Mode = mode.ToString(),
                    DigitCount = digitCount
                }));
            }
            catch { }
        }

        public static (int Length, CharacterMode Mode, int DigitCount) LoadGenerationConfig()
        {
            try
            {
                if (File.Exists(GenerationConfigPath))
                {
                    var json = File.ReadAllText(GenerationConfigPath);
                    var doc = JsonDocument.Parse(json);
                    int length = 4;
                    int digitCount = -1;
                    var mode = CharacterMode.Alphanumeric;

                    if (doc.RootElement.TryGetProperty("Length", out var lenProp))
                        length = lenProp.GetInt32();
                    if (doc.RootElement.TryGetProperty("DigitCount", out var digProp))
                        digitCount = digProp.GetInt32();
                    if (doc.RootElement.TryGetProperty("Mode", out var modeProp))
                    {
                        var modeStr = modeProp.GetString();
                        if (Enum.TryParse<CharacterMode>(modeStr, out var parsed))
                            mode = parsed;
                    }

                    return (length, mode, digitCount);
                }
            }
            catch { }
            return (4, CharacterMode.Alphanumeric, -1);
        }
    }
}
