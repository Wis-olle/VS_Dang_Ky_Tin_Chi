SET NOCOUNT ON;SET XACT_ABORT ON;
DECLARE @expectedDatabase sysname=N'__SHOWCASE_DATABASE__';
IF DB_NAME()<>@expectedDatabase OR LEN(@expectedDatabase)<>64 OR @expectedDatabase NOT LIKE N'DangKyTinChi_Minh_Test_Showcase[_]%' OR RIGHT(@expectedDatabase,32) COLLATE Latin1_General_100_BIN2 LIKE '%[^a-f0-9]%'
 THROW 54980,N'Full demo requires the exact owned showcase database.',1;
IF EXISTS(SELECT 1 FROM dbo.SEAT_WAITLIST) THROW 54983,N'Create a new demo; do not reseed.',1;
DECLARE @actor int=(SELECT ma_tk FROM dbo.TAI_KHOAN WHERE ten_dang_nhap=ORIGINAL_LOGIN()),@oldRole varchar(20),@oldStudent varchar(20),
 @today date=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())),@source nvarchar(250)=N'SYNTHETIC DEMO - tình huống kiểm thử trực quan',
 @v binary(8),@p nvarchar(max),@id bigint,@configId varchar(20),@student varchar(20),@class varchar(20),@json nvarchar(max),@n int,@date date,@request uniqueidentifier;
