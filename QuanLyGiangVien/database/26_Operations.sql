SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('dbo.OP4_WORKER_RUN','U') IS NULL CREATE TABLE dbo.OP4_WORKER_RUN(
 id bigint IDENTITY PRIMARY KEY,actor int NOT NULL,term varchar(20) NOT NULL,started_utc datetime2 NOT NULL,
 ended_utc datetime2 NULL,duration_ms bigint NULL,status varchar(12) NOT NULL,error_code int NULL);
GO
CREATE OR ALTER PROCEDURE dbo.usp_Operations_Run @term varchar(20)
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @@TRANCOUNT<>0 THROW 53401,N'Worker cần giao dịch riêng.',1;
 EXEC dbo.usp_Campus_RequirePermission 'WAITLIST_OPERATE';
 IF NOT EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@term) THROW 53402,N'Học kỳ không tồn tại.',1;
 DECLARE @started datetime2=SYSUTCDATETIME(),@id bigint;
 INSERT dbo.OP4_WORKER_RUN(actor,term,started_utc,status) VALUES(dbo.ufn_Campus_AccountId(),@term,@started,'RUNNING');SET @id=SCOPE_IDENTITY();
 BEGIN TRY
  EXEC dbo.usp_Seat_Work @term;
  EXEC dbo.usp_Campus_Deliver;
  UPDATE dbo.OP4_WORKER_RUN SET ended_utc=SYSUTCDATETIME(),duration_ms=DATEDIFF_BIG(millisecond,@started,SYSUTCDATETIME()),status='SUCCEEDED' WHERE id=@id;
 END TRY BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  UPDATE dbo.OP4_WORKER_RUN SET ended_utc=SYSUTCDATETIME(),duration_ms=DATEDIFF_BIG(millisecond,@started,SYSUTCDATETIME()),status='FAILED',error_code=ERROR_NUMBER() WHERE id=@id;
  THROW;
 END CATCH;
 -- Bounded local diagnostics. Business audit/history are retained separately.
 DELETE dbo.OP4_WORKER_RUN WHERE id<@id-1000;
 SELECT id,term,started_utc,ended_utc,duration_ms,status,error_code FROM dbo.OP4_WORKER_RUN WHERE id=@id;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Operations_Snapshot @term varchar(20)
AS
BEGIN
 SET NOCOUNT ON;EXEC dbo.usp_Campus_RequirePermission 'OPERATIONS_READ';
 IF NOT EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@term) THROW 53402,N'Học kỳ không tồn tại.',1;
 SELECT l.ma_lhp,l.ma_hp,h.ten_hp,l.lifecycle,l.si_so_max,
  (SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=l.ma_lhp AND d.trang_thai='DANG_KY') enrolled,
  (SELECT COUNT(DISTINCT c.student) FROM dbo.DK3_CART c CROSS APPLY OPENJSON(c.class_json) j WHERE c.term=@term AND j.value=l.ma_lhp) cart_students,
  COALESCE(w.waiting,0) waiting,COALESCE(w.offered,0) offered,COALESCE(w.reserved_count,0) reserved,
  CASE WHEN l.si_so_max-(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=l.ma_lhp AND d.trang_thai='DANG_KY')-COALESCE(w.reserved_count,0)<0 THEN 0
   ELSE l.si_so_max-(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=l.ma_lhp AND d.trang_thai='DANG_KY')-COALESCE(w.reserved_count,0) END available,
  COALESCE(p.planned_students,0) planned_students
 FROM dbo.LOP_HOC_PHAN l JOIN dbo.HOC_PHAN h ON h.ma_hp=l.ma_hp
 LEFT JOIN dbo.v_Seat_Demand w ON w.class_id=l.ma_lhp LEFT JOIN dbo.v_AP4_Demand p ON p.course=l.ma_hp AND p.term=l.ma_hk
 WHERE l.ma_hk=@term ORDER BY l.ma_hp,l.ma_lhp;
 SELECT r.student,r.course,r.term,r.reason FROM dbo.v_AP4_PlanRisks r WHERE r.term=@term ORDER BY r.student,r.course;
 SELECT SYSUTCDATETIME() observed_utc,(SELECT version FROM dbo.CAMPUS_SCHEMA_VERSION WHERE id=1) schema_version,
  (SELECT COUNT(*) FROM dbo.CAMPUS_OUTBOX WHERE delivered_utc IS NULL) pending_events,
  (SELECT COUNT(*) FROM dbo.CAMPUS_OUTBOX WHERE delivered_utc IS NULL AND last_error IS NOT NULL) failed_events,
  (SELECT MIN(created_utc) FROM dbo.CAMPUS_OUTBOX WHERE delivered_utc IS NULL) oldest_pending_utc,
  (SELECT COUNT(*) FROM dbo.SINH_VIEN s WHERE s.trang_thai=1 AND NOT EXISTS(SELECT 1 FROM dbo.v_HV_Assignments a WHERE a.student=s.ma_sv)) students_without_curriculum,
  (SELECT COUNT(*) FROM dbo.OP4_WORKER_RUN WHERE status='RUNNING' AND started_utc<DATEADD(minute,-5,SYSUTCDATETIME())) stale_runs;
 SELECT TOP(100) id,term,started_utc,ended_utc,duration_ms,status,error_code FROM dbo.OP4_WORKER_RUN ORDER BY id DESC;
 SELECT TOP(100) batch_id,source,row_count,applied_utc FROM dbo.DK4_IMPORT_BATCH ORDER BY applied_utc DESC;
 SELECT TOP(100) id,event_type,entity,attempts,next_attempt_utc,last_error FROM dbo.CAMPUS_OUTBOX WHERE delivered_utc IS NULL ORDER BY id;
END;
GO
DENY SELECT,INSERT,UPDATE,DELETE ON dbo.OP4_WORKER_RUN TO public;
GRANT EXECUTE ON dbo.usp_Operations_Run TO app_minh;
GRANT EXECUTE ON dbo.usp_Operations_Snapshot TO app_minh;
GO
