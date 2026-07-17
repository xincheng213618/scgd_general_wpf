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
        }

        public static void AppendWaitForOriginalProcess(StringBuilder builder)
        {
            builder.AppendLine(":wait_for_original_process");
            builder.AppendLine("set \"WAIT_ATTEMPTS=0\"");
            builder.AppendLine(":wait_for_original_process_loop");
            builder.AppendLine("tasklist /fi \"PID eq %ORIGINAL_PID%\" /nh 2>nul | findstr /r /c:\"[ ]%ORIGINAL_PID%[ ]\" >nul");
            builder.AppendLine("if errorlevel 1 exit /b 0");
            builder.AppendLine("set /a WAIT_ATTEMPTS+=1");
            builder.AppendLine("if %WAIT_ATTEMPTS% GEQ 15 goto wait_for_original_process_timeout");
            builder.AppendLine("ping -n 2 127.0.0.1 >nul");
            builder.AppendLine("goto wait_for_original_process_loop");
            builder.AppendLine(":wait_for_original_process_timeout");
            builder.AppendLine("taskkill /f /pid \"%ORIGINAL_PID%\" >nul 2>nul");
            builder.AppendLine("ping -n 2 127.0.0.1 >nul");
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
