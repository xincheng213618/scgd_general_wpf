#pragma warning disable CA1513,CA1716,CA1863

namespace ColorVision.UI
{
    public static class TimedButtonOperationWarmupSession
    {
        private static readonly object SyncRoot = new object();
        private static readonly HashSet<string> WarmedOperationKeys = new HashSet<string>(StringComparer.Ordinal);

        public static bool NeedsWarmup(string operationKey, bool enabled)
        {
            if (!enabled || string.IsNullOrWhiteSpace(operationKey))
            {
                return false;
            }

            lock (SyncRoot)
            {
                return !WarmedOperationKeys.Contains(operationKey.Trim());
            }
        }

        public static bool ConsumeWarmup(string operationKey, bool enabled)
        {
            if (!enabled || string.IsNullOrWhiteSpace(operationKey))
            {
                return false;
            }

            string normalizedKey = operationKey.Trim();
            lock (SyncRoot)
            {
                if (WarmedOperationKeys.Contains(normalizedKey))
                {
                    return false;
                }

                WarmedOperationKeys.Add(normalizedKey);
                return true;
            }
        }

        public static void Reset(string operationKey)
        {
            if (string.IsNullOrWhiteSpace(operationKey))
            {
                return;
            }

            lock (SyncRoot)
            {
                WarmedOperationKeys.Remove(operationKey.Trim());
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                WarmedOperationKeys.Clear();
            }
        }
    }
}
