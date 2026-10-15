-- Additive student management integration from the supplied QL_DANGKY_TINCHI source.
-- SQL always checks the authenticated identity; no direct table permissions are granted.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('dbo.usp_Minh_KiemTraQuyen','P') IS NULL OR OBJECT_ID('dbo.SINH_VIEN','U') IS NULL
 THROW 52300,N'Cần khởi tạo schema và migration Minh trước.',1;
GO
CREATE OR ALTER PROCEDURE dbo.usp_SinhVien_DanhSach @tu_khoa nvarchar(200)=N'',@gom_ngung bit=1 AS
BEGIN
 SET NOCOUNT ON; EXEC dbo.usp_Minh_KiemTraQuyen;
 SELECT s.*,l.ten_lop,n.ten_nganh,k.ten_khoa,n.ma_khoa
 FROM dbo.SINH_VIEN s JOIN dbo.LOP l ON l.ma_lop=s.ma_lop
 JOIN dbo.NGANH n ON n.ma_nganh=s.ma_nganh JOIN dbo.KHOA k ON k.ma_khoa=n.ma_khoa
 WHERE (@gom_ngung=1 OR s.trang_thai=1)
 AND (@tu_khoa=N'' OR CHARINDEX(@tu_khoa,s.ma_sv)>0 OR CHARINDEX(@tu_khoa,s.ho_ten)>0 OR CHARINDEX(@tu_khoa,s.email)>0)
 ORDER BY s.ho_ten,s.ma_sv;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_SinhVien_DanhMuc AS
BEGIN
 SET NOCOUNT ON; EXEC dbo.usp_Minh_KiemTraQuyen;
 SELECT k.ma_khoa,k.ten_khoa,n.ma_nganh,n.ten_nganh,l.ma_lop,l.ten_lop,
 CONVERT(bit,CASE WHEN k.trang_thai=1 AND n.trang_thai=1 AND l.trang_thai=1 THEN 1 ELSE 0 END) hoat_dong
 FROM dbo.KHOA k JOIN dbo.NGANH n ON n.ma_khoa=k.ma_khoa JOIN dbo.LOP l ON l.ma_nganh=n.ma_nganh
 ORDER BY k.ten_khoa,n.ten_nganh,l.ten_lop;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_SinhVien_Luu
 @la_moi bit,@ma_sv varchar(128),@ho_ten nvarchar(4000),@ngay_sinh date=NULL,@gioi_tinh nvarchar(128)=NULL,
 @email varchar(1000)=NULL,@sdt varchar(128)=NULL,@dia_chi nvarchar(4000)=NULL,
 @ma_nganh varchar(128)=NULL,@ma_lop varchar(128)=NULL,@trang_thai bit=1 AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON; EXEC dbo.usp_Minh_KiemTraQuyen;
 SET @ma_sv=TRIM(@ma_sv); SET @ho_ten=TRIM(@ho_ten);
 SET @email=NULLIF(TRIM(@email),''); SET @sdt=NULLIF(TRIM(@sdt),''); SET @dia_chi=NULLIF(TRIM(@dia_chi),N'');
 IF NULLIF(@ma_sv,'') IS NULL OR LEN(@ma_sv)>20 OR @ma_sv LIKE '%[^A-Za-z0-9_-]%' COLLATE Latin1_General_100_BIN2
 OR NULLIF(@ho_ten,N'') IS NULL OR DATALENGTH(@ho_ten)>200 OR @la_moi IS NULL OR @trang_thai IS NULL
 OR @ngay_sinh<'19000101' OR @ngay_sinh>CONVERT(date,DATEADD(HOUR,7,SYSUTCDATETIME()))
 OR (@gioi_tinh IS NOT NULL AND @gioi_tinh NOT IN (N'Nam',N'Nữ',N'Khác'))
 OR LEN(@email)>254 OR (@email IS NOT NULL AND (@email NOT LIKE '%_@_%._%' OR @email LIKE '% %'))
 OR LEN(@sdt)>20 OR (@sdt IS NOT NULL AND (LEN(@sdt)<6 OR @sdt LIKE '%[^0-9+ ()-]%' COLLATE Latin1_General_100_BIN2))
 OR DATALENGTH(@dia_chi)>500 OR NULLIF(@ma_nganh,'') IS NULL OR NULLIF(@ma_lop,'') IS NULL
 OR LEN(@ma_nganh)>20 OR LEN(@ma_lop)>20 THROW 52300,N'Thông tin sinh viên không hợp lệ.',1;
 BEGIN TRY
  BEGIN TRAN;
  DECLARE @old varchar(20),@oldnganh varchar(20),@oldlop varchar(20);
  SELECT @old=ma_sv,@oldnganh=ma_nganh,@oldlop=ma_lop FROM dbo.SINH_VIEN WITH(UPDLOCK,HOLDLOCK) WHERE ma_sv=@ma_sv;
  IF @la_moi=1 AND @old IS NOT NULL THROW 52301,N'Mã sinh viên đã tồn tại.',1;
  IF @la_moi=0 AND @old IS NULL THROW 52302,N'Không tìm thấy sinh viên.',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.LOP l WITH(HOLDLOCK) JOIN dbo.NGANH n WITH(HOLDLOCK) ON n.ma_nganh=l.ma_nganh
   JOIN dbo.KHOA k WITH(HOLDLOCK) ON k.ma_khoa=n.ma_khoa
   WHERE l.ma_lop=@ma_lop AND n.ma_nganh=@ma_nganh AND l.trang_thai=1 AND n.trang_thai=1 AND k.trang_thai=1)
   AND (@la_moi=1 OR @oldnganh<>@ma_nganh OR @oldlop<>@ma_lop)
   THROW 52303,N'Lớp và ngành không hợp lệ hoặc đã ngừng hoạt động.',1;
  IF @la_moi=1 INSERT dbo.SINH_VIEN(ma_sv,ho_ten,ngay_sinh,gioi_tinh,email,sdt,dia_chi,ma_nganh,ma_lop,trang_thai)
   VALUES(@ma_sv,@ho_ten,@ngay_sinh,@gioi_tinh,@email,@sdt,@dia_chi,@ma_nganh,@ma_lop,@trang_thai);
  ELSE UPDATE dbo.SINH_VIEN SET ho_ten=@ho_ten,ngay_sinh=@ngay_sinh,gioi_tinh=@gioi_tinh,email=@email,sdt=@sdt,dia_chi=@dia_chi,
   ma_nganh=@ma_nganh,ma_lop=@ma_lop,trang_thai=@trang_thai WHERE ma_sv=@ma_sv;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_SinhVien_TrangThai @ma_sv varchar(20),@trang_thai bit AS
BEGIN
 SET NOCOUNT ON; EXEC dbo.usp_Minh_KiemTraQuyen;
 IF @trang_thai IS NULL THROW 52300,N'Trạng thái sinh viên không hợp lệ.',1;
 UPDATE dbo.SINH_VIEN SET trang_thai=@trang_thai WHERE ma_sv=@ma_sv;
 IF @@ROWCOUNT=0 THROW 52302,N'Không tìm thấy sinh viên.',1;
END;
GO
GRANT EXECUTE ON dbo.usp_SinhVien_DanhSach TO app_minh;
GRANT EXECUTE ON dbo.usp_SinhVien_DanhMuc TO app_minh;
GRANT EXECUTE ON dbo.usp_SinhVien_Luu TO app_minh;
GRANT EXECUTE ON dbo.usp_SinhVien_TrangThai TO app_minh;
GO
