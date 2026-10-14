CREATE DATABASE DangKyTinChi
GO
USE DangKyTinChi;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
GO

-- Không ghi đè schema hoặc dữ liệu đã có.
IF EXISTS (
    SELECT 1 FROM sys.tables
    WHERE name IN (
        N'KHOA', N'NGANH', N'LOP', N'SINH_VIEN', N'GIANG_VIEN', N'TAI_KHOAN',
        N'HOC_PHAN', N'HOC_KY', N'PHONG_HOC', N'LOP_HOC_PHAN', N'LICH_HOC',
        N'DANG_KY', N'KET_QUA'
    )
)
BEGIN
    RAISERROR(N'Đã có bảng nghiệp vụ trong DangKyTinChi. Script khởi tạo không được chạy chồng lên database hiện có.', 16, 1);
    SET NOEXEC ON;
END;
GO

-- PHẦN 1: BẢNG, KHÓA VÀ CHỈ MỤC
BEGIN TRAN;
CREATE TABLE KHOA (
 ma_khoa varchar(20) NOT NULL CONSTRAINT PK_KHOA PRIMARY KEY,
 ten_khoa nvarchar(100) NOT NULL, mo_ta nvarchar(500) NULL,
 trang_thai bit NOT NULL DEFAULT 1
);
CREATE TABLE NGANH (
 ma_nganh varchar(20) NOT NULL CONSTRAINT PK_NGANH PRIMARY KEY, ten_nganh nvarchar(100) NOT NULL,
 ma_khoa varchar(20) NOT NULL CONSTRAINT FK_NGANH_ma_khoa REFERENCES KHOA(ma_khoa),
 mo_ta nvarchar(500) NULL, trang_thai bit NOT NULL DEFAULT 1
);
CREATE TABLE LOP (
 ma_lop varchar(20) NOT NULL CONSTRAINT PK_LOP PRIMARY KEY, ten_lop nvarchar(100) NOT NULL,
 ma_nganh varchar(20) NOT NULL CONSTRAINT FK_LOP_ma_nganh REFERENCES NGANH(ma_nganh),
 khoa_hoc smallint NULL, nien_khoa varchar(9) NULL,
 trang_thai bit NOT NULL DEFAULT 1,
 CONSTRAINT UQ_LOP_NGANH UNIQUE(ma_lop, ma_nganh)
);
CREATE TABLE SINH_VIEN (
 ma_sv varchar(20) NOT NULL CONSTRAINT PK_SINH_VIEN PRIMARY KEY, ho_ten nvarchar(100) NOT NULL,
 ngay_sinh date NULL, gioi_tinh nvarchar(10) NULL,
 email varchar(254) NULL, sdt varchar(20) NULL,
 ma_nganh varchar(20) NOT NULL CONSTRAINT FK_SINH_VIEN_ma_nganh REFERENCES NGANH(ma_nganh),
 ma_lop varchar(20) NOT NULL, dia_chi nvarchar(250) NULL,
 trang_thai bit NOT NULL DEFAULT 1,
 CONSTRAINT FK_SINH_VIEN_LOP_NGANH FOREIGN KEY(ma_lop,ma_nganh)
   REFERENCES LOP(ma_lop,ma_nganh)
);
CREATE TABLE GIANG_VIEN (
 ma_gv varchar(20) NOT NULL CONSTRAINT PK_GIANG_VIEN PRIMARY KEY, ho_ten nvarchar(100) NOT NULL,
 ngay_sinh date NULL, gioi_tinh nvarchar(10) NULL,
 email varchar(254) NULL, sdt varchar(20) NULL,
 ma_khoa varchar(20) NOT NULL CONSTRAINT FK_GIANG_VIEN_ma_khoa REFERENCES KHOA(ma_khoa),
 trang_thai bit NOT NULL DEFAULT 1
);
CREATE TABLE TAI_KHOAN (
 ma_tk int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TAI_KHOAN PRIMARY KEY,
 ten_dang_nhap nvarchar(128) NOT NULL UNIQUE,
 mat_khau_hash nvarchar(512) NULL, -- Windows Authentication không lưu mật khẩu
 vai_tro varchar(20) NOT NULL CHECK(vai_tro IN ('SINH_VIEN','DAO_TAO','QUAN_TRI','GIANG_VIEN')),
 trang_thai bit NOT NULL DEFAULT 1, email varchar(254) NULL,
 ma_sv varchar(20) NULL CONSTRAINT FK_TAI_KHOAN_ma_sv REFERENCES SINH_VIEN(ma_sv),
 ma_gv varchar(20) NULL CONSTRAINT FK_TAI_KHOAN_ma_gv REFERENCES GIANG_VIEN(ma_gv),
 ngay_tao datetime2(0) NOT NULL DEFAULT DATEADD(HOUR,7,SYSUTCDATETIME()),
 lan_dang_nhap_cuoi datetime2(0) NULL,
 CONSTRAINT CK_TK_SV CHECK(vai_tro <> 'SINH_VIEN' OR ma_sv IS NOT NULL),
 CONSTRAINT CK_TK_GV CHECK(vai_tro <> 'GIANG_VIEN' OR ma_gv IS NOT NULL)
);
CREATE TABLE HOC_PHAN (
 ma_hp varchar(20) NOT NULL CONSTRAINT PK_HOC_PHAN PRIMARY KEY, ten_hp nvarchar(150) NOT NULL,
 so_tin_chi tinyint NOT NULL CHECK(so_tin_chi BETWEEN 1 AND 30),
 loai_hp varchar(20) NOT NULL CHECK(loai_hp IN ('BAT_BUOC','TU_CHON')),
 tin_chi_min tinyint NULL, tin_chi_max tinyint NULL,
 trang_thai bit NOT NULL DEFAULT 1,
 -- Giữ đúng quy ước: hai cột này thuộc HOC_PHAN; chưa có quy tắc tổng tín chỉ học kỳ.
 CONSTRAINT CK_HP_TC CHECK(tin_chi_min IS NULL OR tin_chi_max IS NULL OR tin_chi_min <= tin_chi_max)
);
CREATE TABLE HOC_KY (
 ma_hk varchar(20) NOT NULL CONSTRAINT PK_HOC_KY PRIMARY KEY, nam_hoc varchar(9) NOT NULL,
 hoc_ky tinyint NOT NULL CHECK(hoc_ky BETWEEN 1 AND 3),
 tg_bat_dau datetime2(0) NOT NULL, tg_ket_thuc datetime2(0) NOT NULL,
 han_huy_dk datetime2(0) NOT NULL,
 trang_thai varchar(12) NOT NULL CHECK(trang_thai IN ('DANG_MO','DONG')),
 CONSTRAINT UQ_HK_NAM UNIQUE(nam_hoc,hoc_ky),
 CONSTRAINT CK_HK_THOI_GIAN CHECK(tg_bat_dau < tg_ket_thuc AND han_huy_dk >= tg_bat_dau)
);
CREATE TABLE PHONG_HOC (
 ma_phong varchar(20) NOT NULL CONSTRAINT PK_PHONG_HOC PRIMARY KEY, ten_phong nvarchar(80) NOT NULL,
 toa_nha nvarchar(80) NULL, suc_chua int NOT NULL CHECK(suc_chua > 0),
 loai_phong nvarchar(50) NULL, trang_thai bit NOT NULL DEFAULT 1
);
CREATE TABLE LOP_HOC_PHAN (
 ma_lhp varchar(20) NOT NULL CONSTRAINT PK_LOP_HOC_PHAN PRIMARY KEY,
 ma_hp varchar(20) NOT NULL CONSTRAINT FK_LOP_HOC_PHAN_ma_hp REFERENCES HOC_PHAN(ma_hp),
 ma_gv varchar(20) NOT NULL CONSTRAINT FK_LOP_HOC_PHAN_ma_gv REFERENCES GIANG_VIEN(ma_gv),
 ma_hk varchar(20) NOT NULL CONSTRAINT FK_LOP_HOC_PHAN_ma_hk REFERENCES HOC_KY(ma_hk),
 ma_phong varchar(20) NULL CONSTRAINT FK_LOP_HOC_PHAN_ma_phong REFERENCES PHONG_HOC(ma_phong),
 si_so_max int NOT NULL CHECK(si_so_max > 0),
 si_so_hien_tai int NOT NULL DEFAULT 0,
 trang_thai varchar(12) NOT NULL CHECK(trang_thai IN ('DANG_MO','FULL','DONG','HUY')),
 CONSTRAINT CK_LHP_SI_SO CHECK(si_so_hien_tai BETWEEN 0 AND si_so_max)
);
CREATE TABLE LICH_HOC (
 ma_lich int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LICH_HOC PRIMARY KEY,
 ma_lhp varchar(20) NOT NULL CONSTRAINT FK_LICH_HOC_ma_lhp REFERENCES LOP_HOC_PHAN(ma_lhp),
 thu tinyint NOT NULL CHECK(thu BETWEEN 2 AND 8), -- 8 = Chủ nhật
 tiet_bd tinyint NOT NULL, tiet_kt tinyint NOT NULL,
 ma_phong varchar(20) NOT NULL CONSTRAINT FK_LICH_HOC_ma_phong REFERENCES PHONG_HOC(ma_phong),
 ngay_bd date NOT NULL, ngay_kt date NOT NULL,
 CONSTRAINT CK_LICH_TIET CHECK(tiet_bd >= 1 AND tiet_kt >= tiet_bd AND tiet_kt <= 20),
 CONSTRAINT CK_LICH_NGAY CHECK(ngay_bd >= '20000101' AND ngay_kt >= ngay_bd)
);
CREATE TABLE DANG_KY (
 ma_dk bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DANG_KY PRIMARY KEY,
 ma_sv varchar(20) NOT NULL CONSTRAINT FK_DANG_KY_ma_sv REFERENCES SINH_VIEN(ma_sv),
 ma_lhp varchar(20) NOT NULL CONSTRAINT FK_DANG_KY_ma_lhp REFERENCES LOP_HOC_PHAN(ma_lhp),
 ngay_dk datetime2(0) NOT NULL DEFAULT DATEADD(HOUR,7,SYSUTCDATETIME()),
 trang_thai varchar(12) NOT NULL CHECK(trang_thai IN ('DANG_KY','DA_HUY')),
 ngay_huy datetime2(0) NULL,
 CONSTRAINT CK_DK_NGAY_HUY CHECK(
   (trang_thai = 'DANG_KY' AND ngay_huy IS NULL) OR
   (trang_thai = 'DA_HUY' AND ngay_huy IS NOT NULL AND ngay_huy >= ngay_dk))
);
CREATE UNIQUE INDEX UX_DK_DANG_HIEU_LUC ON DANG_KY(ma_sv,ma_lhp) WHERE trang_thai='DANG_KY';
CREATE INDEX IX_DK_LHP_TT ON DANG_KY(ma_lhp,trang_thai) INCLUDE(ma_sv,ngay_dk);
CREATE INDEX IX_DK_SV_TT ON DANG_KY(ma_sv,trang_thai) INCLUDE(ma_lhp,ngay_dk);
CREATE INDEX IX_LHP_HK_HP ON LOP_HOC_PHAN(ma_hk,ma_hp);
CREATE INDEX IX_LICH_LHP ON LICH_HOC(ma_lhp) INCLUDE(thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt,ma_phong);
CREATE TABLE KET_QUA (
 ma_kq bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_KET_QUA PRIMARY KEY,
 ma_sv varchar(20) NOT NULL CONSTRAINT FK_KET_QUA_ma_sv REFERENCES SINH_VIEN(ma_sv),
 ma_hp varchar(20) NOT NULL CONSTRAINT FK_KET_QUA_ma_hp REFERENCES HOC_PHAN(ma_hp),
 diem_qt decimal(4,2) NULL CHECK(diem_qt BETWEEN 0 AND 10),
 diem_gk decimal(4,2) NULL CHECK(diem_gk BETWEEN 0 AND 10),
 diem_ck decimal(4,2) NULL CHECK(diem_ck BETWEEN 0 AND 10),
 diem_tk decimal(4,2) NULL CHECK(diem_tk BETWEEN 0 AND 10),
 diem_he4 decimal(3,2) NULL CHECK(diem_he4 BETWEEN 0 AND 4),
 diem_chu varchar(3) NULL, ket_qua nvarchar(30) NULL
);
COMMIT;
GO

