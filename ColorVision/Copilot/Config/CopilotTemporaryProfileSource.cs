using System;
using System.Collections.ObjectModel;
using System.Linq;

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
            RemoveExpiredBuiltInTemporaryProfiles(context);
            SyncCore(context);
            return context.Changed;
        }

        private static void RemoveExpiredBuiltInTemporaryProfiles(CopilotTemporaryProfileSyncContext context)
        {
            const string expiredProfileId = "builtin-minimax-trial-20260527";
            var existing = context.Profiles.FirstOrDefault(profile => string.Equals(profile.Id, expiredProfileId, StringComparison.Ordinal));

            if (existing != null)
            {
                context.Profiles.Remove(existing);
                context.Changed = true;
            }
        }

        static partial void SyncCore(CopilotTemporaryProfileSyncContext context);
    }
}
