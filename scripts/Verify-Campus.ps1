#requires -Version 7.4
[CmdletBinding()]
param(
    [ValidatePattern('^(localhost|\.|\(local\))(\\[A-Za-z0-9_]+)?$')][string]$Server = '.',
    [switch]$PlanOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runId = 'V' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$output = Join-Path $repo ".tmp/verify-campus/$runId"
$roles = @('Full','Lifecycle','Concurrent','Team','Import','Seats','Academic','Planner','Native1','Native2','Native3','Native4','NativeAudit')
$fixtures = [ordered]@{}
foreach ($role in $roles) {
    $fixtures[$role] = if ($role -eq 'Planner') { "DangKyTinChi_Minh_Test_Planner4_${runId}_Sql" } else { "DangKyTinChi_Minh_Test_Verify_${runId}_$role" }
}
if ($PlanOnly) { @{ runId=$runId; output=$output; fixtures=@($fixtures.Values) } | ConvertTo-Json; return }
if (-not $IsWindows) { throw 'Native Campus verification requires Windows.' }
foreach ($command in @('dotnet','sqlcmd')) { if (-not (Get-Command $command -ErrorAction SilentlyContinue)) { throw "Required command: $command" } }
[IO.Directory]::CreateDirectory($output) | Out-Null
@{runId=$runId;server=$Server;fixtures=@($fixtures.Values)} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'fixture-manifest.json') -Encoding utf8
$owned = [Collections.Generic.List[string]]::new()
$environmentBefore = @{}
$cleanupErrors = [Collections.Generic.List[string]]::new()
$summary = [ordered]@{runId=$runId;server=$Server;passed=$false;fixtures=@($fixtures.Values);tests=$null;native=@();cleanupErrors=@();error=$null}
$locationPushed = $false
function Set-RunEnvironment([string]$Key, [AllowNull()][string]$Value) {
    if (-not $environmentBefore.ContainsKey($Key)) { $environmentBefore[$Key] = [Environment]::GetEnvironmentVariable($Key,'Process') }
    [Environment]::SetEnvironmentVariable($Key,$Value,'Process')
}
function Restore-Environment {
    foreach ($key in $environmentBefore.Keys) { [Environment]::SetEnvironmentVariable($key,$environmentBefore[$key],'Process') }
}
function Find-CoverageFile([string]$ResultsDirectory) {
    $files = @(Get-ChildItem -LiteralPath $ResultsDirectory -Recurse -Filter 'coverage.cobertura.xml')
    if ($files.Count -eq 0) { throw 'Missing coverage.cobertura.xml.' }
    # TRX may copy collector attachments under a machine-name directory. Only byte-identical duplicates are safe to collapse.
    $hashes = @($files | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } | Select-Object -Unique)
    if ($hashes.Count -ne 1) { throw 'Conflicting coverage.cobertura.xml files; refusing to choose an arbitrary report.' }
    return $files | Sort-Object FullName | Select-Object -First 1
}
function Assert-OwnedName([string]$Name) {
    if ($Name -notmatch '^DangKyTinChi_Minh_Test_(Verify|Planner4)_V[0-9a-f]{12}_[A-Za-z0-9]+$' -or $fixtures.Values -cnotcontains $Name) {
        throw 'Refusing SQL mutation outside the exact run fixture manifest.'
    }
}
function Invoke-Tool([string]$Name,[string[]]$Arguments,[string]$LogName) {
    & $Name @Arguments *> (Join-Path $output $LogName)
    if ($LASTEXITCODE -ne 0) { throw "$Name failed; see $LogName (exit $LASTEXITCODE)." }
}
function Invoke-FixtureSql([string]$Name,[string]$Query,[string]$LogName) {
    Assert-OwnedName $Name
    $path = Join-Path $output ($LogName + '.sql')
    [IO.File]::WriteAllText($path,"IF DB_NAME()<>N'$Name' THROW 52990,N'Wrong fixture database',1;`n"+$Query)
    Invoke-Tool 'sqlcmd' @('-S',$Server,'-E','-C','-I','-x','-b','-r','1','-l','5','-t','120','-f','65001','-d',$Name,'-i',$path) ($LogName+'.log')
}
function New-Fixture([string]$Role) {
    $name = $fixtures[$Role]; Assert-OwnedName $name
    # Claim ownership only after an exact-name existence check. Never upgrade a pre-existing database.
    $exists = & sqlcmd -S $Server -E -C -I -x -b -l 5 -h -1 -W -d master -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$name') IS NULL THEN 0 ELSE 1 END;"
    if ($LASTEXITCODE -ne 0 -or ($exists | Out-String).Trim() -ne '0') { throw "Fixture unavailable; no changes: $name" }
    $owned.Add($name)
    Write-Host "Preparing disposable fixture: $Role"
    & (Join-Path $PSScriptRoot 'Initialize-Database.ps1') -Server $Server -DatabaseName $name -SeedDemo -MapLocalLogin *> (Join-Path $output "bootstrap-$Role.log")
    return "Server=$Server;Database=$name;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5"
}
function Add-TestGrants([string]$Role) {
    Invoke-FixtureSql $fixtures[$Role] @'
SET NOCOUNT ON;
INSERT dbo.CAMPUS_PERMISSION SELECT t.ma_tk,p.permission FROM dbo.TAI_KHOAN t
CROSS JOIN (VALUES('CLASS_OPERATE'),('EXCEPTION_REVIEW'),('PROFILE_EDIT'),('HOLD_MANAGE'),('AUDIT_READ'),('SEAT_MANAGE'),('WAITLIST_OPERATE'),('ADVISE_PLAN'),('IMPORT_DATA'),('OPERATIONS_READ'))p(permission)
WHERE t.ten_dang_nhap=ORIGINAL_LOGIN() AND NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_PERMISSION old WHERE old.account_id=t.ma_tk AND old.permission=p.permission);
'@ "grants-$Role"
}
function New-TestCredentials([string]$Role,[string]$StudentPassword,[string]$TrainingPassword) {
    # Fresh random passwords belong only to this run. No sample account or credential file is reused.
    & (Join-Path $PSScriptRoot 'Initialize-LoginDemo.ps1') -Server $Server -DatabaseName $fixtures[$Role] -StudentPassword $StudentPassword -TrainingPassword $TrainingPassword -CredentialFile (Join-Path $output "$Role-credentials.private.txt") *> (Join-Path $output "login-$Role.log")
    Remove-Item -LiteralPath (Join-Path $output "$Role-credentials.private.txt")
}
function Invoke-Native([string]$Role,[string]$Variable) {
    $cs = New-Fixture $Role
    New-TestCredentials $Role $studentPassword $trainingPassword
    if ($Role -eq 'Native1') {
        Invoke-FixtureSql $fixtures[$Role] "INSERT dbo.CAMPUS_PERMISSION SELECT ma_tk,'PROFILE_EDIT' FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='daotao';" 'grants-Native1'
    }
    if ($Role -eq 'Native2') {
        & (Join-Path $PSScriptRoot 'Initialize-AcademicPolicy.ps1') -Server $Server -DatabaseName $fixtures[$Role] -ApplyUitReference *> (Join-Path $output 'policy-Native2.log')
        Invoke-FixtureSql $fixtures[$Role] "INSERT dbo.CAMPUS_PERMISSION SELECT t.ma_tk,p.permission FROM dbo.TAI_KHOAN t CROSS JOIN (VALUES('CLASS_OPERATE'),('EXCEPTION_REVIEW'),('PROFILE_EDIT'),('HOLD_MANAGE'),('AUDIT_READ'),('SEAT_MANAGE'),('WAITLIST_OPERATE'),('ADVISE_PLAN'),('IMPORT_DATA'),('OPERATIONS_READ'))p(permission) WHERE t.ten_dang_nhap='daotao';" 'grants-Native2'
    }
    $nativeOutput = Join-Path $output $Role
    [IO.Directory]::CreateDirectory($nativeOutput) | Out-Null
    Set-RunEnvironment 'MINH_DB_CONNECTION' $cs
    Set-RunEnvironment $Variable $nativeOutput
    $start = [Diagnostics.ProcessStartInfo]::new($appExe)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.ArgumentList.Add('--database')
    $process = [Diagnostics.Process]::Start($start)
    try {
        if (-not $process.WaitForExit(180000)) { $process.Kill($true); throw "$Role timed out; only the verification process was stopped." }
        if ($process.ExitCode -ne 0) { throw "$Role exited $($process.ExitCode)." }
    } finally { $process.Dispose(); Set-RunEnvironment $Variable $null }
    $resultFile = Join-Path $nativeOutput 'results.json'
    if (-not (Test-Path -LiteralPath $resultFile)) { throw "$Role did not produce results.json." }
    $result = Get-Content -LiteralPath $resultFile -Raw | ConvertFrom-Json
    if (-not $result.passed -or @($result.checks).Count -eq 0) { throw "$Role native verification failed; see results.json." }
    $summary.native += @{name=$Role;checks=@($result.checks).Count;passed=$true}
}
try {
    Push-Location $repo
    $locationPushed = $true
    # Disable unrelated inherited harness switches, then restore every variable in finally.
    foreach ($entry in Get-ChildItem Env: | Where-Object { $_.Name -match '(UI_CHECK$|^CAMPUS_QUICK_LOGIN_FILE$)' }) { Set-RunEnvironment $entry.Name $null }
    Invoke-Tool 'dotnet' @('build','QuanLyGiangVien/QuanLyGiangVien.slnx','-c','Release','--artifacts-path',(Join-Path $output 'artifacts')) 'build.log'
    $appExe = Join-Path $output 'artifacts/bin/QuanLyGiangVien/release/QuanLyGiangVien.exe'
    if (-not (Test-Path -LiteralPath $appExe)) { throw 'Built native executable not found.' }
    $full = New-Fixture 'Full'; Add-TestGrants 'Full'
    $studentPassword = 'Test!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)) + 'a7'
    $trainingPassword = 'Test!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12)) + 'b8'
    New-TestCredentials 'Full' $studentPassword $trainingPassword
    Invoke-FixtureSql $fixtures.Full "INSERT dbo.CAMPUS_PERMISSION SELECT ma_tk,'PROFILE_EDIT' FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='daotao';" 'grants-Full-login'
    foreach ($key in @('MINH_DB_TEST_CONNECTION','ACADEMIC_TEST_CONNECTION','CAMPUS_LOGIN_TEST_CONNECTION','PHASE3_TEST_CONNECTION')) { Set-RunEnvironment $key $full }
    Set-RunEnvironment 'DEMO_STUDENT_PASSWORD' $studentPassword
    Set-RunEnvironment 'DEMO_TRAINING_PASSWORD' $trainingPassword
    foreach ($pair in @(@('Lifecycle','LIFECYCLE_TEST_CONNECTION'),@('Concurrent','MINH_DB_CONCURRENCY_CONNECTION'),@('Team','TEAM_WORKFLOW_CONNECTION'),@('Import','IMPORT_TEST_CONNECTION'),@('Seats','SEAT_TEST_CONNECTION'),@('Academic','ACADEMIC4_TEST_CONNECTION'),@('Planner','PLANNER_TEST_CONNECTION'))) {
        Set-RunEnvironment $pair[1] (New-Fixture $pair[0])
    }
    Add-TestGrants 'Team'; Add-TestGrants 'Concurrent'
    Invoke-FixtureSql $fixtures.Concurrent ([IO.File]::ReadAllText((Join-Path $repo 'QuanLyGiangVien/database/tests/Concurrency.Setup.sql'))) 'concurrency-setup'
    Set-RunEnvironment 'MINH_DB_CONCURRENCY_OUTPUT' (Join-Path $output 'concurrency.json')
    Set-RunEnvironment 'SEAT_LOAD_OUTPUT' (Join-Path $output 'seat-load.json')
    $settings = Join-Path $output 'coverage.runsettings'
    [IO.File]::WriteAllText($settings,'<RunSettings><DataCollectionRunSettings><DataCollectors><DataCollector friendlyName="XPlat Code Coverage"><Configuration><Format>cobertura</Format><IncludeTestAssembly>true</IncludeTestAssembly><Include>[QuanLyGiangVien.Tests]QuanLyGiangVien.*</Include><ExcludeByFile>**/QuanLyGiangVien.Tests/**/*.cs,**/obj/**</ExcludeByFile></Configuration></DataCollector></DataCollectors></DataCollectionRunSettings></RunSettings>')
    Invoke-Tool 'dotnet' @('test','QuanLyGiangVien.Tests/QuanLyGiangVien.Tests.csproj','-c','Release','--no-restore','--artifacts-path',(Join-Path $output 'artifacts'),'--logger','trx;LogFileName=tests.trx','--results-directory',(Join-Path $output 'tests'),'--settings',$settings,'--collect','XPlat Code Coverage') 'tests.log'
    [xml]$trx = Get-Content -LiteralPath (Join-Path $output 'tests/tests.trx') -Raw
    $counters = $trx.TestRun.ResultSummary.Counters
    if ([int]$counters.total -le 0 -or [int]$counters.failed -ne 0 -or [int]$counters.NotExecuted -ne 0 -or [int]$counters.passed -ne [int]$counters.total) { throw 'Full test run requires all discovered tests to pass with zero skipped.' }
    $coverageFile = Find-CoverageFile (Join-Path $output 'tests')
    [xml]$coverage = Get-Content -LiteralPath $coverageFile.FullName -Raw
    $lineRate = [double]::Parse($coverage.coverage.'line-rate',[Globalization.CultureInfo]::InvariantCulture)
    if ($lineRate -lt 0.8) { throw 'Linked production-source line coverage must reach 80%.' }
    $summary.tests = @{total=[int]$counters.total;passed=[int]$counters.passed;skipped=[int]$counters.NotExecuted;lineCoverage=$lineRate;scope='Production source linked into test assembly; excludes remaining WinForms, worker and T-SQL.'}
    foreach ($pair in @(@('Native1','CAMPUS_PHASE1_UI_CHECK'),@('Native2','CAMPUS_PHASE2_UI_CHECK'),@('Native3','CAMPUS_PHASE3_UI_CHECK'),@('Native4','CAMPUS_PHASE4_UI_CHECK'),@('NativeAudit','CAMPUS_AUDIT_FIX_UI_CHECK'))) { Invoke-Native $pair[0] $pair[1] }
    $summary.passed = $true
} catch {
    $summary.error = $_.Exception.Message
} finally {
    foreach ($name in $owned) {
        try {
            Assert-OwnedName $name
            Invoke-Tool 'sqlcmd' @('-S',$Server,'-E','-C','-I','-x','-b','-l','5','-t','60','-d','master','-Q',"IF DB_ID(N'$name') IS NOT NULL BEGIN ALTER DATABASE [$name] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$name]; END;") ("cleanup-$name.log")
        } catch { $cleanupErrors.Add("$name : $($_.Exception.Message)") }
    }
    Restore-Environment
    if ($locationPushed) { Pop-Location }
    $summary.cleanupErrors = @($cleanupErrors)
    if ($cleanupErrors.Count) { $summary.passed=$false }
    $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'summary.json') -Encoding utf8
    Write-Host "Verification evidence: $output"
}
if (-not $summary.passed) { throw "Campus verification failed. See summary.json in $output" }
Write-Output "PASS: $($summary.tests.total) tests; $(@($summary.native | ForEach-Object { $_.checks } | Measure-Object -Sum).Sum) native checks; all owned fixtures removed."
