using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates
{
    public static class SensorCommandTextFormatter
    {
        private static readonly char[] HexSeparators = { ' ', '\r', '\n', '\t', ',', ';', '-' };
        private static readonly Dictionary<string, byte> ControlNameToByte = new(StringComparer.OrdinalIgnoreCase)
        {
            ["NUL"] = 0x00,
            ["SOH"] = 0x01,
            ["STX"] = 0x02,
            ["STARTOFTEXT"] = 0x02,
            ["ETX"] = 0x03,
            ["ENDOFTEXT"] = 0x03,
            ["EOT"] = 0x04,
            ["ENQ"] = 0x05,
            ["ACK"] = 0x06,
            ["BEL"] = 0x07,
            ["BS"] = 0x08,
            ["HT"] = 0x09,
            ["TAB"] = 0x09,
            ["LF"] = 0x0A,
            ["NL"] = 0x0A,
            ["VT"] = 0x0B,
            ["FORMFEED"] = 0x0C,
            ["CR"] = 0x0D,
            ["SO"] = 0x0E,
            ["SI"] = 0x0F,
            ["DLE"] = 0x10,
            ["DC1"] = 0x11,
            ["DC2"] = 0x12,
            ["DC3"] = 0x13,
            ["DC4"] = 0x14,
            ["NAK"] = 0x15,
            ["SYN"] = 0x16,
            ["ETB"] = 0x17,
            ["CAN"] = 0x18,
            ["EM"] = 0x19,
            ["SUB"] = 0x1A,
            ["ESC"] = 0x1B,
            ["FS"] = 0x1C,
            ["GS"] = 0x1D,
            ["RS"] = 0x1E,
            ["US"] = 0x1F,
            ["DEL"] = 0x7F,
        };

        private static readonly Dictionary<byte, string> ControlByteToName = new()
        {
            [0x00] = "NUL",
            [0x01] = "SOH",
            [0x02] = "STX",
            [0x03] = "ETX",
            [0x04] = "EOT",
            [0x05] = "ENQ",
            [0x06] = "ACK",
            [0x07] = "BEL",
            [0x08] = "BS",
            [0x09] = "TAB",
            [0x0A] = "LF",
            [0x0B] = "VT",
            [0x0C] = "FORMFEED",
            [0x0D] = "CR",
            [0x0E] = "SO",
            [0x0F] = "SI",
            [0x10] = "DLE",
            [0x11] = "DC1",
            [0x12] = "DC2",
            [0x13] = "DC3",
            [0x14] = "DC4",
            [0x15] = "NAK",
            [0x16] = "SYN",
            [0x17] = "ETB",
            [0x18] = "CAN",
            [0x19] = "EM",
            [0x1A] = "SUB",
            [0x1B] = "ESC",
            [0x1C] = "FS",
            [0x1D] = "GS",
            [0x1E] = "RS",
            [0x1F] = "US",
            [0x7F] = "DEL",
        };

        public static string BracketTextToHex(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            List<byte> bytes = new();
            StringBuilder textBuffer = new();

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '[')
                {
                    int end = text.IndexOf(']', i + 1);
                    if (end > i + 1)
                    {
                        string token = text.Substring(i + 1, end - i - 1).Trim();
                        if (TryParseBracketByte(token, out byte value))
                        {
                            FlushTextBuffer(textBuffer, bytes);
                            bytes.Add(value);
                            i = end;
                            continue;
                        }
                    }
                }

                textBuffer.Append(text[i]);
            }

            FlushTextBuffer(textBuffer, bytes);
            return BytesToHex(bytes);
        }

        public static bool TryHexToBracketText(string? hexText, out string bracketText)
        {
            return TryHexToBracketText(hexText, useControlNames: false, out bracketText);
        }

        public static bool TryHexToBracketText(string? hexText, bool useControlNames, out string bracketText)
        {
            bracketText = string.Empty;
            if (!TryParseHexText(hexText, out List<byte> bytes))
            {
                return false;
            }

            StringBuilder builder = new();
            foreach (byte value in bytes)
            {
                if (value >= 0x20 && value <= 0x7E)
                {
                    builder.Append((char)value);
                }
                else
                {
                    string token = useControlNames && ControlByteToName.TryGetValue(value, out string? name)
                        ? name
                        : value.ToString("X2", CultureInfo.InvariantCulture);
                    builder.Append('[').Append(token).Append(']');
                }
            }

            bracketText = builder.ToString();
            return true;
        }

        public static string ToBracketTextOrOriginal(string? hexText, bool useControlNames = false)
        {
            return TryHexToBracketText(hexText, useControlNames, out string bracketText) ? bracketText : hexText ?? string.Empty;
        }

        public static string NormalizeHex(string? hexText)
        {
            return TryParseHexText(hexText, out List<byte> bytes) ? BytesToHex(bytes) : hexText?.Trim() ?? string.Empty;
        }

        private static void FlushTextBuffer(StringBuilder textBuffer, List<byte> bytes)
        {
            if (textBuffer.Length == 0)
            {
                return;
            }

            bytes.AddRange(Encoding.UTF8.GetBytes(textBuffer.ToString()));
            textBuffer.Clear();
        }

        private static bool TryParseHexText(string? hexText, out List<byte> bytes)
        {
            bytes = new List<byte>();
            if (string.IsNullOrWhiteSpace(hexText))
            {
                return true;
            }

            string normalized = hexText.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
            string[] tokens = normalized.Split(HexSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return true;
            }

            foreach (string token in tokens)
            {
                if (token.Length <= 2)
                {
                    if (!TryParseHexByte(token, out byte value))
                    {
                        return false;
                    }

                    bytes.Add(value);
                    continue;
                }

                if (token.Length % 2 != 0)
                {
                    return false;
                }

                for (int i = 0; i < token.Length; i += 2)
                {
                    if (!TryParseHexByte(token.Substring(i, 2), out byte value))
                    {
                        return false;
                    }

                    bytes.Add(value);
                }
            }

            return true;
        }

        private static bool TryParseHexByte(string token, out byte value)
        {
            value = 0;
            return token.Length is 1 or 2 && byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseBracketByte(string token, out byte value)
        {
            string normalized = NormalizeControlToken(token);
            if (normalized.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[2..];
            }

            return TryParseHexByte(normalized, out value) || ControlNameToByte.TryGetValue(normalized, out value);
        }

        private static string NormalizeControlToken(string token)
        {
            return token.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
        }

        private static string BytesToHex(IEnumerable<byte> bytes)
        {
            StringBuilder builder = new();
            foreach (byte value in bytes)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
