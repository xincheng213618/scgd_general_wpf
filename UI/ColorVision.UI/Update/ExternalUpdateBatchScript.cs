using System.IO;
using System.Text;

namespace ColorVision.Update
{
    public static class ExternalUpdateBatchScript
    {
        public static void AppendSessionVariables(StringBuilder builder, int originalProcessId, ExitUpdateHandoffState handoffState)
        {
            builder.AppendLine($"set \"ORIGINAL_PID={originalProcessId}\"");
            builder.AppendLine($"set \"UPDATE_MARKER={EscapeValue(handoffState.MarkerPath)}\"");
            builder.AppendLine($"set \"REOPEN_REQUEST={EscapeValue(handoffState.ReopenRequestPath)}\"");
            builder.AppendLine($"set \"UPDATE_TOKEN={handoffState.LaunchToken}\"");
            builder.AppendLine($"set \"UPDATE_LOG={EscapeValue(Path.Combine(Path.GetDirectoryName(handoffState.MarkerPath)!, "update.log"))}\"");
        }

        public static void AppendLog(StringBuilder builder, string message)
        {
            builder.AppendLine($">>\"%UPDATE_LOG%\" echo [%date% %time%] {message}");
        }

        public static void AppendWaitForOriginalProcess(StringBuilder builder)
        {
            builder.AppendLine(":wait_for_original_process");
            AppendLog(builder, "Waiting for original application process to exit.");
            builder.AppendLine("powershell.exe -NoLogo -NoProfile -NonInteractive -Command \"try { $process = [Diagnostics.Process]::GetProcessById([int]$env:ORIGINAL_PID); if ($process.WaitForExit(15000)) { exit 0 }; exit 1 } catch [ArgumentException] { exit 0 } catch { exit 2 }\"");
            builder.AppendLine("if not errorlevel 1 goto wait_for_original_process_completed");
            builder.AppendLine(":wait_for_original_process_timeout");
            AppendLog(builder, "Original application process exit timed out; forcing termination.");
            builder.AppendLine("taskkill /f /pid \"%ORIGINAL_PID%\" >nul 2>nul");
            builder.AppendLine("ping -n 2 127.0.0.1 >nul");
            AppendLog(builder, "Original application process was forced to exit.");
            builder.AppendLine("exit /b 0");
            builder.AppendLine(":wait_for_original_process_completed");
            AppendLog(builder, "Original application process exited normally.");
            builder.AppendLine("exit /b 0");
        }

        public static void AppendRestartAndComplete(StringBuilder builder, string? restartArguments)
        {
            builder.AppendLine($"set \"{ExitUpdateHandoff.LaunchTokenEnvironmentVariable}=%UPDATE_TOKEN%\"");
            builder.AppendLine(string.IsNullOrWhiteSpace(restartArguments)
                ? "start \"\" /b \"%EXEPATH%\""
                : $"start \"\" /b \"%EXEPATH%\" {restartArguments}");
            builder.AppendLine("ping -n 4 127.0.0.1 >nul");
            builder.AppendLine("del /f /q \"%UPDATE_MARKER%\" >nul 2>nul");
            builder.AppendLine("del /f /q \"%REOPEN_REQUEST%\" >nul 2>nul");
        }

        private static string EscapeValue(string value) => value.Replace("%", "%%", StringComparison.Ordinal);
    }
}
