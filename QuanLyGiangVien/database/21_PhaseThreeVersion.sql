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
 OR OBJECT_ID('dbo.CAMPUS_OUTBOX','U') IS NULL
 OR OBJECT_ID('dbo.DK3_CART','U') IS NULL
 OR OBJECT_ID('dbo.DK3_REQUEST','U') IS NULL
 OR OBJECT_ID('dbo.DK3_EXCEPTION_REQUEST','U') IS NULL
 OR OBJECT_ID('dbo.usp_DK3_Submit','P') IS NULL
 OR OBJECT_ID('dbo.usp_DK3_FilterIssues','P') IS NULL
 OR OBJECT_ID('dbo.usp_Lifecycle_ChangeSchedule','P') IS NULL
 OR OBJECT_ID('dbo.ufn_Campus_ActualMeetings','IF') IS NULL
 OR OBJECT_ID('dbo.usp_Campus_Notifications','P') IS NULL
 OR COL_LENGTH('dbo.TAI_KHOAN','phien_ban') IS NULL
 THROW 52503,N'Chưa đủ bản cập nhật Campus giai đoạn 3.',1;
IF EXISTS(SELECT 1 FROM (VALUES ('dbo.usp_DK3_Cart'),('dbo.usp_DK3_SaveCart'),('dbo.usp_DK3_Components'),('dbo.usp_DK3_SaveCombination'),('dbo.usp_DK3_Submit'),('dbo.usp_DK3_Precheck'),('dbo.usp_DK3_Result'),('dbo.usp_DK3_FilterIssues'),('dbo.usp_DK3_ExceptionList'),('dbo.usp_DK3_ExceptionSubmit'),('dbo.usp_DK3_ExceptionDecide'),('dbo.usp_Lifecycle_Classes'),('dbo.usp_Lifecycle_Terms'),('dbo.usp_Lifecycle_Calendar'),('dbo.usp_Lifecycle_History'),('dbo.usp_Lifecycle_Transition'),('dbo.usp_Lifecycle_ChangeSchedule'),('dbo.usp_Lifecycle_Holiday'),('dbo.usp_Campus_Notifications'),('dbo.usp_Campus_Permissions'),('dbo.usp_Campus_SetPermission'),('dbo.usp_Campus_Operations')) required(name) WHERE OBJECT_ID(required.name,'P') IS NULL) THROW 52503,N'Thiếu entrypoint của quy trình đăng ký giai đoạn 3.',1;
UPDATE dbo.CAMPUS_SCHEMA_VERSION SET version=3,applied_utc=SYSUTCDATETIME() WHERE id=1;
IF @@ROWCOUNT<>1 THROW 52503,N'Thiếu dấu phiên bản Campus.',1;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Campus_KiemTraSchema
WITH EXECUTE AS OWNER
AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_SCHEMA_VERSION WHERE id=1 AND version=3)
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
  OR OBJECT_ID('dbo.CAMPUS_OUTBOX','U') IS NULL
 OR OBJECT_ID('dbo.DK3_CART','U') IS NULL
 OR OBJECT_ID('dbo.DK3_REQUEST','U') IS NULL
 OR OBJECT_ID('dbo.DK3_EXCEPTION_REQUEST','U') IS NULL
 OR OBJECT_ID('dbo.usp_DK3_Submit','P') IS NULL
 OR OBJECT_ID('dbo.usp_DK3_FilterIssues','P') IS NULL
 OR OBJECT_ID('dbo.usp_Lifecycle_ChangeSchedule','P') IS NULL
 OR OBJECT_ID('dbo.ufn_Campus_ActualMeetings','IF') IS NULL
 OR OBJECT_ID('dbo.usp_Campus_Notifications','P') IS NULL
 OR COL_LENGTH('dbo.TAI_KHOAN','phien_ban') IS NULL
  OR EXISTS(SELECT 1 FROM (VALUES ('dbo.usp_DK3_Cart'),('dbo.usp_DK3_SaveCart'),('dbo.usp_DK3_Components'),('dbo.usp_DK3_SaveCombination'),('dbo.usp_DK3_Submit'),('dbo.usp_DK3_Precheck'),('dbo.usp_DK3_Result'),('dbo.usp_DK3_FilterIssues'),('dbo.usp_DK3_ExceptionList'),('dbo.usp_DK3_ExceptionSubmit'),('dbo.usp_DK3_ExceptionDecide'),('dbo.usp_Lifecycle_Classes'),('dbo.usp_Lifecycle_Terms'),('dbo.usp_Lifecycle_Calendar'),('dbo.usp_Lifecycle_History'),('dbo.usp_Lifecycle_Transition'),('dbo.usp_Lifecycle_ChangeSchedule'),('dbo.usp_Lifecycle_Holiday'),('dbo.usp_Campus_Notifications'),('dbo.usp_Campus_Permissions'),('dbo.usp_Campus_SetPermission'),('dbo.usp_Campus_Operations')) required(name) WHERE OBJECT_ID(required.name,'P') IS NULL)
  OR OBJECT_ID('dbo.CAMPUS_PERMISSION','U') IS NULL OR OBJECT_ID('dbo.CAMPUS_NOTIFICATION','U') IS NULL OR OBJECT_ID('dbo.CAMPUS_AUDIT','U') IS NULL
  SELECT 0;
 ELSE SELECT 3;
END;
GO
GRANT EXECUTE ON dbo.usp_Campus_KiemTraSchema TO app_minh;
GO
