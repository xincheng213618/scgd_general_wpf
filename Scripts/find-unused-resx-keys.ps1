param(
    [Parameter(Mandatory = $true)]
    [string]$BaseResx,

    [string]$SearchRoot,

    [string]$DesignerPath,

    [string[]]$IncludeExtensions = @('.cs', '.xaml'),

    [switch]$AsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-NormalizedPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path)
}

function Get-DefaultSearchRoot {
    param([Parameter(Mandatory = $true)][string]$StartPath)

    $current = [System.IO.DirectoryInfo](Split-Path -Parent $StartPath)
    $candidate = $null

    while ($current) {
        $hasGit = Test-Path -LiteralPath (Join-Path $current.FullName '.git')
        $hasDirectoryBuildProps = Test-Path -LiteralPath (Join-Path $current.FullName 'Directory.Build.props')
        $hasSolution = @(Get-ChildItem -LiteralPath $current.FullName -Filter *.sln -File -ErrorAction SilentlyContinue).Count -gt 0

        if ($hasGit -or $hasDirectoryBuildProps -or $hasSolution) {
            $candidate = $current.FullName
        }

        $current = $current.Parent
    }

    if ($candidate) {
        return $candidate
    }

    return Split-Path -Parent (Split-Path -Parent $StartPath)
}

function Get-ResourceMap {
    param([Parameter(Mandatory = $true)][string]$Path)

    [xml]$xml = Get-Content -Raw -LiteralPath $Path
    $items = foreach ($data in @($xml.root.data)) {
        if (-not $data.name) {
            continue
        }

        [pscustomobject]@{
            Key = [string]$data.name
            Value = [string]$data.value
        }
    }

    return $items
}

function Get-DesignerInfo {
    param([Parameter(Mandatory = $true)][string]$Path)

    $text = Get-Content -Raw -LiteralPath $Path

    $baseNameMatch = [regex]::Match(
        $text,
        'new\s+global::System\.Resources\.ResourceManager\("(?<base>[^"]+)"',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    $keyToProperty = @{}
    $propertyToKey = @{}
    $propertyPattern = [regex]::new(
        'public\s+static\s+string\s+(?<property>[A-Za-z_][A-Za-z0-9_]*)\s*\{\s*get\s*\{\s*return\s+ResourceManager\.GetString\("(?<key>[^"]+)"',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    foreach ($match in $propertyPattern.Matches($text)) {
        $key = $match.Groups['key'].Value
        $property = $match.Groups['property'].Value
        $keyToProperty[$key] = $property
        $propertyToKey[$property] = $key
    }

    [pscustomobject]@{
        BaseName = if ($baseNameMatch.Success) { $baseNameMatch.Groups['base'].Value } else { $null }
        ResourceNamespace = if ($baseNameMatch.Success) { $baseNameMatch.Groups['base'].Value -replace '\.Resources$', '' } else { $null }
        KeyToProperty = $keyToProperty
        PropertyToKey = $propertyToKey
    }
}

function Test-IsRelevantSourceFile {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$File,
        [Parameter(Mandatory = $true)][string[]]$Extensions,
        [Parameter(Mandatory = $true)][string]$BaseResxPath,
        [Parameter(Mandatory = $true)][string]$DesignerFilePath
    )

    if ($File.FullName -eq $BaseResxPath -or $File.FullName -eq $DesignerFilePath) {
        return $false
    }

    if ($File.FullName -match '[\\/](bin|obj)[\\/]') {
        return $false
    }

    if ($File.Extension -notin $Extensions) {
        return $false
    }

    if ($File.Name -like 'Resources*.resx') {
        return $false
    }

    return $true
}

function Add-UsedPropertyMatches {
    param(
        [AllowEmptyString()]
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][regex]$Pattern,
        [Parameter(Mandatory = $true)][hashtable]$PropertyToKey,
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][System.Collections.Generic.HashSet[string]]$UsedKeys
    )

    foreach ($match in $Pattern.Matches($Text)) {
        $propertyName = $match.Groups['property'].Value
        if ($PropertyToKey.ContainsKey($propertyName)) {
            $null = $UsedKeys.Add($PropertyToKey[$propertyName])
        }
    }
}

