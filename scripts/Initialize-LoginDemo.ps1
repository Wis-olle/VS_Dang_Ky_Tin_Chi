[CmdletBinding()]
param(
    [ValidatePattern('^(localhost|\.|\(local\))(\\[A-Za-z0-9_]+)?$')][string]$Server = '.\SQLEXPRESS',
    [ValidatePattern('^DangKyTinChi_Minh(_Test_[A-Za-z0-9_]+)?$')][string]$DatabaseName = 'DangKyTinChi_Minh',
    [string]$StudentPassword,
    [string]$TrainingPassword,
    [switch]$ResetPasswords
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    $candidate = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE'
    if (-not (Test-Path -LiteralPath $candidate)) { throw 'Cần sqlcmd.' }
    $sqlcmdPath = $candidate
} else { $sqlcmdPath = $sqlcmd.Source }
foreach ($migration in @('08_PasswordLogin.sql','09_Students.sql')) {
    & $sqlcmdPath -S $Server -E -C -I -b -f 65001 -d $DatabaseName -i (Join-Path $root ('QuanLyGiangVien/database/' + $migration))
    if ($LASTEXITCODE -ne 0) { throw "Migration $migration thất bại." }
}
function New-DemoPassword { 'Demo!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(6)) + 'a7' }
if (-not $StudentPassword) { $StudentPassword = New-DemoPassword }
if (-not $TrainingPassword) { $TrainingPassword = New-DemoPassword }
if ($StudentPassword.Length -lt 12 -or $TrainingPassword.Length -lt 12) { throw 'Mật khẩu mẫu cần ít nhất 12 ký tự.' }
$temp = Join-Path $root '.tmp/login-setup'
[IO.Directory]::CreateDirectory($temp) | Out-Null
$reset = if ($ResetPasswords) { 1 } else { 0 }
$created = @()
foreach ($account in @(
    @{Username='sv2024001';Role='SINH_VIEN';Student="'SV2024001'";Password=$StudentPassword},
    @{Username='daotao';Role='DAO_TAO';Student='NULL';Password=$TrainingPassword}
)) {
    $salt = [Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
    $key = [Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2($account.Password, $salt, 600000, [Security.Cryptography.HashAlgorithmName]::SHA256, 32)
    $saltHex = [Convert]::ToHexString($salt)
    $keyHex = [Convert]::ToHexString($key)
    $storedHash = 'PBKDF2-SHA256$600000$' + [Convert]::ToBase64String($salt) + '$' + [Convert]::ToBase64String($key)
    # Only password-derived data reaches the SQL execution file; plaintext stays in the local demo handoff.
    $sql = @"
SET NOCOUNT ON; SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SINH_VIEN WHERE ma_sv='SV2024001' AND trang_thai=1)
 THROW 52110,N'Cần dữ liệu mẫu SV2024001 hoạt động trước khi tạo tài khoản.',1;
BEGIN TRAN;
DECLARE @id int;
SELECT @id=ma_tk FROM dbo.TAI_KHOAN WITH(UPDLOCK,HOLDLOCK) WHERE ten_dang_nhap=N'$($account.Username)';
IF @id IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.TAI_KHOAN WHERE ma_tk=@id
 AND (vai_tro<>'$($account.Role)' OR COALESCE(ma_sv,'')<>COALESCE($($account.Student),'')))
 THROW 52111,N'Tài khoản mẫu đã có ánh xạ khác. Không tự ghi đè.',1;
IF @id IS NULL
BEGIN
 INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro,ma_sv,trang_thai)
 VALUES(N'$($account.Username)','$($account.Role)',$($account.Student),1);
 SET @id=SCOPE_IDENTITY();
END;
IF EXISTS(SELECT 1 FROM dbo.APP_CREDENTIAL WHERE ma_tk=@id) AND $reset=0
 THROW 52112,N'Tài khoản mẫu đã có mật khẩu; dùng -ResetPasswords nếu cần đặt lại.',1;
UPDATE dbo.TAI_KHOAN SET mat_khau_hash=N'$storedHash' WHERE ma_tk=@id;
UPDATE dbo.APP_CREDENTIAL SET salt=0x$saltHex,verifier=0x$keyHex,iterations=600000,failed_count=0,locked_until=NULL WHERE ma_tk=@id;
IF @@ROWCOUNT=0 INSERT dbo.APP_CREDENTIAL(ma_tk,salt,verifier,iterations) VALUES(@id,0x$saltHex,0x$keyHex,600000);
DELETE dbo.APP_SESSION WHERE ma_tk=@id;
COMMIT;
"@
    $path = Join-Path $temp ($account.Username + '.sql')
    [IO.File]::WriteAllText($path,$sql,[Text.UTF8Encoding]::new($false))
    & $sqlcmdPath -S $Server -E -C -I -b -f 65001 -d $DatabaseName -i $path
    [Security.Cryptography.CryptographicOperations]::ZeroMemory($key)
    if ($LASTEXITCODE -ne 0) { throw 'Chưa tạo được tài khoản mẫu.' }
    $created += "$($account.Username) | $($account.Password) | $($account.Role)"
}
$handoff = Join-Path $root '.tmp/TaiKhoanMau.txt'
[IO.File]::WriteAllLines($handoff, @("Server: $Server", "Database: $DatabaseName", 'Tên đăng nhập | Mật khẩu | Vai trò') + $created, [Text.UTF8Encoding]::new($false))
Write-Host "Đã tạo 2 tài khoản mẫu. Thông tin đăng nhập tại $handoff"
