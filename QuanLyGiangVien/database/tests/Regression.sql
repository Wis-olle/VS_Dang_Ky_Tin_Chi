-- Only run in a disposable database created by the verification runner.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
IF DB_NAME() NOT LIKE N'DangKyTinChi_Minh_Test[_]%'
 THROW 52099,N'Kiểm thử chỉ chạy trên database dùng một lần DangKyTinChi_Minh_Test_*.',1;
IF OBJECT_ID('dbo.usp_Minh_LuuLop','P') IS NULL
 THROW 52098,N'RED: thiếu thủ tục lưu lớp của Minh.',1;
IF OBJECT_ID('dbo.usp_Minh_DocDuLieu','P') IS NULL
 THROW 52098,N'RED: thiếu thủ tục đọc dữ liệu của Minh.',1;

-- No passwords. The test database is discarded after verification.
DELETE dbo.TAI_KHOAN WHERE ten_dang_nhap=ORIGINAL_LOGIN();
INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro) VALUES(ORIGINAL_LOGIN(),'DAO_TAO');
IF DATABASE_PRINCIPAL_ID('MinhRegressionUser') IS NULL CREATE USER MinhRegressionUser WITHOUT LOGIN;
ALTER ROLE app_minh ADD MEMBER MinhRegressionUser;
EXECUTE AS USER='MinhRegressionUser';
BEGIN TRY
 EXEC dbo.usp_Minh_LuuPhong 'TEST_PERMISSION',N'Tòa kiểm thử',60,N'Lý thuyết',1,1;
 DECLARE @direct int;
 BEGIN TRY
  SELECT @direct=COUNT(*) FROM dbo.PHONG_HOC;
  THROW 52098,N'Role ứng dụng được đọc bảng trực tiếp.',1;
 END TRY BEGIN CATCH IF ERROR_NUMBER()<>229 THROW; END CATCH;
 EXEC dbo.usp_Minh_XoaPhong 'TEST_PERMISSION';
 REVERT;
END TRY BEGIN CATCH REVERT; THROW; END CATCH;
DECLARE @before int=(SELECT COUNT(*) FROM dbo.GIANG_VIEN);
EXEC dbo.usp_Minh_LuuGiangVien 'TEST_GV',N'Giảng viên kiểm thử','19800101',N'Nam','test@example.invalid','0901111111',N'Công nghệ thông tin',N'Thạc sĩ','portrait-01',1,1;
IF (SELECT COUNT(*) FROM dbo.GIANG_VIEN)<>@before+1 THROW 52098,N'Thêm giảng viên thất bại.',1;
EXEC dbo.usp_Minh_LuuPhong 'TEST_ROOM',N'Tòa kiểm thử',60,N'Lý thuyết',1,1;
UPDATE dbo.PHONG_HOC SET ten_phong=N'Tên phòng tích hợp' WHERE ma_phong='TEST_ROOM';
EXEC dbo.usp_Minh_LuuPhong 'TEST_ROOM',N'Tòa kiểm thử',61,N'Lý thuyết',1,0;
IF (SELECT ten_phong FROM dbo.PHONG_HOC WHERE ma_phong='TEST_ROOM')<>N'Tên phòng tích hợp'
 THROW 52098,N'Sửa phòng đã ghi đè tên phòng của module khác.',1;

DECLARE @schedule nvarchar(max)=N'[{"RoomId":"TEST_ROOM","Thu":8,"StartPeriod":10,"EndPeriod":12,"StartDate":"2026-09-07","EndDate":"2026-12-27"}]';
-- Intersecting date ranges without a common Sunday must be accepted.
DECLARE @dates1 nvarchar(max)=N'[{"RoomId":"TEST_ROOM","Thu":8,"StartPeriod":10,"EndPeriod":12,"StartDate":"2026-09-13","EndDate":"2026-09-19"}]',
 @dates2 nvarchar(max)=N'[{"RoomId":"TEST_ROOM","Thu":8,"StartPeriod":10,"EndPeriod":12,"StartDate":"2026-09-14","EndDate":"2026-09-20"}]';