function Add-UsedLiteralKeyMatches {
    param(
        [AllowEmptyString()]
        [Parameter(Mandatory = $true)][string]$Text,
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][System.Collections.Generic.HashSet[string]]$KnownKeys,
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][System.Collections.Generic.HashSet[string]]$UsedKeys
    )

    $literalPattern = [regex]::new(
        'GetString\s*\(\s*(?:@)?"(?<key>[^"]+)"',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    foreach ($match in $literalPattern.Matches($Text)) {
        $key = $match.Groups['key'].Value
        if ($KnownKeys.Contains($key)) {
            $null = $UsedKeys.Add($key)
        }
    }
}

function Add-UsedDisplayNameMatches {
    param(
        [AllowEmptyString()]
        [Parameter(Mandatory = $true)][string]$Text,
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][System.Collections.Generic.HashSet[string]]$KnownKeys,
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][System.Collections.Generic.HashSet[string]]$UsedKeys
    )

    $patterns = @(
        [regex]::new('(?:DisplayName|DisplayNameAttribute)\s*\(\s*(?:@)?"(?<key>[^"]+)"', [System.Text.RegularExpressions.RegexOptions]::Singleline),
        [regex]::new('(?:CommandDisplay|CommandDisplayAttribute)\s*\(\s*(?:@)?"(?<key>[^"]+)"', [System.Text.RegularExpressions.RegexOptions]::Singleline),
        [regex]::new('(?:Category|CategoryAttribute)\s*\(\s*(?:@)?"(?<key>[^"]+)"', [System.Text.RegularExpressions.RegexOptions]::Singleline),
        [regex]::new('(?:Description|DescriptionAttribute)\s*\(\s*(?:@)?"(?<key>[^"]+)"', [System.Text.RegularExpressions.RegexOptions]::Singleline),
        [regex]::new('(?:LocalizedDisplayName|LocalizedDisplayNameAttribute)\s*\((?:[^,\r\n]|\([^\)]*\))*?,\s*(?:@)?"(?<key>[^"]+)"', [System.Text.RegularExpressions.RegexOptions]::Singleline),
        [regex]::new('(?:LocalizedDescription|LocalizedDescriptionAttribute)\s*\((?:[^,\r\n]|\([^\)]*\))*?,\s*(?:@)?"(?<key>[^"]+)"', [System.Text.RegularExpressions.RegexOptions]::Singleline),
        [regex]::new('\bDisplayName\s*=\s*(?:@)?"(?<key>[^"]+)"', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    )

    foreach ($pattern in $patterns) {
        foreach ($match in $pattern.Matches($Text)) {
            $key = $match.Groups['key'].Value
            if ($KnownKeys.Contains($key)) {
                $null = $UsedKeys.Add($key)
            }
        }
    }
}

function Add-UsedPropertyNameMatches {
    param(
        [AllowEmptyString()]
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][hashtable]$KeyToProperty,
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][System.Collections.Generic.HashSet[string]]$UsedKeys
    )

    $propertyPattern = [regex]::new(
        '(?m)^\s*(?:public|internal|protected)\s+(?:static\s+)?(?:new\s+|virtual\s+|override\s+|sealed\s+|abstract\s+|partial\s+)*[A-Za-z_][A-Za-z0-9_<>,\.\[\]\?]*\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{\s*(?:get|set)',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    foreach ($match in $propertyPattern.Matches($Text)) {
        $propertyName = $match.Groups['name'].Value
        if ($KeyToProperty.ContainsKey($propertyName)) {
            $null = $UsedKeys.Add($propertyName)
        }
    }
}

function Add-UsedLanguageKeyMatches {
    param(
        [AllowEmptyString()]
        [Parameter(Mandatory = $true)][string]$Text,
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][System.Collections.Generic.HashSet[string]]$KnownKeys,
        [AllowEmptyCollection()]
        [Parameter(Mandatory = $true)][System.Collections.Generic.HashSet[string]]$UsedKeys
    )

    if ($Text -notmatch 'ResourceManager\.GetString\(\s*Name\b' -and $Text -notmatch 'ResourceManager\.GetString\(\s*Thread\.CurrentThread\.CurrentUICulture\.Name\b') {
        return
    }

    foreach ($key in $KnownKeys) {
        if ($key -match '^[a-z]{2}(?:-[A-Za-z0-9]+)?$') {
            $null = $UsedKeys.Add($key)
        }
    }
}

