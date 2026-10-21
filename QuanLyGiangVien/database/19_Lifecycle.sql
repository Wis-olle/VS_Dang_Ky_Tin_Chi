-- Phase 3: lifecycle and one actual-date calendar shared by all consumers.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF COL_LENGTH('dbo.LOP_HOC_PHAN','lifecycle') IS NULL
 ALTER TABLE dbo.LOP_HOC_PHAN ADD lifecycle varchar(20) NOT NULL CONSTRAINT DF_LifecycleClass DEFAULT 'REGISTRATION'
 CONSTRAINT CK_LifecycleClass CHECK(lifecycle IN('REGISTRATION','CLOSED','TEACHING','COMPLETED','CANCELED','ARCHIVED'));
IF COL_LENGTH('dbo.HOC_KY','lifecycle') IS NULL
 ALTER TABLE dbo.HOC_KY ADD lifecycle varchar(20) NOT NULL CONSTRAINT DF_LifecycleTerm DEFAULT 'REGISTRATION'
 CONSTRAINT CK_LifecycleTerm CHECK(lifecycle IN('REGISTRATION','CLOSED','TEACHING','COMPLETED','CANCELED','ARCHIVED'));
IF COL_LENGTH('dbo.HOC_KY','lifecycle_version') IS NULL ALTER TABLE dbo.HOC_KY ADD lifecycle_version rowversion;
GO
-- Refresh SELECT h.* metadata after additive lifecycle columns.
EXEC sys.sp_refreshview N'dbo.vw_Phong_HocKy';
GO
UPDATE dbo.LOP_HOC_PHAN SET lifecycle=CASE trang_thai WHEN 'DONG' THEN 'CLOSED' WHEN 'HUY' THEN 'CANCELED' ELSE lifecycle END
 WHERE lifecycle='REGISTRATION' AND trang_thai IN('DONG','HUY');
UPDATE dbo.HOC_KY SET lifecycle='CLOSED' WHERE lifecycle='REGISTRATION' AND trang_thai='DONG';
IF OBJECT_ID('dbo.CAMPUS_HOLIDAY','U') IS NULL
 CREATE TABLE dbo.CAMPUS_HOLIDAY(term varchar(20) NOT NULL REFERENCES dbo.HOC_KY(ma_hk),[date] date NOT NULL,
 active bit NOT NULL,reason nvarchar(500) NOT NULL,actor int NOT NULL,changed_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 version rowversion,PRIMARY KEY(term,[date]));
IF OBJECT_ID('dbo.CAMPUS_SCHEDULE_CHANGE','U') IS NULL
 CREATE TABLE dbo.CAMPUS_SCHEDULE_CHANGE(id bigint IDENTITY PRIMARY KEY,class_id varchar(20) NOT NULL REFERENCES dbo.LOP_HOC_PHAN(ma_lhp),
 kind varchar(12) NOT NULL CHECK(kind IN('CANCEL','REPLACE','MAKEUP')),source_date date NULL,target_date date NULL,source_meeting_id bigint NULL,
 room_id varchar(20) NULL REFERENCES dbo.PHONG_HOC(ma_phong),lecturer_id varchar(20) NULL REFERENCES dbo.GIANG_VIEN(ma_gv),
 start_period tinyint NULL,end_period tinyint NULL,reason nvarchar(500) NOT NULL,actor int NOT NULL,
 changed_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),before_version binary(8) NOT NULL,
 after_version binary(8) NULL,
 CHECK((kind='CANCEL' AND source_date IS NOT NULL AND target_date IS NULL) OR
 (kind IN('MAKEUP','REPLACE') AND target_date IS NOT NULL AND room_id IS NOT NULL AND lecturer_id IS NOT NULL
 AND start_period BETWEEN 1 AND 12 AND end_period BETWEEN start_period AND 12 AND (kind='MAKEUP' OR source_date IS NOT NULL))));
IF COL_LENGTH('dbo.CAMPUS_SCHEDULE_CHANGE','source_meeting_id') IS NULL ALTER TABLE dbo.CAMPUS_SCHEDULE_CHANGE ADD source_meeting_id bigint NULL;
IF COL_LENGTH('dbo.CAMPUS_SCHEDULE_CHANGE','after_version') IS NULL ALTER TABLE dbo.CAMPUS_SCHEDULE_CHANGE ADD after_version binary(8) NULL;
GO
-- Migrate a prior unambiguous date-only revision; fail closed if its source cannot be identified.
UPDATE r SET source_meeting_id=(SELECT MAX(CONVERT(bigint,c.ma_lich)) FROM dbo.LICH_HOC c WHERE c.ma_lhp=r.class_id
 AND r.source_date BETWEEN c.ngay_bd AND c.ngay_kt AND dbo.ufn_Minh_CoNgayHoc(c.thu,r.source_date,r.source_date)=1)
FROM dbo.CAMPUS_SCHEDULE_CHANGE r WHERE r.kind<>'MAKEUP' AND r.source_meeting_id IS NULL
 AND (SELECT COUNT(*) FROM dbo.LICH_HOC c WHERE c.ma_lhp=r.class_id AND r.source_date BETWEEN c.ngay_bd AND c.ngay_kt
 AND dbo.ufn_Minh_CoNgayHoc(c.thu,r.source_date,r.source_date)=1)=1;
IF EXISTS(SELECT 1 FROM dbo.CAMPUS_SCHEDULE_CHANGE WHERE kind<>'MAKEUP' AND source_meeting_id IS NULL)
 THROW 52606,N'Có lịch ngoại lệ cũ không xác định được buổi gốc. Cần đối soát trước nâng cấp.',1;
