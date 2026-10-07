#Requires -Version 7.0
[CmdletBinding(DefaultParameterSetName = 'Latest')]
param(
    [string]$BaseUrl,

    [Parameter(ParameterSetName = 'Latest')]
    [switch]$Latest,

    [Parameter(Mandatory = $true, ParameterSetName = 'Download')]
    [ValidatePattern('^[A-Za-z0-9_.-]{1,128}$')]
    [string]$FeedbackId,

    [string]$OutputDirectory = (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'ColorVision/Feedback'),

    [ValidateSet('Auto', 'Local', 'Remote')]
    [string]$Source = 'Auto',

    [string]$LocalRoot = 'H:\ColorVision\Feedback',

    [Parameter(Mandatory = $true, ParameterSetName = 'List')]
    [switch]$List,

    [string]$Query,

    [string]$Machine,

    [string]$AppVersion,

    [datetime]$CreatedFrom,

    [datetime]$CreatedTo,

    [Parameter(ParameterSetName = 'List')]
    [ValidateRange(1, 100)]
    [int]$Limit = 20,

    [string]$ApiKeyEnvironmentVariable = 'COLORVISION_FEEDBACK_API_KEY',

    [switch]$AllowInsecureLocalhost,

    [switch]$AllowInsecureHttp
)

$ErrorActionPreference = 'Stop'

function Get-FeedbackSetting {
    param([string]$Name)
    $value = [Environment]::GetEnvironmentVariable($Name)
    if (-not $value -and $IsWindows) {
        $value = [Environment]::GetEnvironmentVariable($Name, 'User')
    }
    return $value
}

function Get-FeedbackTimestamp {
    param($Metadata, [string]$Identifier)
    foreach ($value in @($Metadata.serverReceivedAt, $Metadata.createdAt)) {
        if ($value -is [DateTimeOffset]) { return $value.ToUniversalTime() }
        if ($value -is [DateTime]) {
            if ($value.Kind -eq [DateTimeKind]::Unspecified) { $value = [DateTime]::SpecifyKind($value, [DateTimeKind]::Utc) }
            return ([DateTimeOffset]$value).ToUniversalTime()
        }
        $timestamp = [DateTimeOffset]::MinValue
        if ($value -and [DateTimeOffset]::TryParse([string]$value, [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::AssumeUniversal, [ref]$timestamp)) {
            return $timestamp.ToUniversalTime()
        }
    }
    if ($Identifier -match '^(\d{8}_\d{6})(_BJT_)?') {
        $zone = if ($Matches[2]) { '+08:00' } else { '+00:00' }
        $timestamp = [DateTimeOffset]::MinValue
        if ([DateTimeOffset]::TryParseExact($Matches[1] + $zone, 'yyyyMMdd_HHmmsszzz',
            [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref]$timestamp)) {
            return $timestamp.ToUniversalTime()
        }
    }
    return $null
}

function Get-LocalFeedback {
    $root = Get-Item -LiteralPath $LocalRoot
    if ($root.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'LocalRoot cannot be a reparse point.' }
    $directories = foreach ($parent in Get-ChildItem -LiteralPath $root.FullName -Directory) {
        if ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
        $children = @(Get-ChildItem -LiteralPath $parent.FullName -Directory | Where-Object { -not ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) })
        if ((Test-Path -LiteralPath (Join-Path $parent.FullName 'feedback.json')) -or $parent.Name -match '^\d{8}_\d{6}_' -or $children.Count -eq 0) { $parent }
        else { $children }
    }
    $records = foreach ($directory in $directories) {
        if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
        if ($directory.Name -notmatch '^[A-Za-z0-9_.-]{1,128}$') { continue }
        $metadata = @{}
        $metadataPath = Join-Path $directory.FullName 'feedback.json'
        try {
            $metadataFile = Get-Item -LiteralPath $metadataPath -ErrorAction Stop
            if ($metadataFile.Length -gt 1MB -or ($metadataFile.Attributes -band [IO.FileAttributes]::ReparsePoint)) { continue }
            $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json -AsHashtable
        } catch { Write-Verbose "Missing or invalid feedback metadata: $($directory.Name)" }
        $metadataValid = $metadata -is [System.Collections.IDictionary] -and $metadata.Count -gt 0
        if (-not $metadataValid) { $metadata = @{} }
        $identifier = [string]$metadata.feedbackId
        if ($identifier -notmatch '^[A-Za-z0-9_.-]{1,128}$' -or $identifier -in @('.', '..')) { $identifier = $directory.Name }
        $timestamp = Get-FeedbackTimestamp $metadata $directory.Name
        $machineName = [string]$metadata.machineName
        if (-not $machineName -and $metadata.machineInfo -like '* / *') {
            $machineName = ([string]$metadata.machineInfo -split ' / ', 2)[0].Trim()
        }
        $attachments = @(Get-ChildItem -LiteralPath $directory.FullName -File | Where-Object {
            $_.Name -notin @('feedback.json', '.admin.json') -and $_.Name -notlike '.admin.*' -and
            $_.Name -notlike '.feedback.json.*' -and -not ($_.Attributes -band [IO.FileAttributes]::ReparsePoint)
        })
        [pscustomobject]@{
            feedback_id = $identifier
            metadata_valid = $metadataValid
            machine_name = $machineName
            created_at = if ($null -ne $timestamp) { $timestamp.ToString('o') } else { '' }
            received_beijing = if ($null -ne $timestamp) { $timestamp.ToOffset([TimeSpan]::FromHours(8)).ToString('yyyy-MM-dd HH:mm:ss') } else { 'Unknown' }
            app_version = [string]$metadata.appVersion
            message_preview = [string]$metadata.message
            user_name = [string]$metadata.userName
            directory = $directory.FullName
            attachment_count = $attachments.Count
        }
    }
    $filtered = @($records | Where-Object {
        (-not $Machine -or $_.machine_name.IndexOf($Machine, [StringComparison]::OrdinalIgnoreCase) -ge 0) -and
        (-not $AppVersion -or $_.app_version.IndexOf($AppVersion, [StringComparison]::OrdinalIgnoreCase) -ge 0) -and
        (-not $Query -or (($_.feedback_id, $_.machine_name, $_.user_name, $_.app_version, $_.message_preview) -join ' ').IndexOf($Query, [StringComparison]::OrdinalIgnoreCase) -ge 0) -and
        (-not $CreatedFrom -or ($_.created_at -and [DateTimeOffset]::Parse($_.created_at) -ge $CreatedFrom.ToUniversalTime())) -and
        (-not $CreatedTo -or ($_.created_at -and [DateTimeOffset]::Parse($_.created_at) -lt $CreatedTo.ToUniversalTime()))
    } | Sort-Object @{Expression={ if ($_.created_at) { [DateTimeOffset]::Parse($_.created_at) } else { [DateTimeOffset]::MinValue } }; Descending=$true}, @{Expression='feedback_id';Descending=$true})
    return $filtered
}

