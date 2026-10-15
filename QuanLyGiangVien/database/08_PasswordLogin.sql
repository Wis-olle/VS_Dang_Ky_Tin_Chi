-- Additive application login. No server authentication-mode changes or SQL logins.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('dbo.APP_CREDENTIAL') IS NULL
 CREATE TABLE dbo.APP_CREDENTIAL(
  ma_tk int NOT NULL PRIMARY KEY REFERENCES dbo.TAI_KHOAN(ma_tk),
  salt binary(16) NOT NULL, verifier binary(32) NOT NULL,
  iterations int NOT NULL CHECK(iterations BETWEEN 100000 AND 2000000),
  failed_count int NOT NULL DEFAULT 0, locked_until datetime2 NULL
 );
IF OBJECT_ID('dbo.APP_LOGIN_CHALLENGE') IS NULL
 CREATE TABLE dbo.APP_LOGIN_CHALLENGE(
  id uniqueidentifier NOT NULL PRIMARY KEY, ma_tk int NULL,
  nonce binary(32) NOT NULL, owner_name sysname NOT NULL, expires datetime2 NOT NULL
 );
IF OBJECT_ID('dbo.APP_SESSION') IS NULL
 CREATE TABLE dbo.APP_SESSION(
  token_hash binary(32) NOT NULL PRIMARY KEY, ma_tk int NOT NULL REFERENCES dbo.TAI_KHOAN(ma_tk),
  owner_name sysname NOT NULL, expires datetime2 NOT NULL
 );
GO
CREATE OR ALTER FUNCTION dbo.ufn_Campus_AccountId()
RETURNS int
AS
BEGIN
 DECLARE @id int, @token varbinary(32)=TRY_CONVERT(varbinary(32),SESSION_CONTEXT(N'campus_token'));
 IF SESSION_CONTEXT(N'campus_token') IS NOT NULL
  SELECT @id=ma_tk FROM dbo.APP_SESSION WHERE token_hash=HASHBYTES('SHA2_256',@token)
   AND owner_name=ORIGINAL_LOGIN() AND expires>SYSUTCDATETIME();
 ELSE
  SELECT @id=ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap=ORIGINAL_LOGIN();
 RETURN @id;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Auth_Begin @username nvarchar(128)
AS
BEGIN
 SET NOCOUNT ON;
 DELETE dbo.APP_LOGIN_CHALLENGE WHERE expires<SYSUTCDATETIME();
 DELETE dbo.APP_SESSION WHERE expires<SYSUTCDATETIME();
 DECLARE @id int,@salt binary(16),@iterations int=600000,@challenge uniqueidentifier=NEWID(),@nonce binary(32)=CRYPT_GEN_RANDOM(32);
 SELECT @id=t.ma_tk,@salt=c.salt,@iterations=c.iterations FROM dbo.TAI_KHOAN t
 JOIN dbo.APP_CREDENTIAL c ON c.ma_tk=t.ma_tk WHERE t.ten_dang_nhap=@username;
 INSERT dbo.APP_LOGIN_CHALLENGE(id,ma_tk,nonce,owner_name,expires)
 VALUES(@challenge,@id,@nonce,ORIGINAL_LOGIN(),DATEADD(MINUTE,2,SYSUTCDATETIME()));
 SELECT @challenge challenge,COALESCE(@salt,CRYPT_GEN_RANDOM(16)) salt,@nonce nonce,@iterations iterations;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Auth_Complete @challenge uniqueidentifier,@proof varbinary(32),@portal varchar(10)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @id int,@nonce binary(32),@verifier binary(32),@role varchar(20),@active bit,@sv varchar(20),@until datetime2;
 BEGIN TRAN;
 SELECT @id=ma_tk,@nonce=nonce FROM dbo.APP_LOGIN_CHALLENGE WITH(UPDLOCK,HOLDLOCK)
  WHERE id=@challenge AND owner_name=ORIGINAL_LOGIN() AND expires>SYSUTCDATETIME();
 DELETE dbo.APP_LOGIN_CHALLENGE WHERE id=@challenge AND owner_name=ORIGINAL_LOGIN();
 SELECT @verifier=c.verifier,@until=c.locked_until,@role=t.vai_tro,@active=t.trang_thai,@sv=t.ma_sv
  FROM dbo.APP_CREDENTIAL c WITH(UPDLOCK,HOLDLOCK) JOIN dbo.TAI_KHOAN t ON t.ma_tk=c.ma_tk WHERE c.ma_tk=@id;
 IF @until>SYSUTCDATETIME() BEGIN COMMIT; THROW 52104,N'Tạm khóa đăng nhập.',1; END;
 IF @id IS NULL OR @verifier IS NULL OR @proof IS NULL OR @nonce IS NULL
   OR HASHBYTES('SHA2_256',@nonce+@verifier)<>@proof
 BEGIN
  UPDATE dbo.APP_CREDENTIAL SET failed_count=CASE WHEN locked_until<=SYSUTCDATETIME() THEN 1 ELSE failed_count+1 END,
   locked_until=CASE WHEN failed_count>=4 AND (locked_until IS NULL OR locked_until>SYSUTCDATETIME()) THEN DATEADD(MINUTE,10,SYSUTCDATETIME()) ELSE NULL END
   WHERE ma_tk=@id;
  COMMIT; THROW 52101,N'Đăng nhập thất bại.',1;
 END;
 IF @active<>1 OR (@role='SINH_VIEN' AND NOT EXISTS(SELECT 1 FROM dbo.SINH_VIEN WHERE ma_sv=@sv AND trang_thai=1))
 BEGIN COMMIT; THROW 52102,N'Tài khoản hoặc hồ sơ không hoạt động.',1; END;
 IF @portal IS NULL OR NOT ((@portal='STUDENT' AND @role='SINH_VIEN') OR (@portal='TRAINING' AND @role IN ('DAO_TAO','QUAN_TRI')))
 BEGIN COMMIT; THROW 52103,N'Sai cổng đăng nhập.',1; END;
 UPDATE dbo.APP_CREDENTIAL SET failed_count=0,locked_until=NULL WHERE ma_tk=@id;
 UPDATE dbo.TAI_KHOAN SET lan_dang_nhap_cuoi=DATEADD(HOUR,7,SYSUTCDATETIME()) WHERE ma_tk=@id;
 DECLARE @token varbinary(32)=CRYPT_GEN_RANDOM(32);
 INSERT dbo.APP_SESSION(token_hash,ma_tk,owner_name,expires)
 VALUES(HASHBYTES('SHA2_256',@token),@id,ORIGINAL_LOGIN(),DATEADD(HOUR,8,SYSUTCDATETIME()));
 COMMIT;
 SELECT @token token;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Auth_UseSession @token varbinary(32)
