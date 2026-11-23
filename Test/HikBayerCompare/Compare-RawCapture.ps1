# Validate the two candidates against the SDK output recorded for this frame in the camera diagnostic window.
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position=0)][string]$CaptureZip,
    [string]$Program = (Join-Path $PSScriptRoot '../../artifacts/HikBayerCompare/HikBayerCompare.exe'),
    [string]$SdkDirectory,
    [string]$LogDirectory
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath = (Resolve-Path -LiteralPath $CaptureZip).ProviderPath
$exe = (Resolve-Path -LiteralPath $Program).ProviderPath
$rawPath = $null
try {
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $metadata = @($archive.Entries | Where-Object FullName -CEQ 'metadata.json')
        $rawEntry = @($archive.Entries | Where-Object FullName -CEQ 'frame.bayer16.raw')
        if ($metadata.Count -ne 1 -or $metadata[0].Length -gt 1MB -or $rawEntry.Count -ne 1) {
            throw 'Expected a Bayer capture ZIP with one metadata.json and one frame.bayer16.raw.'
        }
        $reader = [IO.StreamReader]::new($metadata[0].Open())
        try { $frame = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        $reference = $frame.SdkReference
        if ($frame.SchemaVersion -ne 2 -or $reference.Sha256 -notmatch '^[0-9a-fA-F]{64}$' -or
            $reference.PixelFormat -cne 'RGB16_Packed' -or $reference.ByteOrder -cne 'little-endian' -or
            $reference.ByteLength -ne (9568 * 6380 * 6)) {
            throw 'Missing or incompatible connected SDK RGB16 reference in capture metadata.'
        }
        $imageBytes = 9568 * 6380 * 2
        if ($frame.RawFile -cne 'frame.bayer16.raw' -or $frame.PixelFormatCode -ine '0x0110002F' -or
            $frame.BayerPattern -cne 'RGGB' -or $frame.BayerQuality -ne 3 -or $frame.Width -ne 9568 -or $frame.Height -ne 6380 -or
            $frame.ByteOrder -cne 'little-endian' -or $frame.HeaderBytes -ne 0 -or $frame.RowStrideBytes -ne (9568 * 2) -or
            $frame.PayloadBytes -ne $imageBytes -or $rawEntry[0].Length -ne $imageBytes) {
            throw 'Expected tightly packed, full-size 9568 x 6380, Q3 BayerRG16 input.'
        }
        $rawPath = Join-Path ([IO.Path]::GetTempPath()) ("HikBayerFrame_{0}_{1:N}.raw" -f [uint32]$frame.FrameId, [Guid]::NewGuid())
        [IO.Compression.ZipFileExtensions]::ExtractToFile($rawEntry[0], $rawPath)
    } finally { $archive.Dispose() }

    Write-Host ("FrameId={0} CameraSdkVersion={1} ExposureUs={2} ConnectedRgb16Sha256={3}" -f
        $frame.FrameId, $frame.SdkVersion, $frame.ExposureUs, $reference.Sha256)
    $arguments = @('--raw', $rawPath, '--reference-rgb-sha256', $reference.Sha256)
    if ($SdkDirectory) { $arguments += @('--sdk-dir', (Resolve-Path -LiteralPath $SdkDirectory).ProviderPath) }
    if ($LogDirectory) { $arguments += @('--log-dir', $LogDirectory) }
    & $exe @arguments
    $result = $LASTEXITCODE
} finally {
    if ($rawPath -and [IO.File]::Exists($rawPath)) { Remove-Item -LiteralPath $rawPath }
}
exit $result
