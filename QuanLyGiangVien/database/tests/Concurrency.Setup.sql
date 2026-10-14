SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
IF DB_NAME() NOT LIKE N'DangKyTinChi_Minh_Test[_]%'
 THROW 52099,N'Concurrency chỉ chạy trên database kiểm thử dùng một lần.',1;
UPDATE dbo.TAI_KHOAN SET vai_tro='DAO_TAO',ma_sv=NULL,trang_thai=1 WHERE ten_dang_nhap=ORIGINAL_LOGIN();
DECLARE @lich nvarchar(max)=N'[{"RoomId":"C101","Thu":8,"StartPeriod":10,"EndPeriod":12,"StartDate":"2026-09-07","EndDate":"2026-12-27"}]';
EXEC dbo.usp_Minh_LuuLop 'TEST_CONCURRENT','IT101','GV0009','HK1-2627',60,'DANG_MO',@lich,1;
-- Open only the disposable test semester so Huệ's real procedure passes its deadline checks.
UPDATE dbo.HOC_KY SET tg_bat_dau=DATEADD(DAY,-1,DATEADD(HOUR,7,SYSUTCDATETIME())),
 tg_ket_thuc=DATEADD(DAY,1,DATEADD(HOUR,7,SYSUTCDATETIME())),
 han_huy_dk=DATEADD(DAY,1,DATEADD(HOUR,7,SYSUTCDATETIME())),trang_thai='DANG_MO' WHERE ma_hk='HK1-2627';
UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024002' WHERE ten_dang_nhap=ORIGINAL_LOGIN();
GO
-- Pause inside Huệ's own transaction after INSERT. Authentication has already
-- completed outside that transaction, so changing the test-only identity mapping
-- does not serialize the two sessions before Minh reaches the class lock.
CREATE OR ALTER TRIGGER dbo.tr_Minh_Test_PauseHue ON dbo.DANG_KY AFTER INSERT
AS
BEGIN
 SET NOCOUNT ON;
 IF DB_NAME() LIKE N'DangKyTinChi_Minh_Test[_]%'
  AND EXISTS(SELECT 1 FROM inserted WHERE ma_lhp='TEST_CONCURRENT')
 BEGIN
  RAISERROR(N'HUE_LOCK_ACQUIRED',0,1) WITH NOWAIT;
  WAITFOR DELAY '00:00:08';
 END;
END;
GO
