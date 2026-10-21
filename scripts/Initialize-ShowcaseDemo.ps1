#requires -Version 7.4
[CmdletBinding()]
param(
 [ValidatePattern('^(localhost|\.|\(local\))$')][string]$Server='.',
 [ValidatePattern('^DangKyTinChi_Minh_Test_Showcase_[a-f0-9]{32}$')][string]$DatabaseName=('DangKyTinChi_Minh_Test_Showcase_'+[Guid]::NewGuid().ToString('N')),
 [switch]$PlanOnly,[switch]$VerifyNative,[switch]$Ephemeral,[switch]$NoSelect
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if($Ephemeral -and -not $VerifyNative){throw 'Ephemeral requires VerifyNative.'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$run=$DatabaseName.Substring('DangKyTinChi_Minh_Test_Showcase_'.Length)
$output=Join-Path $repo ".tmp/showcase-20/$run"
if($PlanOnly){@{database=$DatabaseName;output=$output;keepDatabase=(-not $Ephemeral)}|ConvertTo-Json;return}
if(-not $IsWindows){throw 'Showcase requires Windows and local SQL Server.'}
foreach($tool in @('dotnet','sqlcmd')){if(-not(Get-Command $tool -ErrorAction SilentlyContinue)){throw "Required command: $tool"}}
if(Test-Path -LiteralPath $output){throw 'Showcase output already exists; no changes.'}
$exists=&sqlcmd -S $Server -E -C -I -b -x -l 5 -h -1 -W -d master -Q "SET NOCOUNT ON;SELECT CASE WHEN DB_ID(N'$DatabaseName') IS NULL THEN 0 ELSE 1 END;"
if($LASTEXITCODE-ne 0 -or ($exists|Out-String).Trim()-ne '0'){throw 'Database already exists or SQL unavailable; no changes.'}
[IO.Directory]::CreateDirectory($output)|Out-Null
$manifestPath=Join-Path $output 'manifest.json'
$manifest=[ordered]@{database=$DatabaseName;server=$Server;synthetic=$true;purpose='Dedicated showcase only; all academic evidence is synthetic.';createdUtc=[DateTime]::UtcNow.ToString('o');passed=$false;cleaned=$false;keepDatabase=(-not $Ephemeral);output=$output;credentialFile=(Join-Path $output 'accounts.private.txt');quickLoginFile=(Join-Path $output 'quick-login.dat');executable=(Join-Path $output 'artifacts/bin/QuanLyGiangVien/release/QuanLyGiangVien.exe');term='HK1-2627';source='SYNTHETIC DEMO - Campus showcase; not official student records';error=$null}
$owned=$false
function Save-Manifest {$manifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $manifestPath -Encoding utf8}
function Assert-Owned {
 if(-not $owned -or $DatabaseName -cnotmatch '^DangKyTinChi_Minh_Test_Showcase_[a-f0-9]{32}$' -or $manifest.database -cne $DatabaseName){throw 'Refusing mutation outside exact owned showcase database.'}
}
function Sql([string]$query,[string]$name){
 Assert-Owned
 $path=Join-Path $output ($name+'.sql')
 [IO.File]::WriteAllText($path,"IF DB_NAME()<>N'$DatabaseName' THROW 54990,N'Wrong showcase database',1;`n"+$query,[Text.UTF8Encoding]::new($false))
 &sqlcmd -S $Server -E -C -I -b -x -l 5 -t 120 -f 65001 -d $DatabaseName -i $path *> (Join-Path $output ($name+'.log'))
 if($LASTEXITCODE-ne 0){throw "Showcase SQL failed; see $name.log"}
}
function Cleanup {
 Assert-Owned
 &sqlcmd -S $Server -E -C -b -x -l 5 -d master -Q "IF DB_ID(N'$DatabaseName') IS NOT NULL BEGIN ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;DROP DATABASE [$DatabaseName];END;" *> (Join-Path $output 'cleanup.log')
 if($LASTEXITCODE-ne 0){throw 'Exact showcase cleanup failed; see cleanup.log.'}
 $manifest.cleaned=$true
}
try {
 # This previously absent exact UUID is the only database eligible for failure cleanup.
 $owned=$true
 Save-Manifest
 & (Join-Path $PSScriptRoot 'Initialize-Database.ps1') -Server $Server -DatabaseName $DatabaseName -SeedDemo -MapLocalLogin *> (Join-Path $output 'bootstrap.log')
 & (Join-Path $PSScriptRoot 'Initialize-LoginDemo.ps1') -Server $Server -DatabaseName $DatabaseName -CredentialFile $manifest.credentialFile *> (Join-Path $output 'accounts.log')
 # Restrict the plaintext handoff to this Windows user and SYSTEM before displaying its path.
 $acl=[Security.AccessControl.FileSecurity]::new();$acl.SetAccessRuleProtection($true,$false)
 foreach($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','Allow'))}
 Set-Acl -LiteralPath $manifest.credentialFile -AclObject $acl
 & (Join-Path $PSScriptRoot 'Initialize-FirstAdministrator.ps1') -Server $Server -DatabaseName $DatabaseName -CreateFirstAdministrator -Username 'showcase-admin' -Reason 'SYNTHETIC DEMO: dedicated newly-created showcase database only' -CredentialFile (Join-Path $output 'administrator.private.txt') *> (Join-Path $output 'administrator.log')
 Sql @'
SET NOCOUNT ON;SET XACT_ABORT ON;
BEGIN TRY BEGIN TRAN;
DECLARE @today date=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())),@start date,@finish date;
SET @start=DATEADD(day,-7,@today);SET @finish=DATEADD(day,84,@today);
UPDATE dbo.HOC_KY SET trang_thai='DANG_MO',lifecycle='REGISTRATION',tg_bat_dau=DATEADD(day,-2,@today),tg_ket_thuc=DATEADD(day,21,@today),han_huy_dk=DATEADD(day,21,@today),ngay_hoc_bd=@start,ngay_hoc_kt=@finish WHERE ma_hk='HK1-2627';
UPDATE dbo.HOC_KY SET ngay_hoc_bd=DATEADD(day,100,@today),ngay_hoc_kt=DATEADD(day,190,@today),tg_bat_dau=DATEADD(day,80,@today),tg_ket_thuc=DATEADD(day,99,@today),han_huy_dk=DATEADD(day,110,@today) WHERE ma_hk='HK2-2627';
UPDATE h SET ngay_bd=k.ngay_hoc_bd,ngay_kt=k.ngay_hoc_kt FROM dbo.LICH_HOC h JOIN dbo.LOP_HOC_PHAN l ON h.ma_lhp=l.ma_lhp JOIN dbo.HOC_KY k ON k.ma_hk=l.ma_hk;
DELETE dbo.DANG_KY WHERE ma_sv='SV2024001' AND ma_lhp IN('LHP0001','LHP0007');
UPDATE dbo.LOP_HOC_PHAN SET si_so_hien_tai=(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=LOP_HOC_PHAN.ma_lhp AND d.trang_thai='DANG_KY');
UPDATE dbo.LOP_HOC_PHAN SET trang_thai='DANG_MO',lifecycle='REGISTRATION' WHERE ma_lhp='LHP0007';
INSERT dbo.LOP_HOC_PHAN(ma_lhp,ma_hp,ma_gv,ma_hk,si_so_max,trang_thai,lifecycle) VALUES
 ('LHP0020','IT103','GV0009','HK1-2627',30,'DANG_MO','REGISTRATION'),
 ('LHP0021','IT103','GV0007','HK1-2627',30,'DANG_MO','REGISTRATION'),
 ('LHP0022','EN102','GV0008','HK1-2627',40,'DANG_MO','REGISTRATION'),
 ('LHP0023','EC102','GV0010','HK1-2627',1,'DANG_MO','REGISTRATION');
INSERT dbo.LICH_HOC(ma_lhp,ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt) VALUES
 ('LHP0020','A301',3,1,3,@start,@finish),('LHP0021','A301',6,7,9,@start,@finish),('LHP0022','A202',5,7,9,@start,@finish),('LHP0023','B201',7,1,3,@start,@finish);
INSERT dbo.DANG_KY(ma_sv,ma_lhp,ngay_dk,trang_thai) VALUES('SV2024002','LHP0023',DATEADD(hour,7,SYSUTCDATETIME()),'DANG_KY');
UPDATE dbo.LOP_HOC_PHAN SET si_so_hien_tai=1,trang_thai='FULL' WHERE ma_lhp='LHP0023';
DECLARE @mapped int=(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap=ORIGINAL_LOGIN()),@oldRole varchar(20),@oldStudent varchar(20);
IF @mapped IS NULL THROW 54991,N'Showcase requires an explicit local identity mapping',1;
SELECT @oldRole=vai_tro,@oldStudent=ma_sv FROM dbo.TAI_KHOAN WHERE ma_tk=@mapped;
UPDATE dbo.TAI_KHOAN SET vai_tro='QUAN_TRI',ma_sv=NULL WHERE ma_tk=@mapped;
EXEC dbo.usp_HocVu_Save 'PROGRAM','CNTT_2024',NULL,N'{"name":"Công nghệ thông tin · Khóa 2024","major":"CNTT","cohort":2024,"requiredCredits":23}';
EXEC dbo.usp_HocVu_Save 'GROUP','CO_SO',NULL,N'{"program":"CNTT_2024","name":"Nền tảng công nghệ","requiredCredits":9}';
EXEC dbo.usp_HocVu_Save 'GROUP','KY_NANG',NULL,N'{"program":"CNTT_2024","name":"Ngoại ngữ & kỹ năng","requiredCredits":5}';
EXEC dbo.usp_HocVu_Save 'GROUP','MO_RONG',NULL,N'{"program":"CNTT_2024","name":"Kiến thức mở rộng","requiredCredits":9}';
EXEC dbo.usp_HocVu_Save 'COURSE','CT_IT101',NULL,N'{"program":"CNTT_2024","course":"IT101","group":"CO_SO","required":true}';
EXEC dbo.usp_HocVu_Save 'COURSE','CT_IT102',NULL,N'{"program":"CNTT_2024","course":"IT102","group":"CO_SO","required":true}';
EXEC dbo.usp_HocVu_Save 'COURSE','CT_IT103',NULL,N'{"program":"CNTT_2024","course":"IT103","group":"CO_SO","required":true}';
EXEC dbo.usp_HocVu_Save 'COURSE','CT_EN101',NULL,N'{"program":"CNTT_2024","course":"EN101","group":"KY_NANG","required":true}';
EXEC dbo.usp_HocVu_Save 'COURSE','CT_EN102',NULL,N'{"program":"CNTT_2024","course":"EN102","group":"KY_NANG","required":true}';
EXEC dbo.usp_HocVu_Save 'COURSE','CT_EC101',NULL,N'{"program":"CNTT_2024","course":"EC101","group":"MO_RONG","required":true}';
EXEC dbo.usp_HocVu_Save 'COURSE','CT_EC102',NULL,N'{"program":"CNTT_2024","course":"EC102","group":"MO_RONG","required":true}';
EXEC dbo.usp_HocVu_Save 'COURSE','CT_ET101',NULL,N'{"program":"CNTT_2024","course":"ET101","group":"MO_RONG","required":true}';
DECLARE @version binary(8)=(SELECT version FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND id='CNTT_2024');
EXEC dbo.usp_HocVu_Publish 'PROGRAM','CNTT_2024',@version;
EXEC dbo.usp_HocVu_Save 'ASSIGNMENT','CT_MINH',NULL,N'{"student":"SV2024001","program":"CNTT_2024"}';
DECLARE @publication varchar(10)=CONVERT(varchar(10),DATEADD(day,-10,@today),23),@payload nvarchar(max);
SET @payload=(SELECT 'SV2024001' student,'IT101' course,'HK1-2627' term,1 attempt,'TRANSFER' recognition,CAST(1 AS bit) passed,@publication publication,N'SYNTHETIC DEMO - Campus showcase; not official records' source,N'Công nhận mô phỏng để trình diễn tiến độ, không phải điểm thật' reason FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
EXEC dbo.usp_HocVu_Save 'RESULT','KQ_IT101',NULL,@payload;
SET @payload=JSON_MODIFY(@payload,'$.course','EC101');EXEC dbo.usp_HocVu_Save 'RESULT','KQ_EC101',NULL,@payload;
EXEC dbo.usp_AP4_SaveOffering 'IT103','HK2-2627',N'SYNTHETIC DEMO - dự kiến mở môn minh họa';
EXEC dbo.usp_AP4_SaveOffering 'EN102','HK2-2627',N'SYNTHETIC DEMO - dự kiến mở môn minh họa';
EXEC dbo.usp_AP4_SaveOffering 'EC102','HK2-2627',N'SYNTHETIC DEMO - dự kiến mở môn minh họa';
DECLARE @advisor int=(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='daotao');
INSERT dbo.CAMPUS_PERMISSION SELECT @advisor,p.permission FROM (VALUES('CLASS_OPERATE'),('EXCEPTION_REVIEW'),('PROFILE_EDIT'),('HOLD_MANAGE'),('AUDIT_READ'),('SEAT_MANAGE'),('WAITLIST_OPERATE'),('ADVISE_PLAN'),('IMPORT_DATA'),('OPERATIONS_READ'))p(permission);
EXEC dbo.usp_AP4_AssignAdvisor 'SV2024001',@advisor,1,N'SYNTHETIC DEMO - phân công cố vấn trong database trình diễn';
EXEC dbo.usp_Seat_Save 'GROUP','CNTT24',NULL,N'{"name":"Sinh viên Công nghệ thông tin 2024","major":"CNTT","cohort":2024,"source":"SYNTHETIC DEMO - nhóm theo hồ sơ minh họa","priority":100}';
SET @payload=(SELECT 'HK1-2627' term,N'Đăng ký học phần · Đợt chính thức' name,DATEADD(day,-1,SYSUTCDATETIME()) startsUtc,DATEADD(day,21,SYSUTCDATETIME()) endsUtc,DATEADD(day,-1,SYSUTCDATETIME()) publicUtc,60 offerMinutes,'ELAPSED' calendarMode,CAST(1 AS bit) publishPosition FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
EXEC dbo.usp_Seat_Save 'ROUND','DOT_CHINH',NULL,@payload;
SET @payload=(SELECT 'DOT_CHINH' round,'CNTT24' [group],DATEADD(hour,-1,SYSUTCDATETIME()) opensUtc FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
EXEC dbo.usp_Seat_Save 'TICKET','HEN_CNTT24',NULL,@payload;
SET @payload=(SELECT 'LHP0021' class,'CNTT24' [group],10 capacity,DATEADD(day,20,SYSUTCDATETIME()) releaseUtc FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
EXEC dbo.usp_Seat_Save 'QUOTA','CHO_CNTT24',NULL,@payload;
UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024001' WHERE ma_tk=@mapped;
DECLARE @hash binary(32)=dbo.ufn_AP4_SourceHash('SV2024001','CNTT_2024');
EXEC dbo.usp_AP4_SavePlan 'CNTT_2024',@hash,N'[{"course":"IT103","term":"HK2-2627"},{"course":"EN102","term":"HK2-2627"},{"course":"EC102","term":"HK2-2627"}]';
DECLARE @plan binary(8)=(SELECT version FROM dbo.AP4_PLAN WHERE student='SV2024001');
EXEC dbo.usp_AP4_Submit 'SV2024001',@plan,@hash;
UPDATE dbo.TAI_KHOAN SET vai_tro=@oldRole,ma_sv=@oldStudent WHERE ma_tk=@mapped;
EXEC dbo.usp_Campus_KiemTraSchema;
COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
'@ 'showcase-seed'
 & (Join-Path $PSScriptRoot 'Enable-QuickLogin.ps1') -CredentialFile $manifest.credentialFile -OutputFile $manifest.quickLoginFile *> (Join-Path $output 'quick-login.log')
 &dotnet build (Join-Path $repo 'QuanLyGiangVien/QuanLyGiangVien.csproj') -c Release --artifacts-path (Join-Path $output 'artifacts') *> (Join-Path $output 'build.log')
 if($LASTEXITCODE-ne 0 -or -not(Test-Path -LiteralPath $manifest.executable)){throw 'Showcase build failed; see build.log.'}
 $manifest.passed=$true;Save-Manifest
 if($VerifyNative){& (Join-Path $PSScriptRoot 'Run-ShowcaseDemo.ps1') -Manifest $manifestPath -Verify *> (Join-Path $output 'native-launch.log')}
 if($Ephemeral){Cleanup}
} catch {
 $manifest.passed=$false;$manifest.error=$_.Exception.Message
 if($owned -and -not $manifest.cleaned){try{Cleanup}catch{$manifest.error+=' Cleanup: '+$_.Exception.Message}}
 throw
} finally {Save-Manifest}
if(-not $Ephemeral -and -not $NoSelect){
 # Only a completed retained demo becomes the one-click launch target.
 [IO.File]::WriteAllText((Join-Path (Split-Path $output -Parent) 'current-manifest-path.txt'),$manifestPath,[Text.UTF8Encoding]::new($false))
}
# No passwords or connection strings are printed. Launch/cleanup instructions use the exact manifest.
Write-Output ('{"manifest":'+(ConvertTo-Json $manifestPath -Compress)+'}')
Write-Host "Showcase ready. Manifest: $manifestPath"
if(-not $Ephemeral){Write-Host "Launch: pwsh -NoProfile -File scripts/Run-ShowcaseDemo.ps1 -Manifest `"$manifestPath`""}
