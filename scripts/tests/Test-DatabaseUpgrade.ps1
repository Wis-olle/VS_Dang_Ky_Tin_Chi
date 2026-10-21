[CmdletBinding()]
param([string]$BootstrapPath = (Join-Path $PSScriptRoot '../Initialize-Database.ps1'))

# No SQL Server is contacted: sqlcmd is shadowed only inside this test process.
$ErrorActionPreference = 'Stop'
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('campus-upgrade-test-' + [Guid]::NewGuid().ToString('N'))
$fixtureScripts = Join-Path $fixtureRoot 'scripts'
$fixtureSql = Join-Path $fixtureRoot 'QuanLyGiangVien/database'
[IO.Directory]::CreateDirectory($fixtureScripts) | Out-Null
[IO.Directory]::CreateDirectory($fixtureSql) | Out-Null
$fixtureBootstrap = Join-Path $fixtureScripts 'Initialize-Database.ps1'
Copy-Item -LiteralPath $BootstrapPath -Destination $fixtureBootstrap
$migrationNames = @('01_Minh.sql','02_Resources.sql','03_Classes.sql','03_Read.sql','06_Catalogs.sql',
    '07_RegistrationIntegration.sql','08_PasswordLogin.sql','09_Students.sql','10_StudentTimetable.sql','11_SchemaVersion.sql',
    '12_AcademicSchema.sql','13_AcademicCommands.sql','14_AcademicEligibility.sql','15_AccountAdministration.sql','16_PhaseTwoVersion.sql', '17_PhaseThreeFoundation.sql', '18_RegistrationWorkflow.sql', '19_Lifecycle.sql', '20_Exceptions.sql', '21_PhaseThreeVersion.sql', '22_SeatAllocation.sql', '23_AcademicPlanning.sql', '24_SchedulePlanner.sql', '25_DataImport.sql', '26_Operations.sql', '27_PhaseFourVersion.sql')
foreach ($name in $migrationNames) { [IO.File]::WriteAllText((Join-Path $fixtureSql $name), '-- migration ' + $name) }
$testState = @{ Commands = [Collections.Generic.List[string]]::new(); FailBackup = $false }
Set-Item Function:sqlcmd -Value {
    $arguments = @($args)
    $global:LASTEXITCODE = 0
    if ($arguments -contains '-Q') { '1'; return }
    $index = [Array]::IndexOf($arguments, '-i')
    if ($index -lt 0) { throw 'Unexpected sqlcmd call in upgrade test.' }
    $query = [IO.File]::ReadAllText([string]$arguments[$index + 1])
    $testState.Commands.Add($query)
    if ($testState.FailBackup -and $query.Contains('BACKUP DATABASE')) { $global:LASTEXITCODE = 1 }
}.GetNewClosure()
try {
    $testState.FailBackup = $true
    $failed = $false
    try { & $fixtureBootstrap -Server '.' -DatabaseName 'DangKyTinChi_Minh_Test_UpgradeContract' -UpgradeExisting }
    catch { $failed = $true; $failureMessage = $_.Exception.Message }
    if (-not $failed -or $testState.Commands.Count -ne 1 -or -not $testState.Commands[0].Contains('BACKUP DATABASE')) {
        throw "Backup failure must stop before the first migration. Calls: $($testState.Commands.Count); caught: $failureMessage"
    }
    if (-not $testState.Commands[0].Contains('COPY_ONLY,CHECKSUM') -or -not $testState.Commands[0].Contains('RESTORE VERIFYONLY')) {
        throw 'An existing database requires copy-only checksummed backup and verification.'
    }
    $testState.Commands.Clear(); $testState.FailBackup = $false
    & $fixtureBootstrap -Server '.' -DatabaseName 'DangKyTinChi_Minh_Test_UpgradeContract' -UpgradeExisting
    if ($testState.Commands.Count -ne $migrationNames.Count + 2) { throw 'Upgrade must run backup, migrations and postflight only.' }
    for ($index = 0; $index -lt $migrationNames.Count; $index++) {
        if ($testState.Commands[$index + 1] -ne ('-- migration ' + $migrationNames[$index])) { throw 'Migration order changed.' }
    }
    $testState.Commands.Clear(); $rejected = $false
    try { & $fixtureBootstrap -Server '.' -DatabaseName 'DangKyTinChi_Minh_Test_UpgradeContract' }
    catch { $rejected = $true }
    if (-not $rejected -or $testState.Commands.Count -ne 0) { throw 'Existing databases require an explicit upgrade flag.' }
    Write-Output 'PASS: backup failure blocks DDL; verified backup precedes ordered migrations; no implicit seed/map; explicit upgrade required.'
}
finally {
    # Resolve and validate the exact temporary fixture before recursive cleanup.
    $resolvedFixture = [IO.Path]::GetFullPath($fixtureRoot)
    $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolvedFixture.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedFixture) -notlike 'campus-upgrade-test-*') { throw 'Unexpected fixture cleanup path.' }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
