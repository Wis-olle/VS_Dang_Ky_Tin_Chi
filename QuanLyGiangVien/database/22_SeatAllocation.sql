-- Stage 4 seat allocation. Policies are opt-in; this migration seeds no operational data.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('dbo.SEAT_GROUP','U') IS NULL CREATE TABLE dbo.SEAT_GROUP(
 id varchar(20) PRIMARY KEY,name nvarchar(150) NOT NULL,major varchar(20) NULL REFERENCES dbo.NGANH(ma_nganh),cohort int NULL,
 source nvarchar(250) NOT NULL,priority int NOT NULL DEFAULT 1000 CHECK(priority BETWEEN 0 AND 1000),version rowversion);
IF OBJECT_ID('dbo.SEAT_MEMBER','U') IS NULL CREATE TABLE dbo.SEAT_MEMBER(
 id varchar(20) PRIMARY KEY,group_id varchar(20) NOT NULL REFERENCES dbo.SEAT_GROUP(id),student varchar(20) NOT NULL REFERENCES dbo.SINH_VIEN(ma_sv),
 source nvarchar(250) NOT NULL,expires_utc datetime2 NOT NULL,version rowversion,UNIQUE(group_id,student));
IF OBJECT_ID('dbo.SEAT_ROUND','U') IS NULL CREATE TABLE dbo.SEAT_ROUND(
 id varchar(20) PRIMARY KEY,term varchar(20) NOT NULL REFERENCES dbo.HOC_KY(ma_hk),name nvarchar(150) NOT NULL,
 starts_utc datetime2 NOT NULL,ends_utc datetime2 NOT NULL,public_utc datetime2 NULL,offer_minutes int NOT NULL CHECK(offer_minutes BETWEEN 1 AND 10080),
 calendar_mode varchar(20) NOT NULL CHECK(calendar_mode IN('ELAPSED','SKIP_HOLIDAYS')),publish_position bit NOT NULL,version rowversion,
 CHECK(starts_utc<ends_utc AND (public_utc IS NULL OR public_utc BETWEEN starts_utc AND ends_utc)));
IF OBJECT_ID('dbo.SEAT_TICKET','U') IS NULL CREATE TABLE dbo.SEAT_TICKET(
 id varchar(20) PRIMARY KEY,round_id varchar(20) NOT NULL REFERENCES dbo.SEAT_ROUND(id),group_id varchar(20) NOT NULL REFERENCES dbo.SEAT_GROUP(id),
 opens_utc datetime2 NOT NULL,version rowversion,UNIQUE(round_id,group_id));
IF OBJECT_ID('dbo.SEAT_QUOTA','U') IS NULL CREATE TABLE dbo.SEAT_QUOTA(
 id varchar(20) PRIMARY KEY,class_id varchar(20) NOT NULL REFERENCES dbo.LOP_HOC_PHAN(ma_lhp),group_id varchar(20) NOT NULL REFERENCES dbo.SEAT_GROUP(id),
 capacity int NOT NULL CHECK(capacity BETWEEN 0 AND 1000),release_utc datetime2 NOT NULL,version rowversion,UNIQUE(class_id,group_id));
IF OBJECT_ID('dbo.SEAT_ALLOCATION','U') IS NULL CREATE TABLE dbo.SEAT_ALLOCATION(
 registration_id bigint PRIMARY KEY REFERENCES dbo.DANG_KY(ma_dk),group_id varchar(20) NULL REFERENCES dbo.SEAT_GROUP(id),
 allocated_utc datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
IF OBJECT_ID('dbo.SEAT_WAITLIST','U') IS NULL CREATE TABLE dbo.SEAT_WAITLIST(
 id bigint IDENTITY PRIMARY KEY,student varchar(20) NOT NULL REFERENCES dbo.SINH_VIEN(ma_sv),term varchar(20) NOT NULL REFERENCES dbo.HOC_KY(ma_hk),
 course varchar(20) NOT NULL REFERENCES dbo.HOC_PHAN(ma_hp),class_json nvarchar(max) NOT NULL CHECK(ISJSON(class_json)=1),
 priority int NOT NULL,status varchar(12) NOT NULL DEFAULT 'Waiting' CHECK(status IN('Waiting','Offered','Enrolled','Expired','Declined','Cancelled')),
 joined_utc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),offer_expires_utc datetime2 NULL,claim_request_id uniqueidentifier NULL,last_error nvarchar(500) NULL,version rowversion);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.SEAT_WAITLIST') AND name='UX_Seat_ActiveWait')
 CREATE UNIQUE INDEX UX_Seat_ActiveWait ON dbo.SEAT_WAITLIST(student,term,course) WHERE status IN('Waiting','Offered');
IF OBJECT_ID('dbo.SEAT_WAITLIST_ITEM','U') IS NULL CREATE TABLE dbo.SEAT_WAITLIST_ITEM(
 entry_id bigint NOT NULL REFERENCES dbo.SEAT_WAITLIST(id),class_id varchar(20) NOT NULL REFERENCES dbo.LOP_HOC_PHAN(ma_lhp),
 group_id varchar(20) NULL REFERENCES dbo.SEAT_GROUP(id),PRIMARY KEY(entry_id,class_id));
IF COL_LENGTH('dbo.SEAT_WAITLIST','next_attempt_utc') IS NULL ALTER TABLE dbo.SEAT_WAITLIST ADD next_attempt_utc datetime2 NOT NULL CONSTRAINT DF_Seat_NextAttempt DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('dbo.SEAT_WAITLIST','last_attempt_utc') IS NULL ALTER TABLE dbo.SEAT_WAITLIST ADD last_attempt_utc datetime2 NULL;
GO
CREATE OR ALTER FUNCTION dbo.ufn_Seat_Groups(@student varchar(20),@now datetime2)
RETURNS TABLE AS RETURN
 SELECT g.id,g.priority FROM dbo.SEAT_GROUP g WHERE EXISTS(
  SELECT 1 FROM dbo.SINH_VIEN s JOIN dbo.LOP l ON l.ma_lop=s.ma_lop WHERE s.ma_sv=@student AND s.trang_thai=1
  AND (g.major IS NOT NULL OR g.cohort IS NOT NULL) AND (g.major IS NULL OR g.major=s.ma_nganh) AND (g.cohort IS NULL OR g.cohort=l.khoa_hoc))
 OR EXISTS(SELECT 1 FROM dbo.SEAT_MEMBER m WHERE m.group_id=g.id AND m.student=@student AND m.expires_utc>@now);
GO
CREATE OR ALTER FUNCTION dbo.ufn_Seat_Rounds(@student varchar(20),@term varchar(20),@now datetime2)
RETURNS TABLE AS RETURN
 SELECT r.* FROM dbo.SEAT_ROUND r WHERE r.term=@term AND @now>=r.starts_utc AND @now<r.ends_utc
 AND (r.public_utc<=@now OR EXISTS(SELECT 1 FROM dbo.SEAT_TICKET t JOIN dbo.ufn_Seat_Groups(@student,@now) g ON g.id=t.group_id WHERE t.round_id=r.id AND t.opens_utc<=@now));
