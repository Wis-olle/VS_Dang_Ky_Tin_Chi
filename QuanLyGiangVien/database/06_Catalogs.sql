-- Phong_Function@da04d8b adapted additively to Huệ + Minh schema. No seeds or automatic opening.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('dbo.usp_Minh_KiemTraQuyen','P') IS NULL OR COL_LENGTH('dbo.HOC_KY','ngay_hoc_bd') IS NULL
 THROW 52100,N'Cần khởi tạo SQL Huệ và migration Minh trước.',1;
GO
CREATE OR ALTER VIEW dbo.vw_Phong_HocPhan AS
 SELECT h.*,HASHBYTES('SHA2_256',CONVERT(nvarchar(max),CONCAT(LEN(ten_hp),':',ten_hp,'|',so_tin_chi,'|',loai_hp,'|',
 COALESCE(CONVERT(varchar(3),tin_chi_min),'NULL'),'|',COALESCE(CONVERT(varchar(3),tin_chi_max),'NULL'),'|',trang_thai,'|',CONCAT(CASE WHEN ma_khoa IS NULL THEN -1 ELSE LEN(ma_khoa) END,':',ma_khoa)))) phien_ban
 FROM dbo.HOC_PHAN h;
GO
CREATE OR ALTER VIEW dbo.vw_Phong_HocKy AS
 SELECT h.*,HASHBYTES('SHA2_256',CONVERT(nvarchar(max),CONCAT(nam_hoc,'|',hoc_ky,'|',CONVERT(varchar(19),tg_bat_dau,126),'|',
 CONVERT(varchar(19),tg_ket_thuc,126),'|',CONVERT(varchar(19),han_huy_dk,126),'|',trang_thai,'|',
 COALESCE(CONVERT(varchar(10),ngay_hoc_bd,126),'NULL'),'|',COALESCE(CONVERT(varchar(10),ngay_hoc_kt,126),'NULL')))) phien_ban
 FROM dbo.HOC_KY h;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Phong_Khoa_DanhSach AS
BEGIN
 SET NOCOUNT ON;EXEC dbo.usp_Minh_KiemTraQuyen;
 SELECT ma_khoa,ten_khoa,trang_thai FROM dbo.KHOA ORDER BY ten_khoa,ma_khoa;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Phong_HocPhan_DanhSach
 @tu_khoa nvarchar(200)=N'',@loai_hp varchar(20)=NULL,@trang_thai bit=NULL AS
BEGIN
 SET NOCOUNT ON;EXEC dbo.usp_Minh_KiemTraQuyen;
 SELECT * FROM dbo.vw_Phong_HocPhan
 WHERE (NULLIF(@tu_khoa,N'') IS NULL OR CHARINDEX(@tu_khoa,ma_hp)>0 OR CHARINDEX(@tu_khoa,ten_hp)>0)
 AND (@loai_hp IS NULL OR loai_hp=@loai_hp) AND (@trang_thai IS NULL OR trang_thai=@trang_thai)
 ORDER BY ma_hp;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Phong_HocKy_DanhSach @tu_khoa nvarchar(200)=N'' AS
