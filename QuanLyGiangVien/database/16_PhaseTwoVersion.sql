-- Publish this contract only after all academic/account entrypoints exist.
SET XACT_ABORT ON;
GO
IF OBJECT_ID('dbo.usp_HocVu_Snapshot','P') IS NULL
 OR OBJECT_ID('dbo.HV_DOCUMENT','U') IS NULL
 OR OBJECT_ID('dbo.HV_HISTORY','U') IS NULL
 OR OBJECT_ID('dbo.usp_HocVu_Save','P') IS NULL
 OR OBJECT_ID('dbo.usp_HocVu_Publish','P') IS NULL
 OR OBJECT_ID('dbo.usp_HocVu_Precheck','P') IS NULL
 OR OBJECT_ID('dbo.usp_HocVu_DangKyNhom','P') IS NULL
 OR OBJECT_ID('dbo.usp_TaiKhoan_DanhSach','P') IS NULL
 OR OBJECT_ID('dbo.usp_TaiKhoan_Luu','P') IS NULL
 OR OBJECT_ID('dbo.usp_TaiKhoan_Reset','P') IS NULL
 OR OBJECT_ID('dbo.usp_TaiKhoan_DoiMatKhau','P') IS NULL
 OR COL_LENGTH('dbo.TAI_KHOAN','phien_ban') IS NULL
 THROW 52503,N'Chưa đủ bản cập nhật Campus giai đoạn 2.',1;
UPDATE dbo.CAMPUS_SCHEMA_VERSION SET version=2,applied_utc=SYSUTCDATETIME() WHERE id=1;
IF @@ROWCOUNT<>1 THROW 52503,N'Thiếu dấu phiên bản Campus.',1;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Campus_KiemTraSchema
WITH EXECUTE AS OWNER
AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_SCHEMA_VERSION WHERE id=1 AND version=2)
  OR OBJECT_ID('dbo.ufn_Campus_AccountId','FN') IS NULL
  OR OBJECT_ID('dbo.usp_Auth_Begin','P') IS NULL
  OR OBJECT_ID('dbo.usp_Auth_Complete','P') IS NULL
  OR OBJECT_ID('dbo.usp_Auth_UseSession','P') IS NULL
  OR OBJECT_ID('dbo.usp_Auth_Logout','P') IS NULL
  OR OBJECT_ID('dbo.usp_SinhVien_DanhSach','P') IS NULL
  OR OBJECT_ID('dbo.usp_SinhVien_DanhMuc','P') IS NULL
  OR OBJECT_ID('dbo.usp_SinhVien_Luu','P') IS NULL
  OR OBJECT_ID('dbo.usp_SinhVien_TrangThai','P') IS NULL
  OR OBJECT_ID('dbo.usp_SinhVien_ThoiKhoaBieu','P') IS NULL
  OR OBJECT_ID('dbo.usp_HocVu_Snapshot','P') IS NULL
  OR OBJECT_ID('dbo.HV_DOCUMENT','U') IS NULL
  OR OBJECT_ID('dbo.HV_HISTORY','U') IS NULL
  OR OBJECT_ID('dbo.usp_HocVu_Save','P') IS NULL
  OR OBJECT_ID('dbo.usp_HocVu_Publish','P') IS NULL
  OR OBJECT_ID('dbo.usp_HocVu_Precheck','P') IS NULL
  OR OBJECT_ID('dbo.usp_HocVu_DangKyNhom','P') IS NULL
  OR OBJECT_ID('dbo.usp_TaiKhoan_DanhSach','P') IS NULL
  OR OBJECT_ID('dbo.usp_TaiKhoan_Luu','P') IS NULL
  OR OBJECT_ID('dbo.usp_TaiKhoan_Reset','P') IS NULL
  OR OBJECT_ID('dbo.usp_TaiKhoan_DoiMatKhau','P') IS NULL
  OR COL_LENGTH('dbo.GIANG_VIEN','phien_ban') IS NULL
  OR COL_LENGTH('dbo.PHONG_HOC','phien_ban') IS NULL
  OR COL_LENGTH('dbo.LOP_HOC_PHAN','phien_ban') IS NULL
  OR COL_LENGTH('dbo.TAI_KHOAN','phien_ban') IS NULL
  SELECT 0;
 ELSE SELECT 2;
END;
GO
GRANT EXECUTE ON dbo.usp_Campus_KiemTraSchema TO app_minh;
GO
