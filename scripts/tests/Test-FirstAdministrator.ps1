#requires -Version 7.4
[CmdletBinding()]
param([switch]$SqlIntegration, [string]$Server = '.')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$script = Join-Path $root 'scripts/Initialize-FirstAdministrator.ps1'
if (-not (Test-Path -LiteralPath $script)) { throw 'Missing explicit first-administrator provisioning entry point.' }
$run = [Guid]::NewGuid().ToString('N')
$fixtureRoot = Join-Path $root ".tmp/first-admin-tests/$run"
[IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
function Check([bool]$ok, [string]$message) { if (-not $ok) { throw $message }; Write-Output "PASS: $message" }
function Reject([scriptblock]$work, [string]$message) {
    $rejected = $false
    try { & $work | Out-Null } catch { $rejected = $true }
    Check $rejected $message
}
Reject { & $script -Username 'daotao' -Reason 'test' } 'Creation requires explicit first-administrator switch.'
Reject { & $script -CreateFirstAdministrator -Username "bad'name" -Reason 'test' } 'Unsafe username is rejected before SQL.'
Reject { & $script -CreateFirstAdministrator -Username 'test-admin' -Reason ' ' } 'Blank authorization reason is rejected.'
Reject { & $script -CreateFirstAdministrator -Username 'test-admin' -Reason 'test' -CredentialFile (Join-Path $root 'credentials.txt') } 'Handoff outside ignored local directory is rejected.'
if (-not $SqlIntegration) { Write-Output 'SQL checks not requested.'; return }
$database = 'DangKyTinChi_Minh_Test_FirstAdmin_' + $run.Substring(0, 12)
$created = $false
function Query([string]$query) {
    $result = & sqlcmd -S $Server -E -C -I -b -x -l 5 -h -1 -W -d $database -Q ("SET NOCOUNT ON; " + $query)
    if ($LASTEXITCODE -ne 0) { throw 'Isolated administrator test SQL failed.' }
    ($result | Out-String).Trim()
}
function Create([string]$username, [string]$file) { & $script -Server $Server -DatabaseName $database -CreateFirstAdministrator -Username $username -Reason 'Isolated administrator provisioning contract test' -CredentialFile $file }
try {
    $existing = & sqlcmd -S $Server -E -C -I -b -x -l 5 -h -1 -W -d master -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$database') IS NULL THEN 0 ELSE 1 END;"
    if ($LASTEXITCODE -ne 0 -or ($existing | Out-String).Trim() -ne '0') { throw 'Fixture already exists or SQL is unavailable; no changes.' }
    # Own this exact, previously absent random name before bootstrap can partially create it.
    $created = $true
    & (Join-Path $root 'scripts/Initialize-Database.ps1') -Server $Server -DatabaseName $database -SeedDemo -MapLocalLogin *> (Join-Path $fixtureRoot 'bootstrap.log')
    $before = Query 'SELECT COUNT(*) FROM dbo.TAI_KHOAN;'
    $file = Join-Path $fixtureRoot 'admin.txt'
    Create 'fixture-first-admin' $file
    Check ((Test-Path -LiteralPath $file) -and (Query "SELECT COUNT(*) FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='fixture-first-admin' AND vai_tro='QUAN_TRI' AND ma_sv IS NULL AND trang_thai=1;") -eq '1') 'Exactly one new active administrator is provisioned.'
    Check ((Query 'SELECT COUNT(*) FROM dbo.TAI_KHOAN;') -eq ([int]$before + 1).ToString()) 'Existing accounts are preserved.'
    Check ((Query "SELECT COUNT(*) FROM dbo.ACCOUNT_AUDIT WHERE action='BOOTSTRAP' AND actor=target;") -eq '1') 'Bootstrap has a durable audit entry.'
    $parts = (Get-Content -LiteralPath $file | Where-Object { $_ -like 'fixture-first-admin |*' }).Split('|')
    $password = $parts[1].Trim()
    $credential = (Query "SELECT CONVERT(varchar(32),c.salt,2)+':'+CONVERT(varchar(64),c.verifier,2) FROM dbo.APP_CREDENTIAL c JOIN dbo.TAI_KHOAN t ON c.ma_tk=t.ma_tk WHERE t.ten_dang_nhap='fixture-first-admin';").Split(':')
    $derived = [Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2($password, [Convert]::FromHexString($credential[0]), 600000, [Security.Cryptography.HashAlgorithmName]::SHA256, 32)
    try { Check ([Security.Cryptography.CryptographicOperations]::FixedTimeEquals($derived,[Convert]::FromHexString($credential[1]))) 'Handoff password matches committed verifier.' }
    finally { [Security.Cryptography.CryptographicOperations]::ZeroMemory($derived); $password = $null }
    Reject { Create 'another-admin' (Join-Path $fixtureRoot 'second.txt') } 'A second bootstrap is rejected when an administrator exists.'
    Query "DELETE dbo.ACCOUNT_AUDIT WHERE target IN(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='fixture-first-admin'); DELETE dbo.APP_CREDENTIAL WHERE ma_tk IN(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='fixture-first-admin'); DELETE dbo.TAI_KHOAN WHERE ten_dang_nhap='fixture-first-admin';" | Out-Null
    Query "INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro,ma_sv,trang_thai) VALUES('sv2024001','SINH_VIEN','SV2024001',1);" | Out-Null
    Reject { Create 'sv2024001' (Join-Path $fixtureRoot 'collision.txt') } 'Existing username is never promoted or overwritten.'
    Check ((Query "SELECT COUNT(*) FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='sv2024001' AND vai_tro='SINH_VIEN';") -eq '1') 'Student identity remains unchanged after rejection.'
} finally {
    if ($created) {
        if ($database -notmatch '^DangKyTinChi_Minh_Test_FirstAdmin_[a-f0-9]{12}$') { throw 'Unsafe fixture cleanup target.' }
        & sqlcmd -S $Server -E -C -b -x -d master -Q "IF DB_ID(N'$database') IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; END;"
        if ($LASTEXITCODE -ne 0) { throw 'First-administrator fixture cleanup failed.' }
    }
}
