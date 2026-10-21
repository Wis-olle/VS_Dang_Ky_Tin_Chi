-- Phase 4.3: real curriculum projection, versioned student plans and scoped advising.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('dbo.AP4_PLAN','U') IS NULL
 CREATE TABLE dbo.AP4_PLAN(student varchar(20) PRIMARY KEY REFERENCES dbo.SINH_VIEN(ma_sv),program_id varchar(20) NOT NULL,
 program_version binary(8) NOT NULL,source_hash binary(32) NOT NULL,items_json nvarchar(max) NOT NULL CHECK(ISJSON(items_json)=1),
 status varchar(12) NOT NULL CHECK(status IN('DRAFT','SUBMITTED','APPROVED','RETURNED')),version rowversion,
 changed_utc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),approved_hash binary(32) NULL);
IF OBJECT_ID('dbo.AP4_ADVISOR','U') IS NULL
 CREATE TABLE dbo.AP4_ADVISOR(student varchar(20) NOT NULL REFERENCES dbo.SINH_VIEN(ma_sv),account_id int NOT NULL REFERENCES dbo.TAI_KHOAN(ma_tk),
 active bit NOT NULL,PRIMARY KEY(student,account_id));
IF OBJECT_ID('dbo.AP4_COMMENT','U') IS NULL
 CREATE TABLE dbo.AP4_COMMENT(id bigint IDENTITY PRIMARY KEY,student varchar(20) NOT NULL REFERENCES dbo.AP4_PLAN(student),
 plan_version binary(8) NOT NULL,actor int NOT NULL,action varchar(12) NOT NULL CHECK(action IN('COMMENT','APPROVE','RETURN')),
 content nvarchar(500) NOT NULL,created_utc datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
IF OBJECT_ID('dbo.AP4_OFFERING','U') IS NULL
 CREATE TABLE dbo.AP4_OFFERING(course varchar(20) NOT NULL REFERENCES dbo.HOC_PHAN(ma_hp),term varchar(20) NOT NULL REFERENCES dbo.HOC_KY(ma_hk),
 source nvarchar(500) NOT NULL,version rowversion,PRIMARY KEY(course,term));
IF OBJECT_ID('dbo.AP4_SUBSTITUTION','U') IS NULL
 CREATE TABLE dbo.AP4_SUBSTITUTION(program_id varchar(20) NOT NULL,program_version binary(8) NOT NULL,
 source_course varchar(20) NOT NULL REFERENCES dbo.HOC_PHAN(ma_hp),target_course varchar(20) NOT NULL REFERENCES dbo.HOC_PHAN(ma_hp),
 source nvarchar(500) NOT NULL,version rowversion,PRIMARY KEY(program_id,program_version,source_course,target_course),CHECK(source_course<>target_course));
GO
CREATE OR ALTER FUNCTION dbo.ufn_AP4_Matches(@program varchar(20),@source varchar(20),@target varchar(20)) RETURNS bit AS
BEGIN
 RETURN CASE WHEN @source=@target OR EXISTS(SELECT 1 FROM dbo.v_HV_Courses a JOIN dbo.v_HV_Courses b ON a.program=b.program AND a.equivalence=b.equivalence
 WHERE a.program=@program AND a.course=@source AND b.course=@target AND a.equivalence IS NOT NULL)
 OR EXISTS(SELECT 1 FROM dbo.AP4_SUBSTITUTION s JOIN dbo.HV_DOCUMENT p ON p.kind='PROGRAM' AND p.id=s.program_id AND p.version=s.program_version
 WHERE s.program_id=@program AND s.source_course=@source AND s.target_course=@target) THEN 1 ELSE 0 END;
END;
GO
CREATE OR ALTER FUNCTION dbo.ufn_AP4_SourceHash(@student varchar(20),@program varchar(20)) RETURNS binary(32) AS
BEGIN
 DECLARE @docs nvarchar(max),@registrations nvarchar(max),@terms nvarchar(max),@offerings nvarchar(max),@subs nvarchar(max),@courses nvarchar(max),@profile nvarchar(200);
 SELECT @docs=STRING_AGG(CONVERT(nvarchar(max),CONCAT(kind,':',id,':',CONVERT(varchar(16),CONVERT(binary(8),version),2))),N'|') WITHIN GROUP(ORDER BY kind,id)
 FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND id=@program OR JSON_VALUE(payload,'$.program')=@program
 OR kind='ASSIGNMENT' AND JSON_VALUE(payload,'$.student')=@student
 OR kind='RESULT' AND JSON_VALUE(payload,'$.student')=@student AND TRY_CONVERT(date,JSON_VALUE(payload,'$.publication'))<=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME()));
 SELECT @profile=CONCAT(s.ma_nganh,':',s.ma_lop,':',l.khoa_hoc) FROM dbo.SINH_VIEN s LEFT JOIN dbo.LOP l ON l.ma_lop=s.ma_lop WHERE s.ma_sv=@student;
 SELECT @registrations=STRING_AGG(CONVERT(nvarchar(max),CONCAT(d.ma_dk,':',d.ma_lhp,':',d.trang_thai,':',l.ma_hp,':',l.ma_hk)),N'|') WITHIN GROUP(ORDER BY d.ma_dk)
 FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp WHERE d.ma_sv=@student;
 SELECT @terms=STRING_AGG(CONVERT(nvarchar(max),CONCAT(ma_hk,':',CONVERT(varchar(10),ngay_hoc_bd,23),':',CONVERT(varchar(10),ngay_hoc_kt,23))),N'|') WITHIN GROUP(ORDER BY ma_hk) FROM dbo.HOC_KY;
 SELECT @offerings=STRING_AGG(CONVERT(nvarchar(max),CONCAT(course,':',term,':',CONVERT(varchar(16),CONVERT(binary(8),version),2))),N'|') WITHIN GROUP(ORDER BY course,term) FROM dbo.AP4_OFFERING;
 SELECT @subs=STRING_AGG(CONVERT(nvarchar(max),CONCAT(source_course,':',target_course,':',CONVERT(varchar(16),CONVERT(binary(8),version),2))),N'|') WITHIN GROUP(ORDER BY source_course,target_course) FROM dbo.AP4_SUBSTITUTION WHERE program_id=@program;
 SELECT @courses=STRING_AGG(CONVERT(nvarchar(max),CONCAT(ma_hp,':',so_tin_chi,':',trang_thai)),N'|') WITHIN GROUP(ORDER BY ma_hp) FROM dbo.HOC_PHAN;
 RETURN HASHBYTES('SHA2_256',CONCAT(@student,N'#',@program,N'#',@docs,N'#',@registrations,N'#',@terms,N'#',@offerings,N'#',@subs,N'#',@courses,N'#',@profile));
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AP4_Authorize @student varchar(20) OUTPUT,@write bit=0 AS
BEGIN
 SET NOCOUNT ON;DECLARE @owner varchar(20),@role varchar(20);EXEC dbo.usp_XacThucTaiKhoan @owner OUTPUT,@role OUTPUT;
 IF @role='SINH_VIEN' BEGIN
  IF @student IS NOT NULL AND @student<>@owner THROW 53103,N'Chỉ xem và sửa kế hoạch của chính mình.',1;
  SET @student=@owner;
 END ELSE BEGIN
  IF @write=1 THROW 53103,N'Kế hoạch do sinh viên sở hữu; cố vấn chỉ góp ý hoặc duyệt.',1;
  EXEC dbo.usp_Campus_RequirePermission 'ADVISE_PLAN';
  IF @student IS NULL SELECT TOP(1) @student=student FROM dbo.AP4_ADVISOR WHERE account_id=dbo.ufn_Campus_AccountId() AND active=1 ORDER BY student;
  IF @student IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.AP4_ADVISOR WHERE student=@student AND account_id=dbo.ufn_Campus_AccountId() AND active=1)
   THROW 53103,N'Chưa được phân công cố vấn cho sinh viên này.',1;
 END;
 IF NOT EXISTS(SELECT 1 FROM dbo.SINH_VIEN WHERE ma_sv=@student AND trang_thai=1) THROW 53103,N'Hồ sơ sinh viên không còn hoạt động.',1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AP4_ValidateItems @student varchar(20),@program varchar(20),@items_json nvarchar(max),@strict bit=0 AS
