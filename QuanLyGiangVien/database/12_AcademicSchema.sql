-- Academic documents are typed by server views. Changes require procedures; no direct table grants.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('dbo.HV_DOCUMENT','U') IS NULL
BEGIN
 CREATE TABLE dbo.HV_DOCUMENT(
  kind varchar(20) NOT NULL,id varchar(20) NOT NULL,payload nvarchar(max) NOT NULL,
  published bit NOT NULL CONSTRAINT DF_HV_Published DEFAULT 0,
  published_at datetime2(7) NULL,version rowversion NOT NULL,
  CONSTRAINT PK_HV_DOCUMENT PRIMARY KEY(kind,id),
  CONSTRAINT CK_HV_JSON CHECK(ISJSON(payload)=1),
  CONSTRAINT CK_HV_KIND CHECK(kind IN('PROGRAM','GROUP','COURSE','REQUIREMENT','ASSIGNMENT','RESULT','POLICY','OVERRIDE','HOLD','STANDING')));
 CREATE TABLE dbo.HV_HISTORY(
  sequence bigint IDENTITY PRIMARY KEY,kind varchar(20) NOT NULL,id varchar(20) NOT NULL,
  old_payload nvarchar(max) NULL,new_payload nvarchar(max) NOT NULL,reason nvarchar(500) NULL,
  actor nvarchar(128) NOT NULL,changed_at datetime2(7) NOT NULL CONSTRAINT DF_HV_Changed DEFAULT SYSUTCDATETIME());
END;
GO
CREATE OR ALTER VIEW dbo.v_HV_Programs AS
 SELECT id,CONVERT(varchar(16),CONVERT(binary(8),version),2) version,JSON_VALUE(payload,'$.name') name,
 JSON_VALUE(payload,'$.major') major,TRY_CONVERT(int,JSON_VALUE(payload,'$.cohort')) cohort,
 TRY_CONVERT(int,JSON_VALUE(payload,'$.requiredCredits')) requiredCredits,published
 FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM';
GO
CREATE OR ALTER VIEW dbo.v_HV_Groups AS
 SELECT id,CONVERT(varchar(16),CONVERT(binary(8),version),2) version,JSON_VALUE(payload,'$.program') program,JSON_VALUE(payload,'$.name') name,
 TRY_CONVERT(int,JSON_VALUE(payload,'$.requiredCredits')) requiredCredits FROM dbo.HV_DOCUMENT WHERE kind='GROUP';
GO
CREATE OR ALTER VIEW dbo.v_HV_Courses AS
 SELECT id,CONVERT(varchar(16),CONVERT(binary(8),version),2) version,JSON_VALUE(payload,'$.program') program,
 JSON_VALUE(payload,'$.course') course,JSON_VALUE(payload,'$.courseName') name,TRY_CONVERT(int,JSON_VALUE(payload,'$.credits')) credits,NULLIF(JSON_VALUE(payload,'$.group'),'') [group],
 CONVERT(bit,CASE JSON_VALUE(payload,'$.required') WHEN 'true' THEN 1 ELSE 0 END) required,
 NULLIF(JSON_VALUE(payload,'$.equivalence'),'') equivalence FROM dbo.HV_DOCUMENT WHERE kind='COURSE';
GO
CREATE OR ALTER VIEW dbo.v_HV_Requirements AS
 SELECT id,CONVERT(varchar(16),CONVERT(binary(8),version),2) version,JSON_VALUE(payload,'$.program') program,
 JSON_VALUE(payload,'$.course') course,TRY_CONVERT(int,JSON_VALUE(payload,'$.group')) [group],
 JSON_VALUE(payload,'$.requiredCourse') requiredCourse,JSON_VALUE(payload,'$.kind') kind,
 TRY_CONVERT(decimal(4,2),JSON_VALUE(payload,'$.minGrade')) minGrade FROM dbo.HV_DOCUMENT WHERE kind='REQUIREMENT';
GO
CREATE OR ALTER VIEW dbo.v_HV_Assignments AS
 SELECT id,CONVERT(varchar(16),CONVERT(binary(8),version),2) version,JSON_VALUE(payload,'$.student') student,
 JSON_VALUE(payload,'$.program') program FROM dbo.HV_DOCUMENT WHERE kind='ASSIGNMENT';
GO
CREATE OR ALTER VIEW dbo.v_HV_Results AS
 SELECT id,CONVERT(varchar(16),CONVERT(binary(8),version),2) version,JSON_VALUE(payload,'$.student') student,
 JSON_VALUE(payload,'$.course') course,JSON_VALUE(payload,'$.courseName') name,TRY_CONVERT(int,JSON_VALUE(payload,'$.credits')) credits,JSON_VALUE(payload,'$.term') term,
 COALESCE(JSON_VALUE(payload,'$.recognition'),'GRADED') recognition,TRY_CONVERT(int,JSON_VALUE(payload,'$.attempt')) attempt,TRY_CONVERT(decimal(4,2),JSON_VALUE(payload,'$.grade')) grade,
 CONVERT(bit,CASE JSON_VALUE(payload,'$.passed') WHEN 'true' THEN 1 ELSE 0 END) passed,
 JSON_VALUE(payload,'$.source') source,TRY_CONVERT(date,JSON_VALUE(payload,'$.publication')) publication,
 JSON_VALUE(payload,'$.reason') reason FROM dbo.HV_DOCUMENT WHERE kind='RESULT';
