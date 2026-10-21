-- Applied last, only after the phase-one contracts are installed successfully.
SET XACT_ABORT ON;
GO
IF OBJECT_ID(N'dbo.usp_SinhVien_ThoiKhoaBieu',N'P') IS NULL
 OR COL_LENGTH(N'dbo.GIANG_VIEN',N'phien_ban') IS NULL
 OR COL_LENGTH(N'dbo.PHONG_HOC',N'phien_ban') IS NULL
 OR COL_LENGTH(N'dbo.LOP_HOC_PHAN',N'phien_ban') IS NULL
 THROW 52410,N'Chưa đủ migration Campus giai đoạn 1.',1;
GO
IF OBJECT_ID(N'dbo.CAMPUS_SCHEMA_VERSION',N'U') IS NULL
 CREATE TABLE dbo.CAMPUS_SCHEMA_VERSION(
  id tinyint NOT NULL CONSTRAINT PK_CampusSchemaVersion PRIMARY KEY CHECK(id=1),
  version int NOT NULL,applied_utc datetime2 NOT NULL
 );
UPDATE dbo.CAMPUS_SCHEMA_VERSION SET version=1,applied_utc=SYSUTCDATETIME() WHERE id=1;
IF @@ROWCOUNT=0 INSERT dbo.CAMPUS_SCHEMA_VERSION(id,version,applied_utc) VALUES(1,1,SYSUTCDATETIME());
GRANT SELECT ON dbo.CAMPUS_SCHEMA_VERSION TO app_minh;
GO
-- App users have EXECUTE rights only and cannot inspect private table metadata.
-- Owner context exposes a contract number without granting table reads or VIEW DEFINITION.
CREATE OR ALTER PROCEDURE dbo.usp_Campus_KiemTraSchema
WITH EXECUTE AS OWNER
AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_SCHEMA_VERSION WHERE id=1 AND version=1)
  SELECT 0;
 ELSE IF OBJECT_ID(N'dbo.ufn_Campus_AccountId',N'FN') IS NULL
  OR OBJECT_ID(N'dbo.usp_Auth_Begin',N'P') IS NULL
  OR OBJECT_ID(N'dbo.usp_Auth_Complete',N'P') IS NULL
  OR OBJECT_ID(N'dbo.usp_Auth_UseSession',N'P') IS NULL
  OR OBJECT_ID(N'dbo.usp_Auth_Logout',N'P') IS NULL
  OR OBJECT_ID(N'dbo.usp_SinhVien_DanhSach',N'P') IS NULL
  OR OBJECT_ID(N'dbo.usp_SinhVien_DanhMuc',N'P') IS NULL
  OR OBJECT_ID(N'dbo.usp_SinhVien_Luu',N'P') IS NULL
  OR OBJECT_ID(N'dbo.usp_SinhVien_TrangThai',N'P') IS NULL
  OR OBJECT_ID(N'dbo.usp_SinhVien_ThoiKhoaBieu',N'P') IS NULL
  OR COL_LENGTH(N'dbo.GIANG_VIEN',N'phien_ban') IS NULL
  OR COL_LENGTH(N'dbo.PHONG_HOC',N'phien_ban') IS NULL
  OR COL_LENGTH(N'dbo.LOP_HOC_PHAN',N'phien_ban') IS NULL
  SELECT 0;
 ELSE SELECT 1;
END;
GO
GRANT EXECUTE ON dbo.usp_Campus_KiemTraSchema TO app_minh;
GO
