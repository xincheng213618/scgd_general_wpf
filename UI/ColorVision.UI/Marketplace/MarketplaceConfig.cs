using System.IO;

namespace ColorVision.UI.Marketplace
{
    public static class MarketplaceConfig
    {
        public const string DefaultServiceBaseUrl = "http://xc213618.ddns.me:9998";

        private static readonly char[] ServiceBaseUrlSeparators = [';', ',', '\r', '\n'];
        private static string[] _serviceBaseUrls = [DefaultServiceBaseUrl];
        private static string? _activeServiceBaseUrl;

        /// <summary>Configured update sources in the order they are tried: HTTP(S) services or offline update folders; never empty.</summary>
        public static IReadOnlyList<string> ServiceBaseUrls => Volatile.Read(ref _serviceBaseUrls);

        /// <summary>
        /// The update source for update checks, package downloads and the plugin marketplace: the source last confirmed
        /// reachable, otherwise the first configured one. It may be an offline update folder (<c>file:</c> URI).
        /// </summary>
        public static string UpdateSourceUrl => Volatile.Read(ref _activeServiceBaseUrl) ?? Volatile.Read(ref _serviceBaseUrls)[0];

        public static string ServiceBaseUrl => DefaultServiceBaseUrl;

        public static bool UsesOnlyDefaultServiceBaseUrl
        {
            get
            {
                string[] urls = Volatile.Read(ref _serviceBaseUrls);
                return urls.Length == 1 && string.Equals(urls[0], DefaultServiceBaseUrl, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool HasFolderSource => Volatile.Read(ref _serviceBaseUrls).Any(IsFolderSource);

        public static bool IsFolderSource(string url) => url.StartsWith("file:", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Parses a user-entered list (separated by semicolons, commas or new lines) into normalized sources:
        /// HTTP(S) base addresses, or local / UNC folders converted to <c>file:</c> URIs. Invalid entries are dropped;
        /// an empty result means the official service.
        /// </summary>
        public static string[] ParseServiceBaseUrls(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return [DefaultServiceBaseUrl];

            List<string> result = new();
            foreach (string part in text.Split(ServiceBaseUrlSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string? normalized = IsFolderPath(part) ? NormalizeFolder(part) : NormalizeHttp(part);
                if (normalized != null && !result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    result.Add(normalized);
            }

            return result.Count == 0 ? [DefaultServiceBaseUrl] : result.ToArray();
        }

        private static bool IsFolderPath(string text)
        {
            return text.StartsWith(@"\\", StringComparison.Ordinal)
                || text.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                || (text.Length >= 3 && char.IsAsciiLetter(text[0]) && text[1] == ':' && (text[2] == '\\' || text[2] == '/'));
        }

        private static string? NormalizeFolder(string text)
        {
            try
            {
                Uri uri = text.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? new Uri(text) : new Uri(Path.GetFullPath(text));
                return uri.IsFile && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) ? uri.AbsoluteUri.TrimEnd('/') : null;
            }
            catch (Exception ex) when (ex is UriFormatException or ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }

        private static string? NormalizeHttp(string text)
        {
            string candidate = text.Contains("://", StringComparison.Ordinal) ? text : "http://" + text;
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                return null;
            return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        }

        public static void SetServiceBaseUrls(IEnumerable<string> urls)
        {
            string[] normalized = ParseServiceBaseUrls(string.Join(';', urls));
            Volatile.Write(ref _serviceBaseUrls, normalized);
            string? active = Volatile.Read(ref _activeServiceBaseUrl);
            if (active != null && !normalized.Contains(active, StringComparer.OrdinalIgnoreCase))
                Volatile.Write(ref _activeServiceBaseUrl, null);
        }

        public static void ConfigureUpdateSources(string? text)
        {
            string[] urls = ParseServiceBaseUrls(text);
            if (urls.Length == 1 && !IsFolderSource(urls[0])
                && !string.Equals(urls[0], DefaultServiceBaseUrl, StringComparison.OrdinalIgnoreCase))
                urls = [DefaultServiceBaseUrl, urls[0]];
            SetServiceBaseUrls(urls);
        }

        public static void SetActiveServiceBaseUrl(string url)
        {
            if (ServiceBaseUrls.Contains(url, StringComparer.OrdinalIgnoreCase))
                Volatile.Write(ref _activeServiceBaseUrl, url);
        }

        public static string BuildApiUrl(string relativePath)
        {
            return $"{ServiceBaseUrl}/{relativePath.TrimStart('/')}";
        }

        public static string BuildUpdateApiUrl(string relativePath, string? source = null)
        {
            return $"{source ?? UpdateSourceUrl}/{relativePath.TrimStart('/')}";
        }

        public static string BuildLegacyPluginUrl(string relativePath, string? source = null)
        {
            return BuildUpdateApiUrl($"D%3A/ColorVision/Plugins/{relativePath.TrimStart('/')}", source);
        }
    }
}
