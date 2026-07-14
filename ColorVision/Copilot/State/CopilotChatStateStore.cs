using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    public interface ICopilotChatStateStore
    {
        string AttachmentDirectoryPath { get; }

        CopilotChatState Load();

        void Save(CopilotChatState state);

        int CleanupOrphanedAttachments(CopilotChatState state);
    }

    public sealed class CopilotChatStateStore : ICopilotChatStateStore
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

        public string BackupStateFilePath { get; }

        public string AttachmentDirectoryPath { get; }

        private CopilotChatStateStore()
            : this(Path.Combine(Environments.DirLocalAppData, "Copilot"))
        {
        }

        public CopilotChatStateStore(string rootDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(rootDirectoryPath))
                throw new ArgumentException("A root directory is required.", nameof(rootDirectoryPath));

            RootDirectoryPath = Path.GetFullPath(rootDirectoryPath);
            StateDirectoryPath = Path.Combine(RootDirectoryPath, "State");
            StateFilePath = Path.Combine(StateDirectoryPath, "chat-state.json");
            BackupStateFilePath = StateFilePath + ".bak";
            AttachmentDirectoryPath = Path.Combine(StateDirectoryPath, "Attachments");
        }

        public CopilotChatState Load()
        {
            EnsureDirectory();

            if (!File.Exists(StateFilePath))
                return new CopilotChatState();

            if (TryLoad(StateFilePath, out var state))
                return state;

            return TryLoad(BackupStateFilePath, out state) ? state : new CopilotChatState();
        }

        public void Save(CopilotChatState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            EnsureDirectory();
            state.SchemaVersion = CopilotChatState.CurrentSchemaVersion;
            var json = JsonConvert.SerializeObject(state, SerializerSettings);
            var tempFilePath = StateFilePath + ".tmp";

            try
            {
                File.WriteAllText(tempFilePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (File.Exists(StateFilePath))
                    File.Copy(StateFilePath, BackupStateFilePath, overwrite: true);

                File.Move(tempFilePath, StateFilePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
        }

        public int CleanupOrphanedAttachments(CopilotChatState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            EnsureDirectory();

            var attachmentRoot = Path.GetFullPath(AttachmentDirectoryPath);
            var referencedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attachment in (state.Conversations ?? new System.Collections.ObjectModel.ObservableCollection<CopilotConversationRecord>())
                .Where(conversation => conversation != null)
                .SelectMany(conversation => conversation.Attachments))
            {
                if (string.IsNullOrWhiteSpace(attachment.Value))
                    continue;

                try
                {
                    var fullPath = Path.GetFullPath(attachment.Value);
                    if (IsUnderRoot(fullPath, attachmentRoot))
                        referencedPaths.Add(fullPath);
                }
                catch
                {
                }
            }

            var deletedCount = 0;
            foreach (var filePath in Directory.EnumerateFiles(attachmentRoot, "*", SearchOption.AllDirectories))
            {
                if (referencedPaths.Contains(Path.GetFullPath(filePath)))
                    continue;

                try
                {
                    File.Delete(filePath);
                    deletedCount++;
                }
                catch
                {
                }
            }

            return deletedCount;
        }

        private void EnsureDirectory()
        {
            if (!Directory.Exists(StateDirectoryPath))
                Directory.CreateDirectory(StateDirectoryPath);

            if (!Directory.Exists(AttachmentDirectoryPath))
                Directory.CreateDirectory(AttachmentDirectoryPath);
        }

        private static bool TryLoad(string filePath, out CopilotChatState state)
        {
            state = new CopilotChatState();
            if (!File.Exists(filePath))
                return false;

            try
            {
                var json = File.ReadAllText(filePath);
                state = JsonConvert.DeserializeObject<CopilotChatState>(json, SerializerSettings) ?? new CopilotChatState();
                state.SchemaVersion = CopilotChatState.CurrentSchemaVersion;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsUnderRoot(string path, string root)
        {
            var relativePath = Path.GetRelativePath(root, path);
            return !relativePath.Equals("..", StringComparison.Ordinal)
                && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !Path.IsPathRooted(relativePath);
        }
    }
}
