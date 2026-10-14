/* Chạy trên database thử nghiệm sau 01,02,03. Không dùng transaction bao ngoài:
   procedure rollback toàn transaction khi lỗi. Mỗi case lỗi có TRY/CATCH riêng.
   Dữ liệu test dùng tiền tố ZTEST_ và được dọn sau khi thành công. */
USE DangKyTinChi;
GO
SET NOCOUNT ON;
IF EXISTS(SELECT 1 FROM dbo.HOC_PHAN WHERE ma_hp LIKE 'ZTEST[_]%') OR EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk LIKE 'ZTEST[_]%')
 THROW 51999,N'Còn dữ liệu ZTEST_ từ lần test trước. Kiểm tra rồi dọn trên database thử nghiệm.',1;
DECLARE @now datetime2(0)=DATEADD(HOUR,7,SYSUTCDATETIME()),@a datetime2(0),@b datetime2(0),@c datetime2(0),@token binary(32),@old binary(32);
SET @a=DATEADD(DAY,-1,@now);SET @b=DATEADD(DAY,10,@now);SET @c=DATEADD(DAY,20,@now);
EXEC dbo.usp_HocPhan_Luu 1,'ZTEST_HP',N'Học phần kiểm thử',3,'BAT_BUOC',3,3,1;
IF NOT EXISTS(SELECT 1 FROM dbo.HOC_PHAN WHERE ma_hp='ZTEST_HP') THROW 51999,N'Thêm HP thất bại',1;
PRINT 'PASS: add course';
BEGIN TRY
 EXEC dbo.usp_HocPhan_Luu 1,'ZTEST_HP',N'Trùng mã',3,'BAT_BUOC',3,3,1;
 THROW 51999,N'Không chặn trùng HP',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>51005 THROW; END CATCH;
PRINT 'PASS: duplicate code';
BEGIN TRY
 EXEC dbo.usp_HocPhan_Luu 1,'ZTEST_BAD',N'Tín chỉ lỗi',3,'TU_CHON',4,5,1;
 THROW 51999,N'Không chặn tín chỉ sai',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>51004 THROW; END CATCH;
PRINT 'PASS: credit bounds';
BEGIN TRY
 INSERT dbo.HOC_PHAN VALUES('ZTEST_BAD',N'Chèn thẳng',0,'TU_CHON',0,0,1);
 THROW 51999,N'CHECK không chặn tín chỉ sai',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>547 THROW; END CATCH;
PRINT 'PASS: direct SQL constraint';
SELECT @old=phien_ban FROM dbo.vw_HocPhanPhong WHERE ma_hp='ZTEST_HP';
EXEC dbo.usp_HocPhan_Luu 0,'ZTEST_HP',N'Học phần đã sửa',3,'TU_CHON',3,3,1,@old;
BEGIN TRY
 EXEC dbo.usp_HocPhan_Luu 0,'ZTEST_HP',N'Ghi đè dữ liệu cũ',3,'BAT_BUOC',3,3,1,@old;
 THROW 51999,N'Không chặn token cũ',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>51007 THROW; END CATCH;
PRINT 'PASS: stale update';
-- Năm 2998 dành riêng test, không sử dụng cho dữ liệu thật.
EXEC dbo.usp_HocKy_Luu 1,'ZTEST_HK','2998-2999',1,@a,@b,@c;
IF EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk='ZTEST_HK' AND trang_thai<>'DONG') THROW 51999,N'HK mới phải đóng',1;
PRINT 'PASS: new semester closed';
BEGIN TRY
 EXEC dbo.usp_HocKy_Luu 1,'ZTEST_DUP','2998-2999',1,@a,@b,@c;
 THROW 51999,N'Không chặn trùng năm/kỳ',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>51105 THROW; END CATCH;
PRINT 'PASS: unique semester';
BEGIN TRY
 EXEC dbo.usp_HocKy_Luu 1,'ZTEST_BAD','2998-2999',2,@b,@a,@c;
 THROW 51999,N'Không chặn thời gian đảo',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>51104 THROW; END CATCH;
