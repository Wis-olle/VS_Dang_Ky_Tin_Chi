SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
-- Called only by wrappers in this module. All mutations and academic writes share one transaction lock.
CREATE OR ALTER PROCEDURE dbo.usp_HocVu_Evaluate @sv varchar(20),@lop_json nvarchar(max),@op varchar(20),@dk bigint=NULL,@hoc_ky varchar(20)=NULL
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @now date=CONVERT(date,DATEADD(HOUR,7,SYSUTCDATETIME())),@hk varchar(20),@program varchar(20),
  @policy varchar(20),@standing varchar(20)='*',@status varchar(20)='ACTIVE',@gpa decimal(4,2),@final bit=0,
  @min int,@max int,@pass decimal(4,2),@improve bit,@attempts int,@curriculum bit,@warning bit,@credits int;
 IF @op NOT IN('REGISTER','CANCEL','FINALIZE') OR ISJSON(@lop_json)<>1 OR LEFT(LTRIM(@lop_json),1)<>'['
  THROW 52602,N'Danh sách hoặc thao tác học vụ không hợp lệ.',1;
 DECLARE @selected TABLE(id varchar(20) PRIMARY KEY);
 IF EXISTS(SELECT 1 FROM OPENJSON(@lop_json) WHERE type<>1 OR LEN(value) NOT BETWEEN 1 AND 20 OR value COLLATE Latin1_General_100_BIN2 LIKE '%[^-A-Z0-9_]%')
  OR (SELECT COUNT(*) FROM OPENJSON(@lop_json))>30
  OR (SELECT COUNT(*) FROM OPENJSON(@lop_json))<>(SELECT COUNT(DISTINCT value) FROM OPENJSON(@lop_json))
  THROW 52602,N'Chọn tối đa 30 mã lớp khác nhau.',1;
 INSERT @selected SELECT value FROM OPENJSON(@lop_json);
 IF @op='REGISTER' AND NOT EXISTS(SELECT 1 FROM @selected) THROW 52602,N'Chưa chọn lớp học phần.',1;
 IF @op='FINALIZE' AND @hoc_ky IS NOT NULL
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@hoc_ky) THROW 51004,N'Học kỳ không tồn tại.',1;
  IF EXISTS(SELECT 1 FROM @selected) THROW 52602,N'Kiểm tra tổng tín chỉ chỉ đọc kế hoạch hiện tại; không chọn thêm lớp.',1;
  SET @hk=@hoc_ky;
 END
 ELSE IF @op='CANCEL'
 BEGIN
  SELECT @hk=l.ma_hk FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp WHERE d.ma_dk=@dk AND d.ma_sv=@sv AND d.trang_thai='DANG_KY';
  IF @hk IS NULL THROW 51017,N'Đăng ký không thuộc về bạn hoặc đã hủy.',1;
 END;
 ELSE
 BEGIN
  IF EXISTS(SELECT 1 FROM @selected s LEFT JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=s.id WHERE l.ma_lhp IS NULL) THROW 51007,N'Lớp học phần không tồn tại.',1;
  IF (SELECT COUNT(DISTINCT l.ma_hk) FROM @selected s JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=s.id)<>1 THROW 52602,N'Nhóm lớp phải thuộc cùng một học kỳ.',1;
  SELECT TOP(1) @hk=l.ma_hk FROM @selected s JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=s.id;
 END;
 DECLARE @proposal TABLE(class varchar(20) PRIMARY KEY,course varchar(20),credits int,proposed bit);
 INSERT @proposal SELECT l.ma_lhp,l.ma_hp,h.so_tin_chi,0 FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp JOIN dbo.HOC_PHAN h ON h.ma_hp=l.ma_hp
  WHERE d.ma_sv=@sv AND d.trang_thai='DANG_KY' AND l.ma_hk=@hk AND (@op<>'CANCEL' OR d.ma_dk<>@dk);
 IF @op='REGISTER'
 BEGIN
  IF EXISTS(SELECT 1 FROM @selected s JOIN @proposal p ON p.class=s.id) INSERT #HV_Issues VALUES('DUPLICATE',N'Có lớp đã đăng ký trong nhóm chọn.',1);
  INSERT @proposal SELECT l.ma_lhp,l.ma_hp,h.so_tin_chi,1 FROM @selected s JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=s.id JOIN dbo.HOC_PHAN h ON h.ma_hp=l.ma_hp
   WHERE NOT EXISTS(SELECT 1 FROM @proposal p WHERE p.class=s.id);
  IF EXISTS(SELECT 1 FROM @proposal GROUP BY course HAVING COUNT(*)>1) INSERT #HV_Issues VALUES('DUPLICATE',N'Không đăng ký hai lớp cùng học phần trong một kỳ.',1);
  IF EXISTS(SELECT 1 FROM @proposal p JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=p.class JOIN dbo.HOC_PHAN h ON h.ma_hp=p.course
   JOIN dbo.HOC_KY k ON k.ma_hk=l.ma_hk WHERE p.proposed=1 AND (l.trang_thai NOT IN('DANG_MO','FULL') OR h.trang_thai=0 OR k.trang_thai<>'DANG_MO'
    OR DATEADD(HOUR,7,SYSUTCDATETIME())<k.tg_bat_dau OR DATEADD(HOUR,7,SYSUTCDATETIME())>=k.tg_ket_thuc))
   INSERT #HV_Issues VALUES('CLOSED',N'Lớp/học phần/đợt đăng ký chưa mở hoặc đã đóng.',1);
  IF EXISTS(SELECT 1 FROM @proposal p JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=p.class WHERE p.proposed=1 AND
   l.si_so_max<=(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=l.ma_lhp AND d.trang_thai='DANG_KY'))
   INSERT #HV_Issues VALUES('FULL',N'Có lớp đã đủ sĩ số.',1);
  IF EXISTS(SELECT 1 FROM @proposal p WHERE p.proposed=1 AND NOT EXISTS(SELECT 1 FROM dbo.LICH_HOC WHERE ma_lhp=p.class))
   INSERT #HV_Issues VALUES('NO_SCHEDULE',N'Có lớp chưa có lịch học.',1);
  IF EXISTS(SELECT 1 FROM @proposal p JOIN @proposal q ON p.class<q.class AND (p.proposed=1 OR q.proposed=1)
   JOIN dbo.LICH_HOC a ON a.ma_lhp=p.class JOIN dbo.LICH_HOC b ON b.ma_lhp=q.class AND a.thu=b.thu AND a.tiet_bd<=b.tiet_kt AND b.tiet_bd<=a.tiet_kt
   CROSS APPLY(SELECT CASE WHEN a.ngay_bd>b.ngay_bd THEN a.ngay_bd ELSE b.ngay_bd END bd,CASE WHEN a.ngay_kt<b.ngay_kt THEN a.ngay_kt ELSE b.ngay_kt END kt) t
   WHERE dbo.ufn_Minh_CoNgayHoc(a.thu,t.bd,t.kt)=1)
   INSERT #HV_Issues VALUES('SCHEDULE',N'Các lớp trong nhóm chọn hoặc lịch hiện tại bị trùng lịch.',1);
 END;
 IF EXISTS(SELECT 1 FROM dbo.v_HV_Holds WHERE student=@sv AND active=1 AND expires>=@now AND (scope='ALL' OR scope=@op))
  INSERT #HV_Issues SELECT 'HOLD',CONCAT(N'Khóa ',scope,N': ',reason,N' — ',authority,N' (đến ',CONVERT(varchar(10),expires,23),N')'),1
   FROM dbo.v_HV_Holds WHERE student=@sv AND active=1 AND expires>=@now AND (scope='ALL' OR scope=@op);
 SELECT @standing=standing,@status=status,@gpa=gpa,@final=finalTerm FROM dbo.v_HV_Standing WHERE student=@sv AND term=@hk;
 IF @status<>'ACTIVE' AND @op<>'CANCEL' INSERT #HV_Issues VALUES('STANDING',N'Trạng thái học vụ không cho phép thao tác đăng ký.',1);
 SELECT @program=program FROM dbo.v_HV_Assignments WHERE student=@sv;
 SELECT TOP(1) @policy=id,@min=minCredits,@max=CASE WHEN @gpa>=excellentThreshold THEN excellentMaxCredits ELSE maxCredits END,
  @pass=passGrade,@improve=allowImprove,@attempts=maxAttempts,@curriculum=enforceCurriculum,@warning=warningRetakeOnly
 FROM dbo.v_HV_Policies WHERE term=@hk AND published=1 AND standing IN(@standing,'*')
 ORDER BY CASE WHEN standing=@standing THEN 0 ELSE 1 END,published_at DESC,id DESC;
 SELECT @credits=ISNULL(SUM(credits),0) FROM @proposal;
 INSERT #HV_Issues VALUES('CREDITS',CONCAT(N'Tổng tín chỉ sau thao tác: ',@credits),0);
 IF @policy IS NULL
 BEGIN
  INSERT #HV_Issues VALUES('UNCONFIGURED',N'Kỳ này chưa công bố quy chế học vụ. Chưa áp dụng tiên quyết, học lại và giới hạn tín chỉ; các ràng buộc hiện có vẫn được kiểm tra.',0);
  RETURN;
 END;
 IF EXISTS(SELECT 1 FROM dbo.v_HV_Overrides WHERE student=@sv AND term=@hk AND expires>=@now)
  SELECT @min=minCredits,@max=maxCredits FROM dbo.v_HV_Overrides WHERE student=@sv AND term=@hk AND expires>=@now;
 IF @program IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.v_HV_Programs p JOIN dbo.SINH_VIEN s ON s.ma_sv=@sv AND s.ma_nganh=p.major JOIN dbo.LOP l ON l.ma_lop=s.ma_lop AND l.khoa_hoc=p.cohort WHERE p.id=@program AND p.published=1)
  INSERT #HV_Issues VALUES('PROGRAM_MISMATCH',N'CTĐT đang gắn không khớp ngành/khóa hiện tại. Phòng đào tạo cần kiểm tra lại.',CASE WHEN @op='REGISTER' THEN @curriculum ELSE 0 END);
 IF @op='REGISTER' AND @credits>@max INSERT #HV_Issues VALUES('MAX_CREDITS',CONCAT(N'Vượt giới hạn ',@max,N' tín chỉ của quy chế/ngoại lệ.'),1);
 IF @credits<@min AND @final=0 INSERT #HV_Issues VALUES('MIN_CREDITS',CONCAT(N'Chưa đạt tối thiểu ',@min,N' tín chỉ. Bổ sung trước khi chốt đăng ký.'),CASE WHEN @op='FINALIZE' THEN 1 ELSE 0 END);
 IF @program IS NULL
 BEGIN
  INSERT #HV_Issues VALUES('NO_PROGRAM',N'Sinh viên chưa được gắn CTĐT đã công bố theo ngành/khóa.',CASE WHEN @op='CANCEL' THEN 0 ELSE @curriculum END);
  IF @curriculum=1 RETURN;
 END;
 IF @op='REGISTER' AND @curriculum=1 AND EXISTS(SELECT 1 FROM @proposal p WHERE NOT EXISTS(SELECT 1 FROM dbo.v_HV_Courses c WHERE c.program=@program AND c.course=p.course))
  INSERT #HV_Issues VALUES('OUTSIDE_PROGRAM',N'Có học phần ngoài CTĐT đã gắn.',1);
 IF @op='REGISTER' AND EXISTS(SELECT 1 FROM @proposal p JOIN dbo.v_HV_Courses c ON c.program=@program AND c.course=p.course
  GROUP BY COALESCE('E:'+c.equivalence,'C:'+c.course) HAVING COUNT(*)>1)
  INSERT #HV_Issues VALUES('EQUIVALENT_DUPLICATE',N'Không đăng ký đồng thời các học phần tương đương trong cùng một kỳ.',1);
 DECLARE @results TABLE(course varchar(20),grade decimal(4,2),passed bit,term varchar(20),attempt int,recognition varchar(20));
 INSERT @results SELECT course,grade,passed,term,attempt,recognition FROM dbo.v_HV_Results WHERE student=@sv AND publication<=@now;
 -- Highest recognized result is used; equivalent courses form an explicit, program-local group.
 IF @op='REGISTER'
 BEGIN
  IF @improve=0 AND EXISTS(SELECT 1 FROM @proposal p LEFT JOIN dbo.v_HV_Courses c ON c.program=@program AND c.course=p.course
   JOIN @results r ON r.course=p.course OR EXISTS(SELECT 1 FROM dbo.v_HV_Courses e WHERE e.program=@program AND e.course=r.course AND e.equivalence=c.equivalence)
   WHERE p.proposed=1 AND r.passed=1 AND (r.grade>=@pass OR r.recognition IN('TRANSFER','EXEMPT')))
   INSERT #HV_Issues VALUES('ALREADY_PASSED',N'Học phần hoặc môn tương đương đã đạt; quy chế không cho học cải thiện.',1);
  IF @attempts>0 AND EXISTS(SELECT 1 FROM @proposal p WHERE p.proposed=1 AND
   (SELECT COUNT(*) FROM @results r WHERE r.recognition='GRADED' AND (r.course=p.course OR EXISTS(SELECT 1 FROM dbo.v_HV_Courses a JOIN dbo.v_HV_Courses b ON a.program=b.program AND a.equivalence=b.equivalence WHERE a.program=@program AND a.course=r.course AND b.course=p.course)))>=@attempts)
   INSERT #HV_Issues VALUES('ATTEMPTS',N'Đã đạt giới hạn số lần học theo kết quả chính thức.',1);
  IF @warning=1 AND EXISTS(SELECT 1 FROM @proposal p WHERE p.proposed=1 AND (NOT EXISTS(SELECT 1 FROM @results r WHERE r.course=p.course) OR EXISTS(SELECT 1 FROM @results r WHERE r.passed=1 AND (r.grade>=@pass OR r.recognition IN('TRANSFER','EXEMPT')) AND (r.course=p.course OR EXISTS(SELECT 1 FROM dbo.v_HV_Courses a JOIN dbo.v_HV_Courses b ON a.program=b.program AND a.equivalence=b.equivalence WHERE a.program=@program AND a.course=r.course AND b.course=p.course)))))
   INSERT #HV_Issues VALUES('RETAKE_ONLY',N'Quy chế của học lực này chỉ cho phép học lại học phần chưa đạt.',1);
 END;
 -- AND across numbered groups; OR within each numbered group. Recheck all remaining courses on cancellation.
 DECLARE @rules TABLE(course varchar(20),[group] int,description nvarchar(200),satisfied bit);
 INSERT @rules
 SELECT r.course,r.[group],CONCAT(r.kind,N' ',r.requiredCourse,N' ≥',r.minGrade),CASE WHEN EXISTS(SELECT 1 FROM @results t LEFT JOIN dbo.HOC_KY old ON old.ma_hk=t.term JOIN dbo.HOC_KY currentTerm ON currentTerm.ma_hk=@hk
  WHERE (t.course=r.requiredCourse OR EXISTS(SELECT 1 FROM dbo.v_HV_Courses a JOIN dbo.v_HV_Courses b ON a.program=b.program AND a.equivalence=b.equivalence
    WHERE a.program=@program AND a.course=t.course AND b.course=r.requiredCourse))
  AND (t.recognition IN('TRANSFER','EXEMPT') AND t.passed=1 AND r.minGrade=0 OR t.recognition='GRADED' AND (r.kind='PRIOR' AND t.grade>=r.minGrade OR r.kind<>'PRIOR' AND t.passed=1 AND t.grade>=CASE WHEN r.minGrade>@pass THEN r.minGrade ELSE @pass END))
  AND (t.recognition IN('TRANSFER','EXEMPT') OR r.kind='CO' OR COALESCE(old.ngay_hoc_bd,CONVERT(date,old.tg_bat_dau))<COALESCE(currentTerm.ngay_hoc_bd,CONVERT(date,currentTerm.tg_bat_dau))))
  OR r.kind='CO' AND EXISTS(SELECT 1 FROM @proposal q WHERE q.course=r.requiredCourse OR EXISTS(
   SELECT 1 FROM dbo.v_HV_Courses a JOIN dbo.v_HV_Courses b ON a.program=b.program AND a.equivalence=b.equivalence
   WHERE a.program=@program AND a.course=q.course AND b.course=r.requiredCourse)) THEN 1 ELSE 0 END
 FROM dbo.v_HV_Requirements r JOIN @proposal p ON p.course=r.course WHERE r.program=@program AND (@op<>'CANCEL' OR EXISTS(SELECT 1 FROM dbo.v_HV_Requirements rc WHERE rc.program=r.program AND rc.course=r.course AND rc.[group]=r.[group] AND rc.kind='CO'));
 INSERT #HV_Issues
 SELECT 'REQUIREMENT',CONCAT(N'Học phần ',course,N' thiếu điều kiện nhóm ',[group],N' (',STRING_AGG(description,N' hoặc '),N').'),1
 FROM @rules GROUP BY course,[group] HAVING MAX(CONVERT(int,satisfied))=0;
