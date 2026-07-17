using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ColorVision.Copilot
{
    internal static class CopilotPluginSubagentRolePreference
    {
        internal const int MaximumStoredDisabledRoles = 256;

        private static readonly Regex RoleKeyRegex = new(
            "^[a-z][a-z0-9._-]{1,63}/[a-z][a-z0-9-]{1,47}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string CreateKey(string? sourceId, string? roleId)
        {
            return $"{sourceId?.Trim().ToLowerInvariant()}/{roleId?.Trim().ToLowerInvariant()}";
        }

        public static string[] NormalizeKeys(IEnumerable<string>? keys)
        {
            return (keys ?? Array.Empty<string>())
                .Select(key => key?.Trim().ToLowerInvariant() ?? string.Empty)
                .Where(key => RoleKeyRegex.IsMatch(key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .Take(MaximumStoredDisabledRoles)
                .ToArray();
        }
    }
}