GO
CREATE OR ALTER FUNCTION dbo.ufn_Campus_ActualMeetings()
RETURNS TABLE AS RETURN
WITH digits(n) AS (SELECT n FROM (VALUES(0),(1),(2),(3),(4),(5),(6),(7),(8),(9))d(n)),
 date_offsets(n) AS (SELECT a.n+b.n*10+c.n*100+d.n*1000 FROM digits a CROSS JOIN digits b CROSS JOIN digits c CROSS JOIN digits d),
 bases AS (
 SELECT l.ma_lhp,l.ma_hk,c.ma_phong,l.ma_gv,DATEADD(day,o.n,c.ngay_bd) source_date,c.tiet_bd,c.tiet_kt,CONVERT(bigint,c.ma_lich) source_meeting_id
 FROM dbo.LOP_HOC_PHAN l JOIN dbo.LICH_HOC c ON c.ma_lhp=l.ma_lhp JOIN date_offsets o ON o.n<=DATEDIFF(day,c.ngay_bd,c.ngay_kt)
 WHERE ((DATEDIFF(day,CONVERT(date,'19000101'),c.ngay_bd)+o.n)%7+7)%7=c.thu-2
 UNION ALL
 SELECT l.ma_lhp,l.ma_hk,r.room_id,r.lecturer_id,r.target_date,r.start_period,r.end_period,-r.id
 FROM dbo.LOP_HOC_PHAN l JOIN dbo.CAMPUS_SCHEDULE_CHANGE r ON r.class_id=l.ma_lhp WHERE r.kind='MAKEUP'),
 actual AS (
 SELECT b.ma_lhp,b.ma_hk,COALESCE(r.room_id,b.ma_phong) ma_phong,COALESCE(r.lecturer_id,b.ma_gv) ma_gv,
 COALESCE(r.target_date,b.source_date) ngay_hoc,COALESCE(r.start_period,b.tiet_bd) tiet_bd,COALESCE(r.end_period,b.tiet_kt) tiet_kt,
 b.source_date,b.source_meeting_id
 FROM bases b OUTER APPLY(SELECT TOP(1) c.* FROM dbo.CAMPUS_SCHEDULE_CHANGE c
 WHERE c.class_id=b.ma_lhp AND c.source_date=b.source_date AND c.source_meeting_id=b.source_meeting_id ORDER BY c.id DESC)r
 WHERE r.kind IS NULL OR r.kind<>'CANCEL')
SELECT a.* FROM actual a WHERE NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_HOLIDAY h WHERE h.term=a.ma_hk AND h.[date]=a.ngay_hoc AND h.active=1);
GO
CREATE OR ALTER FUNCTION dbo.ufn_Campus_LifecycleTransition(@before varchar(20),@after varchar(20))
RETURNS bit AS BEGIN RETURN CASE WHEN
 (@before='REGISTRATION' AND @after IN('CLOSED','CANCELED')) OR
 (@before='CLOSED' AND @after IN('REGISTRATION','TEACHING','CANCELED')) OR
 (@before='TEACHING' AND @after IN('COMPLETED','CANCELED')) OR
 (@before IN('COMPLETED','CANCELED') AND @after='ARCHIVED') THEN 1 ELSE 0 END; END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Lifecycle_Conflicts @class varchar(20)=NULL AS
BEGIN
 SET NOCOUNT ON;
 SELECT a.* INTO #calendar FROM dbo.ufn_Campus_ActualMeetings() a JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=a.ma_lhp
 WHERE l.lifecycle NOT IN('COMPLETED','CANCELED','ARCHIVED') AND l.trang_thai<>'HUY';
 SELECT DISTINCT a.ma_lhp class_id,b.ma_lhp other_class,a.ngay_hoc [date],
 CASE WHEN a.ma_phong=b.ma_phong THEN 'ROOM' WHEN a.ma_gv=b.ma_gv THEN 'LECTURER' ELSE 'STUDENT' END resource,
 s.ma_sv student
 FROM #calendar a JOIN #calendar b ON a.ma_lhp<b.ma_lhp AND a.ngay_hoc=b.ngay_hoc
 AND a.tiet_bd<=b.tiet_kt AND b.tiet_bd<=a.tiet_kt
 OUTER APPLY(SELECT d.ma_sv FROM dbo.DANG_KY d JOIN dbo.DANG_KY e ON e.ma_sv=d.ma_sv AND e.ma_lhp=b.ma_lhp AND e.trang_thai='DANG_KY'
 WHERE d.ma_lhp=a.ma_lhp AND d.trang_thai='DANG_KY')s
 WHERE (@class IS NULL OR a.ma_lhp=@class OR b.ma_lhp=@class)
 AND (a.ma_phong=b.ma_phong OR a.ma_gv=b.ma_gv OR s.ma_sv IS NOT NULL)
 UNION
 SELECT a.ma_lhp,a.ma_lhp,a.ngay_hoc,'SELF',NULL FROM #calendar a JOIN #calendar b
 ON a.ma_lhp=b.ma_lhp AND a.ngay_hoc=b.ngay_hoc AND a.tiet_bd<=b.tiet_kt AND b.tiet_bd<=a.tiet_kt
 WHERE (@class IS NULL OR a.ma_lhp=@class) AND (a.tiet_bd<>b.tiet_bd OR a.tiet_kt<>b.tiet_kt OR a.ma_phong<>b.ma_phong OR a.ma_gv<>b.ma_gv)
 ORDER BY class_id,other_class;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Lifecycle_Classes @term varchar(20)=NULL AS
BEGIN
 SET NOCOUNT ON;EXEC dbo.usp_Minh_KiemTraQuyen;
 SELECT ma_lhp,ma_hp,ma_hk,ma_gv,lifecycle,trang_thai,phien_ban,si_so_hien_tai,si_so_max FROM dbo.LOP_HOC_PHAN WHERE @term IS NULL OR ma_hk=@term ORDER BY ma_lhp;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Lifecycle_Terms AS
BEGIN
 SET NOCOUNT ON;EXEC dbo.usp_Minh_KiemTraQuyen;
 SELECT ma_hk,nam_hoc,hoc_ky,lifecycle,trang_thai,lifecycle_version phien_ban FROM dbo.HOC_KY ORDER BY ma_hk;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Lifecycle_CheckCancellation @class varchar(20) AS
