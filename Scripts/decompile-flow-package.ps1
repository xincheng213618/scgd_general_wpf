[CmdletBinding()]
param(
    [string]$PackagePath = 'C:\Users\17917\Desktop\Flow.817.zip',
    [string]$DependencyDirectory = 'C:\Users\17917\Desktop\CVWindowsService\InstallTool',
    [string]$OutputDirectory = 'C:\Users\17917\Desktop\新建文件夹',
    [string]$WorkspaceDirectory = 'C:\Users\17917\Desktop\Flow.817.decompile',
    [switch]$SkipBuildValidation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ilSpyVersion = '11.0.0.9375'
$targetAssemblyName = 'FlowEngineLib.dll'

function Get-NormalizedPath {
    param([Parameter(Mandatory)][string]$Path)

    return [System.IO.Path]::GetFullPath($Path).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
}

function Assert-SafeOutputDirectory {
    param([Parameter(Mandatory)][string]$Path)

    $root = [System.IO.Path]::GetPathRoot($Path)
    if ([string]::IsNullOrWhiteSpace($Path) -or
        [string]::Equals($Path, $root, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to replace an unsafe output directory: $Path"
    }
}

$packageFullPath = Get-NormalizedPath $PackagePath
$dependencyFullPath = Get-NormalizedPath $DependencyDirectory
$outputFullPath = Get-NormalizedPath $OutputDirectory
$workspaceFullPath = Get-NormalizedPath $WorkspaceDirectory

if (-not (Test-Path -LiteralPath $packageFullPath -PathType Leaf)) {
    throw "Flow package not found: $packageFullPath"
}
if (-not (Test-Path -LiteralPath $dependencyFullPath -PathType Container)) {
    throw "Dependency directory not found: $dependencyFullPath"
}
Assert-SafeOutputDirectory $outputFullPath

$runName = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), ([guid]::NewGuid().ToString('N').Substring(0, 8))
$runDirectory = Join-Path $workspaceFullPath "Runs\$runName"
$packageDirectory = Join-Path $runDirectory 'Package'
$assemblyDirectory = Join-Path $runDirectory 'Assemblies'
$generatedDirectory = Join-Path $runDirectory 'Generated'
$previousOutputDirectory = Join-Path $runDirectory 'PreviousOutput'

New-Item -ItemType Directory -Path $packageDirectory, $assemblyDirectory, $generatedDirectory -Force | Out-Null

Write-Host "Extracting package: $packageFullPath"
Expand-Archive -LiteralPath $packageFullPath -DestinationPath $packageDirectory -Force

$targetAssemblies = @(Get-ChildItem -LiteralPath $packageDirectory -Filter $targetAssemblyName -File -Recurse)
if ($targetAssemblies.Count -ne 1) {
    throw "Expected exactly one $targetAssemblyName in the package, found $($targetAssemblies.Count)."
}

$dependencyDlls = @(Get-ChildItem -LiteralPath $dependencyFullPath -Filter '*.dll' -File |
    Where-Object { $_.Name -notlike 'FlowEngineLib*.dll' })
if ($dependencyDlls.Count -eq 0) {
    throw "No dependency DLLs were found in: $dependencyFullPath"
}

Write-Host "Copying $($dependencyDlls.Count) dependency DLLs into: $assemblyDirectory"
foreach ($dependencyDll in $dependencyDlls) {
    Copy-Item -LiteralPath $dependencyDll.FullName -Destination $assemblyDirectory -Force
}

$packageDlls = @(Get-ChildItem -LiteralPath $packageDirectory -Filter '*.dll' -File -Recurse)
Write-Host "Overlaying $($packageDlls.Count) DLLs from the delivery package."
foreach ($packageDll in $packageDlls) {
    Copy-Item -LiteralPath $packageDll.FullName -Destination $assemblyDirectory -Force
}

$targetAssemblyPath = Join-Path $assemblyDirectory $targetAssemblyName
if (-not (Test-Path -LiteralPath $targetAssemblyPath -PathType Leaf)) {
    throw "Target assembly was not staged: $targetAssemblyPath"
}

$toolDirectory = Join-Path $env:LOCALAPPDATA "ColorVision\Tools\ILSpyCmd\$ilSpyVersion"
$ilSpyExecutable = Join-Path $toolDirectory 'ilspycmd.exe'
if (-not (Test-Path -LiteralPath $ilSpyExecutable -PathType Leaf)) {
    Write-Host "Installing ilspycmd $ilSpyVersion into: $toolDirectory"
    New-Item -ItemType Directory -Path $toolDirectory -Force | Out-Null
    & dotnet tool install ilspycmd --version $ilSpyVersion --tool-path $toolDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install ilspycmd $ilSpyVersion."
    }
}

