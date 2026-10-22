-- Additive/repeatable commands for the existing Huệ KHOA/NGANH/LOP tables; no seed or schema-version change.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('dbo.usp_Minh_KiemTraQuyen','P') IS NULL OR OBJECT_ID('dbo.usp_HocVu_Lock','P') IS NULL
 OR OBJECT_ID('dbo.LOP','U') IS NULL THROW 53400,N'Cần hoàn tất migration Campus trước.',1;
GO
CREATE OR ALTER VIEW dbo.vw_ToChuc_DanhMuc AS
 SELECT 0 kind,k.ma_khoa code,k.ten_khoa name,CAST(NULL AS varchar(20)) parent_code,
 CAST(NULL AS nvarchar(100)) parent_name,k.trang_thai active,CAST(NULL AS smallint) cohort,
 CAST(NULL AS varchar(9)) academic_years,k.mo_ta description,
 HASHBYTES('SHA2_256',CONVERT(nvarchar(max),CONCAT(LEN(k.ten_khoa),N':',k.ten_khoa,N'|',
 COALESCE(LEN(k.mo_ta),-1),N':',k.mo_ta,N'|',k.trang_thai))) version
 FROM dbo.KHOA k
 UNION ALL
 SELECT 1,n.ma_nganh,n.ten_nganh,n.ma_khoa,k.ten_khoa,n.trang_thai,NULL,NULL,n.mo_ta,
 HASHBYTES('SHA2_256',CONVERT(nvarchar(max),CONCAT(LEN(n.ten_nganh),N':',n.ten_nganh,N'|',
 LEN(n.ma_khoa),N':',n.ma_khoa,N'|',COALESCE(LEN(n.mo_ta),-1),N':',n.mo_ta,N'|',n.trang_thai)))
 FROM dbo.NGANH n JOIN dbo.KHOA k ON k.ma_khoa=n.ma_khoa
 UNION ALL
 SELECT 2,l.ma_lop,l.ten_lop,l.ma_nganh,n.ten_nganh,l.trang_thai,l.khoa_hoc,l.nien_khoa,NULL,
 HASHBYTES('SHA2_256',CONVERT(nvarchar(max),CONCAT(LEN(l.ten_lop),N':',l.ten_lop,N'|',
 LEN(l.ma_nganh),N':',l.ma_nganh,N'|',COALESCE(CONVERT(varchar(6),l.khoa_hoc),'NULL'),N'|',
 COALESCE(LEN(l.nien_khoa),-1),N':',l.nien_khoa,N'|',l.trang_thai)))
 FROM dbo.LOP l JOIN dbo.NGANH n ON n.ma_nganh=l.ma_nganh;
GO
CREATE OR ALTER PROCEDURE dbo.usp_ToChuc_DanhSach
 @loai int,@tu_khoa nvarchar(4000)=N'',@trang_thai bit=NULL,@ma_cha varchar(128)=NULL AS
