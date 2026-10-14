-- Additive, repeatable migration. Run in the explicitly selected database.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('dbo.GIANG_VIEN','U') IS NULL OR OBJECT_ID('dbo.usp_XacThucTaiKhoan','P') IS NULL
 THROW 52000,N'Cần khởi tạo schema Huệ trước migration Minh.',1;
BEGIN TRAN;
IF COL_LENGTH('dbo.GIANG_VIEN','hoc_vi') IS NULL ALTER TABLE dbo.GIANG_VIEN ADD hoc_vi nvarchar(80) NULL;
IF COL_LENGTH('dbo.GIANG_VIEN','avatar_key') IS NULL ALTER TABLE dbo.GIANG_VIEN ADD avatar_key varchar(40) NULL;
IF COL_LENGTH('dbo.HOC_PHAN','ma_khoa') IS NULL
 ALTER TABLE dbo.HOC_PHAN ADD ma_khoa varchar(20) NULL CONSTRAINT FK_Minh_HOC_PHAN_KHOA REFERENCES dbo.KHOA(ma_khoa);
IF COL_LENGTH('dbo.HOC_KY','ngay_hoc_bd') IS NULL ALTER TABLE dbo.HOC_KY ADD ngay_hoc_bd date NULL;
IF COL_LENGTH('dbo.HOC_KY','ngay_hoc_kt') IS NULL ALTER TABLE dbo.HOC_KY ADD ngay_hoc_kt date NULL;
COMMIT;
GO
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_Minh_HK_NgayHoc')
 ALTER TABLE dbo.HOC_KY ADD CONSTRAINT CK_Minh_HK_NgayHoc CHECK(
  (ngay_hoc_bd IS NULL AND ngay_hoc_kt IS NULL) OR
  (ngay_hoc_bd IS NOT NULL AND ngay_hoc_kt IS NOT NULL AND ngay_hoc_bd>='20000101' AND ngay_hoc_bd<=ngay_hoc_kt));
GO
-- This role grants only explicitly listed EXECUTE permissions; no direct table access.
IF DATABASE_PRINCIPAL_ID(N'app_minh') IS NULL CREATE ROLE app_minh;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Minh_KiemTraQuyen
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20);
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role NOT IN ('DAO_TAO','QUAN_TRI') THROW 52001,N'Chỉ phòng đào tạo hoặc quản trị được thay đổi dữ liệu.',1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Minh_KhoaLapLich
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @rc int;
 EXEC @rc=sys.sp_getapplock @Resource=N'Minh:Scheduling',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @rc<0 THROW 52002,N'Dữ liệu đang được cập nhật. Vui lòng thử lại.',1;
END;
GO
-- Uses a Monday anchor rather than DATEFIRST or a language-dependent weekday name.
CREATE OR ALTER FUNCTION dbo.ufn_Minh_CoNgayHoc(@thu int,@bd date,@kt date)
RETURNS bit WITH SCHEMABINDING
AS
BEGIN
 RETURN CASE WHEN @bd<=@kt AND DATEADD(DAY,(@thu-2-DATEDIFF(DAY,CONVERT(date,'19000101',112),@bd)%7+7)%7,@bd)<=@kt THEN 1 ELSE 0 END;
END;
GO
