param(
    [Parameter(Mandatory = $true)]
    [string]$InputJsonPath
)

$ErrorActionPreference = 'Stop'
$data = Get-Content -LiteralPath $InputJsonPath -Raw | ConvertFrom-Json
$appPath = [string]$data.appPath
if ([string]::IsNullOrWhiteSpace($appPath) -or -not (Test-Path -LiteralPath $appPath)) {
    throw "ColorVision executable was not found: $appPath"
}
if ([IO.Path]::GetFileName($appPath) -ine 'ColorVision.exe') {
    throw "Unexpected executable name: $appPath"
}

$appDirectory = Split-Path -Parent $appPath
$iconPath = Join-Path $appDirectory 'ColorVisionIcons64.dll'
$comHostPath = Join-Path $appDirectory 'ColorVision.ShellExtension.comhost.dll'
$thumbnailProviderIid = '{E357FCCD-A995-4576-B01F-234630154E96}'
$cvRawThumbnailClsid = '{7B5E2A3C-8F1D-4E6A-B9C2-1D3E5F7A8B9C}'
$cvCieThumbnailClsid = '{8C6F3B4D-9E2A-5F7B-C3D4-2E4F6A8B9C0D}'

function Resolve-RegistryPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ($Path -match '^HKCR\\(.+)$') {
        return @{ Root = [Microsoft.Win32.Registry]::ClassesRoot; SubKey = $Matches[1] }
    }
    if ($Path -match '^HKLM\\(.+)$') {
        return @{ Root = [Microsoft.Win32.Registry]::LocalMachine; SubKey = $Matches[1] }
    }
    throw "Unsupported registry path: $Path"
}

function Set-RegistryString {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Name,
        [AllowEmptyString()][string]$Value = ''
    )

    $resolved = Resolve-RegistryPath $Path
    $key = $resolved.Root.CreateSubKey($resolved.SubKey)
    if ($null -eq $key) {
        throw "Unable to create registry key: $Path"
    }

    try {
        $key.SetValue($Name, $Value, [Microsoft.Win32.RegistryValueKind]::String)
    }
    finally {
        $key.Dispose()
    }
}

function Add-PackageExtension {
    param(
        [string]$Extension,
        [string]$ProgId,
        [string]$Description,
        [int]$IconIndex,
        [bool]$Compressed,
        [bool]$Preview
    )

    Set-RegistryString "HKCR\$Extension" '' $ProgId
    if ($Compressed) {
        Set-RegistryString "HKCR\$Extension" 'PerceivedType' 'compressed'
        Set-RegistryString "HKCR\$Extension" 'Content Type' 'application/x-zip-compressed'
        Set-RegistryString "HKCR\$Extension\OpenWithProgids" 'CompressedFolder' ''
    }

    Set-RegistryString "HKCR\$ProgId" '' $Description
    Set-RegistryString "HKCR\$ProgId\DefaultIcon" '' "$iconPath,$IconIndex"
    Set-RegistryString "HKCR\$ProgId\shell\open\command" '' "`"$appPath`" -i `"%1`""

    if ($Preview) {
        Set-RegistryString "HKCR\$ProgId\shell\preview" '' 'Preview as Winrar'
        Set-RegistryString "HKCR\$ProgId\shell\preview\command" '' '"C:\Program Files\WinRAR\WinRAR.exe" "%1"'
    }
}

function Add-ThumbnailComClass {
    param(
        [string]$Clsid,
        [string]$Description
    )

    Set-RegistryString "HKCR\CLSID\$Clsid" '' $Description
    Set-RegistryString "HKCR\CLSID\$Clsid\InprocServer32" '' $comHostPath
    Set-RegistryString "HKCR\CLSID\$Clsid\InprocServer32" 'ThreadingModel' 'Both'
    Set-RegistryString 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved' $Clsid $Description
}

function Add-ImageExtension {
    param(
        [string]$Extension,
        [string]$ProgId,
        [string]$Description,
        [int]$IconIndex,
        [string]$ThumbnailClsid
    )

    Add-PackageExtension -Extension $Extension -ProgId $ProgId -Description $Description -IconIndex $IconIndex -Compressed $false -Preview $false
    Set-RegistryString "HKCR\$Extension\ShellEx\$thumbnailProviderIid" '' $ThumbnailClsid
}

Add-PackageExtension '.cvx' 'ColorVision.Launcher.cvx' 'ColorVision Core Update Package' 4 $true $true
Add-PackageExtension '.cvxp' 'ColorVision.Launcher.cvxp' 'ColorVision Launcher Package' 5 $true $true
Add-PackageExtension '.lic' 'ColorVision.Launcher.lic' 'ColorVision Launcher Package' 6 $false $false
Add-PackageExtension '.cvcal' 'ColorVision.Launcher.cvcal' 'ColorVision Launcher Package' 7 $true $true

Add-ThumbnailComClass $cvRawThumbnailClsid 'ColorVision CVRaw Thumbnail Handler'
Add-ThumbnailComClass $cvCieThumbnailClsid 'ColorVision CVCie Thumbnail Handler'

Add-ImageExtension '.cvraw' 'ColorVision.Launcher.cvraw' 'ColorVision Raw Image File' 1 $cvRawThumbnailClsid
Add-ImageExtension '.cvcie' 'ColorVision.Launcher.cvcie' 'ColorVision CIE Image File' 2 $cvCieThumbnailClsid
Add-PackageExtension '.cvflow' 'ColorVision.Launcher.cvflow' 'ColorVision Launcher Package' 3 $false $false

Write-Output "File associations registered for: $appPath"