GO
CREATE OR ALTER VIEW dbo.v_HV_Policies AS
 SELECT id,CONVERT(varchar(16),CONVERT(binary(8),version),2) version,JSON_VALUE(payload,'$.name') name,JSON_VALUE(payload,'$.term') term,
 JSON_VALUE(payload,'$.standing') standing,TRY_CONVERT(int,JSON_VALUE(payload,'$.minCredits')) minCredits,
 TRY_CONVERT(int,JSON_VALUE(payload,'$.maxCredits')) maxCredits,TRY_CONVERT(decimal(4,2),JSON_VALUE(payload,'$.passGrade')) passGrade,
 CONVERT(bit,CASE JSON_VALUE(payload,'$.allowImprove') WHEN 'true' THEN 1 ELSE 0 END) allowImprove,
 TRY_CONVERT(int,JSON_VALUE(payload,'$.maxAttempts')) maxAttempts,
 CONVERT(bit,CASE JSON_VALUE(payload,'$.enforceCurriculum') WHEN 'true' THEN 1 ELSE 0 END) enforceCurriculum,
 TRY_CONVERT(decimal(4,2),JSON_VALUE(payload,'$.excellentThreshold')) excellentThreshold,
 TRY_CONVERT(int,JSON_VALUE(payload,'$.excellentMaxCredits')) excellentMaxCredits,
 CONVERT(bit,CASE JSON_VALUE(payload,'$.warningRetakeOnly') WHEN 'true' THEN 1 ELSE 0 END) warningRetakeOnly,
 JSON_VALUE(payload,'$.source') source,published,published_at FROM dbo.HV_DOCUMENT WHERE kind='POLICY';
GO
CREATE OR ALTER VIEW dbo.v_HV_Overrides AS
 SELECT id,CONVERT(varchar(16),CONVERT(binary(8),version),2) version,JSON_VALUE(payload,'$.student') student,
 JSON_VALUE(payload,'$.term') term,TRY_CONVERT(int,JSON_VALUE(payload,'$.minCredits')) minCredits,
 TRY_CONVERT(int,JSON_VALUE(payload,'$.maxCredits')) maxCredits,TRY_CONVERT(date,JSON_VALUE(payload,'$.expires')) expires,
 JSON_VALUE(payload,'$.reason') reason FROM dbo.HV_DOCUMENT WHERE kind='OVERRIDE';
GO
CREATE OR ALTER VIEW dbo.v_HV_Holds AS
 SELECT id,CONVERT(varchar(16),CONVERT(binary(8),version),2) version,JSON_VALUE(payload,'$.student') student,
 JSON_VALUE(payload,'$.scope') scope,JSON_VALUE(payload,'$.authority') authority,JSON_VALUE(payload,'$.reason') reason,
 TRY_CONVERT(date,JSON_VALUE(payload,'$.expires')) expires,
 CONVERT(bit,CASE JSON_VALUE(payload,'$.active') WHEN 'true' THEN 1 ELSE 0 END) active FROM dbo.HV_DOCUMENT WHERE kind='HOLD';
GO
CREATE OR ALTER VIEW dbo.v_HV_Standing AS
 SELECT id,CONVERT(varchar(16),CONVERT(binary(8),version),2) version,JSON_VALUE(payload,'$.student') student,
 JSON_VALUE(payload,'$.term') term,JSON_VALUE(payload,'$.status') status,JSON_VALUE(payload,'$.standing') standing,
 TRY_CONVERT(decimal(4,2),JSON_VALUE(payload,'$.gpa')) gpa,
 CONVERT(bit,CASE JSON_VALUE(payload,'$.finalTerm') WHEN 'true' THEN 1 ELSE 0 END) finalTerm,
 JSON_VALUE(payload,'$.reason') reason FROM dbo.HV_DOCUMENT WHERE kind='STANDING';
GO
CREATE OR ALTER PROCEDURE dbo.usp_HocVu_Lock
AS
BEGIN
 DECLARE @rc int;
 EXEC @rc=sys.sp_getapplock @Resource=N'Campus:Academics',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @rc<0 THROW 52601,N'Học vụ đang được cập nhật. Hãy thử lại.',1;
 EXEC @rc=sys.sp_getapplock @Resource=N'Campus:Accounts',@LockMode='Shared',@LockOwner='Transaction',@LockTimeout=10000;
 IF @rc<0 THROW 52601,N'Quyền tài khoản đang được cập nhật. Hãy thử lại.',1;
END;
GO
