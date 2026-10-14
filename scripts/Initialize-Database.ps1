[CmdletBinding()]
param(
    [ValidatePattern('^(localhost|\.|\(local\))(\\[A-Za-z0-9_]+)?$')]
    [string]$Server = 'localhost',
    [ValidatePattern('^DangKyTinChi_Minh(_Test_[A-Za-z0-9_]+)?$')]
    [string]$DatabaseName = 'DangKyTinChi_Minh',
    [switch]$UpgradeExisting,
    [switch]$SeedDemo,
    [switch]$MapLocalLogin,
    [switch]$BaseOnly
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sqlRoot = Join-Path $projectRoot 'QuanLyGiangVien/database'
$logRoot = Join-Path $projectRoot '.tmp/database-bootstrap'
[IO.Directory]::CreateDirectory($logRoot) | Out-Null
if ($BaseOnly -and $DatabaseName -notmatch '^DangKyTinChi_Minh_Test_') {
    throw 'BaseOnly chỉ dành cho database kiểm thử riêng.'
}
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) { throw 'Cần cài sqlcmd trước khi khởi tạo.' }

function Invoke-ScriptText([string]$Content, [string]$Catalog) {
    $scriptPath = Join-Path $logRoot ([Guid]::NewGuid().ToString('N') + '.sql')
    [IO.File]::WriteAllText($scriptPath, $Content, [Text.UTF8Encoding]::new($false))
    & sqlcmd -S $Server -E -C -I -l 5 -t 60 -b -r 1 -f 65001 -d $Catalog -i $scriptPath
    if ($LASTEXITCODE -ne 0) { throw "SQL thất bại; dừng khởi tạo $DatabaseName. Log script: $scriptPath" }
}

# The name is restricted above; there is no user-provided SQL or password interpolation.
$exists = & sqlcmd -S $Server -E -C -I -l 5 -b -h -1 -W -d master -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$DatabaseName') IS NULL THEN 0 ELSE 1 END;"
if ($LASTEXITCODE -ne 0) { throw 'Không kết nối được SQL Server cục bộ bằng Windows Authentication.' }
$databaseExists = ($exists | Out-String).Trim() -eq '1'
if ($databaseExists -and -not $UpgradeExisting) {
    throw "Database $DatabaseName đã tồn tại. Không ghi đè; dùng -UpgradeExisting để áp dụng migration bổ sung."
}
if (-not $databaseExists) {
    $sourcePath = Join-Path $sqlRoot '00_Hue.sql'
    $expectedHash = '4FFE647BAEA4D75D6CD43E23D78227658827279A288A9759406475D70F10DE8A'
    if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash -ne $expectedHash) {
        throw 'SQL gốc Huệ đã khác checksum; kiểm tra nguồn trước khi khởi tạo.'
    }
    $source = [IO.File]::ReadAllText($sourcePath)
    # Only database selection changes in the execution copy; the upstream file stays byte-for-byte intact.
    $source = $source.Replace('CREATE DATABASE DangKyTinChi', "CREATE DATABASE [$DatabaseName]")
    $source = $source.Replace('USE DangKyTinChi;', "USE [$DatabaseName];")
    Invoke-ScriptText $source 'master'
}
if ($BaseOnly) { Write-Host "Đã tạo schema Huệ để kiểm tra RED tại $DatabaseName."; return }

foreach ($name in @('01_Minh.sql', '02_Resources.sql', '03_Classes.sql', '03_Read.sql', '06_Catalogs.sql', '07_RegistrationIntegration.sql')) {
    Invoke-ScriptText ([IO.File]::ReadAllText((Join-Path $sqlRoot $name))) $DatabaseName
}
if ($SeedDemo) { Invoke-ScriptText ([IO.File]::ReadAllText((Join-Path $sqlRoot '05_DemoSeed.sql'))) $DatabaseName }
if ($MapLocalLogin) { Invoke-ScriptText ([IO.File]::ReadAllText((Join-Path $sqlRoot '04_LocalLogin.sql'))) $DatabaseName }
Write-Host "Database $DatabaseName đã sẵn sàng trên SQL Server $Server."
