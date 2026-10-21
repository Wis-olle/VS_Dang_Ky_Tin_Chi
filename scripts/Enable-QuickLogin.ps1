[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CredentialFile,
    [string]$OutputFile = (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Campus/quick-login.dat')
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$privateRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot '.tmp')) + [IO.Path]::DirectorySeparatorChar
$sourcePath = [IO.Path]::GetFullPath($CredentialFile)
if (-not $sourcePath.StartsWith($privateRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Chọn tệp tài khoản cục bộ trong .tmp của dự án.' }
$localRoot = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Campus')) + [IO.Path]::DirectorySeparatorChar
$targetPath = [IO.Path]::GetFullPath($OutputFile)
if (-not ($targetPath.StartsWith($privateRoot, [StringComparison]::OrdinalIgnoreCase) -or $targetPath.StartsWith($localRoot, [StringComparison]::OrdinalIgnoreCase))) { throw 'Chỉ lưu cấu hình trong Campus LocalAppData hoặc .tmp của dự án.' }
if ((Get-Item -LiteralPath $sourcePath).Length -gt 65536) { throw 'Tệp tài khoản không hợp lệ.' }
$lines = [IO.File]::ReadAllLines($sourcePath)
function Header([string]$Name) {
    $found = @($lines | Where-Object { $_.StartsWith($Name + ':', [StringComparison]::OrdinalIgnoreCase) })
    if ($found.Count -ne 1) { throw 'Thiếu hoặc trùng máy chủ/database trong tệp tài khoản.' }
    return $found[0].Substring($Name.Length + 1).Trim()
}
function Account([string]$Name, [string]$Role) {
    $found = @($lines | Where-Object { ($_ -split '\|', 3)[0].Trim() -eq $Name })
    if ($found.Count -ne 1) { throw 'Thiếu hoặc trùng tài khoản mẫu trong tệp bàn giao.' }
    $parts = $found[0] -split '\|', 3
    if ($parts.Length -ne 3 -or $parts[2].Trim() -ne $Role -or $parts[1].Trim().Length -lt 12 -or $parts[1].Trim().Length -gt 256) { throw 'Tài khoản mẫu hoặc mật khẩu không đúng định dạng.' }
    return @{ username = $parts[0].Trim(); password = $parts[1].Trim() }
}
$payload = @{ version = 1; server = (Header 'Server'); database = (Header 'Database'); student = (Account 'sv2024001' 'SINH_VIEN'); training = (Account 'daotao' 'DAO_TAO') }
if ($payload.server -notmatch '^(localhost|\.|\(local\))$' -or $payload.database -notmatch '^DangKyTinChi_Minh(_Test_[A-Za-z0-9_]+)?$') { throw 'Đăng nhập mẫu hỗ trợ SQL cục bộ instance mặc định và database Campus.' }
$bytes = [Text.UTF8Encoding]::new($false).GetBytes(($payload | ConvertTo-Json -Depth 4 -Compress))
$entropy = [Text.Encoding]::UTF8.GetBytes('Campus.QuickLogin.v1')
$encrypted = $null
$pendingPath = $targetPath + '.' + [Guid]::NewGuid().ToString('N') + '.pending'
try {
    $encrypted = [Security.Cryptography.ProtectedData]::Protect($bytes, $entropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($targetPath)) | Out-Null
    [IO.File]::WriteAllBytes($pendingPath, $encrypted)
    if ([IO.File]::Exists($targetPath)) {
        $backupPath = $targetPath + '.previous-' + [Guid]::NewGuid().ToString('N')
        [IO.File]::Replace($pendingPath, $targetPath, $backupPath)
    } else { [IO.File]::Move($pendingPath, $targetPath) }
    Write-Output "Đã bật đăng nhập nhanh cho hai tài khoản mẫu trên Windows hiện tại. Cấu hình mã hóa: $targetPath"
}
finally {
    [Security.Cryptography.CryptographicOperations]::ZeroMemory($bytes)
    if ($encrypted) { [Security.Cryptography.CryptographicOperations]::ZeroMemory($encrypted) }
}