-- PHẦN 2: VIEW VÀ STORED PROCEDURE
-- Xác định người dùng từ SQL login thực tế, không tin mã sinh viên/vai trò do máy khách tự gửi.
CREATE OR ALTER PROCEDURE usp_XacThucTaiKhoan
 @ma_sv varchar(20) OUTPUT, @vai_tro varchar(20) OUTPUT
AS
BEGIN
 SET NOCOUNT ON;
 SET @ma_sv=NULL; SET @vai_tro=NULL;
 SELECT @ma_sv=ma_sv,@vai_tro=vai_tro
 FROM TAI_KHOAN WHERE ten_dang_nhap=ORIGINAL_LOGIN() AND trang_thai=1;
 IF @vai_tro IS NULL THROW 51001,N'Tài khoản chưa được ánh xạ hoặc đã bị khóa.',1;
 IF @vai_tro NOT IN ('SINH_VIEN','DAO_TAO','QUAN_TRI')
   THROW 51002,N'Vai trò không có quyền sử dụng phân hệ đăng ký.',1;
 IF @vai_tro='SINH_VIEN'
 BEGIN
   -- Lấy mã sinh viên chuẩn từ khóa chính để mọi tài khoản cùng sinh viên dùng cùng khóa giao dịch.
   SELECT @ma_sv=ma_sv FROM SINH_VIEN WHERE ma_sv=@ma_sv AND trang_thai=1;
   IF @@ROWCOUNT=0 THROW 51003,N'Hồ sơ sinh viên không hoạt động.',1;
 END;
