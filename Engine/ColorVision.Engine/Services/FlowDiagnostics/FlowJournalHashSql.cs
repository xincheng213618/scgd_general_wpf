namespace ColorVision.Engine.Services.FlowDiagnostics;

/// <summary>Projects bounded hash text without allowing SQLite NUL handling to hide a suffix.</summary>
internal static class FlowJournalHashSql
{
    internal static string Value(string column) => $"CASE WHEN {ExactText(column)} THEN {column} ELSE NULL END";

    internal static string Invalid(string column) => $"CASE WHEN {column} IS NOT NULL AND {column}<>'' AND NOT ({ExactText(column)}) THEN 1 ELSE 0 END";

    // Compare byte lengths with the prefix, not a fixed byte count: SQLite databases may use UTF-16.
    // length(text) and substr(text) stop at NUL; the complete BLOB length also detects a hidden suffix.
    private static string ExactText(string column) => $"typeof({column})='text' AND length({column})=64 AND length(CAST({column} AS BLOB))=length(CAST(substr({column},1,64) AS BLOB))";
}
