-- Replaced only by the owned UUID showcase provisioning entry point.
SET NOCOUNT ON;
DECLARE @expectedDatabase sysname=N'__SHOWCASE_DATABASE__';
IF DB_NAME()<>@expectedDatabase OR @expectedDatabase NOT LIKE N'DangKyTinChi_Minh_Test_Showcase[_]%' OR LEN(@expectedDatabase)<>64 OR RIGHT(@expectedDatabase,32) COLLATE Latin1_General_100_BIN2 LIKE '%[^a-f0-9]%'
 THROW 54980,N'Full demo assertions require the exact owned showcase database.',1;
DECLARE @checks TABLE(name nvarchar(150),actual int,minimum int);
INSERT @checks VALUES
 (N'Khoa có ngành và lớp', (SELECT COUNT(DISTINCT k.ma_khoa) FROM dbo.KHOA k JOIN dbo.NGANH n ON n.ma_khoa=k.ma_khoa JOIN dbo.LOP l ON l.ma_nganh=n.ma_nganh),4),
 (N'Ngành có thể chọn', (SELECT COUNT(*) FROM dbo.NGANH),4),
 (N'Lớp sinh viên', (SELECT COUNT(*) FROM dbo.LOP),8),
 (N'Sinh viên có hồ sơ', (SELECT COUNT(*) FROM dbo.SINH_VIEN),25),
 (N'Giảng viên', (SELECT COUNT(*) FROM dbo.GIANG_VIEN),24),
 (N'Học phần', (SELECT COUNT(*) FROM dbo.HOC_PHAN),20),
 (N'Phòng học', (SELECT COUNT(*) FROM dbo.PHONG_HOC),16),
 (N'Lớp HK1', (SELECT COUNT(*) FROM dbo.LOP_HOC_PHAN WHERE ma_hk='HK1-2627'),24),
 (N'Đăng ký có hiệu lực', (SELECT COUNT(*) FROM dbo.DANG_KY WHERE trang_thai='DANG_KY'),70),
 (N'Đăng ký đã hủy', (SELECT COUNT(*) FROM dbo.DANG_KY WHERE trang_thai='DA_HUY'),3),
 (N'Đủ mười loại tài liệu học vụ', (SELECT COUNT(DISTINCT kind) FROM dbo.HV_DOCUMENT),10),
 (N'CTĐT đã công bố', (SELECT COUNT(*) FROM dbo.HV_DOCUMENT WHERE kind='PROGRAM' AND published=1),2),
 (N'Điều kiện học phần', (SELECT COUNT(*) FROM dbo.HV_DOCUMENT WHERE kind='REQUIREMENT'),3),
 (N'Kết quả học tập', (SELECT COUNT(*) FROM dbo.HV_DOCUMENT WHERE kind='RESULT'),25),
 (N'Lịch sử chỉnh sửa kết quả', (SELECT COUNT(*) FROM dbo.HV_HISTORY WHERE kind='RESULT' AND old_payload IS NOT NULL),1),
 (N'Trạng thái kế hoạch học tập', (SELECT COUNT(DISTINCT status) FROM dbo.AP4_PLAN),4),
 (N'Kế hoạch đã đối soát nguồn hiện tại', (SELECT COUNT(*) FROM dbo.AP4_PLAN WHERE source_hash=dbo.ufn_AP4_SourceHash(student,program_id)),4),
 (N'Góp ý cố vấn', (SELECT COUNT(*) FROM dbo.AP4_COMMENT),3),
 (N'Dự kiến mở môn', (SELECT COUNT(*) FROM dbo.AP4_OFFERING),8),
 (N'Thay thế học phần một chiều', (SELECT COUNT(*) FROM dbo.AP4_SUBSTITUTION),1),
 (N'Nhóm ưu tiên', (SELECT COUNT(*) FROM dbo.SEAT_GROUP),2),
 (N'Thành viên ưu tiên tường minh', (SELECT COUNT(*) FROM dbo.SEAT_MEMBER),3),
 (N'Đợt đăng ký', (SELECT COUNT(*) FROM dbo.SEAT_ROUND),3),
 (N'Hạn ngạch', (SELECT COUNT(*) FROM dbo.SEAT_QUOTA),3),
 (N'Lịch hẹn', (SELECT COUNT(*) FROM dbo.SEAT_TICKET),3),
 (N'Trạng thái hàng chờ', (SELECT COUNT(DISTINCT status) FROM dbo.SEAT_WAITLIST),6),
 (N'SV mẫu có hàng chờ', (SELECT COUNT(*) FROM dbo.SEAT_WAITLIST WHERE student='SV2024001'),1),
 (N'SV mẫu có giỏ nháp', (SELECT COUNT(*) FROM dbo.DK3_CART c CROSS APPLY OPENJSON(c.class_json) WHERE student='SV2024001'),2),
 (N'Trạng thái đơn ngoại lệ', (SELECT COUNT(DISTINCT status) FROM dbo.DK3_EXCEPTION_REQUEST),4),
 (N'SV mẫu có đơn ngoại lệ', (SELECT COUNT(*) FROM dbo.DK3_EXCEPTION_REQUEST WHERE student='SV2024001'),4),
 (N'Tổ hợp lớp', (SELECT COUNT(*) FROM dbo.DK3_COMBINATION),1),
 (N'Ngày nghỉ', (SELECT COUNT(*) FROM dbo.CAMPUS_HOLIDAY),2),
 (N'Lịch thay đổi', (SELECT COUNT(*) FROM dbo.CAMPUS_SCHEDULE_CHANGE),2),
 (N'Thông báo SV mẫu', (SELECT COUNT(*) FROM dbo.CAMPUS_NOTIFICATION n JOIN dbo.TAI_KHOAN t ON t.ma_tk=n.account_id WHERE t.ten_dang_nhap='sv2024001'),3),
 (N'Lịch sử nhập nguồn', (SELECT COUNT(*) FROM dbo.DK4_IMPORT_BATCH),1),
 (N'Lịch sử vận hành', (SELECT COUNT(*) FROM dbo.OP4_WORKER_RUN WHERE status='SUCCEEDED'),1),
 (N'Tài khoản có mật khẩu dùng được', (SELECT COUNT(*) FROM dbo.APP_CREDENTIAL),10),
 (N'SV mẫu có thời khóa biểu', (SELECT COUNT(*) FROM dbo.DANG_KY WHERE ma_sv='SV2024001' AND trang_thai='DANG_KY'),2);
