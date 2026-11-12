using ColorVision.Copilot.Mcp;
using System.Collections.Generic;
using System.Text;

namespace ColorVision.Copilot
{
    public static partial class CopilotDatabaseSqlPolicy
    {
        internal static string RedactForApproval(string sql)
        {
            if (!TryAnalyze(sql, out var analysis, out _))
                return "<SQL hidden: could not be safely analyzed>";
            if (!analysis!.ContainsSensitiveIdentifier)
                return CopilotMcpAuditLogger.RedactText(sql);

            // One sensitive column can receive values from a positional INSERT,
            // expression, or subquery. Hide every literal and comment rather than
            // guessing which value belongs to that column.
            var redactions = new List<SqlApprovalRedactionSpan>();
            if (!TryTokenize(sql, out _, out _, redactions))
                return "<SQL hidden: could not be safely analyzed>";
            var display = new StringBuilder();
            var position = 0;
            foreach (var redaction in redactions)
            {
                display.Append(sql, position, redaction.Start - position);
                display.Append(redaction.Replacement);
                position = redaction.Start + redaction.Length;
            }
            display.Append(sql, position, sql.Length - position);
            return CopilotMcpAuditLogger.RedactText(display.ToString());
        }

        private sealed record SqlApprovalRedactionSpan(int Start, int Length, string Replacement);
    }
}
