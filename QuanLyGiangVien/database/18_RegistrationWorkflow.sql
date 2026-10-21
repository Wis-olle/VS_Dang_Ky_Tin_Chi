SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('dbo.DK3_CART','U') IS NULL
 CREATE TABLE dbo.DK3_CART(student varchar(20) NOT NULL REFERENCES dbo.SINH_VIEN(ma_sv),term varchar(20) NOT NULL REFERENCES dbo.HOC_KY(ma_hk),class_json nvarchar(max) NOT NULL CHECK(ISJSON(class_json)=1),updated_at datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),version rowversion,CONSTRAINT PK_DK3_CART PRIMARY KEY(student,term));
IF OBJECT_ID('dbo.DK3_REQUEST','U') IS NULL
 CREATE TABLE dbo.DK3_REQUEST(student varchar(20) NOT NULL REFERENCES dbo.SINH_VIEN(ma_sv),request_id uniqueidentifier NOT NULL,payload_hash binary(32) NOT NULL,operation varchar(10) NOT NULL,term varchar(20) NOT NULL REFERENCES dbo.HOC_KY(ma_hk),result_json nvarchar(max) NOT NULL CHECK(ISJSON(result_json)=1),created_at datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),CONSTRAINT PK_DK3_REQUEST PRIMARY KEY(student,request_id));
IF OBJECT_ID('dbo.DK3_COMBINATION','U') IS NULL
 CREATE TABLE dbo.DK3_COMBINATION(id varchar(20) NOT NULL PRIMARY KEY,term varchar(20) NOT NULL REFERENCES dbo.HOC_KY(ma_hk),course varchar(20) NOT NULL REFERENCES dbo.HOC_PHAN(ma_hp),version rowversion);
IF OBJECT_ID('dbo.DK3_COMBINATION_MEMBER','U') IS NULL
 CREATE TABLE dbo.DK3_COMBINATION_MEMBER(combination_id varchar(20) NOT NULL REFERENCES dbo.DK3_COMBINATION(id),class_id varchar(20) NOT NULL REFERENCES dbo.LOP_HOC_PHAN(ma_lhp),CONSTRAINT PK_DK3_COMBINATION_MEMBER PRIMARY KEY(combination_id,class_id));
IF COL_LENGTH('dbo.DK3_COMBINATION_MEMBER','component_type') IS NULL
 ALTER TABLE dbo.DK3_COMBINATION_MEMBER ADD component_type varchar(2) NULL CONSTRAINT CK_DK3_COMPONENT_TYPE CHECK(component_type IN('LT','TH','TL'));
GO
CREATE OR ALTER PROCEDURE dbo.usp_DK3_ValidateJson @class_json nvarchar(max),@remove_json nvarchar(max)=N'[]'
AS
BEGIN
 IF ISNULL(ISJSON(@class_json),0)<>1 OR LEFT(LTRIM(@class_json),1)<>'[' OR ISNULL(ISJSON(@remove_json),0)<>1 OR LEFT(LTRIM(@remove_json),1)<>'[' THROW 52702,N'Danh sách không hợp lệ.',1;
 IF (SELECT COUNT(*) FROM OPENJSON(@class_json))>30 OR EXISTS(SELECT 1 FROM OPENJSON(@class_json) WHERE type<>1 OR LEN(value) NOT BETWEEN 1 AND 20 OR value COLLATE Latin1_General_100_BIN2 LIKE '%[^-A-Z0-9_]%')
  OR (SELECT COUNT(*) FROM OPENJSON(@class_json))<>(SELECT COUNT(DISTINCT value) FROM OPENJSON(@class_json)) THROW 52702,N'Chọn tối đa 30 mã lớp khác nhau.',1;
 IF (SELECT COUNT(*) FROM OPENJSON(@remove_json))>30 OR EXISTS(SELECT 1 FROM OPENJSON(@remove_json) WHERE type<>2 OR TRY_CONVERT(bigint,value) IS NULL OR TRY_CONVERT(bigint,value)<=0 OR value LIKE '%[^0-9]%')
  OR (SELECT COUNT(*) FROM OPENJSON(@remove_json))<>(SELECT COUNT(DISTINCT value) FROM OPENJSON(@remove_json)) THROW 52702,N'Danh sách đăng ký cần bỏ không hợp lệ.',1;