BEGIN
 SET NOCOUNT ON; EXEC dbo.usp_Minh_KiemTraQuyen;
 IF @loai IS NULL OR @loai NOT IN(0,1,2) OR DATALENGTH(@tu_khoa)>400
 OR (@ma_cha IS NOT NULL AND (LEN(@ma_cha)>20 OR @ma_cha='' OR @ma_cha LIKE '%[^A-Za-z0-9_-]%' COLLATE Latin1_General_100_BIN2))
 THROW 53400,N'Bộ lọc danh mục không hợp lệ.',1;
 SELECT code,name,parent_code,parent_name,active,cohort,academic_years,description,version
 FROM dbo.vw_ToChuc_DanhMuc WHERE kind=@loai
 AND (@tu_khoa IS NULL OR @tu_khoa=N'' OR CHARINDEX(@tu_khoa,code)>0 OR CHARINDEX(@tu_khoa,name)>0)
 AND (@trang_thai IS NULL OR active=@trang_thai) AND (@ma_cha IS NULL OR parent_code=@ma_cha)
 ORDER BY name,code;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_ToChuc_Luu
 @loai int,@la_moi bit,@ma varchar(128),@ten nvarchar(4000),@ma_cha varchar(128)=NULL,
 @trang_thai bit=1,@khoa_hoc smallint=NULL,@nien_khoa varchar(128)=NULL,@mo_ta nvarchar(4000)=NULL,@phien_ban binary(32)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON; EXEC dbo.usp_Minh_KiemTraQuyen;
 EXEC dbo.usp_Campus_RequirePermission 'PROFILE_EDIT';
 IF @loai IS NULL OR @loai NOT IN(0,1,2) OR @la_moi IS NULL OR @trang_thai IS NULL
 OR NULLIF(@ma,'') IS NULL OR LEN(@ma)>20 OR @ma LIKE '%[^A-Za-z0-9_-]%' COLLATE Latin1_General_100_BIN2
 OR NULLIF(TRIM(@ten),N'') IS NULL OR DATALENGTH(TRIM(@ten))>200 OR DATALENGTH(@mo_ta)>1000
 OR (@loai=0 AND @ma_cha IS NOT NULL)
 OR (@loai IN(1,2) AND (NULLIF(@ma_cha,'') IS NULL OR LEN(@ma_cha)>20 OR @ma_cha LIKE '%[^A-Za-z0-9_-]%' COLLATE Latin1_General_100_BIN2))
 OR (@loai<>2 AND (@khoa_hoc IS NOT NULL OR @nien_khoa IS NOT NULL)) OR (@loai=2 AND @mo_ta IS NOT NULL)
 OR (@khoa_hoc IS NOT NULL AND @khoa_hoc NOT BETWEEN 1900 AND 2200)
 OR (@nien_khoa IS NOT NULL AND (DATALENGTH(@nien_khoa)<>9 OR @nien_khoa NOT LIKE '[12][0-9][0-9][0-9]-[12][0-9][0-9][0-9]' COLLATE Latin1_General_100_BIN2
     OR TRY_CONVERT(int,LEFT(@nien_khoa,4))>=TRY_CONVERT(int,RIGHT(@nien_khoa,4))))
 THROW 53400,N'Thông tin khoa, ngành hoặc lớp không hợp lệ.',1;
 BEGIN TRY
  BEGIN TRAN;
  EXEC dbo.usp_HocVu_Lock; EXEC dbo.usp_Minh_KhoaLapLich; EXEC dbo.usp_Minh_KiemTraQuyen;
  EXEC dbo.usp_Campus_RequirePermission 'PROFILE_EDIT';
  DECLARE @old varchar(20),@parent varchar(20),@active bit;
  IF @loai=0 SELECT @old=ma_khoa,@active=trang_thai FROM dbo.KHOA WITH(UPDLOCK,HOLDLOCK) WHERE ma_khoa=@ma;
  IF @loai=1 SELECT @old=ma_nganh,@parent=ma_khoa,@active=trang_thai FROM dbo.NGANH WITH(UPDLOCK,HOLDLOCK) WHERE ma_nganh=@ma;
  IF @loai=2 SELECT @old=ma_lop,@parent=ma_nganh,@active=trang_thai FROM dbo.LOP WITH(UPDLOCK,HOLDLOCK) WHERE ma_lop=@ma;
  IF @la_moi=1 AND @old IS NOT NULL THROW 53401,N'Mã danh mục đã tồn tại.',1;
  IF @la_moi=0 AND NOT EXISTS(SELECT 1 FROM dbo.vw_ToChuc_DanhMuc WHERE kind=@loai AND code=@ma AND version=@phien_ban)
   THROW 53402,N'Danh mục đã thay đổi hoặc không còn tồn tại.',1;
  -- Existing records can still be renamed or stopped after a parent stops. New/reparented/reactivated records require an active chain.
  IF @loai IN(1,2) AND (@la_moi=1 OR @parent<>@ma_cha OR (@active=0 AND @trang_thai=1))
  BEGIN
   IF @loai=1 AND NOT EXISTS(SELECT 1 FROM dbo.KHOA WITH(HOLDLOCK) WHERE ma_khoa=@ma_cha AND trang_thai=1)
    THROW 53403,N'Khoa cấp trên không hoạt động.',1;
   IF @loai=2 AND NOT EXISTS(SELECT 1 FROM dbo.NGANH n WITH(HOLDLOCK) JOIN dbo.KHOA k WITH(HOLDLOCK) ON k.ma_khoa=n.ma_khoa
    WHERE n.ma_nganh=@ma_cha AND n.trang_thai=1 AND k.trang_thai=1) THROW 53403,N'Ngành hoặc khoa cấp trên không hoạt động.',1;
  END;
  IF @la_moi=0 AND @parent<>@ma_cha AND
   ((@loai=2 AND EXISTS(SELECT 1 FROM dbo.SINH_VIEN WITH(HOLDLOCK) WHERE ma_lop=@ma))
    OR (@loai=1 AND (EXISTS(SELECT 1 FROM dbo.SINH_VIEN WITH(HOLDLOCK) WHERE ma_nganh=@ma)
       OR EXISTS(SELECT 1 FROM dbo.HV_DOCUMENT WITH(HOLDLOCK) WHERE kind='PROGRAM' AND JSON_VALUE(payload,'$.major')=@ma))))
   THROW 53404,N'Không thể chuyển danh mục đã có sinh viên hoặc chương trình đào tạo.',1;
  IF @loai=0
  BEGIN
   IF @la_moi=1 INSERT dbo.KHOA(ma_khoa,ten_khoa,mo_ta,trang_thai) VALUES(@ma,TRIM(@ten),@mo_ta,@trang_thai);
   ELSE UPDATE dbo.KHOA SET ten_khoa=TRIM(@ten),mo_ta=@mo_ta,trang_thai=@trang_thai WHERE ma_khoa=@ma;
  END;
  IF @loai=1
  BEGIN
   IF @la_moi=1 INSERT dbo.NGANH(ma_nganh,ten_nganh,ma_khoa,mo_ta,trang_thai) VALUES(@ma,TRIM(@ten),@ma_cha,@mo_ta,@trang_thai);
   ELSE UPDATE dbo.NGANH SET ten_nganh=TRIM(@ten),ma_khoa=@ma_cha,mo_ta=@mo_ta,trang_thai=@trang_thai WHERE ma_nganh=@ma;
  END;
  IF @loai=2
  BEGIN
   IF @la_moi=1 INSERT dbo.LOP(ma_lop,ten_lop,ma_nganh,khoa_hoc,nien_khoa,trang_thai) VALUES(@ma,TRIM(@ten),@ma_cha,@khoa_hoc,@nien_khoa,@trang_thai);
   ELSE UPDATE dbo.LOP SET ten_lop=TRIM(@ten),ma_nganh=@ma_cha,khoa_hoc=@khoa_hoc,nien_khoa=@nien_khoa,trang_thai=@trang_thai WHERE ma_lop=@ma;
  END;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_ToChuc_TrangThai @loai int,@ma varchar(128),@trang_thai bit,@phien_ban binary(32) AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON; EXEC dbo.usp_Minh_KiemTraQuyen;
 EXEC dbo.usp_Campus_RequirePermission 'PROFILE_EDIT';
 BEGIN TRY
  BEGIN TRAN; EXEC dbo.usp_HocVu_Lock; EXEC dbo.usp_Minh_KhoaLapLich;
  EXEC dbo.usp_Campus_RequirePermission 'PROFILE_EDIT';
  DECLARE @ten nvarchar(100),@cha varchar(20),@khoa smallint,@nien varchar(9),@mota nvarchar(500);
  SELECT @ten=name,@cha=parent_code,@khoa=cohort,@nien=academic_years,@mota=description
  FROM dbo.vw_ToChuc_DanhMuc WHERE kind=@loai AND code=@ma AND version=@phien_ban;
  IF @ten IS NULL THROW 53402,N'Danh mục đã thay đổi hoặc không còn tồn tại.',1;
  EXEC dbo.usp_ToChuc_Luu @loai=@loai,@la_moi=0,@ma=@ma,@ten=@ten,@ma_cha=@cha,@trang_thai=@trang_thai,
   @khoa_hoc=@khoa,@nien_khoa=@nien,@mo_ta=@mota,@phien_ban=@phien_ban;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