IF @actor IS NULL THROW 54984,N'Missing local identity.',1;
SELECT @oldRole=vai_tro,@oldStudent=ma_sv FROM dbo.TAI_KHOAN WHERE ma_tk=@actor;
BEGIN TRY
UPDATE dbo.TAI_KHOAN SET vai_tro='QUAN_TRI',ma_sv=NULL WHERE ma_tk=@actor;
-- Durable real worker history before any offered seats are synthesized by workflow calls.
EXEC dbo.usp_Operations_Run 'HK1-2627';
EXEC dbo.usp_Seat_Save 'GROUP','UU_TIEN_TIEN_DO',NULL,N'{"name":"Hỗ trợ tiến độ tốt nghiệp · mô phỏng","source":"SYNTHETIC DEMO - danh sách xác nhận riêng","priority":50}';
SELECT @v=version FROM dbo.SEAT_ROUND WHERE id='DOT_CHINH';
SET @p=(SELECT 'HK1-2627' term,N'Đợt chính · đang mở để test' name,DATEADD(day,-1,SYSUTCDATETIME()) startsUtc,DATEADD(day,21,SYSUTCDATETIME()) endsUtc,
 DATEADD(day,-1,SYSUTCDATETIME()) publicUtc,10080 offerMinutes,'ELAPSED' calendarMode,CAST(1 AS bit) publishPosition FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
EXEC dbo.usp_Seat_Save 'ROUND','DOT_CHINH',@v,@p;
SET @p=(SELECT 'HK1-2627' term,N'Đợt bổ sung · chưa đến hạn' name,DATEADD(day,7,SYSUTCDATETIME()) startsUtc,DATEADD(day,21,SYSUTCDATETIME()) endsUtc,
 DATEADD(day,7,SYSUTCDATETIME()) publicUtc,120 offerMinutes,'SKIP_HOLIDAYS' calendarMode,CAST(1 AS bit) publishPosition FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
EXEC dbo.usp_Seat_Save 'ROUND','DOT_BO_SUNG',NULL,@p;
SET @p=(SELECT 'HK1-2627' term,N'Đợt trước · đã kết thúc' name,DATEADD(day,-20,SYSUTCDATETIME()) startsUtc,DATEADD(day,-10,SYSUTCDATETIME()) endsUtc,
 DATEADD(day,-20,SYSUTCDATETIME()) publicUtc,60 offerMinutes,'ELAPSED' calendarMode,CAST(1 AS bit) publishPosition FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
EXEC dbo.usp_Seat_Save 'ROUND','DOT_DA_KET_THUC',NULL,@p;
SET @n=1;
WHILE @n<=3 BEGIN
 SET @student=CASE @n WHEN 1 THEN 'SV2024001' WHEN 2 THEN 'SV2024009' ELSE 'SV2024010' END;SET @configId=CONCAT('THANH_VIEN_',@n);
 SET @p=(SELECT 'UU_TIEN_TIEN_DO' [group],@student student,@source source,DATEADD(day,30,SYSUTCDATETIME()) expiresUtc FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 EXEC dbo.usp_Seat_Save 'MEMBER',@configId,NULL,@p;
 SET @n+=1;
END;
SET @p=(SELECT 'DOT_CHINH' round,'UU_TIEN_TIEN_DO' [group],DATEADD(hour,-1,SYSUTCDATETIME()) opensUtc FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
EXEC dbo.usp_Seat_Save 'TICKET','HEN_UU_TIEN',NULL,@p;
SET @p=(SELECT 'DOT_BO_SUNG' round,'CNTT24' [group],DATEADD(day,8,SYSUTCDATETIME()) opensUtc FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
EXEC dbo.usp_Seat_Save 'TICKET','HEN_BO_SUNG',NULL,@p;
SET @n=36;
WHILE @n<=37 BEGIN
 SET @configId=CONCAT('QUOTA_',@n);SET @class=CONCAT('LHP00',@n);
 SET @p=(SELECT @class class,'CNTT24' [group],5 capacity,DATEADD(day,20,SYSUTCDATETIME()) releaseUtc FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 EXEC dbo.usp_Seat_Save 'QUOTA',@configId,NULL,@p;SET @n+=1;
END;
-- Unenrolled C# testing course has a complete LT/TH combination to experiment with.
BEGIN TRAN;
INSERT dbo.LOP_HOC_PHAN(ma_lhp,ma_hp,ma_gv,ma_hk,si_so_max,trang_thai,lifecycle) VALUES('LHP0046','IT206','GV0018','HK1-2627',25,'DANG_MO','REGISTRATION');
INSERT dbo.LICH_HOC(ma_lhp,ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt)
 SELECT 'LHP0046','D306',5,4,6,ngay_hoc_bd,ngay_hoc_kt FROM dbo.HOC_KY WHERE ma_hk='HK1-2627';
COMMIT;
EXEC dbo.usp_DK3_SaveCombination 'TO_HOP_IT206','HK1-2627','IT206',N'["LHP0035","LHP0046"]',NULL,N'{"LHP0035":"LT","LHP0046":"TH"}';
-- Native schedule commands create both history and affected-student notifications.
SET @date=DATEADD(day,14,@today);SELECT @v=lifecycle_version FROM dbo.HOC_KY WHERE ma_hk='HK1-2627';
EXEC dbo.usp_Lifecycle_Holiday 'HK1-2627',@date,1,N'SYNTHETIC DEMO - ngày nghỉ hoạt động sinh viên',@v;
SET @date=DATEADD(day,21,@today);SELECT @v=lifecycle_version FROM dbo.HOC_KY WHERE ma_hk='HK1-2627';
EXEC dbo.usp_Lifecycle_Holiday 'HK1-2627',@date,0,N'SYNTHETIC DEMO - ngày nghỉ đã được gỡ',@v;
SELECT TOP(1) @date=ngay_hoc FROM dbo.ufn_Campus_ActualMeetings() WHERE ma_lhp='LHP0002' AND ngay_hoc>=DATEADD(day,2,@today) ORDER BY ngay_hoc;
SELECT @v=phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0002';
EXEC dbo.usp_Lifecycle_ChangeSchedule @class='LHP0002',@kind='CANCEL',@source=@date,@version=@v,@reason=N'SYNTHETIC DEMO - hủy một buổi để thử lịch thực tế';
SET @date=DATEADD(day,((5-(DATEDIFF(day,'19000101',@today)%7)+7)%7)+7,@today);
SELECT @v=phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0002';
EXEC dbo.usp_Lifecycle_ChangeSchedule @class='LHP0002',@kind='MAKEUP',@target=@date,@room='D301',@lecturer='GV0004',@start=10,@end=12,@version=@v,@reason=N'SYNTHETIC DEMO - buổi học bù thứ Bảy';
SELECT @v=phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0038';
EXEC dbo.usp_Lifecycle_Transition 'LHP0038','CLOSED',@v,N'SYNTHETIC DEMO - lớp đã chốt đăng ký';
SELECT @v=phien_ban FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='LHP0038';
EXEC dbo.usp_Lifecycle_Transition 'LHP0038','TEACHING',@v,N'SYNTHETIC DEMO - lớp đang giảng dạy';
UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024001' WHERE ma_tk=@actor;
EXEC dbo.usp_DK3_SaveCart 'HK1-2627',N'["LHP0021","LHP0022"]';
SET @n=1;
WHILE @n<=4 BEGIN
 UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024001' WHERE ma_tk=@actor;
 SET @p=CASE @n WHEN 1 THEN N'SYNTHETIC DEMO - xin tăng tín chỉ, đang chờ PĐT duyệt' WHEN 2 THEN N'SYNTHETIC DEMO - xin tăng tín chỉ, đã duyệt' WHEN 3 THEN N'SYNTHETIC DEMO - thiếu căn cứ nên bị từ chối' ELSE N'SYNTHETIC DEMO - quyết định đã thu hồi' END;
 EXEC dbo.usp_DK3_ExceptionSubmit 'MAX_CREDITS','HK1-2627',@p;
 SELECT @id=MAX(id) FROM dbo.DK3_EXCEPTION_REQUEST WHERE student='SV2024001';
 UPDATE dbo.TAI_KHOAN SET vai_tro='QUAN_TRI',ma_sv=NULL WHERE ma_tk=@actor;
 IF @n>1 BEGIN
  SELECT @v=version FROM dbo.DK3_EXCEPTION_REQUEST WHERE id=@id;
  IF @n=3 EXEC dbo.usp_DK3_ExceptionDecide @id,@v,'REJECT',N'SYNTHETIC DEMO - vui lòng bổ sung minh chứng';
  ELSE BEGIN
   DECLARE @expires datetime2=DATEADD(day,14,SYSUTCDATETIME());
   EXEC dbo.usp_DK3_ExceptionDecide @id,@v,'APPROVE',N'SYNTHETIC DEMO - chấp thuận có thời hạn',@expires,2,30;
   IF @n=4 BEGIN SELECT @v=version FROM dbo.DK3_EXCEPTION_REQUEST WHERE id=@id;EXEC dbo.usp_DK3_ExceptionDecide @id,@v,'REVOKE',N'SYNTHETIC DEMO - thu hồi theo đề nghị sinh viên';END;
  END;
 END;
 SET @n+=1;
END;
BEGIN TRAN;
INSERT dbo.DANG_KY(ma_sv,ma_lhp,trang_thai) VALUES('SV2024016','LHP0045','DANG_KY');
UPDATE dbo.LOP_HOC_PHAN SET si_so_hien_tai=1,trang_thai='FULL' WHERE ma_lhp='LHP0045';
COMMIT;
SET @n=0;
WHILE @n<6 BEGIN
 SET @student=CASE WHEN @n=0 THEN 'SV2024001' ELSE CONCAT('SV20240',10+@n) END;
 SET @class=CONCAT('LHP00',40+@n);SET @json=N'["'+@class+N'"]';
 UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv=@student WHERE ma_tk=@actor;
 EXEC dbo.usp_Seat_Join 'HK1-2627',@json;
 SET @n+=1;
END;
UPDATE dbo.TAI_KHOAN SET vai_tro='QUAN_TRI',ma_sv=NULL WHERE ma_tk=@actor;
EXEC dbo.usp_Seat_Process 'HK1-2627',100;
-- Exercise the actual claim/respond procedures; expiry clock adjustment is demo-only.
UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024011' WHERE ma_tk=@actor;
SELECT @id=id,@v=version FROM dbo.SEAT_WAITLIST WHERE student='SV2024011';SET @request=NEWID();
EXEC dbo.usp_Seat_Claim @id,@v,@request;
UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024013' WHERE ma_tk=@actor;
SELECT @id=id,@v=version FROM dbo.SEAT_WAITLIST WHERE student='SV2024013';EXEC dbo.usp_Seat_Respond @id,@v,1;
UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024014' WHERE ma_tk=@actor;
SELECT @id=id,@v=version FROM dbo.SEAT_WAITLIST WHERE student='SV2024014';EXEC dbo.usp_Seat_Respond @id,@v,0;
UPDATE dbo.TAI_KHOAN SET vai_tro='QUAN_TRI',ma_sv=NULL WHERE ma_tk=@actor;
UPDATE dbo.SEAT_WAITLIST SET offer_expires_utc=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE student='SV2024012' AND status='Offered';
EXEC dbo.usp_Seat_Process 'HK1-2627',100;
-- Sourced atomic import creates real history and recognized-result rows.
SET @p=(SELECT 'SV2024023' student,'ET101' course,'HK1-2627' term,1 attempt,'TRANSFER' recognition,CAST(1 AS bit) passed,
 CONVERT(varchar(10),@today,23) publication,@source source,@source reason FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
SET @json=(SELECT NEWID() batchId,@source source,JSON_QUERY((SELECT 'RESULT' kind,'IMPORT_RESULT_23' id,JSON_QUERY(@p) payload FOR JSON PATH)) rows FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
EXEC dbo.usp_Import_Batch @json,1,1;
SET @n=0;
WHILE @n<10 AND EXISTS(SELECT 1 FROM dbo.CAMPUS_OUTBOX WHERE delivered_utc IS NULL AND next_attempt_utc<=SYSUTCDATETIME()) BEGIN EXEC dbo.usp_Campus_Deliver;SET @n+=1;END;
UPDATE n SET read_utc=SYSUTCDATETIME() FROM dbo.CAMPUS_NOTIFICATION n
 JOIN dbo.TAI_KHOAN t ON t.ma_tk=n.account_id WHERE t.ten_dang_nhap IN('sv2024001','daotao') AND n.event_id IN(SELECT TOP(2) n2.event_id FROM dbo.CAMPUS_NOTIFICATION n2 WHERE n2.account_id=n.account_id ORDER BY n2.event_id);
BEGIN TRAN;
EXEC dbo.usp_Campus_Emit 'DEMO_PENDING','DEMO:DELIVERY',N'SYNTHETIC DEMO - thông báo đang chờ thử lại để quan sát vận hành';
UPDATE dbo.CAMPUS_OUTBOX SET next_attempt_utc=DATEADD(day,1,SYSUTCDATETIME()),last_error=N'SYNTHETIC DEMO - tình huống chờ thử lại có chủ ý' WHERE id=(SELECT MAX(id) FROM dbo.CAMPUS_OUTBOX WHERE event_type='DEMO_PENDING');
COMMIT;
UPDATE dbo.TAI_KHOAN SET vai_tro=@oldRole,ma_sv=@oldStudent WHERE ma_tk=@actor;
END TRY BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 UPDATE dbo.TAI_KHOAN SET vai_tro=@oldRole,ma_sv=@oldStudent WHERE ma_tk=@actor;
 THROW;
END CATCH;