END;
GO
-- Academic evaluator is forked deliberately: component validation replaces the old one-class-per-course rule.
-- Do not call the old register core: it rejects a legitimate LT/TH pair.
DECLARE @sql nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_HocVu_Evaluate'));
IF @sql IS NULL OR CHARINDEX('SUM(credits)',@sql)=0 OR CHARINDEX('HAVING COUNT(*)>1',@sql)=0 THROW 52702,N'Không nhận diện hợp đồng học vụ. Dừng migration.',1;
SET @sql=SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));
SET @sql=N'CREATE OR ALTER '+REPLACE(@sql,'usp_HocVu_Evaluate','usp_DK3_Evaluate');
SET @sql=REPLACE(@sql,N'IF @op=''REGISTER'' AND NOT EXISTS(SELECT 1 FROM @selected)',N'IF @op=''REGISTER'' AND @hoc_ky IS NULL AND NOT EXISTS(SELECT 1 FROM @selected)');
SET @sql=REPLACE(@sql,N'IF @op=''FINALIZE'' AND @hoc_ky IS NOT NULL',N'IF @op IN(''FINALIZE'',''CANCEL'') AND @hoc_ky IS NOT NULL');
SET @sql=REPLACE(@sql,N'IF EXISTS(SELECT 1 FROM @proposal GROUP BY course HAVING COUNT(*)>1) INSERT #HV_Issues VALUES(''DUPLICATE'',N''Không đăng ký hai lớp cùng học phần trong một kỳ.'',1);',N'-- Complete combinations are checked independently against the final proposal.');
SET @sql=REPLACE(@sql,N'OR d.ma_dk<>@dk)',N'OR @dk IS NULL OR d.ma_dk<>@dk)');
SET @sql=REPLACE(@sql,N'SELECT @credits=ISNULL(SUM(credits),0) FROM @proposal;',N'SELECT @credits=ISNULL(SUM(credits),0) FROM (SELECT course,MAX(credits) credits FROM @proposal GROUP BY course) creditCourses; UPDATE #DK3_Context SET total_credits=@credits;');
SET @sql=REPLACE(@sql,N'HAVING COUNT(*)>1)',N'HAVING COUNT(DISTINCT p.course)>1)');
SET @sql=REPLACE(@sql,N'FROM dbo.v_HV_Requirements r JOIN @proposal p ON p.course=r.course',N'FROM dbo.v_HV_Requirements r JOIN (SELECT DISTINCT course FROM @proposal) p ON p.course=r.course');
-- Separate deadline exceptions from closed classes/courses. Neither capacity nor class closure is waivable.
DECLARE @start int=CHARINDEX(N'IF EXISTS(SELECT 1 FROM @proposal p JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=p.class JOIN dbo.HOC_PHAN h ON h.ma_hp=p.course',@sql),@finish int;
SET @finish=CHARINDEX(N'IF EXISTS(SELECT 1 FROM @proposal p JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=p.class WHERE p.proposed=1',@sql,@start);
IF @start=0 OR @finish=0 THROW 52702,N'Không nhận diện kiểm tra thời hạn học vụ.',1;
SET @sql=STUFF(@sql,@start,@finish-@start,N'IF EXISTS(SELECT 1 FROM @proposal p JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=p.class JOIN dbo.HOC_PHAN h ON h.ma_hp=p.course WHERE p.proposed=1 AND (l.trang_thai NOT IN(''DANG_MO'',''FULL'') OR h.trang_thai=0)) INSERT #HV_Issues VALUES(''CLASS_CLOSED'',N''Lớp hoặc học phần không nhận đăng ký.'',1);
 IF EXISTS(SELECT 1 FROM @proposal p JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=p.class JOIN dbo.HOC_KY k ON k.ma_hk=l.ma_hk WHERE p.proposed=1 AND (k.trang_thai<>''DANG_MO'' OR DATEADD(HOUR,7,SYSUTCDATETIME())<k.tg_bat_dau OR DATEADD(HOUR,7,SYSUTCDATETIME())>=k.tg_ket_thuc))
 BEGIN
 INSERT #HV_Issues VALUES(''LATE_REGISTER'',N''Ngoài thời hạn đăng ký; cần quyết định đăng ký muộn còn hiệu lực.'',1);
 INSERT #DK3_IssueScope SELECT ''LATE_REGISTER'',p.course,p.class,NULL FROM @proposal p JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=p.class JOIN dbo.HOC_KY k ON k.ma_hk=l.ma_hk WHERE p.proposed=1 AND (k.trang_thai<>''DANG_MO'' OR DATEADD(HOUR,7,SYSUTCDATETIME())<k.tg_bat_dau OR DATEADD(HOUR,7,SYSUTCDATETIME())>=k.tg_ket_thuc);
 END;
 ');
SET @sql=REPLACE(@sql,N'IF @op=''REGISTER'' AND @credits>@max INSERT #HV_Issues VALUES(''MAX_CREDITS'',CONCAT(N''Vượt giới hạn '',@max,N'' tín chỉ của quy chế/ngoại lệ.''),1);',N'IF @op=''REGISTER'' AND @credits>@max BEGIN INSERT #HV_Issues VALUES(''MAX_CREDITS'',CONCAT(N''Vượt giới hạn '',@max,N'' tín chỉ của quy chế/ngoại lệ.''),1); INSERT #DK3_IssueScope VALUES(''MAX_CREDITS'',NULL,NULL,NULL); END;');
SET @sql=REPLACE(@sql,N'FROM @rules GROUP BY course,[group] HAVING MAX(CONVERT(int,satisfied))=0;',N'FROM @rules GROUP BY course,[group] HAVING MAX(CONVERT(int,satisfied))=0;
 INSERT #DK3_IssueScope SELECT ''REQUIREMENT'',course,NULL,CONCAT(''GROUP_'',[group]) FROM @rules GROUP BY course,[group] HAVING MAX(CONVERT(int,satisfied))=0;');
EXEC sys.sp_executesql @sql;
GO
-- Extension point implemented by scoped decisions migration. No override exists by default.
CREATE OR ALTER PROCEDURE dbo.usp_DK3_FilterIssues @student varchar(20),@term varchar(20),@selected_json nvarchar(max),@consume bit,@request_id uniqueidentifier=NULL AS BEGIN SET NOCOUNT ON; END;
GO
-- Optional advanced allocation hooks are replaced by migration 22.
CREATE OR ALTER PROCEDURE dbo.usp_Seat_Check @student varchar(20),@term varchar(20),@add_json nvarchar(max),@request_id uniqueidentifier,@offer_id bigint=NULL AS BEGIN SET NOCOUNT ON; END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Seat_Commit @student varchar(20),@term varchar(20),@request_id uniqueidentifier,@offer_id bigint=NULL AS BEGIN SET NOCOUNT ON; END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_DK3_ValidateProposal @student varchar(20),@term varchar(20),@selected_json nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @final TABLE(class_id varchar(20) PRIMARY KEY,course varchar(20));
 INSERT @final SELECT l.ma_lhp,l.ma_hp FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp WHERE d.ma_sv=@student AND d.trang_thai='DANG_KY' AND l.ma_hk=@term;
 INSERT @final SELECT l.ma_lhp,l.ma_hp FROM OPENJSON(@selected_json) j JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=j.value WHERE NOT EXISTS(SELECT 1 FROM @final f WHERE f.class_id=l.ma_lhp);
 IF EXISTS(SELECT 1 FROM @final f GROUP BY course HAVING COUNT(*)>1 AND NOT EXISTS(SELECT 1 FROM dbo.DK3_COMBINATION c WHERE c.term=@term AND c.course=f.course))
  INSERT #HV_Issues VALUES('DUPLICATE',N'Không được chọn hai lớp cùng học phần nếu chưa có tổ hợp thành phần hợp lệ.',1);
 IF EXISTS(SELECT 1 FROM @final f WHERE EXISTS(SELECT 1 FROM dbo.DK3_COMBINATION c WHERE c.term=@term AND c.course=f.course) AND NOT EXISTS(
  SELECT 1 FROM dbo.DK3_COMBINATION c WHERE c.term=@term AND c.course=f.course
   AND (SELECT COUNT(*) FROM dbo.DK3_COMBINATION_MEMBER m WHERE m.combination_id=c.id AND m.component_type='LT')=1
   AND EXISTS(SELECT 1 FROM dbo.DK3_COMBINATION_MEMBER m WHERE m.combination_id=c.id AND m.component_type IN('TH','TL'))
   AND NOT EXISTS(SELECT 1 FROM dbo.DK3_COMBINATION_MEMBER m WHERE m.combination_id=c.id AND m.component_type IS NULL)
   AND NOT EXISTS(SELECT 1 FROM dbo.DK3_COMBINATION_MEMBER m WHERE m.combination_id=c.id GROUP BY m.component_type HAVING COUNT(*)>1)
   AND NOT EXISTS(SELECT 1 FROM dbo.DK3_COMBINATION_MEMBER m WHERE m.combination_id=c.id AND NOT EXISTS(SELECT 1 FROM @final q WHERE q.class_id=m.class_id))
   AND NOT EXISTS(SELECT 1 FROM @final q WHERE q.course=f.course AND NOT EXISTS(SELECT 1 FROM dbo.DK3_COMBINATION_MEMBER m WHERE m.combination_id=c.id AND m.class_id=q.class_id))))
  INSERT #HV_Issues VALUES('COMPONENT',N'Phải chọn hoặc hủy đầy đủ một tổ hợp LT–TH/thảo luận đã được công bố.',1);
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_DK3_Cart @term varchar(20)
AS
BEGIN
 SET NOCOUNT ON;DECLARE @sv varchar(20),@role varchar(20);EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@term) THROW 51004,N'Học kỳ không tồn tại.',1;
 SELECT @term term,COALESCE(c.class_json,N'[]') class_json,c.version,c.updated_at FROM (VALUES(1)) v(n) LEFT JOIN dbo.DK3_CART c ON c.student=@sv AND c.term=@term;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_DK3_SaveCart @term varchar(20),@class_json nvarchar(max),@version binary(8)=NULL
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;EXEC dbo.usp_DK3_ValidateJson @class_json;
 DECLARE @sv varchar(20),@role varchar(20);EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
 BEGIN TRY
 BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@term) THROW 51004,N'Học kỳ không tồn tại.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(@class_json) j LEFT JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=j.value AND l.ma_hk=@term WHERE l.ma_lhp IS NULL) THROW 52702,N'Giỏ có lớp không thuộc kỳ hoặc không còn tồn tại.',1;
 IF EXISTS(SELECT 1 FROM dbo.DK3_CART WHERE student=@sv AND term=@term)
 BEGIN
 UPDATE dbo.DK3_CART SET class_json=@class_json,updated_at=SYSUTCDATETIME() WHERE student=@sv AND term=@term AND version=@version;
 IF @@ROWCOUNT<>1 THROW 52704,N'Giỏ đã thay đổi ở phiên khác. Hãy tải lại trước khi lưu.',1;
 END
 ELSE BEGIN IF @version IS NOT NULL THROW 52704,N'Giỏ đã thay đổi. Hãy tải lại.',1; INSERT dbo.DK3_CART(student,term,class_json) VALUES(@sv,@term,@class_json); END;
 COMMIT;EXEC dbo.usp_DK3_Cart @term;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_DK3_Components @term varchar(20)