BEGIN
 SET NOCOUNT ON;
 IF @items_json IS NULL OR ISJSON(@items_json)<>1 OR LEFT(LTRIM(@items_json),1)<>'[' OR DATALENGTH(@items_json)>64000
 OR (SELECT COUNT(*) FROM OPENJSON(@items_json))>120 OR EXISTS(SELECT 1 FROM OPENJSON(@items_json) WHERE type<>5)
 THROW 53100,N'Kế hoạch cần tối đa 120 môn và học kỳ hợp lệ.',1;
 DECLARE @items TABLE(course varchar(20) PRIMARY KEY,term varchar(20));
 IF EXISTS(SELECT 1 FROM OPENJSON(@items_json) item CROSS APPLY OPENJSON(item.value) field GROUP BY item.[key]
 HAVING COUNT(*)<>2 OR COUNT(DISTINCT field.[key] COLLATE Latin1_General_100_BIN2)<>2
 OR SUM(CASE WHEN field.[key] COLLATE Latin1_General_100_BIN2 IN('course','term') AND field.type=1 THEN 1 ELSE 0 END)<>2)
 THROW 53100,N'Mỗi dòng chỉ có đúng hai trường course và term, không lặp hoặc đổi chữ hoa/thường.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(@items_json) WITH(course nvarchar(max),term nvarchar(max)) WHERE course IS NULL OR term IS NULL
 OR LEN(course) NOT BETWEEN 1 AND 20 OR LEN(term) NOT BETWEEN 1 AND 20 OR course COLLATE Latin1_General_100_BIN2 LIKE '%[^-A-Z0-9_]%' OR term COLLATE Latin1_General_100_BIN2 LIKE '%[^-A-Z0-9_]%')
 OR (SELECT COUNT(*) FROM OPENJSON(@items_json))<>(SELECT COUNT(DISTINCT course) FROM OPENJSON(@items_json) WITH(course nvarchar(max)))
 THROW 53100,N'Mã môn/kỳ không hợp lệ hoặc môn bị lặp ở nhiều kỳ.',1;
 INSERT @items SELECT course,term FROM OPENJSON(@items_json) WITH(course varchar(20),term varchar(20));
 IF NOT EXISTS(SELECT 1 FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND id=@program AND published=1) THROW 53104,N'Cần CTĐT chính thức đã công bố.',1;
 IF EXISTS(SELECT 1 FROM @items i LEFT JOIN dbo.HOC_PHAN h ON h.ma_hp=i.course LEFT JOIN dbo.HOC_KY t ON t.ma_hk=i.term
 WHERE h.ma_hp IS NULL OR h.trang_thai=0 OR t.ma_hk IS NULL OR t.ngay_hoc_bd IS NULL OR t.ngay_hoc_kt IS NULL
 OR t.ngay_hoc_kt<CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME()))
 OR NOT EXISTS(SELECT 1 FROM dbo.v_HV_Courses c WHERE c.program=@program AND dbo.ufn_AP4_Matches(@program,i.course,c.course)=1))
 THROW 53100,N'Môn phải thuộc hoặc được phép thay thế trong CTĐT; kỳ phải còn thời gian học.',1;
 IF @strict=0 RETURN;
 IF EXISTS(SELECT 1 FROM @items i WHERE NOT EXISTS(SELECT 1 FROM dbo.AP4_OFFERING o WHERE o.course=i.course AND o.term=i.term))
 THROW 53106,N'Chưa có căn cứ dự kiến mở môn cho một học kỳ trong kế hoạch.',1;
 DECLARE @rules TABLE(course varchar(20),[group] int,satisfied bit);
 INSERT @rules SELECT i.course,r.[group],CASE WHEN EXISTS(
  SELECT 1 FROM dbo.v_HV_Results done JOIN dbo.HOC_KY old ON old.ma_hk=done.term
  WHERE done.student=@student AND dbo.ufn_AP4_Matches(@program,done.course,r.requiredCourse)=1 AND done.publication<=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME()))
  AND ((done.recognition IN('TRANSFER','EXEMPT') AND done.passed=1 AND r.minGrade=0) OR done.recognition='GRADED' AND done.grade>=r.minGrade AND (r.kind='PRIOR' OR done.passed=1))
  AND (r.kind='CO' OR old.ngay_hoc_bd<target.ngay_hoc_bd))
 OR EXISTS(SELECT 1 FROM @items required_item JOIN dbo.HOC_KY required_term ON required_term.ma_hk=required_item.term
 WHERE dbo.ufn_AP4_Matches(@program,required_item.course,r.requiredCourse)=1
 AND (r.kind='CO' AND required_term.ngay_hoc_bd<=target.ngay_hoc_bd OR r.kind<>'CO' AND required_term.ngay_hoc_kt<target.ngay_hoc_bd))
 OR EXISTS(SELECT 1 FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp JOIN dbo.HOC_KY current_term ON current_term.ma_hk=l.ma_hk
 WHERE d.ma_sv=@student AND d.trang_thai='DANG_KY' AND l.trang_thai<>'HUY' AND dbo.ufn_AP4_Matches(@program,l.ma_hp,r.requiredCourse)=1
 AND (r.kind='CO' AND current_term.ngay_hoc_bd<=target.ngay_hoc_bd OR r.kind<>'CO' AND current_term.ngay_hoc_kt<target.ngay_hoc_bd)) THEN 1 ELSE 0 END
 FROM @items i JOIN dbo.HOC_KY target ON target.ma_hk=i.term JOIN dbo.v_HV_Requirements r ON r.program=@program AND r.course=i.course;
 IF EXISTS(SELECT 1 FROM @rules GROUP BY course,[group] HAVING MAX(CONVERT(int,satisfied))=0)
 THROW 53106,N'Chuỗi điều kiện chưa đủ: PRE/PRIOR phải ở kỳ trước, CO phải học cùng hoặc trước.',1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AP4_Snapshot @student varchar(20)=NULL,@what_if_program varchar(20)=NULL AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;EXEC dbo.usp_AP4_Authorize @student OUTPUT;
 DECLARE @assigned varchar(20)=(SELECT program FROM dbo.v_HV_Assignments WHERE student=@student),@program varchar(20),@version binary(8),@hash binary(32);
 SET @program=COALESCE(@what_if_program,@assigned);
 IF @what_if_program IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND id=@what_if_program AND published=1) THROW 53104,N'Chỉ mô phỏng CTĐT đã công bố.',1;
 SELECT @version=version FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND id=@program AND published=1;
 SET @hash=dbo.ufn_AP4_SourceHash(@student,@program);
 SELECT @student student,s.ho_ten student_name,@assigned assigned_program,@program selected_program,@version program_version,
 p.requiredCredits required_credits,CONVERT(bit,CASE WHEN @version IS NULL OR @what_if_program IS NULL AND NOT EXISTS(
 SELECT 1 FROM dbo.v_HV_Programs official JOIN dbo.SINH_VIEN current_student ON current_student.ma_sv=@student
 JOIN dbo.LOP current_class ON current_class.ma_lop=current_student.ma_lop
 WHERE official.id=@assigned AND official.published=1 AND official.major=current_student.ma_nganh AND official.cohort=current_class.khoa_hoc)
 THEN 1 ELSE 0 END) missing_program,
 CONVERT(bit,CASE WHEN @what_if_program IS NULL THEN 0 ELSE 1 END) is_what_if,@hash source_hash FROM dbo.SINH_VIEN s
 LEFT JOIN dbo.v_HV_Programs p ON p.id=@program AND p.published=1 WHERE s.ma_sv=@student;
 SELECT id,name,requiredCredits FROM dbo.v_HV_Groups WHERE program=@program AND @version IS NOT NULL ORDER BY id;
 SELECT c.id,c.course,COALESCE(c.name,h.ten_hp) name,COALESCE(c.credits,h.so_tin_chi) credits,c.[group],c.required,c.equivalence FROM dbo.v_HV_Courses c JOIN dbo.HOC_PHAN h ON h.ma_hp=c.course WHERE c.program=@program AND @version IS NOT NULL ORDER BY c.id;
 SELECT course,[group],requiredCourse,kind,minGrade FROM dbo.v_HV_Requirements WHERE program=@program AND @version IS NOT NULL ORDER BY course,[group],id;
 SELECT r.course,COALESCE(r.credits,h.so_tin_chi) credits,'DONE' stage,r.grade,r.passed,r.term FROM dbo.v_HV_Results r JOIN dbo.HOC_PHAN h ON h.ma_hp=r.course WHERE r.student=@student AND r.publication<=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME()))
 UNION ALL SELECT DISTINCT l.ma_hp,h.so_tin_chi,'IN_PROGRESS',NULL,CONVERT(bit,1),l.ma_hk FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp JOIN dbo.HOC_PHAN h ON h.ma_hp=l.ma_hp WHERE d.ma_sv=@student AND d.trang_thai='DANG_KY' AND l.trang_thai<>'HUY';
 SELECT source_course,target_course,source,version FROM dbo.AP4_SUBSTITUTION WHERE program_id=@program AND program_version=@version;
 SELECT o.course,o.term,o.source,o.version FROM dbo.AP4_OFFERING o ORDER BY o.term,o.course;
 SELECT ma_hk,CONCAT(nam_hoc,N' • Học kỳ ',hoc_ky) name,ngay_hoc_bd start_date,ngay_hoc_kt end_date FROM dbo.HOC_KY ORDER BY ngay_hoc_bd,ma_hk;
 SELECT *,CONVERT(bit,CASE WHEN source_hash=@hash AND program_id=@assigned AND program_version=@version THEN 1 ELSE 0 END) source_current,
 CONVERT(bit,CASE WHEN status='APPROVED' AND approved_hash=@hash AND program_id=@assigned AND program_version=@version THEN 1 ELSE 0 END) approval_current
 FROM dbo.AP4_PLAN WHERE student=@student;
 SELECT id,plan_version,actor,action,content,created_utc FROM dbo.AP4_COMMENT WHERE student=@student ORDER BY id DESC;
 SELECT id,name,version FROM dbo.v_HV_Programs WHERE published=1 ORDER BY name,id;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AP4_SavePlan @program varchar(20),@source_hash binary(32),@items_json nvarchar(max),@version binary(8)=NULL AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;
 DECLARE @student varchar(20),@program_version binary(8),@before nvarchar(max),@after nvarchar(max),@entity varchar(100);
 EXEC dbo.usp_AP4_Authorize @student OUTPUT,1;
 IF NOT EXISTS(SELECT 1 FROM dbo.v_HV_Assignments WHERE student=@student AND program=@program) THROW 53104,N'Chỉ lưu kế hoạch theo CTĐT đang được gắn; mô phỏng không thay hồ sơ.',1;
 SELECT @program_version=version FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND id=@program AND published=1;
 IF @program_version IS NULL THROW 53104,N'Cần CTĐT chính thức đã công bố trước khi lưu kế hoạch.',1;
 IF @source_hash IS NULL OR @source_hash<>dbo.ufn_AP4_SourceHash(@student,@program) THROW 53101,N'Dữ liệu học vụ đã đổi. Tải lại để đối soát trước khi lưu.',1;
 EXEC dbo.usp_AP4_ValidateItems @student,@program,@items_json,0;
 SET @before=(SELECT student,program_id,program_version,status,items_json,version FROM dbo.AP4_PLAN WHERE student=@student FOR JSON PATH);
 IF EXISTS(SELECT 1 FROM dbo.AP4_PLAN WITH(UPDLOCK,HOLDLOCK) WHERE student=@student) BEGIN
  UPDATE dbo.AP4_PLAN SET program_id=@program,program_version=@program_version,source_hash=@source_hash,items_json=@items_json,status='DRAFT',approved_hash=NULL,changed_utc=SYSUTCDATETIME()
  WHERE student=@student AND version=@version;
  IF @@ROWCOUNT<>1 THROW 53101,N'Kế hoạch đã được sửa ở phiên khác. Hãy tải lại.',1;
 END ELSE BEGIN
  IF @version IS NOT NULL THROW 53101,N'Kế hoạch đã thay đổi. Hãy tải lại.',1;
  INSERT dbo.AP4_PLAN(student,program_id,program_version,source_hash,items_json,status) VALUES(@student,@program,@program_version,@source_hash,@items_json,'DRAFT');
 END;
 SET @after=(SELECT student,program_id,program_version,status,items_json,version FROM dbo.AP4_PLAN WHERE student=@student FOR JSON PATH);
 SET @entity=CONCAT('PLAN:',@student);EXEC dbo.usp_Campus_Emit 'PLAN_SAVED',@entity,N'Sinh viên lưu kế hoạch nhiều học kỳ.',@before,@after,NULL,@student;
 COMMIT;
 SELECT student,status,version,source_hash FROM dbo.AP4_PLAN WHERE student=@student;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AP4_Submit @student varchar(20),@version binary(8),@source_hash binary(32) AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;EXEC dbo.usp_AP4_Authorize @student OUTPUT,1;
 DECLARE @program varchar(20),@items nvarchar(max),@entity varchar(100)=CONCAT('PLAN:',@student),@before nvarchar(max),@after nvarchar(max),@stored_hash binary(32);
 SELECT @program=program_id,@items=items_json,@stored_hash=source_hash FROM dbo.AP4_PLAN WITH(UPDLOCK,HOLDLOCK) WHERE student=@student AND version=@version AND status IN('DRAFT','RETURNED');
 IF @program IS NULL OR @source_hash IS NULL OR @source_hash<>@stored_hash OR @source_hash<>dbo.ufn_AP4_SourceHash(@student,@program)
 OR NOT EXISTS(SELECT 1 FROM dbo.v_HV_Assignments WHERE student=@student AND program=@program) THROW 53101,N'Kế hoạch hoặc dữ liệu học vụ đã đổi. Tải lại và lưu nháp trước.',1;
 EXEC dbo.usp_AP4_ValidateItems @student,@program,@items,1;
 SET @before=(SELECT status,version FROM dbo.AP4_PLAN WHERE student=@student FOR JSON PATH);
 UPDATE dbo.AP4_PLAN SET status='SUBMITTED',changed_utc=SYSUTCDATETIME() WHERE student=@student;
 SET @after=(SELECT status,version FROM dbo.AP4_PLAN WHERE student=@student FOR JSON PATH);
 EXEC dbo.usp_Campus_Emit 'PLAN_SUBMITTED',@entity,N'Sinh viên gửi kế hoạch để cố vấn xem xét.',@before,@after,NULL,@student;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AP4_Advise @student varchar(20),@version binary(8),@action varchar(12),@comment nvarchar(500) AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @action IS NULL OR @action NOT IN('COMMENT','APPROVE','RETURN') OR @comment IS NULL OR LEN(TRIM(@comment)) NOT BETWEEN 5 AND 500 THROW 53100,N'Cần nội dung góp ý/quyết định 5–500 ký tự.',1;
 BEGIN TRY
 BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;EXEC dbo.usp_AP4_Authorize @student OUTPUT;
 EXEC dbo.usp_Campus_RequirePermission 'ADVISE_PLAN';
 IF NOT EXISTS(SELECT 1 FROM dbo.AP4_ADVISOR WHERE student=@student AND account_id=dbo.ufn_Campus_AccountId() AND active=1) THROW 53103,N'Bạn không được phân công cố vấn sinh viên này.',1;
 DECLARE @program varchar(20),@items nvarchar(max),@hash binary(32),@status varchar(12),@entity varchar(100)=CONCAT('PLAN:',@student),@before nvarchar(max),@after nvarchar(max);
 SELECT @program=program_id,@items=items_json,@hash=source_hash,@status=status FROM dbo.AP4_PLAN WITH(UPDLOCK,HOLDLOCK) WHERE student=@student AND version=@version;
 IF @program IS NULL THROW 53101,N'Kế hoạch đã thay đổi. Hãy tải lại trước khi góp ý.',1;
 IF @action IN('APPROVE','RETURN') AND @status<>'SUBMITTED' THROW 53102,N'Chỉ xử lý kế hoạch đang chờ cố vấn.',1;
 IF @action='APPROVE' BEGIN
  IF @hash<>dbo.ufn_AP4_SourceHash(@student,@program) OR NOT EXISTS(SELECT 1 FROM dbo.v_HV_Assignments WHERE student=@student AND program=@program)
   THROW 53101,N'Dữ liệu học vụ đã đổi. Cần sinh viên cập nhật kế hoạch trước khi duyệt.',1;
  EXEC dbo.usp_AP4_ValidateItems @student,@program,@items,1;
 END;
 SET @before=(SELECT status,version FROM dbo.AP4_PLAN WHERE student=@student FOR JSON PATH);
 INSERT dbo.AP4_COMMENT(student,plan_version,actor,action,content) VALUES(@student,@version,dbo.ufn_Campus_AccountId(),@action,@comment);
 IF @action<>'COMMENT' UPDATE dbo.AP4_PLAN SET status=CASE @action WHEN 'APPROVE' THEN 'APPROVED' ELSE 'RETURNED' END,
 approved_hash=CASE WHEN @action='APPROVE' THEN @hash ELSE NULL END,changed_utc=SYSUTCDATETIME() WHERE student=@student;
 SET @after=(SELECT status,version,@action action,@comment comment FROM dbo.AP4_PLAN WHERE student=@student FOR JSON PATH);
 EXEC dbo.usp_Campus_Emit 'PLAN_ADVISED',@entity,@comment,@before,@after,NULL,@student;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AP4_AssignAdvisor @student varchar(20),@account_id int,@active bit,@reason nvarchar(500) AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;
 DECLARE @owner varchar(20),@role varchar(20);EXEC dbo.usp_XacThucTaiKhoan @owner OUTPUT,@role OUTPUT;
 IF @role<>'QUAN_TRI' THROW 53103,N'Chỉ quản trị được phân công cố vấn.',1;
 IF @active IS NULL OR @reason IS NULL OR LEN(TRIM(@reason)) NOT BETWEEN 5 AND 500
 OR NOT EXISTS(SELECT 1 FROM dbo.SINH_VIEN WHERE ma_sv=@student AND trang_thai=1)
 OR NOT EXISTS(SELECT 1 FROM dbo.TAI_KHOAN WHERE ma_tk=@account_id AND vai_tro='DAO_TAO' AND trang_thai=1) THROW 53100,N'Sinh viên/cố vấn hoặc lý do phân công chưa hợp lệ.',1;
 UPDATE dbo.AP4_ADVISOR SET active=@active WHERE student=@student AND account_id=@account_id;
 IF @@ROWCOUNT=0 INSERT dbo.AP4_ADVISOR(student,account_id,active) VALUES(@student,@account_id,@active);
 DECLARE @entity varchar(100)=CONCAT('ADVISOR:',@student),@after nvarchar(max)=(SELECT @student student,@account_id account_id,@active active FOR JSON PATH);
 EXEC dbo.usp_Campus_Emit 'ADVISOR_ASSIGNED',@entity,@reason,NULL,@after;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AP4_SaveOffering @course varchar(20),@term varchar(20),@source nvarchar(500),@version binary(8)=NULL AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;EXEC dbo.usp_Campus_RequirePermission 'CLASS_OPERATE';
 IF @source IS NULL OR LEN(TRIM(@source)) NOT BETWEEN 5 AND 500 OR NOT EXISTS(SELECT 1 FROM dbo.HOC_PHAN WHERE ma_hp=@course AND trang_thai=1)
 OR NOT EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@term AND ngay_hoc_bd IS NOT NULL AND ngay_hoc_kt>=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME()))) THROW 53100,N'Cần môn, kỳ dự kiến và căn cứ có thật.',1;
 IF EXISTS(SELECT 1 FROM dbo.AP4_OFFERING WHERE course=@course AND term=@term) BEGIN
  UPDATE dbo.AP4_OFFERING SET source=@source WHERE course=@course AND term=@term AND version=@version;
  IF @@ROWCOUNT<>1 THROW 53101,N'Dự kiến mở môn đã thay đổi. Hãy tải lại.',1;
 END ELSE BEGIN
  IF @version IS NOT NULL THROW 53101,N'Dự kiến mở môn đã thay đổi.',1;
  INSERT dbo.AP4_OFFERING(course,term,source) VALUES(@course,@term,@source);
 END;
 DECLARE @entity varchar(100)=CONCAT('OFFERING:',@course,':',@term),@after nvarchar(max)=(SELECT course,term,source,version FROM dbo.AP4_OFFERING WHERE course=@course AND term=@term FOR JSON PATH);
 EXEC dbo.usp_Campus_Emit 'OFFERING_PUBLISHED',@entity,@source,NULL,@after;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AP4_SaveSubstitution @program varchar(20),@source_course varchar(20),@target_course varchar(20),@source nvarchar(500),@version binary(8)=NULL AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;
 DECLARE @owner varchar(20),@role varchar(20),@pv binary(8);EXEC dbo.usp_XacThucTaiKhoan @owner OUTPUT,@role OUTPUT;
 IF @role<>'QUAN_TRI' THROW 53103,N'Chỉ quản trị được xác nhận thay thế học phần.',1;
 SELECT @pv=version FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND id=@program AND published=1;
 IF @pv IS NULL OR @source IS NULL OR LEN(TRIM(@source)) NOT BETWEEN 5 AND 500 OR @source_course=@target_course
 OR NOT EXISTS(SELECT 1 FROM dbo.HOC_PHAN WHERE ma_hp=@source_course AND trang_thai=1)
 OR NOT EXISTS(SELECT 1 FROM dbo.v_HV_Courses WHERE program=@program AND course=@target_course) THROW 53100,N'Phải có CTĐT đã công bố, môn thay thế và căn cứ xác nhận.',1;
 IF EXISTS(SELECT 1 FROM dbo.AP4_SUBSTITUTION WHERE program_id=@program AND program_version=@pv AND source_course=@source_course AND target_course=@target_course) BEGIN
  UPDATE dbo.AP4_SUBSTITUTION SET source=@source WHERE program_id=@program AND program_version=@pv AND source_course=@source_course AND target_course=@target_course AND version=@version;
  IF @@ROWCOUNT<>1 THROW 53101,N'Quyết định thay thế đã thay đổi. Hãy tải lại.',1;
 END ELSE BEGIN
  IF @version IS NOT NULL THROW 53101,N'Quyết định thay thế đã thay đổi.',1;
  INSERT dbo.AP4_SUBSTITUTION(program_id,program_version,source_course,target_course,source) VALUES(@program,@pv,@source_course,@target_course,@source);
 END;
 DECLARE @entity varchar(100)=CONCAT('SUBSTITUTION:',@program),@after nvarchar(max)=(SELECT @source_course source_course,@target_course target_course,@source source FOR JSON PATH);
 EXEC dbo.usp_Campus_Emit 'SUBSTITUTION_CONFIRMED',@entity,@source,NULL,@after;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_AP4_Directory AS