GO
CREATE OR ALTER PROCEDURE dbo.usp_Seat_Wake @entry_id bigint
AS
BEGIN
 SET NOCOUNT ON;IF @@TRANCOUNT=0 THROW 53002,N'Đánh thức hàng chờ cần giao dịch phân bổ chỗ.',1;
 UPDATE w SET next_attempt_utc=SYSUTCDATETIME(),last_attempt_utc=NULL,last_error=NULL FROM dbo.SEAT_WAITLIST w
 WHERE w.status='Waiting' AND EXISTS(SELECT 1 FROM dbo.SEAT_WAITLIST_ITEM i JOIN dbo.SEAT_WAITLIST_ITEM released ON released.class_id=i.class_id AND released.entry_id=@entry_id WHERE i.entry_id=w.id);
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Seat_ValidateCapacity @class varchar(20)
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @now datetime2=SYSUTCDATETIME(),@capacity int=(SELECT si_so_max FROM dbo.LOP_HOC_PHAN WHERE ma_lhp=@class),@active int,@offers int,@reserved int,@protected int;
 SELECT @active=COUNT(*) FROM dbo.DANG_KY WHERE ma_lhp=@class AND trang_thai='DANG_KY';
 SELECT @offers=COUNT(*) FROM dbo.SEAT_WAITLIST_ITEM i JOIN dbo.SEAT_WAITLIST w ON w.id=i.entry_id WHERE i.class_id=@class AND w.status='Offered' AND w.offer_expires_utc>@now;
 SELECT @reserved=ISNULL(SUM(capacity),0) FROM dbo.SEAT_QUOTA WHERE class_id=@class AND release_utc>@now;
 SELECT @protected=(SELECT COUNT(*) FROM dbo.DANG_KY d JOIN dbo.SEAT_ALLOCATION a ON a.registration_id=d.ma_dk JOIN dbo.SEAT_QUOTA q ON q.class_id=d.ma_lhp AND q.group_id=a.group_id AND q.release_utc>@now WHERE d.ma_lhp=@class AND d.trang_thai='DANG_KY')+
  (SELECT COUNT(*) FROM dbo.SEAT_WAITLIST_ITEM i JOIN dbo.SEAT_WAITLIST w ON w.id=i.entry_id JOIN dbo.SEAT_QUOTA q ON q.class_id=i.class_id AND q.group_id=i.group_id AND q.release_utc>@now WHERE i.class_id=@class AND w.status='Offered' AND w.offer_expires_utc>@now);
 IF @active+@offers>@capacity OR @reserved>@capacity OR @active+@offers-@protected>@capacity-@reserved
  THROW 53003,N'Sức chứa/quota mới không bảo toàn đăng ký và ghế đang được mời.',1;
 IF EXISTS(SELECT 1 FROM dbo.SEAT_QUOTA q WHERE q.class_id=@class AND q.release_utc>@now AND q.capacity<
  (SELECT COUNT(*) FROM dbo.DANG_KY d JOIN dbo.SEAT_ALLOCATION a ON a.registration_id=d.ma_dk WHERE d.ma_lhp=@class AND d.trang_thai='DANG_KY' AND a.group_id=q.group_id)+
  (SELECT COUNT(*) FROM dbo.SEAT_WAITLIST_ITEM i JOIN dbo.SEAT_WAITLIST w ON w.id=i.entry_id WHERE i.class_id=@class AND i.group_id=q.group_id AND w.status='Offered' AND w.offer_expires_utc>@now))
  THROW 53003,N'Quota mới thấp hơn số ghế đã cấp/đang mời của nhóm.',1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Seat_Save @kind varchar(12),@id varchar(20),@version binary(8)=NULL,@payload nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @id IS NULL OR LEN(@id) NOT BETWEEN 1 AND 20 OR @id COLLATE Latin1_General_100_BIN2 LIKE '%[^-A-Z0-9_]%' OR @payload IS NULL OR DATALENGTH(@payload)>32000 OR ISJSON(@payload)<>1 OR LEFT(LTRIM(@payload),1)<>'{' OR @kind IS NULL OR @kind NOT IN('ROUND','GROUP','MEMBER','TICKET','QUOTA')
  THROW 53002,N'Cấu hình phân bổ chỗ không hợp lệ.',1;
 IF DATALENGTH(JSON_VALUE(@payload,'$.name'))>300 OR DATALENGTH(JSON_VALUE(@payload,'$.source'))>500
  OR EXISTS(SELECT 1 FROM OPENJSON(@payload) WHERE [key] IN('major','student','term','class','group','round') AND value IS NOT NULL AND (LEN(value) NOT BETWEEN 1 AND 20 OR value COLLATE Latin1_General_100_BIN2 LIKE '%[^-A-Z0-9_]%'))
  THROW 53002,N'Tên/nguồn hoặc mã tham chiếu vượt giới hạn hợp lệ.',1;
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;EXEC dbo.usp_Campus_RequirePermission 'SEAT_MANAGE';
  DECLARE @before nvarchar(max),@after nvarchar(max),@entity varchar(100)=CONCAT('SEAT_',@kind,':',@id),@exists bit=0,@actual binary(8),@name nvarchar(150)=JSON_VALUE(@payload,'$.name'),@source nvarchar(250)=JSON_VALUE(@payload,'$.source'),@group varchar(20)=JSON_VALUE(@payload,'$.group'),@class varchar(20)=JSON_VALUE(@payload,'$.class');
  IF @kind='GROUP' SELECT @exists=1,@actual=version,@before=(SELECT * FROM dbo.SEAT_GROUP WHERE id=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) FROM dbo.SEAT_GROUP WHERE id=@id;
  IF @kind='ROUND' SELECT @exists=1,@actual=version,@before=(SELECT * FROM dbo.SEAT_ROUND WHERE id=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) FROM dbo.SEAT_ROUND WHERE id=@id;
  IF @kind='MEMBER' SELECT @exists=1,@actual=version,@before=(SELECT * FROM dbo.SEAT_MEMBER WHERE id=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) FROM dbo.SEAT_MEMBER WHERE id=@id;
  IF @kind='TICKET' SELECT @exists=1,@actual=version,@before=(SELECT * FROM dbo.SEAT_TICKET WHERE id=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) FROM dbo.SEAT_TICKET WHERE id=@id;
  IF @kind='QUOTA' SELECT @exists=1,@actual=version,@before=(SELECT * FROM dbo.SEAT_QUOTA WHERE id=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) FROM dbo.SEAT_QUOTA WHERE id=@id;
  IF @exists=1 AND (@version IS NULL OR @actual<>@version) OR @exists=0 AND @version IS NOT NULL THROW 53004,N'Cấu hình đã thay đổi. Hãy tải lại trước khi lưu.',1;
  IF @kind='GROUP' BEGIN
   DECLARE @major varchar(20)=JSON_VALUE(@payload,'$.major'),@cohort int=TRY_CONVERT(int,JSON_VALUE(@payload,'$.cohort')),@priority int=COALESCE(TRY_CONVERT(int,JSON_VALUE(@payload,'$.priority')),1000);
   IF NULLIF(TRIM(@name),N'') IS NULL OR NULLIF(TRIM(@source),N'') IS NULL OR JSON_VALUE(@payload,'$.cohort') IS NOT NULL AND @cohort IS NULL OR @cohort NOT BETWEEN 1900 AND 2200 OR JSON_VALUE(@payload,'$.priority') IS NOT NULL AND TRY_CONVERT(int,JSON_VALUE(@payload,'$.priority')) IS NULL OR @priority NOT BETWEEN 0 AND 1000 OR @major IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.NGANH WHERE ma_nganh=@major) THROW 53002,N'Nhóm cần tên, nguồn xác nhận và ngành/khóa hợp lệ hoặc danh sách thành viên tường minh.',1;
   IF @exists=0 INSERT dbo.SEAT_GROUP(id,name,major,cohort,source,priority) VALUES(@id,@name,@major,@cohort,@source,@priority);
   ELSE UPDATE dbo.SEAT_GROUP SET name=@name,major=@major,cohort=@cohort,source=@source,priority=@priority WHERE id=@id;
  END;
  IF @kind='ROUND' BEGIN
   DECLARE @term varchar(20)=JSON_VALUE(@payload,'$.term'),@starts datetime2=TRY_CONVERT(datetime2,JSON_VALUE(@payload,'$.startsUtc')),@ends datetime2=TRY_CONVERT(datetime2,JSON_VALUE(@payload,'$.endsUtc')),@public datetime2=TRY_CONVERT(datetime2,JSON_VALUE(@payload,'$.publicUtc')),@minutes int=TRY_CONVERT(int,JSON_VALUE(@payload,'$.offerMinutes')),@mode varchar(20)=JSON_VALUE(@payload,'$.calendarMode'),@publish bit=CASE JSON_VALUE(@payload,'$.publishPosition') WHEN 'true' THEN 1 WHEN 'false' THEN 0 END;
   IF NULLIF(TRIM(@name),N'') IS NULL OR @starts IS NULL OR @ends IS NULL OR @starts>=@ends OR JSON_VALUE(@payload,'$.publicUtc') IS NOT NULL AND @public IS NULL OR @public IS NOT NULL AND @public NOT BETWEEN @starts AND @ends OR @minutes IS NULL OR @minutes NOT BETWEEN 1 AND 10080 OR @mode IS NULL OR @mode NOT IN('ELAPSED','SKIP_HOLIDAYS') OR @publish IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@term) THROW 53002,N'Đợt cần kỳ, giờ UTC, thời hạn nhận, cách xử lý ngày nghỉ và công bố vị trí hợp lệ.',1;
   DECLARE @old_term varchar(20)=(SELECT term FROM dbo.SEAT_ROUND WHERE id=@id);
   IF EXISTS(SELECT 1 FROM dbo.SEAT_WAITLIST WHERE term IN(@term,@old_term) AND status='Offered' AND offer_expires_utc>SYSUTCDATETIME()) THROW 53003,N'Cần xử lý các lời mời còn hiệu lực trước khi đổi lịch đợt.',1;
   IF @exists=0 INSERT dbo.SEAT_ROUND(id,term,name,starts_utc,ends_utc,public_utc,offer_minutes,calendar_mode,publish_position) VALUES(@id,@term,@name,@starts,@ends,@public,@minutes,@mode,@publish);
   ELSE UPDATE dbo.SEAT_ROUND SET term=@term,name=@name,starts_utc=@starts,ends_utc=@ends,public_utc=@public,offer_minutes=@minutes,calendar_mode=@mode,publish_position=@publish WHERE id=@id;
  END;
  IF @kind='MEMBER' BEGIN
   DECLARE @student varchar(20)=JSON_VALUE(@payload,'$.student'),@expires datetime2=TRY_CONVERT(datetime2,JSON_VALUE(@payload,'$.expiresUtc'));
   IF @expires IS NULL OR @expires<=SYSUTCDATETIME() OR NULLIF(TRIM(@source),N'') IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.SEAT_GROUP WHERE id=@group) OR NOT EXISTS(SELECT 1 FROM dbo.SINH_VIEN WHERE ma_sv=@student) THROW 53002,N'Thành viên cần nhóm/SV thật, nguồn và hạn hiệu lực tương lai.',1;
   IF @exists=0 INSERT dbo.SEAT_MEMBER(id,group_id,student,source,expires_utc) VALUES(@id,@group,@student,@source,@expires);
   ELSE UPDATE dbo.SEAT_MEMBER SET group_id=@group,student=@student,source=@source,expires_utc=@expires WHERE id=@id;
  END;
  IF @kind='TICKET' BEGIN
   DECLARE @round varchar(20)=JSON_VALUE(@payload,'$.round'),@opens datetime2=TRY_CONVERT(datetime2,JSON_VALUE(@payload,'$.opensUtc'));
   IF @opens IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.SEAT_GROUP WHERE id=@group) OR NOT EXISTS(SELECT 1 FROM dbo.SEAT_ROUND WHERE id=@round AND @opens>=starts_utc AND @opens<ends_utc) THROW 53002,N'Lịch hẹn cần nhóm thật và giờ UTC nằm trong đợt.',1;
   IF @exists=0 INSERT dbo.SEAT_TICKET(id,round_id,group_id,opens_utc) VALUES(@id,@round,@group,@opens);
   ELSE UPDATE dbo.SEAT_TICKET SET round_id=@round,group_id=@group,opens_utc=@opens WHERE id=@id;
  END;
  IF @kind='QUOTA' BEGIN
   DECLARE @capacity int=TRY_CONVERT(int,JSON_VALUE(@payload,'$.capacity')),@release datetime2=TRY_CONVERT(datetime2,JSON_VALUE(@payload,'$.releaseUtc')),@old_class varchar(20)=(SELECT class_id FROM dbo.SEAT_QUOTA WHERE id=@id);
   IF @capacity IS NULL OR @capacity NOT BETWEEN 0 AND 1000 OR @release IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.SEAT_GROUP WHERE id=@group) OR NOT EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_lhp=@class AND lifecycle IN('REGISTRATION','CLOSED')) THROW 53002,N'Quota cần lớp/nhóm thật, số chỗ 0–1000 và giờ trả phần dư UTC.',1;
   IF @exists=0 INSERT dbo.SEAT_QUOTA(id,class_id,group_id,capacity,release_utc) VALUES(@id,@class,@group,@capacity,@release);
   ELSE UPDATE dbo.SEAT_QUOTA SET class_id=@class,group_id=@group,capacity=@capacity,release_utc=@release WHERE id=@id;
   EXEC dbo.usp_Seat_ValidateCapacity @class;IF @old_class IS NOT NULL AND @old_class<>@class EXEC dbo.usp_Seat_ValidateCapacity @old_class;
   UPDATE w SET next_attempt_utc=SYSUTCDATETIME(),last_attempt_utc=NULL,last_error=NULL FROM dbo.SEAT_WAITLIST w WHERE w.status='Waiting' AND EXISTS(SELECT 1 FROM dbo.SEAT_WAITLIST_ITEM i WHERE i.entry_id=w.id AND i.class_id IN(@class,@old_class));
  END;
  IF @kind IN('GROUP','MEMBER','TICKET','ROUND') UPDATE dbo.SEAT_WAITLIST SET next_attempt_utc=SYSUTCDATETIME(),last_attempt_utc=NULL,last_error=NULL WHERE status='Waiting';
  SET @after=@payload;EXEC dbo.usp_Campus_Emit 'SEAT_POLICY_CHANGED',@entity,N'Cập nhật chính sách phân bổ chỗ có phiên bản',@before,@after;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
