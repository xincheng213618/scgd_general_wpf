[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Profile,
    [string[]]$Cases = @('all'),
    [ValidateRange(30, 300)][int]$TimeoutSeconds = 120,
    [ValidateRange(8192, 262144)][int]$TokenBudget = 98304,
    [ValidateRange(1, 10)][int]$Repetitions = 1,
    [string]$ArtifactsPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'ColorVision-Copilot-Evaluation-Build'),
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$evaluationOutput = Join-Path ([System.IO.Path]::GetTempPath()) ('ColorVision-Copilot-Evaluation-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$evaluationVariables = @{
    COLORVISION_COPILOT_EVAL_ENABLED = '1'
    COLORVISION_COPILOT_EVAL_PROFILE = $Profile
    COLORVISION_COPILOT_EVAL_CASES = ($Cases -join ',')
    COLORVISION_COPILOT_EVAL_OUTPUT = $evaluationOutput
    COLORVISION_COPILOT_EVAL_TIMEOUT = $TimeoutSeconds.ToString([cultureinfo]::InvariantCulture)
    COLORVISION_COPILOT_EVAL_TOKEN_BUDGET = $TokenBudget.ToString([cultureinfo]::InvariantCulture)
    COLORVISION_COPILOT_EVAL_REPETITIONS = $Repetitions.ToString([cultureinfo]::InvariantCulture)
}
$previousVariables = @{}
foreach ($name in $evaluationVariables.Keys) {
    $previousVariables[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$testExitCode = 1
Push-Location $repositoryRoot
try {
    foreach ($name in $evaluationVariables.Keys) {
        [Environment]::SetEnvironmentVariable($name, $evaluationVariables[$name], 'Process')
    }
    Write-Host 'Live Copilot evaluation uses the selected saved profile API and may incur charges.'
    Write-Host "Synthetic workspace and evidence: $evaluationOutput"
    $testArguments = @('test', '.\Test\ColorVision.Copilot.Tests\ColorVision.Copilot.Tests.csproj', '-p:Platform=x64', '--artifacts-path', $ArtifactsPath,
        '--filter', 'FullyQualifiedName~CopilotBusinessEvaluationTests.EvaluateConfiguredProfile',
        '--results-directory', $evaluationOutput, '--logger', 'trx;LogFileName=evaluation.trx')
    if ($NoBuild) { $testArguments += @('--no-build', '--no-restore') }
    & dotnet @testArguments
    $testExitCode = $LASTEXITCODE
    $reportPath = Join-Path $evaluationOutput 'report.json'
    if (Test-Path -LiteralPath $reportPath) {
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        Write-Host ("Passed {0}/{1}; completed {1}/{2}; provider-reported tokens {3}; failed tool calls {4}." -f $report.PassedCases, $report.CompletedCases, $report.PlannedCases, $report.ReportedTokens, $report.FailedToolCalls)
        Write-Host "Report: $reportPath"
    }
    else {
        Write-Warning 'No case report was produced. Inspect the test result; this is not a successful evaluation.'
        $testExitCode = 1
    }
}
finally {
    foreach ($name in $evaluationVariables.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousVariables[$name], 'Process')
    }
    Pop-Location
}
exit $testExitCode
