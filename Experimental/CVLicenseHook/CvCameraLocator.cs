using System.Buffers.Binary;

namespace ColorVision.Experimental.CVLicenseHook;

/// <summary>只读 PE 指纹，不加载 DLL、不执行任何代码。</summary>
public sealed record CvCameraFingerprint(
    string Path,
    long SizeBytes,
    string Architecture,
    DateTime BuildTimestampUtc,
    int NamedExportCount,
    uint ValiLicRva,
    uint OpenRva,
    bool HasValiLic,
    bool HasOpen,
    string EntryPrefixHex,
    bool EntryPrefixMatches,
    bool HasAuthenticodeSignature,
    bool AslrEnabled,
    bool DepEnabled,
    bool CfgEnabled);

/// <summary>
/// 「实验功能」里的定位步骤：在候选目录中找到 cvCamera.dll，并确认它是不是
/// 我们补丁所针对的那个版本。源码与部署二进制版本对不上时，这一步是防呆关键。
/// </summary>
internal static class CvCameraLocator
{
    private const int Pe32PlusMagic = 0x20B;

    internal static CvCameraFingerprint Inspect(string path)
    {
        byte[] d = File.ReadAllBytes(path);
        if (d.Length < 0x100 || d[0] != 'M' || d[1] != 'Z')
            throw new BadImageFormatException("不是有效 PE 文件");

        int pe = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(0x3C));
        ushort machine = U16(d, pe + 4);
        ushort sectionCount = U16(d, pe + 6);
        uint timestamp = U32(d, pe + 8);
        ushort optionalSize = U16(d, pe + 20);
        int opt = pe + 24;
        ushort magic = U16(d, opt);
        bool pe32Plus = magic == Pe32PlusMagic;

        int directories = opt + (pe32Plus ? 112 : 96);
        uint exportRva = U32(d, directories);
        uint securityRva = U32(d, directories + 4 * 8);   // IMAGE_DIRECTORY_ENTRY_SECURITY = 4
        ushort dllChars = U16(d, opt + 70);

        var sections = new List<(uint Va, uint VirtualSize, uint RawSize, uint RawPtr)>();
        int sectionTable = opt + optionalSize;
        for (int i = 0; i < sectionCount; i++)
        {
            int s = sectionTable + i * 40;
            sections.Add((U32(d, s + 12), U32(d, s + 8), U32(d, s + 16), U32(d, s + 20)));
        }

        int RvaToOffset(uint rva)
        {
            foreach (var sec in sections)
            {
                uint span = Math.Max(sec.VirtualSize, sec.RawSize);
                if (rva >= sec.Va && rva < sec.Va + span)
                    return checked((int)(sec.RawPtr + (rva - sec.Va)));
            }
            return -1;
        }

        int namedExports = 0;
        uint valiRva = 0, openRva = 0;
        if (exportRva != 0)
        {
            int eo = RvaToOffset(exportRva);
            if (eo >= 0)
            {
                uint functions = U32(d, eo + 20);
                uint names = U32(d, eo + 24);
                uint addressOfFunctions = U32(d, eo + 28);
                uint addressOfNames = U32(d, eo + 32);
                uint addressOfOrdinals = U32(d, eo + 36);
                namedExports = checked((int)names);

                for (uint i = 0; i < names; i++)
                {
                    int nameEntry = RvaToOffset(U32(d, RvaToOffset(addressOfNames) + (int)i * 4));
                    if (nameEntry < 0) continue;
                    int end = Array.IndexOf(d, (byte)0, nameEntry);
                    if (end < 0) continue;
                    string name = System.Text.Encoding.ASCII.GetString(d, nameEntry, end - nameEntry);

                    if (name is "CM_ValiLic" or "CM_Open")
                    {
                        int ordOffset = RvaToOffset(addressOfOrdinals) + (int)i * 2;
                        ushort ordinal = U16(d, ordOffset);
                        uint rva = U32(d, RvaToOffset(addressOfFunctions) + ordinal * 4);
                        if (name == "CM_ValiLic") valiRva = rva; else openRva = rva;
                    }
                }
                _ = functions;
            }
        }

        string prefix = "";
        int entryOffset = valiRva != 0 ? RvaToOffset(valiRva) : -1;
        if (entryOffset >= 0 && entryOffset + InlineHook.KnownPrefix.Length <= d.Length)
            prefix = Convert.ToHexString(d, entryOffset, InlineHook.KnownPrefix.Length);

        return new CvCameraFingerprint(
            Path: path,
            SizeBytes: d.Length,
            Architecture: machine == 0x8664 ? "x64" : machine == 0x14C ? "x86" : $"0x{machine:X4}",
            BuildTimestampUtc: DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime,
            NamedExportCount: namedExports,
            ValiLicRva: valiRva,
            OpenRva: openRva,
            HasValiLic: valiRva != 0,
            HasOpen: openRva != 0,
            EntryPrefixHex: prefix,
            EntryPrefixMatches: prefix == Convert.ToHexString(InlineHook.KnownPrefix),
            HasAuthenticodeSignature: securityRva != 0,
            AslrEnabled: (dllChars & 0x0040) != 0,
            DepEnabled: (dllChars & 0x0100) != 0,
            CfgEnabled: (dllChars & 0x4000) != 0);
    }

    private static ushort U16(byte[] d, int o) => BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(o));
    private static uint U32(byte[] d, int o) => BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o));

    /// <summary>从若干候选目录中挑出可安全安装 hook 的那一份。</summary>
    internal static string? FindHookable(IEnumerable<string> candidates, out string reason)
    {
        foreach (string path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                var f = Inspect(path);
                if (f.Architecture == "x64" && f.HasValiLic && f.EntryPrefixMatches)
                {
                    reason = $"匹配：{path}（导出 {f.NamedExportCount} 个，构建 {f.BuildTimestampUtc:yyyy-MM-dd}）";
                    return path;
                }
            }
            catch (BadImageFormatException) { /* 跳过非 PE */ }
        }
        reason = "没有找到「x64 + 导出 CM_ValiLic + 入口特征匹配」的 cvCamera.dll；拒绝安装。";
        return null;
    }
}