END;
GO
CREATE OR ALTER PROCEDURE usp_LayNguoiDung
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20);
 EXEC usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 SELECT t.ma_tk,t.ma_sv,t.vai_tro,COALESCE(s.ho_ten,N'Phòng đào tạo') AS ho_ten
 FROM TAI_KHOAN t LEFT JOIN SINH_VIEN s ON s.ma_sv=t.ma_sv
 WHERE t.ten_dang_nhap=ORIGINAL_LOGIN();
END;
GO
CREATE OR ALTER VIEW v_ThongTinLopHocPhan
AS
SELECT l.ma_lhp,l.ma_hp,h.ten_hp,h.so_tin_chi,h.loai_hp,l.ma_hk,
 l.ma_gv,g.ho_ten AS ten_gv,l.si_so_max,
 CONVERT(int,COALESCE(d.so_sv,0)) AS si_so_hien_tai,
 l.si_so_max-CONVERT(int,COALESCE(d.so_sv,0)) AS so_cho_con,
 l.trang_thai AS trang_thai_luu,
 CASE WHEN l.trang_thai IN ('DONG','HUY') THEN l.trang_thai
      WHEN COALESCE(d.so_sv,0)>=l.si_so_max THEN 'FULL' ELSE 'DANG_MO' END AS trang_thai,
 h.trang_thai AS hp_hoat_dong,
 COALESCE(x.lich_hoc,N'Chưa xếp lịch') AS lich_hoc