BEGIN
 SET NOCOUNT ON;DECLARE @student varchar(20),@role varchar(20);EXEC dbo.usp_XacThucTaiKhoan @student OUTPUT,@role OUTPUT;
 IF @role='SINH_VIEN' SELECT ma_sv,ho_ten FROM dbo.SINH_VIEN WHERE ma_sv=@student;
 ELSE IF @role='QUAN_TRI' SELECT ma_sv,ho_ten FROM dbo.SINH_VIEN WHERE trang_thai=1 ORDER BY ma_sv;
 ELSE BEGIN EXEC dbo.usp_Campus_RequirePermission 'ADVISE_PLAN'; SELECT s.ma_sv,s.ho_ten FROM dbo.SINH_VIEN s JOIN dbo.AP4_ADVISOR a ON a.student=s.ma_sv WHERE a.account_id=dbo.ufn_Campus_AccountId() AND a.active=1 AND s.trang_thai=1 ORDER BY s.ma_sv;END;
 SELECT ma_tk,ten_dang_nhap FROM dbo.TAI_KHOAN WHERE @role='QUAN_TRI' AND vai_tro='DAO_TAO' AND trang_thai=1 ORDER BY ten_dang_nhap;
 SELECT student,account_id,active FROM dbo.AP4_ADVISOR WHERE @role='QUAN_TRI' OR student=@student OR account_id=dbo.ufn_Campus_AccountId();
