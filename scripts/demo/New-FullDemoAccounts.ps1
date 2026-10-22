#requires -Version 7.4
[CmdletBinding()]
param(
 [ValidatePattern('^(localhost|\.|\(local\))$')][string]$Server='.',
 [Parameter(Mandatory)][ValidatePattern('^DangKyTinChi_Minh_Test_Showcase_[a-f0-9]{32}$')][string]$DatabaseName,
 [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$expected=[IO.Path]::GetFullPath((Join-Path $repo ('.tmp/showcase-20/'+$DatabaseName.Substring(32))))
if([IO.Path]::GetFullPath($OutputDirectory) -cne $expected -or -not(Test-Path -LiteralPath (Join-Path $expected 'manifest.json'))){throw 'Use the exact owned showcase manifest folder.'}
$state=Get-Content -LiteralPath (Join-Path $expected 'manifest.json') -Raw|ConvertFrom-Json
if($state.database -cne $DatabaseName -or -not $state.synthetic -or $state.cleaned -or $state.passed){throw 'Additional accounts require a new showcase being provisioned.'}
$handoff=Join-Path $expected 'test-accounts.private.txt'
$sqlFile=Join-Path $expected 'test-accounts.private.sql'
if((Test-Path -LiteralPath $handoff) -or (Test-Path -LiteralPath $sqlFile)){throw 'Never overwrite test accounts or their handoff.'}
function Protect([string]$path){
 $acl=[Security.AccessControl.FileSecurity]::new();$acl.SetAccessRuleProtection($true,$false)
 $acl.SetOwner([Security.Principal.WindowsIdentity]::GetCurrent().User)
 foreach($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','Allow'))}
 Set-Acl -LiteralPath $path -AclObject $acl
}
foreach($path in @($handoff,$sqlFile)){
 $file=[IO.File]::Open($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None);$file.Dispose();Protect $path
}
$rows=[Collections.Generic.List[string]]::new()
$accounts=[Collections.Generic.List[string]]::new()
foreach($n in 2..10){
 $username=if($n -eq 10){'daotao-xem'}else{'sv2024'+$n.ToString('000')}
 $role=if($n -eq 10){'DAO_TAO'}else{'SINH_VIEN'}
 $student=if($n -eq 10){'NULL'}else{"'SV2024$($n.ToString('000'))'"}
 $active=if($n -eq 9){0}else{1}
 $password='Test!'+[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(10))+'a7'
 $salt=[Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
 $key=[Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2($password,$salt,600000,[Security.Cryptography.HashAlgorithmName]::SHA256,32)
 try {
  $accounts.Add("$username | $password | $role | active=$active")
  $rows.Add("EXEC dbo.usp_TaiKhoan_Luu @ten_dang_nhap=N'$username',@vai_tro='$role',@ma_sv=$student,@trang_thai=$active,@ly_do=N'SYNTHETIC DEMO - tài khoản thử riêng theo tình huống',@salt=0x$([Convert]::ToHexString($salt)),@verifier=0x$([Convert]::ToHexString($key));")
 }finally{[Security.Cryptography.CryptographicOperations]::ZeroMemory($key);[Security.Cryptography.CryptographicOperations]::ZeroMemory($salt);$password=$null}
}
# The restricted durable handoff is written and flushed before any SQL account mutation.
$content=(@("Server: $Server","Database: $DatabaseName",'Tên đăng nhập | Mật khẩu | Vai trò | Trạng thái')+$accounts)-join [Environment]::NewLine
$bytes=[Text.UTF8Encoding]::new($false).GetBytes($content+[Environment]::NewLine)
$stream=[IO.File]::Open($handoff,[IO.FileMode]::Open,[IO.FileAccess]::Write,[IO.FileShare]::None)
try{$stream.Write($bytes);$stream.Flush($true)}finally{$stream.Dispose();[Security.Cryptography.CryptographicOperations]::ZeroMemory($bytes);$content=$null;$accounts.Clear()}
$body=$rows -join [Environment]::NewLine
$query=@"
SET NOCOUNT ON;SET XACT_ABORT ON;
IF DB_NAME()<>N'$DatabaseName' THROW 54980,N'Wrong owned showcase database.',1;
BEGIN TRY BEGIN TRAN;
DECLARE @actor int=(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap=ORIGINAL_LOGIN()),@role varchar(20),@student varchar(20);
IF @actor IS NULL THROW 54984,N'Missing local identity.',1;
SELECT @role=vai_tro,@student=ma_sv FROM dbo.TAI_KHOAN WHERE ma_tk=@actor;
UPDATE dbo.TAI_KHOAN SET vai_tro='QUAN_TRI',ma_sv=NULL WHERE ma_tk=@actor;
$body
UPDATE dbo.TAI_KHOAN SET vai_tro=@role,ma_sv=@student WHERE ma_tk=@actor;
COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
"@
[IO.File]::WriteAllText($sqlFile,$query,[Text.UTF8Encoding]::new($false))
& sqlcmd -S $Server -E -C -I -b -x -l 5 -t 90 -f 65001 -d $DatabaseName -i $sqlFile *> (Join-Path $expected 'test-accounts.log')
if($LASTEXITCODE-ne 0){throw 'Additional account creation failed; preserve private handoff and inspect test-accounts.log.'}
Write-Output 'Created nine synthetic test accounts; credentials remain in the private demo folder.'
