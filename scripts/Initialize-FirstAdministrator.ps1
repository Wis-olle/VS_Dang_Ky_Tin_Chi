#requires -Version 7.4
[CmdletBinding()]
param(
    [ValidatePattern('^(localhost|\.|\(local\))(\\[A-Za-z0-9_]+)?$')][string]$Server = '.',
    [ValidatePattern('^DangKyTinChi_Minh(_Test_[A-Za-z0-9_]+)?$')][string]$DatabaseName = 'DangKyTinChi_Minh',
    [switch]$CreateFirstAdministrator,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_.-]{2,63}$')][string]$Username,
    [Parameter(Mandatory)][ValidateLength(1,500)][string]$Reason,
    [string]$CredentialFile
)
$ErrorActionPreference = 'Stop'
if (-not $CreateFirstAdministrator) { throw 'Chỉ khởi tạo khi chủ động chỉ định -CreateFirstAdministrator.' }
if ([string]::IsNullOrWhiteSpace($Reason)) { throw 'Cần lý do/ủy quyền khởi tạo quản trị.' }
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$privateRoot = [IO.Path]::GetFullPath((Join-Path $root '.tmp')) + [IO.Path]::DirectorySeparatorChar
$handoff = if ($CredentialFile) { [IO.Path]::GetFullPath($CredentialFile) } else { Join-Path $privateRoot ('FirstAdministrator-' + [Guid]::NewGuid().ToString('N') + '.txt') }
if (-not $handoff.StartsWith($privateRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Tệp bàn giao phải nằm trong .tmp của workspace.' }
if (Test-Path -LiteralPath $handoff) { throw 'Không ghi đè tệp bàn giao đã tồn tại.' }
$sqlcmd = (Get-Command sqlcmd -ErrorAction Stop).Source
$sqlRoot = Join-Path $privateRoot ('first-administrator/' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($sqlRoot) | Out-Null
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($handoff)) | Out-Null
function Protect-PrivateFile([string]$path) {
    $acl = [Security.AccessControl.FileSecurity]::new()
    $acl.SetAccessRuleProtection($true,$false)
    $acl.SetOwner([Security.Principal.WindowsIdentity]::GetCurrent().User)
    foreach ($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))) {
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,[Security.AccessControl.FileSystemRights]::FullControl,[Security.AccessControl.AccessControlType]::Allow))
    }
    Set-Acl -LiteralPath $path -AclObject $acl
}
# Generate a new credential only; this entry point never promotes/reset existing identities.
$password = 'Admin!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(16)) + 'a7'
$salt = [Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
$key = [Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2($password,$salt,600000,[Security.Cryptography.HashAlgorithmName]::SHA256,32)
$pending = $handoff + '.' + [Guid]::NewGuid().ToString('N') + '.pending'
$stream = $null; $submitted = $false; $committed = $false
try {
    $stream = [IO.FileStream]::new($pending,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    # Limit read access before placing any plaintext credential in this file.
    Protect-PrivateFile $pending
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes("Server: $Server`nDatabase: $DatabaseName`nTên đăng nhập | Mật khẩu | Vai trò`n$Username | $password | QUAN_TRI`n")
    try { $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) }
    finally { [Security.Cryptography.CryptographicOperations]::ZeroMemory($bytes); $stream.Dispose(); $stream = $null; $password = $null }
    $saltHex = [Convert]::ToHexString($salt); $keyHex = [Convert]::ToHexString($key)
    $reasonSql = $Reason.Trim().Replace("'","''")
    $sql = @"
SET NOCOUNT ON; SET XACT_ABORT ON;
IF ISNULL(IS_SRVROLEMEMBER('sysadmin'),0)<>1 AND ISNULL(IS_MEMBER('db_owner'),0)<>1
 THROW 52980,N'Khởi tạo quản trị đầu tiên yêu cầu chủ sở hữu database hoặc quản trị SQL.',1;
DECLARE @health TABLE(version int);
INSERT @health EXEC dbo.usp_Campus_KiemTraSchema;
IF NOT EXISTS(SELECT 1 FROM @health WHERE version=4) THROW 52980,N'Cần schema Campus 4 tương thích trước khi khởi tạo quản trị.',1;
BEGIN TRY
 BEGIN TRAN;
 DECLARE @rc int,@id int;
 EXEC @rc=sys.sp_getapplock @Resource=N'Campus:Accounts',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @rc<0 THROW 52980,N'Tài khoản đang được cập nhật; thử lại sau.',1;
 IF EXISTS(SELECT 1 FROM dbo.TAI_KHOAN WITH(UPDLOCK,HOLDLOCK) WHERE vai_tro='QUAN_TRI')
  THROW 52981,N'Đã có quản trị; dùng quy trình quản trị hiện có, không bootstrap thêm.',1;
 IF EXISTS(SELECT 1 FROM dbo.TAI_KHOAN WITH(UPDLOCK,HOLDLOCK) WHERE ten_dang_nhap=N'$Username')
  THROW 52982,N'Tên đăng nhập đã tồn tại; không nâng quyền hoặc ghi đè danh tính.',1;
 INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro,ma_sv,trang_thai) VALUES(N'$Username','QUAN_TRI',NULL,1);
 SET @id=CONVERT(int,SCOPE_IDENTITY());
 INSERT dbo.APP_CREDENTIAL(ma_tk,salt,verifier,iterations) VALUES(@id,0x$saltHex,0x$keyHex,600000);
 INSERT dbo.ACCOUNT_AUDIT(actor,target,action,reason,before_state,after_state)
 VALUES(@id,@id,'BOOTSTRAP',N'$reasonSql',NULL,CONCAT(N'QUAN_TRI|1|Windows bootstrap: ',ORIGINAL_LOGIN()));
 COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
"@
    $sqlPath = Join-Path $sqlRoot 'create.sql'
    [IO.File]::WriteAllText($sqlPath,'',[Text.UTF8Encoding]::new($false)); Protect-PrivateFile $sqlPath
    [IO.File]::WriteAllText($sqlPath,$sql,[Text.UTF8Encoding]::new($false))
    $submitted = $true
    & $sqlcmd -S $Server -E -C -I -b -x -l 5 -t 60 -f 65001 -d $DatabaseName -i $sqlPath
    if ($LASTEXITCODE -ne 0) { throw 'SQL chưa xác nhận khởi tạo quản trị.' }
    $committed = $true
    [IO.File]::Move($pending,$handoff,$false)
    Write-Host "Đã khởi tạo quản trị đầu tiên. Bàn giao riêng tại $handoff; đăng nhập cổng PĐT rồi đổi mật khẩu."
} catch {
    $state = if ($committed) { 'SQL đã commit nhưng chưa xuất được tệp bàn giao.' } elseif ($submitted) { 'Chưa xác nhận được kết quả SQL.' } else { 'Chưa gửi thay đổi tới SQL.' }
    throw "$state Không tự chạy lại/reset. Giữ tệp bàn giao dự kiến $pending để đối soát."
} finally {
    if ($stream) { $stream.Dispose() }
    [Security.Cryptography.CryptographicOperations]::ZeroMemory($key)
    $password = $null
}