END;
GO
DENY EXECUTE ON dbo.usp_AP4_Authorize TO public;
DENY EXECUTE ON dbo.usp_AP4_ValidateItems TO public;
GRANT EXECUTE ON dbo.usp_AP4_Snapshot TO app_minh;
GRANT EXECUTE ON dbo.usp_AP4_SavePlan TO app_minh;
GRANT EXECUTE ON dbo.usp_AP4_Submit TO app_minh;
GRANT EXECUTE ON dbo.usp_AP4_Advise TO app_minh;
GRANT EXECUTE ON dbo.usp_AP4_AssignAdvisor TO app_minh;
GRANT EXECUTE ON dbo.usp_AP4_SaveOffering TO app_minh;
GRANT EXECUTE ON dbo.usp_AP4_SaveSubstitution TO app_minh;
GRANT EXECUTE ON dbo.usp_AP4_Directory TO app_minh;
GO
-- Additional public catalog rows carry actual credit values for planned/substitute courses.
DECLARE @sql nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_AP4_Snapshot'));
IF CHARINDEX('AP4_CourseChoices',@sql)=0 BEGIN
 SET @sql=REPLACE(@sql,N' COMMIT;',N' -- AP4_CourseChoices
 SELECT ma_hp course,ten_hp name,so_tin_chi credits,trang_thai active FROM dbo.HOC_PHAN ORDER BY ma_hp;
 COMMIT;');
 SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
END;
GO
CREATE OR ALTER FUNCTION dbo.ufn_AP4_RuleSatisfied(@student varchar(20),@program varchar(20),@target_term varchar(20),@required varchar(20),@kind varchar(8),@minimum decimal(4,2),@items nvarchar(max)) RETURNS bit AS
BEGIN
 DECLARE @start date=(SELECT ngay_hoc_bd FROM dbo.HOC_KY WHERE ma_hk=@target_term);
 RETURN CASE WHEN EXISTS(SELECT 1 FROM dbo.v_HV_Results r JOIN dbo.HOC_KY old ON old.ma_hk=r.term
 WHERE r.student=@student AND dbo.ufn_AP4_Matches(@program,r.course,@required)=1 AND r.publication<=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME()))
 AND (r.recognition IN('TRANSFER','EXEMPT') AND r.passed=1 AND @minimum=0 OR r.recognition='GRADED' AND r.grade>=@minimum AND (@kind='PRIOR' OR r.passed=1))
 AND (@kind='CO' OR old.ngay_hoc_bd<@start))
 OR EXISTS(SELECT 1 FROM OPENJSON(@items) WITH(course varchar(20),term varchar(20)) i JOIN dbo.HOC_KY prior ON prior.ma_hk=i.term
 WHERE dbo.ufn_AP4_Matches(@program,i.course,@required)=1 AND (@kind='CO' AND prior.ngay_hoc_bd<=@start OR @kind<>'CO' AND prior.ngay_hoc_kt<@start))
 OR EXISTS(SELECT 1 FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp JOIN dbo.HOC_KY prior ON prior.ma_hk=l.ma_hk
 WHERE d.ma_sv=@student AND d.trang_thai='DANG_KY' AND l.trang_thai<>'HUY' AND dbo.ufn_AP4_Matches(@program,l.ma_hp,@required)=1
 AND (@kind='CO' AND prior.ngay_hoc_bd<=@start OR @kind<>'CO' AND prior.ngay_hoc_kt<@start)) THEN 1 ELSE 0 END;