$localAvailable = Test-Path -LiteralPath $LocalRoot -PathType Container -ErrorAction SilentlyContinue
$useLocal = $Source -eq 'Local' -or ($Source -eq 'Auto' -and -not $PSBoundParameters.ContainsKey('BaseUrl') -and $localAvailable)
if ($useLocal) {
    if (-not $localAvailable) { throw "Feedback share is unavailable: $LocalRoot" }
    $records = @(Get-LocalFeedback)
    if ($PSCmdlet.ParameterSetName -eq 'List') {
        return [pscustomobject]@{ items = @($records | Select-Object -First $Limit); total = $records.Count; source = 'Local' }
    }
    $selected = if ($FeedbackId) { $records | Where-Object feedback_id -EQ $FeedbackId | Select-Object -First 1 }
                else { $records | Where-Object { $_.created_at -and $_.metadata_valid } | Select-Object -First 1 }
    if (-not $selected) { throw 'No matching feedback with a known receive time was found.' }
    if (@($records | Where-Object feedback_id -EQ $selected.feedback_id).Count -ne 1) { throw 'Feedback identifier is ambiguous.' }
    return [pscustomobject]@{
        FeedbackId = $selected.feedback_id; MachineName = $selected.machine_name
        ReceivedAtBeijing = $selected.received_beijing; Directory = $selected.directory
        AttachmentCount = $selected.attachment_count; Source = 'Local'
    }
}

if (-not $BaseUrl) { $BaseUrl = Get-FeedbackSetting 'COLORVISION_FEEDBACK_BASE_URL' }
if (-not $BaseUrl) { $BaseUrl = 'http://xc213618.ddns.me:9998' }

function Resolve-FeedbackBaseUri {
    param([string]$Value)

    $uri = [Uri]$Value
    if (-not $uri.IsAbsoluteUri) {
        throw 'BaseUrl must be an absolute URI.'
    }
    if ($uri.UserInfo -or $uri.Query -or $uri.Fragment -or $uri.Scheme -notin @('http', 'https')) {
        throw 'BaseUrl must be an HTTP(S) service URL without credentials, query or fragment.'
    }
    $isLocal = $uri.Host -in @('localhost', '127.0.0.1', '::1')
    $allowHttp = $AllowInsecureHttp -or (Get-FeedbackSetting 'COLORVISION_FEEDBACK_ALLOW_HTTP') -eq '1'
    if ($uri.Scheme -ne 'https' -and -not $allowHttp -and -not ($AllowInsecureLocalhost -and $isLocal)) {
        throw 'HTTP requires explicit -AllowInsecureHttp or COLORVISION_FEEDBACK_ALLOW_HTTP=1; prefer HTTPS when available.'
    }
    return [Uri]($uri.AbsoluteUri.TrimEnd('/') + '/')
}

