[CmdletBinding()]
param(
    [ValidatePattern('^(localhost|\.|\(local\))(\\[A-Za-z0-9_]+)?$')]
    [string]$Server = '.',
    [ValidatePattern('^DangKyTinChi_Minh(_Test_[A-Za-z0-9_]+)?$')]
    [string]$DatabaseName = 'DangKyTinChi_Minh',
    [switch]$UpgradeExisting,
    [ValidatePattern('^[^\r\n]*$')][string]$BackupDirectory,
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
$migrations = @('01_Minh.sql', '02_Resources.sql', '03_Classes.sql', '03_Read.sql', '06_Catalogs.sql',
    '07_RegistrationIntegration.sql', '08_PasswordLogin.sql', '09_Students.sql', '10_StudentTimetable.sql', '11_SchemaVersion.sql',
    '12_AcademicSchema.sql', '13_AcademicCommands.sql', '14_AcademicEligibility.sql', '15_AccountAdministration.sql', '16_PhaseTwoVersion.sql', '17_PhaseThreeFoundation.sql', '18_RegistrationWorkflow.sql', '19_Lifecycle.sql', '20_Exceptions.sql', '21_PhaseThreeVersion.sql', '22_SeatAllocation.sql', '23_AcademicPlanning.sql', '24_SchedulePlanner.sql', '25_DataImport.sql', '26_Operations.sql', '27_PhaseFourVersion.sql', '28_OrganizationCatalogs.sql')
if (-not $BaseOnly) {
    foreach ($name in $migrations) {
        if (-not (Test-Path -LiteralPath (Join-Path $sqlRoot $name))) { throw "Thiếu migration $name; chưa thay đổi database." }
    }
}

function Invoke-ScriptText([string]$Content, [string]$Catalog, [int]$TimeoutSeconds = 60) {
    $scriptPath = Join-Path $logRoot ([Guid]::NewGuid().ToString('N') + '.sql')
    [IO.File]::WriteAllText($scriptPath, $Content, [Text.UTF8Encoding]::new($false))
    & sqlcmd -S $Server -E -C -I -x -l 5 -t $TimeoutSeconds -b -r 1 -f 65001 -d $Catalog -i $scriptPath
    if ($LASTEXITCODE -ne 0) { throw "SQL thất bại; dừng khởi tạo $DatabaseName. Log script: $scriptPath" }
}

# The name is restricted above; there is no user-provided SQL or password interpolation.
$exists = & sqlcmd -S $Server -E -C -I -l 5 -b -h -1 -W -d master -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$DatabaseName') IS NULL THEN 0 ELSE 1 END;"
if ($LASTEXITCODE -ne 0) { throw 'Không kết nối được SQL Server cục bộ bằng Windows Authentication.' }
$databaseExists = ($exists | Out-String).Trim() -eq '1'
if ($databaseExists -and -not $UpgradeExisting) {
    throw "Database $DatabaseName đã tồn tại. Không ghi đè; dùng -UpgradeExisting để áp dụng migration bổ sung."
}
if ($databaseExists -and $BaseOnly) { throw 'BaseOnly chỉ tạo database kiểm thử mới; không thay đổi database đã có.' }
if ($databaseExists) {
    # SQL Server writes this file with its service identity. Do not guess another instance,
    # overwrite an earlier backup, seed demo records, or reset credentials during an upgrade.
    $escapedDirectory = ($BackupDirectory ?? '').Replace("'", "''")
    $backupFileName = $DatabaseName + '_' + [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss') + '_' + [Guid]::NewGuid().ToString('N') + '.bak'
    $backupSql = @"
SET NOCOUNT ON;
DECLARE @directory nvarchar(4000)=NULLIF(N'$escapedDirectory',N'');
IF @directory IS NULL SET @directory=CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'));
IF @directory IS NULL THROW 52501,N'Không tìm thấy thư mục backup của instance; chỉ định -BackupDirectory.',1;
DECLARE @backupPath nvarchar(4000)=@directory+CASE WHEN RIGHT(@directory,1) IN (N'\',N'/') THEN N'' ELSE N'\' END+N'$backupFileName';
BACKUP DATABASE [$DatabaseName] TO DISK=@backupPath WITH COPY_ONLY,CHECKSUM;
RESTORE VERIFYONLY FROM DISK=@backupPath WITH CHECKSUM;
PRINT N'Backup đã kiểm tra: '+@backupPath;
"@
    Write-Host "Sao lưu $DatabaseName trên $Server trước khi nâng cấp. Backup/VERIFYONLY lỗi sẽ dừng trước migration."
    Invoke-ScriptText $backupSql 'master' 600
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

foreach ($name in $migrations) {
    Invoke-ScriptText ([IO.File]::ReadAllText((Join-Path $sqlRoot $name))) $DatabaseName
}
if ($SeedDemo) { Invoke-ScriptText ([IO.File]::ReadAllText((Join-Path $sqlRoot '05_DemoSeed.sql'))) $DatabaseName }
if ($MapLocalLogin) { Invoke-ScriptText ([IO.File]::ReadAllText((Join-Path $sqlRoot '04_LocalLogin.sql'))) $DatabaseName }
Invoke-ScriptText @'
SET NOCOUNT ON;
IF OBJECT_ID('dbo.CAMPUS_SCHEMA_VERSION','U') IS NULL
 THROW 52502,N'Thiếu phiên bản schema sau migration; chưa thể chạy Campus.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_SCHEMA_VERSION WHERE id=1 AND version=4)
 THROW 52502,N'Phiên bản schema chưa tương thích; chưa thể chạy Campus.',1;
DECLARE @health TABLE(version int);
INSERT @health EXEC dbo.usp_Campus_KiemTraSchema;
IF NOT EXISTS(SELECT 1 FROM @health WHERE version=4)
 THROW 52502,N'Chưa đủ hợp đồng schema Campus sau migration.',1;
'@ $DatabaseName
Write-Host "Database $DatabaseName đã sẵn sàng trên SQL Server '$Server'."
