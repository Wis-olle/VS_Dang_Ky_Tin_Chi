SET NOCOUNT ON; SET XACT_ABORT ON;
DECLARE @expectedDatabase sysname=N'__SHOWCASE_DATABASE__';
IF DB_NAME()<>@expectedDatabase OR LEN(@expectedDatabase)<>64 OR @expectedDatabase NOT LIKE N'DangKyTinChi_Minh_Test_Showcase[_]%' OR RIGHT(@expectedDatabase,32) COLLATE Latin1_General_100_BIN2 LIKE '%[^a-f0-9]%'
 THROW 54980,N'Full demo requires the exact owned showcase database.',1;
IF EXISTS(SELECT 1 FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND id='CNTT24_V2') THROW 54983,N'Create a new demo; do not reseed.',1;
BEGIN TRY BEGIN TRAN;
DECLARE @actor int=(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap=ORIGINAL_LOGIN()),@oldRole varchar(20),@oldStudent varchar(20),
 @today date=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())),@source nvarchar(250)=N'SYNTHETIC DEMO - dữ liệu minh họa, không phải hồ sơ thật',
 @p nvarchar(max),@id varchar(20),@v binary(8),@student varchar(20),@course varchar(20),@n int,@hash binary(32),
 @advisor int=(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap='daotao');