FROM LOP_HOC_PHAN l JOIN HOC_PHAN h ON h.ma_hp=l.ma_hp
JOIN GIANG_VIEN g ON g.ma_gv=l.ma_gv
LEFT JOIN (SELECT ma_lhp,COUNT(*) AS so_sv FROM DANG_KY WHERE trang_thai='DANG_KY' GROUP BY ma_lhp) d
 ON d.ma_lhp=l.ma_lhp
OUTER APPLY(
 SELECT STRING_AGG(CONVERT(nvarchar(max),CONCAT(
  CASE WHEN c.thu=8 THEN N'CN' ELSE CONCAT(N'T',c.thu) END,
  N' tiết ',c.tiet_bd,N'–',c.tiet_kt,N' · ',p.ten_phong,N' · ',
  CONVERT(varchar(10),c.ngay_bd,103),N' đến ',CONVERT(varchar(10),c.ngay_kt,103))),N'; ')
  WITHIN GROUP (ORDER BY c.thu,c.tiet_bd,c.ngay_bd) AS lich_hoc
 FROM LICH_HOC c JOIN PHONG_HOC p ON p.ma_phong=c.ma_phong WHERE c.ma_lhp=l.ma_lhp
) x;
GO
CREATE OR ALTER PROCEDURE usp_LayHocKy
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20);
 EXEC usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 SELECT ma_hk,CONCAT(nam_hoc,N' • Học kỳ ',hoc_ky) AS ten_hk
 FROM HOC_KY ORDER BY nam_hoc DESC,hoc_ky DESC;
