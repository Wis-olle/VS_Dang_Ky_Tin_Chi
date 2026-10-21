-- Shared permission, audit and committed-event inbox. No external delivery channel.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('dbo.CAMPUS_PERMISSION','U') IS NULL
 CREATE TABLE dbo.CAMPUS_PERMISSION(
  account_id int NOT NULL REFERENCES dbo.TAI_KHOAN(ma_tk), permission varchar(40) NOT NULL,
  PRIMARY KEY(account_id,permission));
IF OBJECT_ID('dbo.CAMPUS_AUDIT','U') IS NULL
 CREATE TABLE dbo.CAMPUS_AUDIT(
  id bigint IDENTITY PRIMARY KEY,actor int NOT NULL,occurred_utc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
  event_type varchar(40) NOT NULL,entity varchar(100) NOT NULL,reason nvarchar(500) NOT NULL,
  request_id uniqueidentifier NULL,student varchar(20) NULL,before_json nvarchar(max) NULL,after_json nvarchar(max) NULL);
IF OBJECT_ID('dbo.CAMPUS_OUTBOX','U') IS NULL
 CREATE TABLE dbo.CAMPUS_OUTBOX(
  id bigint IDENTITY PRIMARY KEY,audit_id bigint NOT NULL UNIQUE REFERENCES dbo.CAMPUS_AUDIT(id),
  event_type varchar(40) NOT NULL,entity varchar(100) NOT NULL,message nvarchar(500) NOT NULL,student varchar(20) NULL,
  created_utc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), delivered_utc datetime2 NULL,
  attempts int NOT NULL DEFAULT 0,next_attempt_utc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),last_error nvarchar(200) NULL);
IF OBJECT_ID('dbo.CAMPUS_NOTIFICATION','U') IS NULL
 CREATE TABLE dbo.CAMPUS_NOTIFICATION(
  event_id bigint NOT NULL REFERENCES dbo.CAMPUS_OUTBOX(id),account_id int NOT NULL REFERENCES dbo.TAI_KHOAN(ma_tk),
  read_utc datetime2 NULL,PRIMARY KEY(event_id,account_id));
GO
CREATE OR ALTER PROCEDURE dbo.usp_Campus_RequirePermission @permission varchar(40)
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20); EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'QUAN_TRI' AND (@role<>'DAO_TAO' OR NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission=@permission))
  THROW 52801,N'Chưa được cấp quyền chi tiết cho thao tác này. Liên hệ quản trị.',1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Campus_Emit
 @event_type varchar(40),@entity varchar(100),@reason nvarchar(500),@before_json nvarchar(max)=NULL,
 @after_json nvarchar(max)=NULL,@request_id uniqueidentifier=NULL,@student varchar(20)=NULL
