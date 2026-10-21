SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Planner_Snapshot @term varchar(20)
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @student varchar(20),@role varchar(20);
 EXEC dbo.usp_XacThucTaiKhoan @student OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Bộ sinh thời khóa biểu dành cho sinh viên.',1;
 BEGIN TRY
 BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;
 EXEC dbo.usp_XacThucTaiKhoan @student OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Bộ sinh thời khóa biểu dành cho sinh viên.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@term) THROW 51004,N'Học kỳ không tồn tại.',1;
 DECLARE @classes TABLE(id varchar(20) PRIMARY KEY);
 INSERT @classes SELECT l.ma_lhp FROM dbo.LOP_HOC_PHAN l WHERE l.ma_hk=@term OR EXISTS(SELECT 1 FROM dbo.DANG_KY d WHERE d.ma_lhp=l.ma_lhp AND d.ma_sv=@student AND d.trang_thai='DANG_KY');
 IF (SELECT COUNT(*) FROM @classes)>2000 THROW 53202,N'Có quá nhiều lớp để sinh phương án trong một lần. Cần thu hẹp dữ liệu học kỳ.',1;
 SELECT l.ma_lhp class_id,l.ma_hp course_id,h.ten_hp course_name,CONVERT(int,h.so_tin_chi) credits,l.ma_hk term,
  CONVERT(bit,CASE WHEN l.ma_hk=@term AND h.trang_thai=1 AND l.trang_thai IN('DANG_MO','FULL') AND l.lifecycle='REGISTRATION'
   AND l.si_so_max>(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=l.ma_lhp AND d.trang_thai='DANG_KY') THEN 1 ELSE 0 END) can_add,
  CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.DANG_KY d WHERE d.ma_lhp=l.ma_lhp AND d.ma_sv=@student AND d.trang_thai='DANG_KY') THEN 1 ELSE 0 END) is_registered,
  CONVERT(bit,CASE WHEN l.lifecycle IN('COMPLETED','CANCELED','ARCHIVED') OR l.trang_thai='HUY' THEN 0 ELSE 1 END) blocks_schedule
 FROM @classes p JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=p.id JOIN dbo.HOC_PHAN h ON h.ma_hp=l.ma_hp ORDER BY l.ma_hp,l.ma_lhp;
 SELECT c.id,c.course,m.class_id,m.component_type FROM dbo.DK3_COMBINATION c JOIN dbo.DK3_COMBINATION_MEMBER m ON m.combination_id=c.id WHERE c.term=@term ORDER BY c.id,m.class_id;
 SELECT a.ma_lhp class_id,a.ngay_hoc meeting_date,CONVERT(int,a.tiet_bd) start_period,CONVERT(int,a.tiet_kt) end_period
 FROM dbo.ufn_Campus_ActualMeetings() a JOIN @classes p ON p.id=a.ma_lhp ORDER BY a.ma_lhp,a.ngay_hoc,a.tiet_bd;
 SELECT @term term,c.version,COALESCE(c.class_json,N'[]') class_json FROM (VALUES(1))v(n) LEFT JOIN dbo.DK3_CART c ON c.student=@student AND c.term=@term;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Planner_SaveCart @term varchar(20),@class_json nvarchar(max),@version binary(8)=NULL
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;EXEC dbo.usp_DK3_ValidateJson @class_json;
 DECLARE @student varchar(20),@role varchar(20),@previous nvarchar(max),@merged nvarchar(max),@message nvarchar(1500);
 EXEC dbo.usp_XacThucTaiKhoan @student OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Bộ sinh thời khóa biểu dành cho sinh viên.',1;
 CREATE TABLE #HV_Issues(code varchar(30),message nvarchar(1500),blocking bit);
 CREATE TABLE #DK3_IssueScope(code varchar(30),course varchar(20) NULL,class_id varchar(20) NULL,rule_code varchar(30) NULL);
 CREATE TABLE #DK3_Removed(class_id varchar(20) PRIMARY KEY,course varchar(20));
 CREATE TABLE #DK3_Context(total_credits int);INSERT #DK3_Context VALUES(0);
 CREATE TABLE #SEAT_Plan(class_id varchar(20) PRIMARY KEY,group_id varchar(20) NULL);
 BEGIN TRY
 BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;
 EXEC dbo.usp_XacThucTaiKhoan @student OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Bộ sinh thời khóa biểu dành cho sinh viên.',1;
 IF EXISTS(SELECT 1 FROM dbo.DK3_CART WHERE student=@student AND term=@term AND (@version IS NULL OR version<>@version))
  OR @version IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.DK3_CART WHERE student=@student AND term=@term AND version=@version)
  THROW 52704,N'Giỏ đã thay đổi sau khi sinh phương án. Tải lại trước khi đưa phương án vào giỏ.',1;
 SELECT @previous=class_json FROM dbo.DK3_CART WHERE student=@student AND term=@term;
 SELECT @merged=N'['+STRING_AGG(CONVERT(nvarchar(max),'"'+value+'"'),N',') WITHIN GROUP(ORDER BY value)+N']'
 FROM(SELECT value FROM OPENJSON(@class_json) UNION SELECT value FROM OPENJSON(COALESCE(@previous,N'[]'))) u;
 IF @merged IS NULL THROW 53202,N'Phương án không có lớp mới để đưa vào giỏ.',1;
 EXEC dbo.usp_DK3_ValidateJson @merged;
 IF EXISTS(SELECT 1 FROM OPENJSON(@merged) j LEFT JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=j.value AND l.ma_hk=@term WHERE l.ma_lhp IS NULL) THROW 53202,N'Phương án hoặc giỏ có lớp không thuộc kỳ đã chọn.',1;
 EXEC dbo.usp_DK3_Evaluate @student,@merged,'REGISTER';
 EXEC dbo.usp_DK3_ValidateProposal @student,@term,@merged;
 -- Seat/quota preview is advisory and does not create a reservation.
 IF OBJECT_ID('dbo.usp_Seat_Check','P') IS NOT NULL
 BEGIN DECLARE @request uniqueidentifier=NEWID();EXEC dbo.usp_Seat_Check @student,@term,@merged,@request;END;
 EXEC dbo.usp_DK3_FilterIssues @student,@term,@merged,0;
 SELECT TOP(1) @message=message FROM #HV_Issues WHERE blocking=1 ORDER BY code;
 IF @message IS NOT NULL THROW 53210,@message,1;
 EXEC dbo.usp_DK3_SaveCart @term,@merged,@version;
 COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
GRANT EXECUTE ON dbo.usp_Planner_Snapshot TO app_minh;
GRANT EXECUTE ON dbo.usp_Planner_SaveCart TO app_minh;
GO