function Join-FeedbackUri {
    param([Uri]$Root, [string]$Relative)
    return [Uri]::new($Root, $Relative.TrimStart('/'))
}

function Invoke-FeedbackJson {
    param([System.Net.Http.HttpClient]$Client, [Uri]$Uri)
    $response = $Client.GetAsync($Uri).GetAwaiter().GetResult()
    try {
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            throw "Feedback API request failed with HTTP $([int]$response.StatusCode): $body"
        }
        return $body | ConvertFrom-Json
    }
    finally {
        $response.Dispose()
    }
}

function Get-FileSha256 {
    param([string]$Path)
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

$baseUri = Resolve-FeedbackBaseUri $BaseUrl
if ($PSBoundParameters.ContainsKey('ApiKeyEnvironmentVariable')) {
    $apiKey = Get-FeedbackSetting $ApiKeyEnvironmentVariable
    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        throw "API key environment variable '$ApiKeyEnvironmentVariable' is not set."
    }
    $authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $apiKey)
} else {
    $configPath = Join-Path $PSScriptRoot '../Web/Backend/config.json'
    if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
        throw 'Feedback account configuration is missing from Web/Backend/config.json.'
    }
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    $username = [string]$config.upload_auth.username
    $password = [string]$config.upload_auth.password
    if ([string]::IsNullOrWhiteSpace($username) -or [string]::IsNullOrEmpty($password)) {
        throw 'Feedback account is missing from Web/Backend/config.json.'
    }
    $credential = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${username}:${password}"))
    $authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Basic', $credential)
}

$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [System.Net.Http.HttpClient]::new($handler, $true)
$client.Timeout = [TimeSpan]::FromMinutes(30)
$client.DefaultRequestHeaders.Authorization = $authorization
$client.DefaultRequestHeaders.Accept.ParseAdd('application/json')