-- Called inside the central registration transaction after temporary removals.
CREATE OR ALTER PROCEDURE dbo.usp_Seat_Check @student varchar(20),@term varchar(20),@add_json nvarchar(max),@request_id uniqueidentifier,@offer_id bigint=NULL
AS
BEGIN
 SET NOCOUNT ON;
 IF @@TRANCOUNT=0 THROW 53002,N'Kiểm chỗ cần giao dịch đăng ký.',1;
 DELETE #SEAT_Plan;
 DECLARE @now datetime2=SYSUTCDATETIME();
 IF NOT EXISTS(SELECT 1 FROM dbo.SINH_VIEN WHERE ma_sv=@student AND trang_thai=1) INSERT #HV_Issues VALUES('SEAT_PROFILE',N'Hồ sơ sinh viên không hoạt động.',1);
 IF EXISTS(SELECT 1 FROM OPENJSON(@add_json)) AND EXISTS(SELECT 1 FROM dbo.SEAT_ROUND WHERE term=@term) AND NOT EXISTS(SELECT 1 FROM dbo.ufn_Seat_Rounds(@student,@term,@now))
  INSERT #HV_Issues VALUES('SEAT_WINDOW',N'Chưa tới lịch hẹn, chưa mở hoặc đã hết đợt đăng ký dành cho bạn.',1);
 IF @offer_id IS NOT NULL AND (NOT EXISTS(SELECT 1 FROM dbo.SEAT_WAITLIST WHERE id=@offer_id AND student=@student AND term=@term AND status='Offered' AND offer_expires_utc>@now)
  OR EXISTS(SELECT value FROM OPENJSON(@add_json) EXCEPT SELECT class_id FROM dbo.SEAT_WAITLIST_ITEM WHERE entry_id=@offer_id)
  OR EXISTS(SELECT class_id FROM dbo.SEAT_WAITLIST_ITEM WHERE entry_id=@offer_id EXCEPT SELECT value FROM OPENJSON(@add_json)))
  INSERT #HV_Issues VALUES('SEAT_OFFER',N'Lời mời đã hết hạn, đổi trạng thái hoặc không khớp đủ thành phần.',1);
 DECLARE @class varchar(20),@capacity int,@active int,@offered int,@group varchar(20),@reserved int,@protected int,@general int;
 DECLARE selected CURSOR LOCAL FAST_FORWARD FOR SELECT value FROM OPENJSON(@add_json) ORDER BY value;
 OPEN selected;FETCH NEXT FROM selected INTO @class;
 WHILE @@FETCH_STATUS=0 BEGIN
  SET @group=NULL;SELECT @capacity=si_so_max FROM dbo.LOP_HOC_PHAN WHERE ma_lhp=@class;
  SELECT @active=COUNT(*) FROM dbo.DANG_KY WHERE ma_lhp=@class AND trang_thai='DANG_KY';
  SELECT @offered=COUNT(*) FROM dbo.SEAT_WAITLIST_ITEM i JOIN dbo.SEAT_WAITLIST w ON w.id=i.entry_id WHERE i.class_id=@class AND w.status='Offered' AND w.offer_expires_utc>@now AND (@offer_id IS NULL OR w.id<>@offer_id);
  IF @active+@offered>=@capacity INSERT #HV_Issues VALUES('SEAT_FULL',CONCAT(N'Lớp ',@class,N' đã hết chỗ sau khi tính các lời mời còn hạn.'),1);
  SELECT @reserved=ISNULL(SUM(capacity),0) FROM dbo.SEAT_QUOTA WHERE class_id=@class AND release_utc>@now;
  SELECT @protected=(SELECT COUNT(*) FROM dbo.DANG_KY d JOIN dbo.SEAT_ALLOCATION a ON a.registration_id=d.ma_dk JOIN dbo.SEAT_QUOTA q ON q.class_id=d.ma_lhp AND q.group_id=a.group_id AND q.release_utc>@now WHERE d.ma_lhp=@class AND d.trang_thai='DANG_KY')+
   (SELECT COUNT(*) FROM dbo.SEAT_WAITLIST_ITEM i JOIN dbo.SEAT_WAITLIST w ON w.id=i.entry_id JOIN dbo.SEAT_QUOTA q ON q.class_id=i.class_id AND q.group_id=i.group_id AND q.release_utc>@now WHERE i.class_id=@class AND w.status='Offered' AND w.offer_expires_utc>@now AND (@offer_id IS NULL OR w.id<>@offer_id));
  SET @general=@capacity-@reserved-(@active+@offered-@protected);
  SELECT TOP(1) @group=q.group_id FROM dbo.SEAT_QUOTA q JOIN dbo.ufn_Seat_Groups(@student,@now) g ON g.id=q.group_id
   WHERE q.class_id=@class AND q.release_utc>@now AND q.capacity>
    (SELECT COUNT(*) FROM dbo.DANG_KY d JOIN dbo.SEAT_ALLOCATION a ON a.registration_id=d.ma_dk WHERE d.ma_lhp=@class AND d.trang_thai='DANG_KY' AND a.group_id=q.group_id)+
    (SELECT COUNT(*) FROM dbo.SEAT_WAITLIST_ITEM i JOIN dbo.SEAT_WAITLIST w ON w.id=i.entry_id WHERE i.class_id=@class AND i.group_id=q.group_id AND w.status='Offered' AND w.offer_expires_utc>@now AND (@offer_id IS NULL OR w.id<>@offer_id))
   ORDER BY g.priority,q.group_id;
  IF @offer_id IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.SEAT_WAITLIST_ITEM i JOIN dbo.SEAT_QUOTA q ON q.class_id=i.class_id AND q.group_id=i.group_id AND q.release_utc>@now WHERE i.entry_id=@offer_id AND i.class_id=@class AND NOT EXISTS(SELECT 1 FROM dbo.ufn_Seat_Groups(@student,@now) g WHERE g.id=i.group_id))
   INSERT #HV_Issues VALUES('SEAT_GROUP',CONCAT(N'Bạn không còn thuộc nhóm được mời của lớp ',@class,N'.'),1);
  IF @group IS NULL AND @general<=0 INSERT #HV_Issues VALUES('SEAT_QUOTA',CONCAT(N'Lớp ',@class,N' chưa còn chỗ trong nhóm đủ điều kiện hoặc phần chung.'),1);
  INSERT #SEAT_Plan VALUES(@class,@group);
  FETCH NEXT FROM selected INTO @class;
 END;
 CLOSE selected;DEALLOCATE selected;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Seat_Commit @student varchar(20),@term varchar(20),@request_id uniqueidentifier,@offer_id bigint=NULL