EXEC dbo.usp_Minh_LuuLop 'TEST_DATE1','IT101','TEST_GV','HK1-2627',60,'DANG_MO',@dates1,1;
EXEC dbo.usp_Minh_LuuLop 'TEST_DATE2','IT101','TEST_GV','HK1-2627',60,'DANG_MO',@dates2,1;
EXEC dbo.usp_Minh_XoaLop 'TEST_DATE1';
EXEC dbo.usp_Minh_XoaLop 'TEST_DATE2';
EXEC dbo.usp_Minh_LuuLop 'TEST_CLASS','IT101','TEST_GV','HK1-2627',60,'DANG_MO',@schedule,1;
DECLARE @bad nvarchar(max)=N'[{"RoomId":"TEST_ROOM","Thu":8,"StartPeriod":12,"EndPeriod":13,"StartDate":"2026-09-07","EndDate":"2026-12-27"}]';
BEGIN TRY
 EXEC dbo.usp_Minh_LuuLop 'TEST_BAD','IT101','TEST_GV','HK1-2627',60,'DANG_MO',@bad,1;
 THROW 52098,N'Đã chấp nhận tiết 13.',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>52032 THROW; END CATCH;
BEGIN TRY
 EXEC dbo.usp_Minh_LuuLop 'TEST_ROOMCLASH','IT101','GV0009','HK1-2627',60,'DANG_MO',@schedule,1;
 THROW 52098,N'Đã chấp nhận trùng phòng khác giảng viên.',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>52044 THROW; END CATCH;
DECLARE @otherRoom nvarchar(max)=REPLACE(@schedule,'TEST_ROOM','C101');
BEGIN TRY
 EXEC dbo.usp_Minh_LuuLop 'TEST_GVCLASH','IT101','TEST_GV','HK1-2627',60,'DANG_MO',@otherRoom,1;
 THROW 52098,N'Đã chấp nhận trùng giảng viên khác phòng.',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>52044 THROW; END CATCH;
BEGIN TRY
 EXEC dbo.usp_Minh_LuuLop 'TEST_CLASH','IT101','TEST_GV','HK1-2627',60,'DANG_MO',@schedule,1;
 THROW 52098,N'Đã chấp nhận trùng phòng/giảng viên.',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>52044 THROW; END CATCH;
BEGIN TRY
 EXEC dbo.usp_Minh_LuuPhong 'TEST_ROOM',N'Tòa kiểm thử',59,N'Lý thuyết',1,0;
 THROW 52098,N'Đã thu nhỏ phòng dưới sĩ số tối đa lớp.',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>52023 THROW; END CATCH;
INSERT dbo.DANG_KY(ma_sv,ma_lhp,trang_thai) VALUES('SV2024001','TEST_CLASS','DANG_KY');
BEGIN TRY
 EXEC dbo.usp_Minh_XoaLop 'TEST_CLASS';
 THROW 52098,N'Đã xóa lớp có lịch sử đăng ký.',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>52045 THROW; END CATCH;
BEGIN TRY
 EXEC dbo.usp_Minh_LuuLop 'TEST_CLASS','IT102','TEST_GV','HK1-2627',60,'DANG_MO',@schedule,0;
 THROW 52098,N'Đã đổi học phần của lớp có đăng ký.',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>52041 THROW; END CATCH;
EXEC dbo.usp_Minh_LuuLop 'TEST_CLASS','IT101','TEST_GV','HK1-2627',1,'DANG_MO',@schedule,0;
IF EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_lhp='TEST_CLASS' AND (si_so_hien_tai<>1 OR trang_thai<>'FULL'))
 THROW 52098,N'Sĩ số/FULL không lấy từ đăng ký thật.',1;
UPDATE dbo.TAI_KHOAN SET vai_tro='SINH_VIEN',ma_sv='SV2024001' WHERE ten_dang_nhap=ORIGINAL_LOGIN();
-- Read contract is checked by the C# mapper integration test (nine resultsets,
-- own registrations only, global class counts); SQL cannot INSERT EXEC nine shapes.
EXEC dbo.usp_Minh_DocDuLieu;
BEGIN TRY
 EXEC dbo.usp_Minh_XoaPhong 'TEST_ROOM';
 THROW 52098,N'Sinh viên đã ghi dữ liệu quản lý.',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>52001 THROW; END CATCH;
UPDATE dbo.TAI_KHOAN SET trang_thai=0 WHERE ten_dang_nhap=ORIGINAL_LOGIN();
BEGIN TRY
 EXEC dbo.usp_Minh_DocDuLieu;
 THROW 52098,N'Tài khoản khóa đã đọc dữ liệu.',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>51001 THROW; END CATCH;
PRINT N'PASS: CRUD, tên phòng tích hợp, 12 tiết, trùng lịch, sức chứa, khóa đăng ký, sĩ số thật, phân quyền và tài khoản khóa.';