SELECT name,actual,minimum,CONVERT(bit,CASE WHEN actual>=minimum THEN 1 ELSE 0 END) passed FROM @checks ORDER BY name;
IF EXISTS(SELECT 1 FROM @checks WHERE actual<minimum) THROW 54981,N'Full demo data coverage is incomplete; see assertion rows.',1;
IF EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN l WHERE l.si_so_hien_tai<>(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=l.ma_lhp AND d.trang_thai='DANG_KY'))
 THROW 54982,N'Enrollment counts are inconsistent.',1;
IF EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN l WHERE l.si_so_max<(SELECT COUNT(*) FROM dbo.DANG_KY d WHERE d.ma_lhp=l.ma_lhp AND d.trang_thai='DANG_KY')+(SELECT COUNT(*) FROM dbo.SEAT_WAITLIST w JOIN dbo.SEAT_WAITLIST_ITEM i ON i.entry_id=w.id WHERE i.class_id=l.ma_lhp AND w.status='Offered' AND w.offer_expires_utc>SYSUTCDATETIME()))
 THROW 54982,N'Active registrations plus offered seats exceed capacity.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk='HK1-2627' AND trang_thai='DANG_MO' AND DATEADD(hour,7,SYSUTCDATETIME()) BETWEEN tg_bat_dau AND tg_ket_thuc)
 THROW 54982,N'Demo registration window is not open.',1;
EXEC dbo.usp_Campus_KiemTraSchema;
DECLARE @conflicts TABLE(class_id varchar(20),other_class varchar(20),[date] date,resource varchar(20),student varchar(20));
INSERT @conflicts EXEC dbo.usp_Lifecycle_Conflicts;
IF EXISTS(SELECT 1 FROM @conflicts) THROW 54982,N'Seed contains timetable/resource conflicts.',1;
IF EXISTS(SELECT 1 FROM dbo.CAMPUS_NOTIFICATION n JOIN dbo.CAMPUS_OUTBOX o ON o.id=n.event_id WHERE n.read_utc<o.created_utc)
 THROW 54982,N'Notification cannot be read before its event was created.',1;
DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;
SELECT CONCAT(N'PASS: ',(SELECT COUNT(*) FROM @checks),N' coverage checks, enrollment/seat conservation, active registration window, schema, timetable and constraints.') result;