PRINT 'PASS: reversed dates';
SELECT @token=phien_ban FROM dbo.vw_HocKyPhong WHERE ma_hk='ZTEST_HK';
EXEC dbo.usp_HocKy_DoiTrangThai 'ZTEST_HK','MO',@token;
IF NOT EXISTS(SELECT 1 FROM dbo.vw_HocKyPhong WHERE ma_hk='ZTEST_HK' AND cho_phep_dang_ky=1 AND cho_phep_huy=1) THROW 51999,N'Đợt mở không hiệu lực',1;
PRINT 'PASS: open active window';
SELECT @token=phien_ban FROM dbo.vw_HocKyPhong WHERE ma_hk='ZTEST_HK';
EXEC dbo.usp_HocKy_DoiTrangThai 'ZTEST_HK','DONG',@token;
IF NOT EXISTS(SELECT 1 FROM dbo.vw_HocKyPhong WHERE ma_hk='ZTEST_HK' AND cho_phep_dang_ky=0 AND cho_phep_huy=1) THROW 51999,N'Đóng và hạn hủy sai',1;
PRINT 'PASS: close registration / independent cancellation';
-- Tương lai: MO nhưng chưa đến hạn, không cho đăng ký.
SET @a=DATEADD(DAY,1,@now);SET @b=DATEADD(DAY,2,@now);SET @c=DATEADD(DAY,3,@now);
SELECT @token=phien_ban FROM dbo.vw_HocKyPhong WHERE ma_hk='ZTEST_HK';
EXEC dbo.usp_HocKy_Luu 0,'ZTEST_HK','2998-2999',1,@a,@b,@c,@token;
SELECT @token=phien_ban FROM dbo.vw_HocKyPhong WHERE ma_hk='ZTEST_HK';
EXEC dbo.usp_HocKy_DoiTrangThai 'ZTEST_HK','MO',@token;
IF NOT EXISTS(SELECT 1 FROM dbo.vw_HocKyPhong WHERE ma_hk='ZTEST_HK' AND cho_phep_dang_ky=0 AND trang_thai='MO') THROW 51999,N'Không chặn đợt tương lai',1;
PRINT 'PASS: future window';
-- Đợt đã hết hạn: MO trên cấu hình nhưng view không cho đăng ký.
SET @a=DATEADD(DAY,-3,@now);SET @b=DATEADD(DAY,-2,@now);SET @c=DATEADD(DAY,-1,@now);
SELECT @token=phien_ban FROM dbo.vw_HocKyPhong WHERE ma_hk='ZTEST_HK';
EXEC dbo.usp_HocKy_Luu 0,'ZTEST_HK','2998-2999',1,@a,@b,@c,@token;
IF NOT EXISTS(SELECT 1 FROM dbo.vw_HocKyPhong WHERE ma_hk='ZTEST_HK' AND cho_phep_dang_ky=0 AND cho_phep_huy=0) THROW 51999,N'Không chặn đợt hết hạn',1;
SELECT @token=phien_ban FROM dbo.vw_HocKyPhong WHERE ma_hk='ZTEST_HK';
BEGIN TRY
 EXEC dbo.usp_HocKy_DoiTrangThai 'ZTEST_HK','MO',@token;
 THROW 51999,N'Không chặn mở đợt hết hạn',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>51204 THROW; END CATCH;
PRINT 'PASS: expired window';
-- FK / sĩ số và bảo toàn tín chỉ sau đăng ký: dùng hồ sơ giả của 03.
INSERT dbo.LOP_HOC_PHAN(ma_lhp,ma_hp,ma_gv,ma_hk,ma_phong,si_so_max)
VALUES('ZTEST_LHP','ZTEST_HP','GV_DEMO01','ZTEST_HK','A301',1);
INSERT dbo.DANG_KY(ma_sv,ma_lhp) VALUES('SV_DEMO01','ZTEST_LHP');
IF NOT EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='ZTEST_LHP' AND si_so_hien_tai=1)
 THROW 51999,N'Trigger tăng sĩ số sai',1;
PRINT 'PASS: enrollment increments count';
BEGIN TRY
 INSERT dbo.DANG_KY(ma_sv,ma_lhp) VALUES('SV_DEMO01','ZTEST_LHP');
 THROW 51999,N'Không chặn đăng ký trùng',1;
END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN(2601,2627) THROW; END CATCH;
PRINT 'PASS: duplicate active enrollment';
BEGIN TRY
 UPDATE dbo.LOP_HOC_PHAN SET si_so_max=0 WHERE ma_lhp='ZTEST_LHP';
 THROW 51999,N'Không chặn giảm sức chứa sai',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>547 THROW; END CATCH;
PRINT 'PASS: capacity constraint';
SELECT @token=phien_ban FROM dbo.vw_HocPhanPhong WHERE ma_hp='ZTEST_HP';
BEGIN TRY
 EXEC dbo.usp_HocPhan_Luu 0,'ZTEST_HP',N'Đổi tín chỉ sau đăng ký',4,'TU_CHON',4,4,1,@token;
 THROW 51999,N'Không chặn đổi tín chỉ sau đăng ký',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>51008 THROW; END CATCH;
PRINT 'PASS: historical credits protected';
SELECT @token=phien_ban FROM dbo.vw_HocKyPhong WHERE ma_hk='ZTEST_HK';
BEGIN TRY
 EXEC dbo.usp_HocKy_Luu 0,'ZTEST_HK','2998-2999',2,@a,@b,@c,@token;
 THROW 51999,N'Không chặn đổi số học kỳ đã có lớp',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>51109 THROW; END CATCH;
PRINT 'PASS: semester identity protected';
UPDATE dbo.DANG_KY SET trang_thai='DA_HUY',ngay_huy=DATEADD(HOUR,7,SYSUTCDATETIME()) WHERE ma_lhp='ZTEST_LHP';
IF NOT EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='ZTEST_LHP' AND si_so_hien_tai=0)
 THROW 51999,N'Trigger giảm sĩ số sai',1;
PRINT 'PASS: cancellation decrements count';
DELETE dbo.DANG_KY WHERE ma_lhp='ZTEST_LHP';
DELETE dbo.LOP_HOC_PHAN WHERE ma_lhp='ZTEST_LHP';
DELETE dbo.HOC_PHAN WHERE ma_hp='ZTEST_HP';DELETE dbo.HOC_KY WHERE ma_hk='ZTEST_HK';
PRINT N'PASS toàn bộ smoke tests. Đã dọn dữ liệu test.';
GO
