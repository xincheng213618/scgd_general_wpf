using System.Globalization;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Diagnostics;

internal static class FlowRunIdentitySql
{
    private static readonly string WhitespaceCodes = string.Join(",", Enumerable.Range(char.MinValue, char.MaxValue + 1)
        .Where(value => char.IsWhiteSpace((char)value)).Select(value => value.ToString(CultureInfo.InvariantCulture)));

    // Keep the expression identical in the index and its queries while matching .NET Trim semantics.
    internal static string TrimmedKey(string column) => $"trim({column},char({WhitespaceCodes}))";
}
