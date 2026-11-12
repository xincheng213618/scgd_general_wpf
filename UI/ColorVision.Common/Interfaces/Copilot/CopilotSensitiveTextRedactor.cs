using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ColorVision.UI;

/// <summary>Redacts recognized credential text without opening resources or modifying captured source objects.</summary>
public static class CopilotSensitiveTextRedactor
{
    private const RegexOptions Invariant = RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.NonBacktracking;
    private static readonly Regex AuthorizationHeaderLine = new(
        "(?<name>^[ \\t]*(?:proxy-)?authorization[ \\t]*:[ \\t]*)[^\\r\\n]*", Invariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex AuthorizationInlineHeader = new(
        @"(?<name>\bauthorization\s*[:=]\s*)[^\r\n;]*", Invariant | RegexOptions.IgnoreCase);
    private static readonly Regex AuthorizationHeader = new(
        "(?<name>[\"']?\\bauthorization[\"']?\\s*[:=]\\s*)(?<quote>[\"']?)[^\\r\\n,;\"'}]+", Invariant | RegexOptions.IgnoreCase);
    private static readonly Regex AuthorizationBoundary = new(@"\bauthorization\z", Invariant | RegexOptions.IgnoreCase);
    private static readonly Regex Bearer = new("(?i:\\bBearer)[ \\t]+[A-Za-z0-9._~+/-]{16,}=*", Invariant);
    private static readonly Regex OpenAiKey = new("sk-[A-Za-z0-9]{20,}", Invariant);
    private static readonly Regex AwsKey = new("\\bAKIA[0-9A-Z]{16}\\b", Invariant);

    /// <summary>Standard literal assignment forms shared with streaming redaction; this is not a grammar for arbitrary metadata names.</summary>
    public static IReadOnlyList<string> SupportedAssignmentMarkers { get; } = Array.AsReadOnly(new[]
    {
        "proxy-authorization", "authorization", "bearer", "password", "passwd", "pwd", "secret", "token",
        "privatekey", "private_key", "private-key", "private key", "private.key",
        "accesskey", "access_key", "access-key", "access key", "access.key",
        "apikey", "api_key", "api-key", "api key", "api.key",
        "appsecret", "app_secret", "app-secret", "app secret", "app.secret",
        "servicetoken", "service_token", "service-token", "service token", "service.token",
        "license", "lincense", "serialnumber", "serial_number", "serial-number", "serial number", "serial.number",
        "serialno", "serial_no", "serial-no", "serial no", "serial.no", "sn",
        "密码", "口令", "密钥", "令牌", "许可证", "授权码", "序列号", "设备序号",
    }.OrderByDescending(marker => marker.Length).ThenBy(marker => marker, StringComparer.Ordinal).ToArray());

    private static readonly IReadOnlyDictionary<char, string[]> MarkersByInitial = SupportedAssignmentMarkers
        .GroupBy(marker => char.ToUpperInvariant(marker[0])).ToDictionary(group => group.Key, group => group.ToArray());

    public static string Redact(string? text) => RedactArchiveSource(RedactAssignments(text ?? string.Empty, closedAuthorizationOnly: false));

    /// <summary>Preserves the existing header/key prepass; generic assignments belong to the caller's streaming parser.</summary>
    public static string RedactArchiveSource(string? text)
    {
        var value = AuthorizationHeaderLine.Replace(text ?? string.Empty, "${name}<redacted>");
        value = RedactAssignments(value, closedAuthorizationOnly: true);
        value = AuthorizationInlineHeader.Replace(value, "${name}<redacted>");
        value = AuthorizationHeader.Replace(value, "${name}${quote}<redacted>");
        value = Bearer.Replace(value, "Bearer <redacted>");
        value = OpenAiKey.Replace(value, "<redacted>");
        return AwsKey.Replace(value, "<redacted>");
    }

    private static string RedactAssignments(string text, bool closedAuthorizationOnly)
    {
        StringBuilder? result = null;
        var copied = 0;
        var index = 0;
        while (index < text.Length)
        {
            var marker = MarkerAt(text, index, closedAuthorizationOnly);
            if (marker == null) { index++; continue; }
            var separator = index + marker.Length;
            if (separator < text.Length && text[separator] is '"' or '\'') separator++;
            while (separator < text.Length && char.IsWhiteSpace(text[separator])) separator++;
            if (separator == text.Length || !(text[separator] is ':' or '=' || !closedAuthorizationOnly && text[separator] == '：'))
            {
                index++;
                continue;
            }
            var start = separator + 1;
            while (start < text.Length && char.IsWhiteSpace(text[start])) start++;
            if (start == text.Length) break;
            var quote = text[start] is '"' or '\'' ? text[start++] : '\0';
            if (closedAuthorizationOnly && quote == '\0') { index = separator + 1; continue; }
            var end = start;
            if (quote != '\0')
            {
                while (end < text.Length)
                {
                    if (text[end] == '\\' && end + 1 < text.Length) { end += 2; continue; }
                    if (text[end] == quote) break;
                    end++;
                }
                // An unclosed value is left to the streaming parser in the archive prepass.
                // Do not rescan its tail as further quoted headers; the complete-text pass hides it through EOF.
                if (closedAuthorizationOnly && end == text.Length) break;
            }
            else
                while (end < text.Length && !IsUnquotedDelimiter(text[end])) end++;
            if (end == start && !closedAuthorizationOnly) { index = Math.Min(text.Length, end + 1); continue; }
            // Whole-text generic assignments historically normalize nonempty values without quotes.
            // Authorization keeps its header/JSON formatting; streaming archives own their generic quotes.
            var preserveQuote = closedAuthorizationOnly
                || string.Equals(marker, "authorization", StringComparison.OrdinalIgnoreCase)
                || string.Equals(marker, "proxy-authorization", StringComparison.OrdinalIgnoreCase);
            var redactionStart = quote != '\0' && !preserveQuote ? start - 1 : start;
            result ??= new StringBuilder(text.Length);
            result.Append(text, copied, redactionStart - copied).Append("<redacted>");
            if (!closedAuthorizationOnly && quote != '\0' && end < text.Length)
            {
                if (preserveQuote) result.Append(quote);
                end++;
                // An unescaped quote can end the quoted part before the credential ends.
                // Hide its adjacent tail, but preserve field and closing structure boundaries.
                while (end < text.Length && !IsQuotedRemainderDelimiter(text[end])) end++;
                copied = index = end;
                continue;
            }
            copied = end;
            index = quote == '\0' ? end : Math.Min(text.Length, end + 1);
        }
        return result == null ? text : result.Append(text, copied, text.Length - copied).ToString();
    }

    private static string? MarkerAt(string text, int index, bool authorizationOnly)
    {
        if (authorizationOnly)
        {
            const string marker = "authorization";
            if (!text.AsSpan(index).StartsWith(marker, StringComparison.OrdinalIgnoreCase)) return null;
            var boundary = index == 0 ? marker : text[index - 1] + marker;
            return AuthorizationBoundary.IsMatch(boundary) ? marker : null;
        }
        if (!MarkersByInitial.TryGetValue(char.ToUpperInvariant(text[index]), out var markers)) return null;
        foreach (var marker in markers)
            if (text.AsSpan(index).StartsWith(marker, StringComparison.OrdinalIgnoreCase)
                && CopilotBusinessContextBuilder.IsSensitivePropertyName(marker)) return marker;
        return null;
    }

    private static bool IsUnquotedDelimiter(char character)
        => char.IsWhiteSpace(character) || character is ',' or ';' or '"' or '\'' or '}' or ']';

    private static bool IsQuotedRemainderDelimiter(char character)
        => char.IsWhiteSpace(character) || character is ',' or ';' or '}' or ']' or ')';
}
