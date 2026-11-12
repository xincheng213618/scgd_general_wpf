using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVisionUpdateRelay
{
    /// <summary>
    /// Edits only the lines this tool owns in the Windows hosts file. Conflicting manual mappings of the
    /// update host are commented out with a recognisable prefix and restored on removal.
    /// </summary>
    public static class HostsEditor
    {
        public const string Marker = "# ColorVision update relay";
        public const string DisabledPrefix = "#[ColorVision relay disabled] ";

        public static string HostsPath => Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts");

        /// <summary>Returns the relay address this tool wrote for <paramref name="host"/>, or null.</summary>
        public static string FindRelay(IEnumerable<string> lines, string host)
        {
            foreach (string line in lines)
            {
                if (!IsOwnedLine(line, host)) continue;
                return Tokens(line)[0];
            }
            return null;
        }

        public static List<string> Apply(IEnumerable<string> lines, string host, string relayAddress)
        {
            var result = new List<string>();
            foreach (string line in Remove(lines, host))
                result.Add(MapsHost(line, host) ? DisabledPrefix + line : line);
            while (result.Count > 0 && result[result.Count - 1].Trim().Length == 0) result.RemoveAt(result.Count - 1);
            result.Add(relayAddress + "\t" + host + "\t" + Marker);
            return result;
        }

        public static List<string> Remove(IEnumerable<string> lines, string host)
        {
            var result = new List<string>();
            foreach (string line in lines)
            {
                if (IsOwnedLine(line, host)) continue;
                if (line.StartsWith(DisabledPrefix, StringComparison.Ordinal))
                {
                    string original = line.Substring(DisabledPrefix.Length);
                    if (MapsHost(original, host)) { result.Add(original); continue; }
                }
                result.Add(line);
            }
            return result;
        }

        /// <summary>True when an active (uncommented) hosts line maps <paramref name="host"/>.</summary>
        public static bool MapsHost(string line, string host)
        {
            string[] tokens = Tokens(line);
            return tokens.Length >= 2 && tokens.Skip(1).Any(t => string.Equals(t, host, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsOwnedLine(string line, string host) => line.TrimEnd().EndsWith(Marker, StringComparison.Ordinal) && MapsHost(line, host);

        private static string[] Tokens(string line)
        {
            int comment = line.IndexOf('#');
            string active = comment >= 0 ? line.Substring(0, comment) : line;
            return active.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static List<string> Read(string path, out Encoding encoding)
        {
            byte[] bytes = File.Exists(path) ? File.ReadAllBytes(path) : new byte[0];
            string text;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                encoding = new UTF8Encoding(true);
                text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }
            else
            {
                try
                {
                    text = new UTF8Encoding(false, true).GetString(bytes);
                    encoding = new UTF8Encoding(false);
                }
                catch (DecoderFallbackException)
                {
                    encoding = Encoding.Default;
                    text = encoding.GetString(bytes);
                }
            }
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            if (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
            return lines;
        }

        /// <summary>Backs up the current file next to it, then rewrites it, keeping its encoding and read-only flag.</summary>
        public static string Write(string path, IEnumerable<string> lines, Encoding encoding)
        {
            string backup = path + ".colorvision-relay.bak";
            FileAttributes attributes = File.Exists(path) ? File.GetAttributes(path) : FileAttributes.Normal;
            if (File.Exists(path))
            {
                // hosts is often read-only and the copy inherits that, which would block the next backup.
                if (File.Exists(backup)) File.SetAttributes(backup, FileAttributes.Normal);
                File.Copy(path, backup, true);
                File.SetAttributes(backup, FileAttributes.Normal);
            }
            bool readOnly = (attributes & FileAttributes.ReadOnly) != 0;
            if (readOnly) File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            try
            {
                File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n", encoding);
            }
            finally
            {
                if (readOnly) File.SetAttributes(path, attributes);
            }
            return backup;
        }
    }
}
