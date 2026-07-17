using System.IO;

namespace ColorVision.Solution.Editor
{
    /// <summary>
    /// Compares physical resource paths by their absolute Windows identity while
    /// preserving the caller's original path for display and error reporting.
    /// </summary>
    internal sealed class ResourcePathIdentityComparer : IEqualityComparer<string>
    {
        public static ResourcePathIdentityComparer Instance { get; } = new();

        private ResourcePathIdentityComparer() { }

        public bool Equals(string? left, string? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;
            return string.Equals(
                GetIdentity(left),
                GetIdentity(right),
                StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            return StringComparer.OrdinalIgnoreCase.GetHashCode(GetIdentity(path));
        }

        internal static string GetIdentity(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            }
            catch (Exception ex) when (ex is ArgumentException
                or NotSupportedException
                or PathTooLongException)
            {
                return Path.TrimEndingDirectorySeparator(path);
            }
        }
    }
}