END;
GO
-- Preserve the existing capacity/deadline/schedule implementation; remove direct entry to the internal copy.
IF OBJECT_ID('dbo.usp_HocVu_RegisterCore','P') IS NULL EXEC sys.sp_rename N'dbo.usp_DangKyHocPhan',N'usp_HocVu_RegisterCore',N'OBJECT';
IF OBJECT_ID('dbo.usp_HocVu_CancelCore','P') IS NULL EXEC sys.sp_rename N'dbo.usp_HuyDangKy',N'usp_HocVu_CancelCore',N'OBJECT';
GO
-- A quiet internal mode avoids INSERT...EXEC (which would prevent the core's rollback on failure).
IF NOT EXISTS(SELECT 1 FROM sys.parameters WHERE object_id=OBJECT_ID('dbo.usp_HocVu_RegisterCore') AND name='@emit')
BEGIN
 DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_HocVu_RegisterCore'));
 DECLARE @keyword int=CHARINDEX('PROCEDURE',UPPER(@definition));
 SET @definition=N'ALTER '+SUBSTRING(@definition,@keyword,LEN(@definition));
 SET @definition=REPLACE(@definition,N'usp_DangKyHocPhan',N'usp_HocVu_RegisterCore');
 SET @definition=REPLACE(@definition,N'@ma_lhp varchar(20)',N'@ma_lhp varchar(20),@emit bit=1,@new_id bigint=NULL OUTPUT');
 SET @definition=REPLACE(@definition,N'SELECT @id AS ma_dk',N'SET @new_id=@id; IF @emit=1 SELECT @id AS ma_dk');
 IF CHARINDEX(N'@new_id=@id',@definition)=0 THROW 52602,N'Không nhận diện được hợp đồng đăng ký cũ. Dừng migration.',1;
 EXEC sys.sp_executesql @definition;
END;
GO
DENY EXECUTE ON dbo.usp_HocVu_RegisterCore TO public;
DENY EXECUTE ON dbo.usp_HocVu_CancelCore TO public;
DENY EXECUTE ON dbo.usp_HocVu_Evaluate TO public;
GO
CREATE OR ALTER PROCEDURE dbo.usp_HocVu_Precheck @lop_json nvarchar(max),@thao_tac varchar(20)='REGISTER',@ma_dk bigint=NULL,@ma_hk varchar(20)=NULL
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @sv varchar(20),@role varchar(20);
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
 CREATE TABLE #HV_Issues(code varchar(30),message nvarchar(1500),blocking bit);
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;
  EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
  EXEC dbo.usp_Minh_KhoaLapLich;
  EXEC dbo.usp_HocVu_Evaluate @sv,@lop_json,@thao_tac,@ma_dk,@hoc_ky=@ma_hk;
  COMMIT;
  SELECT code,message,blocking FROM #HV_Issues ORDER BY blocking DESC,code;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_HocVu_DangKyNhom @lop_json nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @sv varchar(20),@role varchar(20),@code varchar(20),@message nvarchar(1500),@new_id bigint;
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
 CREATE TABLE #HV_Issues(code varchar(30),message nvarchar(1500),blocking bit);
 DECLARE @results TABLE(ma_dk bigint,thong_bao nvarchar(1500));
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;
  EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
  EXEC dbo.usp_Minh_KhoaLapLich;
  EXEC dbo.usp_HocVu_Evaluate @sv,@lop_json,'REGISTER';
  SELECT TOP(1) @message=message FROM #HV_Issues WHERE blocking=1 ORDER BY code;
  IF @message IS NOT NULL THROW 52610,@message,1;
  DECLARE selected CURSOR LOCAL FAST_FORWARD FOR SELECT value FROM OPENJSON(@lop_json) ORDER BY value;
  OPEN selected;FETCH NEXT FROM selected INTO @code;
  WHILE @@FETCH_STATUS=0 BEGIN EXEC dbo.usp_HocVu_RegisterCore @ma_lhp=@code,@emit=0,@new_id=@new_id OUTPUT; INSERT @results VALUES(@new_id,N'Đăng ký thành công.');FETCH NEXT FROM selected INTO @code;END;
  CLOSE selected;DEALLOCATE selected;
  COMMIT;SELECT * FROM @results;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_DangKyHocPhan @ma_lhp varchar(20)
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @json nvarchar(max)=(SELECT @ma_lhp AS [value] FOR JSON PATH);
 SET @json=N'["'+STRING_ESCAPE(@ma_lhp,'json')+N'"]';
 EXEC dbo.usp_HocVu_DangKyNhom @json;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_HuyDangKy @ma_dk bigint
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @sv varchar(20),@role varchar(20),@message nvarchar(1500);
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
 CREATE TABLE #HV_Issues(code varchar(30),message nvarchar(1500),blocking bit);
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;
  EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
  EXEC dbo.usp_Minh_KhoaLapLich;
  EXEC dbo.usp_HocVu_Evaluate @sv,N'[]','CANCEL',@ma_dk;
  SELECT TOP(1) @message=message FROM #HV_Issues WHERE blocking=1 ORDER BY code;
  IF @message IS NOT NULL THROW 52610,@message,1;
  EXEC dbo.usp_HocVu_CancelCore @ma_dk=@ma_dk;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
GRANT EXECUTE ON dbo.usp_HocVu_Precheck TO app_minh;
GRANT EXECUTE ON dbo.usp_HocVu_DangKyNhom TO app_minh;
GRANT EXECUTE ON dbo.usp_DangKyHocPhan TO app_minh;
GRANT EXECUTE ON dbo.usp_HuyDangKy TO app_minh;
GRANT EXECUTE ON dbo.usp_DangKyHocPhan TO app_dang_ky;
GRANT EXECUTE ON dbo.usp_HuyDangKy TO app_dang_ky;
GO
