[CmdletBinding()]
param([switch]$UpgradeExisting, [string]$Server = '.', [string]$BackupDirectory)

$ErrorActionPreference = 'Stop'
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solution = Join-Path $workspaceRoot 'QuanLyGiangVien/QuanLyGiangVien.slnx'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Cần .NET SDK 10.0 cho ứng dụng WinForms.' }
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    $sqlcmdDirectory = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn'
    if (-not (Test-Path -LiteralPath (Join-Path $sqlcmdDirectory 'SQLCMD.EXE'))) { throw 'Cần sqlcmd và SQL Server local.' }
    $env:PATH = $sqlcmdDirectory + [IO.Path]::PathSeparator + $env:PATH
}

& dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw 'Restore thất bại; kiểm tra .NET SDK 10.0 và kết nối NuGet.' }
& dotnet build $solution -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build thất bại; chưa thay đổi database.' }

$databaseOptions = @{ Server = $Server; UpgradeExisting = $UpgradeExisting }
if ($BackupDirectory) { $databaseOptions.BackupDirectory = $BackupDirectory }
if (-not $UpgradeExisting) { $databaseOptions.SeedDemo = $true; $databaseOptions.MapLocalLogin = $true }
& (Join-Path $PSScriptRoot 'Initialize-Database.ps1') @databaseOptions
if (-not $UpgradeExisting) { & (Join-Path $PSScriptRoot 'Initialize-LoginDemo.ps1') -Server $Server }

Write-Host 'Đã tạo/nâng cấp database và build cả hai cổng.'
Write-Host 'Phòng đào tạo: dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --launch-profile QuanLyGiangVien'
Write-Host 'Sinh viên: dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --launch-profile ThoiKhoaBieuSinhVien'
Write-Host 'Hai cổng đăng nhập dùng chung DangKyTinChi_Minh. Tài khoản mẫu: .tmp/TaiKhoanMau.txt (sau lần tạo mới).'