BEGIN
 SET NOCOUNT ON;
 CREATE TABLE #HV_Issues(code varchar(30),message nvarchar(1500),blocking bit);
 CREATE TABLE #DK3_IssueScope(code varchar(30),course varchar(20),class_id varchar(20),rule_code varchar(30));
 CREATE TABLE #DK3_Context(total_credits int);INSERT #DK3_Context VALUES(0);
 CREATE TABLE #DK3_Removed(class_id varchar(20) PRIMARY KEY,course varchar(20));
 DECLARE @student varchar(20),@registration bigint,@term varchar(20)=(SELECT ma_hk FROM dbo.LOP_HOC_PHAN WHERE ma_lhp=@class);
 DECLARE affected CURSOR LOCAL FAST_FORWARD FOR SELECT ma_sv,ma_dk FROM dbo.DANG_KY WHERE ma_lhp=@class AND trang_thai='DANG_KY';
 OPEN affected;FETCH NEXT FROM affected INTO @student,@registration;
 WHILE @@FETCH_STATUS=0 BEGIN
  DELETE #HV_Issues;
  DELETE #DK3_IssueScope;DELETE #DK3_Removed;UPDATE #DK3_Context SET total_credits=0;
  INSERT #DK3_Removed SELECT ma_lhp,ma_hp FROM dbo.LOP_HOC_PHAN WHERE ma_lhp=@class;
  EXEC dbo.usp_DK3_Evaluate @student,N'[]','CANCEL',@registration;
  -- Institutional cancellation keeps prior enrollment authorizations, but cannot apply a new grant.
  DELETE #HV_Issues WHERE code<>'REQUIREMENT';
  DELETE #DK3_IssueScope WHERE code<>'REQUIREMENT';
  EXEC dbo.usp_DK3_FilterIssues @student=@student,@term=@term,@selected_json=N'[]',@consume=0,@retained_only=1;
  IF EXISTS(SELECT 1 FROM #HV_Issues WHERE code='REQUIREMENT' AND blocking=1) THROW 52603,N'Hủy lớp làm thiếu học phần đồng hành của sinh viên. Cần xử lý kế hoạch liên quan trước.',1;
  FETCH NEXT FROM affected INTO @student,@registration;
 END;
 CLOSE affected;DEALLOCATE affected;
END;
GO
DENY EXECUTE ON dbo.usp_Lifecycle_CheckCancellation TO public;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Lifecycle_Transition @id varchar(20),@target varchar(20),@version binary(8),@reason nvarchar(500),@term bit=0 AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @reason IS NULL OR LEN(TRIM(@reason)) NOT BETWEEN 5 AND 500 OR @target IS NULL THROW 52600,N'Cần lý do 5–500 ký tự.',1;
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;EXEC dbo.usp_Campus_RequirePermission 'CLASS_OPERATE';
  DECLARE @before varchar(20),@before_json nvarchar(max),@after_json nvarchar(max),@entity varchar(100);
  IF @term=1 BEGIN
   SELECT @before=lifecycle FROM dbo.HOC_KY WITH(UPDLOCK,HOLDLOCK) WHERE ma_hk=@id AND lifecycle_version=@version;
   IF @before IS NULL THROW 52601,N'Phiên bản học kỳ đã thay đổi.',1;
   IF dbo.ufn_Campus_LifecycleTransition(@before,@target)=0 THROW 52602,N'Chuyển trạng thái không hợp lệ.',1;
   IF @target IN('COMPLETED','CANCELED','ARCHIVED') AND EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_hk=@id AND lifecycle NOT IN('COMPLETED','CANCELED','ARCHIVED'))
    THROW 52603,N'Cần xử lý từng lớp còn vận hành trước khi kết thúc học kỳ.',1;
   IF @target='REGISTRATION' AND @before<>'CLOSED' THROW 52602,N'Không thể mở lại học kỳ lịch sử.',1;
   SET @before_json=(SELECT * FROM dbo.HOC_KY WHERE ma_hk=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
   UPDATE dbo.HOC_KY SET lifecycle=@target,trang_thai=CASE WHEN @target='REGISTRATION' THEN 'DANG_MO' ELSE 'DONG' END WHERE ma_hk=@id;
   SET @after_json=(SELECT * FROM dbo.HOC_KY WHERE ma_hk=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
   SET @entity=CONCAT('TERM:',@id);
  END ELSE BEGIN
   SELECT @before=lifecycle FROM dbo.LOP_HOC_PHAN WITH(UPDLOCK,HOLDLOCK) WHERE ma_lhp=@id AND phien_ban=@version;
   IF @before IS NULL THROW 52601,N'Phiên bản lớp đã thay đổi.',1;
   IF dbo.ufn_Campus_LifecycleTransition(@before,@target)=0 THROW 52602,N'Chuyển trạng thái không hợp lệ.',1;
   IF @target='REGISTRATION' AND EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN l JOIN dbo.HOC_KY h ON h.ma_hk=l.ma_hk WHERE l.ma_lhp=@id AND h.lifecycle<>'REGISTRATION')
    THROW 52602,N'Học kỳ không mở đăng ký.',1;
   IF @target='CANCELED' AND EXISTS(SELECT 1 FROM dbo.DANG_KY d WHERE d.ma_lhp=@id AND d.trang_thai='DANG_KY')
    AND EXISTS(SELECT 1 FROM dbo.DK3_COMBINATION_MEMBER WHERE class_id=@id)
    THROW 52603,N'Lớp thuộc tổ hợp có đăng ký: cần xử lý toàn bộ nhóm trước khi hủy lớp.',1;
   IF @target='CANCELED' EXEC dbo.usp_Lifecycle_CheckCancellation @id;
   SET @before_json=(SELECT * FROM dbo.LOP_HOC_PHAN WHERE ma_lhp=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
   IF @target='CANCELED' BEGIN
    DECLARE @impacted_student varchar(20),@cancel_entity varchar(100)=CONCAT('CLASS:',@id);
    DECLARE canceled_students CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT ma_sv FROM dbo.DANG_KY WHERE ma_lhp=@id AND trang_thai='DANG_KY';
    OPEN canceled_students;FETCH NEXT FROM canceled_students INTO @impacted_student;
    WHILE @@FETCH_STATUS=0 BEGIN EXEC dbo.usp_Campus_Emit 'CLASS_CANCELED',@cancel_entity,@reason,@before_json,NULL,NULL,@impacted_student;FETCH NEXT FROM canceled_students INTO @impacted_student;END;
    CLOSE canceled_students;DEALLOCATE canceled_students;
   END;

   IF @target='CANCELED' UPDATE dbo.DANG_KY SET trang_thai='DA_HUY',ngay_huy=CASE WHEN ngay_dk>DATEADD(hour,7,SYSUTCDATETIME()) THEN ngay_dk ELSE DATEADD(hour,7,SYSUTCDATETIME()) END WHERE ma_lhp=@id AND trang_thai='DANG_KY';
   UPDATE dbo.LOP_HOC_PHAN SET lifecycle=@target,si_so_hien_tai=CASE WHEN @target='CANCELED' THEN 0 ELSE si_so_hien_tai END,
    trang_thai=CASE WHEN @target='CANCELED' OR (@target='ARCHIVED' AND @before='CANCELED') THEN 'HUY'
     WHEN @target='REGISTRATION' THEN CASE WHEN si_so_hien_tai>=si_so_max THEN 'FULL' ELSE 'DANG_MO' END ELSE 'DONG' END WHERE ma_lhp=@id;
   IF @target='REGISTRATION' BEGIN
    DECLARE @conflicts TABLE(class_id varchar(20),other_class varchar(20),[date] date,resource varchar(20),student varchar(20));
    INSERT @conflicts EXEC dbo.usp_Lifecycle_Conflicts @id;
    IF EXISTS(SELECT 1 FROM @conflicts) THROW 52604,N'Lớp xung đột lịch thực tế; chưa thể mở lại.',1;
   END;
   SET @after_json=(SELECT * FROM dbo.LOP_HOC_PHAN WHERE ma_lhp=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
   SET @entity=CONCAT('CLASS:',@id);
  END;
  EXEC dbo.usp_Campus_Emit 'LIFECYCLE_CHANGED',@entity,@reason,@before_json,@after_json;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Lifecycle_ChangeSchedule @class varchar(20),@kind varchar(12),@source date=NULL,@target date=NULL,
 @room varchar(20)=NULL,@lecturer varchar(20)=NULL,@start int=NULL,@end int=NULL,@version binary(8)=NULL,@reason nvarchar(500)=NULL,@preview bit=0,@source_meeting_id bigint=NULL AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @reason IS NULL OR LEN(TRIM(@reason)) NOT BETWEEN 5 AND 500 OR @kind IS NULL OR @kind NOT IN('CANCEL','MAKEUP','REPLACE')
 OR (@kind<>'MAKEUP' AND @source IS NULL) OR (@kind<>'CANCEL' AND (@target IS NULL OR @room IS NULL OR @lecturer IS NULL
 OR @start IS NULL OR @end IS NULL OR @start NOT BETWEEN 1 AND 12 OR @end NOT BETWEEN @start AND 12))
 THROW 52600,N'Thay đổi lịch chưa hợp lệ.',1;
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;EXEC dbo.usp_Campus_RequirePermission 'CLASS_OPERATE';
  DECLARE @term varchar(20),@capacity int,@before_json nvarchar(max),@after_json nvarchar(max),@entity varchar(100)=CONCAT('CLASS:',@class);
  SELECT @term=ma_hk,@capacity=si_so_max FROM dbo.LOP_HOC_PHAN WITH(UPDLOCK,HOLDLOCK) WHERE ma_lhp=@class AND phien_ban=@version AND lifecycle IN('REGISTRATION','CLOSED','TEACHING');
  IF @term IS NULL THROW 52601,N'Lớp đã đổi phiên bản hoặc đã kết thúc.',1;
  DECLARE @source_actual_date date;
  IF @kind<>'MAKEUP' BEGIN
   DECLARE @candidates TABLE(source_meeting_id bigint,source_date date,ngay_hoc date);
   INSERT @candidates SELECT a.source_meeting_id,a.source_date,a.ngay_hoc FROM dbo.ufn_Campus_ActualMeetings() a
    WHERE a.ma_lhp=@class AND (a.source_date=@source OR (@source_meeting_id IS NULL AND a.ngay_hoc=@source))
    AND (@source_meeting_id IS NULL OR a.source_meeting_id=@source_meeting_id);
   IF (SELECT COUNT(*) FROM @candidates)<>1 THROW 52605,N'Cần chọn đúng một buổi học thực tế và định danh buổi gốc.',1;
   SELECT @source_meeting_id=source_meeting_id,@source=source_date,@source_actual_date=ngay_hoc FROM @candidates;
  END;
  IF @source_actual_date<CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())) OR @target<CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())) THROW 52605,N'Không sửa lại buổi học thực tế đã qua.',1;
  IF @kind<>'CANCEL' AND (NOT EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@term AND @target BETWEEN ngay_hoc_bd AND ngay_hoc_kt)
   OR NOT EXISTS(SELECT 1 FROM dbo.PHONG_HOC WHERE ma_phong=@room AND trang_thai=1 AND suc_chua>=@capacity)
   OR NOT EXISTS(SELECT 1 FROM dbo.GIANG_VIEN WHERE ma_gv=@lecturer AND trang_thai=1)
   OR EXISTS(SELECT 1 FROM dbo.CAMPUS_HOLIDAY WHERE term=@term AND [date]=@target AND active=1))
   THROW 52605,N'Buổi mới phải trong kỳ, không là ngày nghỉ, phòng đủ chỗ và giảng viên đang dạy.',1;
  SET @before_json=(SELECT * FROM dbo.CAMPUS_SCHEDULE_CHANGE WHERE class_id=@class ORDER BY id FOR JSON PATH);
  INSERT dbo.CAMPUS_SCHEDULE_CHANGE(class_id,kind,source_date,target_date,room_id,lecturer_id,start_period,end_period,reason,actor,before_version,source_meeting_id)
   VALUES(@class,@kind,CASE WHEN @kind='MAKEUP' THEN NULL ELSE @source END,CASE WHEN @kind='CANCEL' THEN NULL ELSE @target END,
    CASE WHEN @kind='CANCEL' THEN NULL ELSE @room END,CASE WHEN @kind='CANCEL' THEN NULL ELSE @lecturer END,
    CASE WHEN @kind='CANCEL' THEN NULL ELSE @start END,CASE WHEN @kind='CANCEL' THEN NULL ELSE @end END,@reason,dbo.ufn_Campus_AccountId(),@version,CASE WHEN @kind='MAKEUP' THEN NULL ELSE @source_meeting_id END);
  DECLARE @change_id bigint=SCOPE_IDENTITY();
  DECLARE @conflicts TABLE(class_id varchar(20),other_class varchar(20),[date] date,resource varchar(20),student varchar(20));
  INSERT @conflicts EXEC dbo.usp_Lifecycle_Conflicts @class;
  IF EXISTS(SELECT 1 FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp=@class GROUP BY ngay_hoc,tiet_bd,tiet_kt,ma_phong,ma_gv HAVING COUNT(*)>1)
   INSERT @conflicts VALUES(@class,@class,COALESCE(@target,@source),'SELF',NULL);
  IF @preview=1 BEGIN SELECT * FROM @conflicts ORDER BY class_id,student;ROLLBACK;RETURN;END;
  IF EXISTS(SELECT 1 FROM @conflicts) THROW 52604,N'Đổi lịch gây trùng phòng, giảng viên hoặc sinh viên bị ảnh hưởng. Chưa lưu thay đổi.',1;
  UPDATE dbo.LOP_HOC_PHAN SET si_so_max=si_so_max WHERE ma_lhp=@class;
  UPDATE c SET after_version=l.phien_ban FROM dbo.CAMPUS_SCHEDULE_CHANGE c JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=c.class_id WHERE c.id=@change_id;
  SET @after_json=(SELECT * FROM dbo.CAMPUS_SCHEDULE_CHANGE WHERE class_id=@class ORDER BY id FOR JSON PATH);
  EXEC dbo.usp_Campus_Emit 'SCHEDULE_CHANGED',@entity,@reason,@before_json,@after_json;
  DECLARE @student varchar(20);DECLARE impacted CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT ma_sv FROM dbo.DANG_KY WHERE ma_lhp=@class AND trang_thai='DANG_KY';
  OPEN impacted;FETCH NEXT FROM impacted INTO @student;
  WHILE @@FETCH_STATUS=0 BEGIN EXEC dbo.usp_Campus_Emit 'SCHEDULE_STUDENT',@entity,@reason,NULL,@after_json,NULL,@student;FETCH NEXT FROM impacted INTO @student;END;
  CLOSE impacted;DEALLOCATE impacted;COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Lifecycle_Holiday @term varchar(20),@date date,@active bit,@reason nvarchar(500),@version binary(8) AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @reason IS NULL OR LEN(TRIM(@reason)) NOT BETWEEN 5 AND 500 OR @active IS NULL OR @date IS NULL THROW 52600,N'Ngày nghỉ/lý do không hợp lệ.',1;
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;EXEC dbo.usp_Campus_RequirePermission 'CLASS_OPERATE';
  IF NOT EXISTS(SELECT 1 FROM dbo.HOC_KY WITH(UPDLOCK,HOLDLOCK) WHERE ma_hk=@term AND lifecycle_version=@version AND lifecycle IN('REGISTRATION','CLOSED','TEACHING') AND @date BETWEEN ngay_hoc_bd AND ngay_hoc_kt)
   THROW 52601,N'Học kỳ đã thay đổi/kết thúc hoặc ngày không thuộc kỳ.',1;
  IF @date<CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())) THROW 52605,N'Không sửa ngày học lịch sử.',1;
  DECLARE @before nvarchar(max)=(SELECT * FROM dbo.CAMPUS_HOLIDAY WHERE term=@term AND [date]=@date FOR JSON PATH),@after nvarchar(max),@entity varchar(100)=CONCAT('TERM:',@term);
  UPDATE dbo.CAMPUS_HOLIDAY SET active=@active,reason=@reason,actor=dbo.ufn_Campus_AccountId(),changed_at=SYSUTCDATETIME() WHERE term=@term AND [date]=@date;
  IF @@ROWCOUNT=0 INSERT dbo.CAMPUS_HOLIDAY(term,[date],active,reason,actor) VALUES(@term,@date,@active,@reason,dbo.ufn_Campus_AccountId());
  DECLARE @conflicts TABLE(class_id varchar(20),other_class varchar(20),[date] date,resource varchar(20),student varchar(20));
  INSERT @conflicts EXEC dbo.usp_Lifecycle_Conflicts;
  IF @active=0 AND EXISTS(SELECT 1 FROM @conflicts WHERE [date]=@date) THROW 52604,N'Bỏ ngày nghỉ làm xuất hiện xung đột lịch.',1;
  UPDATE dbo.HOC_KY SET lifecycle=lifecycle WHERE ma_hk=@term;
  UPDATE dbo.LOP_HOC_PHAN SET si_so_max=si_so_max WHERE ma_hk=@term;
  SET @after=(SELECT * FROM dbo.CAMPUS_HOLIDAY WHERE term=@term AND [date]=@date FOR JSON PATH);
  EXEC dbo.usp_Campus_Emit 'HOLIDAY_CHANGED',@entity,@reason,@before,@after;
  DECLARE @student varchar(20);DECLARE holiday_students CURSOR LOCAL FAST_FORWARD FOR
   SELECT DISTINCT d.ma_sv FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp
   WHERE d.trang_thai='DANG_KY' AND l.ma_hk=@term AND l.lifecycle NOT IN('COMPLETED','CANCELED','ARCHIVED');
  OPEN holiday_students;FETCH NEXT FROM holiday_students INTO @student;
  WHILE @@FETCH_STATUS=0 BEGIN EXEC dbo.usp_Campus_Emit 'HOLIDAY_STUDENT',@entity,@reason,@before,@after,NULL,@student;FETCH NEXT FROM holiday_students INTO @student;END;
  CLOSE holiday_students;DEALLOCATE holiday_students;

  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
