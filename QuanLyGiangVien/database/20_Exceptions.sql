-- Scoped, versioned student petitions. No permissions/accounts are seeded by this migration.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('dbo.DK3_EXCEPTION_REQUEST','U') IS NULL
 CREATE TABLE dbo.DK3_EXCEPTION_REQUEST(
  id bigint IDENTITY PRIMARY KEY,student varchar(20) NOT NULL REFERENCES dbo.SINH_VIEN(ma_sv),
  term varchar(20) NOT NULL REFERENCES dbo.HOC_KY(ma_hk),kind varchar(20) NOT NULL,
  course varchar(20) NULL REFERENCES dbo.HOC_PHAN(ma_hp),class_id varchar(20) NULL REFERENCES dbo.LOP_HOC_PHAN(ma_lhp),
  rule_code varchar(30) NULL,program_id varchar(20) NULL,reason nvarchar(500) NOT NULL,status varchar(20) NOT NULL DEFAULT 'PENDING',
  submitted_by int NOT NULL REFERENCES dbo.TAI_KHOAN(ma_tk),submitted_utc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
  reviewed_by int NULL REFERENCES dbo.TAI_KHOAN(ma_tk),reviewed_utc datetime2 NULL,decision_reason nvarchar(500) NULL,
  expires_utc datetime2 NULL,granted_uses int NULL,remaining_uses int NULL,max_credits int NULL,version rowversion,
  CONSTRAINT CK_DK3_EXCEPTION_KIND CHECK(kind IN('MAX_CREDITS','REQUIREMENT','LATE_REGISTER','WITHDRAW')),
  CONSTRAINT CK_DK3_EXCEPTION_STATUS CHECK(status IN('PENDING','APPROVED','REJECTED','REVOKED','APPLIED')),
  CONSTRAINT CK_DK3_EXCEPTION_USES CHECK(granted_uses BETWEEN 1 AND 100 AND remaining_uses BETWEEN 0 AND granted_uses OR granted_uses IS NULL AND remaining_uses IS NULL),
  CONSTRAINT CK_DK3_EXCEPTION_CREDITS CHECK(max_credits IS NULL OR max_credits BETWEEN 1 AND 1000));
IF COL_LENGTH('dbo.DK3_EXCEPTION_REQUEST','program_id') IS NULL ALTER TABLE dbo.DK3_EXCEPTION_REQUEST ADD program_id varchar(20) NULL;
IF OBJECT_ID('dbo.DK3_EXCEPTION_USE','U') IS NULL
 CREATE TABLE dbo.DK3_EXCEPTION_USE(
  exception_id bigint NOT NULL REFERENCES dbo.DK3_EXCEPTION_REQUEST(id),request_id uniqueidentifier NOT NULL,
  used_utc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),actor int NOT NULL REFERENCES dbo.TAI_KHOAN(ma_tk),
  PRIMARY KEY(exception_id,request_id));
