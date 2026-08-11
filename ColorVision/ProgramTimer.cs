// // Copyright (c) Microsoft. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.

using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ColorVision
{
    public sealed record StartupFailureInfo(
        string? Version,
        string? Stage,
        string? Component,
        DateTimeOffset? StartedAt,
        int? ProcessId);

    public class StartupRegistryChecker
    {
        public static StartupRegistryChecker Instance => new StartupRegistryChecker();

        private const string RegistryPath = @"Software\ColorVision\ColorVision";
        private const string StartupAttemptsSubKey = "StartupAttempts";
        private const string LegacyStartupFlagKey = "Running";
        private static readonly string InstallationKey = ColorVision.Update.ExitUpdateHandoff.GetInstallationKey(AppDomain.CurrentDomain.BaseDirectory);
        private static readonly string LegacyInstallationStartupFlagKey = $"Running_{InstallationKey}";
        private static readonly string LegacyInstallationStartupProcessIdKey = $"RunningPid_{InstallationKey}";
        private static readonly string LegacyInstallationStartupVersionKey = $"RunningVersion_{InstallationKey}";
        private static readonly string LegacyInstallationStartupStageKey = $"RunningStage_{InstallationKey}";
        private static readonly string LegacyInstallationStartupComponentKey = $"RunningComponent_{InstallationKey}";
        private static readonly string LegacyInstallationStartupStartedAtKey = $"RunningStartedAt_{InstallationKey}";
        private static readonly string AttemptId = $"{Environment.ProcessId}_{Guid.NewGuid():N}";
        private static readonly DateTimeOffset ProcessStartedAt = GetCurrentProcessStartedAt();
        private static readonly string ExecutablePath = GetCurrentExecutablePath();
        private static bool _attemptCompleted;

        public static StartupFailureInfo? PreviousFailure { get; private set; }

        public static bool CheckAndSet()
        {
            using RegistryKey regKey = Registry.CurrentUser.CreateSubKey(RegistryPath);
            List<StartupFailureInfo> incompleteAttempts = ReadAndRemoveIncompleteAttempts(regKey);
            StartupFailureInfo? legacyFailure = ReadAndMigrateLegacyAttempt(regKey);
            if (legacyFailure != null)
                incompleteAttempts.Add(legacyFailure);

            PreviousFailure = incompleteAttempts
                .OrderByDescending(item => item.StartedAt ?? DateTimeOffset.MinValue)
                .FirstOrDefault();

            using RegistryKey attemptKey = regKey.CreateSubKey(GetCurrentAttemptSubKeyPath());
            attemptKey.SetValue("ProcessId", Environment.ProcessId, RegistryValueKind.DWord);
            attemptKey.SetValue("ProcessStartedAt", ProcessStartedAt.ToString("O"), RegistryValueKind.String);
            attemptKey.SetValue("ExecutablePath", ExecutablePath, RegistryValueKind.String);
            attemptKey.SetValue("Version", typeof(App).Assembly.GetName().Version?.ToString() ?? string.Empty, RegistryValueKind.String);
            attemptKey.SetValue("Stage", "CoreInitialized", RegistryValueKind.String);
            attemptKey.SetValue("StartedAt", DateTimeOffset.UtcNow.ToString("O"), RegistryValueKind.String);
            WriteRecoverySource(attemptKey, PreviousFailure);
            _attemptCompleted = false;
            return PreviousFailure == null;
        }

        public static void MarkStage(string stage, string? component = null)
        {
            if (string.IsNullOrWhiteSpace(stage) || _attemptCompleted)
                return;

            using RegistryKey? attemptKey = Registry.CurrentUser.OpenSubKey(
                $@"{RegistryPath}\{GetCurrentAttemptSubKeyPath()}",
                writable: true);
            if (attemptKey == null || ReadNullableInt32(attemptKey, "ProcessId") != Environment.ProcessId)
                return;

            attemptKey.SetValue("Stage", stage.Trim(), RegistryValueKind.String);
            if (string.IsNullOrWhiteSpace(component))
                attemptKey.DeleteValue("Component", false);
            else
                attemptKey.SetValue("Component", component.Trim(), RegistryValueKind.String);
            DeleteRecoverySource(attemptKey);
        }

        /// <summary>
        /// 启动成功后清理当前进程的启动记录。
        /// </summary>
        public static void Clear()
        {
            using RegistryKey regKey = Registry.CurrentUser.CreateSubKey(RegistryPath);
            regKey.DeleteSubKeyTree(GetCurrentAttemptSubKeyPath(), false);
            DeleteLegacyAttemptForCurrentProcess(regKey);
            _attemptCompleted = true;
        }

        public static void CompleteForRecoveryRestart() => Clear();

        public static void OnApplicationExit()
        {
            if (_attemptCompleted)
                Clear();
        }

        private static List<StartupFailureInfo> ReadAndRemoveIncompleteAttempts(RegistryKey regKey)
        {
            List<StartupFailureInfo> incompleteAttempts = new();
            using RegistryKey? installationAttemptsKey = regKey.OpenSubKey(
                $@"{StartupAttemptsSubKey}\{InstallationKey}",
                writable: true);
            if (installationAttemptsKey == null)
                return incompleteAttempts;

            foreach (string attemptName in installationAttemptsKey.GetSubKeyNames())
            {
                StartupFailureInfo failure;
                using (RegistryKey? attemptKey = installationAttemptsKey.OpenSubKey(attemptName))
                {
                    if (attemptKey == null || IsRecordedProcessStillRunning(attemptKey))
                        continue;

                    failure = ReadFailureInfo(attemptKey);
                }
                incompleteAttempts.Add(failure);
                installationAttemptsKey.DeleteSubKeyTree(attemptName, false);
            }

            return incompleteAttempts;
        }

        private static StartupFailureInfo? ReadAndMigrateLegacyAttempt(RegistryKey regKey)
        {
            bool globalLegacyAttemptIncomplete = ReadDword(regKey, LegacyStartupFlagKey) == 1;
            bool installationAttemptIncomplete = ReadDword(regKey, LegacyInstallationStartupFlagKey) == 1;
            int? processId = ReadNullableInt32(regKey, LegacyInstallationStartupProcessIdKey);
            bool installationProcessIsRunning = installationAttemptIncomplete
                && processId.HasValue
                && IsProcessRunning(processId.Value);

            StartupFailureInfo? failure = null;
            if (globalLegacyAttemptIncomplete || (installationAttemptIncomplete && !installationProcessIsRunning))
            {
                failure = new StartupFailureInfo(
                    regKey.GetValue(LegacyInstallationStartupVersionKey) as string,
                    regKey.GetValue(LegacyInstallationStartupStageKey) as string,
                    regKey.GetValue(LegacyInstallationStartupComponentKey) as string,
                    ReadDateTimeOffset(regKey, LegacyInstallationStartupStartedAtKey),
                    processId);
            }

            regKey.DeleteValue(LegacyStartupFlagKey, false);
            if (!installationProcessIsRunning)
                DeleteLegacyInstallationAttempt(regKey);
            return failure;
        }

        private static void DeleteLegacyAttemptForCurrentProcess(RegistryKey regKey)
        {
            regKey.DeleteValue(LegacyStartupFlagKey, false);
            int? recordedProcessId = ReadNullableInt32(regKey, LegacyInstallationStartupProcessIdKey);
            if (!recordedProcessId.HasValue || recordedProcessId == Environment.ProcessId)
                DeleteLegacyInstallationAttempt(regKey);
        }

        private static void DeleteLegacyInstallationAttempt(RegistryKey regKey)
        {
            regKey.DeleteValue(LegacyInstallationStartupFlagKey, false);
            regKey.DeleteValue(LegacyInstallationStartupProcessIdKey, false);
            regKey.DeleteValue(LegacyInstallationStartupVersionKey, false);
            regKey.DeleteValue(LegacyInstallationStartupStageKey, false);
            regKey.DeleteValue(LegacyInstallationStartupComponentKey, false);
            regKey.DeleteValue(LegacyInstallationStartupStartedAtKey, false);
        }

        private static StartupFailureInfo ReadFailureInfo(RegistryKey attemptKey)
        {
            return new StartupFailureInfo(
                attemptKey.GetValue("RecoveryVersion") as string
                    ?? attemptKey.GetValue("Version") as string,
                attemptKey.GetValue("RecoveryStage") as string
                    ?? attemptKey.GetValue("Stage") as string,
                attemptKey.GetValue("RecoveryComponent") as string
                    ?? attemptKey.GetValue("Component") as string,
                ReadDateTimeOffset(attemptKey, "RecoveryStartedAt")
                    ?? ReadDateTimeOffset(attemptKey, "StartedAt"),
                ReadNullableInt32(attemptKey, "RecoveryProcessId")
                    ?? ReadNullableInt32(attemptKey, "ProcessId"));
        }

        private static void WriteRecoverySource(RegistryKey attemptKey, StartupFailureInfo? failure)
        {
            if (failure == null)
                return;

            SetOptionalString(attemptKey, "RecoveryVersion", failure.Version);
            SetOptionalString(attemptKey, "RecoveryStage", failure.Stage);
            SetOptionalString(attemptKey, "RecoveryComponent", failure.Component);
            if (failure.StartedAt.HasValue)
                attemptKey.SetValue("RecoveryStartedAt", failure.StartedAt.Value.ToString("O"), RegistryValueKind.String);
            if (failure.ProcessId.HasValue)
                attemptKey.SetValue("RecoveryProcessId", failure.ProcessId.Value, RegistryValueKind.DWord);
        }

        private static void DeleteRecoverySource(RegistryKey attemptKey)
        {
            attemptKey.DeleteValue("RecoveryVersion", false);
            attemptKey.DeleteValue("RecoveryStage", false);
            attemptKey.DeleteValue("RecoveryComponent", false);
            attemptKey.DeleteValue("RecoveryStartedAt", false);
            attemptKey.DeleteValue("RecoveryProcessId", false);
        }

        private static void SetOptionalString(RegistryKey regKey, string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                regKey.SetValue(name, value, RegistryValueKind.String);
        }

        private static int ReadDword(RegistryKey regKey, string name)
        {
            object? value = regKey.GetValue(name, 0);
            return value is int intValue ? intValue : 0;
        }

        private static int? ReadNullableInt32(RegistryKey regKey, string name)
        {
            object? value = regKey.GetValue(name);
            return value is int intValue ? intValue : null;
        }

        private static DateTimeOffset? ReadDateTimeOffset(RegistryKey regKey, string name)
        {
            return regKey.GetValue(name) is string value
                && DateTimeOffset.TryParse(value, out DateTimeOffset result)
                    ? result
                    : null;
        }

        private static bool IsRecordedProcessStillRunning(RegistryKey attemptKey)
        {
            int? processId = ReadNullableInt32(attemptKey, "ProcessId");
            DateTimeOffset? processStartedAt = ReadDateTimeOffset(attemptKey, "ProcessStartedAt");
            string? executablePath = attemptKey.GetValue("ExecutablePath") as string;
            if (!processId.HasValue || !processStartedAt.HasValue || string.IsNullOrWhiteSpace(executablePath))
                return false;

            try
            {
                using Process process = Process.GetProcessById(processId.Value);
                if (process.HasExited)
                    return false;

                DateTimeOffset actualStartedAt = new(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
                if ((actualStartedAt - processStartedAt.Value).Duration() > TimeSpan.FromSeconds(2))
                    return false;

                try
                {
                    string? actualExecutablePath = process.MainModule?.FileName;
                    return string.IsNullOrWhiteSpace(actualExecutablePath)
                        || string.Equals(
                            Path.GetFullPath(actualExecutablePath),
                            Path.GetFullPath(executablePath),
                            StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    // Reading MainModule can be denied across elevation boundaries. PID plus the
                    // process start timestamp still uniquely identifies the active attempt.
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsProcessRunning(int processId)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private static string GetCurrentAttemptSubKeyPath() =>
            $@"{StartupAttemptsSubKey}\{InstallationKey}\{AttemptId}";

        private static DateTimeOffset GetCurrentProcessStartedAt()
        {
            try
            {
                using Process process = Process.GetCurrentProcess();
                return new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
            }
            catch
            {
                return DateTimeOffset.UtcNow;
            }
        }

        private static string GetCurrentExecutablePath()
        {
            return Environment.ProcessPath
                ?? Assembly.GetEntryAssembly()?.Location
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ColorVision.exe");
        }
    }

    public class InitAppender : AppenderSkeleton
    {
        public StringBuilder Buffer { get; set; } = new StringBuilder();

        protected override void Append(LoggingEvent loggingEvent)
        {
            var renderedMessage = RenderLoggingEvent(loggingEvent);
            Buffer.Append(renderedMessage);
        }
        protected override void OnClose()
        {
            base.OnClose();
            Buffer.Clear();
        }
    }


    public static class ProgramTimer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));
        private static Stopwatch _stopwatch;
        public static InitAppender InitAppender { get; set; }
        private static Hierarchy Hierarchy { get; set; }

        public static void Start()
        
        {
            _stopwatch = Stopwatch.StartNew();

            Hierarchy = (Hierarchy)LogManager.GetRepository();
            InitAppender = new InitAppender();
            InitAppender.Layout = new PatternLayout("%date{HH:mm:ss;fff} %-5level %message%newline");
            Hierarchy.Root.AddAppender(InitAppender);
            log4net.Config.BasicConfigurator.Configure(Hierarchy);
        }

        public static void StopAndReport()
        {
            if (_stopwatch != null)
            {
                _stopwatch.Stop();
                log.Info($"StopAndReport: {_stopwatch.Elapsed.TotalSeconds} s");
            }
            else
            {
                log.Info("StopAndReport");
            }
        }
    }
}