GRANT EXECUTE ON dbo.usp_Lifecycle_Classes TO app_minh;
GRANT EXECUTE ON dbo.usp_Lifecycle_Terms TO app_minh;
GRANT EXECUTE ON dbo.usp_Lifecycle_Transition TO app_minh;
GRANT EXECUTE ON dbo.usp_Lifecycle_ChangeSchedule TO app_minh;
GRANT EXECUTE ON dbo.usp_Lifecycle_Holiday TO app_minh;
GO
-- Upgrade read contracts in place without changing resultset order or recurring history.
DECLARE @proc sysname,@sql nvarchar(max),@json nvarchar(max)=N'l.lifecycle,(SELECT CONVERT(varchar(10),a.ngay_hoc,23) [Date],a.ma_phong RoomId,a.ma_gv LecturerId,a.tiet_bd StartPeriod,a.tiet_kt EndPeriod FROM dbo.ufn_Campus_ActualMeetings() a WHERE a.ma_lhp=v.ma_lhp ORDER BY a.ngay_hoc,a.tiet_bd FOR JSON PATH) actual_meetings_json';
DECLARE read_contracts CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.procedures WHERE name IN('usp_Minh_DocDuLieu','usp_SinhVien_ThoiKhoaBieu');
OPEN read_contracts;FETCH NEXT FROM read_contracts INTO @proc;
WHILE @@FETCH_STATUS=0 BEGIN
 SET @sql=OBJECT_DEFINITION(OBJECT_ID('dbo.'+@proc));
 IF CHARINDEX('actual_meetings_json',@sql)=0 BEGIN
  IF @proc='usp_Minh_DocDuLieu' SET @sql=REPLACE(@sql,N'v.trang_thai,l.phien_ban',N'v.trang_thai,l.phien_ban,'+@json);
  ELSE BEGIN
   SET @sql=REPLACE(@sql,N'v.trang_thai',N'v.trang_thai,'+@json);
   SET @sql=REPLACE(@sql,N'FROM dbo.v_ThongTinLopHocPhan v JOIN @own',N'FROM dbo.v_ThongTinLopHocPhan v JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=v.ma_lhp JOIN @own');
   SET @sql=REPLACE(@sql,N'WHERE l.ma_gv=g.ma_gv)',N'WHERE l.ma_gv=g.ma_gv)');
   SET @sql=REPLACE(@sql,N'WHERE l.ma_gv=g.ma_gv) ORDER BY',N'WHERE l.ma_gv=g.ma_gv) ORDER BY');
   SET @sql=REPLACE(@sql,N'WHERE l.ma_gv=g.ma_gv)',N'WHERE l.ma_gv=g.ma_gv)');
   SET @sql=REPLACE(@sql,N'WHERE l.ma_gv=g.ma_gv) ORDER BY g.ma_gv;',N'WHERE l.ma_gv=g.ma_gv) OR EXISTS(SELECT 1 FROM dbo.ufn_Campus_ActualMeetings() a JOIN @own o ON o.ma_lhp=a.ma_lhp WHERE a.ma_gv=g.ma_gv) ORDER BY g.ma_gv;');
   SET @sql=REPLACE(@sql,N'WHERE c.ma_phong=p.ma_phong) ORDER BY p.ma_phong;',N'WHERE c.ma_phong=p.ma_phong) OR EXISTS(SELECT 1 FROM dbo.ufn_Campus_ActualMeetings() a JOIN @own o ON o.ma_lhp=a.ma_lhp WHERE a.ma_phong=p.ma_phong) ORDER BY p.ma_phong;');
  END;
  IF CHARINDEX('actual_meetings_json',@sql)=0 THROW 52606,N'Không nhận diện snapshot lịch.',1;
 END;
 IF CHARINDEX('COALESCE((SELECT CONVERT(varchar(10),a.ngay_hoc',@sql)=0 BEGIN
 SET @sql=REPLACE(@sql,N'l.lifecycle,(SELECT CONVERT(varchar(10),a.ngay_hoc',N'l.lifecycle,COALESCE((SELECT CONVERT(varchar(10),a.ngay_hoc');
 SET @sql=REPLACE(@sql,N'FOR JSON PATH) actual_meetings_json',N'FOR JSON PATH),N''[]'') actual_meetings_json');
 END;
 IF CHARINDEX('l.lifecycle NOT IN',@sql)=0 SET @sql=REPLACE(@sql,N'l.trang_thai<>''HUY''',N'l.trang_thai<>''HUY'' AND l.lifecycle NOT IN(''COMPLETED'',''CANCELED'',''ARCHIVED'')');
 SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
 FETCH NEXT FROM read_contracts INTO @proc;