$baseResxPath = Resolve-NormalizedPath -Path $BaseResx
$resourceProjectRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent (Split-Path -Parent $baseResxPath)))

if (-not $PSBoundParameters.ContainsKey('SearchRoot')) {
    $SearchRoot = Get-DefaultSearchRoot -StartPath $baseResxPath
}

if (-not $PSBoundParameters.ContainsKey('DesignerPath')) {
    $DesignerPath = Join-Path (Split-Path -Parent $baseResxPath) 'Resources.Designer.cs'
}

$searchRootPath = Resolve-NormalizedPath -Path $SearchRoot
$designerFilePath = Resolve-NormalizedPath -Path $DesignerPath

$resources = Get-ResourceMap -Path $baseResxPath
$designerInfo = Get-DesignerInfo -Path $designerFilePath

$knownKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($resource in $resources) {
    $null = $knownKeys.Add($resource.Key)
}

$usedKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

$qualifiedPatterns = @()
if ($designerInfo.BaseName) {
    $qualifiedPatterns += [regex]::new(
        '(?<![A-Za-z0-9_\.])' + [regex]::Escape($designerInfo.BaseName) + '\.(?<property>[A-Za-z_][A-Za-z0-9_]*)\b',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
}

$qualifiedPatterns += [regex]::new(
    '(?<![A-Za-z0-9_\.])Properties\.Resources\.(?<property>[A-Za-z_][A-Za-z0-9_]*)\b',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)

$unqualifiedPattern = [regex]::new(
    '(?<![A-Za-z0-9_\.])Resources\.(?<property>[A-Za-z_][A-Za-z0-9_]*)\b',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)

$sourceFiles = Get-ChildItem -LiteralPath $searchRootPath -Recurse -File | Where-Object {
    Test-IsRelevantSourceFile -File $_ -Extensions $IncludeExtensions -BaseResxPath $baseResxPath -DesignerFilePath $designerFilePath
}

foreach ($file in $sourceFiles) {
    $text = Get-Content -Raw -LiteralPath $file.FullName
    $isResourceProjectFile = $file.FullName.StartsWith($resourceProjectRoot, [System.StringComparison]::OrdinalIgnoreCase)

    foreach ($pattern in $qualifiedPatterns) {
        Add-UsedPropertyMatches -Text $text -Pattern $pattern -PropertyToKey $designerInfo.PropertyToKey -UsedKeys $usedKeys
    }

    $shouldCheckUnqualified = $false
    if ($file.Extension -eq '.xaml') {
        $shouldCheckUnqualified = $true
    }
    elseif ($designerInfo.ResourceNamespace) {
        $usingPattern = '(?m)^\s*using\s+' + [regex]::Escape($designerInfo.ResourceNamespace) + '\s*;'
        $shouldCheckUnqualified = $text -match $usingPattern
    }

    if ($shouldCheckUnqualified) {
        Add-UsedPropertyMatches -Text $text -Pattern $unqualifiedPattern -PropertyToKey $designerInfo.PropertyToKey -UsedKeys $usedKeys
    }

    Add-UsedLiteralKeyMatches -Text $text -KnownKeys $knownKeys -UsedKeys $usedKeys

    if ($isResourceProjectFile) {
        Add-UsedDisplayNameMatches -Text $text -KnownKeys $knownKeys -UsedKeys $usedKeys
        Add-UsedPropertyNameMatches -Text $text -KeyToProperty $designerInfo.KeyToProperty -UsedKeys $usedKeys
        Add-UsedLanguageKeyMatches -Text $text -KnownKeys $knownKeys -UsedKeys $usedKeys
    }
}

$unused = foreach ($resource in $resources) {
    if (-not $usedKeys.Contains($resource.Key)) {
        [pscustomobject]@{
            Key = $resource.Key
            Property = if ($designerInfo.KeyToProperty.ContainsKey($resource.Key)) { $designerInfo.KeyToProperty[$resource.Key] } else { $null }
            Value = $resource.Value
        }
    }
}

if ($AsJson) {
    $unused | ConvertTo-Json -Depth 4
}
else {
    $unused | Sort-Object Key | Format-Table -AutoSize
}