END;
GO
CREATE OR ALTER PROCEDURE usp_KiemTraThoiHan @ma_hk varchar(20)
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20),@now datetime2(0)=CONVERT(datetime2(0), DATEADD(HOUR, 7, SYSUTCDATETIME()));
 EXEC usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF NOT EXISTS(SELECT 1 FROM HOC_KY WHERE ma_hk=@ma_hk)
   THROW 51004,N'Học kỳ không tồn tại.',1;
 SELECT ma_hk,tg_bat_dau,tg_ket_thuc,han_huy_dk,trang_thai,
 @now AS gio_may_chu,
 CONVERT(bit,CASE WHEN trang_thai='DANG_MO' AND @now>=tg_bat_dau AND @now<tg_ket_thuc THEN 1 ELSE 0 END) AS cho_dang_ky,
 CONVERT(bit,CASE WHEN trang_thai='DANG_MO' AND @now>=tg_bat_dau AND @now<han_huy_dk THEN 1 ELSE 0 END) AS cho_huy
 FROM HOC_KY WHERE ma_hk=@ma_hk;
END;
GO
CREATE OR ALTER PROCEDURE usp_TraCuuLopDangMo
 @ma_hk varchar(20),@tu_khoa nvarchar(100)=N'',@chi_con_cho bit=0
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20);
 EXEC usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 SELECT ma_lhp,ma_hp,ten_hp,so_tin_chi,loai_hp,ten_gv,lich_hoc,
 si_so_hien_tai,si_so_max,so_cho_con,trang_thai
 FROM v_ThongTinLopHocPhan
 WHERE ma_hk=@ma_hk AND trang_thai IN ('DANG_MO','FULL') AND hp_hoat_dong=1
 AND (@chi_con_cho=0 OR so_cho_con>0)
 AND (CHARINDEX(@tu_khoa,ma_lhp)>0 OR CHARINDEX(@tu_khoa,ma_hp)>0 OR CHARINDEX(@tu_khoa,ten_hp)>0)
 ORDER BY ma_hp,ma_lhp;
