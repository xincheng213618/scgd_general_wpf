using ColorVision.Copilot.Mcp;
using ColorVision.Database;
using log4net;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public enum CopilotDatabaseSqlStatementKind
    {
        Query,
        DataChange,
        Definition,
    }

    public sealed record CopilotDatabaseSqlAnalysis(
        string RootKeyword,
        CopilotDatabaseSqlStatementKind Kind,
        bool IsDestructive,
        bool IsUnboundedChange,
        bool IsTransactional,
        bool ContainsSensitiveIdentifier,
        string Fingerprint);

    public sealed record CopilotDatabaseQueryResult(
        IReadOnlyList<string> Columns,
        IReadOnlyList<IReadOnlyList<string>> Rows,
        bool IsTruncated);

    public sealed record CopilotDatabaseMutationResult(int AffectedRows, bool Transactional);

    public interface ICopilotDatabaseSqlExecutor
    {
        bool IsAvailable { get; }

        Task<CopilotDatabaseQueryResult> QueryAsync(
            string sql,
            int maxRows,
            int timeoutSeconds,
            CancellationToken cancellationToken);

        Task<CopilotDatabaseMutationResult> ExecuteAsync(
            string sql,
            CopilotDatabaseSqlAnalysis analysis,
            int timeoutSeconds,
            CancellationToken cancellationToken);
    }

    public static class CopilotDatabaseSqlPolicy
    {
        public const int MaximumSqlLength = 20_000;
        private static readonly HashSet<string> ReadKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "SHOW", "DESCRIBE", "DESC", "EXPLAIN", "TABLE",
        };
        private static readonly HashSet<string> DataChangeKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "INSERT", "UPDATE", "DELETE", "REPLACE",
        };
        private static readonly HashSet<string> DefinitionKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "CREATE", "ALTER", "DROP", "TRUNCATE", "RENAME",
        };
        private static readonly HashSet<string> ProhibitedRootKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "USE", "SET", "GRANT", "REVOKE", "SHUTDOWN", "KILL", "INSTALL", "UNINSTALL",
            "BEGIN", "START", "COMMIT", "ROLLBACK", "SAVEPOINT", "RELEASE", "LOCK", "UNLOCK",
            "PREPARE", "EXECUTE", "DEALLOCATE", "DELIMITER", "CALL", "DO", "HANDLER", "LOAD",
        };
        private static readonly HashSet<string> WithTargets = new(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "TABLE", "INSERT", "UPDATE", "DELETE", "REPLACE",
        };
        private static readonly HashSet<string> ProhibitedDefinitionObjects = new(StringComparer.OrdinalIgnoreCase)
        {
            "DATABASE", "SCHEMA", "USER", "ROLE", "SERVER", "TABLESPACE", "LOGFILE", "FUNCTION",
            "PROCEDURE", "EVENT", "RESOURCE", "SYSTEM", "INSTANCE",
        };
        private static readonly HashSet<string> AllowedDefinitionObjects = new(StringComparer.OrdinalIgnoreCase)
        {
            "TABLE", "VIEW", "INDEX", "TRIGGER",
        };
        private static readonly HashSet<string> SensitiveIdentifiers = new(StringComparer.OrdinalIgnoreCase)
        {
            "PASSWORD", "PASSWD", "PWD", "SECRET", "TOKEN", "API_KEY", "APIKEY", "AUTHORIZATION",
            "BEARER", "PRIVATE_KEY", "ACCESS_KEY",
        };

        public static bool TryAnalyze(string? sql, out CopilotDatabaseSqlAnalysis? analysis, out string error)
        {
            analysis = null;
            error = string.Empty;
            var text = sql?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                error = "SQL must not be empty.";
                return false;
            }
            if (text.Length > MaximumSqlLength)
            {
                error = $"SQL exceeds the {MaximumSqlLength}-character limit.";
                return false;
            }
            if (!TryTokenize(text, out var tokens, out error))
                return false;
            if (tokens.Count == 0)
            {
                error = "SQL does not contain an executable statement.";
                return false;
            }

            var semicolons = tokens.Where(token => token.Text == ";").ToArray();
            if (semicolons.Length > 1 || semicolons.Length == 1 && semicolons[0].Index != tokens.Count - 1)
            {
                error = "Only one SQL statement is allowed.";
                return false;
            }
            if (semicolons.Length == 1)
                tokens.RemoveAt(tokens.Count - 1);
            if (tokens.Count == 0)
            {
                error = "SQL does not contain an executable statement.";
                return false;
            }

            var root = tokens[0].Text.ToUpperInvariant();
            if (root == "WITH")
            {
                var target = tokens.FirstOrDefault(token => token.Depth == 0 && WithTargets.Contains(token.Text));
                if (target == null)
                {
                    error = "WITH must end in SELECT, TABLE, INSERT, UPDATE, DELETE, or REPLACE.";
                    return false;
                }
                root = target.Text.ToUpperInvariant();
            }

            if (ProhibitedRootKeywords.Contains(root))
            {
                error = $"SQL command '{root}' is not available to Copilot.";
                return false;
            }
            if (!ReadKeywords.Contains(root) && !DataChangeKeywords.Contains(root) && !DefinitionKeywords.Contains(root))
            {
                error = $"SQL command '{root}' is not supported.";
                return false;
            }

            var normalizedTokens = tokens.Select(token => token.Text.ToUpperInvariant()).ToArray();
            if (ContainsSequence(normalizedTokens, "INTO", "OUTFILE")
                || ContainsSequence(normalizedTokens, "INTO", "DUMPFILE")
                || normalizedTokens.Contains("LOAD_FILE", StringComparer.OrdinalIgnoreCase)
                || normalizedTokens.Contains("SLEEP", StringComparer.OrdinalIgnoreCase)
                || normalizedTokens.Contains("BENCHMARK", StringComparer.OrdinalIgnoreCase))
            {
                error = "File access and server-delay SQL functions are not available to Copilot.";
                return false;
            }
            if (root is "CREATE" or "ALTER" or "DROP" or "RENAME")
            {
                var objectToken = tokens.Skip(1).FirstOrDefault(token => token.Depth == 0
                    && (AllowedDefinitionObjects.Contains(token.Text) || ProhibitedDefinitionObjects.Contains(token.Text)));
                if (objectToken == null)
                {
                    error = $"SQL command '{root}' must target a table, view, index, or trigger.";
                    return false;
                }
                if (ProhibitedDefinitionObjects.Contains(objectToken.Text))
                {
                    error = $"Database administration for '{objectToken.Text.ToUpperInvariant()}' is not available to Copilot.";
                    return false;
                }
            }
            if (normalizedTokens.Contains("IMPORT", StringComparer.OrdinalIgnoreCase)
                || normalizedTokens.Contains("EXPORT", StringComparer.OrdinalIgnoreCase)
                || ContainsSequence(normalizedTokens, "DATA", "DIRECTORY")
                || ContainsSequence(normalizedTokens, "INDEX", "DIRECTORY"))
            {
                error = "Server-side SQL import, export, and data-directory operations are not available to Copilot.";
                return false;
            }

            var kind = ReadKeywords.Contains(root)
                ? CopilotDatabaseSqlStatementKind.Query
                : DataChangeKeywords.Contains(root)
                    ? CopilotDatabaseSqlStatementKind.DataChange
                    : CopilotDatabaseSqlStatementKind.Definition;
            var hasTopLevelWhere = tokens.Any(token => token.Depth == 0 && token.Text.Equals("WHERE", StringComparison.OrdinalIgnoreCase));
            var unbounded = root is "DELETE" or "UPDATE" && !hasTopLevelWhere;
            var destructive = unbounded || root is "TRUNCATE" or "DROP";
            var containsSensitiveIdentifier = tokens.Any(token => SensitiveIdentifiers.Contains(token.Text));
            analysis = new CopilotDatabaseSqlAnalysis(
                root,
                kind,
                destructive,
                unbounded,
                kind == CopilotDatabaseSqlStatementKind.DataChange,
                containsSensitiveIdentifier,
                CreateFingerprint(text));
            return true;
        }

        public static string CreateFingerprint(string sql)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sql.Trim()));
            return Convert.ToHexString(hash)[..16].ToLowerInvariant();
        }

        public static bool TryFindReferencedTable(string sql, IEnumerable<string> tableNames, out string tableName)
        {
            tableName = string.Empty;
            if (!TryTokenize(sql, out var tokens, out _))
                return false;

            var names = tableNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            tableName = tokens.Select(token => token.Text).FirstOrDefault(names.Contains) ?? string.Empty;
            return tableName.Length > 0;
        }

        private static bool TryTokenize(string sql, out List<SqlToken> tokens, out string error)
        {
            tokens = new List<SqlToken>();
            error = string.Empty;
            var depth = 0;
            for (var index = 0; index < sql.Length;)
            {
                var current = sql[index];
                if (char.IsWhiteSpace(current))
                {
                    index++;
                    continue;
                }
                if (current == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
                {
                    index += 2;
                    while (index < sql.Length && sql[index] is not '\r' and not '\n')
                        index++;
                    continue;
                }
                if (current == '#')
                {
                    while (index < sql.Length && sql[index] is not '\r' and not '\n')
                        index++;
                    continue;
                }
                if (current == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
                {
                    if (index + 2 < sql.Length && sql[index + 2] == '!')
                    {
                        error = "MySQL executable comments are not allowed.";
                        return false;
                    }
                    var end = sql.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        error = "SQL contains an unterminated comment.";
                        return false;
                    }
                    index = end + 2;
                    continue;
                }
                if (current == '`')
                {
                    index++;
                    var identifier = new StringBuilder();
                    var closed = false;
                    while (index < sql.Length)
                    {
                        if (sql[index] == '`')
                        {
                            if (index + 1 < sql.Length && sql[index + 1] == '`')
                            {
                                identifier.Append('`');
                                index += 2;
                                continue;
                            }
                            index++;
                            closed = true;
                            break;
                        }
                        identifier.Append(sql[index++]);
                    }
                    if (!closed)
                    {
                        error = "SQL contains an unterminated quoted identifier.";
                        return false;
                    }
                    if (identifier.Length > 0)
                        tokens.Add(new SqlToken(identifier.ToString(), depth, tokens.Count));
                    continue;
                }
                if (current is '\'' or '"')
                {
                    var quote = current;
                    index++;
                    var closed = false;
                    while (index < sql.Length)
                    {
                        if (sql[index] == '\\' && index + 1 < sql.Length)
                        {
                            index += 2;
                            continue;
                        }
                        if (sql[index] == quote)
                        {
                            if (index + 1 < sql.Length && sql[index + 1] == quote)
                            {
                                index += 2;
                                continue;
                            }
                            index++;
                            closed = true;
                            break;
                        }
                        index++;
                    }
                    if (!closed)
                    {
                        error = "SQL contains an unterminated quoted value.";
                        return false;
                    }
                    continue;
                }
                if (current == '(')
                {
                    depth++;
                    index++;
                    continue;
                }
                if (current == ')')
                {
                    if (depth == 0)
                    {
                        error = "SQL contains an unmatched closing parenthesis.";
                        return false;
                    }
                    depth--;
                    index++;
                    continue;
                }
                if (current == ';')
                {
                    tokens.Add(new SqlToken(";", depth, tokens.Count));
                    index++;
                    continue;
                }
                if (char.IsLetter(current) || current == '_')
                {
                    var start = index++;
                    while (index < sql.Length && (char.IsLetterOrDigit(sql[index]) || sql[index] is '_' or '$'))
                        index++;
                    tokens.Add(new SqlToken(sql[start..index], depth, tokens.Count));
                    continue;
                }
                index++;
            }
            if (depth != 0)
            {
                error = "SQL contains unmatched parentheses.";
                return false;
            }
            return true;
        }

        private static bool ContainsSequence(string[] values, string first, string second)
        {
            for (var index = 0; index + 1 < values.Length; index++)
            {
                if (values[index].Equals(first, StringComparison.OrdinalIgnoreCase)
                    && values[index + 1].Equals(second, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private sealed record SqlToken(string Text, int Depth, int Index);
    }

    public sealed class CopilotMySqlDatabaseSqlExecutor : ICopilotDatabaseSqlExecutor
    {
        private static readonly string[] SensitiveColumnMarkers =
        {
            "password", "passwd", "pwd", "secret", "token", "api_key", "apikey", "authorization",
            "bearer", "private_key", "access_key",
        };

        public bool IsAvailable => MySqlControl.GetInstance().IsConnect;

        public async Task<CopilotDatabaseQueryResult> QueryAsync(
            string sql,
            int maxRows,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            await using var connection = new MySqlConnection(MySqlControl.GetConnectionString());
            await connection.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection) { CommandTimeout = timeoutSeconds };
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
            var fieldCount = Math.Min(reader.FieldCount, 100);
            var columns = Enumerable.Range(0, fieldCount).Select(reader.GetName).ToArray();
            var rows = new List<IReadOnlyList<string>>();
            var truncated = reader.FieldCount > fieldCount;
            while (rows.Count < maxRows && await reader.ReadAsync(cancellationToken))
            {
                var values = new string[fieldCount];
                for (var column = 0; column < fieldCount; column++)
                    values[column] = IsSensitiveColumn(columns[column]) ? "<redacted>" : FormatValue(reader.GetValue(column));
                rows.Add(values);
            }
            if (rows.Count == maxRows && await reader.ReadAsync(cancellationToken))
                truncated = true;
            return new CopilotDatabaseQueryResult(columns, rows, truncated);
        }

        public async Task<CopilotDatabaseMutationResult> ExecuteAsync(
            string sql,
            CopilotDatabaseSqlAnalysis analysis,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            await using var connection = new MySqlConnection(MySqlControl.GetConnectionString());
            await connection.OpenAsync(cancellationToken);
            MySqlTransaction? transaction = null;
            try
            {
                if (analysis.IsTransactional)
                    transaction = await connection.BeginTransactionAsync(cancellationToken);
                await using var command = new MySqlCommand(sql, connection, transaction) { CommandTimeout = timeoutSeconds };
                var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
                if (transaction != null)
                    await transaction.CommitAsync(cancellationToken);
                return new CopilotDatabaseMutationResult(affectedRows, transaction != null);
            }
            catch
            {
                if (transaction != null)
                {
                    try
                    {
                        await transaction.RollbackAsync(CancellationToken.None);
                    }
                    catch
                    {
                        // Preserve the original database failure.
                    }
                }
                throw;
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();
            }
        }

        private static bool IsSensitiveColumn(string name)
        {
            return SensitiveColumnMarkers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        private static string FormatValue(object value)
        {
            var text = value switch
            {
                DBNull => "NULL",
                byte[] bytes => $"<binary {bytes.Length} bytes>",
                DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
                _ => value.ToString() ?? string.Empty,
            };
            text = CopilotMcpAuditLogger.RedactText(text).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            return text.Length <= 512 ? text : text[..512] + "…";
        }
    }

    public sealed class CopilotDatabaseSqlService
    {
        private const int MaximumContentLength = 32_000;
        private static readonly string[] SensitiveColumnMarkers =
        {
            "password", "passwd", "pwd", "secret", "token", "api_key", "apikey", "authorization",
            "bearer", "private_key", "access_key",
        };
        private static readonly ILog Log = LogManager.GetLogger(typeof(CopilotDatabaseSqlService));
        private readonly ICopilotDatabaseSqlExecutor _executor;

        public CopilotDatabaseSqlService()
            : this(new CopilotMySqlDatabaseSqlExecutor())
        {
        }

        public CopilotDatabaseSqlService(ICopilotDatabaseSqlExecutor executor)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public static CopilotToolResult Validate(string toolName, CopilotAgentToolInput input, bool requireQuery, out string sql, out CopilotDatabaseSqlAnalysis? analysis)
        {
            sql = ReadString(input, "sql");
            if (!CopilotDatabaseSqlPolicy.TryAnalyze(sql, out analysis, out var error))
                return Failure(toolName, CopilotToolFailureKind.Validation, "The SQL request was rejected.", error);
            if (requireQuery && analysis!.Kind != CopilotDatabaseSqlStatementKind.Query)
                return Failure(toolName, CopilotToolFailureKind.Validation, "QueryDatabaseSql accepts only read-only SQL.", "Use ExecuteDatabaseSql for data changes and schema changes.");
            if (!requireQuery && analysis!.Kind == CopilotDatabaseSqlStatementKind.Query)
                return Failure(toolName, CopilotToolFailureKind.Validation, "ExecuteDatabaseSql accepts only SQL that changes data or schema.", "Use QueryDatabaseSql for read-only SQL.");
            if (!requireQuery && CopilotDatabaseSqlPolicy.TryFindReferencedTable(sql, MySqlLocalServicesManager.ServiceSettingTableNames, out var protectedTable))
                return Failure(toolName, CopilotToolFailureKind.Authorization, "ColorVision service setting tables are read-only to Copilot.", $"Table '{protectedTable}' is version-managed and can only be reset from the native release SQL.");
            return new CopilotToolResult { ToolName = toolName, Success = true };
        }

        public async Task<CopilotToolResult> QueryAsync(CopilotAgentToolInput input, CancellationToken cancellationToken)
        {
            var validation = Validate("QueryDatabaseSql", input, true, out var sql, out var analysis);
            if (!validation.Success)
                return validation;
            if (!_executor.IsAvailable)
                return DatabaseUnavailable("QueryDatabaseSql");
            var maxRows = ReadInt(input, "maxRows", 100, 1, 500);
            var timeout = ReadInt(input, "timeoutSeconds", 15, 1, 30);
            try
            {
                var result = await _executor.QueryAsync(sql, maxRows, timeout, cancellationToken);
                return new CopilotToolResult
                {
                    ToolName = "QueryDatabaseSql",
                    Success = true,
                    Summary = $"Database query {analysis!.Fingerprint} returned {result.Rows.Count} row(s){(result.IsTruncated ? " (truncated)" : string.Empty)}.",
                    Content = BuildQueryContent(analysis, result),
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error("Copilot database query failed: " + ex.GetType().Name);
                return Failure("QueryDatabaseSql", ClassifyDatabaseFailure(ex), "The database query failed.", "The configured database did not return a usable result. See application logs for diagnostics.");
            }
        }

        public async Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentToolInput input, CancellationToken cancellationToken)
        {
            var validation = Validate("ExecuteDatabaseSql", input, false, out var sql, out var analysis);
            if (!validation.Success)
                return validation;
            if (!_executor.IsAvailable)
                return DatabaseUnavailable("ExecuteDatabaseSql");
            var timeout = ReadInt(input, "timeoutSeconds", 30, 1, 60);
            try
            {
                var result = await _executor.ExecuteAsync(sql, analysis!, timeout, cancellationToken);
                return new CopilotToolResult
                {
                    ToolName = "ExecuteDatabaseSql",
                    Success = true,
                    Summary = $"Approved {analysis!.RootKeyword} SQL {analysis.Fingerprint} completed; affected rows: {result.AffectedRows}.",
                    Content = $"statement: {analysis.RootKeyword}\nfingerprint: {analysis.Fingerprint}\naffected_rows: {result.AffectedRows}\ntransactional: {result.Transactional.ToString().ToLowerInvariant()}",
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error("Copilot database change failed: " + ex.GetType().Name);
                return Failure("ExecuteDatabaseSql", ClassifyDatabaseFailure(ex), "The approved database SQL failed.", "The configured database rejected the operation. See application logs for diagnostics.");
            }
        }

        public static CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput input)
        {
            var validation = Validate("ExecuteDatabaseSql", input, false, out var sql, out var analysis);
            if (!validation.Success || analysis == null)
                return new CopilotToolApprovalPresentation("Reject invalid database SQL", validation.ErrorMessage);
            var excerpt = CopilotMcpAuditLogger.RedactText(sql).Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (excerpt.Length > 1000)
                excerpt = excerpt[..1000] + "…";
            var warning = analysis.IsUnboundedChange
                ? " WARNING: this statement has no top-level WHERE clause and may affect every row."
                : analysis.IsDestructive
                    ? " WARNING: this is a destructive schema or table-data operation."
                    : analysis.Kind == CopilotDatabaseSqlStatementKind.Definition
                        ? " Schema changes may cause an implicit commit and cannot be rolled back by this tool."
                        : string.Empty;
            return new CopilotToolApprovalPresentation(
                $"Approve database {analysis.RootKeyword}",
                $"Execute one {analysis.RootKeyword} statement on the configured ColorVision MySQL database. Fingerprint: {analysis.Fingerprint}.{warning}\n\nSQL preview: {excerpt}");
        }

        private static string BuildQueryContent(CopilotDatabaseSqlAnalysis analysis, CopilotDatabaseQueryResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"statement: {analysis.RootKeyword}");
            builder.AppendLine($"fingerprint: {analysis.Fingerprint}");
            builder.AppendLine($"rows_returned: {result.Rows.Count}");
            builder.AppendLine($"truncated: {result.IsTruncated.ToString().ToLowerInvariant()}");
            if (result.Columns.Count == 0)
                return builder.ToString().TrimEnd();
            builder.AppendLine("data_tsv:");
            builder.AppendLine(string.Join('\t', result.Columns.Select(SanitizeCell)));
            foreach (var row in result.Rows)
            {
                var cells = row.Select((value, index) => analysis.ContainsSensitiveIdentifier
                    || index < result.Columns.Count && IsSensitiveColumn(result.Columns[index])
                    ? "<redacted>"
                    : SanitizeCell(CopilotMcpAuditLogger.RedactText(value)));
                var line = string.Join('\t', cells);
                if (builder.Length + line.Length + Environment.NewLine.Length > MaximumContentLength)
                {
                    builder.AppendLine("<output truncated at 32000 characters>");
                    break;
                }
                builder.AppendLine(line);
            }
            return builder.ToString().TrimEnd();
        }

        private static string SanitizeCell(string value)
        {
            return (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        }

        private static bool IsSensitiveColumn(string name)
        {
            return SensitiveColumnMarkers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        private static string ReadString(CopilotAgentToolInput input, string name)
        {
            if (!input.Arguments.TryGetValue(name, out var raw) || raw == null)
                return string.Empty;
            if (raw is string text)
                return text;
            if (raw is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } element)
                return element.GetString() ?? string.Empty;
            return string.Empty;
        }

        private static int ReadInt(CopilotAgentToolInput input, string name, int fallback, int minimum, int maximum)
        {
            if (!input.Arguments.TryGetValue(name, out var raw) || raw == null)
                return fallback;
            var value = raw switch
            {
                int number => number,
                long number when number >= int.MinValue && number <= int.MaxValue => (int)number,
                System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Number } element when element.TryGetInt32(out var number) => number,
                _ => fallback,
            };
            return Math.Clamp(value, minimum, maximum);
        }

        private static CopilotToolResult DatabaseUnavailable(string toolName)
        {
            return Failure(toolName, CopilotToolFailureKind.Transient, "The ColorVision business database is not connected.", "Connect the configured MySQL database, then retry.");
        }

        private static CopilotToolFailureKind ClassifyDatabaseFailure(Exception exception)
        {
            return exception is MySqlException mysqlException
                ? mysqlException.Number switch
                {
                    1045 => CopilotToolFailureKind.Authorization,
                    1049 or 1146 => CopilotToolFailureKind.NotFound,
                    1054 or 1064 => CopilotToolFailureKind.Validation,
                    _ => CopilotToolFailureKind.Transient,
                }
                : CopilotToolFailureKind.Transient;
        }

        private static CopilotToolResult Failure(string toolName, CopilotToolFailureKind kind, string summary, string error)
        {
            return new CopilotToolResult
            {
                ToolName = toolName,
                Success = false,
                FailureKind = kind,
                Summary = summary,
                ErrorMessage = error,
            };
        }
    }
}
