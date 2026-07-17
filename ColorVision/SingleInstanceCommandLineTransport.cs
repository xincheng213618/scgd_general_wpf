using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ColorVision
{
    internal static class SingleInstanceCommandLineTransport
    {
        internal const int MessageId = 0x004A; // WM_COPYDATA
        private const long PayloadIdentifier = 0x43564152; // CVAR
        private const uint AbortIfHung = 0x0002;
        private const uint SendTimeoutMilliseconds = 5000;
        private const int MaximumPayloadBytes = 1024 * 1024;

        [StructLayout(LayoutKind.Sequential)]
        private struct CopyDataMessage
        {
            public IntPtr Identifier;
            public int ByteCount;
            public IntPtr Data;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr windowHandle,
            uint message,
            IntPtr wordParameter,
            IntPtr longParameter,
            uint flags,
            uint timeout,
            out UIntPtr result);

        public static bool TrySend(IntPtr windowHandle, IReadOnlyList<string> arguments)
        {
            if (windowHandle == IntPtr.Zero)
                return false;
            ArgumentNullException.ThrowIfNull(arguments);

            string payload = SerializeArguments(arguments);
            int byteCount = checked((payload.Length + 1) * sizeof(char));
            if (byteCount > MaximumPayloadBytes)
                return false;

            IntPtr payloadPointer = IntPtr.Zero;
            IntPtr messagePointer = IntPtr.Zero;
            try
            {
                payloadPointer = Marshal.StringToHGlobalUni(payload);
                var copyData = new CopyDataMessage
                {
                    Identifier = new IntPtr(PayloadIdentifier),
                    ByteCount = byteCount,
                    Data = payloadPointer,
                };
                messagePointer = Marshal.AllocHGlobal(Marshal.SizeOf<CopyDataMessage>());
                Marshal.StructureToPtr(copyData, messagePointer, fDeleteOld: false);

                return SendMessageTimeout(
                    windowHandle,
                    MessageId,
                    IntPtr.Zero,
                    messagePointer,
                    AbortIfHung,
                    SendTimeoutMilliseconds,
                    out _) != IntPtr.Zero;
            }
            finally
            {
                if (messagePointer != IntPtr.Zero)
                    Marshal.FreeHGlobal(messagePointer);
                if (payloadPointer != IntPtr.Zero)
                    Marshal.FreeHGlobal(payloadPointer);
            }
        }

        public static bool TryReceive(IntPtr messagePointer, out string[] arguments)
        {
            arguments = Array.Empty<string>();
            if (messagePointer == IntPtr.Zero)
                return false;

            try
            {
                CopyDataMessage message = Marshal.PtrToStructure<CopyDataMessage>(messagePointer);
                if (message.Identifier.ToInt64() != PayloadIdentifier
                    || message.Data == IntPtr.Zero
                    || message.ByteCount < sizeof(char)
                    || message.ByteCount > MaximumPayloadBytes
                    || message.ByteCount % sizeof(char) != 0)
                {
                    return false;
                }

                int characterCount = message.ByteCount / sizeof(char) - 1;
                string? payload = Marshal.PtrToStringUni(message.Data, characterCount);
                return TryDeserializeArguments(payload, out arguments);
            }
            catch (Exception ex) when (ex is ArgumentException
                or JsonException
                or OverflowException)
            {
                return false;
            }
        }

        internal static string SerializeArguments(IReadOnlyList<string> arguments)
        {
            return JsonSerializer.Serialize(arguments);
        }

        internal static bool TryDeserializeArguments(string? payload, out string[] arguments)
        {
            arguments = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(payload))
                return false;

            try
            {
                string[]? parsed = JsonSerializer.Deserialize<string[]>(payload);
                if (parsed == null)
                    return false;
                arguments = parsed;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
