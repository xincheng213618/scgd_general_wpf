[CmdletBinding(DefaultParameterSetName = 'Download')]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [Parameter(Mandatory = $true, ParameterSetName = 'Download')]
    [ValidatePattern('^[A-Za-z0-9_.-]{1,128}$')]
    [string]$FeedbackId,

    [Parameter(Mandatory = $true, ParameterSetName = 'Download')]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true, ParameterSetName = 'List')]
    [switch]$List,

    [Parameter(ParameterSetName = 'List')]
    [string]$Query,

    [Parameter(ParameterSetName = 'List')]
    [string]$Machine,

    [Parameter(ParameterSetName = 'List')]
    [string]$AppVersion,

    [Parameter(ParameterSetName = 'List')]
    [datetime]$CreatedFrom,

    [Parameter(ParameterSetName = 'List')]
    [datetime]$CreatedTo,

    [Parameter(ParameterSetName = 'List')]
    [ValidateRange(1, 100)]
    [int]$Limit = 20,

    [string]$ApiKeyEnvironmentVariable = 'COLORVISION_FEEDBACK_API_KEY',

    [switch]$AllowInsecureLocalhost
)

$ErrorActionPreference = 'Stop'

function Resolve-FeedbackBaseUri {
    param([string]$Value)

    $uri = [Uri]$Value
    if (-not $uri.IsAbsoluteUri) {
        throw 'BaseUrl must be an absolute URI.'
    }
    $isLocal = $uri.Host -in @('localhost', '127.0.0.1', '::1')
    if ($uri.Scheme -ne 'https' -and -not ($AllowInsecureLocalhost -and $isLocal)) {
        throw 'BaseUrl must use HTTPS. Use -AllowInsecureLocalhost only for a local test server.'
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

$apiKey = [Environment]::GetEnvironmentVariable($ApiKeyEnvironmentVariable)
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    throw "API key environment variable '$ApiKeyEnvironmentVariable' is not set."
}

$baseUri = Resolve-FeedbackBaseUri $BaseUrl
$handler = [System.Net.Http.HttpClientHandler]::new()
$client = [System.Net.Http.HttpClient]::new($handler, $true)
$client.Timeout = [TimeSpan]::FromMinutes(30)
$client.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $apiKey)
$client.DefaultRequestHeaders.Accept.ParseAdd('application/json')

try {
    if ($PSCmdlet.ParameterSetName -eq 'List') {
        $parameters = [System.Collections.Generic.List[string]]::new()
        $parameters.Add("limit=$Limit")
        $parameters.Add('offset=0')
        if ($Query) { $parameters.Add('query=' + [Uri]::EscapeDataString($Query)) }
        if ($Machine) { $parameters.Add('machine=' + [Uri]::EscapeDataString($Machine)) }
        if ($AppVersion) { $parameters.Add('app_version=' + [Uri]::EscapeDataString($AppVersion)) }
        if ($PSBoundParameters.ContainsKey('CreatedFrom')) {
            $parameters.Add('created_from=' + [Uri]::EscapeDataString($CreatedFrom.ToUniversalTime().ToString('o')))
        }
        if ($PSBoundParameters.ContainsKey('CreatedTo')) {
            $parameters.Add('created_to=' + [Uri]::EscapeDataString($CreatedTo.ToUniversalTime().ToString('o')))
        }
        $uri = Join-FeedbackUri $baseUri ('api/feedback?' + ($parameters -join '&'))
        Invoke-FeedbackJson $client $uri
        return
    }

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

    [PSCustomObject]@{
        FeedbackId = $FeedbackId
        Directory = $feedbackDirectory
        AttachmentCount = @($detail.attachments).Count
        Manifest = $manifestPath
    }
}
finally {
    $client.Dispose()
}