AS
BEGIN
 SET NOCOUNT ON;DECLARE @sv varchar(20),@role varchar(20);EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 SELECT c.id combination_id,c.term,c.course,N'['+STRING_AGG(CONVERT(nvarchar(max),'"'+STRING_ESCAPE(m.class_id,'json')+'"'),N',') WITHIN GROUP(ORDER BY m.class_id)+N']' class_json,c.version
 ,N'{'+STRING_AGG(CONVERT(nvarchar(max),'"'+STRING_ESCAPE(m.class_id,'json')+'":"'+COALESCE(m.component_type,'')+'"'),N',') WITHIN GROUP(ORDER BY m.class_id)+N'}' roles_json
 FROM dbo.DK3_COMBINATION c JOIN dbo.DK3_COMBINATION_MEMBER m ON m.combination_id=c.id WHERE c.term=@term GROUP BY c.id,c.term,c.course,c.version ORDER BY c.course,c.id;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_DK3_SaveCombination @id varchar(20),@term varchar(20),@course varchar(20),@class_json nvarchar(max),@version binary(8)=NULL,@roles_json nvarchar(max)=NULL
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;EXEC dbo.usp_DK3_ValidateJson @class_json;
 IF (SELECT COUNT(*) FROM OPENJSON(@class_json))<2 THROW 52702,N'Tổ hợp cần ít nhất hai thành phần bắt buộc.',1;
 IF ISNULL(ISJSON(@roles_json),0)<>1 OR LEFT(LTRIM(@roles_json),1)<>'{' THROW 52702,N'Khai báo rõ LT/TH/TL cho từng thành phần.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(@roles_json) WHERE type<>1 OR value NOT IN('LT','TH','TL'))
  OR (SELECT COUNT(*) FROM OPENJSON(@roles_json))<>(SELECT COUNT(*) FROM OPENJSON(@class_json))
  OR EXISTS(SELECT 1 FROM OPENJSON(@class_json) c WHERE NOT EXISTS(SELECT 1 FROM OPENJSON(@roles_json) r WHERE r.[key] COLLATE DATABASE_DEFAULT=c.value))
  OR (SELECT COUNT(*) FROM OPENJSON(@roles_json) WHERE value='LT')<>1
  OR NOT EXISTS(SELECT 1 FROM OPENJSON(@roles_json) WHERE value IN('TH','TL'))
  OR EXISTS(SELECT 1 FROM OPENJSON(@roles_json) GROUP BY value HAVING COUNT(*)>1)
  OR EXISTS(SELECT 1 FROM OPENJSON(@roles_json) GROUP BY [key] HAVING COUNT(*)>1)
  THROW 52702,N'Tổ hợp cần một LT và TH/TL; mỗi mã lớp và vai trò chỉ được khai báo một lần.',1;
 BEGIN TRY
 BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;EXEC dbo.usp_Campus_RequirePermission 'CLASS_OPERATE';
 DECLARE @before_config nvarchar(max)=(SELECT c.term,c.course,(SELECT m.class_id,m.component_type FROM dbo.DK3_COMBINATION_MEMBER m WHERE m.combination_id=c.id ORDER BY m.class_id FOR JSON PATH) members FROM dbo.DK3_COMBINATION c WHERE c.id=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 IF EXISTS(SELECT 1 FROM OPENJSON(@class_json) j LEFT JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=j.value AND l.ma_hp=@course AND l.ma_hk=@term WHERE l.ma_lhp IS NULL) THROW 52702,N'Thành phần phải cùng học phần và học kỳ.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(@roles_json) r JOIN dbo.DK3_COMBINATION_MEMBER m ON m.class_id=r.[key] COLLATE DATABASE_DEFAULT WHERE m.combination_id<>@id AND m.component_type<>r.value) THROW 52702,N'Vai trò của một lớp phải nhất quán giữa các tổ hợp.',1;
 IF EXISTS(SELECT 1 FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp WHERE d.trang_thai='DANG_KY' AND (l.ma_hp=@course AND l.ma_hk=@term OR EXISTS(SELECT 1 FROM dbo.DK3_COMBINATION c WHERE c.id=@id AND c.term=l.ma_hk AND c.course=l.ma_hp))) THROW 52703,N'Không thay cấu trúc thành phần khi học phần đã có đăng ký. Hãy dùng quy trình vận hành.',1;
 IF EXISTS(SELECT 1 FROM dbo.DK3_COMBINATION WHERE id=@id)
 BEGIN
 UPDATE dbo.DK3_COMBINATION SET term=@term,course=@course WHERE id=@id AND version=@version;
 IF @@ROWCOUNT<>1 THROW 52704,N'Tổ hợp đã thay đổi. Hãy tải lại.',1;
 DELETE dbo.DK3_COMBINATION_MEMBER WHERE combination_id=@id;
 END
 ELSE BEGIN IF @version IS NOT NULL THROW 52704,N'Tổ hợp đã thay đổi.',1;INSERT dbo.DK3_COMBINATION(id,term,course) VALUES(@id,@term,@course); END;
 INSERT dbo.DK3_COMBINATION_MEMBER(combination_id,class_id,component_type) SELECT @id,[key],value FROM OPENJSON(@roles_json);
 DECLARE @after_config nvarchar(max)=(SELECT @term term,@course course,JSON_QUERY(@roles_json) roles FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 EXEC dbo.usp_Campus_Emit 'COMPONENT_CONFIG',@id,N'Cấu hình tổ hợp thành phần bắt buộc',@before_config,@after_config;
 COMMIT;EXEC dbo.usp_DK3_Components @term;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_DK3_Result @request_id uniqueidentifier
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;DECLARE @sv varchar(20),@role varchar(20);EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
 BEGIN TRY
 BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
 SELECT request_id,operation,term,result_json,created_at FROM dbo.DK3_REQUEST WHERE student=@sv AND request_id=@request_id;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_DK3_Submit @request_id uniqueidentifier,@term varchar(20),@add_json nvarchar(max),@remove_json nvarchar(max)=N'[]',@version binary(8)=NULL,@preview bit=0,@emit bit=1,@offer_id bigint=NULL
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 EXEC dbo.usp_DK3_ValidateJson @add_json,@remove_json;
 IF @request_id IS NULL OR @request_id='00000000-0000-0000-0000-000000000000' THROW 52702,N'Mã yêu cầu không hợp lệ.',1;
 IF NOT EXISTS(SELECT 1 FROM OPENJSON(@add_json)) AND NOT EXISTS(SELECT 1 FROM OPENJSON(@remove_json)) AND @preview=0 THROW 52702,N'Chưa chọn thay đổi đăng ký.',1;
 DECLARE @sv varchar(20),@role varchar(20),@hash binary(32),@canonical_add nvarchar(max),@canonical_remove nvarchar(max),@message nvarchar(1500),@now datetime2(0),@before nvarchar(max),@after nvarchar(max),@result nvarchar(max),@op varchar(10),@entry int=@@TRANCOUNT;
 SELECT @canonical_add=N'['+COALESCE(STRING_AGG(CONVERT(nvarchar(max),'"'+value+'"'),N',') WITHIN GROUP(ORDER BY value),N'')+N']' FROM OPENJSON(@add_json);
 SELECT @canonical_remove=N'['+COALESCE(STRING_AGG(CONVERT(nvarchar(max),value),N',') WITHIN GROUP(ORDER BY value),N'')+N']' FROM OPENJSON(@remove_json);
 SET @hash=HASHBYTES('SHA2_256',CONCAT(@term,N'|',@canonical_add,N'|',@canonical_remove,N'|',CONVERT(varchar(16),@version,2)));
 IF @offer_id IS NOT NULL SET @hash=HASHBYTES('SHA2_256',CONCAT(CONVERT(varchar(64),@hash,2),'|OFFER:',@offer_id));
 SET @op=CASE WHEN @canonical_remove=N'[]' THEN 'ADD' WHEN @canonical_add=N'[]' THEN 'DROP' ELSE 'SWAP' END;
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
 CREATE TABLE #HV_Issues(code varchar(30),message nvarchar(1500),blocking bit);
 CREATE TABLE #DK3_IssueScope(code varchar(30),course varchar(20) NULL,class_id varchar(20) NULL,rule_code varchar(30) NULL);
 CREATE TABLE #DK3_Removed(class_id varchar(20) PRIMARY KEY,course varchar(20));
 CREATE TABLE #DK3_Context(total_credits int);INSERT #DK3_Context VALUES(0);
 CREATE TABLE #SEAT_Plan(class_id varchar(20) PRIMARY KEY,group_id varchar(20) NULL);
 DECLARE @changes TABLE(ma_dk bigint,ma_lhp varchar(20),action varchar(10));
 DECLARE @issues TABLE(code varchar(30),message nvarchar(1500),blocking bit);
 BEGIN TRY
 IF @preview=1 AND @entry>0 SAVE TRANSACTION DK3_Preview; ELSE BEGIN TRAN;
 EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
 -- Authentication runs after both locks, including replay. A revoked session cannot retrieve a prior result.
 IF @preview=0 AND EXISTS(SELECT 1 FROM dbo.DK3_REQUEST WHERE student=@sv AND request_id=@request_id)
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.DK3_REQUEST WHERE student=@sv AND request_id=@request_id AND payload_hash=@hash) THROW 52705,N'Mã yêu cầu đã dùng với nội dung khác. Hãy tra cứu giao dịch cũ.',1;
  COMMIT;IF @emit=1 EXEC dbo.usp_DK3_Result @request_id;RETURN;
 END;
 IF NOT EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@term) THROW 51004,N'Học kỳ không tồn tại.',1;
 IF @version IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.DK3_CART WHERE student=@sv AND term=@term AND version=@version) THROW 52704,N'Giỏ đã thay đổi ở phiên khác. Hãy tải lại.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(@add_json) j LEFT JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=j.value AND l.ma_hk=@term WHERE l.ma_lhp IS NULL) THROW 52702,N'Có lớp không tồn tại hoặc không thuộc kỳ đã chọn.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(@remove_json) j LEFT JOIN dbo.DANG_KY d ON d.ma_dk=TRY_CONVERT(bigint,j.value) AND d.ma_sv=@sv AND d.trang_thai='DANG_KY' LEFT JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp AND l.ma_hk=@term WHERE l.ma_lhp IS NULL) THROW 51017,N'Đăng ký cần bỏ không thuộc bạn, khác kỳ hoặc đã thay đổi.',1;
 INSERT #DK3_Removed SELECT l.ma_lhp,l.ma_hp FROM OPENJSON(@remove_json) j JOIN dbo.DANG_KY d ON d.ma_dk=TRY_CONVERT(bigint,j.value) JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp;
 IF EXISTS(SELECT 1 FROM #DK3_Removed r JOIN OPENJSON(@add_json) j ON j.value=r.class_id) THROW 52702,N'Không thêm và bỏ cùng một lớp trong một giao dịch.',1;
 SET @now=DATEADD(HOUR,7,SYSUTCDATETIME());
 SET @before=(SELECT d.ma_dk,d.ma_lhp,d.trang_thai FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp WHERE d.ma_sv=@sv AND l.ma_hk=@term AND d.trang_thai='DANG_KY' ORDER BY d.ma_dk FOR JSON PATH);
 IF EXISTS(SELECT 1 FROM #DK3_Removed)
 BEGIN
  IF EXISTS(SELECT 1 FROM dbo.v_HV_Holds WHERE student=@sv AND active=1 AND expires>=CONVERT(date,@now) AND scope IN('ALL','CANCEL'))
   INSERT #HV_Issues VALUES('HOLD',N'Đang có hold chặn hủy/rút môn. Liên hệ phòng đào tạo.',1);
  IF EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@term AND (trang_thai<>'DANG_MO' OR @now<tg_bat_dau OR @now>=han_huy_dk))
  BEGIN INSERT #HV_Issues VALUES('WITHDRAW',N'Ngoài thời hạn hủy; cần quyết định rút môn còn hiệu lực.',1);INSERT #DK3_IssueScope SELECT 'WITHDRAW',course,class_id,NULL FROM #DK3_Removed;END;
 END;
 -- Temporarily remove old rows inside the one transaction. A later failure restores both rows and seats.
 UPDATE d SET trang_thai='DA_HUY',ngay_huy=CASE WHEN @now<d.ngay_dk THEN d.ngay_dk ELSE @now END
 OUTPUT inserted.ma_dk,inserted.ma_lhp,'DROP' INTO @changes
 FROM dbo.DANG_KY d JOIN OPENJSON(@remove_json) j ON d.ma_dk=TRY_CONVERT(bigint,j.value);
 IF EXISTS(SELECT 1 FROM OPENJSON(@add_json)) EXEC dbo.usp_DK3_Evaluate @sv,@add_json,'REGISTER';
 ELSE EXEC dbo.usp_DK3_Evaluate @sv,N'[]','CANCEL',@hoc_ky=@term;
 EXEC dbo.usp_DK3_ValidateProposal @sv,@term,@add_json;
 EXEC dbo.usp_Seat_Check @sv,@term,@add_json,@request_id,@offer_id;
 DECLARE @consume bit=CASE WHEN @preview=1 THEN 0 ELSE 1 END;
 EXEC dbo.usp_DK3_FilterIssues @sv,@term,@add_json,@consume,@request_id;
 IF @preview=1
 BEGIN
  INSERT @issues SELECT code,message,blocking FROM #HV_Issues;
  IF @entry>0 ROLLBACK TRANSACTION DK3_Preview;ELSE ROLLBACK;
  SELECT code,message,blocking FROM @issues ORDER BY blocking DESC,code;RETURN;
 END;
 SELECT TOP(1) @message=message FROM #HV_Issues WHERE blocking=1 ORDER BY code;
 IF @message IS NOT NULL THROW 52710,@message,1;
 -- The original registration table retains its historical DA_HUY encoding. Persist the distinct
 -- approved withdrawal action in the immutable request result/audit instead of inventing grade/fee effects.
 IF EXISTS(SELECT 1 FROM #DK3_IssueScope WHERE code='WITHDRAW')
 BEGIN
  UPDATE @changes SET action='WITHDRAW' WHERE action='DROP';
  IF @op='DROP' SET @op='WITHDRAW';
 END;
 INSERT dbo.DANG_KY(ma_sv,ma_lhp,ngay_dk,trang_thai)
 OUTPUT inserted.ma_dk,inserted.ma_lhp,'ADD' INTO @changes
 SELECT @sv,value,@now,'DANG_KY' FROM OPENJSON(@add_json);
 UPDATE l SET si_so_hien_tai=counts.n,trang_thai=CASE WHEN l.trang_thai NOT IN('DANG_MO','FULL') THEN l.trang_thai WHEN counts.n>=l.si_so_max THEN 'FULL' ELSE 'DANG_MO' END
 FROM dbo.LOP_HOC_PHAN l CROSS APPLY(SELECT COUNT(*) n FROM dbo.DANG_KY d WHERE d.ma_lhp=l.ma_lhp AND d.trang_thai='DANG_KY') counts
 WHERE EXISTS(SELECT 1 FROM @changes c WHERE c.ma_lhp=l.ma_lhp);
 SET @result=(SELECT ma_dk,ma_lhp,action FROM @changes ORDER BY action,ma_lhp FOR JSON PATH);
 INSERT dbo.DK3_REQUEST(student,request_id,payload_hash,operation,term,result_json) VALUES(@sv,@request_id,@hash,@op,@term,@result);
 EXEC dbo.usp_Seat_Commit @sv,@term,@request_id,@offer_id;
 IF @version IS NOT NULL UPDATE dbo.DK3_CART SET class_json=N'[]',updated_at=SYSUTCDATETIME() WHERE student=@sv AND term=@term AND version=@version;
 SET @after=(SELECT d.ma_dk,d.ma_lhp,d.trang_thai FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp WHERE d.ma_sv=@sv AND l.ma_hk=@term AND d.trang_thai='DANG_KY' ORDER BY d.ma_dk FOR JSON PATH);
 DECLARE @event varchar(40)='REGISTRATION_'+@op,@entity varchar(100)=@sv+':'+@term;
 EXEC dbo.usp_Campus_Emit @event,@entity,N'Giao dịch đăng ký nguyên tử',@before,@after,@request_id,@sv;
 COMMIT;IF @emit=1 EXEC dbo.usp_DK3_Result @request_id;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_DK3_Precheck @term varchar(20),@add_json nvarchar(max),@remove_json nvarchar(max)=N'[]'
AS
BEGIN
 SET NOCOUNT ON;DECLARE @id uniqueidentifier=NEWID();EXEC dbo.usp_DK3_Submit @id,@term,@add_json,@remove_json,@preview=1;
END;
GO
-- Route compatibility entry points through the same component/academic/idempotent transaction engine.
CREATE OR ALTER PROCEDURE dbo.usp_HocVu_DangKyNhom @lop_json nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON;EXEC dbo.usp_DK3_ValidateJson @lop_json;
 DECLARE @term varchar(20),@id uniqueidentifier=NEWID(),@sv varchar(20),@role varchar(20);
 SELECT TOP(1) @term=l.ma_hk FROM OPENJSON(@lop_json) j JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=j.value;
 BEGIN TRY
  EXEC dbo.usp_DK3_Submit @id,@term,@lop_json,@emit=0;
 END TRY BEGIN CATCH
  IF ERROR_NUMBER()=52710 BEGIN DECLARE @message nvarchar(1500)=ERROR_MESSAGE();THROW 52610,@message,1;END;
  THROW;
 END CATCH;
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 SELECT x.ma_dk,N'Đăng ký thành công.' thong_bao FROM dbo.DK3_REQUEST r CROSS APPLY OPENJSON(r.result_json) WITH(ma_dk bigint,action varchar(10)) x WHERE r.student=@sv AND r.request_id=@id AND x.action='ADD';
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_DangKyHocPhan @ma_lhp varchar(20)
AS
BEGIN
 SET NOCOUNT ON;DECLARE @json nvarchar(max)=N'["'+STRING_ESCAPE(@ma_lhp,'json')+N'"]';EXEC dbo.usp_HocVu_DangKyNhom @json;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_HuyDangKy @ma_dk bigint
AS
BEGIN
 SET NOCOUNT ON;DECLARE @sv varchar(20),@role varchar(20),@term varchar(20),@id uniqueidentifier=NEWID(),@remove nvarchar(max)=N'['+CONVERT(nvarchar(30),@ma_dk)+N']';
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
 SELECT @term=l.ma_hk FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp WHERE d.ma_dk=@ma_dk AND d.ma_sv=@sv AND d.trang_thai='DANG_KY';
 IF @term IS NULL THROW 51017,N'Đăng ký không thuộc bạn hoặc đã hủy.',1;
 BEGIN TRY
  EXEC dbo.usp_DK3_Submit @id,@term,N'[]',@remove,@emit=0;
 END TRY BEGIN CATCH
  IF ERROR_NUMBER()=52710 BEGIN DECLARE @message nvarchar(1500)=ERROR_MESSAGE();THROW 52610,@message,1;END;
  THROW;
 END CATCH;
 SELECT @ma_dk ma_dk,N'Hủy đăng ký thành công.' thong_bao;
END;
GO
DENY EXECUTE ON dbo.usp_DK3_ValidateJson TO public;
DENY EXECUTE ON dbo.usp_DK3_Evaluate TO public;
DENY EXECUTE ON dbo.usp_DK3_ValidateProposal TO public;
DENY EXECUTE ON dbo.usp_DK3_FilterIssues TO public;
DENY EXECUTE ON dbo.usp_Seat_Check TO public;
DENY EXECUTE ON dbo.usp_Seat_Commit TO public;
DENY SELECT,INSERT,UPDATE,DELETE ON dbo.DK3_CART TO public;
DENY SELECT,INSERT,UPDATE,DELETE ON dbo.DK3_REQUEST TO public;
DENY INSERT,UPDATE,DELETE ON dbo.DK3_COMBINATION TO public;
DENY INSERT,UPDATE,DELETE ON dbo.DK3_COMBINATION_MEMBER TO public;
GRANT EXECUTE ON dbo.usp_DK3_Cart TO app_minh;
GRANT EXECUTE ON dbo.usp_DK3_SaveCart TO app_minh;
GRANT EXECUTE ON dbo.usp_DK3_Components TO app_minh;
GRANT EXECUTE ON dbo.usp_DK3_SaveCombination TO app_minh;
GRANT EXECUTE ON dbo.usp_DK3_Submit TO app_minh;
GRANT EXECUTE ON dbo.usp_DK3_Precheck TO app_minh;
GRANT EXECUTE ON dbo.usp_DK3_Result TO app_minh;
GO




CREATE OR ALTER PROCEDURE dbo.usp_HocVu_Precheck @lop_json nvarchar(max),@thao_tac varchar(20)='REGISTER',@ma_dk bigint=NULL,@ma_hk varchar(20)=NULL
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @sv varchar(20),@role varchar(20),@term varchar(20),@remove nvarchar(max);
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
 EXEC dbo.usp_DK3_ValidateJson @lop_json;
 IF @thao_tac='REGISTER'
 BEGIN
  IF (SELECT COUNT(DISTINCT l.ma_hk) FROM OPENJSON(@lop_json) j JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=j.value)<>1 THROW 52602,N'Nhóm lớp phải thuộc cùng một học kỳ.',1;
  SELECT TOP(1) @term=l.ma_hk FROM OPENJSON(@lop_json) j JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=j.value;
  EXEC dbo.usp_DK3_Precheck @term,@lop_json;RETURN;
 END;
 IF @thao_tac='CANCEL'
 BEGIN
  SELECT @term=l.ma_hk FROM dbo.DANG_KY d JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp WHERE d.ma_dk=@ma_dk AND d.ma_sv=@sv AND d.trang_thai='DANG_KY';
  IF @term IS NULL THROW 51017,N'Đăng ký không thuộc bạn hoặc đã hủy.',1;
  SET @remove=N'['+CONVERT(nvarchar(30),@ma_dk)+N']';EXEC dbo.usp_DK3_Precheck @term,N'[]',@remove;RETURN;
 END;
 IF @thao_tac<>'FINALIZE' OR @ma_hk IS NULL OR EXISTS(SELECT 1 FROM OPENJSON(@lop_json)) THROW 52602,N'Thao tác học vụ không hợp lệ.',1;
 CREATE TABLE #HV_Issues(code varchar(30),message nvarchar(1500),blocking bit);
 CREATE TABLE #DK3_IssueScope(code varchar(30),course varchar(20) NULL,class_id varchar(20) NULL,rule_code varchar(30) NULL);
 CREATE TABLE #DK3_Removed(class_id varchar(20) PRIMARY KEY,course varchar(20));
 CREATE TABLE #DK3_Context(total_credits int);INSERT #DK3_Context VALUES(0);
 BEGIN TRY
 BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
 EXEC dbo.usp_DK3_Evaluate @sv,N'[]','FINALIZE',@hoc_ky=@ma_hk;
 EXEC dbo.usp_DK3_ValidateProposal @sv,@ma_hk,N'[]';
 EXEC dbo.usp_DK3_FilterIssues @sv,@ma_hk,N'[]',0;
 COMMIT;SELECT code,message,blocking FROM #HV_Issues ORDER BY blocking DESC,code;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO

-- Preserve course-credit metadata while publishing an additive, deduplicated credit allocation for CSV/reconciliation.
DECLARE @sql nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_XemKetQuaDangKy'));
IF CHARINDEX('tin_chi_tinh',@sql)=0
BEGIN
 SET @sql=REPLACE(@sql,N'v.so_tin_chi,v.ten_gv',N'v.so_tin_chi,CASE WHEN d.trang_thai=''DANG_KY'' AND NOT EXISTS(SELECT 1 FROM dbo.DANG_KY earlier JOIN dbo.LOP_HOC_PHAN el ON el.ma_lhp=earlier.ma_lhp WHERE earlier.ma_sv=d.ma_sv AND earlier.trang_thai=''DANG_KY'' AND el.ma_hp=v.ma_hp AND el.ma_hk=v.ma_hk AND earlier.ma_lhp<d.ma_lhp) THEN v.so_tin_chi ELSE 0 END tin_chi_tinh,v.ten_gv');
 IF CHARINDEX('tin_chi_tinh',@sql)=0 THROW 52702,N'Không nhận diện hợp đồng kết quả đăng ký.',1;
 SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
END;
GO
DECLARE @sql nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID('dbo.usp_ThongKeLopHocPhan'));
IF CHARINDEX('Phase3 credit allocation',@sql)=0
BEGIN
 SET @sql=REPLACE(@sql,N'CONVERT(bigint,si_so_hien_tai)*so_tin_chi AS tong_tin_chi_dang_ky',N'-- Phase3 credit allocation: one canonical component per active student/course/term.
 CONVERT(bigint,(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=v.ma_lhp AND d.trang_thai=''DANG_KY'' AND NOT EXISTS(SELECT 1 FROM dbo.DANG_KY earlier JOIN dbo.LOP_HOC_PHAN el ON el.ma_lhp=earlier.ma_lhp WHERE earlier.ma_sv=d.ma_sv AND earlier.trang_thai=''DANG_KY'' AND el.ma_hp=v.ma_hp AND el.ma_hk=v.ma_hk AND earlier.ma_lhp<d.ma_lhp)))*v.so_tin_chi AS tong_tin_chi_dang_ky');
 SET @sql=REPLACE(@sql,N'FROM v_ThongTinLopHocPhan WHERE',N'FROM dbo.v_ThongTinLopHocPhan v WHERE');
 IF CHARINDEX('Phase3 credit allocation',@sql)=0 THROW 52702,N'Không nhận diện hợp đồng thống kê đăng ký.',1;
 SET @sql=N'ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',UPPER(@sql)),LEN(@sql));EXEC sys.sp_executesql @sql;
END;
GO