AS
BEGIN
 SET NOCOUNT ON;
 IF @@TRANCOUNT=0 THROW 52802,N'Sự kiện cần nằm trong giao dịch nghiệp vụ.',1;
 IF NULLIF(LTRIM(RTRIM(@reason)),N'') IS NULL OR NULLIF(@entity,'') IS NULL OR NULLIF(@event_type,'') IS NULL
  OR (@before_json IS NOT NULL AND ISJSON(@before_json)<>1) OR (@after_json IS NOT NULL AND ISJSON(@after_json)<>1)
  THROW 52802,N'Dấu vết nghiệp vụ không hợp lệ.',1;
 DECLARE @actor int=dbo.ufn_Campus_AccountId(),@id bigint;
 IF @actor IS NULL THROW 51001,N'Phiên đăng nhập không hợp lệ.',1;
 INSERT dbo.CAMPUS_AUDIT(actor,event_type,entity,reason,request_id,student,before_json,after_json)
 VALUES(@actor,@event_type,@entity,@reason,@request_id,@student,@before_json,@after_json);
 SET @id=SCOPE_IDENTITY();
 INSERT dbo.CAMPUS_OUTBOX(audit_id,event_type,entity,message,student) VALUES(@id,@event_type,@entity,@reason,@student);
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Campus_SetPermission
 @account_id int,@permission varchar(40),@enabled bit,@reason nvarchar(500)
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @sv varchar(20),@role varchar(20);
 BEGIN TRY
  BEGIN TRAN; EXEC dbo.usp_HocVu_Lock;
  EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
  IF @role<>'QUAN_TRI' THROW 52801,N'Chỉ quản trị được phân quyền.',1;
  IF @enabled IS NULL OR @permission NOT IN('CLASS_OPERATE','EXCEPTION_REVIEW','PROFILE_EDIT','HOLD_MANAGE','AUDIT_READ','SEAT_MANAGE','WAITLIST_OPERATE','ADVISE_PLAN','IMPORT_DATA','OPERATIONS_READ')
   OR NULLIF(LTRIM(RTRIM(@reason)),N'') IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.TAI_KHOAN WHERE ma_tk=@account_id AND vai_tro='DAO_TAO' AND trang_thai=1)
   THROW 52802,N'Quyền hoặc tài khoản nhận quyền không hợp lệ.',1;
  IF @enabled=1 AND NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_PERMISSION WHERE account_id=@account_id AND permission=@permission)
   INSERT dbo.CAMPUS_PERMISSION VALUES(@account_id,@permission);
  IF @enabled=0 DELETE dbo.CAMPUS_PERMISSION WHERE account_id=@account_id AND permission=@permission;
  DECLARE @entity varchar(100)=CONCAT('ACCOUNT:',@account_id),@after nvarchar(max)=(SELECT @permission permission,@enabled enabled FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.usp_Campus_Emit 'PERMISSION',@entity,@reason,NULL,@after;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Campus_Deliver
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 -- Delivery runs only after the caller's business transaction is committed.
 IF @@TRANCOUNT<>0 RETURN;
 -- A pooled caller may have previously used SERIALIZABLE; READPAST requires
 -- read committed. Procedure-scoped SET restores the caller setting on exit.
 SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
 -- Short atomic SQL-only routing. Unique event/account prevents duplicate delivery on retry.
 BEGIN TRY
  BEGIN TRAN;
  DECLARE @pending TABLE(id bigint PRIMARY KEY);
  INSERT @pending SELECT TOP(100) id FROM dbo.CAMPUS_OUTBOX WITH(UPDLOCK,READPAST,ROWLOCK)
   WHERE delivered_utc IS NULL AND next_attempt_utc<=SYSUTCDATETIME() ORDER BY id;
  INSERT dbo.CAMPUS_NOTIFICATION(event_id,account_id)
   SELECT p.id,t.ma_tk FROM @pending p JOIN dbo.CAMPUS_OUTBOX o ON o.id=p.id
   JOIN dbo.TAI_KHOAN t ON t.trang_thai=1 AND ((o.student IS NOT NULL AND t.ma_sv=o.student AND t.vai_tro='SINH_VIEN') OR (o.student IS NULL AND t.vai_tro IN('DAO_TAO','QUAN_TRI')))
   WHERE NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_NOTIFICATION n WHERE n.event_id=p.id AND n.account_id=t.ma_tk);
  UPDATE o SET delivered_utc=SYSUTCDATETIME(),attempts=attempts+1,last_error=NULL FROM dbo.CAMPUS_OUTBOX o JOIN @pending p ON p.id=o.id;
  COMMIT;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  -- Delivery is after business commit; its failure must never undo enrollment.
  UPDATE dbo.CAMPUS_OUTBOX SET attempts=attempts+1,next_attempt_utc=DATEADD(minute,1,SYSUTCDATETIME()),last_error=N'Chưa chuyển được thông báo nội bộ. Sẽ thử lại.'
   WHERE delivered_utc IS NULL AND next_attempt_utc<=SYSUTCDATETIME();
 END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Campus_Notifications @read_id bigint=NULL
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20);EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 EXEC dbo.usp_Campus_Deliver;
 DECLARE @actor int=dbo.ufn_Campus_AccountId();
 IF @read_id IS NOT NULL UPDATE dbo.CAMPUS_NOTIFICATION SET read_utc=COALESCE(read_utc,SYSUTCDATETIME()) WHERE event_id=@read_id AND account_id=@actor;
 SELECT TOP(200) o.id,o.event_type,o.entity,o.message,o.created_utc,n.read_utc FROM dbo.CAMPUS_NOTIFICATION n
  JOIN dbo.CAMPUS_OUTBOX o ON o.id=n.event_id WHERE n.account_id=@actor ORDER BY o.id DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Campus_Operations
AS
BEGIN
 SET NOCOUNT ON; EXEC dbo.usp_Campus_RequirePermission 'AUDIT_READ';
 SELECT TOP(300) a.*,o.delivered_utc,o.attempts,o.next_attempt_utc,o.last_error FROM dbo.CAMPUS_AUDIT a
  JOIN dbo.CAMPUS_OUTBOX o ON o.audit_id=a.id ORDER BY a.id DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Campus_Permissions @assignments bit=0
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20);EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @assignments=1
 BEGIN
  IF @role<>'QUAN_TRI' THROW 52801,N'Chỉ quản trị được xem phân quyền tài khoản.',1;
  SELECT t.ma_tk,t.ten_dang_nhap,STRING_AGG(p.permission,', ') permission FROM dbo.TAI_KHOAN t
   LEFT JOIN dbo.CAMPUS_PERMISSION p ON p.account_id=t.ma_tk WHERE t.vai_tro='DAO_TAO' GROUP BY t.ma_tk,t.ten_dang_nhap;
 END
 ELSE SELECT permission FROM dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId();
END;
GO
DENY EXECUTE ON dbo.usp_Campus_Emit TO public;
DENY EXECUTE ON dbo.usp_Campus_Deliver TO public;
DENY SELECT,INSERT,UPDATE,DELETE ON dbo.CAMPUS_PERMISSION TO public;
DENY SELECT,INSERT,UPDATE,DELETE ON dbo.CAMPUS_AUDIT TO public;
DENY SELECT,INSERT,UPDATE,DELETE ON dbo.CAMPUS_OUTBOX TO public;
DENY SELECT,INSERT,UPDATE,DELETE ON dbo.CAMPUS_NOTIFICATION TO public;
GRANT EXECUTE ON dbo.usp_Campus_Notifications TO app_minh;
GRANT EXECUTE ON dbo.usp_Campus_Notifications TO app_dang_ky;
GRANT EXECUTE ON dbo.usp_Campus_Operations TO app_minh;
GRANT EXECUTE ON dbo.usp_Campus_Permissions TO app_minh;
GRANT EXECUTE ON dbo.usp_Campus_SetPermission TO app_minh;
GO
