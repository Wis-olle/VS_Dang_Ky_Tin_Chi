#requires -Version 7.4
[CmdletBinding()]
param([string]$Manifest,[switch]$Verify)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
function Reset-VerificationOutput([string]$Directory){
 # Only outcome files are replaced; preserve screenshots and previous evidence.
 foreach($name in @('results.json','error.txt')){
  $file=Join-Path $Directory $name
  if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}
 }
}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$privateRoot=[IO.Path]::GetFullPath((Join-Path $repo '.tmp/showcase-20'))+[IO.Path]::DirectorySeparatorChar
if([string]::IsNullOrWhiteSpace($Manifest)){
 $pointer=Join-Path $privateRoot 'current-manifest-path.txt'
 if(-not(Test-Path -LiteralPath $pointer)){throw 'Chưa chọn bản trình diễn. Chạy Initialize-ShowcaseDemo.ps1 rồi dùng manifest được trả về.'}
 $Manifest=([IO.File]::ReadAllText($pointer)).Trim()
 if([string]::IsNullOrWhiteSpace($Manifest)){throw 'Đường dẫn bản trình diễn đang trống.'}
}
$path=[IO.Path]::GetFullPath($Manifest)
if(-not $path.StartsWith($privateRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Use the private showcase manifest in this workspace.'}
$data=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
if(-not $data.passed -or $data.cleaned -or -not $data.synthetic -or $data.database -cnotmatch '^DangKyTinChi_Minh_Test_Showcase_[a-f0-9]{32}$' -or $data.server -notmatch '^(localhost|\.|\(local\))$'){throw 'Manifest is not a ready, retained showcase.'}
$expectedRoot=Join-Path $privateRoot $data.database.Substring('DangKyTinChi_Minh_Test_Showcase_'.Length)
if($path -cne (Join-Path $expectedRoot 'manifest.json')){throw 'Manifest must be in its exact showcase folder.'}
foreach($field in @('output','credentialFile','quickLoginFile','executable')) {
 $value=[IO.Path]::GetFullPath([string]$data.$field)
 if($value -cne $expectedRoot -and -not $value.StartsWith($expectedRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Manifest path escapes its exact showcase folder.'}
}
foreach($field in @('credentialFile','quickLoginFile','executable')){
 if(-not(Test-Path -LiteralPath $data.$field -PathType Leaf)){throw "Bản trình diễn thiếu tệp $field. Tạo lại một bản riêng bằng Initialize-ShowcaseDemo.ps1."}
}
$start=[Diagnostics.ProcessStartInfo]::new($data.executable);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($entry in @($start.Environment.Keys)){if($entry -match '(UI_CHECK$|^DEMO_.*PASSWORD$|^CAMPUS_QUICK_LOGIN_FILE$|^MINH_DB_CONNECTION$)'){$start.Environment.Remove($entry)|Out-Null}}
$start.Environment['MINH_DB_CONNECTION']="Server=$($data.server);Database=$($data.database);Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5"
$start.Environment['CAMPUS_QUICK_LOGIN_FILE']=$data.quickLoginFile
$start.Environment['CAMPUS_SHOWCASE_DEMO']='1'
$start.ArgumentList.Add('--database');$start.ArgumentList.Add('--student-portal')
if($Verify){
 $native=Join-Path $data.output 'native';[IO.Directory]::CreateDirectory($native)|Out-Null
 Reset-VerificationOutput $native
 if($data.PSObject.Properties.Name -contains 'fullData' -and $data.fullData){
  $start.Environment['CAMPUS_FULL_DEMO_UI_CHECK']=$native
  $start.Environment['CAMPUS_FULL_DEMO_DATABASE']=$data.database
  $start.Environment['CAMPUS_FULL_DEMO_IMPORT_ROOT']=Join-Path $repo 'QuanLyGiangVien/docs/demo-data/22'
 }else{$start.Environment['CAMPUS_SHOWCASE_UI_CHECK']=$native}
 $lines=[IO.File]::ReadAllLines($data.credentialFile)
 foreach($pair in @(@('sv2024001','DEMO_STUDENT_PASSWORD'),@('daotao','DEMO_TRAINING_PASSWORD'))){
  $account=@($lines|Where-Object{($_ -split '\|',3)[0].Trim()-ceq $pair[0]})
  if($account.Count-ne 1){throw 'Missing exact showcase credential.'}
  $start.Environment[$pair[1]]=($account[0] -split '\|',3)[1].Trim()
 }
}
$verificationStartedUtc=[DateTime]::UtcNow
$process=[Diagnostics.Process]::Start($start)
try {
 if($Verify){
  if(-not $process.WaitForExit(180000)){$process.Kill($true);throw 'Only the native showcase process was stopped after timeout.'}
  $result=Join-Path $native 'results.json'
  if($process.ExitCode-ne 0 -or -not(Test-Path -LiteralPath $result)){throw 'Native showcase did not complete.'}
  if([IO.File]::GetLastWriteTimeUtc($result)-lt $verificationStartedUtc){throw 'Native showcase returned a stale result.'}
  $checks=Get-Content -LiteralPath $result -Raw|ConvertFrom-Json
  if(-not $checks.passed){throw 'Native showcase failed; see native/error.txt.'}
  Write-Output "Native showcase passed $(@($checks.checks).Count) checks."
 }else{Write-Output "Opened the dedicated synthetic showcase. Process: $($process.Id)"}
} finally {$process.Dispose();foreach($key in @('DEMO_STUDENT_PASSWORD','DEMO_TRAINING_PASSWORD')){$start.Environment.Remove($key)|Out-Null}}
