using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColorVision.UI;

namespace ColorVision.Copilot
{
    internal static partial class CopilotToolIntentPolicy
    {
        private static bool ContainsAny(string? text, string[] markers)
        {
            var value = text ?? string.Empty;
            return markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsAnyEnglishWordForm(
            string? text,
            string[] markers,
            bool includeVerbForms = false)
        {
            var value = text ?? string.Empty;
            return markers.Any(marker => ContainsEnglishWordForm(value, marker, includeVerbForms));
        }

        private static bool ContainsEnglishWordForm(
            string value,
            string marker,
            bool includeVerbForms)
        {
            if (string.IsNullOrWhiteSpace(marker))
                return false;

            if (!IsAsciiLetterOrDigit(marker[0]) || !IsAsciiLetterOrDigit(marker[^1]))
                return value.Contains(marker, StringComparison.OrdinalIgnoreCase);

            if (ContainsBoundedAsciiText(value, marker)
                || ContainsBoundedAsciiText(value, CreatePluralForm(marker)))
            {
                return true;
            }

            return includeVerbForms
                && (ContainsBoundedAsciiText(value, CreatePastTenseForm(marker))
                    || ContainsBoundedAsciiText(value, CreateGerundForm(marker))
                    || ContainsIrregularEnglishVerbForm(value, marker));
        }

        private static bool ContainsBoundedAsciiText(string value, string candidate)
        {
            var searchStart = 0;
            while (searchStart <= value.Length - candidate.Length)
            {
                var index = value.IndexOf(
                    candidate,
                    searchStart,
                    StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    return false;

                var end = index + candidate.Length;
                if (IsAsciiTokenStart(value, index)
                    && IsAsciiTokenEnd(value, end))
                {
                    return true;
                }

                searchStart = index + 1;
            }

            return false;
        }

        private static bool ContainsIrregularEnglishVerbForm(string value, string marker)
        {
            return marker.Equals("find", StringComparison.OrdinalIgnoreCase)
                && ContainsBoundedAsciiText(value, "found");
        }

        private static bool IsAsciiTokenStart(string value, int index)
        {
            if (index == 0 || !IsAsciiLetterOrDigit(value[index - 1]))
                return true;

            return IsAsciiLetter(value[index - 1])
                && value[index] is >= 'A' and <= 'Z';
        }

        private static bool IsAsciiTokenEnd(string value, int end)
        {
            if (end == value.Length || !IsAsciiLetterOrDigit(value[end]))
                return true;

            return value[end - 1] is >= 'a' and <= 'z'
                && value[end] is >= 'A' and <= 'Z';
        }

        private static string CreatePluralForm(string marker)
        {
            if (EndsWithConsonantY(marker))
                return marker[..^1] + "ies";
            if (marker.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                || marker.EndsWith("x", StringComparison.OrdinalIgnoreCase)
                || marker.EndsWith("z", StringComparison.OrdinalIgnoreCase)
                || marker.EndsWith("ch", StringComparison.OrdinalIgnoreCase)
                || marker.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
            {
                return marker + "es";
            }

            return marker + "s";
        }

        private static string CreatePastTenseForm(string marker)
        {
            if (EndsWithConsonantY(marker))
                return marker[..^1] + "ied";
            return marker.EndsWith("e", StringComparison.OrdinalIgnoreCase)
                ? marker + "d"
                : marker + "ed";
        }

        private static string CreateGerundForm(string marker)
        {
            if (marker.EndsWith("ie", StringComparison.OrdinalIgnoreCase))
                return marker[..^2] + "ying";
            if (marker.EndsWith("e", StringComparison.OrdinalIgnoreCase)
                && !marker.EndsWith("ee", StringComparison.OrdinalIgnoreCase))
            {
                return marker[..^1] + "ing";
            }

            return marker + "ing";
        }

        private static bool EndsWithConsonantY(string value)
        {
            return value.Length > 1
                && value.EndsWith("y", StringComparison.OrdinalIgnoreCase)
                && IsAsciiLetter(value[^2])
                && !"aeiou".Contains(char.ToLowerInvariant(value[^2]));
        }

        private static bool IsAsciiLetterOrDigit(char value)
        {
            return IsAsciiLetter(value) || value is >= '0' and <= '9';
        }

        private static bool IsAsciiLetter(char value)
        {
            return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
        }

        private static string RemoveExplicitFilePaths(CopilotAgentRequest request)
        {
            var result = request.UserText ?? string.Empty;
            foreach (var path in request.ReadableLocalFilePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                result = result.Replace(path, string.Empty, StringComparison.OrdinalIgnoreCase);
                var alternatePath = path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!string.Equals(alternatePath, path, StringComparison.Ordinal))
                    result = result.Replace(alternatePath, string.Empty, StringComparison.OrdinalIgnoreCase);
            }
            return result;
        }

    }
}