AS
BEGIN
 SET NOCOUNT ON;
 IF DATALENGTH(@token)<>32 OR @token IS NULL OR NOT EXISTS(
  SELECT 1 FROM dbo.APP_SESSION s JOIN dbo.TAI_KHOAN t ON t.ma_tk=s.ma_tk
  WHERE s.token_hash=HASHBYTES('SHA2_256',@token) AND s.owner_name=ORIGINAL_LOGIN()
   AND s.expires>SYSUTCDATETIME() AND t.trang_thai=1)
  THROW 52105,N'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.',1;
 EXEC sys.sp_set_session_context @key=N'campus_token',@value=@token,@read_only=1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Auth_Logout @token varbinary(32)
AS
BEGIN
 SET NOCOUNT ON;
 DELETE dbo.APP_SESSION WHERE token_hash=HASHBYTES('SHA2_256',@token) AND owner_name=ORIGINAL_LOGIN();
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_XacThucTaiKhoan @ma_sv varchar(20) OUTPUT,@vai_tro varchar(20) OUTPUT
AS
BEGIN
 SET NOCOUNT ON;
 SET @ma_sv=NULL; SET @vai_tro=NULL;
 SELECT @ma_sv=ma_sv,@vai_tro=vai_tro FROM dbo.TAI_KHOAN
 WHERE ma_tk=dbo.ufn_Campus_AccountId() AND trang_thai=1;
 IF @vai_tro IS NULL THROW 51001,N'Phiên đăng nhập không hợp lệ hoặc tài khoản đã bị khóa.',1;
 IF @vai_tro NOT IN ('SINH_VIEN','DAO_TAO','QUAN_TRI') THROW 51002,N'Vai trò không có quyền sử dụng phân hệ.',1;
 IF @vai_tro='SINH_VIEN'
 BEGIN
  SELECT @ma_sv=ma_sv FROM dbo.SINH_VIEN WHERE ma_sv=@ma_sv AND trang_thai=1;
  IF @@ROWCOUNT=0 THROW 51003,N'Hồ sơ sinh viên không hoạt động.',1;
 END;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_LayNguoiDung
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20);
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 SELECT t.ma_tk,t.ma_sv,t.vai_tro,COALESCE(s.ho_ten,N'Phòng đào tạo') AS ho_ten
 FROM dbo.TAI_KHOAN t LEFT JOIN dbo.SINH_VIEN s ON s.ma_sv=t.ma_sv
 WHERE t.ma_tk=dbo.ufn_Campus_AccountId();
END;
GO
-- Patch the original snapshot procedure without changing its resultset contract.
DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_Minh_DocDuLieu'));
IF @definition IS NULL THROW 52106,N'Cần khởi tạo procedure đọc dữ liệu trước.',1;
SET @definition=REPLACE(@definition,N'CREATE OR ALTER PROCEDURE',N'ALTER PROCEDURE');
IF LEFT(LTRIM(@definition),6)='CREATE' SET @definition=STUFF(@definition,CHARINDEX('CREATE',@definition),6,'ALTER');
SET @definition=REPLACE(@definition,N'WHERE ten_dang_nhap=ORIGINAL_LOGIN()',N'WHERE ma_tk=dbo.ufn_Campus_AccountId()');
EXEC sys.sp_executesql @definition;
GO
GRANT EXECUTE ON dbo.usp_Auth_Begin TO app_minh;
GRANT EXECUTE ON dbo.usp_Auth_Complete TO app_minh;
GRANT EXECUTE ON dbo.usp_Auth_UseSession TO app_minh;
GRANT EXECUTE ON dbo.usp_Auth_Logout TO app_minh;
GO
