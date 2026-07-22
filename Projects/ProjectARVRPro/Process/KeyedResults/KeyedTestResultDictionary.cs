namespace ProjectARVRPro.Process.KeyedResults
{
    internal static class KeyedTestResultDictionary
    {
        public static string NormalizeKey(string? key, string defaultKey)
        {
            return string.IsNullOrWhiteSpace(key) ? defaultKey : key.Trim();
        }

        public static bool IsKey(string? key, string expectedKey)
        {
            return string.Equals(key?.Trim(), expectedKey, StringComparison.OrdinalIgnoreCase);
        }

        public static void Set<T>(IDictionary<string, T> results, string key, T result)
        {
            string? existingKey = results.Keys.FirstOrDefault(item =>
                string.Equals(item, key, StringComparison.OrdinalIgnoreCase));
            results[existingKey ?? key] = result;
        }

        public static bool TryGetValue<T>(IReadOnlyDictionary<string, T>? results, string key, out T? result)
        {
            result = default;
            if (results == null || string.IsNullOrWhiteSpace(key))
                return false;

            string normalizedKey = key.Trim();
            if (results.TryGetValue(normalizedKey, out T? exactResult))
            {
                result = exactResult;
                return true;
            }

            foreach (KeyValuePair<string, T> item in results)
            {
                if (!string.Equals(item.Key, normalizedKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                result = item.Value;
                return true;
            }

            return false;
        }
    }
}
