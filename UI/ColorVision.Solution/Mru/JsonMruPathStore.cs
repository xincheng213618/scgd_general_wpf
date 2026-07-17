using log4net;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ColorVision.Solution.Mru
{
    internal sealed class JsonMruPathStore : IMruPathStore
    {
        private const int CurrentSchemaVersion = 1;
        private static readonly ILog Log = LogManager.GetLogger(typeof(JsonMruPathStore));
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        private readonly string _filePath;

        public JsonMruPathStore(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            _filePath = Path.GetFullPath(filePath);
        }

        public IReadOnlyList<MruPathEntry> Load()
        {
            return File.Exists(_filePath) ? LoadJson() : Array.Empty<MruPathEntry>();
        }

        public void Save(IReadOnlyList<MruPathEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
            string directoryPath = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(directoryPath);
            string temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                var document = new MruPathDocument
                {
                    Items = entries.ToList(),
                };
                string json = JsonSerializer.Serialize(document, JsonOptions);
                File.WriteAllText(
                    temporaryPath,
                    json,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(temporaryPath, _filePath, overwrite: true);
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }

        private IReadOnlyList<MruPathEntry> LoadJson()
        {
            try
            {
                string json = File.ReadAllText(_filePath);
                MruPathDocument? document = JsonSerializer.Deserialize<MruPathDocument>(json, JsonOptions);
                if (document?.SchemaVersion != CurrentSchemaVersion)
                {
                    Log.Warn($"MRU 路径记录版本无效：{_filePath}");
                    return Array.Empty<MruPathEntry>();
                }
                return document.Items ?? (IReadOnlyList<MruPathEntry>)Array.Empty<MruPathEntry>();
            }
            catch (Exception ex) when (IsStorageException(ex))
            {
                Log.Warn($"无法读取 MRU 路径记录：{_filePath}", ex);
                return Array.Empty<MruPathEntry>();
            }
        }

        private static bool IsStorageException(Exception exception)
        {
            return exception is IOException
                or UnauthorizedAccessException
                or JsonException
                or ArgumentException
                or NotSupportedException;
        }

        private static void TryDeleteTemporaryFile(string temporaryPath)
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Debug($"无法删除 MRU 路径临时文件：{temporaryPath}", ex);
            }
        }

        private sealed class MruPathDocument
        {
            public int SchemaVersion { get; set; } = CurrentSchemaVersion;
            public List<MruPathEntry>? Items { get; set; } = new();
        }
    }
}
