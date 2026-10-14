USE DangKyTinChi;
GO
SET NOCOUNT ON; SET XACT_ABORT ON;
BEGIN TRANSACTION;
-- Dữ liệu giả lập để demo, không phải dữ liệu sinh viên thực tế. Chạy nhiều lần không trùng mã.
IF NOT EXISTS(SELECT 1 FROM dbo.KHOA WHERE ma_khoa='CNTT')
 INSERT dbo.KHOA(ma_khoa,ten_khoa,mo_ta) VALUES('CNTT',N'Công nghệ thông tin',N'Dữ liệu demo');
IF NOT EXISTS(SELECT 1 FROM dbo.NGANH WHERE ma_nganh='ATTT')
 INSERT dbo.NGANH(ma_nganh,ten_nganh,ma_khoa) VALUES('ATTT',N'An toàn thông tin','CNTT');
IF NOT EXISTS(SELECT 1 FROM dbo.LOP WHERE ma_lop='ATTT_K75')
 INSERT dbo.LOP(ma_lop,ten_lop,ma_nganh,khoa_hoc,nien_khoa) VALUES('ATTT_K75',N'An toàn thông tin K75','ATTT',75,'2024-2028');
IF NOT EXISTS(SELECT 1 FROM dbo.SINH_VIEN WHERE ma_sv='SV_DEMO01')
 INSERT dbo.SINH_VIEN(ma_sv,ho_ten,ngay_sinh,gioi_tinh,ma_nganh,ma_lop) VALUES('SV_DEMO01',N'Sinh viên demo','2004-01-01',N'Nam','ATTT','ATTT_K75');
IF NOT EXISTS(SELECT 1 FROM dbo.GIANG_VIEN WHERE ma_gv='GV_DEMO01')
 INSERT dbo.GIANG_VIEN(ma_gv,ho_ten,ngay_sinh,gioi_tinh,ma_khoa) VALUES('GV_DEMO01',N'Giảng viên demo','1980-01-01',N'Nữ','CNTT');
IF NOT EXISTS(SELECT 1 FROM dbo.PHONG_HOC WHERE ma_phong='A301')
 INSERT dbo.PHONG_HOC(ma_phong,ten_phong,toa_nha,suc_chua,loai_phong) VALUES('A301',N'Phòng A301',N'Tòa A',60,N'Lý thuyết');
DECLARE @mau TABLE(ma_hp varchar(20),ten_hp nvarchar(200),so_tin_chi tinyint,loai_hp varchar(20));
INSERT @mau VALUES
 ('CS101',N'Lập trình C',3,'BAT_BUOC'),('CS102',N'Cơ sở dữ liệu',3,'BAT_BUOC'),
 ('CS103',N'Lập trình C# WinForms',3,'BAT_BUOC'),('SEC201',N'An toàn hệ thống thông tin',3,'BAT_BUOC'),
 ('SEC202',N'Kiểm thử an toàn ứng dụng web',3,'TU_CHON'),('SEC203',N'Điều tra số',2,'TU_CHON'),
 ('MAT101',N'Đại số tuyến tính',3,'BAT_BUOC'),('ENG201',N'Tiếng Anh chuyên ngành',2,'TU_CHON');
INSERT dbo.HOC_PHAN(ma_hp,ten_hp,so_tin_chi,loai_hp,tin_chi_min,tin_chi_max,trang_thai)
 SELECT ma_hp,ten_hp,so_tin_chi,loai_hp,so_tin_chi,so_tin_chi,1 FROM @mau m
 WHERE NOT EXISTS(SELECT 1 FROM dbo.HOC_PHAN h WHERE h.ma_hp=m.ma_hp);
DECLARE @now datetime2(0)=DATEADD(HOUR,7,SYSUTCDATETIME()),@year int=YEAR(DATEADD(HOUR,7,SYSUTCDATETIME()));
DECLARE @nh varchar(9)=CONCAT(@year,'-',@year+1),@hk varchar(20)=CONCAT('HK1_',@year);
IF NOT EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE nam_hoc=@nh AND hoc_ky=1)
 INSERT dbo.HOC_KY(ma_hk,nam_hoc,hoc_ky,tg_bat_dau,tg_ket_thuc,han_huy_dk,trang_thai)
 VALUES(@hk,@nh,1,DATEADD(DAY,-1,@now),DATEADD(DAY,14,@now),DATEADD(DAY,21,@now),'DONG');
-- Không mở đợt tự động: vào tab Học kỳ và bấm Mở đợt để demo.
SELECT TOP(1) @hk=ma_hk FROM dbo.HOC_KY WHERE nam_hoc=@nh AND hoc_ky=1;
IF NOT EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='CS101_DEMO')
 INSERT dbo.LOP_HOC_PHAN(ma_lhp,ma_hp,ma_gv,ma_hk,ma_phong,si_so_max)
 VALUES('CS101_DEMO','CS101','GV_DEMO01',@hk,'A301',40);
IF NOT EXISTS(SELECT 1 FROM dbo.LICH_HOC WHERE ma_lhp='CS101_DEMO')
 INSERT dbo.LICH_HOC(ma_lhp,thu,tiet_bd,tiet_kt,ma_phong,ngay_bd,ngay_kt)
 VALUES('CS101_DEMO',2,1,3,'A301',CAST(@now AS date),CAST(DATEADD(DAY,90,@now) AS date));
COMMIT;
PRINT N'Đã thêm dữ liệu demo. Không tạo tài khoản hay mật khẩu mặc định.';
GO