END;
GO
CREATE OR ALTER VIEW dbo.v_AP4_Demand AS
 SELECT i.course,i.term,COUNT(DISTINCT p.student) planned_students FROM dbo.AP4_PLAN p
 CROSS APPLY OPENJSON(p.items_json) WITH(course varchar(20),term varchar(20))i
 WHERE EXISTS(SELECT 1 FROM dbo.v_HV_Assignments a WHERE a.student=p.student AND a.program=p.program_id)
 GROUP BY i.course,i.term;
GO
CREATE OR ALTER VIEW dbo.v_AP4_PlanRisks AS
 SELECT p.student,i.course,i.term,CONVERT(nvarchar(500),N'Chưa có căn cứ dự kiến mở môn trong kỳ đã chọn.') reason
 FROM dbo.AP4_PLAN p CROSS APPLY OPENJSON(p.items_json) WITH(course varchar(20),term varchar(20))i
 WHERE NOT EXISTS(SELECT 1 FROM dbo.AP4_OFFERING o WHERE o.course=i.course AND o.term=i.term)
 UNION ALL
 SELECT p.student,i.course,i.term,CONVERT(nvarchar(500),N'Dữ liệu học vụ/CTĐT đã đổi; cần đối soát kế hoạch.')
 FROM dbo.AP4_PLAN p CROSS APPLY OPENJSON(p.items_json) WITH(course varchar(20),term varchar(20))i
 WHERE p.source_hash<>dbo.ufn_AP4_SourceHash(p.student,p.program_id)
 OR NOT EXISTS(SELECT 1 FROM dbo.v_HV_Assignments a WHERE a.student=p.student AND a.program=p.program_id)
 UNION ALL
 SELECT p.student,i.course,i.term,CONVERT(nvarchar(500),CONCAT(N'Thiếu nhóm điều kiện ',r.[group],N' trong chuỗi kế hoạch.'))
 FROM dbo.AP4_PLAN p CROSS APPLY OPENJSON(p.items_json) WITH(course varchar(20),term varchar(20))i
 JOIN dbo.v_HV_Requirements r ON r.program=p.program_id AND r.course=i.course
 GROUP BY p.student,p.program_id,p.items_json,i.course,i.term,r.[group]
 HAVING MAX(CONVERT(int,dbo.ufn_AP4_RuleSatisfied(p.student,p.program_id,i.term,r.requiredCourse,r.kind,r.minGrade,p.items_json)))=0;
