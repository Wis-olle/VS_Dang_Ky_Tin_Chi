[CmdletBinding()]
param([switch]$UpgradeExisting)

$ErrorActionPreference = 'Stop'
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solution = Join-Path $workspaceRoot 'QuanLyGiangVien/QuanLyGiangVien.slnx'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Cần .NET SDK 10.0 cho ứng dụng WinForms.' }
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) { throw 'Cần sqlcmd và SQL Server local truy cập qua localhost.' }

& dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw 'Restore thất bại; kiểm tra .NET SDK 10.0 và kết nối NuGet.' }
& dotnet build $solution -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build thất bại; chưa thay đổi database.' }

$databaseOptions = @{ SeedDemo = $true; MapLocalLogin = $true; UpgradeExisting = $UpgradeExisting }
& (Join-Path $PSScriptRoot 'Initialize-Database.ps1') @databaseOptions
& (Join-Path $PSScriptRoot 'Initialize-StudentPreview.ps1') -OpenRegistration

Write-Host 'Đã tạo/nâng cấp database và build cả hai cổng.'
Write-Host 'Phòng đào tạo: dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --launch-profile QuanLyGiangVien'
Write-Host 'Sinh viên: dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --launch-profile ThoiKhoaBieuSinhVien'
