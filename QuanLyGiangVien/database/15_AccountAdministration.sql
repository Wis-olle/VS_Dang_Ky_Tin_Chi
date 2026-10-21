-- Account lifecycle. No plaintext passwords, no direct table grants.
SET XACT_ABORT ON;
GO
IF COL_LENGTH('dbo.TAI_KHOAN','phien_ban') IS NULL
 ALTER TABLE dbo.TAI_KHOAN ADD phien_ban rowversion;
IF OBJECT_ID('dbo.ACCOUNT_AUDIT','U') IS NULL
 CREATE TABLE dbo.ACCOUNT_AUDIT(
  id bigint IDENTITY PRIMARY KEY, actor int NOT NULL, target int NOT NULL,
  action varchar(20) NOT NULL, reason nvarchar(500) NOT NULL,
  occurred datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
  before_state nvarchar(500) NULL, after_state nvarchar(500) NULL
 );
GO
CREATE OR ALTER PROCEDURE dbo.usp_TaiKhoan_DanhSach
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20),@id int=dbo.ufn_Campus_AccountId();
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role NOT IN ('DAO_TAO','QUAN_TRI') THROW 52701,N'Bạn không có quyền quản trị tài khoản.',1;
 SELECT t.ma_tk,t.ten_dang_nhap,t.vai_tro,t.ma_sv,t.trang_thai,t.ngay_tao,t.lan_dang_nhap_cuoi,
  CONVERT(varchar(18),CONVERT(binary(8),t.phien_ban),1) phien_ban,CONVERT(bit,CASE WHEN c.ma_tk IS NULL THEN 0 ELSE 1 END) co_mat_khau,
  CONVERT(bit,CASE WHEN t.ma_tk=@id THEN 1 ELSE 0 END) la_ban_than,
  @role vai_tro_nguoi_quan_ly
 FROM dbo.TAI_KHOAN t LEFT JOIN dbo.APP_CREDENTIAL c ON c.ma_tk=t.ma_tk
 WHERE @role='QUAN_TRI' OR t.vai_tro='SINH_VIEN' OR t.ma_tk=@id ORDER BY t.ten_dang_nhap;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_TaiKhoan_Luu
 @ma_tk int=NULL,@ten_dang_nhap nvarchar(128),@vai_tro varchar(20),@ma_sv varchar(20)=NULL,
 @trang_thai bit=1,@phien_ban varchar(18)=NULL,@ly_do nvarchar(500),
 @salt varbinary(16)=NULL,@verifier varbinary(32)=NULL,@iterations int=600000
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @sv varchar(20),@role varchar(20),@actor int=dbo.ufn_Campus_AccountId(),@oldrole varchar(20),@oldsv varchar(20),
  @olduser nvarchar(128),@oldstate bit,@rc int;
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role NOT IN ('DAO_TAO','QUAN_TRI') THROW 52701,N'Bạn không có quyền quản trị tài khoản.',1;
 IF NULLIF(LTRIM(RTRIM(@ly_do)),N'') IS NULL OR @trang_thai IS NULL
  OR NULLIF(LTRIM(RTRIM(@ten_dang_nhap)),N'') IS NULL
  OR @ten_dang_nhap<>LTRIM(RTRIM(@ten_dang_nhap))
  OR @vai_tro IS NULL OR @vai_tro NOT IN ('SINH_VIEN','DAO_TAO','QUAN_TRI')
  OR (@vai_tro='SINH_VIEN' AND @ma_sv IS NULL) OR (@vai_tro<>'SINH_VIEN' AND @ma_sv IS NOT NULL)
  THROW 52702,N'Thông tin tài khoản hoặc lý do không hợp lệ.',1;
 IF @role='DAO_TAO' AND @vai_tro<>'SINH_VIEN' THROW 52701,N'Phòng đào tạo chỉ quản trị tài khoản sinh viên.',1;
 BEGIN TRY
  BEGIN TRAN;
  EXEC @rc=sys.sp_getapplock @Resource=N'Campus:Accounts',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
  IF @rc<0 THROW 52706,N'Hệ thống bận, vui lòng thử lại.',1;
  EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
  SET @actor=dbo.ufn_Campus_AccountId();
  IF @role NOT IN ('DAO_TAO','QUAN_TRI') OR (@role='DAO_TAO' AND @vai_tro<>'SINH_VIEN')
   THROW 52701,N'Quyền quản trị tài khoản đã thay đổi.',1;
  IF @vai_tro='SINH_VIEN' AND NOT EXISTS(SELECT 1 FROM dbo.SINH_VIEN WITH(HOLDLOCK) WHERE ma_sv=@ma_sv AND trang_thai=1)
   THROW 52702,N'Hồ sơ sinh viên không tồn tại hoặc đã ngừng hoạt động.',1;
  IF @ma_tk IS NULL
  BEGIN
   IF @salt IS NULL OR DATALENGTH(@salt)<>16 OR @verifier IS NULL OR DATALENGTH(@verifier)<>32 OR @iterations<>600000
    THROW 52702,N'Cần mật khẩu hợp lệ cho tài khoản mới.',1;
   IF EXISTS(SELECT 1 FROM dbo.TAI_KHOAN WITH(UPDLOCK,HOLDLOCK) WHERE ten_dang_nhap=@ten_dang_nhap)
    THROW 52703,N'Tên đăng nhập đã tồn tại.',1;
   INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro,ma_sv,trang_thai) VALUES(@ten_dang_nhap,@vai_tro,@ma_sv,@trang_thai);
   SET @ma_tk=CONVERT(int,SCOPE_IDENTITY());
   INSERT dbo.APP_CREDENTIAL(ma_tk,salt,verifier,iterations) VALUES(@ma_tk,@salt,@verifier,@iterations);
  END
  ELSE
  BEGIN
   SELECT @oldrole=vai_tro,@oldsv=ma_sv,@olduser=ten_dang_nhap,@oldstate=trang_thai
    FROM dbo.TAI_KHOAN WITH(UPDLOCK,HOLDLOCK) WHERE ma_tk=@ma_tk AND phien_ban=TRY_CONVERT(binary(8),@phien_ban,1);
   IF @oldrole IS NULL THROW 52704,N'Tài khoản đã thay đổi. Tải lại trước khi tiếp tục.',1;
   IF @role='DAO_TAO' AND @oldrole<>'SINH_VIEN' THROW 52701,N'Phòng đào tạo chỉ quản trị tài khoản sinh viên.',1;
   IF @ma_tk=@actor THROW 52705,N'Không tự khóa hoặc sửa quyền của chính mình.',1;
   IF NOT EXISTS(SELECT 1 FROM dbo.APP_CREDENTIAL WHERE ma_tk=@ma_tk)
    THROW 52705,N'Tài khoản Windows được quản lý riêng; không thay đổi tại đây.',1;
   -- Identity mappings are immutable. Create a new account to give a different identity.
   IF @vai_tro<>@oldrole OR COALESCE(@ma_sv,'')<>COALESCE(@oldsv,'') OR @ten_dang_nhap<>@olduser
    THROW 52705,N'Không đổi danh tính hoặc vai trò của tài khoản đã tạo.',1;
   IF @oldrole='QUAN_TRI' AND @trang_thai=0 AND NOT EXISTS(SELECT 1 FROM dbo.TAI_KHOAN WHERE vai_tro='QUAN_TRI' AND trang_thai=1 AND ma_tk<>@ma_tk)
    THROW 52705,N'Không thể khóa tài khoản quản trị cuối cùng.',1;
   UPDATE dbo.TAI_KHOAN SET trang_thai=@trang_thai WHERE ma_tk=@ma_tk;
   DELETE dbo.APP_SESSION WHERE ma_tk=@ma_tk;
   DELETE dbo.APP_LOGIN_CHALLENGE WHERE ma_tk=@ma_tk;
  END;
  INSERT dbo.ACCOUNT_AUDIT(actor,target,action,reason,before_state,after_state)
   VALUES(@actor,@ma_tk,CASE WHEN @oldrole IS NULL THEN 'CREATE' ELSE 'STATUS' END,@ly_do,
    CONCAT(@oldrole,N'|',@oldstate),CONCAT(@vai_tro,N'|',@trang_thai));
  SELECT ma_tk,CONVERT(varchar(18),CONVERT(binary(8),phien_ban),1) phien_ban FROM dbo.TAI_KHOAN WHERE ma_tk=@ma_tk;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_TaiKhoan_Reset
 @ma_tk int,@phien_ban varchar(18),@salt varbinary(16),@verifier varbinary(32),@ly_do nvarchar(500)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @sv varchar(20),@role varchar(20),@targetrole varchar(20),@actor int=dbo.ufn_Campus_AccountId(),@rc int;
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role NOT IN ('DAO_TAO','QUAN_TRI') THROW 52701,N'Bạn không có quyền đặt lại mật khẩu.',1;
 IF @salt IS NULL OR DATALENGTH(@salt)<>16 OR @verifier IS NULL OR DATALENGTH(@verifier)<>32
  OR NULLIF(LTRIM(RTRIM(@ly_do)),N'') IS NULL THROW 52702,N'Cần mật khẩu mới và lý do.',1;
 BEGIN TRY
  BEGIN TRAN;
  EXEC @rc=sys.sp_getapplock @Resource=N'Campus:Accounts',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
  IF @rc<0 THROW 52706,N'Hệ thống bận, vui lòng thử lại.',1;
  EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
  SET @actor=dbo.ufn_Campus_AccountId();
  IF @role NOT IN ('DAO_TAO','QUAN_TRI') THROW 52701,N'Quyền quản trị tài khoản đã thay đổi.',1;
  SELECT @targetrole=vai_tro FROM dbo.TAI_KHOAN WITH(UPDLOCK,HOLDLOCK)
   WHERE ma_tk=@ma_tk AND phien_ban=TRY_CONVERT(binary(8),@phien_ban,1);
  IF @targetrole IS NULL THROW 52704,N'Tài khoản đã thay đổi. Tải lại trước khi tiếp tục.',1;
  IF @role='DAO_TAO' AND @targetrole<>'SINH_VIEN' THROW 52701,N'Phòng đào tạo chỉ đặt lại mật khẩu sinh viên.',1;
  IF @actor=@ma_tk THROW 52705,N'Hãy dùng chức năng đổi mật khẩu của chính mình.',1;
  UPDATE dbo.APP_CREDENTIAL SET salt=@salt,verifier=@verifier,iterations=600000,failed_count=0,locked_until=NULL WHERE ma_tk=@ma_tk;
  IF @@ROWCOUNT=0 THROW 52705,N'Tài khoản Windows không có mật khẩu ứng dụng để đặt lại.',1;
  UPDATE dbo.TAI_KHOAN SET mat_khau_hash=NULL WHERE ma_tk=@ma_tk;
  DELETE dbo.APP_SESSION WHERE ma_tk=@ma_tk;
  DELETE dbo.APP_LOGIN_CHALLENGE WHERE ma_tk=@ma_tk;
  INSERT dbo.ACCOUNT_AUDIT(actor,target,action,reason) VALUES(@actor,@ma_tk,'RESET',@ly_do);
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_TaiKhoan_DoiMatKhau
 @challenge uniqueidentifier,@proof varbinary(32),@salt varbinary(16),@verifier varbinary(32)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @sv varchar(20),@role varchar(20),@actor int=dbo.ufn_Campus_AccountId(),@nonce binary(32),@old binary(32),@until datetime2,@rc int;
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @salt IS NULL OR DATALENGTH(@salt)<>16 OR @verifier IS NULL OR DATALENGTH(@verifier)<>32
  THROW 52702,N'Mật khẩu mới không hợp lệ.',1;
 BEGIN TRY
  BEGIN TRAN;
  EXEC @rc=sys.sp_getapplock @Resource=N'Campus:Accounts',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
  IF @rc<0 THROW 52706,N'Hệ thống bận, vui lòng thử lại.',1;
  EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
  SET @actor=dbo.ufn_Campus_AccountId();
  SELECT @nonce=nonce FROM dbo.APP_LOGIN_CHALLENGE WITH(UPDLOCK,HOLDLOCK)
   WHERE id=@challenge AND ma_tk=@actor AND owner_name=ORIGINAL_LOGIN() AND expires>SYSUTCDATETIME();
  DELETE dbo.APP_LOGIN_CHALLENGE WHERE id=@challenge AND ma_tk=@actor AND owner_name=ORIGINAL_LOGIN();
  SELECT @old=verifier,@until=locked_until FROM dbo.APP_CREDENTIAL WITH(UPDLOCK,HOLDLOCK) WHERE ma_tk=@actor;
  IF @until>SYSUTCDATETIME() BEGIN COMMIT; THROW 52404,N'Tạm khóa xác thực mật khẩu.',1; END;
  IF @nonce IS NULL OR @old IS NULL OR @proof IS NULL OR DATALENGTH(@proof)<>32 OR HASHBYTES('SHA2_256',@nonce+@old)<>@proof
  BEGIN
   UPDATE dbo.APP_CREDENTIAL SET failed_count=CASE WHEN locked_until<=SYSUTCDATETIME() THEN 1 ELSE failed_count+1 END,
    locked_until=CASE WHEN failed_count>=4 AND (locked_until IS NULL OR locked_until>SYSUTCDATETIME()) THEN DATEADD(MINUTE,10,SYSUTCDATETIME()) ELSE NULL END WHERE ma_tk=@actor;
   COMMIT; THROW 52707,N'Mật khẩu hiện tại không đúng hoặc yêu cầu xác thực đã hết hạn.',1;
  END;
  UPDATE dbo.APP_CREDENTIAL SET salt=@salt,verifier=@verifier,iterations=600000,failed_count=0,locked_until=NULL WHERE ma_tk=@actor;
  UPDATE dbo.TAI_KHOAN SET mat_khau_hash=NULL WHERE ma_tk=@actor;
  DELETE dbo.APP_SESSION WHERE ma_tk=@actor;
  DELETE dbo.APP_LOGIN_CHALLENGE WHERE ma_tk=@actor;
  INSERT dbo.ACCOUNT_AUDIT(actor,target,action,reason) VALUES(@actor,@actor,'PASSWORD',N'Chủ tài khoản xác thực mật khẩu hiện tại.');
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_TaiKhoan_NhatKy
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20),@actor int=dbo.ufn_Campus_AccountId();
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role NOT IN ('DAO_TAO','QUAN_TRI') THROW 52701,N'Bạn không có quyền xem nhật ký tài khoản.',1;
 SELECT TOP(500) a.id,a.actor,a.target,a.action,a.reason,a.occurred,a.before_state,a.after_state
 FROM dbo.ACCOUNT_AUDIT a JOIN dbo.TAI_KHOAN t ON t.ma_tk=a.target
 WHERE @role='QUAN_TRI' OR t.vai_tro='SINH_VIEN' OR a.target=@actor ORDER BY a.id DESC;
