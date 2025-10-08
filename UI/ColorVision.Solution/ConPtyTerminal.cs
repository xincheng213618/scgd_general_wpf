using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using static ColorVision.Solution.NativeMethods.ConPtyNativeMethods;

namespace ColorVision.Solution
{
    /// <summary>
    /// ConPTY (Pseudo Console) wrapper for terminal emulation
    /// Provides VSCode-like terminal experience with full ANSI/VT100 support
    /// </summary>
    public class ConPtyTerminal : IDisposable
    {
        private IntPtr _hPC;
        private SafeFileHandle? _consoleInputPipeWriteHandle;
        private SafeFileHandle? _consoleOutputPipeReadHandle;
        private StreamWriter? _inputWriter;
        private StreamReader? _outputReader;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _disposed;

        public event EventHandler<string>? OutputReceived;

        /// <summary>
        /// Starts a new pseudo console with the specified size
        /// </summary>
        /// <param name="cols">Number of columns</param>
        /// <param name="rows">Number of rows</param>
        /// <param name="command">Command to execute (default: cmd.exe)</param>
        public void Start(short cols = 80, short rows = 25, string command = "cmd.exe")
        {
            if (_hPC != IntPtr.Zero)
            {
                throw new InvalidOperationException("Terminal already started");
            }

            _cancellationTokenSource = new CancellationTokenSource();

            // Create pipes for ConPTY
            CreatePipe(out SafeFileHandle inputPipeRead, out _consoleInputPipeWriteHandle, IntPtr.Zero, 0);
            CreatePipe(out _consoleOutputPipeReadHandle, out SafeFileHandle outputPipeWrite, IntPtr.Zero, 0);

            // Create pseudo console
            var size = new COORD(cols, rows);
            int hr = CreatePseudoConsole(size, inputPipeRead, outputPipeWrite, 0, out _hPC);
            
            if (hr != 0)
            {
                throw new InvalidOperationException($"Failed to create pseudo console. Error code: {hr}");
            }

            // Clean up pipe handles we don't need
            inputPipeRead.Dispose();
            outputPipeWrite.Dispose();


            // Start the process attached to the pseudo console
            StartProcess(command);

            // Set up input/output streams
            _inputWriter = new StreamWriter(new FileStream(_consoleInputPipeWriteHandle, FileAccess.Write), Encoding.UTF8)
            {
                AutoFlush = true
            };

            _outputReader = new StreamReader(new FileStream(_consoleOutputPipeReadHandle, FileAccess.Read), Encoding.UTF8);

            // Start reading output
            Task.Run(() => ReadOutput(_cancellationTokenSource.Token));
        }

        private void StartProcess(string command)
        {


            // Create and properly initialize the STARTUPINFOEX structure
            var startupInfo = new STARTUPINFOEX();
            startupInfo.StartupInfo = new STARTUPINFO(); // Ensure STARTUPINFO is also initialized
            startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();

            // Set up attribute list
            IntPtr lpSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);

            startupInfo.lpAttributeList = Marshal.AllocHGlobal(lpSize);

            if (!InitializeProcThreadAttributeList(startupInfo.lpAttributeList, 1, 0, ref lpSize))
            {
                throw new InvalidOperationException("Failed to initialize proc thread attribute list");
            }

            // Attach pseudo console to process
            IntPtr hPCPtr = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(hPCPtr, _hPC);

            if (!UpdateProcThreadAttribute(
                startupInfo.lpAttributeList,
                0,
                (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                hPCPtr,
                (IntPtr)IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero))
            {
                throw new InvalidOperationException("Failed to update proc thread attribute");
            }

           // Create the process
            const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
            const int CREATE_NO_WINDOW = 0x08000000;
            const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
            // Create a copy of the command string in writable memory
            // This is important because CreateProcess may modify the string
            StringBuilder cmdBuilder = new StringBuilder(command);

            // Create the process
            bool success = CreateProcess(
                null,                               // No module name (use command line)
                cmdBuilder,                         // Command line - using StringBuilder instead of string
                IntPtr.Zero,                        // Process security attributes
                IntPtr.Zero,                        // Thread security attributes
                false,                              // Don't inherit handles
                EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT,  // Creation flags
                IntPtr.Zero,                        // Use parent's environment block
                null,                               // Use parent's starting directory
                ref startupInfo,        // Pointer to STARTUPINFO
                out PROCESS_INFORMATION processInfo); // Receives process information

            // Clean up
            Marshal.FreeHGlobal(hPCPtr);
            DeleteProcThreadAttributeList(startupInfo.lpAttributeList);
            Marshal.FreeHGlobal(startupInfo.lpAttributeList);

            if (!success)
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Failed to create process. Error: 0x{errorCode:X} ({errorCode})");
            }

            // Close process and thread handles (we don't need them)
            CloseHandle(processInfo.hProcess);
            CloseHandle(processInfo.hThread);
        }

        private async Task ReadOutput(CancellationToken cancellationToken)
        {
            if (_outputReader == null) return;
            
            try
            {
                char[] buffer = new char[4096];
                while (!cancellationToken.IsCancellationRequested)
                {
                    int bytesRead = await _outputReader.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string output = new string(buffer, 0, bytesRead);
                        OutputReceived?.Invoke(this, output);
                    }
                    else
                    {
                        // End of stream
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // Handle termination gracefully
            }
        }

        /// <summary>
        /// Sends input to the terminal
        /// </summary>
        public void SendInput(string input)
        {
            if (_inputWriter != null && !_disposed)
            {
                _inputWriter.Write(input);
            }
        }

        /// <summary>
        /// Resizes the terminal
        /// </summary>
        public void Resize(short cols, short rows)
        {
            if (_hPC != IntPtr.Zero)
            {
                var size = new COORD(cols, rows);
                ResizePseudoConsole(_hPC, size);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            _inputWriter?.Dispose();
            _outputReader?.Dispose();

            _consoleInputPipeWriteHandle?.Dispose();
            _consoleOutputPipeReadHandle?.Dispose();

            if (_hPC != IntPtr.Zero)
            {
                ClosePseudoConsole(_hPC);
                _hPC = IntPtr.Zero;
            }
        }
    }
}
