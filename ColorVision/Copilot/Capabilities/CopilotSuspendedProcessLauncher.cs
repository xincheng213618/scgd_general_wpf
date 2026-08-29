using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotProcessCommandLineTooLongException : InvalidOperationException
    {
        public CopilotProcessCommandLineTooLongException()
            : base($"The encoded Windows process command line cannot exceed {CopilotSuspendedProcessLauncher.MaximumCommandLineCharacters} characters.")
        {
        }
    }

    /// <summary>
    /// Starts a process suspended, contains it in a kill-on-close Job Object, and only then
    /// permits its primary thread to execute. The explicit inherited-handle list prevents
    /// unrelated inheritable handles from crossing the process boundary.
    /// </summary>
    internal sealed class CopilotSuspendedProcessLauncher : IDisposable
    {
        internal const int MaximumCommandLineCharacters = 32_766;
        private const uint CreateSuspended = 0x00000004;
        private const uint CreateNoWindow = 0x08000000;
        private const uint CreateUnicodeEnvironment = 0x00000400;
        private const uint ExtendedStartupInfoPresent = 0x00080000;
        private const uint StartfUseStdHandles = 0x00000100;
        private const uint HandleFlagInherit = 0x00000001;
        private const int ErrorInsufficientBuffer = 122;
        private static readonly IntPtr ProcThreadAttributeHandleList = (IntPtr)0x00020002;

        private Process? _process;
        private StreamReader? _standardOutput;
        private StreamReader? _standardError;
        private CopilotWindowsProcessJob? _processJob;

        private CopilotSuspendedProcessLauncher(
            Process process,
            StreamReader standardOutput,
            StreamReader standardError,
            CopilotWindowsProcessJob processJob)
        {
            _process = process;
            _standardOutput = standardOutput;
            _standardError = standardError;
            _processJob = processJob;
        }

        public Process Process => _process
            ?? throw new ObjectDisposedException(nameof(CopilotSuspendedProcessLauncher));

        public StreamReader StandardOutput => _standardOutput
            ?? throw new ObjectDisposedException(nameof(CopilotSuspendedProcessLauncher));

        public StreamReader StandardError => _standardError
            ?? throw new ObjectDisposedException(nameof(CopilotSuspendedProcessLauncher));

        public CopilotWindowsProcessJob ProcessJob => _processJob
            ?? throw new ObjectDisposedException(nameof(CopilotSuspendedProcessLauncher));

        public static async Task<CopilotSuspendedProcessLauncher> LaunchAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            IReadOnlyDictionary<string, string> environmentVariables,
            Encoding streamEncoding,
            Func<Process, CopilotWindowsProcessJob?> tryAssignProcessJob,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
            ArgumentNullException.ThrowIfNull(environmentVariables);
            ArgumentNullException.ThrowIfNull(streamEncoding);
            ArgumentNullException.ThrowIfNull(tryAssignProcessJob);
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("Suspended process containment is only supported on Windows.");
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryBuildCommandLine(executablePath, arguments, out var encodedCommandLine))
                throw new CopilotProcessCommandLineTooLongException();

            SafeFileHandle? childStandardInput = null;
            SafeFileHandle? parentStandardInput = null;
            SafeFileHandle? parentStandardOutput = null;
            SafeFileHandle? childStandardOutput = null;
            SafeFileHandle? parentStandardError = null;
            SafeFileHandle? childStandardError = null;
            SafeFileHandle? nativeProcess = null;
            SafeFileHandle? nativeThread = null;
            FileStream? standardOutputStream = null;
            FileStream? standardErrorStream = null;
            StreamReader? standardOutput = null;
            StreamReader? standardError = null;
            Process? process = null;
            CopilotWindowsProcessJob? processJob = null;
            IntPtr attributeList = IntPtr.Zero;
            IntPtr inheritedHandles = IntPtr.Zero;
            GCHandle pinnedEnvironment = default;
            GCHandle pinnedCommandLine = default;
            var attributeListInitialized = false;
            var ownershipTransferred = false;

            try
            {
                CreateAnonymousPipe(out childStandardInput, out parentStandardInput);
                CreateAnonymousPipe(out parentStandardOutput, out childStandardOutput);
                CreateAnonymousPipe(out parentStandardError, out childStandardError);

                SetParentPipeEndNonInheritable(parentStandardInput);
                SetParentPipeEndNonInheritable(parentStandardOutput);
                SetParentPipeEndNonInheritable(parentStandardError);

                var attributeListSize = IntPtr.Zero;
                if (InitializeProcThreadAttributeList(
                    IntPtr.Zero,
                    1,
                    0,
                    ref attributeListSize))
                {
                    throw new InvalidOperationException(
                        "InitializeProcThreadAttributeList unexpectedly succeeded without storage.");
                }
                if (Marshal.GetLastWin32Error() != ErrorInsufficientBuffer
                    || attributeListSize == IntPtr.Zero)
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "The process attribute-list size could not be determined.");
                }

                attributeList = Marshal.AllocHGlobal(attributeListSize);
                if (!InitializeProcThreadAttributeList(
                    attributeList,
                    1,
                    0,
                    ref attributeListSize))
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "The process attribute list could not be initialized.");
                }
                attributeListInitialized = true;

                inheritedHandles = Marshal.AllocHGlobal(checked(IntPtr.Size * 3));
                Marshal.WriteIntPtr(inheritedHandles, 0, childStandardInput.DangerousGetHandle());
                Marshal.WriteIntPtr(inheritedHandles, IntPtr.Size, childStandardOutput.DangerousGetHandle());
                Marshal.WriteIntPtr(inheritedHandles, IntPtr.Size * 2, childStandardError.DangerousGetHandle());
                if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    ProcThreadAttributeHandleList,
                    inheritedHandles,
                    (IntPtr)(IntPtr.Size * 3),
                    IntPtr.Zero,
                    IntPtr.Zero))
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "The inherited process-handle list could not be configured.");
                }

                var startupInfo = new StartupInfoEx
                {
                    StartupInfo = new StartupInfo
                    {
                        Size = Marshal.SizeOf<StartupInfoEx>(),
                        Flags = StartfUseStdHandles,
                        StandardInput = childStandardInput.DangerousGetHandle(),
                        StandardOutput = childStandardOutput.DangerousGetHandle(),
                        StandardError = childStandardError.DangerousGetHandle(),
                    },
                    AttributeList = attributeList,
                };
                var commandLine = new char[encodedCommandLine.Length + 1];
                encodedCommandLine.CopyTo(0, commandLine, 0, encodedCommandLine.Length);
                var environmentBlock = BuildEnvironmentBlock(environmentVariables);
                pinnedCommandLine = GCHandle.Alloc(commandLine, GCHandleType.Pinned);
                pinnedEnvironment = GCHandle.Alloc(environmentBlock, GCHandleType.Pinned);

                cancellationToken.ThrowIfCancellationRequested();
                var creationFlags = CreateSuspended
                    | CreateNoWindow
                    | CreateUnicodeEnvironment
                    | ExtendedStartupInfoPresent;
                if (!CreateProcess(
                    executablePath,
                    pinnedCommandLine.AddrOfPinnedObject(),
                    IntPtr.Zero,
                    IntPtr.Zero,
                    inheritHandles: true,
                    creationFlags,
                    pinnedEnvironment.AddrOfPinnedObject(),
                    workingDirectory,
                    ref startupInfo,
                    out var processInformation))
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "The suspended process could not be created.");
                }

                nativeProcess = new SafeFileHandle(processInformation.Process, ownsHandle: true);
                nativeThread = new SafeFileHandle(processInformation.Thread, ownsHandle: true);

                // The child owns these three handles now. Closing every parent-side stdin
                // handle before ResumeThread makes the target observe EOF immediately.
                childStandardInput.Dispose();
                childStandardInput = null;
                parentStandardInput.Dispose();
                parentStandardInput = null;
                childStandardOutput.Dispose();
                childStandardOutput = null;
                childStandardError.Dispose();
                childStandardError = null;

                standardOutputStream = new FileStream(
                    parentStandardOutput,
                    FileAccess.Read,
                    bufferSize: 4096,
                    isAsync: false);
                parentStandardOutput = null;
                standardOutput = new StreamReader(
                    standardOutputStream,
                    streamEncoding,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 4096,
                    leaveOpen: false);
                standardOutputStream = null;

                standardErrorStream = new FileStream(
                    parentStandardError,
                    FileAccess.Read,
                    bufferSize: 4096,
                    isAsync: false);
                parentStandardError = null;
                standardError = new StreamReader(
                    standardErrorStream,
                    streamEncoding,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 4096,
                    leaveOpen: false);
                standardErrorStream = null;

                process = Process.GetProcessById(checked((int)processInformation.ProcessId));
                _ = process.Handle;
                processJob = await CopilotWindowsProcessJob.AssignRequiredAsync(
                        process,
                        tryAssignProcessJob)
                    .ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                var previousSuspendCount = ResumeThread(nativeThread);
                if (previousSuspendCount == uint.MaxValue)
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "The contained process primary thread could not be resumed.");
                }
                if (previousSuspendCount != 1)
                {
                    throw new InvalidOperationException(
                        $"The contained process had an unexpected primary-thread suspend count of {previousSuspendCount}.");
                }

                var launchedProcess = new CopilotSuspendedProcessLauncher(
                    process,
                    standardOutput,
                    standardError,
                    processJob);
                process = null;
                standardOutput = null;
                standardError = null;
                processJob = null;
                ownershipTransferred = true;
                return launchedProcess;
            }
            catch
            {
                if (!ownershipTransferred)
                {
                    _ = processJob?.TryTerminate();
                    if (nativeProcess is { IsInvalid: false, IsClosed: false })
                        _ = TerminateProcess(nativeProcess, 1);
                }
                throw;
            }
            finally
            {
                if (attributeListInitialized)
                    DeleteProcThreadAttributeList(attributeList);
                if (attributeList != IntPtr.Zero)
                    Marshal.FreeHGlobal(attributeList);
                if (inheritedHandles != IntPtr.Zero)
                    Marshal.FreeHGlobal(inheritedHandles);
                if (pinnedEnvironment.IsAllocated)
                    pinnedEnvironment.Free();
                if (pinnedCommandLine.IsAllocated)
                    pinnedCommandLine.Free();

                childStandardInput?.Dispose();
                parentStandardInput?.Dispose();
                parentStandardOutput?.Dispose();
                childStandardOutput?.Dispose();
                parentStandardError?.Dispose();
                childStandardError?.Dispose();
                standardOutputStream?.Dispose();
                standardErrorStream?.Dispose();
                standardOutput?.Dispose();
                standardError?.Dispose();
                processJob?.Dispose();
                process?.Dispose();
                nativeThread?.Dispose();
                nativeProcess?.Dispose();
            }
        }

        internal static string BuildCommandLine(
            string executablePath,
            IReadOnlyList<string> arguments)
        {
            if (!TryBuildCommandLine(executablePath, arguments, out var commandLine))
            {
                throw new ArgumentException(
                    $"The Windows process command line cannot exceed {MaximumCommandLineCharacters} characters.",
                    nameof(arguments));
            }
            return commandLine;
        }

        internal static bool TryBuildCommandLine(
            string executablePath,
            IReadOnlyList<string> arguments,
            out string commandLine)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
            ArgumentNullException.ThrowIfNull(arguments);
            if (executablePath.Contains('\0'))
                throw new ArgumentException("The executable path cannot contain null characters.", nameof(executablePath));

            var builder = new StringBuilder();
            AppendArgument(builder, executablePath);
            foreach (var argument in arguments)
            {
                ArgumentNullException.ThrowIfNull(argument);
                if (argument.Contains('\0'))
                    throw new ArgumentException("Process arguments cannot contain null characters.", nameof(arguments));
                AppendArgument(builder, argument);
            }
            commandLine = builder.ToString();
            return commandLine.Length <= MaximumCommandLineCharacters;
        }

        private static void AppendArgument(StringBuilder commandLine, string argument)
        {
            if (commandLine.Length > 0)
                commandLine.Append(' ');

            var needsQuotes = argument.Length == 0;
            for (var index = 0; !needsQuotes && index < argument.Length; index++)
                needsQuotes = char.IsWhiteSpace(argument[index]) || argument[index] == '"';
            if (!needsQuotes)
            {
                commandLine.Append(argument);
                return;
            }

            commandLine.Append('"');
            var pendingBackslashes = 0;
            foreach (var character in argument)
            {
                if (character == '\\')
                {
                    pendingBackslashes++;
                    continue;
                }

                if (character == '"')
                {
                    commandLine.Append('\\', checked(pendingBackslashes * 2 + 1));
                    commandLine.Append('"');
                }
                else
                {
                    commandLine.Append('\\', pendingBackslashes);
                    commandLine.Append(character);
                }
                pendingBackslashes = 0;
            }

            commandLine.Append('\\', checked(pendingBackslashes * 2));
            commandLine.Append('"');
        }

        private static char[] BuildEnvironmentBlock(
            IReadOnlyDictionary<string, string> environmentVariables)
        {
            var sortedEnvironment = new SortedDictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var pair in environmentVariables)
            {
                ValidateEnvironmentVariable(pair.Key, pair.Value);
                sortedEnvironment[pair.Key] = pair.Value;
            }

            var block = new StringBuilder();
            foreach (var pair in sortedEnvironment)
            {
                block.Append(pair.Key);
                block.Append('=');
                block.Append(pair.Value);
                block.Append('\0');
            }
            block.Append('\0');
            if (sortedEnvironment.Count == 0)
                block.Append('\0');
            return block.ToString().ToCharArray();
        }

        private static void ValidateEnvironmentVariable(string name, string value)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(value);
            if (name.Length == 0
                || name.Contains('\0')
                || name.AsSpan(name[0] == '=' ? 1 : 0).Contains('='))
            {
                throw new ArgumentException(
                    "Environment-variable names cannot be empty or contain null characters or embedded equals signs.",
                    nameof(name));
            }
            if (value.Contains('\0'))
            {
                throw new ArgumentException(
                    "Environment-variable values cannot contain null characters.",
                    nameof(value));
            }
        }

        private static void CreateAnonymousPipe(
            out SafeFileHandle readHandle,
            out SafeFileHandle writeHandle)
        {
            var securityAttributes = new SecurityAttributes
            {
                Length = Marshal.SizeOf<SecurityAttributes>(),
                InheritHandle = true,
            };
            if (!CreatePipe(
                out readHandle,
                out writeHandle,
                ref securityAttributes,
                0))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "An anonymous process pipe could not be created.");
            }
        }

        private static void SetParentPipeEndNonInheritable(SafeFileHandle handle)
        {
            if (!SetHandleInformation(handle, HandleFlagInherit, 0))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "A parent process-pipe handle could not be marked non-inheritable.");
            }
        }

        public void Dispose()
        {
            var processJob = Interlocked.Exchange(ref _processJob, null);
            var standardOutput = Interlocked.Exchange(ref _standardOutput, null);
            var standardError = Interlocked.Exchange(ref _standardError, null);
            var process = Interlocked.Exchange(ref _process, null);

            processJob?.Dispose();
            standardOutput?.Dispose();
            standardError?.Dispose();
            process?.Dispose();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes
        {
            public int Length;
            public IntPtr SecurityDescriptor;
            [MarshalAs(UnmanagedType.Bool)]
            public bool InheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            public int Size;
            public IntPtr Reserved;
            public IntPtr Desktop;
            public IntPtr Title;
            public uint X;
            public uint Y;
            public uint XSize;
            public uint YSize;
            public uint XCountChars;
            public uint YCountChars;
            public uint FillAttribute;
            public uint Flags;
            public ushort ShowWindow;
            public ushort Reserved2Size;
            public IntPtr Reserved2;
            public IntPtr StandardInput;
            public IntPtr StandardOutput;
            public IntPtr StandardError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StartupInfoEx
        {
            public StartupInfo StartupInfo;
            public IntPtr AttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr Process;
            public IntPtr Thread;
            public uint ProcessId;
            public uint ThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreatePipe(
            out SafeFileHandle readPipe,
            out SafeFileHandle writePipe,
            ref SecurityAttributes pipeAttributes,
            uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetHandleInformation(
            SafeFileHandle handle,
            uint mask,
            uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InitializeProcThreadAttributeList(
            IntPtr attributeList,
            int attributeCount,
            int flags,
            ref IntPtr size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateProcThreadAttribute(
            IntPtr attributeList,
            uint flags,
            IntPtr attribute,
            IntPtr value,
            IntPtr size,
            IntPtr previousValue,
            IntPtr returnSize);

        [DllImport("kernel32.dll")]
        private static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

        [DllImport(
            "kernel32.dll",
            EntryPoint = "CreateProcessW",
            ExactSpelling = true,
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcess(
            string applicationName,
            IntPtr commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string currentDirectory,
            ref StartupInfoEx startupInfo,
            out ProcessInformation processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(SafeFileHandle thread);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(SafeFileHandle process, uint exitCode);
    }
}
