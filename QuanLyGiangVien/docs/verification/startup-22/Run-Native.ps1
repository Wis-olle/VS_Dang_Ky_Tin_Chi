#requires -Version 7.4
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'; Set-StrictMode -Version Latest
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$id=[Guid]::NewGuid().ToString('N')
$database='DangKyTinChi_Minh_Test_Showcase_'+$id
$fixture=Join-Path $repo ('.tmp/showcase-20/'+$id)
$output=Join-Path $repo ('.tmp/startup-22/'+$id)
[IO.Directory]::CreateDirectory($output)|Out-Null
$summary=[ordered]@{passed=$false;runs=@();cleaned=$false;error=$null}
try {
 & (Join-Path $repo 'scripts/Initialize-ShowcaseDemo.ps1') -DatabaseName $database -NoSelect *> (Join-Path $output 'provision.log')
 $manifest=Get-Content -LiteralPath (Join-Path $fixture 'manifest.json') -Raw|ConvertFrom-Json
 if(-not $manifest.passed -or $manifest.database -cne $database -or -not $manifest.synthetic){throw 'Fixture provisioning failed.'}
 $entropy=[Text.Encoding]::UTF8.GetBytes('Campus.QuickLogin.v1'); $plain=$null; $badBytes=$null
 try {
  $plain=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($manifest.quickLoginFile),$entropy,[Security.Cryptography.DataProtectionScope]::CurrentUser)
  $payload=[Text.Encoding]::UTF8.GetString($plain)|ConvertFrom-Json
  $payload.training.password=[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
  $badBytes=[Text.Encoding]::UTF8.GetBytes(($payload|ConvertTo-Json -Depth 5 -Compress))
  $badFile=Join-Path $fixture 'rejected-login.dat'
  [IO.File]::WriteAllBytes($badFile,[Security.Cryptography.ProtectedData]::Protect($badBytes,$entropy,[Security.Cryptography.DataProtectionScope]::CurrentUser))
 } finally {
  if($plain){[Security.Cryptography.CryptographicOperations]::ZeroMemory($plain)}
  if($badBytes){[Security.Cryptography.CryptographicOperations]::ZeroMemory($badBytes)}
 }
 foreach($scenario in @('success','missing','rejected')) {
  $checkOutput=Join-Path $output $scenario; [IO.Directory]::CreateDirectory($checkOutput)|Out-Null
  $start=[Diagnostics.ProcessStartInfo]::new($manifest.executable); $start.UseShellExecute=$false; $start.CreateNoWindow=$true
  foreach($key in @($start.Environment.Keys)){if($key -match '(UI_CHECK$|SCREEN_CHECK$|^CAMPUS_STARTUP_|^MINH_DB_CONNECTION$|^CAMPUS_QUICK_LOGIN_FILE$)'){$start.Environment.Remove($key)|Out-Null}}
  $start.Environment['MINH_DB_CONNECTION']="Server=.;Database=$database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5"
  $start.Environment['CAMPUS_QUICK_LOGIN_FILE']=switch($scenario){'success'{$manifest.quickLoginFile}'missing'{Join-Path $fixture 'absent.dat'}'rejected'{$badFile}}
  $start.Environment['CAMPUS_STARTUP_UI_CHECK']=$checkOutput
  $start.Environment['CAMPUS_STARTUP_EXPECT']=if($scenario -eq 'success'){'success'}else{'fallback'}
  $start.Environment['CAMPUS_STARTUP_VALID_FILE']=$manifest.quickLoginFile
  $start.ArgumentList.Add('--database')
  $process=[Diagnostics.Process]::Start($start)
  try {
   if(-not $process.WaitForExit(60000)){$process.Kill($true);throw 'Only the startup-check process was stopped after timeout.'}
   $result=Get-Content -LiteralPath (Join-Path $checkOutput 'results.json') -Raw|ConvertFrom-Json
   if($process.ExitCode -ne 0 -or -not $result.passed){throw "Startup verification failed: $scenario"}
   $summary.runs+=@{scenario=$scenario;passed=$true;checks=@($result.checks).Count}
  } finally {$process.Dispose()}
 }
 $summary.passed=$true
} catch {$summary.error=$_.Exception.Message;throw}
finally {
 $manifestPath=Join-Path $fixture 'manifest.json'
 if(Test-Path -LiteralPath $manifestPath){
  $owned=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
  if($database -cnotmatch '^DangKyTinChi_Minh_Test_Showcase_[a-f0-9]{32}$' -or $owned.database -cne $database -or -not $owned.synthetic -or $owned.server -cne '.'){throw 'Refusing cleanup of an unowned fixture.'}
  &sqlcmd -S '.' -E -C -b -x -l 5 -d master -Q "IF DB_ID(N'$database') IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; END;" *> (Join-Path $output 'cleanup.log')
  if($LASTEXITCODE -ne 0){throw 'Exact startup fixture cleanup failed.'}
  $summary.cleaned=$true;$owned.cleaned=$true;$owned|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $manifestPath -Encoding utf8
 }
 $summary|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $output 'summary.json') -Encoding utf8
}
Write-Output "Startup verification passed. Evidence: $output"
