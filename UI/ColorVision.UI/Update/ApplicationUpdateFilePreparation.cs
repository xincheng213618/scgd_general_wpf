using System.Text;

namespace ColorVision.Update;

/// <summary>Preserves DLLs still mapped by shell thumbnail hosts before the bulk update copies files.</summary>
public static class ApplicationUpdateFilePreparation
{
    public static void AppendToBatch(StringBuilder builder)
    {
        string command = Convert.ToBase64String(Encoding.Unicode.GetBytes(Script));
        builder.AppendLine("set \"UPDATE_DLL_SCRIPT=%UPDATE_ROOT%\\prepare-dlls.b64\"");
        for (int offset = 0; offset < command.Length; offset += 2000)
        {
            string redirect = offset == 0 ? ">" : ">>";
            builder.AppendLine($"{redirect}\"%UPDATE_DLL_SCRIPT%\" echo {command.Substring(offset, Math.Min(2000, command.Length - offset))}");
        }
        builder.AppendLine("powershell.exe -NoLogo -NoProfile -NonInteractive -Command \"& ([scriptblock]::Create([Text.Encoding]::Unicode.GetString([Convert]::FromBase64String([IO.File]::ReadAllText($env:UPDATE_DLL_SCRIPT)))))\" >>\"%UPDATE_LOG%\" 2>&1");
        builder.AppendLine("if errorlevel 1 goto fail");
    }

    // Use environment variables for paths, never interpolate them into executable PowerShell text.
    // A loaded assembly can deny writes while allowing rename. Its existing readers keep the old file.
    private const string Script = """
        $ErrorActionPreference = 'Stop'
        function Get-ContentHash([string]$path) {
            $stream = [IO.File]::OpenRead($path)
            $hash = [Security.Cryptography.SHA256]::Create()
            try { return [Convert]::ToBase64String($hash.ComputeHash($stream)) }
            finally { $hash.Dispose(); $stream.Dispose() }
        }
        function Release-ThumbnailHosts([string]$lockedFile, [string]$applicationDirectory) {
            foreach ($hostProcess in [Diagnostics.Process]::GetProcessesByName('dllhost')) {
                try {
                    $started = $hostProcess.StartTime
                    $systemHost = [IO.Path]::Combine($env:WINDIR, 'System32\dllhost.exe')
                    if (![string]::Equals($hostProcess.MainModule.FileName, $systemHost, [StringComparison]::OrdinalIgnoreCase)) { continue }
                    $ownsExtension = $false
                    $ownsLockedFile = $false
                    foreach ($module in $hostProcess.Modules) {
                        if ([string]::Equals($module.FileName, $lockedFile, [StringComparison]::OrdinalIgnoreCase)) { $ownsLockedFile = $true }
                        if ($module.ModuleName -like 'ColorVision.ShellExtension*' -and $module.FileName.StartsWith($applicationDirectory, [StringComparison]::OrdinalIgnoreCase)) { $ownsExtension = $true }
                    }
                    if (!$ownsExtension -or !$ownsLockedFile) { continue }
                    $current = [Diagnostics.Process]::GetProcessById($hostProcess.Id)
                    try {
                        if ($current.StartTime -ne $started) { continue }
                        $current.Kill()
                        if (!$current.WaitForExit(5000)) { throw 'Thumbnail host did not exit.' }
                        Write-Output ('Released ColorVision thumbnail host ' + $hostProcess.Id)
                    } finally { $current.Dispose() }
                } catch { Write-Output ('Could not release thumbnail host: ' + $_.Exception.Message) }
                finally { $hostProcess.Dispose() }
            }
        }
        try {
            $stage = [IO.Path]::GetFullPath($env:STAGE).TrimEnd('\') + '\'
            $target = [IO.Path]::GetFullPath($env:TARGET).TrimEnd('\') + '\'
            if (![IO.Directory]::Exists($stage) -or ![IO.Directory]::Exists($target)) {
                throw 'The update stage or application directory does not exist.'
            }
            foreach ($source in [IO.Directory]::EnumerateFiles($stage, '*.dll', [IO.SearchOption]::AllDirectories)) {
                if ([IO.Path]::GetFileName($source) -like 'ColorVision.ShellExtension*') { continue }
                $relative = $source.Substring($stage.Length)
                $destination = [IO.Path]::GetFullPath([IO.Path]::Combine($target, $relative))
                if (!$destination.StartsWith($target, [StringComparison]::OrdinalIgnoreCase)) { throw 'Update path escapes the application directory.' }
                if (![IO.File]::Exists($destination)) { continue }
                try {
                    $probe = [IO.File]::Open($destination, [IO.FileMode]::Open, [IO.FileAccess]::Write, [IO.FileShare]::Read)
                    $probe.Dispose()
                    continue
                } catch [IO.IOException] {
                    if (($_.Exception.HResult -band 65535) -ne 32) { throw }
                }
                if ((Get-ContentHash $source) -eq (Get-ContentHash $destination)) {
                    # Robocopy /IS also overwrites identical files. Omit this verified duplicate from its source.
                    [IO.File]::Delete($source)
                    Write-Output ('Kept identical DLL held by another process: ' + $relative)
                    continue
                }
                $suffix = '.cv-update-' + [Guid]::NewGuid().ToString('N')
                $temporary = $destination + $suffix + '.new'
                $previous = $destination + $suffix + '.previous'
                $renamed = $false
                try {
                    [IO.File]::Copy($source, $temporary, $false)
                    try { [IO.File]::Move($destination, $previous) }
                    catch [IO.IOException] {
                        if (($_.Exception.HResult -band 65535) -ne 32) { throw }
                        Release-ThumbnailHosts $destination $target
                        [IO.File]::Move($destination, $previous)
                    }
                    $renamed = $true
                    [IO.File]::Move($temporary, $destination)
                    Write-Output ('Replaced DLL held by another process: ' + $relative)
                } catch {
                    if ($renamed -and ![IO.File]::Exists($destination)) { [IO.File]::Move($previous, $destination) }
                    throw
                } finally {
                    if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
                }
                try { [IO.File]::Delete($previous) }
                catch { Write-Output ('Previous DLL remains in use; retained at ' + $previous) }
            }
            exit 0
        } catch {
            Write-Output ('Application DLL preparation failed: ' + $_.Exception.Message)
            exit 1
        }
        """;
}