BEGIN
 SET NOCOUNT ON;EXEC dbo.usp_Minh_KiemTraQuyen;
 DECLARE @now datetime2(0)=DATEADD(HOUR,7,SYSUTCDATETIME());
 SELECT *,CASE WHEN trang_thai='DONG' THEN N'Đã đóng (kể cả hủy)' WHEN @now<tg_bat_dau THEN N'Chưa đến hạn'
 WHEN @now<=tg_ket_thuc THEN N'Đang nhận đăng ký' WHEN @now<=han_huy_dk THEN N'Chỉ còn thời hạn hủy' ELSE N'Đã hết hạn' END trang_thai_hieu_luc
 FROM dbo.vw_Phong_HocKy WHERE NULLIF(@tu_khoa,N'') IS NULL OR CHARINDEX(@tu_khoa,ma_hk)>0 OR CHARINDEX(@tu_khoa,nam_hoc)>0
 ORDER BY nam_hoc DESC,hoc_ky DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Phong_HocPhan_Luu
 @la_moi bit,@ma_hp varchar(128),@ten_hp nvarchar(4000),@so_tin_chi tinyint,@loai_hp varchar(20),
 @tin_chi_min tinyint,@tin_chi_max tinyint,@trang_thai bit,@ma_khoa varchar(20)=NULL,@phien_ban binary(32)=NULL AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;EXEC dbo.usp_Minh_KiemTraQuyen;
 IF NULLIF(TRIM(@ma_hp),'') IS NULL OR LEN(@ma_hp)>20 OR @ma_hp LIKE '%[^A-Za-z0-9_-]%' COLLATE Latin1_General_100_BIN2
 OR NULLIF(TRIM(@ten_hp),N'') IS NULL OR DATALENGTH(TRIM(@ten_hp))/2>150 OR @so_tin_chi NOT BETWEEN 1 AND 12 OR @so_tin_chi IS NULL
 OR @loai_hp NOT IN ('BAT_BUOC','TU_CHON') OR @loai_hp IS NULL OR @tin_chi_min IS NULL OR @tin_chi_max IS NULL
 OR @tin_chi_min<1 OR @tin_chi_min>@so_tin_chi OR @tin_chi_max<@so_tin_chi OR @tin_chi_max>30
 OR @trang_thai IS NULL OR @la_moi IS NULL THROW 52100,N'Thông tin học phần không hợp lệ.',1;
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_Minh_KhoaLapLich;
  DECLARE @lock varchar(20),@oldFaculty varchar(20);
  SELECT @lock=h.ma_hk FROM dbo.HOC_KY h WITH(UPDLOCK,HOLDLOCK) WHERE EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN l WHERE l.ma_hk=h.ma_hk AND l.ma_hp=@ma_hp) ORDER BY h.ma_hk;
  SELECT @lock=ma_lhp FROM dbo.LOP_HOC_PHAN WITH(UPDLOCK,HOLDLOCK) WHERE ma_hp=@ma_hp ORDER BY ma_lhp;
  SELECT @oldFaculty=ma_khoa FROM dbo.HOC_PHAN WITH(UPDLOCK,HOLDLOCK) WHERE ma_hp=@ma_hp;
  IF @la_moi=1 AND EXISTS(SELECT 1 FROM dbo.HOC_PHAN WHERE ma_hp=@ma_hp) THROW 52106,N'Mã học phần đã tồn tại.',1;
  IF @la_moi=0 AND NOT EXISTS(SELECT 1 FROM dbo.vw_Phong_HocPhan WHERE ma_hp=@ma_hp AND phien_ban=@phien_ban) THROW 52101,N'Phiên bản học phần đã thay đổi.',1;
  IF @ma_khoa IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.KHOA WITH(HOLDLOCK) WHERE ma_khoa=@ma_khoa AND trang_thai=1)
   AND (@la_moi=1 OR @oldFaculty IS NULL OR @oldFaculty<>@ma_khoa) THROW 52104,N'Khoa không hoạt động.',1;
  IF @trang_thai=0 AND EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_hp=@ma_hp AND trang_thai<>'HUY') THROW 52102,N'Học phần còn lớp đang sử dụng.',1;
  IF @la_moi=0 AND (EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN l JOIN dbo.DANG_KY d ON d.ma_lhp=l.ma_lhp WHERE l.ma_hp=@ma_hp AND d.trang_thai='DANG_KY')
    OR EXISTS(SELECT 1 FROM dbo.KET_QUA WHERE ma_hp=@ma_hp))
   AND EXISTS(SELECT 1 FROM dbo.HOC_PHAN WHERE ma_hp=@ma_hp AND (ten_hp<>TRIM(@ten_hp) OR so_tin_chi<>@so_tin_chi OR loai_hp<>@loai_hp
     OR ISNULL(tin_chi_min,so_tin_chi)<>@tin_chi_min OR ISNULL(tin_chi_max,so_tin_chi)<>@tin_chi_max OR ISNULL(ma_khoa,'')<>ISNULL(@ma_khoa,''))) THROW 52102,N'Học phần đã có đăng ký/kết quả.',1;
  IF @la_moi=1 INSERT dbo.HOC_PHAN(ma_hp,ten_hp,so_tin_chi,loai_hp,tin_chi_min,tin_chi_max,trang_thai,ma_khoa)
    VALUES(@ma_hp,TRIM(@ten_hp),@so_tin_chi,@loai_hp,@tin_chi_min,@tin_chi_max,@trang_thai,@ma_khoa);
  ELSE UPDATE dbo.HOC_PHAN SET ten_hp=TRIM(@ten_hp),so_tin_chi=@so_tin_chi,loai_hp=@loai_hp,tin_chi_min=@tin_chi_min,tin_chi_max=@tin_chi_max,trang_thai=@trang_thai,ma_khoa=@ma_khoa WHERE ma_hp=@ma_hp;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Phong_HocKy_Luu
 @la_moi bit,@ma_hk varchar(20),@nam_hoc varchar(9),@hoc_ky tinyint,@tg_bat_dau datetime2(0),@tg_ket_thuc datetime2(0),
 @han_huy_dk datetime2(0),@ngay_hoc_bd date,@ngay_hoc_kt date,@phien_ban binary(32)=NULL AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;EXEC dbo.usp_Minh_KiemTraQuyen;
 IF NULLIF(TRIM(@ma_hk),'') IS NULL OR @ma_hk LIKE '%[^A-Za-z0-9_-]%' COLLATE Latin1_General_100_BIN2
 OR @nam_hoc IS NULL OR @nam_hoc NOT LIKE '[12][0-9][0-9][0-9]-[12][0-9][0-9][0-9]' COLLATE Latin1_General_100_BIN2
 OR TRY_CONVERT(int,RIGHT(@nam_hoc,4))<>TRY_CONVERT(int,LEFT(@nam_hoc,4))+1 OR @hoc_ky NOT BETWEEN 1 AND 3 OR @hoc_ky IS NULL
 OR @tg_bat_dau IS NULL OR @tg_ket_thuc IS NULL OR @han_huy_dk IS NULL OR @tg_bat_dau>=@tg_ket_thuc OR @han_huy_dk<@tg_bat_dau
 OR @ngay_hoc_bd IS NULL OR @ngay_hoc_kt IS NULL OR @ngay_hoc_bd<'20000101' OR @ngay_hoc_bd>@ngay_hoc_kt
 OR DATEDIFF(DAY,@ngay_hoc_bd,@ngay_hoc_kt)>366 OR @la_moi IS NULL THROW 52100,N'Thông tin học kỳ không hợp lệ.',1;
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_Minh_KhoaLapLich;
  DECLARE @lock varchar(20);
  SELECT @lock=ma_hk FROM dbo.HOC_KY WITH(UPDLOCK,HOLDLOCK) WHERE ma_hk=@ma_hk;
  SELECT @lock=ma_lhp FROM dbo.LOP_HOC_PHAN WITH(UPDLOCK,HOLDLOCK) WHERE ma_hk=@ma_hk ORDER BY ma_lhp;
  IF @la_moi=1 AND EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@ma_hk) THROW 52106,N'Mã học kỳ đã tồn tại.',1;
  IF @la_moi=0 AND NOT EXISTS(SELECT 1 FROM dbo.vw_Phong_HocKy WHERE ma_hk=@ma_hk AND phien_ban=@phien_ban) THROW 52101,N'Phiên bản học kỳ đã thay đổi.',1;
  IF EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN l JOIN dbo.LICH_HOC c ON c.ma_lhp=l.ma_lhp WHERE l.ma_hk=@ma_hk AND (c.ngay_bd<@ngay_hoc_bd OR c.ngay_kt>@ngay_hoc_kt)) THROW 52103,N'Khoảng ngày học không chứa lịch lớp.',1;
  IF @la_moi=1 INSERT dbo.HOC_KY(ma_hk,nam_hoc,hoc_ky,tg_bat_dau,tg_ket_thuc,han_huy_dk,trang_thai,ngay_hoc_bd,ngay_hoc_kt)
   VALUES(@ma_hk,@nam_hoc,@hoc_ky,@tg_bat_dau,@tg_ket_thuc,@han_huy_dk,'DONG',@ngay_hoc_bd,@ngay_hoc_kt);
  ELSE UPDATE dbo.HOC_KY SET nam_hoc=@nam_hoc,hoc_ky=@hoc_ky,tg_bat_dau=@tg_bat_dau,tg_ket_thuc=@tg_ket_thuc,han_huy_dk=@han_huy_dk,ngay_hoc_bd=@ngay_hoc_bd,ngay_hoc_kt=@ngay_hoc_kt WHERE ma_hk=@ma_hk;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Phong_HocKy_DoiTrangThai @ma_hk varchar(20),@trang_thai varchar(12),@phien_ban binary(32) AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;EXEC dbo.usp_Minh_KiemTraQuyen;
 IF @trang_thai IS NULL OR @trang_thai NOT IN('DANG_MO','DONG') THROW 52100,N'Trạng thái không hợp lệ.',1;
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_Minh_KhoaLapLich;
  DECLARE @lock varchar(20);SELECT @lock=ma_hk FROM dbo.HOC_KY WITH(UPDLOCK,HOLDLOCK) WHERE ma_hk=@ma_hk;
  IF NOT EXISTS(SELECT 1 FROM dbo.vw_Phong_HocKy WHERE ma_hk=@ma_hk AND phien_ban=@phien_ban) THROW 52101,N'Phiên bản học kỳ đã thay đổi.',1;
  UPDATE dbo.HOC_KY SET trang_thai=@trang_thai WHERE ma_hk=@ma_hk;COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Phong_HocPhan_Xoa @ma_hp varchar(20),@phien_ban binary(32) AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;EXEC dbo.usp_Minh_KiemTraQuyen;
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_Minh_KhoaLapLich;
  DECLARE @lock varchar(20);SELECT @lock=ma_hp FROM dbo.HOC_PHAN WITH(UPDLOCK,HOLDLOCK) WHERE ma_hp=@ma_hp;
  IF NOT EXISTS(SELECT 1 FROM dbo.vw_Phong_HocPhan WHERE ma_hp=@ma_hp AND phien_ban=@phien_ban) THROW 52101,N'Phiên bản học phần đã thay đổi.',1;
  IF EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_hp=@ma_hp) OR EXISTS(SELECT 1 FROM dbo.KET_QUA WHERE ma_hp=@ma_hp) THROW 52105,N'Học phần đang có dữ liệu liên kết.',1;
  DELETE dbo.HOC_PHAN WHERE ma_hp=@ma_hp;COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Phong_HocKy_Xoa @ma_hk varchar(20),@phien_ban binary(32) AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;EXEC dbo.usp_Minh_KiemTraQuyen;
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_Minh_KhoaLapLich;
  DECLARE @lock varchar(20);SELECT @lock=ma_hk FROM dbo.HOC_KY WITH(UPDLOCK,HOLDLOCK) WHERE ma_hk=@ma_hk;
  IF NOT EXISTS(SELECT 1 FROM dbo.vw_Phong_HocKy WHERE ma_hk=@ma_hk AND phien_ban=@phien_ban) THROW 52101,N'Phiên bản học kỳ đã thay đổi.',1;
  IF EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_hk=@ma_hk) THROW 52105,N'Học kỳ đang có lớp.',1;
  DELETE dbo.HOC_KY WHERE ma_hk=@ma_hk;COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH
END;
GO
GRANT EXECUTE ON dbo.usp_Phong_Khoa_DanhSach TO app_minh;
GRANT EXECUTE ON dbo.usp_Phong_HocPhan_DanhSach TO app_minh;
GRANT EXECUTE ON dbo.usp_Phong_HocKy_DanhSach TO app_minh;
GRANT EXECUTE ON dbo.usp_Phong_HocPhan_Luu TO app_minh;
GRANT EXECUTE ON dbo.usp_Phong_HocKy_Luu TO app_minh;
GRANT EXECUTE ON dbo.usp_Phong_HocKy_DoiTrangThai TO app_minh;
GRANT EXECUTE ON dbo.usp_Phong_HocPhan_Xoa TO app_minh;
GRANT EXECUTE ON dbo.usp_Phong_HocKy_Xoa TO app_minh;
GO
