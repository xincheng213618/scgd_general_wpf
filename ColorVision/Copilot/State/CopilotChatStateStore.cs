using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.IO;

namespace ColorVision.Copilot
{
    public sealed class CopilotChatStateStore
    {
        private static readonly Lazy<CopilotChatStateStore> _instance = new(() => new CopilotChatStateStore());
        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static CopilotChatStateStore Instance => _instance.Value;

        public string RootDirectoryPath { get; }

        public string StateDirectoryPath { get; }

        public string StateFilePath { get; }

        private CopilotChatStateStore()
        {
            RootDirectoryPath = Path.Combine(Environments.DirLocalAppData, "Copilot");
            StateDirectoryPath = Path.Combine(RootDirectoryPath, "State");
            StateFilePath = Path.Combine(StateDirectoryPath, "chat-state.json");
        }

        public CopilotChatState Load()
        {
            EnsureDirectory();

            if (!File.Exists(StateFilePath))
                return new CopilotChatState();

            try
            {
                var json = File.ReadAllText(StateFilePath);
                return JsonConvert.DeserializeObject<CopilotChatState>(json, SerializerSettings) ?? new CopilotChatState();
            }
            catch
            {
                return new CopilotChatState();
            }
        }

        public void Save(CopilotChatState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            EnsureDirectory();
            var json = JsonConvert.SerializeObject(state, SerializerSettings);
            File.WriteAllText(StateFilePath, json);
        }

        private void EnsureDirectory()
        {
            if (!Directory.Exists(StateDirectoryPath))
                Directory.CreateDirectory(StateDirectoryPath);
        }
    }
}