AS
BEGIN
 SET NOCOUNT ON;IF @@TRANCOUNT=0 THROW 53002,N'Ghi chỗ cần giao dịch đăng ký.',1;
 INSERT dbo.SEAT_ALLOCATION(registration_id,group_id)
 SELECT x.ma_dk,p.group_id FROM dbo.DK3_REQUEST r CROSS APPLY OPENJSON(r.result_json) WITH(ma_dk bigint,ma_lhp varchar(20),action varchar(10)) x
 JOIN #SEAT_Plan p ON p.class_id=x.ma_lhp WHERE r.student=@student AND r.term=@term AND r.request_id=@request_id AND x.action='ADD'
 AND NOT EXISTS(SELECT 1 FROM dbo.SEAT_ALLOCATION WHERE registration_id=x.ma_dk);
 -- A real drop/withdrawal wakes relevant deferred entries immediately. Other transient
 -- eligibility changes remain bounded by the 30-second retry interval.
 UPDATE w SET next_attempt_utc=SYSUTCDATETIME(),last_attempt_utc=NULL,last_error=NULL FROM dbo.SEAT_WAITLIST w WHERE w.status='Waiting'
 AND EXISTS(SELECT 1 FROM dbo.SEAT_WAITLIST_ITEM i JOIN dbo.DK3_REQUEST r ON r.student=@student AND r.term=@term AND r.request_id=@request_id
 CROSS APPLY OPENJSON(r.result_json) WITH(ma_lhp varchar(20),action varchar(10)) x
 WHERE i.entry_id=w.id AND i.class_id=x.ma_lhp AND x.action IN('DROP','WITHDRAW'));
 IF @offer_id IS NOT NULL BEGIN
  UPDATE dbo.SEAT_WAITLIST SET status='Enrolled',claim_request_id=@request_id,last_error=NULL WHERE id=@offer_id AND student=@student AND term=@term AND status='Offered' AND offer_expires_utc>SYSUTCDATETIME();
  IF @@ROWCOUNT<>1 THROW 53005,N'Lời mời đã đổi trạng thái hoặc hết hạn trong lúc nhận.',1;
  DECLARE @entity varchar(100)=CONCAT('WAITLIST:',@offer_id);EXEC dbo.usp_Campus_Emit 'WAITLIST_ENROLLED',@entity,N'Đã nhận đủ các thành phần của lời mời',NULL,NULL,@request_id,@student;
 END;
 DECLARE @cancel_id bigint,@cancel_entity varchar(100);
 DECLARE obsolete CURSOR LOCAL FAST_FORWARD FOR SELECT w.id FROM dbo.SEAT_WAITLIST w WHERE w.student=@student AND w.term=@term AND w.status IN('Waiting','Offered') AND EXISTS(SELECT 1 FROM #SEAT_Plan p JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=p.class_id WHERE l.ma_hp=w.course);
 OPEN obsolete;FETCH NEXT FROM obsolete INTO @cancel_id;
 WHILE @@FETCH_STATUS=0 BEGIN
  UPDATE dbo.SEAT_WAITLIST SET status='Cancelled',last_error=N'Đã đăng ký học phần qua giao dịch khác.' WHERE id=@cancel_id;
  EXEC dbo.usp_Seat_Wake @cancel_id;
  SET @cancel_entity=CONCAT('WAITLIST:',@cancel_id);EXEC dbo.usp_Campus_Emit 'WAITLIST_CANCELLED',@cancel_entity,N'Đã đăng ký học phần qua giao dịch khác',NULL,NULL,@request_id,@student;
  FETCH NEXT FROM obsolete INTO @cancel_id;
 END;
 CLOSE obsolete;DEALLOCATE obsolete;
END;
GO
-- Internal academic preview: all registration rules except temporary seat shortages are checked.
CREATE OR ALTER PROCEDURE dbo.usp_Seat_Evaluate @student varchar(20),@term varchar(20),@class_json nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON;
 DELETE #HV_Issues;DELETE #DK3_IssueScope;DELETE #DK3_Removed;UPDATE #DK3_Context SET total_credits=0;
 EXEC dbo.usp_DK3_Evaluate @student,@class_json,'REGISTER';EXEC dbo.usp_DK3_ValidateProposal @student,@term,@class_json;
 DECLARE @request uniqueidentifier=NEWID();EXEC dbo.usp_Seat_Check @student,@term,@class_json,@request;
 EXEC dbo.usp_DK3_FilterIssues @student,@term,@class_json,0;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Seat_Join @term varchar(20),@class_json nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;EXEC dbo.usp_DK3_ValidateJson @class_json;
 IF NOT EXISTS(SELECT 1 FROM OPENJSON(@class_json)) THROW 53002,N'Chọn đủ lớp hoặc tổ hợp muốn chờ.',1;
 CREATE TABLE #HV_Issues(code varchar(30),message nvarchar(1500),blocking bit);CREATE TABLE #DK3_IssueScope(code varchar(30),course varchar(20),class_id varchar(20),rule_code varchar(30));
 CREATE TABLE #DK3_Removed(class_id varchar(20) PRIMARY KEY,course varchar(20));CREATE TABLE #DK3_Context(total_credits int);INSERT #DK3_Context VALUES(0);CREATE TABLE #SEAT_Plan(class_id varchar(20) PRIMARY KEY,group_id varchar(20));
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;
  DECLARE @student varchar(20),@role varchar(20),@course varchar(20),@message nvarchar(1500),@priority int,@id bigint,@canonical nvarchar(max);
  EXEC dbo.usp_XacThucTaiKhoan @student OUTPUT,@role OUTPUT;IF @role<>'SINH_VIEN' THROW 51005,N'Chỉ sinh viên được vào hàng chờ của mình.',1;
  IF EXISTS(SELECT 1 FROM OPENJSON(@class_json) j LEFT JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=j.value AND l.ma_hk=@term WHERE l.ma_lhp IS NULL) OR (SELECT COUNT(DISTINCT l.ma_hp) FROM OPENJSON(@class_json) j JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=j.value)<>1 THROW 53002,N'Mỗi yêu cầu chờ cần đủ một tổ hợp của cùng học phần/kỳ.',1;
  SELECT TOP(1) @course=l.ma_hp FROM OPENJSON(@class_json) j JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=j.value;
  IF EXISTS(SELECT 1 FROM dbo.SEAT_WAITLIST WHERE student=@student AND term=@term AND course=@course AND status IN('Waiting','Offered')) THROW 53003,N'Bạn đã có một yêu cầu đang chờ hoặc lời mời cho học phần này.',1;
  EXEC dbo.usp_Seat_Evaluate @student,@term,@class_json;
  SELECT TOP(1) @message=message FROM #HV_Issues WHERE blocking=1 AND code NOT IN('FULL','SEAT_FULL','SEAT_QUOTA') ORDER BY code;
  IF @message IS NOT NULL THROW 53010,@message,1;
  SELECT @priority=COALESCE(MIN(priority),1000) FROM dbo.ufn_Seat_Groups(@student,SYSUTCDATETIME());
  SELECT @canonical=N'['+STRING_AGG(CONVERT(nvarchar(max),'"'+value+'"'),N',') WITHIN GROUP(ORDER BY value)+N']' FROM OPENJSON(@class_json);
  INSERT dbo.SEAT_WAITLIST(student,term,course,class_json,priority) VALUES(@student,@term,@course,@canonical,@priority);SET @id=SCOPE_IDENTITY();
  INSERT dbo.SEAT_WAITLIST_ITEM(entry_id,class_id) SELECT @id,value FROM OPENJSON(@class_json);
  DECLARE @entity varchar(100)=CONCAT('WAITLIST:',@id);EXEC dbo.usp_Campus_Emit 'WAITLIST_JOINED',@entity,N'Đã vào hàng chờ đủ thành phần; chưa giữ chỗ',NULL,@canonical,NULL,@student;
  COMMIT;SELECT @id id;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Seat_Process @term varchar(20),@batch int=20
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;IF @batch IS NULL OR @batch NOT BETWEEN 1 AND 100 THROW 53002,N'Mỗi lượt xử lý 1–100 yêu cầu.',1;
 CREATE TABLE #HV_Issues(code varchar(30),message nvarchar(1500),blocking bit);CREATE TABLE #DK3_IssueScope(code varchar(30),course varchar(20),class_id varchar(20),rule_code varchar(30));
 CREATE TABLE #DK3_Removed(class_id varchar(20) PRIMARY KEY,course varchar(20));CREATE TABLE #DK3_Context(total_credits int);INSERT #DK3_Context VALUES(0);CREATE TABLE #SEAT_Plan(class_id varchar(20) PRIMARY KEY,group_id varchar(20));
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;EXEC dbo.usp_Campus_RequirePermission 'WAITLIST_OPERATE';
  DECLARE @id bigint,@student varchar(20),@json nvarchar(max),@entity varchar(100),@now datetime2=SYSUTCDATETIME(),@expires datetime2,@minutes int,@mode varchar(20),@ends datetime2,@message nvarchar(500),@offered int=0;
  DECLARE expired CURSOR LOCAL FAST_FORWARD FOR SELECT id,student FROM dbo.SEAT_WAITLIST WHERE term=@term AND status='Offered' AND offer_expires_utc<=@now ORDER BY id;
  OPEN expired;FETCH NEXT FROM expired INTO @id,@student;
  WHILE @@FETCH_STATUS=0 BEGIN
   UPDATE dbo.SEAT_WAITLIST SET status='Expired',last_error=N'Hết thời hạn nhận chỗ.' WHERE id=@id;SET @entity=CONCAT('WAITLIST:',@id);
   EXEC dbo.usp_Seat_Wake @id;
   EXEC dbo.usp_Campus_Emit 'WAITLIST_EXPIRED',@entity,N'Lời mời đã hết hạn; ghế được trả lại',NULL,NULL,NULL,@student;
   FETCH NEXT FROM expired INTO @id,@student;
  END;CLOSE expired;DEALLOCATE expired;
  -- Durable scan aging prevents starvation even when worker intervals exceed backoff.
  -- Within a fresh scan cohort preserve priority/FIFO; a temporarily blocked entry
  -- yields until re-evaluated. Same-course unexamined predecessors are protected below.
  DECLARE waiting CURSOR LOCAL STATIC FOR SELECT TOP(@batch) id,student,class_json FROM dbo.SEAT_WAITLIST WHERE term=@term AND status='Waiting' AND next_attempt_utc<=SYSUTCDATETIME() ORDER BY ISNULL(last_attempt_utc,CONVERT(datetime2,'19000101')),priority,id;
  OPEN waiting;FETCH NEXT FROM waiting INTO @id,@student,@json;
  WHILE @@FETCH_STATUS=0 BEGIN
   UPDATE dbo.SEAT_WAITLIST SET last_attempt_utc=SYSUTCDATETIME() WHERE id=@id;
   SET @message=NULL;SET @minutes=30;SET @mode='ELAPSED';SET @ends=NULL;
   SELECT TOP(1) @minutes=offer_minutes,@mode=calendar_mode,@ends=ends_utc FROM dbo.ufn_Seat_Rounds(@student,@term,SYSUTCDATETIME()) ORDER BY ends_utc,id;
   IF @mode='SKIP_HOLIDAYS' AND EXISTS(SELECT 1 FROM dbo.CAMPUS_HOLIDAY WHERE term=@term AND [date]=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())) AND active=1)
    SET @message=N'Tạm dừng mời trong ngày nghỉ theo cấu hình đợt.';
   IF @message IS NULL BEGIN
    EXEC dbo.usp_Seat_Evaluate @student,@term,@json;
    SELECT TOP(1) @message=message FROM #HV_Issues WHERE blocking=1 ORDER BY code;
   END;
   IF @message IS NULL AND EXISTS(SELECT 1 FROM dbo.SEAT_WAITLIST earlier JOIN dbo.SEAT_WAITLIST current_entry ON current_entry.id=@id
    WHERE earlier.term=current_entry.term AND earlier.course=current_entry.course AND earlier.status='Waiting' AND earlier.last_error IS NULL
    AND (earlier.priority<current_entry.priority OR earlier.priority=current_entry.priority AND earlier.id<current_entry.id))
    SET @message=N'Có yêu cầu ưu tiên/FIFO trước cùng học phần chưa được xét. Sẽ kiểm ở lượt tiếp theo.';
   IF @message IS NULL BEGIN
    SET @expires=DATEADD(minute,@minutes,SYSUTCDATETIME());
    WHILE @mode='SKIP_HOLIDAYS' AND EXISTS(SELECT 1 FROM dbo.CAMPUS_HOLIDAY WHERE term=@term AND [date]=CONVERT(date,DATEADD(hour,7,@expires)) AND active=1) SET @expires=DATEADD(day,1,@expires);
    IF @ends IS NOT NULL AND @expires>@ends SET @expires=@ends;
    UPDATE dbo.SEAT_WAITLIST SET status='Offered',offer_expires_utc=@expires,last_error=NULL WHERE id=@id AND status='Waiting';
    UPDATE i SET group_id=p.group_id FROM dbo.SEAT_WAITLIST_ITEM i JOIN #SEAT_Plan p ON p.class_id=i.class_id WHERE i.entry_id=@id;
    SET @entity=CONCAT('WAITLIST:',@id);DECLARE @after nvarchar(max)=(SELECT @expires expiresUtc FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
    EXEC dbo.usp_Campus_Emit 'WAITLIST_OFFERED',@entity,N'Đã giữ đủ thành phần có thời hạn; nhận chỗ để đăng ký',NULL,@after,NULL,@student;SET @offered+=1;
   END ELSE UPDATE dbo.SEAT_WAITLIST SET last_error=@message,next_attempt_utc=DATEADD(second,30,SYSUTCDATETIME()) WHERE id=@id;
   FETCH NEXT FROM waiting INTO @id,@student,@json;
  END;CLOSE waiting;DEALLOCATE waiting;
  COMMIT;SELECT @offered offered;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Seat_Respond @id bigint,@version binary(8),@decline bit
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;
  DECLARE @student varchar(20),@role varchar(20),@owner varchar(20),@entity varchar(100)=CONCAT('WAITLIST:',@id);
  EXEC dbo.usp_XacThucTaiKhoan @student OUTPUT,@role OUTPUT;IF @role<>'SINH_VIEN' EXEC dbo.usp_Campus_RequirePermission 'WAITLIST_OPERATE';
  SELECT @owner=student FROM dbo.SEAT_WAITLIST WHERE id=@id AND version=@version AND status IN('Waiting','Offered') AND (@role<>'SINH_VIEN' OR student=@student) AND (@decline=0 OR status='Offered');
  IF @owner IS NULL OR @decline IS NULL THROW 53004,N'Yêu cầu đã đổi trạng thái, phiên bản hoặc không thuộc bạn.',1;
  UPDATE dbo.SEAT_WAITLIST SET status=CASE WHEN @decline=1 THEN 'Declined' ELSE 'Cancelled' END WHERE id=@id;
  EXEC dbo.usp_Seat_Wake @id;
  EXEC dbo.usp_Campus_Emit 'WAITLIST_RESPONDED',@entity,N'Đã rút khỏi hàng chờ/từ chối lời mời; ghế được trả lại',NULL,NULL,NULL,@owner;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Seat_Claim @id bigint,@version binary(8),@request_id uniqueidentifier
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;
  DECLARE @student varchar(20),@role varchar(20),@term varchar(20),@json nvarchar(max);
  EXEC dbo.usp_XacThucTaiKhoan @student OUTPUT,@role OUTPUT;IF @role<>'SINH_VIEN' THROW 51005,N'Chỉ sinh viên được nhận lời mời của mình.',1;
  IF EXISTS(SELECT 1 FROM dbo.SEAT_WAITLIST WHERE id=@id AND student=@student AND status='Enrolled' AND claim_request_id=@request_id) BEGIN COMMIT;EXEC dbo.usp_DK3_Result @request_id;RETURN;END;
  SELECT @term=term,@json=class_json FROM dbo.SEAT_WAITLIST WHERE id=@id AND student=@student AND version=@version AND status='Offered' AND offer_expires_utc>SYSUTCDATETIME();
  IF @term IS NULL THROW 53004,N'Lời mời đã hết hạn, đổi phiên bản hoặc không thuộc bạn.',1;
  EXEC dbo.usp_DK3_Submit @request_id=@request_id,@term=@term,@add_json=@json,@emit=0,@offer_id=@id;
  IF NOT EXISTS(SELECT 1 FROM dbo.SEAT_WAITLIST WHERE id=@id AND status='Enrolled' AND claim_request_id=@request_id) THROW 53005,N'Chưa xác nhận nhận chỗ. Tra cứu yêu cầu trước khi thử lại.',1;
  COMMIT;EXEC dbo.usp_DK3_Result @request_id;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Seat_Snapshot @term varchar(20)
AS
BEGIN
 SET NOCOUNT ON;DECLARE @student varchar(20),@role varchar(20),@now datetime2=SYSUTCDATETIME();EXEC dbo.usp_XacThucTaiKhoan @student OUTPUT,@role OUTPUT;
 DECLARE @manage bit=CASE WHEN @role='QUAN_TRI' OR @role='DAO_TAO' AND EXISTS(SELECT 1 FROM dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='SEAT_MANAGE') THEN 1 ELSE 0 END;
 DECLARE @operate bit=CASE WHEN @manage=1 OR @role='DAO_TAO' AND EXISTS(SELECT 1 FROM dbo.CAMPUS_PERMISSION WHERE account_id=dbo.ufn_Campus_AccountId() AND permission='WAITLIST_OPERATE') THEN 1 ELSE 0 END;
 SELECT id,term,name,starts_utc,ends_utc,public_utc,offer_minutes,calendar_mode,publish_position,CONVERT(varchar(16),CONVERT(binary(8),version),2) version FROM dbo.SEAT_ROUND WHERE term=@term ORDER BY starts_utc,id;
 SELECT id,name,major,cohort,source,priority,CONVERT(varchar(16),CONVERT(binary(8),version),2) version FROM dbo.SEAT_GROUP ORDER BY priority,id;
 SELECT q.id,q.class_id,q.group_id,q.capacity,q.release_utc,CONVERT(varchar(16),CONVERT(binary(8),q.version),2) version FROM dbo.SEAT_QUOTA q JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=q.class_id WHERE l.ma_hk=@term ORDER BY q.class_id,q.group_id;
 SELECT w.id,w.student,w.course,w.class_json,w.status,w.priority,w.joined_utc,w.offer_expires_utc,w.last_error,w.next_attempt_utc,w.last_attempt_utc,w.claim_request_id,
  CASE WHEN @operate=1 OR (SELECT TOP(1) publish_position FROM dbo.ufn_Seat_Rounds(@student,@term,@now) ORDER BY ends_utc,id)=1 THEN
   CASE WHEN w.status='Waiting' THEN (SELECT COUNT(*)+1 FROM dbo.SEAT_WAITLIST earlier WHERE earlier.term=w.term AND earlier.course=w.course AND earlier.status='Waiting' AND (earlier.priority<w.priority OR earlier.priority=w.priority AND earlier.id<w.id)) END END position,
  CONVERT(varchar(16),CONVERT(binary(8),w.version),2) version FROM dbo.SEAT_WAITLIST w WHERE w.term=@term AND (@operate=1 OR @role='SINH_VIEN' AND w.student=@student) ORDER BY w.priority,w.id;
 SELECT id,group_id,student,source,expires_utc,CONVERT(varchar(16),CONVERT(binary(8),version),2) version FROM dbo.SEAT_MEMBER WHERE @manage=1 OR @role='SINH_VIEN' AND student=@student;
 SELECT t.id,t.round_id,t.group_id,t.opens_utc,CONVERT(varchar(16),CONVERT(binary(8),t.version),2) version FROM dbo.SEAT_TICKET t JOIN dbo.SEAT_ROUND r ON r.id=t.round_id WHERE r.term=@term;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Seat_Work @term varchar(20)
AS BEGIN SET NOCOUNT ON;EXEC dbo.usp_Seat_Process @term,@batch=100;END;
GO
CREATE OR ALTER VIEW dbo.v_Seat_Demand AS
 SELECT l.ma_lhp class_id,
  (SELECT COUNT(*) FROM dbo.SEAT_WAITLIST_ITEM i JOIN dbo.SEAT_WAITLIST w ON w.id=i.entry_id WHERE i.class_id=l.ma_lhp AND w.status='Waiting') waiting,
  (SELECT COUNT(*) FROM dbo.SEAT_WAITLIST_ITEM i JOIN dbo.SEAT_WAITLIST w ON w.id=i.entry_id WHERE i.class_id=l.ma_lhp AND w.status='Offered' AND w.offer_expires_utc>SYSUTCDATETIME()) offered,
  (SELECT COUNT(*) FROM dbo.SEAT_WAITLIST_ITEM i JOIN dbo.SEAT_WAITLIST w ON w.id=i.entry_id WHERE i.class_id=l.ma_lhp AND w.status='Offered' AND w.offer_expires_utc>SYSUTCDATETIME()) reserved_count
 FROM dbo.LOP_HOC_PHAN l;
GO
-- Capacity changes cannot steal a valid offer; class cancellation releases its entire waitlist group.
CREATE OR ALTER TRIGGER dbo.tr_Seat_ClassGuard ON dbo.LOP_HOC_PHAN AFTER UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF UPDATE(si_so_max) BEGIN
  DECLARE @class varchar(20);DECLARE changed CURSOR LOCAL FAST_FORWARD FOR SELECT i.ma_lhp FROM inserted i JOIN deleted d ON d.ma_lhp=i.ma_lhp WHERE i.si_so_max<>d.si_so_max;
  OPEN changed;FETCH NEXT FROM changed INTO @class;WHILE @@FETCH_STATUS=0 BEGIN EXEC dbo.usp_Seat_ValidateCapacity @class;FETCH NEXT FROM changed INTO @class;END;CLOSE changed;DEALLOCATE changed;
  UPDATE w SET next_attempt_utc=SYSUTCDATETIME(),last_attempt_utc=NULL,last_error=NULL FROM dbo.SEAT_WAITLIST w WHERE w.status='Waiting' AND EXISTS(SELECT 1 FROM dbo.SEAT_WAITLIST_ITEM item JOIN inserted i ON i.ma_lhp=item.class_id JOIN deleted d ON d.ma_lhp=i.ma_lhp WHERE item.entry_id=w.id AND i.si_so_max>d.si_so_max);
 END;
 IF UPDATE(lifecycle) BEGIN
  DECLARE @id bigint,@student varchar(20),@entity varchar(100);DECLARE cancelled CURSOR LOCAL FAST_FORWARD FOR
   SELECT DISTINCT w.id,w.student FROM dbo.SEAT_WAITLIST w JOIN dbo.SEAT_WAITLIST_ITEM item ON item.entry_id=w.id JOIN inserted i ON i.ma_lhp=item.class_id
   WHERE i.lifecycle IN('CANCELED','COMPLETED','ARCHIVED') AND w.status IN('Waiting','Offered');
  OPEN cancelled;FETCH NEXT FROM cancelled INTO @id,@student;
  WHILE @@FETCH_STATUS=0 BEGIN UPDATE dbo.SEAT_WAITLIST SET status='Cancelled',last_error=N'Lớp đã kết thúc/hủy; không còn nhận chỗ.' WHERE id=@id;SET @entity=CONCAT('WAITLIST:',@id);EXEC dbo.usp_Campus_Emit 'WAITLIST_CANCELLED',@entity,N'Lớp đã kết thúc/hủy; lời mời và hàng chờ đã được giải phóng',NULL,NULL,NULL,@student;FETCH NEXT FROM cancelled INTO @id,@student;END;
  CLOSE cancelled;DEALLOCATE cancelled;
 END;
END;
GO
DENY EXECUTE ON dbo.usp_Seat_Check TO public;DENY EXECUTE ON dbo.usp_Seat_Commit TO public;DENY EXECUTE ON dbo.usp_Seat_Evaluate TO public;DENY EXECUTE ON dbo.usp_Seat_ValidateCapacity TO public;DENY EXECUTE ON dbo.usp_Seat_Wake TO public;
DENY SELECT,INSERT,UPDATE,DELETE ON dbo.SEAT_GROUP TO public;DENY SELECT,INSERT,UPDATE,DELETE ON dbo.SEAT_MEMBER TO public;DENY SELECT,INSERT,UPDATE,DELETE ON dbo.SEAT_ROUND TO public;DENY SELECT,INSERT,UPDATE,DELETE ON dbo.SEAT_TICKET TO public;DENY SELECT,INSERT,UPDATE,DELETE ON dbo.SEAT_QUOTA TO public;DENY SELECT,INSERT,UPDATE,DELETE ON dbo.SEAT_ALLOCATION TO public;DENY SELECT,INSERT,UPDATE,DELETE ON dbo.SEAT_WAITLIST TO public;DENY SELECT,INSERT,UPDATE,DELETE ON dbo.SEAT_WAITLIST_ITEM TO public;
GRANT EXECUTE ON dbo.usp_Seat_Save TO app_minh;GRANT EXECUTE ON dbo.usp_Seat_Snapshot TO app_minh;GRANT EXECUTE ON dbo.usp_Seat_Join TO app_minh;GRANT EXECUTE ON dbo.usp_Seat_Process TO app_minh;GRANT EXECUTE ON dbo.usp_Seat_Work TO app_minh;GRANT EXECUTE ON dbo.usp_Seat_Respond TO app_minh;GRANT EXECUTE ON dbo.usp_Seat_Claim TO app_minh;
GO