END;
GO
CREATE OR ALTER PROCEDURE usp_DangKyHocPhan @ma_lhp varchar(20)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @sv varchar(20),@role varchar(20),@hk varchar(20),@hp varchar(20),
 @state varchar(12),@hkstate varchar(12),@max int,@count int,@tc int,
 @start datetime2(0),@end datetime2(0),@now datetime2(0),@rc int,@resource nvarchar(255),@id bigint;
 EXEC usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
 BEGIN TRY
  BEGIN TRAN;
  -- Tuần tự hóa các thao tác đăng ký/hủy của cùng một sinh viên.
  SET @resource=N'DangKyTinChi:SV:'+@sv;
  EXEC @rc=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
  IF @rc<0 THROW 51006,N'Hệ thống đang bận. Vui lòng thử lại.',1;
  SELECT @hk=ma_hk FROM LOP_HOC_PHAN WHERE ma_lhp=@ma_lhp;
  IF @hk IS NULL THROW 51007,N'Lớp học phần không tồn tại.',1;
  -- Giữ trạng thái và thời hạn học kỳ ổn định trong giao dịch.
  SELECT @start=tg_bat_dau,@end=tg_ket_thuc,@hkstate=trang_thai
  FROM HOC_KY WITH(HOLDLOCK) WHERE ma_hk=@hk;
  -- Khóa dòng lớp để kiểm tra chỗ và ghi đăng ký trong cùng giao dịch.
  SELECT @hp=ma_hp,@max=si_so_max,@state=trang_thai
  FROM LOP_HOC_PHAN WITH(UPDLOCK,HOLDLOCK) WHERE ma_lhp=@ma_lhp AND ma_hk=@hk;
  IF @hp IS NULL THROW 51008,N'Lớp đã thay đổi học kỳ. Hãy tải lại.',1;
  SET @now=CONVERT(datetime2(0), DATEADD(HOUR, 7, SYSUTCDATETIME())); -- Lấy giờ sau khi đã chờ khóa.
  IF @hkstate<>'DANG_MO' OR @now<@start OR @now>=@end
    THROW 51009,N'Chưa đến hạn, đã hết hạn hoặc đợt đăng ký đã đóng.',1;
  IF @state NOT IN ('DANG_MO','FULL') THROW 51010,N'Lớp không nhận đăng ký.',1;
  SELECT @tc=so_tin_chi FROM HOC_PHAN WITH(HOLDLOCK) WHERE ma_hp=@hp AND trang_thai=1;
  IF @tc IS NULL THROW 51011,N'Học phần không hoạt động.',1;
  IF NOT EXISTS(SELECT 1 FROM LICH_HOC WITH(HOLDLOCK) WHERE ma_lhp=@ma_lhp)
    THROW 51012,N'Lớp chưa có lịch học; chưa thể đăng ký.',1;
  IF EXISTS(SELECT 1 FROM DANG_KY d JOIN LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp
    WHERE d.ma_sv=@sv AND d.trang_thai='DANG_KY' AND l.ma_hk=@hk AND l.ma_hp=@hp)
    THROW 51013,N'Bạn đã đăng ký học phần này trong học kỳ.',1;
  SELECT @count=COUNT(*) FROM DANG_KY WHERE ma_lhp=@ma_lhp AND trang_thai='DANG_KY';
  IF @count>=@max THROW 51014,N'Lớp đã đủ sĩ số.',1;
  -- Kiểm tra thứ, tiết, khoảng ngày và ngày học thực sự trong phần giao.
  IF EXISTS(
   SELECT 1 FROM LICH_HOC a WITH(HOLDLOCK)
   JOIN LICH_HOC b WITH(HOLDLOCK) ON a.thu=b.thu AND a.tiet_bd<=b.tiet_kt AND b.tiet_bd<=a.tiet_kt
   JOIN DANG_KY d ON d.ma_lhp=b.ma_lhp AND d.ma_sv=@sv AND d.trang_thai='DANG_KY'
   CROSS APPLY(SELECT CASE WHEN a.ngay_bd>b.ngay_bd THEN a.ngay_bd ELSE b.ngay_bd END AS bd,
                      CASE WHEN a.ngay_kt<b.ngay_kt THEN a.ngay_kt ELSE b.ngay_kt END AS kt) p
   WHERE a.ma_lhp=@ma_lhp AND p.bd<=p.kt
   AND DATEADD(DAY,(a.thu-2-DATEDIFF(DAY,CONVERT(date,'19000101'),p.bd)%7+7)%7,p.bd)<=p.kt
  ) THROW 51016,N'Lớp này trùng lịch với lớp bạn đã đăng ký.',1;
  INSERT DANG_KY(ma_sv,ma_lhp,ngay_dk,trang_thai) VALUES(@sv,@ma_lhp,@now,'DANG_KY');
  SET @id=CONVERT(bigint,SCOPE_IDENTITY());
  UPDATE LOP_HOC_PHAN SET si_so_hien_tai=@count+1,
   trang_thai=CASE WHEN @count+1>=@max THEN 'FULL' ELSE 'DANG_MO' END WHERE ma_lhp=@ma_lhp;
  COMMIT;
  SELECT @id AS ma_dk,N'Đăng ký thành công.' AS thong_bao;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH
END;
GO
CREATE OR ALTER PROCEDURE usp_HuyDangKy @ma_dk bigint
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @sv varchar(20),@role varchar(20),@lhp varchar(20),@hk varchar(20),@rc int,
 @start datetime2(0),@limit datetime2(0),@now datetime2(0),@state varchar(12),
 @resource nvarchar(255),@count int;
 EXEC usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Chức năng này chỉ dành cho sinh viên.',1;
 BEGIN TRY
  BEGIN TRAN;
  SET @resource=N'DangKyTinChi:SV:'+@sv;
  EXEC @rc=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
  IF @rc<0 THROW 51006,N'Hệ thống đang bận. Vui lòng thử lại.',1;
  SELECT @lhp=d.ma_lhp,@hk=l.ma_hk FROM DANG_KY d JOIN LOP_HOC_PHAN l ON l.ma_lhp=d.ma_lhp
   WHERE d.ma_dk=@ma_dk AND d.ma_sv=@sv AND d.trang_thai='DANG_KY';
  IF @lhp IS NULL THROW 51017,N'Đăng ký không tồn tại, đã hủy hoặc không thuộc về bạn.',1;
  SELECT @start=tg_bat_dau,@limit=han_huy_dk,@state=trang_thai
  FROM HOC_KY WITH(HOLDLOCK) WHERE ma_hk=@hk;
  SELECT @count=si_so_hien_tai FROM LOP_HOC_PHAN WITH(UPDLOCK,HOLDLOCK) WHERE ma_lhp=@lhp;
  SET @now=CONVERT(datetime2(0), DATEADD(HOUR, 7, SYSUTCDATETIME()));
  IF @state<>'DANG_MO' OR @now<@start OR @now>=@limit
    THROW 51018,N'Không nằm trong thời hạn hủy hoặc đợt đăng ký đã đóng.',1;
  UPDATE DANG_KY SET trang_thai='DA_HUY',ngay_huy=@now
   WHERE ma_dk=@ma_dk AND ma_sv=@sv AND trang_thai='DANG_KY';
  IF @@ROWCOUNT<>1 THROW 51017,N'Đăng ký đã thay đổi. Vui lòng tải lại.',1;
  SELECT @count=COUNT(*) FROM DANG_KY WHERE ma_lhp=@lhp AND trang_thai='DANG_KY';
  UPDATE LOP_HOC_PHAN SET si_so_hien_tai=@count,
   trang_thai=CASE WHEN trang_thai IN ('DONG','HUY') THEN trang_thai
                  WHEN @count>=si_so_max THEN 'FULL' ELSE 'DANG_MO' END WHERE ma_lhp=@lhp;
  COMMIT;
  SELECT @ma_dk AS ma_dk,N'Hủy đăng ký thành công.' AS thong_bao;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH
END;
GO
CREATE OR ALTER PROCEDURE usp_XemKetQuaDangKy
 @ma_hk varchar(20),@ma_sv varchar(20)=NULL,@gom_da_huy bit=0
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20);
 EXEC usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role='SINH_VIEN' SET @ma_sv=@sv; -- Sinh viên chỉ được xem kết quả của chính mình.
 IF @ma_sv IS NULL OR NOT EXISTS(SELECT 1 FROM SINH_VIEN WHERE ma_sv=@ma_sv)
   THROW 51019,N'Hãy nhập mã sinh viên hợp lệ.',1;
 SELECT d.ma_dk,d.ma_lhp,v.ma_hp,v.ten_hp,v.so_tin_chi,v.ten_gv,v.lich_hoc,
 d.ngay_dk,d.trang_thai,d.ngay_huy,v.trang_thai AS trang_thai_lop
 FROM DANG_KY d JOIN v_ThongTinLopHocPhan v ON v.ma_lhp=d.ma_lhp
 WHERE d.ma_sv=@ma_sv AND v.ma_hk=@ma_hk AND (@gom_da_huy=1 OR d.trang_thai='DANG_KY')
 ORDER BY d.ngay_dk DESC,d.ma_dk DESC;
END;
GO
CREATE OR ALTER PROCEDURE usp_ThongKeLopHocPhan @ma_hk varchar(20),@nguong_it_sv int=NULL
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20);
 EXEC usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role NOT IN ('DAO_TAO','QUAN_TRI') THROW 51020,N'Chỉ phòng đào tạo hoặc quản trị được xem thống kê.',1;
 IF @nguong_it_sv<0 THROW 51021,N'Ngưỡng sinh viên không hợp lệ.',1;
 SELECT ma_lhp,ma_hp,ten_hp,so_tin_chi,ten_gv,si_so_hien_tai,si_so_max,
 CONVERT(decimal(6,2),100.0*si_so_hien_tai/si_so_max) AS ty_le_lap_day,
 CONVERT(bigint,si_so_hien_tai)*so_tin_chi AS tong_tin_chi_dang_ky,trang_thai
 FROM v_ThongTinLopHocPhan WHERE ma_hk=@ma_hk
 AND (@nguong_it_sv IS NULL OR si_so_hien_tai<@nguong_it_sv)
 ORDER BY si_so_hien_tai,ma_lhp;