END;
GO
GRANT EXECUTE ON dbo.usp_TaiKhoan_DanhSach TO app_minh;
GRANT EXECUTE ON dbo.usp_TaiKhoan_Luu TO app_minh;
GRANT EXECUTE ON dbo.usp_TaiKhoan_Reset TO app_minh;
GRANT EXECUTE ON dbo.usp_TaiKhoan_DoiMatKhau TO app_minh;
GRANT EXECUTE ON dbo.usp_TaiKhoan_NhatKy TO app_minh;
GO
-- Auth and account administration use the same first lock, preventing the
-- credential/account lock inversion when a password is reset during login.
DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_Auth_Complete'));
IF @definition IS NULL THROW 52708,N'Thiếu phân hệ đăng nhập.',1;
IF CHARINDEX(N'Campus:Accounts',@definition)=0
BEGIN
 SET @definition=STUFF(@definition,1,CHARINDEX(N'PROCEDURE',@definition)-1,N'ALTER ');
 SET @definition=REPLACE(@definition,N'BEGIN TRAN;',N'BEGIN TRAN;
 DECLARE @accountLock int;
 EXEC @accountLock=sys.sp_getapplock @Resource=N''Campus:Accounts'',@LockMode=''Exclusive'',@LockOwner=''Transaction'',@LockTimeout=10000;
 IF @accountLock<0 BEGIN ROLLBACK; THROW 52406,N''Xác thực đang bận. Hãy thử lại.'',1; END;');
 EXEC sys.sp_executesql @definition;
END;
GO
