SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF COL_LENGTH('dbo.GIANG_VIEN','phien_ban') IS NULL ALTER TABLE dbo.GIANG_VIEN ADD phien_ban rowversion;
IF COL_LENGTH('dbo.PHONG_HOC','phien_ban') IS NULL ALTER TABLE dbo.PHONG_HOC ADD phien_ban rowversion;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Minh_LuuGiangVien
 @ma_gv varchar(20),@ho_ten nvarchar(100),@ngay_sinh date,@gioi_tinh nvarchar(10),
 @email varchar(254),@sdt varchar(20),@ten_khoa nvarchar(100),@hoc_vi nvarchar(80),
 @avatar_key varchar(40),@trang_thai bit,@la_moi bit,@phien_ban binary(8)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 EXEC dbo.usp_Minh_KiemTraQuyen;
 IF NULLIF(TRIM(@ma_gv),'') IS NULL OR @ma_gv LIKE '%[^A-Za-z0-9_-]%' COLLATE Latin1_General_100_BIN2
  OR NULLIF(TRIM(@ho_ten),N'') IS NULL OR @ngay_sinh IS NULL OR @ngay_sinh<'19000101' OR @ngay_sinh>CONVERT(date,GETDATE())
  OR @gioi_tinh NOT IN (N'Nam',N'Nữ',N'Khác') OR @gioi_tinh IS NULL
  OR NULLIF(TRIM(@email),'') IS NULL OR @email NOT LIKE '%_@_%._%' OR @email LIKE '% %'
  OR LEN(@sdt) NOT BETWEEN 10 AND 11 OR @sdt LIKE '%[^0-9]%' COLLATE Latin1_General_100_BIN2 OR @sdt IS NULL
  OR @hoc_vi NOT IN (N'Thạc sĩ',N'Tiến sĩ',N'PGS. Tiến sĩ',N'Giáo sư') OR @hoc_vi IS NULL
  OR @avatar_key NOT LIKE 'portrait-[0-9][0-9]' OR TRY_CONVERT(int,RIGHT(@avatar_key,2)) NOT BETWEEN 1 AND 48 OR @avatar_key IS NULL
  OR @trang_thai IS NULL OR @la_moi IS NULL
  THROW 52010,N'Thông tin giảng viên không hợp lệ.',1;
 BEGIN TRY
  BEGIN TRAN;
  EXEC dbo.usp_Minh_KhoaLapLich;
  DECLARE @khoa varchar(20),@lock varchar(20);
  SELECT @khoa=ma_khoa FROM dbo.KHOA WITH(HOLDLOCK) WHERE ten_khoa=@ten_khoa AND trang_thai=1;
  IF @khoa IS NULL OR (SELECT COUNT(*) FROM dbo.KHOA WHERE ten_khoa=@ten_khoa AND trang_thai=1)<>1
   THROW 52011,N'Khoa không hoạt động hoặc tên khoa không duy nhất.',1;
  -- Match Huệ lock order: semester, then class. The shared scheduling lock serializes Minh writes.
  SELECT @lock=h.ma_hk FROM dbo.HOC_KY h WITH(HOLDLOCK)
   WHERE EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN l WHERE l.ma_hk=h.ma_hk AND l.ma_gv=@ma_gv) ORDER BY h.ma_hk;
  SELECT @lock=ma_lhp FROM dbo.LOP_HOC_PHAN WITH(UPDLOCK,HOLDLOCK) WHERE ma_gv=@ma_gv ORDER BY ma_lhp;
  IF @la_moi=1 AND EXISTS(SELECT 1 FROM dbo.GIANG_VIEN WHERE ma_gv=@ma_gv) THROW 52012,N'Mã giảng viên đã tồn tại.',1;
  IF @la_moi=0 AND NOT EXISTS(SELECT 1 FROM dbo.GIANG_VIEN WHERE ma_gv=@ma_gv) THROW 52013,N'Giảng viên không còn tồn tại. Hãy tải lại.',1;
  IF @la_moi=0 AND NOT EXISTS(SELECT 1 FROM dbo.GIANG_VIEN WITH(UPDLOCK,HOLDLOCK) WHERE ma_gv=@ma_gv AND phien_ban=@phien_ban)
   THROW 52046,N'Dữ liệu đã thay đổi. Vui lòng tải lại trước khi sửa hoặc xóa.',1;
  IF @trang_thai=0 AND EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_gv=@ma_gv AND trang_thai<>'HUY')
   THROW 52014,N'Giảng viên còn được phân công lớp; chưa thể tạm nghỉ.',1;
  IF EXISTS(SELECT 1 FROM dbo.GIANG_VIEN WITH(HOLDLOCK) WHERE ma_gv<>@ma_gv AND (email=@email OR sdt=@sdt))
   THROW 52015,N'Email hoặc số điện thoại đã được sử dụng.',1;
  IF @la_moi=1
   INSERT dbo.GIANG_VIEN(ma_gv,ho_ten,ngay_sinh,gioi_tinh,email,sdt,ma_khoa,hoc_vi,avatar_key,trang_thai)
   VALUES(@ma_gv,TRIM(@ho_ten),@ngay_sinh,@gioi_tinh,@email,@sdt,@khoa,@hoc_vi,@avatar_key,@trang_thai);
  ELSE UPDATE dbo.GIANG_VIEN SET ho_ten=TRIM(@ho_ten),ngay_sinh=@ngay_sinh,gioi_tinh=@gioi_tinh,
   email=@email,sdt=@sdt,ma_khoa=@khoa,hoc_vi=@hoc_vi,avatar_key=@avatar_key,trang_thai=@trang_thai WHERE ma_gv=@ma_gv;
  SELECT phien_ban FROM dbo.GIANG_VIEN WHERE ma_gv=@ma_gv;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Minh_XoaGiangVien @ma_gv varchar(20),@phien_ban binary(8)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 EXEC dbo.usp_Minh_KiemTraQuyen;
 BEGIN TRY
  BEGIN TRAN; EXEC dbo.usp_Minh_KhoaLapLich;
  IF NOT EXISTS(SELECT 1 FROM dbo.GIANG_VIEN WITH(UPDLOCK,HOLDLOCK) WHERE ma_gv=@ma_gv AND phien_ban=@phien_ban)
   THROW 52046,N'Dữ liệu đã thay đổi. Vui lòng tải lại trước khi sửa hoặc xóa.',1;
  IF EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_gv=@ma_gv) OR EXISTS(SELECT 1 FROM dbo.TAI_KHOAN WHERE ma_gv=@ma_gv)
   THROW 52016,N'Giảng viên đang có dữ liệu liên kết; không thể xóa.',1;
  DELETE dbo.GIANG_VIEN WHERE ma_gv=@ma_gv;
  IF @@ROWCOUNT<>1 THROW 52013,N'Giảng viên không còn tồn tại. Hãy tải lại.',1;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Minh_LuuPhong
 @ma_phong varchar(20),@toa_nha nvarchar(80),@suc_chua int,@loai_phong nvarchar(50),@trang_thai bit,@la_moi bit,@phien_ban binary(8)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 EXEC dbo.usp_Minh_KiemTraQuyen;
 IF NULLIF(TRIM(@ma_phong),'') IS NULL OR @ma_phong LIKE '%[^A-Za-z0-9_-]%' COLLATE Latin1_General_100_BIN2
  OR NULLIF(TRIM(@toa_nha),N'') IS NULL OR @suc_chua NOT BETWEEN 1 AND 1000 OR @suc_chua IS NULL
  OR @loai_phong NOT IN (N'Lý thuyết',N'Thực hành',N'Hội trường') OR @loai_phong IS NULL
  OR @trang_thai IS NULL OR @la_moi IS NULL THROW 52020,N'Thông tin phòng không hợp lệ.',1;
 BEGIN TRY
  BEGIN TRAN; EXEC dbo.usp_Minh_KhoaLapLich;
  DECLARE @lock varchar(20);
  SELECT @lock=h.ma_hk FROM dbo.HOC_KY h WITH(HOLDLOCK) WHERE EXISTS(
   SELECT 1 FROM dbo.LOP_HOC_PHAN l JOIN dbo.LICH_HOC c ON c.ma_lhp=l.ma_lhp WHERE l.ma_hk=h.ma_hk AND c.ma_phong=@ma_phong) ORDER BY h.ma_hk;
  SELECT @lock=l.ma_lhp FROM dbo.LOP_HOC_PHAN l WITH(UPDLOCK,HOLDLOCK) WHERE l.ma_phong=@ma_phong OR EXISTS(
   SELECT 1 FROM dbo.LICH_HOC c WHERE c.ma_lhp=l.ma_lhp AND c.ma_phong=@ma_phong) ORDER BY l.ma_lhp;
  IF @la_moi=1 AND EXISTS(SELECT 1 FROM dbo.PHONG_HOC WHERE ma_phong=@ma_phong) THROW 52021,N'Mã phòng đã tồn tại.',1;
  IF @la_moi=0 AND NOT EXISTS(SELECT 1 FROM dbo.PHONG_HOC WHERE ma_phong=@ma_phong) THROW 52022,N'Phòng không còn tồn tại. Hãy tải lại.',1;
  IF @la_moi=0 AND NOT EXISTS(SELECT 1 FROM dbo.PHONG_HOC WITH(UPDLOCK,HOLDLOCK) WHERE ma_phong=@ma_phong AND phien_ban=@phien_ban)
   THROW 52046,N'Dữ liệu đã thay đổi. Vui lòng tải lại trước khi sửa hoặc xóa.',1;
  IF EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN l WHERE l.trang_thai<>'HUY' AND
   (l.ma_phong=@ma_phong OR EXISTS(SELECT 1 FROM dbo.LICH_HOC c WHERE c.ma_lhp=l.ma_lhp AND c.ma_phong=@ma_phong))
   AND (@trang_thai=0 OR l.si_so_max>@suc_chua))
   THROW 52023,N'Phòng còn lớp sử dụng hoặc sức chứa thấp hơn sĩ số tối đa lớp.',1;
  IF @la_moi=1 INSERT dbo.PHONG_HOC(ma_phong,ten_phong,toa_nha,suc_chua,loai_phong,trang_thai)
   VALUES(@ma_phong,@ma_phong,TRIM(@toa_nha),@suc_chua,@loai_phong,@trang_thai);
  ELSE UPDATE dbo.PHONG_HOC SET toa_nha=TRIM(@toa_nha),suc_chua=@suc_chua,loai_phong=@loai_phong,trang_thai=@trang_thai WHERE ma_phong=@ma_phong;
  SELECT phien_ban FROM dbo.PHONG_HOC WHERE ma_phong=@ma_phong;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Minh_XoaPhong @ma_phong varchar(20),@phien_ban binary(8)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 EXEC dbo.usp_Minh_KiemTraQuyen;
 BEGIN TRY
  BEGIN TRAN; EXEC dbo.usp_Minh_KhoaLapLich;
  IF NOT EXISTS(SELECT 1 FROM dbo.PHONG_HOC WITH(UPDLOCK,HOLDLOCK) WHERE ma_phong=@ma_phong AND phien_ban=@phien_ban)
   THROW 52046,N'Dữ liệu đã thay đổi. Vui lòng tải lại trước khi sửa hoặc xóa.',1;
  IF EXISTS(SELECT 1 FROM dbo.LICH_HOC WHERE ma_phong=@ma_phong) OR EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_phong=@ma_phong)
   THROW 52024,N'Phòng có lịch hoặc lớp liên kết; không thể xóa.',1;
  DELETE dbo.PHONG_HOC WHERE ma_phong=@ma_phong;
  IF @@ROWCOUNT<>1 THROW 52022,N'Phòng không còn tồn tại. Hãy tải lại.',1;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
END;
GO
GRANT EXECUTE ON dbo.usp_Minh_LuuGiangVien TO app_minh;
GRANT EXECUTE ON dbo.usp_Minh_XoaGiangVien TO app_minh;
GRANT EXECUTE ON dbo.usp_Minh_LuuPhong TO app_minh;
GRANT EXECUTE ON dbo.usp_Minh_XoaPhong TO app_minh;
GO