-- Keep empty branches visible in student editing; only complete active chains can be assigned to a student.
CREATE OR ALTER PROCEDURE dbo.usp_SinhVien_DanhMuc AS
BEGIN
 SET NOCOUNT ON; EXEC dbo.usp_Minh_KiemTraQuyen;
 SELECT k.ma_khoa,k.ten_khoa,n.ma_nganh,n.ten_nganh,l.ma_lop,l.ten_lop,
 CONVERT(bit,CASE WHEN k.trang_thai=1 AND n.trang_thai=1 AND l.trang_thai=1 THEN 1 ELSE 0 END) hoat_dong,
 k.trang_thai khoa_hoat_dong,n.trang_thai nganh_hoat_dong,l.trang_thai lop_hoat_dong
 FROM dbo.KHOA k LEFT JOIN dbo.NGANH n ON n.ma_khoa=k.ma_khoa LEFT JOIN dbo.LOP l ON l.ma_nganh=n.ma_nganh
 ORDER BY k.ten_khoa,n.ten_nganh,l.ten_lop;
END;
GO
GRANT EXECUTE ON dbo.usp_ToChuc_DanhSach TO app_minh;
GRANT EXECUTE ON dbo.usp_ToChuc_Luu TO app_minh;
GRANT EXECUTE ON dbo.usp_ToChuc_TrangThai TO app_minh;
GO
