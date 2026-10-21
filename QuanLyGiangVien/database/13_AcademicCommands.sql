SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
CREATE OR ALTER PROCEDURE dbo.usp_HocVu_Validate @kind varchar(20),@id varchar(20)
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @p nvarchar(max)=(SELECT payload FROM dbo.HV_DOCUMENT WHERE kind=@kind AND id=@id);
 DECLARE @program varchar(20)=JSON_VALUE(@p,'$.program');
 IF EXISTS(SELECT 1 FROM OPENJSON(@p) WHERE [key] IN('program','course','requiredCourse','student','term','major','equivalence') AND value IS NOT NULL AND value<>''
  AND (LEN(value)>20 OR value COLLATE Latin1_General_100_BIN2 LIKE '%[^-A-Z0-9_]%'))
  THROW 52602,N'Mã tham chiếu học vụ phải là mã ASCII tối đa 20 ký tự.',1;
 IF @kind='PROGRAM' AND NOT EXISTS(SELECT 1 FROM dbo.v_HV_Programs p JOIN dbo.NGANH n ON n.ma_nganh=p.major
  WHERE p.id=@id AND LEN(LTRIM(RTRIM(p.name))) BETWEEN 1 AND 150 AND p.cohort BETWEEN 1900 AND 2200 AND p.requiredCredits BETWEEN 1 AND 1000)
  THROW 52602,N'CTĐT cần tên, ngành, khóa và tín chỉ tốt nghiệp hợp lệ.',1;
 IF @kind IN('GROUP','COURSE','REQUIREMENT') AND NOT EXISTS(SELECT 1 FROM dbo.v_HV_Programs WHERE id=@program AND published=0)
  THROW 52603,N'CTĐT đã công bố là bất biến. Hãy tạo phiên bản mới để sửa.',1;
 IF @kind='GROUP' AND NOT EXISTS(SELECT 1 FROM dbo.v_HV_Groups WHERE id=@id AND LEN(LTRIM(RTRIM(name))) BETWEEN 1 AND 150 AND requiredCredits BETWEEN 0 AND 1000)
  THROW 52602,N'Nhóm tự chọn cần tên và số tín chỉ yêu cầu hợp lệ.',1;
 IF @kind='COURSE'
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.v_HV_Courses c JOIN dbo.HOC_PHAN h ON h.ma_hp=c.course WHERE c.id=@id
   AND (c.[group] IS NULL OR EXISTS(SELECT 1 FROM dbo.v_HV_Groups g WHERE g.id=c.[group] AND g.program=c.program))
   AND JSON_VALUE(@p,'$.required') IN('true','false') AND LEN(ISNULL(c.equivalence,''))<=20)
   THROW 52602,N'Môn CTĐT hoặc nhóm tự chọn không hợp lệ.',1;
  IF EXISTS(SELECT 1 FROM dbo.v_HV_Courses c JOIN dbo.v_HV_Courses b ON b.program=c.program AND b.course=c.course AND b.id<>c.id WHERE c.id=@id)
   THROW 52602,N'Học phần đã tồn tại trong CTĐT.',1;
  IF EXISTS(SELECT 1 FROM dbo.v_HV_Courses a JOIN dbo.v_HV_Courses b ON a.program=b.program AND a.equivalence=b.equivalence AND a.id<>b.id WHERE a.id=@id AND (ISNULL(a.[group],'')<>ISNULL(b.[group],'') OR a.required<>b.required)) THROW 52602,N'Các môn tương đương phải thuộc cùng nhóm yêu cầu và cùng loại bắt buộc/tự chọn để đếm tín chỉ một lần.',1;
 END;
 IF @kind='REQUIREMENT'
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.v_HV_Requirements r WHERE r.id=@id AND r.[group] BETWEEN 1 AND 1000
   AND r.kind IN('PRE','PRIOR','CO') AND r.minGrade BETWEEN 0 AND 10
   AND EXISTS(SELECT 1 FROM dbo.v_HV_Courses WHERE program=r.program AND course=r.course)
   AND EXISTS(SELECT 1 FROM dbo.v_HV_Courses WHERE program=r.program AND course=r.requiredCourse))
   THROW 52602,N'Điều kiện cần môn, nhóm AND/OR, loại và ngưỡng điểm hợp lệ.',1;
  DECLARE @edges TABLE(a varchar(22),b varchar(22),PRIMARY KEY(a,b));
  INSERT @edges SELECT DISTINCT CASE WHEN a.equivalence IS NULL THEN 'C:'+a.course ELSE 'E:'+a.equivalence END,CASE WHEN b.equivalence IS NULL THEN 'C:'+b.course ELSE 'E:'+b.equivalence END
   FROM dbo.v_HV_Requirements r JOIN dbo.v_HV_Courses a ON a.program=r.program AND a.course=r.course
   JOIN dbo.v_HV_Courses b ON b.program=r.program AND b.course=r.requiredCourse WHERE r.program=@program AND r.kind IN('PRE','PRIOR');
  DECLARE @added int=1;
  WHILE @added>0
  BEGIN
   IF EXISTS(SELECT 1 FROM @edges WHERE a=b) THROW 52605,N'Tiên quyết/học trước tạo chu trình, kể cả môn tương đương.',1;
   INSERT @edges SELECT DISTINCT a.a,b.b FROM @edges a JOIN @edges b ON a.b=b.a
    WHERE NOT EXISTS(SELECT 1 FROM @edges e WHERE e.a=a.a AND e.b=b.b);
   SET @added=@@ROWCOUNT;
  END;
 END;
 IF @kind='ASSIGNMENT'
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.v_HV_Assignments a JOIN dbo.SINH_VIEN s ON s.ma_sv=a.student JOIN dbo.LOP l ON l.ma_lop=s.ma_lop
   JOIN dbo.v_HV_Programs p ON p.id=a.program AND p.published=1 AND p.major=s.ma_nganh AND p.cohort=l.khoa_hoc WHERE a.id=@id)
   THROW 52602,N'Chỉ gắn CTĐT đã công bố đúng ngành và khóa của sinh viên.',1;
  IF EXISTS(SELECT 1 FROM dbo.v_HV_Assignments a JOIN dbo.v_HV_Assignments b ON b.student=a.student AND b.id<>a.id WHERE a.id=@id)
   THROW 52602,N'Sinh viên đã có CTĐT. Sửa bản gắn hiện tại để chuyển phiên bản có kiểm soát.',1;
 END;
 IF @kind='RESULT'
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.v_HV_Results r JOIN dbo.SINH_VIEN s ON s.ma_sv=r.student JOIN dbo.HOC_PHAN h ON h.ma_hp=r.course
   JOIN dbo.HOC_KY k ON k.ma_hk=r.term WHERE r.id=@id AND r.attempt BETWEEN 1 AND 1000 AND (r.recognition='GRADED' AND r.grade BETWEEN 0 AND 10 OR r.recognition IN('TRANSFER','EXEMPT') AND r.grade IS NULL AND r.passed=1)
   AND (JSON_VALUE(@p,'$.publication') IS NULL OR r.publication IS NOT NULL) AND LEN(LTRIM(RTRIM(r.source))) BETWEEN 1 AND 250 AND LEN(LTRIM(RTRIM(r.reason))) BETWEEN 1 AND 500
   AND JSON_VALUE(@p,'$.passed') IN('true','false'))
   THROW 52602,N'Kết quả cần SV, môn, kỳ, lần học, nguồn và lý do; điểm 0–10 cho GRADED, không tự đặt điểm cho TRANSFER/EXEMPT.',1;
  IF EXISTS(SELECT 1 FROM dbo.v_HV_Results a JOIN dbo.v_HV_Results b ON b.student=a.student AND b.course=a.course AND b.attempt=a.attempt AND b.id<>a.id WHERE a.id=@id)
   THROW 52602,N'Lần học đã có kết quả. Hãy sửa kết quả và ghi lý do.',1;
  -- Use the student's published policy for the result's semester. Absence of policy never invents a pass mark.
  DECLARE @resultPass decimal(4,2);
  SELECT TOP(1) @resultPass=p.passGrade FROM dbo.v_HV_Results r
   LEFT JOIN dbo.v_HV_Standing st ON st.student=r.student AND st.term=r.term
   JOIN dbo.v_HV_Policies p ON p.term=r.term AND p.published=1 AND p.standing IN(COALESCE(st.standing,'*'),'*')
   WHERE r.id=@id ORDER BY CASE WHEN p.standing=COALESCE(st.standing,'*') THEN 0 ELSE 1 END,p.published_at DESC,p.id DESC;
  IF @resultPass IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.v_HV_Results WHERE id=@id AND recognition='GRADED'
   AND passed<>CONVERT(bit,CASE WHEN grade>=@resultPass THEN 1 ELSE 0 END))
   THROW 52602,N'Cờ đạt/chưa đạt không khớp ngưỡng điểm đạt của quy chế đã công bố cho SV/kỳ kết quả. Hãy kiểm tra lại nguồn điểm và trạng thái.',1;
 END;
 IF @kind='POLICY' AND NOT EXISTS(SELECT 1 FROM dbo.v_HV_Policies p JOIN dbo.HOC_KY h ON h.ma_hk=p.term WHERE p.id=@id
  AND LEN(LTRIM(RTRIM(p.name))) BETWEEN 1 AND 150 AND LEN(p.standing) BETWEEN 1 AND 20
  AND p.minCredits BETWEEN 0 AND 100 AND p.maxCredits BETWEEN p.minCredits AND 100 AND p.passGrade BETWEEN 0 AND 10
  AND p.maxAttempts BETWEEN 0 AND 1000 AND JSON_VALUE(@p,'$.allowImprove') IN('true','false')
  AND JSON_VALUE(@p,'$.enforceCurriculum') IN('true','false')
  AND (p.excellentThreshold IS NULL AND p.excellentMaxCredits IS NULL OR p.excellentThreshold BETWEEN 0 AND 10 AND p.excellentMaxCredits BETWEEN p.maxCredits AND 100))
  THROW 52602,N'Quy chế cần kỳ, học lực, ngưỡng tín chỉ, điểm đạt và quy tắc số lần hợp lệ. 0 lần là không giới hạn.',1;
 IF @kind IN('OVERRIDE','HOLD','STANDING')
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.SINH_VIEN WHERE ma_sv=JSON_VALUE(@p,'$.student')) THROW 52602,N'Sinh viên không tồn tại.',1;
  IF LEN(ISNULL(LTRIM(RTRIM(JSON_VALUE(@p,'$.reason'))),'')) NOT BETWEEN 1 AND 500 THROW 52602,N'Cần lý do có nội dung, tối đa 500 ký tự.',1;
 END;
 IF @kind='OVERRIDE'
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.v_HV_Overrides o JOIN dbo.HOC_KY k ON k.ma_hk=o.term WHERE o.id=@id
   AND o.minCredits BETWEEN 0 AND 100 AND o.maxCredits BETWEEN o.minCredits AND 100 AND o.expires IS NOT NULL)
   THROW 52602,N'Ngoại lệ cần kỳ, ngưỡng tín chỉ và ngày hết hạn.',1;
  IF EXISTS(SELECT 1 FROM dbo.v_HV_Overrides a JOIN dbo.v_HV_Overrides b ON a.student=b.student AND a.term=b.term AND a.id<>b.id WHERE a.id=@id)
   THROW 52602,N'Sinh viên đã có ngoại lệ kỳ này. Hãy sửa bản hiện tại.',1;
 END;
 IF @kind='HOLD' AND NOT EXISTS(SELECT 1 FROM dbo.v_HV_Holds WHERE id=@id AND scope IN('REGISTER','CANCEL','ALL')
  AND LEN(LTRIM(RTRIM(authority))) BETWEEN 1 AND 150 AND expires IS NOT NULL AND JSON_VALUE(@p,'$.active') IN('true','false'))
  THROW 52602,N'Hold cần phạm vi, đơn vị chịu trách nhiệm, ngày hết hạn và trạng thái.',1;
 IF @kind='STANDING'
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.v_HV_Standing s JOIN dbo.HOC_KY k ON k.ma_hk=s.term WHERE s.id=@id
   AND s.status IN('ACTIVE','SUSPENDED','STOPPED') AND LEN(s.standing) BETWEEN 1 AND 20 AND (s.gpa IS NULL OR s.gpa BETWEEN 0 AND 10))
   THROW 52602,N'Trạng thái học vụ cần kỳ, trạng thái và mã học lực hợp lệ.',1;
  IF EXISTS(SELECT 1 FROM dbo.v_HV_Standing a JOIN dbo.v_HV_Standing b ON a.student=b.student AND a.term=b.term AND a.id<>b.id WHERE a.id=@id)
   THROW 52602,N'Sinh viên đã có trạng thái học vụ kỳ này. Hãy sửa bản hiện tại.',1;
 END;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_HocVu_Save @kind varchar(20),@id varchar(20),@version binary(8)=NULL,@payload nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 EXEC dbo.usp_Minh_KiemTraQuyen;
 IF @kind NOT IN('PROGRAM','GROUP','COURSE','REQUIREMENT','ASSIGNMENT','RESULT','POLICY','OVERRIDE','HOLD','STANDING')
  OR ISJSON(@payload)<>1 OR LEFT(LTRIM(@payload),1)<>'{' OR LEN(@payload)>32000
  OR LEN(@id) NOT BETWEEN 1 AND 20 OR @id COLLATE Latin1_General_100_BIN2 LIKE '%[^-A-Z0-9_]%'
  THROW 52602,N'Nội dung học vụ không hợp lệ.',1;
 BEGIN TRY
  BEGIN TRAN; EXEC dbo.usp_HocVu_Lock; EXEC dbo.usp_Minh_KiemTraQuyen;
  DECLARE @old nvarchar(max),@oldversion binary(8),@published bit;
  SELECT @old=payload,@oldversion=version,@published=published FROM dbo.HV_DOCUMENT WITH(UPDLOCK,HOLDLOCK) WHERE kind=@kind AND id=@id;
  IF (@old IS NOT NULL AND (@version IS NULL OR @version<>@oldversion)) OR (@old IS NULL AND @version IS NOT NULL)
   THROW 52604,N'Dữ liệu đã thay đổi. Bản nháp vẫn được giữ; hãy tải lại trước khi sửa tiếp.',1;
  IF @published=1 THROW 52603,N'Phiên bản đã công bố là bất biến. Hãy tạo bản mới.',1;
  IF @kind IN('GROUP','COURSE','REQUIREMENT') AND @old IS NOT NULL AND
   EXISTS(SELECT 1 FROM dbo.v_HV_Programs WHERE id=JSON_VALUE(@old,'$.program') AND published=1)
   THROW 52603,N'Không được di chuyển hoặc sửa chi tiết thuộc CTĐT đã công bố.',1;
  IF @kind IN('COURSE','RESULT')
  BEGIN
   DECLARE @credits int,@courseName nvarchar(150);
   IF @kind='RESULT' AND @old IS NOT NULL
   BEGIN
    IF JSON_VALUE(@payload,'$.student')<>JSON_VALUE(@old,'$.student') OR JSON_VALUE(@payload,'$.course')<>JSON_VALUE(@old,'$.course')
     OR JSON_VALUE(@payload,'$.term')<>JSON_VALUE(@old,'$.term') OR JSON_VALUE(@payload,'$.attempt')<>JSON_VALUE(@old,'$.attempt')
     THROW 52602,N'Không được đổi SV/học phần/kỳ/lần học của kết quả. Sửa điểm có lý do; lập kết quả mới nếu cần.',1;
    SELECT @credits=TRY_CONVERT(int,JSON_VALUE(@old,'$.credits')),@courseName=JSON_VALUE(@old,'$.courseName');
   END;
   ELSE SELECT @credits=so_tin_chi,@courseName=ten_hp FROM dbo.HOC_PHAN WHERE ma_hp=JSON_VALUE(@payload,'$.course');
   SET @payload=JSON_MODIFY(JSON_MODIFY(@payload,'$.credits',@credits),'$.courseName',@courseName);
  END;
  IF @kind='GROUP' AND EXISTS(SELECT 1 FROM dbo.v_HV_Courses WHERE [group]=@id AND program<>JSON_VALUE(@payload,'$.program'))
   THROW 52602,N'Nhóm đang được học phần tham chiếu; không được chuyển sang CTĐT khác.',1;
  IF @kind='COURSE' AND @old IS NOT NULL AND (JSON_VALUE(@old,'$.program')<>JSON_VALUE(@payload,'$.program') OR JSON_VALUE(@old,'$.course')<>JSON_VALUE(@payload,'$.course'))
   AND EXISTS(SELECT 1 FROM dbo.v_HV_Requirements WHERE program=JSON_VALUE(@old,'$.program') AND (course=JSON_VALUE(@old,'$.course') OR requiredCourse=JSON_VALUE(@old,'$.course')))
   THROW 52602,N'Học phần đang được điều kiện học tham chiếu; không được đổi mã hoặc CTĐT.',1;
  IF @old IS NULL INSERT dbo.HV_DOCUMENT(kind,id,payload) VALUES(@kind,@id,@payload);
  ELSE UPDATE dbo.HV_DOCUMENT SET payload=@payload WHERE kind=@kind AND id=@id;
  EXEC dbo.usp_HocVu_Validate @kind,@id;
  -- Equivalence edits can create an indirect prerequisite cycle: revalidate all rules in the draft.
  IF @kind='COURSE'
  BEGIN
   DECLARE @rule varchar(20);
   DECLARE rules CURSOR LOCAL FAST_FORWARD FOR SELECT id FROM dbo.v_HV_Requirements WHERE program=JSON_VALUE(@payload,'$.program');
   OPEN rules;FETCH NEXT FROM rules INTO @rule;
   WHILE @@FETCH_STATUS=0 BEGIN EXEC dbo.usp_HocVu_Validate 'REQUIREMENT',@rule;FETCH NEXT FROM rules INTO @rule;END;
   CLOSE rules;DEALLOCATE rules;
  END;
  -- Parent version represents the entire draft aggregate, including children changed by other sessions.
  IF @kind IN('GROUP','COURSE','REQUIREMENT')
   UPDATE d SET payload=d.payload FROM dbo.HV_DOCUMENT d WHERE d.kind='PROGRAM'
    AND d.id IN(JSON_VALUE(@payload,'$.program'),JSON_VALUE(@old,'$.program'));
  INSERT dbo.HV_HISTORY(kind,id,old_payload,new_payload,reason,actor)
   VALUES(@kind,@id,@old,@payload,JSON_VALUE(@payload,'$.reason'),CONCAT(N'account:',dbo.ufn_Campus_AccountId(),N';sql:',ORIGINAL_LOGIN()));
  COMMIT;
 END TRY
 BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_HocVu_Publish @kind varchar(20),@id varchar(20),@version binary(8)
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;EXEC dbo.usp_Minh_KiemTraQuyen;
 IF @kind NOT IN('PROGRAM','POLICY') THROW 52602,N'Chỉ công bố CTĐT hoặc quy chế.',1;
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KiemTraQuyen;
  IF NOT EXISTS(SELECT 1 FROM dbo.HV_DOCUMENT WITH(UPDLOCK,HOLDLOCK) WHERE kind=@kind AND id=@id AND version=@version AND published=0)
   THROW 52604,N'Phiên bản đã thay đổi hoặc đã công bố. Hãy tải lại.',1;
  EXEC dbo.usp_HocVu_Validate @kind,@id;
  IF @kind='POLICY' AND NOT EXISTS(SELECT 1 FROM dbo.v_HV_Policies WHERE id=@id AND LEN(LTRIM(RTRIM(source))) BETWEEN 1 AND 500) THROW 52602,N'Cần ghi nguồn quy chế/văn bản và URL trước khi công bố.',1;
  IF @kind='PROGRAM'
  BEGIN
   DECLARE @childKind varchar(20),@childId varchar(20);
   DECLARE children CURSOR LOCAL FAST_FORWARD FOR
    SELECT kind,id FROM dbo.HV_DOCUMENT WHERE kind IN('GROUP','COURSE','REQUIREMENT') AND JSON_VALUE(payload,'$.program')=@id;
   OPEN children;FETCH NEXT FROM children INTO @childKind,@childId;
   WHILE @@FETCH_STATUS=0 BEGIN EXEC dbo.usp_HocVu_Validate @childKind,@childId;FETCH NEXT FROM children INTO @childKind,@childId;END;
   CLOSE children;DEALLOCATE children;
   IF NOT EXISTS(SELECT 1 FROM dbo.v_HV_Courses WHERE program=@id) THROW 52602,N'CTĐT chưa có học phần.',1;
   IF (SELECT requiredCredits FROM dbo.v_HV_Programs WHERE id=@id)>(SELECT SUM(x.credits) FROM (SELECT MAX(credits) credits FROM dbo.v_HV_Courses WHERE program=@id GROUP BY COALESCE('E:'+equivalence,'C:'+course)) x) THROW 52602,N'CTĐT chưa đủ số tín chỉ khả dụng để đạt yêu cầu tốt nghiệp (mỗi nhóm tương đương chỉ đếm một lần).',1;
   IF EXISTS(SELECT 1 FROM dbo.v_HV_Groups g WHERE g.program=@id AND g.requiredCredits>
    ISNULL((SELECT SUM(x.credits) FROM (SELECT MAX(c.credits) credits FROM dbo.v_HV_Courses c WHERE c.[group]=g.id GROUP BY COALESCE('E:'+c.equivalence,'C:'+c.course)) x),0))
    THROW 52602,N'Nhóm tự chọn không đủ học phần cho số tín chỉ yêu cầu.',1;
  END;
  UPDATE dbo.HV_DOCUMENT SET published=1,published_at=SYSUTCDATETIME() WHERE kind=@kind AND id=@id;
  INSERT dbo.HV_HISTORY(kind,id,old_payload,new_payload,reason,actor) SELECT kind,id,payload,payload,N'Công bố phiên bản',CONCAT(N'account:',dbo.ufn_Campus_AccountId(),N';sql:',ORIGINAL_LOGIN()) FROM dbo.HV_DOCUMENT WHERE kind=@kind AND id=@id;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_HocVu_Snapshot @student varchar(20)=NULL
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @sv varchar(20),@role varchar(20),@program varchar(20);
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role='SINH_VIEN' BEGIN IF @student IS NOT NULL AND @student<>@sv THROW 52600,N'Không được đọc học vụ của sinh viên khác.',1;SET @student=@sv;END;
 IF @role NOT IN('SINH_VIEN','DAO_TAO','QUAN_TRI') THROW 52600,N'Không có quyền đọc học vụ.',1;
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;
  EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
  IF @role='SINH_VIEN' AND (@student IS NULL OR @student<>@sv) THROW 52600,N'Không được đọc học vụ của sinh viên khác.',1;
  IF @role NOT IN('SINH_VIEN','DAO_TAO','QUAN_TRI') THROW 52600,N'Không có quyền đọc học vụ.',1;
  SELECT @program=program FROM dbo.v_HV_Assignments WHERE student=@student;
  SELECT * FROM dbo.v_HV_Programs WHERE @student IS NULL OR id=@program;
  SELECT * FROM dbo.v_HV_Groups WHERE @student IS NULL OR program=@program;
  SELECT * FROM dbo.v_HV_Courses WHERE @student IS NULL OR program=@program;
  SELECT * FROM dbo.v_HV_Requirements WHERE @student IS NULL OR program=@program;
  SELECT * FROM dbo.v_HV_Assignments WHERE @student IS NULL OR student=@student;
  SELECT * FROM dbo.v_HV_Results WHERE (@student IS NULL OR student=@student) AND (@role<>'SINH_VIEN' OR publication<=CONVERT(date,DATEADD(HOUR,7,SYSUTCDATETIME())));
  SELECT h.* FROM dbo.HV_HISTORY h WHERE kind='RESULT' AND (@student IS NULL OR JSON_VALUE(new_payload,'$.student')=@student) AND (@role<>'SINH_VIEN' OR TRY_CONVERT(date,JSON_VALUE(new_payload,'$.publication'))<=CONVERT(date,DATEADD(HOUR,7,SYSUTCDATETIME())));
  SELECT * FROM dbo.v_HV_Policies WHERE @student IS NULL OR published=1;
  SELECT * FROM dbo.v_HV_Overrides WHERE @student IS NULL OR student=@student;
  SELECT * FROM dbo.v_HV_Holds WHERE @student IS NULL OR student=@student;
  SELECT * FROM dbo.v_HV_Standing WHERE @student IS NULL OR student=@student;
  SELECT s.ma_sv id,s.ho_ten name,s.ma_nganh major,l.khoa_hoc cohort FROM dbo.SINH_VIEN s JOIN dbo.LOP l ON l.ma_lop=s.ma_lop WHERE @student IS NULL OR s.ma_sv=@student;
  SELECT ma_hp id,ten_hp name,so_tin_chi credits FROM dbo.HOC_PHAN;
  SELECT ma_hk id,CONCAT(nam_hoc,N' • ',hoc_ky) name FROM dbo.HOC_KY;
  SELECT ma_nganh id,ten_nganh name FROM dbo.NGANH;
  -- Each equivalent group earns credit once. Published curriculum and result credit metadata are snapshots.
  DECLARE @earned TABLE(course varchar(20),grade decimal(4,2),credits int,name nvarchar(150),[key] varchar(50));
  ;WITH e AS(
   SELECT r.course,r.grade,r.credits,r.name,COALESCE('E:'+c.equivalence,'C:'+r.course) [key],
    ROW_NUMBER() OVER(PARTITION BY COALESCE('E:'+c.equivalence,'C:'+r.course) ORDER BY r.grade DESC,r.publication DESC,r.id) n
   FROM dbo.v_HV_Results r LEFT JOIN dbo.v_HV_Courses c ON c.program=@program AND c.course=r.course
   LEFT JOIN dbo.v_HV_Standing st ON st.student=r.student AND st.term=r.term
   OUTER APPLY(SELECT TOP(1) p.passGrade FROM dbo.v_HV_Policies p WHERE p.term=r.term AND p.published=1 AND p.standing IN(COALESCE(st.standing,'*'),'*')
    ORDER BY CASE WHEN p.standing=COALESCE(st.standing,'*') THEN 0 ELSE 1 END,p.published_at DESC,p.id DESC) policy
   WHERE r.student=@student AND r.passed=1 AND r.publication<=CONVERT(date,DATEADD(HOUR,7,SYSUTCDATETIME()))
    AND (r.recognition IN('TRANSFER','EXEMPT') OR policy.passGrade IS NULL OR r.grade>=policy.passGrade))
  INSERT @earned SELECT course,grade,credits,name,[key] FROM e WHERE n=1;
  DECLARE @audit TABLE(course varchar(20),name nvarchar(150),[group] varchar(20),required bit,credits int,grade decimal(4,2),passed bit,[key] varchar(50));
  ;WITH c AS(
   SELECT *,ROW_NUMBER() OVER(PARTITION BY COALESCE('E:'+equivalence,'C:'+course) ORDER BY required DESC,id) n FROM dbo.v_HV_Courses WHERE program=@program)
  INSERT @audit SELECT c.course,c.name,c.[group],c.required,c.credits,e.grade,CASE WHEN e.[key] IS NULL THEN 0 ELSE 1 END,COALESCE('E:'+c.equivalence,'C:'+c.course)
   FROM c LEFT JOIN @earned e ON e.[key]=COALESCE('E:'+c.equivalence,'C:'+c.course) WHERE c.n=1;
  SELECT course,name,[group],required,credits,grade,passed FROM @audit;
  SELECT @student student,ISNULL((SELECT SUM(credits) FROM @earned),0) accumulatedCredits,
   (SELECT requiredCredits FROM dbo.v_HV_Programs WHERE id=@program) requiredCredits,
   (SELECT COUNT(*) FROM @audit WHERE required=1 AND passed=0) unmetRequiredCourses,
   CASE WHEN @program IS NULL THEN N'Chưa gắn CTĐT' ELSE N'Đếm mỗi nhóm tương đương một lần, theo kết quả chính thức đã công bố' END note;
  SELECT g.id,g.name,g.requiredCredits,ISNULL((SELECT SUM(a.credits) FROM @audit a WHERE a.[group]=g.id AND a.passed=1),0) earnedCredits
   FROM dbo.v_HV_Groups g WHERE g.program=@program;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
GRANT EXECUTE ON dbo.usp_HocVu_Save TO app_minh;
GRANT EXECUTE ON dbo.usp_HocVu_Publish TO app_minh;
GRANT EXECUTE ON dbo.usp_HocVu_Snapshot TO app_minh;
GO