GO
CREATE OR ALTER PROCEDURE dbo.usp_DK3_ExceptionList @term varchar(20)=NULL
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20);EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' EXEC dbo.usp_Campus_RequirePermission 'EXCEPTION_REVIEW';
 SELECT r.id,r.student,r.term,r.kind,r.course,r.class_id,r.rule_code,r.program_id,r.reason,r.status,
  CASE WHEN r.status='APPROVED' AND r.expires_utc<=SYSUTCDATETIME() THEN 'EXPIRED' ELSE r.status END effective_status,
  r.submitted_by,r.submitted_utc,r.reviewed_by,r.reviewed_utc,r.decision_reason,r.expires_utc,r.granted_uses,r.remaining_uses,r.max_credits,
  CONVERT(varchar(16),r.version,2) version
 FROM dbo.DK3_EXCEPTION_REQUEST r WHERE (@role<>'SINH_VIEN' OR r.student=@sv) AND (@term IS NULL OR r.term=@term)
 ORDER BY r.id DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_DK3_ExceptionSubmit
 @kind varchar(20),@term varchar(20),@reason nvarchar(500),@course varchar(20)=NULL,@class varchar(20)=NULL,@rule varchar(30)=NULL
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @sv varchar(20),@role varchar(20),@id bigint,@actor int,@after nvarchar(max),@entity varchar(100),@program varchar(20);
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;
  EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
  IF @role<>'SINH_VIEN' THROW 51005,N'Chỉ sinh viên được gửi đơn cho hồ sơ của mình.',1;
  SET @actor=dbo.ufn_Campus_AccountId();
  IF @kind IS NULL OR @kind NOT IN('MAX_CREDITS','REQUIREMENT','LATE_REGISTER','WITHDRAW') OR NULLIF(LTRIM(RTRIM(@reason)),N'') IS NULL
   OR NOT EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@term)
   OR (@course IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.HOC_PHAN WHERE ma_hp=@course))
   OR (@class IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_lhp=@class AND ma_hk=@term AND (@course IS NULL OR ma_hp=@course)))
   THROW 52902,N'Loại đơn, kỳ, môn, lớp hoặc lý do không hợp lệ.',1;
  IF @kind='MAX_CREDITS' AND (@course IS NOT NULL OR @class IS NOT NULL OR @rule IS NOT NULL)
   THROW 52902,N'Ngoại lệ tín chỉ chỉ có phạm vi sinh viên/học kỳ.',1;
  IF @kind='REQUIREMENT' AND (@course IS NULL OR @rule IS NULL OR LEFT(@rule,6)<>'GROUP_' OR
   TRY_CONVERT(int,SUBSTRING(@rule,7,24)) NOT BETWEEN 1 AND 1000 OR @rule<>CONCAT('GROUP_',TRY_CONVERT(int,SUBSTRING(@rule,7,24))))
   THROW 52902,N'Ngoại lệ điều kiện cần môn và đúng nhóm điều kiện GROUP_1–GROUP_1000.',1;
  IF @kind IN('LATE_REGISTER','WITHDRAW') AND (@class IS NULL OR @rule IS NOT NULL)
   THROW 52902,N'Đăng ký muộn/rút môn cần lớp cụ thể; không cấp quyền bỏ qua điều kiện học vụ.',1;
  IF @kind='REQUIREMENT' AND NOT EXISTS(SELECT 1 FROM dbo.v_HV_Assignments a JOIN dbo.v_HV_Requirements r ON r.program=a.program
   WHERE a.student=@sv AND r.course=@course AND r.[group]=TRY_CONVERT(int,SUBSTRING(@rule,7,24)))
   THROW 52902,N'Nhóm điều kiện không thuộc CTĐT được gắn cho sinh viên.',1;
  IF @kind='REQUIREMENT' SELECT @program=program FROM dbo.v_HV_Assignments WHERE student=@sv;
  IF @kind='WITHDRAW' AND NOT EXISTS(SELECT 1 FROM dbo.DANG_KY WHERE ma_sv=@sv AND ma_lhp=@class AND trang_thai='DANG_KY')
   THROW 52902,N'Chỉ gửi đơn rút lớp đang đăng ký.',1;
  INSERT dbo.DK3_EXCEPTION_REQUEST(student,term,kind,course,class_id,rule_code,program_id,reason,submitted_by)
   VALUES(@sv,@term,@kind,@course,@class,@rule,@program,LTRIM(RTRIM(@reason)),@actor);
  SET @id=SCOPE_IDENTITY();SET @entity=CONCAT('EXCEPTION:',@id);
  SET @after=(SELECT id,student,term,kind,course,class_id,rule_code,program_id,reason,status,submitted_by,submitted_utc FROM dbo.DK3_EXCEPTION_REQUEST WHERE id=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.usp_Campus_Emit 'EXCEPTION_SUBMITTED',@entity,@reason,NULL,@after,NULL,@sv;
  COMMIT;
  SELECT @id id,CONVERT(varchar(16),version,2) version FROM dbo.DK3_EXCEPTION_REQUEST WHERE id=@id;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_DK3_ExceptionDecide
 @id bigint,@version binary(8),@decision varchar(20),@reason nvarchar(500),@expires_utc datetime2=NULL,@uses int=NULL,@max_credits int=NULL
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @kind varchar(20),@status varchar(20),@sv varchar(20),@before nvarchar(max),@after nvarchar(max),@entity varchar(100),@event varchar(40);
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Campus_RequirePermission 'EXCEPTION_REVIEW';
  IF @id<=0 OR @decision IS NULL OR @decision NOT IN('APPROVE','REJECT','REVOKE') OR NULLIF(LTRIM(RTRIM(@reason)),N'') IS NULL
   THROW 52902,N'Quyết định hoặc lý do không hợp lệ.',1;
  SELECT @kind=kind,@status=status,@sv=student FROM dbo.DK3_EXCEPTION_REQUEST WITH(UPDLOCK,HOLDLOCK) WHERE id=@id AND version=@version;
  IF @kind IS NULL THROW 52904,N'Đơn đã thay đổi hoặc không tồn tại. Hãy tải lại trước khi quyết định.',1;
  IF @decision IN('APPROVE','REJECT') AND @status<>'PENDING' OR @decision='REVOKE' AND @status<>'APPROVED'
   THROW 52903,N'Trạng thái đơn không cho phép chuyển trạng thái này.',1;
  IF @decision='APPROVE' AND (@expires_utc IS NULL OR @expires_utc<=SYSUTCDATETIME() OR @uses IS NULL OR @uses NOT BETWEEN 1 AND 100
   OR @kind='MAX_CREDITS' AND (@max_credits IS NULL OR @max_credits NOT BETWEEN 1 AND 1000)
   OR @kind<>'MAX_CREDITS' AND @max_credits IS NOT NULL)
   THROW 52902,N'Phê duyệt cần hạn tương lai và 1–100 lượt; chỉ ngoại lệ tín chỉ được cấp ngưỡng tín chỉ.',1;
  IF @decision<>'APPROVE' AND (@expires_utc IS NOT NULL OR @uses IS NOT NULL OR @max_credits IS NOT NULL)
   THROW 52902,N'Từ chối/thu hồi không được cấp thêm quyền sử dụng.',1;
  SET @before=(SELECT * FROM dbo.DK3_EXCEPTION_REQUEST WHERE id=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  UPDATE dbo.DK3_EXCEPTION_REQUEST SET status=CASE @decision WHEN 'APPROVE' THEN 'APPROVED' WHEN 'REJECT' THEN 'REJECTED' ELSE 'REVOKED' END,
   reviewed_by=dbo.ufn_Campus_AccountId(),reviewed_utc=SYSUTCDATETIME(),decision_reason=LTRIM(RTRIM(@reason)),
   expires_utc=CASE WHEN @decision='APPROVE' THEN @expires_utc ELSE expires_utc END,
   granted_uses=CASE WHEN @decision='APPROVE' THEN @uses ELSE granted_uses END,
   remaining_uses=CASE WHEN @decision='APPROVE' THEN @uses ELSE remaining_uses END,
   max_credits=CASE WHEN @decision='APPROVE' THEN @max_credits ELSE max_credits END WHERE id=@id;
  SET @after=(SELECT * FROM dbo.DK3_EXCEPTION_REQUEST WHERE id=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  SET @entity=CONCAT('EXCEPTION:',@id);SET @event=CONCAT('EXCEPTION_',@decision);
  EXEC dbo.usp_Campus_Emit @event,@entity,@reason,@before,@after,NULL,@sv;
  COMMIT;
  SELECT id,status,CONVERT(varchar(16),version,2) version FROM dbo.DK3_EXCEPTION_REQUEST WHERE id=@id;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
-- Internal hook called with the final plan and all locks held by the enrollment engine.
-- A code is removed only when every occurrence of its scope is covered. Capacity, hold,
-- timetable, missing components and class lifecycle are deliberately never filtered.
CREATE OR ALTER PROCEDURE dbo.usp_DK3_FilterIssues
 @student varchar(20),@term varchar(20),@selected_json nvarchar(max),@consume bit,@request_id uniqueidentifier=NULL,@retained_only bit=0
AS
BEGIN
 SET NOCOUNT ON;
 IF @@TRANCOUNT=0 OR @consume IS NULL OR @consume=1 AND @request_id IS NULL
  THROW 52902,N'Áp dụng ngoại lệ cần nằm trong giao dịch và có mã yêu cầu.',1;
 DECLARE @total int=(SELECT MAX(total_credits) FROM #DK3_Context);
 DECLARE @coverage TABLE(code varchar(30),course varchar(20),class_id varchar(20),rule_code varchar(30),exception_id bigint NULL,retained bit NOT NULL);
 INSERT @coverage
 SELECT s.code,s.course,s.class_id,s.rule_code,COALESCE(retained.id,g.id),CONVERT(bit,CASE WHEN retained.id IS NULL THEN 0 ELSE 1 END)
 FROM #DK3_IssueScope s OUTER APPLY(
  -- A committed prerequisite authorization belongs to the exact enrollment it admitted.
  -- Expiry/revocation limit new uses; they do not silently undo an existing enrollment.
  -- A different curriculum, a dropped registration, or a newly selected class of this
  -- course must pass the current rules or spend a new approved use instead.
  SELECT TOP(1) e.id FROM dbo.DK3_EXCEPTION_REQUEST e
  JOIN dbo.DK3_EXCEPTION_USE u ON u.exception_id=e.id
  JOIN dbo.DK3_REQUEST request ON request.student=@student AND request.term=@term AND request.request_id=u.request_id
  CROSS APPLY OPENJSON(request.result_json) WITH(ma_dk bigint,ma_lhp varchar(20),action varchar(10)) result
  JOIN dbo.DANG_KY enrollment ON enrollment.ma_dk=result.ma_dk AND enrollment.ma_lhp=result.ma_lhp
   AND enrollment.ma_sv=@student AND enrollment.trang_thai='DANG_KY'
  JOIN dbo.LOP_HOC_PHAN enrolled_class ON enrolled_class.ma_lhp=enrollment.ma_lhp AND enrolled_class.ma_hk=@term AND enrolled_class.ma_hp=s.course
  WHERE s.code='REQUIREMENT' AND e.kind='REQUIREMENT' AND e.student=@student AND e.term=@term
   AND e.course=s.course AND e.rule_code=s.rule_code AND result.action='ADD'
   AND (e.class_id IS NULL OR e.class_id=enrollment.ma_lhp)
   AND EXISTS(SELECT 1 FROM dbo.v_HV_Assignments assignment WHERE assignment.student=@student AND assignment.program=e.program_id)
   AND NOT EXISTS(SELECT 1 FROM OPENJSON(@selected_json) selected JOIN dbo.LOP_HOC_PHAN proposed ON proposed.ma_lhp=selected.value WHERE proposed.ma_hp=s.course)
  ORDER BY e.id) retained
 OUTER APPLY(
  SELECT TOP(1) e.id FROM dbo.DK3_EXCEPTION_REQUEST e WITH(UPDLOCK,HOLDLOCK)
  WHERE @retained_only=0 AND retained.id IS NULL AND e.student=@student AND e.term=@term AND e.kind=s.code AND e.expires_utc>SYSUTCDATETIME()
   AND (e.status='APPROVED' AND e.remaining_uses>0 OR e.status='APPLIED' AND EXISTS(SELECT 1 FROM dbo.DK3_EXCEPTION_USE u WHERE u.exception_id=e.id AND u.request_id=@request_id))
   AND (e.kind='MAX_CREDITS' AND e.course IS NULL AND e.class_id IS NULL AND e.rule_code IS NULL AND e.max_credits>=@total
    OR e.kind='REQUIREMENT' AND e.course=s.course AND e.rule_code=s.rule_code
     AND EXISTS(SELECT 1 FROM dbo.v_HV_Assignments assignment WHERE assignment.student=@student AND assignment.program=e.program_id)
     AND (e.class_id IS NULL OR e.class_id=s.class_id
     OR s.class_id IS NULL AND EXISTS(SELECT 1 FROM OPENJSON(@selected_json) selected WHERE selected.value=e.class_id))
    OR e.kind IN('LATE_REGISTER','WITHDRAW') AND e.class_id=s.class_id AND (e.course IS NULL OR e.course=s.course) AND e.rule_code IS NULL)
  ORDER BY e.expires_utc,e.id) g
 WHERE s.code IN('MAX_CREDITS','REQUIREMENT','LATE_REGISTER','WITHDRAW') AND EXISTS(SELECT 1 FROM #HV_Issues i WHERE i.code=s.code AND i.blocking=1);
 DECLARE @eligible TABLE(code varchar(30) PRIMARY KEY);
 INSERT @eligible SELECT code FROM @coverage GROUP BY code HAVING COUNT(*)=COUNT(exception_id);
 -- Do not consume any decision if a different issue still blocks the whole atomic request.
 IF @consume=1 AND NOT EXISTS(SELECT 1 FROM #HV_Issues WHERE blocking=1 AND code NOT IN(SELECT code FROM @eligible))
 BEGIN
  DECLARE @used TABLE(id bigint PRIMARY KEY,before_json nvarchar(max));
  INSERT @used SELECT DISTINCT e.id,(SELECT e.id,e.status,e.remaining_uses FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)
   FROM @coverage c JOIN @eligible ok ON ok.code=c.code JOIN dbo.DK3_EXCEPTION_REQUEST e ON e.id=c.exception_id
   WHERE c.retained=0 AND NOT EXISTS(SELECT 1 FROM dbo.DK3_EXCEPTION_USE u WHERE u.exception_id=e.id AND u.request_id=@request_id);
  INSERT dbo.DK3_EXCEPTION_USE(exception_id,request_id,actor) SELECT id,@request_id,dbo.ufn_Campus_AccountId() FROM @used;
  UPDATE e SET remaining_uses=e.remaining_uses-1,status=CASE WHEN e.remaining_uses=1 THEN 'APPLIED' ELSE 'APPROVED' END
   FROM dbo.DK3_EXCEPTION_REQUEST e JOIN @used u ON u.id=e.id;
  DECLARE @id bigint,@before nvarchar(max),@after nvarchar(max),@entity varchar(100);
  DECLARE used CURSOR LOCAL FAST_FORWARD FOR SELECT id,before_json FROM @used ORDER BY id;
  OPEN used;FETCH NEXT FROM used INTO @id,@before;
  WHILE @@FETCH_STATUS=0
  BEGIN
   SET @entity=CONCAT('EXCEPTION:',@id);SET @after=(SELECT id,status,remaining_uses FROM dbo.DK3_EXCEPTION_REQUEST WHERE id=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
   EXEC dbo.usp_Campus_Emit 'EXCEPTION_APPLIED',@entity,N'Đã áp dụng quyết định ngoại lệ trong giao dịch đăng ký.',@before,@after,@request_id,@student;
   FETCH NEXT FROM used INTO @id,@before;
  END;
  CLOSE used;DEALLOCATE used;
 END;
 DELETE i FROM #HV_Issues i JOIN @eligible e ON e.code=i.code WHERE i.blocking=1;
END;
GO
-- Keep the existing full academic validation/history implementation behind a guarded entrypoint.
IF OBJECT_ID('dbo.usp_HocVu_SaveLegacy','P') IS NULL
 EXEC sys.sp_rename N'dbo.usp_HocVu_Save',N'usp_HocVu_SaveLegacy',N'OBJECT';
ELSE IF CHARINDEX(N'usp_HocVu_SaveLegacy',OBJECT_DEFINITION(OBJECT_ID('dbo.usp_HocVu_Save')))=0
BEGIN
 DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_HocVu_Save'));
 DECLARE @start int=CHARINDEX('PROCEDURE',UPPER(@definition));
 SET @definition=N'CREATE OR ALTER '+SUBSTRING(@definition,@start,LEN(@definition));
 SET @definition=REPLACE(@definition,N'dbo.usp_HocVu_Save ',N'dbo.usp_HocVu_SaveLegacy ');
 EXEC sys.sp_executesql @definition;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_HocVu_Save @kind varchar(20),@id varchar(20),@version binary(8)=NULL,@payload nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;
  IF @kind IN('STANDING','ASSIGNMENT') EXEC dbo.usp_Campus_RequirePermission 'PROFILE_EDIT';
  IF @kind='HOLD' EXEC dbo.usp_Campus_RequirePermission 'HOLD_MANAGE';
  IF @kind='OVERRIDE' EXEC dbo.usp_Campus_RequirePermission 'EXCEPTION_REVIEW';
  EXEC dbo.usp_HocVu_SaveLegacy @kind,@id,@version,@payload;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
DENY EXECUTE ON dbo.usp_HocVu_SaveLegacy TO public;
DENY EXECUTE ON dbo.usp_DK3_FilterIssues TO public;
GRANT EXECUTE ON dbo.usp_HocVu_Save TO app_minh;
GRANT EXECUTE ON dbo.usp_DK3_ExceptionList TO app_minh;
GRANT EXECUTE ON dbo.usp_DK3_ExceptionSubmit TO app_minh;
GRANT EXECUTE ON dbo.usp_DK3_ExceptionDecide TO app_minh;
GRANT EXECUTE ON dbo.usp_DK3_ExceptionList TO app_dang_ky;
GRANT EXECUTE ON dbo.usp_DK3_ExceptionSubmit TO app_dang_ky;
GO
-- Student profile editing uses the same detailed right, including recheck after lock waits.
-- Patch the existing validated procedures in place so there is no second bypassable core.
DECLARE @procedure sysname,@definition nvarchar(max),@start int;
DECLARE profile_commands CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.procedures WHERE schema_id=SCHEMA_ID('dbo') AND name IN('usp_SinhVien_Luu','usp_SinhVien_TrangThai');
OPEN profile_commands;FETCH NEXT FROM profile_commands INTO @procedure;
WHILE @@FETCH_STATUS=0
BEGIN
 SET @definition=OBJECT_DEFINITION(OBJECT_ID(N'dbo.'+@procedure));
 IF CHARINDEX(N'usp_Campus_RequirePermission',@definition)=0
 BEGIN
  SET @start=CHARINDEX('PROCEDURE',UPPER(@definition));
  SET @definition=N'CREATE OR ALTER '+SUBSTRING(@definition,@start,LEN(@definition));
  SET @definition=REPLACE(@definition,N'EXEC dbo.usp_Minh_KiemTraQuyen;',N'EXEC dbo.usp_Minh_KiemTraQuyen; EXEC dbo.usp_Campus_RequirePermission ''PROFILE_EDIT'';');
  EXEC sys.sp_executesql @definition;
 END;
 FETCH NEXT FROM profile_commands INTO @procedure;
END;
CLOSE profile_commands;DEALLOCATE profile_commands;
GO
