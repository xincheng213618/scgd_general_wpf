using ColorVision.Update;
using System.IO;
using System.Text;

namespace ColorVision.UI.Plugins
{
    internal sealed record PluginDirectoryReplacement(
        string PluginId,
        string SourceDirectory,
        string TargetDirectory);

    /// <summary>
    /// Emits the directory-swap portion shared by plugin update and plugin recovery batches.
    /// New content is copied to a same-volume transaction directory before the live directory
    /// is renamed. If any swap fails, already-swapped plugins are restored in reverse order.
    /// </summary>
    internal static class PluginDirectoryTransactionBatchScript
    {
        internal static void AppendTransaction(
            StringBuilder builder,
            IReadOnlyList<PluginDirectoryReplacement> replacements,
            string transactionDirectory,
            string failureLabel,
            string labelPrefix = "plugin_transaction")
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(replacements);
            ArgumentException.ThrowIfNullOrWhiteSpace(transactionDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(failureLabel);
            ArgumentException.ThrowIfNullOrWhiteSpace(labelPrefix);

            if (replacements.Count == 0)
                return;

            string normalizedTransactionDirectory = NormalizeAbsoluteDirectory(transactionDirectory, nameof(transactionDirectory));
            EnsureExistingDirectoryIsNotReparsePoint(normalizedTransactionDirectory, "Plugin transaction directory");
            foreach (PluginDirectoryReplacement replacement in replacements)
            {
                string sourceDirectory = NormalizeAbsoluteDirectory(replacement.SourceDirectory, nameof(replacement.SourceDirectory));
                string targetDirectory = NormalizeAbsoluteDirectory(replacement.TargetDirectory, nameof(replacement.TargetDirectory));
                string targetParent = Path.GetDirectoryName(targetDirectory)
                    ?? throw new InvalidDataException("Plugin transaction target has no parent directory.");
                if (!string.Equals(Path.GetFileName(targetDirectory), replacement.PluginId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(Path.GetDirectoryName(normalizedTransactionDirectory), targetParent, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Plugin transaction target must be a matching direct child of one installation Plugins directory.");
                }
                if (PathsOverlap(sourceDirectory, targetDirectory))
                    throw new InvalidDataException("Plugin transaction source and target must not overlap.");

                EnsureExistingDirectoryIsNotReparsePoint(sourceDirectory, "Plugin transaction source", mustExist: true);
                EnsureExistingDirectoryIsNotReparsePoint(targetParent, "Installed Plugins directory");
                EnsureExistingDirectoryIsNotReparsePoint(targetDirectory, "Installed plugin target");
            }

            builder.AppendLine($"set \"PLUGIN_TRANSACTION_ROOT={EscapeValue(normalizedTransactionDirectory)}\"");
            builder.AppendLine("if exist \"%PLUGIN_TRANSACTION_ROOT%\" rd /s /q \"%PLUGIN_TRANSACTION_ROOT%\"");
            builder.AppendLine("if exist \"%PLUGIN_TRANSACTION_ROOT%\" goto " + failureLabel);
            builder.AppendLine("mkdir \"%PLUGIN_TRANSACTION_ROOT%\\incoming\"");
            builder.AppendLine("if errorlevel 1 goto " + failureLabel);
            builder.AppendLine("mkdir \"%PLUGIN_TRANSACTION_ROOT%\\rollback\"");
            builder.AppendLine("if errorlevel 1 goto " + failureLabel);
            builder.AppendLine("set \"PLUGIN_ROLLBACK_FAILED=0\"");

            for (int index = 0; index < replacements.Count; index++)
            {
                PluginDirectoryReplacement replacement = replacements[index];
                builder.AppendLine($"set \"PLUGIN_HAD_TARGET_{index}=0\"");
                builder.AppendLine($"set \"PLUGIN_INSTALLED_{index}=0\"");
                builder.AppendLine($"set \"COPY_SOURCE={EscapeValue(replacement.SourceDirectory)}\"");
                builder.AppendLine($"set \"COPY_TARGET=%PLUGIN_TRANSACTION_ROOT%\\incoming\\{index}\"");
                builder.AppendLine("call :copy_complete_directory");
                builder.AppendLine($"if errorlevel 1 goto {labelPrefix}_rollback");
            }

            for (int index = 0; index < replacements.Count; index++)
            {
                PluginDirectoryReplacement replacement = replacements[index];
                builder.AppendLine($"set \"PLUGIN_TARGET_{index}={EscapeValue(replacement.TargetDirectory)}\"");
                builder.AppendLine($"set \"PLUGIN_ROLLBACK_{index}=%PLUGIN_TRANSACTION_ROOT%\\rollback\\{index}\"");
                builder.AppendLine($"if not exist \"%PLUGIN_TARGET_{index}%\" goto {labelPrefix}_no_target_{index}");
                builder.AppendLine($"move /y \"%PLUGIN_TARGET_{index}%\" \"%PLUGIN_ROLLBACK_{index}%\" >nul");
                builder.AppendLine($"if errorlevel 1 goto {labelPrefix}_rollback");
                builder.AppendLine($"set \"PLUGIN_HAD_TARGET_{index}=1\"");
                builder.AppendLine($":{labelPrefix}_no_target_{index}");
                builder.AppendLine($"move /y \"%PLUGIN_TRANSACTION_ROOT%\\incoming\\{index}\" \"%PLUGIN_TARGET_{index}%\" >nul");
                builder.AppendLine($"if errorlevel 1 goto {labelPrefix}_rollback");
                builder.AppendLine($"set \"PLUGIN_INSTALLED_{index}=1\"");
            }

            builder.AppendLine("rd /s /q \"%PLUGIN_TRANSACTION_ROOT%\" >nul 2>nul");
            builder.AppendLine($"goto {labelPrefix}_complete");
            builder.AppendLine($":{labelPrefix}_rollback");
            ExternalUpdateBatchScript.AppendLog(builder, "Plugin directory transaction failed; rolling back switched plugins.");

            for (int index = replacements.Count - 1; index >= 0; index--)
            {
                builder.AppendLine($"if \"%PLUGIN_HAD_TARGET_{index}%\"==\"1\" goto {labelPrefix}_restore_old_{index}");
                builder.AppendLine($"if not \"%PLUGIN_INSTALLED_{index}%\"==\"1\" goto {labelPrefix}_rollback_next_{index}");
                builder.AppendLine($"if exist \"%PLUGIN_TARGET_{index}%\" rd /s /q \"%PLUGIN_TARGET_{index}%\"");
                builder.AppendLine($"if exist \"%PLUGIN_TARGET_{index}%\" set \"PLUGIN_ROLLBACK_FAILED=1\"");
                builder.AppendLine($"goto {labelPrefix}_rollback_next_{index}");
                builder.AppendLine($":{labelPrefix}_restore_old_{index}");
                builder.AppendLine($"if exist \"%PLUGIN_TARGET_{index}%\" rd /s /q \"%PLUGIN_TARGET_{index}%\"");
                builder.AppendLine($"if exist \"%PLUGIN_TARGET_{index}%\" set \"PLUGIN_ROLLBACK_FAILED=1\"");
                builder.AppendLine($"if not exist \"%PLUGIN_ROLLBACK_{index}%\" set \"PLUGIN_ROLLBACK_FAILED=1\"");
                builder.AppendLine($"if exist \"%PLUGIN_ROLLBACK_{index}%\" move /y \"%PLUGIN_ROLLBACK_{index}%\" \"%PLUGIN_TARGET_{index}%\" >nul");
                builder.AppendLine($"if not exist \"%PLUGIN_TARGET_{index}%\" set \"PLUGIN_ROLLBACK_FAILED=1\"");
                builder.AppendLine($":{labelPrefix}_rollback_next_{index}");
            }

            builder.AppendLine("if \"%PLUGIN_ROLLBACK_FAILED%\"==\"0\" rd /s /q \"%PLUGIN_TRANSACTION_ROOT%\" >nul 2>nul");
            builder.AppendLine("if \"%PLUGIN_ROLLBACK_FAILED%\"==\"0\" goto " + failureLabel);
            ExternalUpdateBatchScript.AppendLog(builder, "Plugin rollback was incomplete; persistent recovery backup was preserved.");
            builder.AppendLine("goto " + failureLabel);
            builder.AppendLine($":{labelPrefix}_complete");
        }

        internal static void AppendCopyCompleteDirectoryFunction(StringBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AppendLine(":copy_complete_directory");
            builder.AppendLine("if not exist \"%COPY_SOURCE%\" exit /b 1");
            builder.AppendLine("if exist \"%COPY_TARGET%\" rd /s /q \"%COPY_TARGET%\"");
            builder.AppendLine("if exist \"%COPY_TARGET%\" exit /b 1");
            builder.AppendLine("mkdir \"%COPY_TARGET%\"");
            builder.AppendLine("if errorlevel 1 exit /b 1");
            builder.AppendLine("where robocopy >nul 2>nul");
            builder.AppendLine("if errorlevel 1 goto copy_complete_directory_xcopy");
            builder.AppendLine("robocopy \"%COPY_SOURCE%\" \"%COPY_TARGET%\" *.* /E /COPY:DAT /DCOPY:T /NFL /NDL /NP /NJH /NJS /R:2 /W:1");
            builder.AppendLine("if errorlevel 8 exit /b 1");
            builder.AppendLine("exit /b 0");
            builder.AppendLine(":copy_complete_directory_xcopy");
            builder.AppendLine("xcopy /y /e /h /i \"%COPY_SOURCE%\\*\" \"%COPY_TARGET%\\\" >nul");
            builder.AppendLine("if errorlevel 1 exit /b 1");
            builder.AppendLine("exit /b 0");
        }

        private static string EscapeValue(string value) => value.Replace("%", "%%", StringComparison.Ordinal);

        private static string NormalizeAbsoluteDirectory(string directory, string parameterName)
        {
            if (!Path.IsPathFullyQualified(directory))
                throw new ArgumentException("Plugin transaction path must be absolute.", parameterName);
            return Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool PathsOverlap(string firstDirectory, string secondDirectory)
        {
            if (string.Equals(firstDirectory, secondDirectory, StringComparison.OrdinalIgnoreCase))
                return true;
            return firstDirectory.StartsWith(secondDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || secondDirectory.StartsWith(firstDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureExistingDirectoryIsNotReparsePoint(
            string directory,
            string description,
            bool mustExist = false)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(directory);
                if (!attributes.HasFlag(FileAttributes.Directory))
                    throw new IOException($"{description} is not a directory: {directory}");
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new InvalidDataException($"{description} cannot be a reparse point: {directory}");
            }
            catch (FileNotFoundException) when (!mustExist)
            {
            }
            catch (DirectoryNotFoundException) when (!mustExist)
            {
            }
        }
    }
}