Write-Host "Decompiling $targetAssemblyName with ILSpy $ilSpyVersion."
& $ilSpyExecutable `
    --project `
    --nested-directories `
    --disable-updatecheck `
    --referencepath $assemblyDirectory `
    --outputdir $generatedDirectory `
    $targetAssemblyPath
if ($LASTEXITCODE -ne 0) {
    throw "ILSpy decompilation failed with exit code $LASTEXITCODE. Staged files remain in: $runDirectory"
}

$projectFiles = @(Get-ChildItem -LiteralPath $generatedDirectory -Filter '*.csproj' -File)
$sourceFiles = @(Get-ChildItem -LiteralPath $generatedDirectory -Filter '*.cs' -File -Recurse)
if ($projectFiles.Count -ne 1 -or $sourceFiles.Count -eq 0) {
    throw "ILSpy output validation failed. Projects=$($projectFiles.Count), Sources=$($sourceFiles.Count)."
}

$selfContainedAssemblyDirectory = Join-Path $generatedDirectory 'Assemblies'
New-Item -ItemType Directory -Path $selfContainedAssemblyDirectory -Force | Out-Null
Get-ChildItem -LiteralPath $assemblyDirectory -Filter '*.dll' -File |
    Copy-Item -Destination $selfContainedAssemblyDirectory -Force

foreach ($projectFile in $projectFiles) {
    $projectContent = [System.IO.File]::ReadAllText($projectFile.FullName)
    $updatedProjectContent = $projectContent.Replace('..\Assemblies\', 'Assemblies\')
    if ($updatedProjectContent -eq $projectContent) {
        throw "ILSpy project does not contain the expected dependency path: $($projectFile.FullName)"
    }
    [System.IO.File]::WriteAllText(
        $projectFile.FullName,
        $updatedProjectContent,
        [System.Text.UTF8Encoding]::new($false))
}

if (-not $SkipBuildValidation) {
    $validationDirectory = Join-Path $runDirectory 'BuildValidation'
    $validationOutputDirectory = (Join-Path $validationDirectory 'bin') + [System.IO.Path]::DirectorySeparatorChar
    $validationIntermediateDirectory = (Join-Path $validationDirectory 'obj') + [System.IO.Path]::DirectorySeparatorChar
    Write-Host "Validating generated project: $($projectFiles[0].FullName)"
    & dotnet build $projectFiles[0].FullName `
        --verbosity minimal `
        -p:BaseOutputPath=$validationOutputDirectory `
        -p:BaseIntermediateOutputPath=$validationIntermediateDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Generated project validation failed with exit code $LASTEXITCODE. Run files remain in: $runDirectory"
    }
}

$outputParent = Split-Path -Path $outputFullPath -Parent
New-Item -ItemType Directory -Path $outputParent -Force | Out-Null
$movedPreviousOutput = $false
try {
    if (Test-Path -LiteralPath $outputFullPath) {
        if (-not (Test-Path -LiteralPath $outputFullPath -PathType Container)) {
            throw "Output path exists but is not a directory: $outputFullPath"
        }
        Move-Item -LiteralPath $outputFullPath -Destination $previousOutputDirectory
        $movedPreviousOutput = $true
    }
    Move-Item -LiteralPath $generatedDirectory -Destination $outputFullPath
}
catch {
    if ($movedPreviousOutput -and
        -not (Test-Path -LiteralPath $outputFullPath) -and
        (Test-Path -LiteralPath $previousOutputDirectory)) {
        Move-Item -LiteralPath $previousOutputDirectory -Destination $outputFullPath
    }
    throw
}

Write-Host ''
Write-Host 'Flow package decompilation completed.' -ForegroundColor Green
Write-Host "Package:      $packageFullPath"
Write-Host "Dependencies: $assemblyDirectory"
Write-Host "Output:       $outputFullPath"
Write-Host "References:   $(Join-Path $outputFullPath 'Assemblies')"
Write-Host "Sources:      $($sourceFiles.Count)"
if ($movedPreviousOutput) {
    Write-Host "Previous:     $previousOutputDirectory"
}
