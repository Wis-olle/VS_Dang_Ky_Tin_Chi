[CmdletBinding()]
param(
    [string]$ProvisioningPath = (Join-Path $PSScriptRoot '../Initialize-LoginDemo.ps1'),
    [ValidatePattern('^(localhost|\.|\(local\))(\\[A-Za-z0-9_]+)?$')][string]$Server = '.',
    [switch]$SqlIntegration
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runId = [Guid]::NewGuid().ToString('N')
$fixtureRoot = Join-Path $projectRoot ('.tmp/phase1-provision-tests/' + $runId)
$fixtureScripts = Join-Path $fixtureRoot 'scripts'
$fixtureData = Join-Path $fixtureRoot '.tmp'
[IO.Directory]::CreateDirectory($fixtureScripts) | Out-Null
[IO.Directory]::CreateDirectory($fixtureData) | Out-Null
$helper = Join-Path $fixtureScripts 'Initialize-LoginDemo.ps1'
Copy-Item -LiteralPath $ProvisioningPath -Destination $helper
$database = 'DangKyTinChi_Minh_Test_Phase1Provision18_' + $runId.Substring(0, 12)
$createdDatabase = $false
function Assert-Check([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message }; Write-Output ('PASS: ' + $Message) }
function New-TestPassword { 'Test!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)) + 'a7' }
function Invoke-FixtureSql([string]$Query) {
    $result = & sqlcmd -S $Server -E -C -I -b -x -l 5 -h -1 -W -d $database -Q ('SET NOCOUNT ON; ' + $Query)
    if ($LASTEXITCODE -ne 0) { throw 'Fixture SQL failed.' }
    return ($result | Out-String).Trim()
}
function Invoke-Helper([string]$Destination, [switch]$Reset) {
    & $helper -Server $Server -DatabaseName $database -CredentialFile $Destination -StudentPassword $studentPassword -TrainingPassword $trainingPassword -ResetPasswords:$Reset
}
function Assert-CredentialPassword([string]$Username, [string]$Password) {
    $encoded = Invoke-FixtureSql "SELECT CONVERT(varchar(32),c.salt,2)+':'+CONVERT(varchar(64),c.verifier,2) FROM dbo.APP_CREDENTIAL c JOIN dbo.TAI_KHOAN t ON t.ma_tk=c.ma_tk WHERE t.ten_dang_nhap=N'$Username';"
    $parts = $encoded.Split(':')
    if ($parts.Length -ne 2) { throw 'Expected exactly one isolated credential.' }
    $actual = [Convert]::FromHexString($parts[1])
    $expected = [Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2($Password, [Convert]::FromHexString($parts[0]), 600000, [Security.Cryptography.HashAlgorithmName]::SHA256, 32)
    try { Assert-Check ([Security.Cryptography.CryptographicOperations]::FixedTimeEquals($actual, $expected)) 'The committed verifier matches the isolated handoff password.' }
    finally { [Security.Cryptography.CryptographicOperations]::ZeroMemory($actual); [Security.Cryptography.CryptographicOperations]::ZeroMemory($expected) }
}
function Test-FileSafety {
    # This shim handles only IO failure tests; SQL atomicity is checked against the real fixture below.
    $shimRoot = Join-Path $fixtureRoot 'shim'
    [IO.Directory]::CreateDirectory($shimRoot) | Out-Null
    [IO.File]::WriteAllText((Join-Path $shimRoot 'sqlcmd.ps1'), @'
$global:LASTEXITCODE=0
if ($args -ccontains '-i') {
    $global:CampusProvisionTest.Writes++
    if ($global:CampusProvisionTest.Hook) { & $global:CampusProvisionTest.Hook }
    if ($global:CampusProvisionTest.FailSql) { $global:LASTEXITCODE=1 }
}
'@)
    $oldPath = $env:PATH
    $env:PATH = $shimRoot + [IO.Path]::PathSeparator + $env:PATH
    $global:CampusProvisionTest = @{Writes=0; Hook=$null; FailSql=$false; FileLock=$null}
    try {
        $studentPassword = New-TestPassword; $trainingPassword = New-TestPassword
        $destination = Join-Path $fixtureData 'mock-create.txt'
        Invoke-Helper $destination
        Assert-Check ((Test-Path -LiteralPath $destination -PathType Leaf) -and $global:CampusProvisionTest.Writes -eq 1) "A new handoff is published after a single SQL mutation call (calls: $($global:CampusProvisionTest.Writes))."
        $originalHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        $failed = $false
        try { Invoke-Helper $destination } catch { $failed = $true }
        Assert-Check ($failed -and $global:CampusProvisionTest.Writes -eq 1 -and (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -eq $originalHash) 'An existing output is rejected before SQL mutation and stays byte-for-byte intact.'
        Invoke-Helper $destination -Reset
        $backups = @(Get-ChildItem -LiteralPath $fixtureData -Filter 'mock-create.txt.previous-*' -File)
        Assert-Check ($backups.Count -eq 1 -and (Get-FileHash -LiteralPath $backups[0].FullName -Algorithm SHA256).Hash -eq $originalHash) 'Reset keeps a durable copy of the previous handoff.'

        # A pending name longer than one filesystem component fails before any SQL write.
        $before = $global:CampusProvisionTest.Writes
        $tooLong = Join-Path $fixtureData (('x' * 240) + '.txt')
        $failed = $false
        try { Invoke-Helper $tooLong } catch { $failed = $true }
        Assert-Check ($failed -and $global:CampusProvisionTest.Writes -eq $before) 'Failure to create the pending handoff prevents all SQL writes.'

        # Another process claims the final filename after preflight; Move must never overwrite it.
        $raceTarget = Join-Path $fixtureData 'mock-race.txt'
        $global:CampusProvisionTest.Hook = { [IO.File]::WriteAllText($raceTarget, 'other-process-handoff') }.GetNewClosure()
        $failed = $false
        try { Invoke-Helper $raceTarget } catch { $failed = $true }
        $pending = @(Get-ChildItem -LiteralPath $fixtureData -Filter 'mock-race.txt.*.pending' -File)
        Assert-Check ($failed -and [IO.File]::ReadAllText($raceTarget) -eq 'other-process-handoff' -and $pending.Count -eq 1) 'A final-name race preserves the other file and retains the pending rescue handoff.'
        Assert-Check ([IO.File]::ReadAllText($pending[0].FullName).Contains($studentPassword)) 'The rescue file contains the proposed password without printing it.'

        $lockedTarget = Join-Path $fixtureData 'mock-locked.txt'
        [IO.File]::WriteAllText($lockedTarget, 'old-handoff')
        $global:CampusProvisionTest.Hook = { $global:CampusProvisionTest.FileLock = [IO.File]::Open($lockedTarget, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read) }.GetNewClosure()
        $failed = $false
        try { Invoke-Helper $lockedTarget -Reset } catch { $failed = $true }
        finally { if ($global:CampusProvisionTest.FileLock) { $global:CampusProvisionTest.FileLock.Dispose(); $global:CampusProvisionTest.FileLock=$null } }
        $pending = @(Get-ChildItem -LiteralPath $fixtureData -Filter 'mock-locked.txt.*.pending' -File)
        Assert-Check ($failed -and [IO.File]::ReadAllText($lockedTarget) -eq 'old-handoff' -and $pending.Count -eq 1) 'A failed atomic replacement preserves both the old and rescue handoffs.'

        $global:CampusProvisionTest.Hook = $null; $global:CampusProvisionTest.FailSql = $true
        $unknownTarget = Join-Path $fixtureData 'mock-unknown.txt'
        $failed = $false; $failureMessage = ''
        try { Invoke-Helper $unknownTarget } catch { $failed = $true; $failureMessage=$_.Exception.Message }
        $pending = @(Get-ChildItem -LiteralPath $fixtureData -Filter 'mock-unknown.txt.*.pending' -File)
        Assert-Check ($failed -and -not (Test-Path -LiteralPath $unknownTarget) -and $pending.Count -eq 1 -and $failureMessage.Contains($pending[0].FullName)) 'An uncertain SQL result retains and identifies the rescue file.'
        Assert-Check (-not $failureMessage.Contains($studentPassword) -and -not $failureMessage.Contains($trainingPassword)) 'Failure messages do not expose plaintext passwords.'
    }
    finally {
        if ($global:CampusProvisionTest.FileLock) { $global:CampusProvisionTest.FileLock.Dispose() }
        $env:PATH = $oldPath
        Remove-Variable CampusProvisionTest -Scope Global
    }
}
try {
    Test-FileSafety
    if ($SqlIntegration) {
        $createdDatabase = $true
        & (Join-Path $projectRoot 'scripts/Initialize-Database.ps1') -Server $Server -DatabaseName $database -SeedDemo -MapLocalLogin
        $null = Invoke-FixtureSql @'
DELETE c FROM dbo.APP_CREDENTIAL c JOIN dbo.TAI_KHOAN t ON t.ma_tk=c.ma_tk WHERE t.ten_dang_nhap IN(N'sv2024001',N'daotao');
DELETE dbo.TAI_KHOAN WHERE ten_dang_nhap IN(N'sv2024001',N'daotao');
INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro,trang_thai) VALUES(N'daotao','DAO_TAO',1);
INSERT dbo.APP_CREDENTIAL(ma_tk,salt,verifier,iterations) VALUES(SCOPE_IDENTITY(),CRYPT_GEN_RANDOM(16),CRYPT_GEN_RANDOM(32),600000);
'@
        $studentPassword = New-TestPassword; $trainingPassword = New-TestPassword
        $destination = Join-Path $fixtureData 'accounts.txt'
        $failed = $false
        try { Invoke-Helper $destination } catch { $failed = $true }
        Assert-Check $failed 'A pre-existing second credential rejects provisioning.'
        Assert-Check ((Invoke-FixtureSql "SELECT COUNT(*) FROM dbo.TAI_KHOAN WHERE ten_dang_nhap=N'sv2024001';") -eq '0') 'Failure on account two leaves no new student account or orphaned credential.'
        $null = Invoke-FixtureSql "DELETE c FROM dbo.APP_CREDENTIAL c JOIN dbo.TAI_KHOAN t ON t.ma_tk=c.ma_tk WHERE t.ten_dang_nhap=N'daotao'; DELETE dbo.TAI_KHOAN WHERE ten_dang_nhap=N'daotao';"
        $null = Invoke-FixtureSql @'
EXEC(N'CREATE TRIGGER dbo.Phase1ProvisionReject ON dbo.APP_CREDENTIAL AFTER INSERT AS
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.TAI_KHOAN t ON t.ma_tk=i.ma_tk WHERE t.ten_dang_nhap=N''daotao'')
 THROW 52990,N''Isolated provisioning rollback test.'',1;');
'@
        $failed = $false
        try { Invoke-Helper (Join-Path $fixtureData 'fault.txt') } catch { $failed = $true }
        Assert-Check ($failed -and (Invoke-FixtureSql "SELECT COUNT(*) FROM dbo.TAI_KHOAN WHERE ten_dang_nhap IN(N'sv2024001',N'daotao');") -eq '0') 'A late SQL trigger failure rolls back both inserted accounts and all credential changes.'
        $null = Invoke-FixtureSql 'DROP TRIGGER dbo.Phase1ProvisionReject;'
        Invoke-Helper $destination
        Assert-Check (Test-Path -LiteralPath $destination -PathType Leaf) 'Successful SQL provisioning publishes the credential file.'
        Assert-Check ((Invoke-FixtureSql "SELECT COUNT(*) FROM dbo.APP_CREDENTIAL c JOIN dbo.TAI_KHOAN t ON t.ma_tk=c.ma_tk WHERE t.ten_dang_nhap IN(N'sv2024001',N'daotao');") -eq '2') 'Both credentials exist after one successful provisioning.'
        Assert-CredentialPassword 'sv2024001' $studentPassword
        Assert-CredentialPassword 'daotao' $trainingPassword
        $originalFileHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        $failed = $false
        try { Invoke-Helper $destination } catch { $failed = $true }
        Assert-Check ($failed -and (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -eq $originalFileHash) 'A normal rerun preserves the existing credential file.'
        $studentPassword = New-TestPassword; $trainingPassword = New-TestPassword
        Invoke-Helper $destination -Reset
        Assert-Check ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $originalFileHash) 'Explicit password reset publishes a new handoff.'
        $backups = @(Get-ChildItem -LiteralPath $fixtureData -Filter 'accounts.txt.previous-*' -File)
        Assert-Check ($backups.Count -eq 1 -and (Get-FileHash -LiteralPath $backups[0].FullName -Algorithm SHA256).Hash -eq $originalFileHash) 'Explicit reset preserves the exact old credential file.'
        Assert-CredentialPassword 'sv2024001' $studentPassword
        Assert-CredentialPassword 'daotao' $trainingPassword
    }
}
finally {
    if ($createdDatabase) {
        if ($database -notmatch '^DangKyTinChi_Minh_Test_Phase1Provision18_[a-f0-9]{12}$') { throw 'Unexpected fixture database cleanup target.' }
        & sqlcmd -S $Server -E -C -I -b -x -l 5 -d master -Q "IF DB_ID(N'$database') IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; END;"
        if ($LASTEXITCODE -ne 0) { throw 'Could not clean the exact provisioning fixture database.' }
    }
    # Only this generated workspace is removed; SQL backups and live handoffs are outside it.
    $resolvedFixture = [IO.Path]::GetFullPath($fixtureRoot)
    $fixtureParent = [IO.Path]::GetFullPath((Join-Path $projectRoot '.tmp/phase1-provision-tests')) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedFixture.StartsWith($fixtureParent, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedFixture) -notmatch '^[a-f0-9]{32}$') { throw 'Unexpected provisioning file cleanup target.' }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    Write-Output "Cleaned isolated provisioning fixture: $database"
}
