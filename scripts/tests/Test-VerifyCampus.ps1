[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$runner = Join-Path $PSScriptRoot '../Verify-Campus.ps1'
if (-not (Test-Path -LiteralPath $runner)) { throw 'Verification runner has not been delivered.' }
$tokens = $null; $errors = $null
$null = [Management.Automation.Language.Parser]::ParseFile($runner, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw ($errors.Message -join '; ') }
$ast = [Management.Automation.Language.Parser]::ParseFile($runner,[ref]$tokens,[ref]$errors)
$selector = $ast.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Find-CoverageFile'},$true)
if (-not $selector) { throw 'Coverage selector must handle duplicate TRX attachments safely.' }
. ([scriptblock]::Create($selector.Extent.Text))
$coverageRoot = Join-Path ([IO.Path]::GetTempPath()) ('campus-coverage-contract-' + [Guid]::NewGuid().ToString('N'))
try {
    [IO.Directory]::CreateDirectory((Join-Path $coverageRoot 'collector')) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $coverageRoot 'trx-copy')) | Out-Null
    Set-Content (Join-Path $coverageRoot 'collector/coverage.cobertura.xml') '<coverage line-rate="0.9" />'
    Copy-Item (Join-Path $coverageRoot 'collector/coverage.cobertura.xml') (Join-Path $coverageRoot 'trx-copy/coverage.cobertura.xml')
    if (-not (Find-CoverageFile $coverageRoot)) { throw 'Identical collector/TRX copies must be accepted.' }
    Set-Content (Join-Path $coverageRoot 'trx-copy/coverage.cobertura.xml') '<coverage line-rate="0.1" />'
    $rejectedCoverage=$false
    try { Find-CoverageFile $coverageRoot | Out-Null } catch { $rejectedCoverage=$true }
    if (-not $rejectedCoverage) { throw 'Conflicting coverage reports must be rejected.' }
} finally {
    $resolvedCoverageRoot=[IO.Path]::GetFullPath($coverageRoot)
    if ($resolvedCoverageRoot.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()),[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolvedCoverageRoot) -match '^campus-coverage-contract-[a-f0-9]{32}$') { Remove-Item -LiteralPath $resolvedCoverageRoot -Recurse -Force }
}
$first = & $runner -PlanOnly | ConvertFrom-Json
$second = & $runner -PlanOnly | ConvertFrom-Json
if (@($first.fixtures).Count -ne 13) { throw 'Full verification needs eight SQL and five native fixtures.' }
if (@($first.fixtures | Select-Object -Unique).Count -ne 13) { throw 'Fixture names must be unique.' }
if (@($first.fixtures | Where-Object { $_ -notmatch '^DangKyTinChi_Minh_Test_(Verify|Planner4)_V[0-9a-f]{12}_[A-Za-z0-9]+$' }).Count) { throw 'Unsafe fixture name.' }
if (@($first.fixtures | Where-Object { $second.fixtures -contains $_ }).Count) { throw 'Invocations must not share fixtures.' }
$rejected = $false
try { & $runner -Server 'remote.example' -PlanOnly | Out-Null } catch { $rejected = $true }
if (-not $rejected) { throw 'Remote SQL targets must be rejected before work.' }
$source = Get-Content -LiteralPath $runner -Raw
foreach ($required in @('finally','Restore-Environment','NotExecuted','DROP DATABASE','results.json','--artifacts-path','cleanupErrors','coverage.cobertura.xml')) {
    if (-not $source.Contains($required)) { throw "Missing verification safeguard: $required" }
}
Write-Output 'PASS: valid PowerShell; unique disposable fixture manifest; remote target rejected; full-run safeguards present.'
