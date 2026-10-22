#requires -Version 7.4
[CmdletBinding()]
param([switch]$SqlGuards)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$passed=0
function Check([bool]$condition,[string]$message){if(-not $condition){throw $message};$script:passed++;Write-Output "PASS: $message"}
$initializer=Join-Path $repo 'scripts/Initialize-ShowcaseDemo.ps1'
$plan=& $initializer -PlanOnly -FullData|ConvertFrom-Json
Check ($plan.fullData -and $plan.keepDatabase) 'FullData plan retains an isolated UUID demo.'
$baseline=& $initializer -PlanOnly|ConvertFrom-Json
Check (-not $baseline.fullData) 'Existing baseline demo behavior remains opt-in compatible.'
$rejected=$false
try{& $initializer -FullData -DatabaseName 'DangKyTinChi_Minh' -PlanOnly|Out-Null}catch{$rejected=$true}
Check $rejected 'FullData cannot provision over the main database.'
$rejected=$false
try{& (Join-Path $repo 'scripts/demo/New-FullDemoAccounts.ps1') -DatabaseName $plan.database -OutputDirectory $repo|Out-Null}catch{$rejected=$_.Exception.Message -like '*exact owned showcase*'}
Check $rejected 'Additional-account setup rejects a handoff outside its owned demo folder.'
foreach($file in @('scripts/Initialize-ShowcaseDemo.ps1','scripts/Run-ShowcaseDemo.ps1','scripts/demo/New-FullDemoAccounts.ps1')){
 $tokens=$null;$errors=$null
 [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $repo $file),[ref]$tokens,[ref]$errors)
 Check ($errors.Count -eq 0) "PowerShell parses: $file"
}
foreach($file in @('21-catalog-data','22-academic-data','23-registration-data','24-organization-data','Assert-FullDemo')){
 $path=Join-Path $repo "scripts/demo/$file.sql"
 Check ([IO.File]::ReadAllText($path).Contains("N'__SHOWCASE_DATABASE__'")) "Raw seed fails closed before UUID substitution: $file"
 if($SqlGuards){
  $guardLog=Join-Path $repo ".tmp/full-demo-guard-$file.log"
  & sqlcmd -S . -E -C -I -b -x -l 5 -f 65001 -d master -i $path *> $guardLog
  Check ($LASTEXITCODE -ne 0 -and [IO.File]::ReadAllText($guardLog).Contains('54980')) "SQL guard rejects the unbound seed in master: $file"
 }
}
Write-Output "Full demo safety checks passed: $passed."
