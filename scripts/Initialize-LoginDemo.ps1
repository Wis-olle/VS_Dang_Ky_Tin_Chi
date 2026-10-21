[CmdletBinding()]
param(
    [ValidatePattern('^(localhost|\.|\(local\))(\\[A-Za-z0-9_]+)?$')][string]$Server = '.',
    [ValidatePattern('^DangKyTinChi_Minh(_Test_[A-Za-z0-9_]+)?$')][string]$DatabaseName = 'DangKyTinChi_Minh',
    [string]$StudentPassword,
    [string]$TrainingPassword,
    [string]$CredentialFile,
    [switch]$ResetPasswords
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$credentialRoot = [IO.Path]::GetFullPath((Join-Path $root '.tmp')) + [IO.Path]::DirectorySeparatorChar
$handoff = if ($CredentialFile) { [IO.Path]::GetFullPath($CredentialFile) } else { Join-Path $root '.tmp/TaiKhoanMau.txt' }
if (-not $handoff.StartsWith($credentialRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Tệp tài khoản phải nằm trong .tmp của workspace.' }
if ((Test-Path -LiteralPath $handoff) -and -not $ResetPasswords) { throw 'Tệp tài khoản đã tồn tại. Chọn -CredentialFile khác để giữ thông tin cũ.' }
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($handoff)) | Out-Null
$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    $candidate = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE'
    if (-not (Test-Path -LiteralPath $candidate)) { throw 'Cần sqlcmd.' }
    $sqlcmdPath = $candidate
} else { $sqlcmdPath = $sqlcmd.Source }
$preflight = @'
SET NOCOUNT ON;
IF OBJECT_ID('dbo.CAMPUS_SCHEMA_VERSION','U') IS NULL OR OBJECT_ID('dbo.APP_CREDENTIAL','U') IS NULL
 OR OBJECT_ID('dbo.usp_Auth_Begin','P') IS NULL OR OBJECT_ID('dbo.usp_SinhVien_Luu','P') IS NULL
 THROW 52502,N'Chạy Initialize-Database.ps1 -UpgradeExisting để sao lưu và nâng cấp schema trước.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_SCHEMA_VERSION WHERE id=1 AND version=4)
 THROW 52502,N'Schema chưa tương thích; nâng cấp database trước khi tạo tài khoản mẫu.',1;
DECLARE @health TABLE(version int);
INSERT @health EXEC dbo.usp_Campus_KiemTraSchema;
IF NOT EXISTS(SELECT 1 FROM @health WHERE version=4)
 THROW 52502,N'Thiếu hợp đồng schema Campus; chưa thay đổi tài khoản mẫu.',1;
'@
& $sqlcmdPath -S $Server -E -C -I -b -x -l 5 -f 65001 -d $DatabaseName -Q $preflight
if ($LASTEXITCODE -ne 0) { throw 'Schema chưa đủ; chưa thay đổi tài khoản hoặc mật khẩu mẫu.' }
function New-DemoPassword { 'Demo!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(6)) + 'a7' }
if (-not $StudentPassword) { $StudentPassword = New-DemoPassword }
if (-not $TrainingPassword) { $TrainingPassword = New-DemoPassword }
if ($StudentPassword.Length -lt 12 -or $TrainingPassword.Length -lt 12) { throw 'Mật khẩu mẫu cần ít nhất 12 ký tự.' }
$temp = Join-Path $root ('.tmp/login-setup/' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temp) | Out-Null
$reset = if ($ResetPasswords) { 1 } else { 0 }
$created = @()
$accountRows = @()
foreach ($account in @(
    @{Username='sv2024001';Role='SINH_VIEN';Student="'SV2024001'";Password=$StudentPassword},
    @{Username='daotao';Role='DAO_TAO';Student='NULL';Password=$TrainingPassword}
)) {
    $salt = [Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
    $key = [Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2($account.Password, $salt, 600000, [Security.Cryptography.HashAlgorithmName]::SHA256, 32)
    $saltHex = [Convert]::ToHexString($salt)
    $keyHex = [Convert]::ToHexString($key)
    $storedHash = 'PBKDF2-SHA256$600000$' + [Convert]::ToBase64String($salt) + '$' + [Convert]::ToBase64String($key)
    # Only password-derived data reaches SQL; plaintext stays in the local handoff.
    $accountRows += "(N'$($account.Username)','$($account.Role)',$($account.Student),N'$storedHash',0x$saltHex,0x$keyHex)"
    [Security.Cryptography.CryptographicOperations]::ZeroMemory($key)
    $created += "$($account.Username) | $($account.Password) | $($account.Role)"
}

# A durable, unique rescue file must exist before SQL can change either account.
# A timeout cannot tell us whether COMMIT reached SQL, so keep this file on every failure.
$pending = $handoff + '.' + [Guid]::NewGuid().ToString('N') + '.pending'
$stream = $null
$bytes = $null
try {
    $stream = [IO.FileStream]::new($pending, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    $content = (@("Server: $Server", "Database: $DatabaseName", 'Tên đăng nhập | Mật khẩu | Vai trò') + $created) -join [Environment]::NewLine
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($content + [Environment]::NewLine)
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Flush($true)
}
catch { throw "Không ghi được tệp bàn giao trước khi thay đổi SQL. Tài khoản chưa được cập nhật. Đường dẫn: $pending" }
finally {
    if ($stream) { $stream.Dispose() }
    if ($bytes) { [Security.Cryptography.CryptographicOperations]::ZeroMemory($bytes) }
}

$rows = $accountRows -join ",`n"
$sql = @"
SET NOCOUNT ON; SET XACT_ABORT ON;
DECLARE @accounts TABLE(username nvarchar(128) PRIMARY KEY,role varchar(20),student varchar(20),stored_hash nvarchar(500),salt varbinary(16),verifier varbinary(32));
INSERT @accounts VALUES $rows;
BEGIN TRY
 BEGIN TRAN;
 IF NOT EXISTS(SELECT 1 FROM dbo.SINH_VIEN WITH(HOLDLOCK) WHERE ma_sv='SV2024001' AND trang_thai=1)
  THROW 52110,N'Cần dữ liệu mẫu SV2024001 hoạt động trước khi tạo tài khoản.',1;
 -- Lock existing accounts and the missing username ranges before validating both.
 DECLARE @locked int;
 SELECT @locked=t.ma_tk FROM dbo.TAI_KHOAN t WITH(UPDLOCK,HOLDLOCK)
  WHERE t.ten_dang_nhap IN(N'sv2024001',N'daotao') ORDER BY t.ten_dang_nhap;
 IF EXISTS(SELECT 1 FROM @accounts a JOIN dbo.TAI_KHOAN t ON t.ten_dang_nhap=a.username
  WHERE t.vai_tro<>a.role OR COALESCE(t.ma_sv,'')<>COALESCE(a.student,''))
  THROW 52111,N'Tài khoản mẫu đã có ánh xạ khác. Không tự ghi đè.',1;
 IF $reset=0 AND EXISTS(SELECT 1 FROM @accounts a JOIN dbo.TAI_KHOAN t ON t.ten_dang_nhap=a.username
  JOIN dbo.APP_CREDENTIAL c WITH(UPDLOCK,HOLDLOCK) ON c.ma_tk=t.ma_tk)
  THROW 52112,N'Tài khoản mẫu đã có mật khẩu; dùng -ResetPasswords nếu cần đặt lại.',1;
 INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro,ma_sv,trang_thai)
  SELECT a.username,a.role,a.student,1 FROM @accounts a
  WHERE NOT EXISTS(SELECT 1 FROM dbo.TAI_KHOAN t WHERE t.ten_dang_nhap=a.username);
 UPDATE t SET mat_khau_hash=a.stored_hash FROM dbo.TAI_KHOAN t JOIN @accounts a ON a.username=t.ten_dang_nhap;
 UPDATE c SET salt=a.salt,verifier=a.verifier,iterations=600000,failed_count=0,locked_until=NULL
  FROM dbo.APP_CREDENTIAL c JOIN dbo.TAI_KHOAN t ON t.ma_tk=c.ma_tk JOIN @accounts a ON a.username=t.ten_dang_nhap;
 INSERT dbo.APP_CREDENTIAL(ma_tk,salt,verifier,iterations)
  SELECT t.ma_tk,a.salt,a.verifier,600000 FROM @accounts a JOIN dbo.TAI_KHOAN t ON t.ten_dang_nhap=a.username
  WHERE NOT EXISTS(SELECT 1 FROM dbo.APP_CREDENTIAL c WITH(UPDLOCK,HOLDLOCK) WHERE c.ma_tk=t.ma_tk);
 DELETE s FROM dbo.APP_SESSION s JOIN dbo.TAI_KHOAN t ON t.ma_tk=s.ma_tk JOIN @accounts a ON a.username=t.ten_dang_nhap;
 COMMIT;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
"@
$submitted = $false; $committed = $false
try {
    $path = Join-Path $temp 'accounts.sql'
    [IO.File]::WriteAllText($path,$sql,[Text.UTF8Encoding]::new($false))
    $submitted = $true
    & $sqlcmdPath -S $Server -E -C -I -b -x -l 5 -t 60 -f 65001 -d $DatabaseName -i $path
    if ($LASTEXITCODE -ne 0) { throw 'SQL chưa xác nhận cấp tài khoản thành công.' }
    $committed = $true
    if ($ResetPasswords -and [IO.File]::Exists($handoff)) {
        # Replace atomically and retain the previous handoff until the new one is durable.
        $previous = $handoff + '.previous-' + [Guid]::NewGuid().ToString('N')
        [IO.File]::Replace($pending, $handoff, $previous)
        Write-Host "Đã giữ bản bàn giao cũ tại $previous"
    }
    else {
        # Move without overwrite is also the final race check for a new destination.
        [IO.File]::Move($pending, $handoff, $false)
    }
}
catch {
    $status = if ($committed) { 'SQL đã xác nhận cập nhật, nhưng chưa xuất được tệp bàn giao.' }
        elseif ($submitted) { 'SQL chưa xác nhận thành công; kết quả commit có thể chưa xác định.' }
        else { 'Chưa gửi thay đổi tài khoản đến SQL.' }
    throw "$status Không tự chạy reset. Giữ tệp mật khẩu dự kiến tại $pending để đối chiếu; tệp bàn giao cũ được giữ nguyên nếu chưa thay thế thành công."
}
Write-Host "Đã tạo 2 tài khoản mẫu. Thông tin đăng nhập tại $handoff"
