using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace ColorVision;

internal sealed record StartupFailurePresentation(
    string Component,
    string ExceptionType,
    string Detail,
    string Message);

/// <summary>
/// Keeps the earliest startup failure path independent from ColorVision UI and utility assemblies.
/// </summary>
internal static class StartupFailureGuard
{
    private const string ServicePipeName = "ColorVisionServiceHost";
    private const string StartupStatusCommand = "application-startup-status";
    private const int BackgroundConnectTimeoutMilliseconds = 250;
    private const int FailureConnectTimeoutMilliseconds = 750;
    private static int _begun;
    private static int _startupCompleted;
    private static int _failurePromptShown;

    public static void Begin()
    {
        if (Interlocked.Exchange(ref _begun, 1) != 0)
            return;

        QueueStatus("begin", "AppConstructed", null, null);
    }

    public static void ReportProgress(string stage, string? component = null)
    {
        if (Volatile.Read(ref _begun) == 0 || Volatile.Read(ref _startupCompleted) != 0)
            return;

        QueueStatus("progress", stage, component, null);
    }

    public static void MarkReady()
    {
        if (Volatile.Read(ref _begun) == 0 || Interlocked.Exchange(ref _startupCompleted, 1) != 0)
            return;

        QueueStatus("ready", "StartupCompleted", null, null);
    }

    public static bool TryHandleStartupFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (Volatile.Read(ref _begun) == 0
            || Volatile.Read(ref _startupCompleted) != 0
            || !TryCreateFailurePresentation(exception, out StartupFailurePresentation? presentation))
        {
            return false;
        }

        if (Interlocked.Exchange(ref _failurePromptShown, 1) != 0)
            return true;

        SendStatus(
            "failed-handled",
            "DispatcherUnhandledException",
            presentation.Component,
            new
            {
                exceptionType = presentation.ExceptionType,
                detail = Truncate(presentation.Detail, 1000),
                promptShown = true,
            },
            FailureConnectTimeoutMilliseconds);