END;
GO
CREATE OR ALTER PROCEDURE usp_DanhSachSinhVienTheoLop @ma_lhp varchar(20)
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20);
 EXEC usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role NOT IN ('DAO_TAO','QUAN_TRI') THROW 51020,N'Chỉ phòng đào tạo hoặc quản trị được xem danh sách lớp.',1;
 SELECT ROW_NUMBER() OVER(ORDER BY s.ma_sv) AS stt,s.ma_sv,s.ho_ten,s.ngay_sinh,s.gioi_tinh,
 s.email,s.ma_lop,n.ten_nganh,d.ngay_dk
 FROM DANG_KY d JOIN SINH_VIEN s ON s.ma_sv=d.ma_sv JOIN NGANH n ON n.ma_nganh=s.ma_nganh
 WHERE d.ma_lhp=@ma_lhp AND d.trang_thai='DANG_KY' ORDER BY s.ma_sv;
END;
GO
-- Chỉ cấp quyền gọi thủ tục công khai; không cấp quyền ghi trực tiếp lên các bảng.
IF DATABASE_PRINCIPAL_ID(N'app_dang_ky') IS NULL CREATE ROLE app_dang_ky;
GRANT EXECUTE ON usp_LayNguoiDung TO app_dang_ky;
GRANT EXECUTE ON usp_LayHocKy TO app_dang_ky;
GRANT EXECUTE ON usp_KiemTraThoiHan TO app_dang_ky;
GRANT EXECUTE ON usp_TraCuuLopDangMo TO app_dang_ky;
GRANT EXECUTE ON usp_DangKyHocPhan TO app_dang_ky;
GRANT EXECUTE ON usp_HuyDangKy TO app_dang_ky;
GRANT EXECUTE ON usp_XemKetQuaDangKy TO app_dang_ky;
GRANT EXECUTE ON usp_ThongKeLopHocPhan TO app_dang_ky;
GRANT EXECUTE ON usp_DanhSachSinhVienTheoLop TO app_dang_ky;
GO

-- PHẦN 3: CÁCH GỌI CÁC CHỨC NĂNG TỪ C#
-- CommandType = StoredProcedure. CommandText dùng đúng tên thủ tục bên dưới.
-- Connection string trỏ Initial Catalog=DangKyTinChi.
-- Các ví dụ chỉ là chú thích; thay mã theo dữ liệu đã nhập trước khi chạy.
-- EXEC usp_LayNguoiDung;
-- EXEC usp_LayHocKy;
-- EXEC usp_KiemTraThoiHan @ma_hk = 'HK01';
-- EXEC usp_TraCuuLopDangMo @ma_hk = 'HK01', @tu_khoa = N'', @chi_con_cho = 1;
-- EXEC usp_DangKyHocPhan @ma_lhp = 'LHP01';
-- EXEC usp_XemKetQuaDangKy @ma_hk = 'HK01', @gom_da_huy = 0;
-- EXEC usp_HuyDangKy @ma_dk = 1;
-- EXEC usp_ThongKeLopHocPhan @ma_hk = 'HK01', @nguong_it_sv = 15;
-- EXEC usp_DanhSachSinhVienTheoLop @ma_lhp = 'LHP01';
-- Phòng đào tạo có thể thêm @ma_sv khi gọi usp_XemKetQuaDangKy.
-- Tổng tín chỉ SV = tổng so_tin_chi của các dòng kết quả có trang_thai DANG_KY.
-- Không bọc usp_DangKyHocPhan / usp_HuyDangKy trong transaction ngoài;
-- các thủ tục này tự quản lý COMMIT/ROLLBACK.
-- C# bản trước cần đổi tên thủ tục và bỏ việc đọc các cột giới hạn tín chỉ
-- học kỳ được đề xuất thêm trước đây; chúng không có trong schema quy ước.

-- Khôi phục chế độ thực thi của phiên nếu phần kiểm tra đầu file đã dừng cài đặt.
SET NOEXEC OFF;
GO
