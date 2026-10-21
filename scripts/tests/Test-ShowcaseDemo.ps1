#requires -Version 7.4
[CmdletBinding()]
param([switch]$SqlIntegration, [switch]$Native, [string]$Server='.')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$script=Join-Path $root 'scripts/Initialize-ShowcaseDemo.ps1'
function Check([bool]$condition,[string]$message) { if(-not $condition){throw $message}; Write-Output "PASS: $message" }
function Reject([scriptblock]$work,[string]$message,[string]$reason='') {
 $rejected=$false
 try{&$work|Out-Null}catch{$rejected=[string]::IsNullOrEmpty($reason) -or $_.Exception.Message.Contains($reason,[StringComparison]::Ordinal)}
 Check $rejected $message
}
function Remove-TestFixture {
 param([string]$Database,[string]$Manifest,[scriptblock]$DropDatabase={
  param($database)
  &sqlcmd -S $Server -E -C -b -x -d master -Q "IF DB_ID(N'$database') IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;DROP DATABASE [$database];END;"|Out-Null
  if($LASTEXITCODE-ne 0){throw 'Fixture cleanup failed.'}
 })
 if($Database -cnotmatch '^DangKyTinChi_Minh_Test_Showcase_[a-f0-9]{32}$'){throw 'Unsafe cleanup target.'}
 $privateRoot=Join-Path $root '.tmp/showcase-20'
 $expected=[IO.Path]::GetFullPath((Join-Path $privateRoot ($Database.Substring('DangKyTinChi_Minh_Test_Showcase_'.Length)+'/manifest.json')))
 if([IO.Path]::GetFullPath($Manifest)-cne $expected){throw 'Cleanup manifest must belong to the exact fixture.'}
 $state=Get-Content -LiteralPath $Manifest -Raw|ConvertFrom-Json
 if($state.database-cne $Database -or -not $state.synthetic){throw 'Cleanup requires the exact synthetic fixture manifest.'}
 & $DropDatabase $Database
 # Update only after SQL reports success; a failed cleanup must remain visible.
 $state.cleaned=$true
 $state.keepDatabase=$false
 $state|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $Manifest -Encoding utf8
}
Check (Test-Path -LiteralPath $script) 'Showcase has a dedicated provisioning entry point.'
foreach($name in @('DangKyTinChi_Minh','DangKyTinChi_Minh_Test_StudentPreview','DangKyTinChi_Minh_Test_Showcase_abc',"DangKyTinChi_Minh_Test_Showcase_';DROP")) {
 Reject { &$script -DatabaseName $name -PlanOnly } "Unsafe or pre-existing target name rejected: $name"
}
Reject { &$script -Server 'remote.example' -PlanOnly } 'Remote server rejected before SQL.'
Reject { &$script -Ephemeral -PlanOnly } 'Ephemeral database requires verification.'
$plan=&$script -PlanOnly | ConvertFrom-Json
Check ($plan.database -cmatch '^DangKyTinChi_Minh_Test_Showcase_[a-f0-9]{32}$') 'Default database is a fresh UUID in the showcase namespace.'
Check ($plan.keepDatabase -and $plan.output.StartsWith((Join-Path $root '.tmp/showcase-20'),[StringComparison]::OrdinalIgnoreCase)) 'Default demo is retained with private local evidence.'
# Filesystem-only lifecycle regression; SQL is replaced by an explicit stub.
$lifecycleId=[Guid]::NewGuid().ToString('N')
$lifecycleDatabase='DangKyTinChi_Minh_Test_Showcase_'+$lifecycleId
$lifecycleFolder=Join-Path $root ".tmp/showcase-20/$lifecycleId"
$lifecycleManifest=Join-Path $lifecycleFolder 'manifest.json'
[IO.Directory]::CreateDirectory($lifecycleFolder)|Out-Null
try {
 $launcherTokens=$null;$launcherErrors=$null
 $launcherAst=[Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'scripts/Run-ShowcaseDemo.ps1'),[ref]$launcherTokens,[ref]$launcherErrors)
 $reset=$launcherAst.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Reset-VerificationOutput'},$true)
 Check ($null-ne $reset) 'Launcher resets previous native results before a verification launch.'
 . ([scriptblock]::Create($reset.Extent.Text))
 foreach($file in @('results.json','error.txt','evidence.txt')){Set-Content -LiteralPath (Join-Path $lifecycleFolder $file) -Value 'Historical output'}
 Reset-VerificationOutput $lifecycleFolder
 Check (-not(Test-Path -LiteralPath (Join-Path $lifecycleFolder 'results.json')) -and -not(Test-Path -LiteralPath (Join-Path $lifecycleFolder 'error.txt')) -and (Test-Path -LiteralPath (Join-Path $lifecycleFolder 'evidence.txt'))) 'Verification removes stale outcome files and preserves other evidence.'
 @{database=$lifecycleDatabase;synthetic=$true;passed=$true;cleaned=$false;keepDatabase=$true}|ConvertTo-Json|Set-Content -LiteralPath $lifecycleManifest
 Reject {Remove-TestFixture -Database $lifecycleDatabase -Manifest $lifecycleManifest -DropDatabase {throw 'Expected simulated SQL cleanup failure.'}} 'Cleanup failure is reported.'
 $state=Get-Content -LiteralPath $lifecycleManifest -Raw|ConvertFrom-Json
 Check (-not $state.cleaned -and $state.keepDatabase) 'Failed cleanup keeps the manifest available for recovery.'
 Remove-TestFixture -Database $lifecycleDatabase -Manifest $lifecycleManifest -DropDatabase {param($database) if($database -cne $lifecycleDatabase){throw 'Wrong fixture passed to cleanup.'}}
 $state=Get-Content -LiteralPath $lifecycleManifest -Raw|ConvertFrom-Json
 Check ($state.cleaned -and -not $state.keepDatabase) 'Successful cleanup marks the manifest as cleaned and no longer retained.'
 Reject {& (Join-Path $root 'scripts/Run-ShowcaseDemo.ps1') -Manifest $lifecycleManifest} 'Launcher refuses a cleaned fixture.' 'Manifest is not a ready, retained showcase.'
 $state.cleaned=$false
 $state|Add-Member -NotePropertyName server -NotePropertyValue '.'
 $state|Add-Member -NotePropertyName output -NotePropertyValue $lifecycleFolder
 $state|Add-Member -NotePropertyName credentialFile -NotePropertyValue (Join-Path $root 'accounts.private.txt')
 $state|Add-Member -NotePropertyName quickLoginFile -NotePropertyValue (Join-Path $lifecycleFolder 'quick-login.dat')
 $state|Add-Member -NotePropertyName executable -NotePropertyValue (Join-Path $lifecycleFolder 'app.exe')
 $state|ConvertTo-Json|Set-Content -LiteralPath $lifecycleManifest
 Reject {& (Join-Path $root 'scripts/Run-ShowcaseDemo.ps1') -Manifest $lifecycleManifest} 'Launcher refuses a path outside the exact fixture folder before starting a process.' 'Manifest path escapes its exact showcase folder.'
 Reject {Remove-TestFixture -Database 'DangKyTinChi_Minh' -Manifest $lifecycleManifest -DropDatabase {throw 'Must never execute.'}} 'Cleanup rejects the main database before SQL.' 'Unsafe cleanup target.'
 Reject {Remove-TestFixture -Database $lifecycleDatabase -Manifest (Join-Path $root 'manifest.json') -DropDatabase {throw 'Must never execute.'}} 'Cleanup rejects a manifest outside the exact fixture folder.' 'Cleanup manifest must belong to the exact fixture.'
} finally {
 if(Test-Path -LiteralPath $lifecycleManifest){Remove-Item -LiteralPath $lifecycleManifest}
 foreach($file in @('results.json','error.txt','evidence.txt')){if(Test-Path -LiteralPath (Join-Path $lifecycleFolder $file)){Remove-Item -LiteralPath (Join-Path $lifecycleFolder $file)}}
 [IO.Directory]::Delete($lifecycleFolder)
}
if(-not $SqlIntegration){return}
$name='DangKyTinChi_Minh_Test_Showcase_'+[Guid]::NewGuid().ToString('N')
$result=$null
try {
 $out=&$script -Server $Server -DatabaseName $name -VerifyNative:$Native -Ephemeral:$Native -NoSelect
 $result=($out|Where-Object{$_ -is [string] -and $_.StartsWith('{"manifest":')}|Select-Object -Last 1)|ConvertFrom-Json
 Check ($null-ne $result) 'Provisioning returned a private manifest.'
 $manifest=Get-Content -LiteralPath $result.manifest -Raw|ConvertFrom-Json
 Check ($manifest.passed -and $manifest.synthetic -and $manifest.database -ceq $name) 'Ready fixture has an explicit synthetic marker and exact ownership.'
 if($Native){Check $manifest.cleaned 'Native test removes its exact ephemeral fixture.';return}
 Reject { &$script -Server $Server -DatabaseName $name } 'An existing showcase database is never upgraded or overwritten.'
 $count=&sqlcmd -S $Server -E -C -b -x -h -1 -W -d $name -Q "SET NOCOUNT ON;SELECT COUNT(*) FROM dbo.HV_DOCUMENT WHERE kind='RESULT' AND JSON_VALUE(payload,'$.source') LIKE 'SYNTHETIC DEMO%';"
 Check ($LASTEXITCODE-eq 0 -and ($count|Out-String).Trim()-eq '2') 'Recognized evidence is sourced explicitly to synthetic demo data.'
} finally {
 if($null-ne $result -and -not $Native){Remove-TestFixture -Database $name -Manifest $result.manifest}
}