GO

CREATE OR ALTER PROCEDURE dbo.usp_AP4_EnsureCurrentProgram @student varchar(20),@program varchar(20) AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.v_HV_Assignments a JOIN dbo.SINH_VIEN s ON s.ma_sv=a.student JOIN dbo.LOP l ON l.ma_lop=s.ma_lop
 JOIN dbo.v_HV_Programs p ON p.id=a.program AND p.published=1 AND p.major=s.ma_nganh AND p.cohort=l.khoa_hoc
 WHERE a.student=@student AND a.program=@program) THROW 53104,N'CTĐT hiện gắn không khớp ngành/khóa hoặc chưa công bố. Cần cập nhật từ hồ sơ chính thức.',1;
END;
GO
DECLARE @name sysname,@sql nvarchar(max);
DECLARE current_program_guards CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM sys.procedures WHERE name IN('usp_AP4_SavePlan','usp_AP4_Submit');
OPEN current_program_guards;FETCH NEXT FROM current_program_guards INTO @name;
WHILE @@FETCH_STATUS=0 BEGIN
 SET @sql=OBJECT_DEFINITION(OBJECT_ID('dbo.'+@name));
 SET @sql=REPLACE(@sql,N' EXEC dbo.usp_AP4_ValidateItems @student,@program',N' EXEC dbo.usp_AP4_EnsureCurrentProgram @student,@program; EXEC dbo.usp_AP4_ValidateItems @student,@program');
 SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
 FETCH NEXT FROM current_program_guards INTO @name;
END;CLOSE current_program_guards;DEALLOCATE current_program_guards;
SET @sql=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_AP4_Advise'));
SET @sql=REPLACE(@sql,N'  EXEC dbo.usp_AP4_ValidateItems @student,@program',N'  EXEC dbo.usp_AP4_EnsureCurrentProgram @student,@program; EXEC dbo.usp_AP4_ValidateItems @student,@program');
SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
GO
DENY EXECUTE ON dbo.usp_AP4_EnsureCurrentProgram TO public;
GO
