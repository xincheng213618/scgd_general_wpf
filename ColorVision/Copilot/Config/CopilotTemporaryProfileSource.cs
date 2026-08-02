using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotTemporaryProfileSource
    {
        public static bool Sync(ObservableCollection<CopilotProfileConfig> profiles)
        {
            ArgumentNullException.ThrowIfNull(profiles);

            const string expiredProfileId = "builtin-minimax-trial-20260527";
            var existing = profiles.FirstOrDefault(profile => string.Equals(profile.Id, expiredProfileId, StringComparison.Ordinal));
            if (existing == null)
                return false;

            profiles.Remove(existing);
            return true;
        }
    }
}