        NativeMethods.MessageBox(
            IntPtr.Zero,
            presentation.Message,
            "ColorVision 无法启动",
            NativeMethods.MessageBoxOk
                | NativeMethods.MessageBoxIconError
                | NativeMethods.MessageBoxSetForeground
                | NativeMethods.MessageBoxTopMost);
        Environment.Exit(-1);
        return true;
    }

    internal static bool TryCreateFailurePresentation(
        Exception exception,
        out StartupFailurePresentation? presentation)
    {
        ArgumentNullException.ThrowIfNull(exception);
        foreach (Exception candidate in EnumerateExceptions(exception))
        {
            if (!TryDescribeDependencyFailure(candidate, out string component))
                continue;

            string detail = string.IsNullOrWhiteSpace(candidate.Message)
                ? candidate.GetType().Name
                : candidate.Message.Trim();
            string message =
                $"检测到 ColorVision 启动组件缺失、损坏或版本不一致，程序无法继续启动。{Environment.NewLine}{Environment.NewLine}" +
                $"组件：{component}{Environment.NewLine}" +
                $"错误：{Truncate(detail, 700)}{Environment.NewLine}{Environment.NewLine}" +
                "请重新安装 ColorVision 后再试。重新安装不会删除现有配置。";
            presentation = new StartupFailurePresentation(
                component,
                candidate.GetType().FullName ?? candidate.GetType().Name,
                detail,
                message);
            return true;
        }

        presentation = null;
        return false;
    }

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        Stack<Exception> pending = new();
        HashSet<Exception> visited = new(ReferenceEqualityComparer.Instance);
        pending.Push(exception);
        while (pending.Count > 0)
        {
            Exception current = pending.Pop();
            if (!visited.Add(current))
                continue;

            yield return current;
            if (current is ReflectionTypeLoadException reflectionTypeLoadException)
            {
                foreach (Exception? loaderException in reflectionTypeLoadException.LoaderExceptions)
                {
                    if (loaderException != null)
                        pending.Push(loaderException);
                }
            }
            if (current is AggregateException aggregateException)
            {
                foreach (Exception innerException in aggregateException.InnerExceptions)
                    pending.Push(innerException);
            }
            if (current.InnerException != null)
                pending.Push(current.InnerException);
        }
    }

    private static bool TryDescribeDependencyFailure(Exception exception, out string component)
    {
        switch (exception)
        {
            case FileNotFoundException fileNotFoundException
                when TryFormatAssemblyComponent(fileNotFoundException.FileName, out component):
                return true;
            case FileLoadException fileLoadException
                when TryFormatAssemblyComponent(fileLoadException.FileName, out component):
                return true;
            case BadImageFormatException badImageFormatException:
                component = TryFormatAssemblyComponent(badImageFormatException.FileName, out string formattedBadImage)
                    ? formattedBadImage
                    : "应用运行组件";
                return true;
            case DllNotFoundException:
                component = "本机运行库 DLL";
                return true;
            case EntryPointNotFoundException:
                component = "本机运行库 DLL";
                return true;
            case TypeLoadException typeLoadException:
                component = string.IsNullOrWhiteSpace(typeLoadException.TypeName)
                    ? "应用程序集"
                    : typeLoadException.TypeName;
                return true;
            case ReflectionTypeLoadException:
                component = "应用程序集";
                return true;
            default:
                component = string.Empty;
                return false;
        }
    }

    private static bool TryFormatAssemblyComponent(string? fileName, out string component)
    {
        component = string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        string value = fileName.Trim();
        if (value.Contains(", Version=", StringComparison.OrdinalIgnoreCase))
        {
            string assemblyName = value.Split(',')[0].Trim();
            if (assemblyName.Length == 0)
                return false;
            component = assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? assemblyName
                : assemblyName + ".dll";
            return true;
        }

        if (!value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            && !value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        component = Path.GetFileName(value);
        return !string.IsNullOrWhiteSpace(component);
    }

    private static void QueueStatus(string state, string stage, string? component, object? details)
    {
        ThreadPool.QueueUserWorkItem(
            static context =>
            {
                StartupStatus status = (StartupStatus)context!;
                SendStatus(
                    status.State,
                    status.Stage,
                    status.Component,
                    status.Details,
                    BackgroundConnectTimeoutMilliseconds);
            },
            new StartupStatus(state, stage, component, details));
    }

    private static bool SendStatus(
        string state,
        string stage,
        string? component,
        object? details,
        int timeoutMilliseconds)
    {
        try
        {
            using NamedPipeClientStream pipe = new(".", ServicePipeName, PipeDirection.InOut);
            pipe.Connect(timeoutMilliseconds);
            pipe.ReadMode = PipeTransmissionMode.Byte;
            if (pipe.CanTimeout)
            {
                pipe.ReadTimeout = timeoutMilliseconds;
                pipe.WriteTimeout = timeoutMilliseconds;
            }

            string requestId = Guid.NewGuid().ToString("N");
            string requestJson = JsonSerializer.Serialize(new
            {
                protocolVersion = 2,
                requestId,
                operationId = Guid.NewGuid().ToString("N"),
                command = StartupStatusCommand,
                data = new
                {
                    state,
                    stage = Truncate(stage, 160),
                    component = Truncate(component, 320),
                    details,
                },
            });

            using StreamWriter writer = new(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using StreamReader reader = new(pipe, new UTF8Encoding(false), false, leaveOpen: true);
            writer.WriteLine(requestJson);
            string? responseJson = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(responseJson))
                return false;

            using JsonDocument response = JsonDocument.Parse(responseJson);
            return response.RootElement.TryGetProperty("success", out JsonElement success)
                && success.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }

    private static string Truncate(string? value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maximumLength)
            return value ?? string.Empty;
        return value[..maximumLength];
    }

    private sealed record StartupStatus(string State, string Stage, string? Component, object? Details);

    private static class NativeMethods
    {
        public const uint MessageBoxOk = 0x00000000;
        public const uint MessageBoxIconError = 0x00000010;
        public const uint MessageBoxSetForeground = 0x00010000;
        public const uint MessageBoxTopMost = 0x00040000;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
        public static extern int MessageBox(IntPtr owner, string text, string caption, uint type);
    }
}
