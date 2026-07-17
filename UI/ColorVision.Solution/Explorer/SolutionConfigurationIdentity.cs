namespace ColorVision.Solution.Explorer
{
    internal static class SolutionConfigurationIdentity
    {
        public const string DefaultConfiguration = "Debug";
        public const string DefaultPlatform = "Any CPU";

        public static string NormalizeConfiguration(string? configuration)
        {
            return string.IsNullOrWhiteSpace(configuration)
                ? DefaultConfiguration
                : configuration.Trim();
        }

        public static string NormalizePlatform(string? platform)
        {
            return string.IsNullOrWhiteSpace(platform)
                ? DefaultPlatform
                : platform.Trim();
        }

        public static string CreateKey(string? configuration, string? platform)
        {
            return $"{NormalizeConfiguration(configuration)}|{NormalizePlatform(platform)}";
        }

        public static bool TryParseKey(
            string? value,
            out string configuration,
            out string platform)
        {
            string normalizedValue = value?.Trim() ?? string.Empty;
            int separatorIndex = normalizedValue.IndexOf('|');
            if (separatorIndex <= 0 || separatorIndex == normalizedValue.Length - 1)
            {
                configuration = NormalizeConfiguration(normalizedValue);
                platform = DefaultPlatform;
                return false;
            }

            configuration = NormalizeConfiguration(normalizedValue[..separatorIndex]);
            platform = NormalizePlatform(normalizedValue[(separatorIndex + 1)..]);
            return true;
        }
    }
}