try {
    if ($PSCmdlet.ParameterSetName -in @('List', 'Latest')) {
        $parameters = [System.Collections.Generic.List[string]]::new()
        $requestedLimit = if ($PSCmdlet.ParameterSetName -eq 'Latest') { 100 } else { $Limit }
        $parameters.Add("limit=$requestedLimit")
        if ($Query) { $parameters.Add('query=' + [Uri]::EscapeDataString($Query)) }
        if ($Machine) { $parameters.Add('machine=' + [Uri]::EscapeDataString($Machine)) }
        if ($AppVersion) { $parameters.Add('app_version=' + [Uri]::EscapeDataString($AppVersion)) }
        if ($PSBoundParameters.ContainsKey('CreatedFrom')) {
            $parameters.Add('created_from=' + [Uri]::EscapeDataString($CreatedFrom.ToUniversalTime().ToString('o')))
        }
        if ($PSBoundParameters.ContainsKey('CreatedTo')) {
            $parameters.Add('created_to=' + [Uri]::EscapeDataString($CreatedTo.ToUniversalTime().ToString('o')))
        }
        $queryUri = 'api/feedback?' + ($parameters -join '&')
        $uri = Join-FeedbackUri $baseUri ($queryUri + '&offset=0')
        $listing = Invoke-FeedbackJson $client $uri
        if ($PSCmdlet.ParameterSetName -eq 'List') {
            foreach ($item in @($listing.items)) {
                $received = Get-FeedbackTimestamp @{createdAt=$item.created_at} $item.feedback_id
                $displayTime = if ($null -ne $received) { $received.ToOffset([TimeSpan]::FromHours(8)).ToString('yyyy-MM-dd HH:mm:ss') } else { 'Unknown' }
                $item | Add-Member -NotePropertyName received_beijing -NotePropertyValue $displayTime -Force
            }
            return $listing
        }
        $offset = 0
        do {
            $selected = @($listing.items) | Where-Object { $_.metadata_valid -and $_.created_at } | Select-Object -First 1
            $offset += @($listing.items).Count
            if ($selected -or @($listing.items).Count -eq 0 -or $offset -ge $listing.total) { break }
            $listing = Invoke-FeedbackJson $client (Join-FeedbackUri $baseUri ($queryUri + "&offset=$offset"))
        } while ($true)
        if (-not $selected) { throw 'No completed matching feedback with a known receive time was found.' }
        $FeedbackId = [string]$selected.feedback_id
        if ($FeedbackId -notmatch '^[A-Za-z0-9_.-]{1,128}$' -or $FeedbackId -in @('.', '..')) { throw 'Server returned an unsafe feedback identifier.' }
    }

    if ($FeedbackId -in @('.', '..')) { throw 'Invalid feedback identifier.' }
    $detailUri = Join-FeedbackUri $baseUri ('api/feedback/' + [Uri]::EscapeDataString($FeedbackId))
    $detail = Invoke-FeedbackJson $client $detailUri
    if ($detail.feedback_id -ne $FeedbackId) {
        throw 'Server returned a different feedback identifier.'
    }

    $outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
    [IO.Directory]::CreateDirectory($outputRoot) | Out-Null
    $feedbackDirectory = [IO.Path]::Combine($outputRoot, $FeedbackId)
    [IO.Directory]::CreateDirectory($feedbackDirectory) | Out-Null

    foreach ($attachment in @($detail.attachments)) {
        $name = [string]$attachment.name
        if ([string]::IsNullOrWhiteSpace($name) -or [IO.Path]::GetFileName($name) -ne $name) {
            throw "Server returned an unsafe attachment name: '$name'."
        }
        $expectedSize = [int64]$attachment.size_bytes
        $expectedHash = ([string]$attachment.sha256).ToLowerInvariant()
        if ($expectedHash -notmatch '^[0-9a-f]{64}$') {
            throw "Attachment '$name' does not include a valid SHA-256 digest."
        }

        $targetPath = [IO.Path]::Combine($feedbackDirectory, $name)
        if ([IO.File]::Exists($targetPath)) {
            $existingSize = ([IO.FileInfo]$targetPath).Length
            $existingHash = Get-FileSha256 $targetPath
            if ($existingSize -eq $expectedSize -and $existingHash -eq $expectedHash) {
                Write-Verbose "Verified existing attachment: $name"
                continue
            }
            throw "Target already exists with different content: $targetPath"
        }

        $temporaryPath = "$targetPath.part.$([Guid]::NewGuid().ToString('N'))"
        $attachmentUri = Join-FeedbackUri $baseUri (
            'api/feedback/' + [Uri]::EscapeDataString($FeedbackId) +
            '/attachments/' + [Uri]::EscapeDataString($name)
        )
        try {
            $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $attachmentUri)
            $response = $client.SendAsync(
                $request,
                [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead
            ).GetAwaiter().GetResult()
            try {
                if (-not $response.IsSuccessStatusCode) {
                    $errorBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                    throw "Attachment download failed with HTTP $([int]$response.StatusCode): $errorBody"
                }
                $inputStream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                $outputStream = [IO.File]::Open($temporaryPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
                try {
                    $inputStream.CopyTo($outputStream)
                    $outputStream.Flush($true)
                }
                finally {
                    $outputStream.Dispose()
                    $inputStream.Dispose()
                }
            }
            finally {
                $response.Dispose()
                $request.Dispose()
            }

            $actualSize = ([IO.FileInfo]$temporaryPath).Length
            $actualHash = Get-FileSha256 $temporaryPath
            if ($actualSize -ne $expectedSize -or $actualHash -ne $expectedHash) {
                throw "Attachment verification failed for '$name'."
            }
            [IO.File]::Move($temporaryPath, $targetPath)
        }
        finally {
            if ([IO.File]::Exists($temporaryPath)) {
                [IO.File]::Delete($temporaryPath)
            }
        }
    }

    $manifestPath = [IO.Path]::Combine($feedbackDirectory, 'feedback-manifest.json')
    if (-not [IO.File]::Exists($manifestPath)) {
        $manifestTemporary = "$manifestPath.part.$([Guid]::NewGuid().ToString('N'))"
        try {
            $manifestJson = $detail | ConvertTo-Json -Depth 20
            [IO.File]::WriteAllText($manifestTemporary, $manifestJson, [Text.UTF8Encoding]::new($false))
            [IO.File]::Move($manifestTemporary, $manifestPath)
        }
        finally {
            if ([IO.File]::Exists($manifestTemporary)) {
                [IO.File]::Delete($manifestTemporary)
            }
        }
    }

    $receivedTimestamp = Get-FeedbackTimestamp @{createdAt=$detail.created_at} $FeedbackId
    [PSCustomObject]@{
        FeedbackId = $FeedbackId
        MachineName = $detail.machine_name
        ReceivedAtBeijing = if ($null -ne $receivedTimestamp) { $receivedTimestamp.ToOffset([TimeSpan]::FromHours(8)).ToString('yyyy-MM-dd HH:mm:ss') } else { 'Unknown' }
        Directory = $feedbackDirectory
        AttachmentCount = @($detail.attachments).Count
        Manifest = $manifestPath
        Source = 'Remote'
    }
}
finally {
    $client.Dispose()
}