IF @actor IS NULL THROW 54984,N'Missing owned local identity.',1;
SELECT @oldRole=vai_tro,@oldStudent=ma_sv FROM dbo.TAI_KHOAN WHERE ma_tk=@actor;
UPDATE dbo.TAI_KHOAN SET vai_tro='QUAN_TRI',ma_sv=NULL WHERE ma_tk=@actor;
EXEC dbo.usp_HocVu_Save 'PROGRAM','CNTT24_V2',NULL,N'{"name":"CNTT 2024 · Phiên bản thực hành","major":"CNTT","cohort":2024,"requiredCredits":23}';
EXEC dbo.usp_HocVu_Save 'GROUP','V2_CO_SO',NULL,N'{"program":"CNTT24_V2","name":"Cơ sở ngành","requiredCredits":9}';
EXEC dbo.usp_HocVu_Save 'GROUP','V2_NGOAI_NGU',NULL,N'{"program":"CNTT24_V2","name":"Ngoại ngữ","requiredCredits":5}';
EXEC dbo.usp_HocVu_Save 'GROUP','V2_MO_RONG',NULL,N'{"program":"CNTT24_V2","name":"Kiến thức mở rộng","requiredCredits":9}';
DECLARE courses CURSOR LOCAL FAST_FORWARD FOR SELECT ma_hp FROM dbo.HOC_PHAN WHERE ma_hp IN('IT101','IT102','IT103','EN101','EN102','EC101','EC102','ET101');
OPEN courses; FETCH NEXT FROM courses INTO @course;
WHILE @@FETCH_STATUS=0 BEGIN
 SET @id='V2_'+@course;
 SET @p=(SELECT 'CNTT24_V2' program,@course course,CASE LEFT(@course,2) WHEN 'IT' THEN 'V2_CO_SO' WHEN 'EN' THEN 'V2_NGOAI_NGU' ELSE 'V2_MO_RONG' END [group],CAST(1 AS bit) required FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 EXEC dbo.usp_HocVu_Save 'COURSE',@id,NULL,@p;
 FETCH NEXT FROM courses INTO @course;
END; CLOSE courses; DEALLOCATE courses;
EXEC dbo.usp_HocVu_Save 'REQUIREMENT','V2_PRE_IT102',NULL,N'{"program":"CNTT24_V2","course":"IT102","group":1,"requiredCourse":"IT101","kind":"PRE","minGrade":5}';
EXEC dbo.usp_HocVu_Save 'REQUIREMENT','V2_PRIOR_IT103',NULL,N'{"program":"CNTT24_V2","course":"IT103","group":1,"requiredCourse":"IT102","kind":"PRIOR","minGrade":0}';
EXEC dbo.usp_HocVu_Save 'REQUIREMENT','V2_CO_EN102',NULL,N'{"program":"CNTT24_V2","course":"EN102","group":1,"requiredCourse":"EN101","kind":"CO","minGrade":0}';
SELECT @v=version FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND id='CNTT24_V2';
EXEC dbo.usp_HocVu_Publish 'PROGRAM','CNTT24_V2',@v;
EXEC dbo.usp_HocVu_Save 'PROGRAM','CNTT25_DRAFT',NULL,N'{"name":"CNTT 2025 · Bản nháp để thử chỉnh sửa","major":"CNTT","cohort":2025,"requiredCredits":120}';
-- Policies are synthetic, permissive enough for the main student's add/drop/swap tests.
SET @n=1;
WHILE @n<=3 BEGIN
 DECLARE @standing varchar(20)=CASE WHEN @n=2 THEN 'WARNING' ELSE '*' END,@term varchar(20)=CASE WHEN @n=3 THEN 'HK2-2627' ELSE 'HK1-2627' END;
 SET @id=CONCAT('DEMO_POLICY_',@n);
 SET @p=(SELECT N'Quy chế mô phỏng · '+@standing name,@term term,@standing standing,0 minCredits,CASE WHEN @n=2 THEN 14 ELSE 24 END maxCredits,
  CAST(5 AS decimal(4,2)) passGrade,CAST(1 AS bit) allowImprove,0 maxAttempts,CAST(0 AS bit) enforceCurriculum,@source source FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 EXEC dbo.usp_HocVu_Save 'POLICY',@id,NULL,@p;
 SELECT @v=version FROM dbo.HV_DOCUMENT WHERE kind='POLICY' AND id=@id;
 EXEC dbo.usp_HocVu_Publish 'POLICY',@id,@v;
 SET @n+=1;
END;
EXEC dbo.usp_HocVu_Save 'POLICY','POLICY_DRAFT',NULL,N'{"name":"Quy chế nháp · chưa có hiệu lực","term":"HK2-2627","standing":"NORMAL","minCredits":12,"maxCredits":22,"passGrade":5,"allowImprove":true,"maxAttempts":0,"enforceCurriculum":false,"source":"SYNTHETIC DEMO"}';
SET @n=1;
WHILE @n<=24 BEGIN
 SET @student=CONCAT('SV2024',RIGHT(CONCAT('000',@n),3));
 IF @n>1 BEGIN
  SET @id=CONCAT('ASSIGN_',@n);SET @p=(SELECT @student student,'CNTT24_V2' program FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.usp_HocVu_Save 'ASSIGNMENT',@id,NULL,@p;
 END;
 SET @id=CONCAT('STANDING_',@n);
 SET @p=(SELECT @student student,'HK1-2627' term,CASE @n WHEN 6 THEN 'SUSPENDED' WHEN 7 THEN 'STOPPED' ELSE 'ACTIVE' END status,
  CASE WHEN @n=5 THEN 'WARNING' ELSE 'NORMAL' END standing,CAST(CASE WHEN @n=5 THEN 4.2 WHEN @n%3=0 THEN 8.7 ELSE 7.4 END AS decimal(4,2)) gpa,
  CONVERT(bit,CASE WHEN @n=8 THEN 1 ELSE 0 END) finalTerm,@source reason FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 EXEC dbo.usp_HocVu_Save 'STANDING',@id,NULL,@p;
 IF @n>1 BEGIN
  SET @id=CONCAT('RESULT_IT_',@n);
  SET @p=(SELECT @student student,'IT101' course,'HK1-2627' term,1 attempt,'GRADED' recognition,
   CAST(CASE WHEN @n IN(3,5) THEN 3.5 ELSE 7.5+(@n%3)*0.5 END AS decimal(4,2)) grade,CONVERT(bit,CASE WHEN @n IN(3,5) THEN 0 ELSE 1 END) passed,
   CONVERT(varchar(10),DATEADD(day,-10,@today),23) publication,@source source,N'Kết quả mô phỏng để thử đạt/chưa đạt và học lại' reason FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.usp_HocVu_Save 'RESULT',@id,NULL,@p;
  SET @id=CONCAT('RESULT_EC_',@n);
  SET @p=(SELECT @student student,'EC101' course,'HK1-2627' term,1 attempt,CASE WHEN @n%2=0 THEN 'TRANSFER' ELSE 'EXEMPT' END recognition,
   CAST(1 AS bit) passed,CONVERT(varchar(10),DATEADD(day,-10,@today),23) publication,@source source,N'Công nhận/miễn mô phỏng, không gán điểm' reason FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.usp_HocVu_Save 'RESULT',@id,NULL,@p;
 END;
 SET @n+=1;
END;
SET @p=(SELECT 'SV2024003' student,'IT101' course,'HK1-2627' term,2 attempt,'GRADED' recognition,CAST(8 AS decimal(4,2)) grade,CAST(1 AS bit) passed,
 CONVERT(varchar(10),DATEADD(day,-2,@today),23) publication,@source source,N'Lần học lại đã đạt · dữ liệu mô phỏng' reason FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
EXEC dbo.usp_HocVu_Save 'RESULT','RESULT_RETAKE_3',NULL,@p;
SELECT @p=payload,@v=version FROM dbo.HV_DOCUMENT WHERE kind='RESULT' AND id='RESULT_IT_2';
SET @p=JSON_MODIFY(JSON_MODIFY(@p,'$.grade',8.5),'$.reason',N'Điều chỉnh theo nguồn mô phỏng để thử lịch sử sửa kết quả');
EXEC dbo.usp_HocVu_Save 'RESULT','RESULT_IT_2',@v,@p;
SET @n=5;
WHILE @n<=8 BEGIN
 SET @id=CONCAT('HOLD_',@n);SET @student=CONCAT('SV202400',@n);
 SET @p=(SELECT @student student,CASE WHEN @n=6 THEN 'ALL' ELSE 'REGISTER' END scope,N'Phòng Công tác sinh viên · mô phỏng' authority,
  CASE @n WHEN 5 THEN N'Chưa hoàn tất học phí · liên hệ bộ phận tài chính' WHEN 6 THEN N'Đang bảo lưu · cần xác nhận quay lại học' WHEN 7 THEN N'Đã bổ sung hồ sơ và gỡ khóa' ELSE N'Khóa cũ đã hết hạn' END reason,
  CONVERT(varchar(10),DATEADD(day,CASE WHEN @n=8 THEN -2 ELSE 30 END,@today),23) expires,CONVERT(bit,CASE WHEN @n=7 THEN 0 ELSE 1 END) active FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 EXEC dbo.usp_HocVu_Save 'HOLD',@id,NULL,@p;
 SET @n+=1;
END;
SET @p=(SELECT 'SV2024008' student,'HK1-2627' term,0 minCredits,30 maxCredits,CONVERT(varchar(10),DATEADD(day,30,@today),23) expires,@source reason FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
EXEC dbo.usp_HocVu_Save 'OVERRIDE','OVERRIDE_8',NULL,@p;
SET @p=(SELECT 'SV2024004' student,'HK1-2627' term,0 minCredits,28 maxCredits,CONVERT(varchar(10),DATEADD(day,-1,@today),23) expires,N'SYNTHETIC DEMO - ngoại lệ cũ đã hết hạn' reason FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
EXEC dbo.usp_HocVu_Save 'OVERRIDE','OVERRIDE_EXPIRED',NULL,@p;
DECLARE offerings CURSOR LOCAL FAST_FORWARD FOR SELECT ma_hp FROM dbo.HOC_PHAN WHERE ma_hp IN('IT101','IT102','EN101','EC101','ET101');
OPEN offerings; FETCH NEXT FROM offerings INTO @course;
WHILE @@FETCH_STATUS=0 BEGIN EXEC dbo.usp_AP4_SaveOffering @course,'HK2-2627',@source;FETCH NEXT FROM offerings INTO @course;END;
CLOSE offerings;DEALLOCATE offerings;
EXEC dbo.usp_AP4_SaveSubstitution 'CNTT24_V2','IT201','IT103',@source;
-- Reconcile the default student's baseline plan after the expanded catalog/offering hash changes.
INSERT dbo.AP4_ADVISOR(student,account_id,active) VALUES('SV2024001',@actor,1);
UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024001' WHERE ma_tk=@actor;
SELECT @v=version,@p=items_json FROM dbo.AP4_PLAN WHERE student='SV2024001';
SET @hash=dbo.ufn_AP4_SourceHash('SV2024001','CNTT_2024');
EXEC dbo.usp_AP4_SavePlan 'CNTT_2024',@hash,@p,@v;
SELECT @v=version FROM dbo.AP4_PLAN WHERE student='SV2024001';
EXEC dbo.usp_AP4_Submit 'SV2024001',@v,@hash;
UPDATE dbo.TAI_KHOAN SET vai_tro='QUAN_TRI',ma_sv=NULL WHERE ma_tk=@actor;
SELECT @v=version FROM dbo.AP4_PLAN WHERE student='SV2024001';
EXEC dbo.usp_AP4_Advise 'SV2024001',@v,'COMMENT',N'SYNTHETIC DEMO - kế hoạch đã đối soát với danh mục mới, đang chờ cố vấn quyết định.';
DELETE dbo.AP4_ADVISOR WHERE student='SV2024001' AND account_id=@actor;
-- Actual procedure-generated plans; temporary mapped advisor is removed before commit.
SET @n=2;
WHILE @n<=4 BEGIN
 SET @student=CONCAT('SV202400',@n);
 EXEC dbo.usp_AP4_AssignAdvisor @student,@advisor,1,@source;
 INSERT dbo.AP4_ADVISOR(student,account_id,active) VALUES(@student,@actor,1);
 UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv=@student WHERE ma_tk=@actor;
 SET @hash=dbo.ufn_AP4_SourceHash(@student,'CNTT24_V2');
 EXEC dbo.usp_AP4_SavePlan 'CNTT24_V2',@hash,N'[{"course":"EN101","term":"HK2-2627"},{"course":"ET101","term":"HK2-2627"}]';
 SELECT @v=version FROM dbo.AP4_PLAN WHERE student=@student;
 IF @n>2 EXEC dbo.usp_AP4_Submit @student,@v,@hash;
 UPDATE dbo.TAI_KHOAN SET vai_tro='QUAN_TRI',ma_sv=NULL WHERE ma_tk=@actor;
 SELECT @v=version FROM dbo.AP4_PLAN WHERE student=@student;
 DECLARE @action varchar(12)=CASE @n WHEN 3 THEN 'APPROVE' WHEN 4 THEN 'RETURN' ELSE 'COMMENT' END;
 EXEC dbo.usp_AP4_Advise @student,@v,@action,N'SYNTHETIC DEMO - góp ý kế hoạch: cân đối khối lượng học và kiểm tra kỳ dự kiến mở môn.';
 DELETE dbo.AP4_ADVISOR WHERE student=@student AND account_id=@actor;
 SET @n+=1;
END;
UPDATE dbo.TAI_KHOAN SET vai_tro=@oldRole,ma_sv=@oldStudent WHERE ma_tk=@actor;
COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
