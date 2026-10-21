SET NOCOUNT ON; SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON;
IF DB_NAME()<>'DangKyTinChi_Minh_Test_Audit20261016_Core' THROW 52999,'Audit database only',1;
BEGIN TRY
 BEGIN TRAN;
 DECLARE @meet nvarchar(max)=N'[{"RoomId":"C101","Thu":8,"StartPeriod":10,"EndPeriod":12,"StartDate":"2026-09-07","EndDate":"2026-12-27"}]';
 EXEC dbo.usp_Minh_LuuLop 'LHP9898','IT101','GV0009','HK1-2627',60,'DANG_MO',@meet,1;
 INSERT dbo.DANG_KY(ma_sv,ma_lhp,ngay_dk,trang_thai,ngay_huy)
 VALUES('SV2024002','LHP9898','2026-09-01','DA_HUY','2026-09-02');
 DECLARE @id bigint=SCOPE_IDENTITY();
 SELECT 'BEFORE_HISTORY' evidence,d.ma_dk,v.ma_hp,v.ma_hk,d.trang_thai
 FROM dbo.DANG_KY d JOIN dbo.v_ThongTinLopHocPhan v ON v.ma_lhp=d.ma_lhp WHERE d.ma_dk=@id;
 SET @meet=N'[{"RoomId":"C101","Thu":8,"StartPeriod":10,"EndPeriod":12,"StartDate":"2027-01-11","EndDate":"2027-05-23"}]';
 EXEC dbo.usp_Minh_LuuLop 'LHP9898','IT102','GV0009','HK2-2627',60,'DANG_MO',@meet,0;
 IF NOT EXISTS(SELECT 1 FROM dbo.DANG_KY d JOIN dbo.v_ThongTinLopHocPhan v ON v.ma_lhp=d.ma_lhp
  WHERE d.ma_dk=@id AND v.ma_hp='IT102' AND v.ma_hk='HK2-2627') THROW 52998,'History reproduction not observed',1;
 SELECT 'AFTER_HISTORY' evidence,d.ma_dk,v.ma_hp,v.ma_hk,d.trang_thai
 FROM dbo.DANG_KY d JOIN dbo.v_ThongTinLopHocPhan v ON v.ma_lhp=d.ma_lhp WHERE d.ma_dk=@id;
 EXEC dbo.usp_SinhVien_Luu @la_moi=1,@ma_sv='SV_AUDIT',@ho_ten=N'Audit synthetic student',@ma_nganh='CNTT',@ma_lop='CNTT24A',@trang_thai=1;
 EXEC dbo.usp_SinhVien_TrangThai 'SV_AUDIT',0;
 SELECT 'AFTER_DISABLE' evidence,trang_thai FROM dbo.SINH_VIEN WHERE ma_sv='SV_AUDIT';
 -- Simulate a stale editor that loaded Active=true before another operator disabled the record.
 EXEC dbo.usp_SinhVien_Luu @la_moi=0,@ma_sv='SV_AUDIT',@ho_ten=N'Audit synthetic student',@sdt='0901234567',@ma_nganh='CNTT',@ma_lop='CNTT24A',@trang_thai=1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SINH_VIEN WHERE ma_sv='SV_AUDIT' AND trang_thai=1) THROW 52997,'Stale save reproduction not observed',1;
 SELECT 'AFTER_STALE_SAVE' evidence,trang_thai FROM dbo.SINH_VIEN WHERE ma_sv='SV_AUDIT';
 ROLLBACK;
 PRINT 'Both reproductions observed; all synthetic changes rolled back.';
END TRY BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
