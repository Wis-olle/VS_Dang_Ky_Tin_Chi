[CmdletBinding()]
param([switch]$OpenRegistration,
    [ValidatePattern('^(localhost|\.|\(local\))(\\[A-Za-z0-9_]+)?$')][string]$Server = '.')

$ErrorActionPreference = 'Stop'
# A separate local fixture for opening Huệ's student UI. Never remap the working DB.
$previewDatabase = 'DangKyTinChi_Minh_Test_StudentPreview'
$exists = & sqlcmd -S $Server -E -C -I -b -h -1 -W -d master -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$previewDatabase') IS NULL THEN 0 ELSE 1 END;"
if ($LASTEXITCODE -ne 0) { throw 'Không kết nối được SQL Server local.' }
$createdHere = ($exists | Out-String).Trim() -eq '0'
if ($createdHere) {
    & (Join-Path $PSScriptRoot 'Initialize-Database.ps1') -Server $Server -DatabaseName $previewDatabase -SeedDemo -MapLocalLogin
} else {
    & (Join-Path $PSScriptRoot 'Initialize-Database.ps1') -Server $Server -DatabaseName $previewDatabase -UpgradeExisting
}

# Only a newly created fixture may change its bootstrap management mapping.
$allowInitialMapping = if ($createdHere) { 1 } else { 0 }
$openWindow = if ($OpenRegistration) { 1 } else { 0 }
$query = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'DangKyTinChi_Minh_Test_StudentPreview'
 THROW 52099,N'Chỉ dùng database xem thử sinh viên riêng.',1;
BEGIN TRAN;
IF $allowInitialMapping=1
 UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024001'
 WHERE ten_dang_nhap=ORIGINAL_LOGIN() AND vai_tro='DAO_TAO' AND ma_sv IS NULL AND trang_thai=1;
IF NOT EXISTS(SELECT 1 FROM dbo.TAI_KHOAN WHERE ten_dang_nhap=ORIGINAL_LOGIN()
 AND vai_tro='SINH_VIEN' AND ma_sv='SV2024001' AND trang_thai=1)
BEGIN
 ROLLBACK;
 THROW 52099,N'Database đã có ánh xạ khác; không tự đổi quyền. Kiểm tra cấu hình preview.',1;
END;
IF $openWindow=1
BEGIN
 DECLARE @now datetime2(0)=CONVERT(datetime2(0),DATEADD(HOUR,7,SYSUTCDATETIME()));
 UPDATE dbo.HOC_KY SET trang_thai='DANG_MO',tg_bat_dau=DATEADD(MINUTE,-5,@now),
 tg_ket_thuc=DATEADD(DAY,7,@now),han_huy_dk=DATEADD(DAY,7,@now)
 WHERE ma_hk='HK1-2627';
 IF @@ROWCOUNT<>1
 BEGIN
  ROLLBACK;
  THROW 52099,N'Không tìm thấy học kỳ mẫu HK1-2627; chưa mở đợt.',1;
 END;
END;
COMMIT;
EXEC dbo.usp_LayNguoiDung;
IF $openWindow=1 EXEC dbo.usp_KiemTraThoiHan @ma_hk='HK1-2627';
"@
& sqlcmd -S $Server -E -C -I -b -f 65001 -d $previewDatabase -Q $query
if ($LASTEXITCODE -ne 0) { throw 'Chưa thiết lập được tài khoản xem thử sinh viên.' }
Write-Host 'Sẵn sàng: chọn profile DangKyHocPhanSinhVien hoặc ThoiKhoaBieuSinhVien, nhấn F5 để mở cổng sinh viên hai tab.'