END;CLOSE read_contracts;DEALLOCATE read_contracts;
GO
-- Both academic evaluators must evaluate the same actual dates shown to students.
DECLARE @proc sysname,@sql nvarchar(max),@start int,@finish int;
DECLARE evaluators CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.procedures WHERE name IN('usp_HocVu_Evaluate','usp_DK3_Evaluate');
OPEN evaluators;FETCH NEXT FROM evaluators INTO @proc;
WHILE @@FETCH_STATUS=0 BEGIN
 SET @sql=OBJECT_DEFINITION(OBJECT_ID('dbo.'+@proc));
 SET @start=CHARINDEX(N'IF EXISTS(SELECT 1 FROM @proposal p WHERE p.proposed=1 AND NOT EXISTS(SELECT 1 FROM dbo.LICH_HOC',@sql);
 IF @start>0 BEGIN
  SET @finish=CHARINDEX(N'INSERT #HV_Issues VALUES(''SCHEDULE''',@sql,@start);
  SET @finish=CHARINDEX(N';',@sql,@finish)+1;
  IF @finish<=@start THROW 52606,N'Không nhận diện kiểm tra lịch học vụ.',1;
  SET @sql=STUFF(@sql,@start,@finish-@start,N'IF EXISTS(SELECT 1 FROM @proposal p WHERE p.proposed=1 AND NOT EXISTS(SELECT 1 FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp=p.class))
 INSERT #HV_Issues VALUES(''NO_SCHEDULE'',N''Có lớp chưa có ngày học thực tế.'',1);
 IF EXISTS(SELECT 1 FROM @proposal p JOIN @proposal q ON p.class<q.class AND (p.proposed=1 OR q.proposed=1)
 JOIN dbo.ufn_Campus_ActualMeetings() a ON a.ma_lhp=p.class JOIN dbo.ufn_Campus_ActualMeetings() b ON b.ma_lhp=q.class
 JOIN dbo.LOP_HOC_PHAN la ON la.ma_lhp=a.ma_lhp JOIN dbo.LOP_HOC_PHAN lb ON lb.ma_lhp=b.ma_lhp
 WHERE la.lifecycle NOT IN(''COMPLETED'',''CANCELED'',''ARCHIVED'') AND lb.lifecycle NOT IN(''COMPLETED'',''CANCELED'',''ARCHIVED'')
 AND a.ngay_hoc=b.ngay_hoc AND a.tiet_bd<=b.tiet_kt AND b.tiet_bd<=a.tiet_kt)
 INSERT #HV_Issues VALUES(''SCHEDULE'',N''Các lớp bị trùng ngày học thực tế.'',1);');
 END;
 SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
 FETCH NEXT FROM evaluators INTO @proc;
END;CLOSE evaluators;DEALLOCATE evaluators;
GO
-- Guard old class editing; preserve immutable base schedule/history.
DECLARE @sql nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_Minh_LuuLop')),@start int,@finish int;
IF CHARINDEX('Lifecycle edit guard',@sql)=0 BEGIN
 SET @sql=REPLACE(@sql,N'BEGIN TRAN; EXEC dbo.usp_Minh_KhoaLapLich;',N'BEGIN TRAN; EXEC dbo.usp_HocVu_Lock; EXEC dbo.usp_Minh_KhoaLapLich; EXEC dbo.usp_Minh_KiemTraQuyen;
 -- Lifecycle edit guard
 IF EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_lhp=@ma_lhp AND lifecycle NOT IN(''REGISTRATION'',''CLOSED'')) THROW 52602,N''Dùng quy trình vận hành cho lớp đang học hoặc lịch sử.'',1;
 IF @la_moi=0 AND EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_lhp=@ma_lhp AND CASE WHEN trang_thai=''FULL'' THEN ''DANG_MO'' ELSE trang_thai END<>@trang_thai) EXEC dbo.usp_Campus_RequirePermission ''CLASS_OPERATE'';
 IF EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@ma_hk AND lifecycle IN(''COMPLETED'',''CANCELED'',''ARCHIVED'')) THROW 52602,N''Học kỳ đã kết thúc.'',1;');
 SET @start=CHARINDEX(N'IF @trang_thai<>''HUY'' AND EXISTS(',@sql);
 SET @finish=CHARINDEX(N'DECLARE @state varchar(12)',@sql,@start);
 IF @start=0 OR @finish<=@start THROW 52606,N'Không nhận diện kiểm tra lịch lớp.',1;
 SET @sql=STUFF(@sql,@start,@finish-@start,N'IF @trang_thai<>''HUY'' AND EXISTS(SELECT 1 FROM @lich a JOIN dbo.ufn_Campus_ActualMeetings() b
 ON b.ngay_hoc BETWEEN a.ngay_bd AND a.ngay_kt AND dbo.ufn_Minh_CoNgayHoc(a.thu,b.ngay_hoc,b.ngay_hoc)=1
 AND a.tiet_bd<=b.tiet_kt AND b.tiet_bd<=a.tiet_kt JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=b.ma_lhp
 WHERE l.ma_lhp<>@ma_lhp AND l.trang_thai<>''HUY'' AND l.lifecycle NOT IN(''COMPLETED'',''CANCELED'',''ARCHIVED'')
 AND (a.ma_phong=b.ma_phong OR b.ma_gv=@ma_gv)
 AND NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_HOLIDAY h WHERE h.term=@ma_hk AND h.[date]=b.ngay_hoc AND h.active=1))
 THROW 52044,N''Trùng phòng hoặc giảng viên vào ngày học thực tế.'',1;
 ');
 SET @sql=REPLACE(@sql,N'SELECT @count AS si_so_hien_tai',N'UPDATE dbo.LOP_HOC_PHAN SET lifecycle=CASE @state WHEN ''DANG_MO'' THEN ''REGISTRATION'' WHEN ''FULL'' THEN ''REGISTRATION'' WHEN ''DONG'' THEN ''CLOSED'' WHEN ''HUY'' THEN ''CANCELED'' END WHERE ma_lhp=@ma_lhp;
 SELECT @count AS si_so_hien_tai');
 SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
END;
GO
-- Resource maintenance uses actual meetings and ignores completed classes.
DECLARE @sql nvarchar(max),@start int,@finish int;
SET @sql=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_Minh_LuuPhong'));
IF CHARINDEX('Lifecycle resources',@sql)=0 BEGIN
 SET @start=CHARINDEX(N'IF EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN l WHERE l.trang_thai',@sql);
 SET @finish=CHARINDEX(N'IF @la_moi=1 INSERT dbo.PHONG_HOC',@sql,@start);
 IF @start=0 OR @finish<=@start THROW 52606,N'Không nhận diện kiểm tra phòng.',1;
 SET @sql=STUFF(@sql,@start,@finish-@start,N'-- Lifecycle resources
 IF EXISTS(SELECT 1 FROM dbo.ufn_Campus_ActualMeetings() a JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=a.ma_lhp
 WHERE a.ma_phong=@ma_phong AND l.lifecycle NOT IN(''COMPLETED'',''CANCELED'',''ARCHIVED'') AND l.trang_thai<>''HUY''
 AND (@trang_thai=0 OR l.si_so_max>@suc_chua)) THROW 52023,N''Phòng còn lớp sử dụng hoặc chưa đủ chỗ.'',1;
 ');
 SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
END;
SET @sql=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_Minh_LuuGiangVien'));
SET @sql=REPLACE(@sql,N'EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_gv=@ma_gv AND trang_thai<>''HUY'')',N'EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN l WHERE l.lifecycle NOT IN(''COMPLETED'',''CANCELED'',''ARCHIVED'') AND l.trang_thai<>''HUY'' AND (l.ma_gv=@ma_gv OR EXISTS(SELECT 1 FROM dbo.ufn_Campus_ActualMeetings() a WHERE a.ma_lhp=l.ma_lhp AND a.ma_gv=@ma_gv)))');
SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Lifecycle_Calendar @class varchar(20) AS
BEGIN
 SET NOCOUNT ON;EXEC dbo.usp_Minh_KiemTraQuyen;
 SELECT ma_lhp,ngay_hoc,ma_phong,ma_gv,tiet_bd,tiet_kt,source_date,source_meeting_id FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp=@class ORDER BY ngay_hoc,tiet_bd;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Lifecycle_History @term varchar(20) AS
BEGIN
 SET NOCOUNT ON;EXEC dbo.usp_Minh_KiemTraQuyen;
 SELECT CONVERT(varchar(20),c.id) id,c.class_id,c.kind,c.source_date,c.target_date,c.room_id,c.lecturer_id,c.start_period,c.end_period,c.reason,c.actor,c.changed_at
 FROM dbo.CAMPUS_SCHEDULE_CHANGE c JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=c.class_id WHERE l.ma_hk=@term
 UNION ALL
 SELECT 'HOLIDAY','',CASE active WHEN 1 THEN 'HOLIDAY' ELSE 'RESTORE' END,[date],[date],NULL,NULL,NULL,NULL,reason,actor,changed_at
 FROM dbo.CAMPUS_HOLIDAY WHERE term=@term ORDER BY changed_at DESC;
END;
GO
GRANT EXECUTE ON dbo.usp_Lifecycle_Calendar TO app_minh;
GRANT EXECUTE ON dbo.usp_Lifecycle_History TO app_minh;
GO
-- Legacy semester editors cannot revive a completed semester or bypass operation permission.
DECLARE @name sysname,@sql nvarchar(max);
DECLARE term_writes CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.procedures WHERE name IN('usp_Phong_HocKy_Luu','usp_Phong_HocKy_DoiTrangThai');
OPEN term_writes;FETCH NEXT FROM term_writes INTO @name;
WHILE @@FETCH_STATUS=0 BEGIN
 SET @sql=OBJECT_DEFINITION(OBJECT_ID('dbo.'+@name));
 IF CHARINDEX('Lifecycle term guard',@sql)=0 BEGIN
  SET @sql=REPLACE(@sql,N'BEGIN TRAN;',N'BEGIN TRAN; EXEC dbo.usp_HocVu_Lock; EXEC dbo.usp_Minh_KhoaLapLich; EXEC dbo.usp_Minh_KiemTraQuyen;
 -- Lifecycle term guard
 IF EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@ma_hk AND lifecycle NOT IN(''REGISTRATION'',''CLOSED'')) THROW 52602,N''Dùng quy trình vận hành học kỳ.'',1;');
  IF @name='usp_Phong_HocKy_DoiTrangThai' BEGIN
   SET @sql=REPLACE(@sql,N'UPDATE dbo.HOC_KY SET trang_thai=@trang_thai',N'EXEC dbo.usp_Campus_RequirePermission ''CLASS_OPERATE''; UPDATE dbo.HOC_KY SET lifecycle=CASE @trang_thai WHEN ''DANG_MO'' THEN ''REGISTRATION'' ELSE ''CLOSED'' END,trang_thai=@trang_thai');
  END ELSE BEGIN
   SET @sql=REPLACE(@sql,N'IF @la_moi=0 AND NOT EXISTS',N'IF @la_moi=0 AND EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@ma_hk AND (ngay_hoc_bd<>@ngay_hoc_bd OR ngay_hoc_kt<>@ngay_hoc_kt))
    AND (EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN l JOIN dbo.DANG_KY d ON d.ma_lhp=l.ma_lhp WHERE l.ma_hk=@ma_hk)
      OR EXISTS(SELECT 1 FROM dbo.CAMPUS_HOLIDAY WHERE term=@ma_hk)
      OR EXISTS(SELECT 1 FROM dbo.CAMPUS_SCHEDULE_CHANGE c JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=c.class_id WHERE l.ma_hk=@ma_hk))
    THROW 52605,N''Không đổi cửa sổ học của học kỳ đã có lịch sử hoặc lịch ngoại lệ.'',1;
   IF @la_moi=0 AND NOT EXISTS');
  END;
  SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
 END;
 FETCH NEXT FROM term_writes INTO @name;
END;CLOSE term_writes;DEALLOCATE term_writes;
GO
-- Final proposal guard includes actual dates of existing registrations across overlapping terms.
DECLARE @sql nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_DK3_ValidateProposal')),@position int;
IF @sql IS NOT NULL AND CHARINDEX('Lifecycle actual proposal',@sql)=0 BEGIN
 SET @position=LEN(@sql)-CHARINDEX(REVERSE('END;'),REVERSE(@sql))-LEN('END;')+2;
 IF @position<1 THROW 52606,N'Không nhận diện kiểm tra tổ hợp.',1;
 SET @sql=STUFF(@sql,@position,0,N'
 -- Lifecycle actual proposal
 IF EXISTS(SELECT 1 FROM OPENJSON(@selected_json) j JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=j.value JOIN dbo.HOC_KY h ON h.ma_hk=l.ma_hk
 WHERE l.lifecycle<>''REGISTRATION'' OR h.lifecycle<>''REGISTRATION'')
 INSERT #HV_Issues VALUES(''CLASS_CLOSED'',N''Vòng đời lớp hoặc học kỳ không nhận đăng ký.'',1);
 IF EXISTS(SELECT 1 FROM OPENJSON(@selected_json) j WHERE NOT EXISTS(SELECT 1 FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp=j.value))
 INSERT #HV_Issues VALUES(''NO_SCHEDULE'',N''Lớp chưa có ngày học thực tế.'',1);
 DECLARE @calendar_classes TABLE(id varchar(20) PRIMARY KEY);
 INSERT @calendar_classes SELECT class_id FROM @final UNION SELECT d.ma_lhp FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp
 WHERE d.ma_sv=@student AND d.trang_thai=''DANG_KY'' AND l.ma_hk<>@term;
 IF EXISTS(SELECT 1 FROM @calendar_classes p JOIN @calendar_classes q ON p.id<q.id
 JOIN dbo.ufn_Campus_ActualMeetings() a ON a.ma_lhp=p.id JOIN dbo.ufn_Campus_ActualMeetings() b ON b.ma_lhp=q.id
 JOIN dbo.LOP_HOC_PHAN la ON la.ma_lhp=a.ma_lhp JOIN dbo.LOP_HOC_PHAN lb ON lb.ma_lhp=b.ma_lhp
 WHERE la.lifecycle NOT IN(''COMPLETED'',''CANCELED'',''ARCHIVED'') AND lb.lifecycle NOT IN(''COMPLETED'',''CANCELED'',''ARCHIVED'')
 AND a.ngay_hoc=b.ngay_hoc AND a.tiet_bd<=b.tiet_kt AND b.tiet_bd<=a.tiet_kt)
 INSERT #HV_Issues VALUES(''SCHEDULE'',N''Trùng ngày học thực tế với lớp trong đề xuất cuối hoặc kỳ khác.'',1);
 ');
 SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
END;
GO
-- The internal legacy core must not reintroduce recurring-calendar false conflicts.
DECLARE @sql nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_HocVu_RegisterCore')),@start int,@finish int;
IF @sql IS NOT NULL AND CHARINDEX('Lifecycle core calendar',@sql)=0 BEGIN
 SET @sql=REPLACE(@sql,N'FROM LICH_HOC WITH(HOLDLOCK) WHERE ma_lhp=@ma_lhp',N'FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp=@ma_lhp');
 SET @start=CHARINDEX(N'IF EXISTS(',@sql,CHARINDEX(N'-- Kiểm tra thứ',@sql));
 SET @finish=CHARINDEX(N') THROW 51016',@sql,@start);
 IF @start=0 OR @finish<=@start THROW 52606,N'Không nhận diện kiểm tra lịch core cũ.',1;
 SET @sql=STUFF(@sql,@start,@finish-@start+1,N'-- Lifecycle core calendar
 IF EXISTS(SELECT 1 FROM dbo.ufn_Campus_ActualMeetings() a JOIN dbo.ufn_Campus_ActualMeetings() b ON a.ngay_hoc=b.ngay_hoc
 AND a.tiet_bd<=b.tiet_kt AND b.tiet_bd<=a.tiet_kt
 JOIN dbo.DANG_KY d ON d.ma_lhp=b.ma_lhp AND d.ma_sv=@sv AND d.trang_thai=''DANG_KY''
 JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=b.ma_lhp WHERE a.ma_lhp=@ma_lhp AND l.lifecycle NOT IN(''COMPLETED'',''CANCELED'',''ARCHIVED''))');
 SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
END;
GO
-- Freeze the recurring editor after the first operational revision. The operational API owns later changes.
DECLARE @sql nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_Minh_LuuLop'));
IF CHARINDEX('Operational revision guard',@sql)=0 BEGIN
 SET @sql=REPLACE(@sql,N'-- Lifecycle edit guard',N'-- Lifecycle edit guard
 -- Operational revision guard
 IF EXISTS(SELECT 1 FROM dbo.CAMPUS_SCHEDULE_CHANGE WHERE class_id=@ma_lhp) THROW 52602,N''Lớp đã có lịch ngoại lệ; dùng quy trình vận hành.'',1;');
 SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
END;
GO
-- Return the actual-date metadata of the committed base edit, never the caller's old projection.
DECLARE @sql nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_Minh_LuuLop'));
IF CHARINDEX('actual_meetings_json',@sql)=0 BEGIN
 SET @sql=REPLACE(@sql,N'SELECT @count AS si_so_hien_tai,phien_ban,',N'SELECT @count AS si_so_hien_tai,phien_ban,lifecycle,
 COALESCE((SELECT CONVERT(varchar(10),a.ngay_hoc,23) [Date],a.ma_phong RoomId,a.ma_gv LecturerId,a.tiet_bd StartPeriod,a.tiet_kt EndPeriod
 FROM dbo.ufn_Campus_ActualMeetings() a WHERE a.ma_lhp=@ma_lhp ORDER BY a.ngay_hoc,a.tiet_bd FOR JSON PATH),N''[]'') actual_meetings_json,');
 IF CHARINDEX('actual_meetings_json',@sql)=0 THROW 52606,N'Không nhận diện kết quả lưu lớp.',1;
 SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
END;
GO
-- Editing an existing registration window is an operation too, even when its status is unchanged.
DECLARE @sql nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_Phong_HocKy_Luu'));
IF CHARINDEX('Lifecycle window permission',@sql)=0 BEGIN
 SET @sql=REPLACE(@sql,N'-- Lifecycle term guard',N'-- Lifecycle term guard
 -- Lifecycle window permission
 IF @la_moi=0 AND EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@ma_hk AND
 (tg_bat_dau<>@tg_bat_dau OR tg_ket_thuc<>@tg_ket_thuc OR han_huy_dk<>@han_huy_dk OR ngay_hoc_bd<>@ngay_hoc_bd OR ngay_hoc_kt<>@ngay_hoc_kt))
 EXEC dbo.usp_Campus_RequirePermission ''CLASS_OPERATE'';');
 SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
END;
GO
