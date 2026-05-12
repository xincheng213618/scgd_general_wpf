using System;
using System.Collections.ObjectModel;

namespace ColorVision.Copilot
{
    internal sealed class CopilotTemporaryProfileSyncContext
    {
        public CopilotTemporaryProfileSyncContext(ObservableCollection<CopilotProfileConfig> profiles, DateTimeOffset nowUtc)
        {
            Profiles = profiles;
            NowUtc = nowUtc;
        }

        public ObservableCollection<CopilotProfileConfig> Profiles { get; }

        public DateTimeOffset NowUtc { get; }

        public bool Changed { get; set; }
    }

    internal static partial class CopilotTemporaryProfileSource
    {
        public static bool Sync(ObservableCollection<CopilotProfileConfig> profiles, DateTimeOffset nowUtc)
        {
            ArgumentNullException.ThrowIfNull(profiles);

            var context = new CopilotTemporaryProfileSyncContext(profiles, nowUtc);
            SyncCore(context);
            return context.Changed;
        }

        static partial void SyncCore(CopilotTemporaryProfileSyncContext context);
    }
}