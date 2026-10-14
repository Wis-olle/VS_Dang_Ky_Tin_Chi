/* SQL Server 2016 SP1+ / Express / LocalDB. Chạy trên database mới bằng SSMS.
   KHÔNG DROP database, KHÔNG xóa dữ liệu cũ. Nếu đã có bảng, dừng để tránh ghi đè. */
USE master;
GO
IF DB_ID(N'DangKyTinChi') IS NULL EXEC(N'CREATE DATABASE [DangKyTinChi]');
GO
USE DangKyTinChi;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF EXISTS (SELECT 1 FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo'))
    THROW 51000, N'Database đã có bảng. Không chạy lại 01; hãy dùng database mới hoặc kiểm tra schema hiện có.', 1;
BEGIN TRANSACTION;
CREATE TABLE dbo.KHOA (
 ma_khoa varchar(20) NOT NULL CONSTRAINT PK_KHOA PRIMARY KEY,
 ten_khoa nvarchar(150) NOT NULL,
 mo_ta nvarchar(500) NULL,
 trang_thai bit NOT NULL CONSTRAINT DF_KHOA_tt DEFAULT 1,
 CONSTRAINT CK_KHOA_ten CHECK(LEN(LTRIM(RTRIM(ten_khoa)))>0)
);
CREATE TABLE dbo.NGANH (
 ma_nganh varchar(20) NOT NULL CONSTRAINT PK_NGANH PRIMARY KEY,
 ten_nganh nvarchar(150) NOT NULL,
 ma_khoa varchar(20) NOT NULL,
 mo_ta nvarchar(500) NULL,
 trang_thai bit NOT NULL CONSTRAINT DF_NGANH_tt DEFAULT 1,
 CONSTRAINT FK_NGANH_KHOA FOREIGN KEY(ma_khoa) REFERENCES dbo.KHOA(ma_khoa),
 CONSTRAINT CK_NGANH_ten CHECK(LEN(LTRIM(RTRIM(ten_nganh)))>0)
);
CREATE TABLE dbo.LOP (
 ma_lop varchar(20) NOT NULL CONSTRAINT PK_LOP PRIMARY KEY,
 ten_lop nvarchar(100) NOT NULL,
 ma_nganh varchar(20) NOT NULL,
 khoa_hoc int NOT NULL,
 nien_khoa varchar(9) NOT NULL,
 trang_thai bit NOT NULL CONSTRAINT DF_LOP_tt DEFAULT 1,
 CONSTRAINT UQ_LOP_nganh UNIQUE(ma_lop,ma_nganh),
 CONSTRAINT FK_LOP_NGANH FOREIGN KEY(ma_nganh) REFERENCES dbo.NGANH(ma_nganh),
 CONSTRAINT CK_LOP_khoa CHECK(khoa_hoc>0),
 CONSTRAINT CK_LOP_ten CHECK(LEN(LTRIM(RTRIM(ten_lop)))>0)
);
CREATE TABLE dbo.SINH_VIEN (
 ma_sv varchar(20) NOT NULL CONSTRAINT PK_SINH_VIEN PRIMARY KEY,
 ho_ten nvarchar(150) NOT NULL,
 ngay_sinh date NOT NULL,
 gioi_tinh nvarchar(10) NOT NULL,
 email varchar(254) NULL,
 sdt varchar(15) NULL,
 ma_nganh varchar(20) NOT NULL,
 ma_lop varchar(20) NOT NULL,
 dia_chi nvarchar(300) NULL,
 trang_thai bit NOT NULL CONSTRAINT DF_SV_tt DEFAULT 1,
 CONSTRAINT FK_SV_NGANH FOREIGN KEY(ma_nganh) REFERENCES dbo.NGANH(ma_nganh),
 CONSTRAINT FK_SV_LOP FOREIGN KEY(ma_lop,ma_nganh) REFERENCES dbo.LOP(ma_lop,ma_nganh),
 CONSTRAINT CK_SV_gt CHECK(gioi_tinh IN(N'Nam',N'Nữ',N'Khác')),
 CONSTRAINT CK_SV_ten CHECK(LEN(LTRIM(RTRIM(ho_ten)))>0)
);
CREATE TABLE dbo.GIANG_VIEN (
 ma_gv varchar(20) NOT NULL CONSTRAINT PK_GIANG_VIEN PRIMARY KEY,
 ho_ten nvarchar(150) NOT NULL,
 ngay_sinh date NOT NULL,
 gioi_tinh nvarchar(10) NOT NULL,
 email varchar(254) NULL,
 sdt varchar(15) NULL,
 ma_khoa varchar(20) NOT NULL,
 trang_thai bit NOT NULL CONSTRAINT DF_GV_tt DEFAULT 1,
 CONSTRAINT FK_GV_KHOA FOREIGN KEY(ma_khoa) REFERENCES dbo.KHOA(ma_khoa),
 CONSTRAINT CK_GV_gt CHECK(gioi_tinh IN(N'Nam',N'Nữ',N'Khác')),
 CONSTRAINT CK_GV_ten CHECK(LEN(LTRIM(RTRIM(ho_ten)))>0)
);
CREATE TABLE dbo.TAI_KHOAN (
 ma_tk bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TAI_KHOAN PRIMARY KEY,
 ten_dang_nhap varchar(100) NOT NULL CONSTRAINT UQ_TK_ten UNIQUE,
 mat_khau_hash varchar(512) NOT NULL,
 vai_tro varchar(20) NOT NULL,
 trang_thai bit NOT NULL CONSTRAINT DF_TK_tt DEFAULT 1,
 email varchar(254) NULL,
 ma_sv varchar(20) NULL,
 ma_gv varchar(20) NULL,
 ngay_tao datetime2(0) NOT NULL CONSTRAINT DF_TK_tao DEFAULT DATEADD(HOUR,7,SYSUTCDATETIME()),
 lan_dang_nhap_cuoi datetime2(0) NULL,
 CONSTRAINT FK_TK_SV FOREIGN KEY(ma_sv) REFERENCES dbo.SINH_VIEN(ma_sv),
 CONSTRAINT FK_TK_GV FOREIGN KEY(ma_gv) REFERENCES dbo.GIANG_VIEN(ma_gv),
 CONSTRAINT CK_TK_role CHECK(
  (vai_tro='SINH_VIEN' AND ma_sv IS NOT NULL AND ma_gv IS NULL) OR
  (vai_tro='GIANG_VIEN' AND ma_gv IS NOT NULL AND ma_sv IS NULL) OR
  (vai_tro IN('DAO_TAO','QUAN_TRI') AND ma_sv IS NULL AND ma_gv IS NULL)),
 CONSTRAINT CK_TK_ten CHECK(LEN(LTRIM(RTRIM(ten_dang_nhap)))>0),
 CONSTRAINT CK_TK_hash CHECK(LEN(mat_khau_hash)>0)
);
CREATE UNIQUE INDEX UX_TK_sv ON dbo.TAI_KHOAN(ma_sv) WHERE ma_sv IS NOT NULL;
CREATE UNIQUE INDEX UX_TK_gv ON dbo.TAI_KHOAN(ma_gv) WHERE ma_gv IS NOT NULL;
CREATE TABLE dbo.HOC_PHAN (
 ma_hp varchar(20) NOT NULL CONSTRAINT PK_HOC_PHAN PRIMARY KEY,
 ten_hp nvarchar(200) NOT NULL,
 so_tin_chi tinyint NOT NULL,
 loai_hp varchar(20) NOT NULL,
 tin_chi_min tinyint NOT NULL,
 tin_chi_max tinyint NOT NULL,
 trang_thai bit NOT NULL CONSTRAINT DF_HP_tt DEFAULT 1,
 CONSTRAINT CK_HP_ma CHECK(LEN(ma_hp)>0 AND ma_hp NOT LIKE '%[^A-Za-z0-9_-]%' COLLATE Latin1_General_100_BIN2),
 CONSTRAINT CK_HP_ten CHECK(LEN(LTRIM(RTRIM(ten_hp)))>0),
 CONSTRAINT CK_HP_loai CHECK(loai_hp IN('BAT_BUOC','TU_CHON')),
 CONSTRAINT CK_HP_tc CHECK(tin_chi_min>=1 AND tin_chi_max<=30 AND tin_chi_min<=so_tin_chi AND so_tin_chi<=tin_chi_max)
);
CREATE TABLE dbo.HOC_KY (
 ma_hk varchar(20) NOT NULL CONSTRAINT PK_HOC_KY PRIMARY KEY,
 nam_hoc varchar(9) NOT NULL,
 hoc_ky tinyint NOT NULL,
 tg_bat_dau datetime2(0) NOT NULL,
 tg_ket_thuc datetime2(0) NOT NULL,
 han_huy_dk datetime2(0) NOT NULL,
 trang_thai varchar(10) NOT NULL CONSTRAINT DF_HK_tt DEFAULT 'DONG',
 CONSTRAINT UQ_HK_nam_ky UNIQUE(nam_hoc,hoc_ky),
 CONSTRAINT CK_HK_ma CHECK(LEN(ma_hk)>0 AND ma_hk NOT LIKE '%[^A-Za-z0-9_-]%' COLLATE Latin1_General_100_BIN2),
 CONSTRAINT CK_HK_nam CHECK(nam_hoc LIKE '[12][0-9][0-9][0-9]-[12][0-9][0-9][0-9]' AND TRY_CONVERT(int,RIGHT(nam_hoc,4))=TRY_CONVERT(int,LEFT(nam_hoc,4))+1),
 CONSTRAINT CK_HK_ky CHECK(hoc_ky IN(1,2,3)),
 CONSTRAINT CK_HK_tg CHECK(tg_bat_dau<tg_ket_thuc AND han_huy_dk>=tg_bat_dau),
 CONSTRAINT CK_HK_tt CHECK(trang_thai IN('MO','DONG'))
);
CREATE TABLE dbo.PHONG_HOC (
 ma_phong varchar(20) NOT NULL CONSTRAINT PK_PHONG_HOC PRIMARY KEY,
 ten_phong nvarchar(100) NOT NULL,
 toa_nha nvarchar(100) NOT NULL,
 suc_chua smallint NOT NULL,
 loai_phong nvarchar(50) NOT NULL,
 trang_thai bit NOT NULL CONSTRAINT DF_PHONG_tt DEFAULT 1,
 CONSTRAINT CK_PHONG_sc CHECK(suc_chua>0)
);
CREATE TABLE dbo.LOP_HOC_PHAN (
 ma_lhp varchar(20) NOT NULL CONSTRAINT PK_LOP_HOC_PHAN PRIMARY KEY,
 ma_hp varchar(20) NOT NULL,
 ma_gv varchar(20) NOT NULL,
 ma_hk varchar(20) NOT NULL,
 ma_phong varchar(20) NULL,
 si_so_max smallint NOT NULL,
 si_so_hien_tai smallint NOT NULL CONSTRAINT DF_LHP_ss DEFAULT 0,
 trang_thai varchar(10) NOT NULL CONSTRAINT DF_LHP_tt DEFAULT 'MO',
 CONSTRAINT FK_LHP_HP FOREIGN KEY(ma_hp) REFERENCES dbo.HOC_PHAN(ma_hp),
 CONSTRAINT FK_LHP_GV FOREIGN KEY(ma_gv) REFERENCES dbo.GIANG_VIEN(ma_gv),
 CONSTRAINT FK_LHP_HK FOREIGN KEY(ma_hk) REFERENCES dbo.HOC_KY(ma_hk),
 CONSTRAINT FK_LHP_PHONG FOREIGN KEY(ma_phong) REFERENCES dbo.PHONG_HOC(ma_phong),
 CONSTRAINT CK_LHP_ss CHECK(si_so_max>0 AND si_so_hien_tai>=0 AND si_so_hien_tai<=si_so_max),
 CONSTRAINT CK_LHP_tt CHECK(trang_thai IN('MO','DONG','HUY'))
);
CREATE TABLE dbo.DANG_KY (
 ma_dk bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DANG_KY PRIMARY KEY,
 ma_sv varchar(20) NOT NULL,
 ma_lhp varchar(20) NOT NULL,
 ngay_dk datetime2(0) NOT NULL CONSTRAINT DF_DK_ngay DEFAULT DATEADD(HOUR,7,SYSUTCDATETIME()),
 trang_thai varchar(20) NOT NULL CONSTRAINT DF_DK_tt DEFAULT 'DA_DANG_KY',
 ngay_huy datetime2(0) NULL,
 CONSTRAINT FK_DK_SV FOREIGN KEY(ma_sv) REFERENCES dbo.SINH_VIEN(ma_sv),
 CONSTRAINT FK_DK_LHP FOREIGN KEY(ma_lhp) REFERENCES dbo.LOP_HOC_PHAN(ma_lhp),
 CONSTRAINT CK_DK_tt CHECK((trang_thai='DA_DANG_KY' AND ngay_huy IS NULL) OR (trang_thai='DA_HUY' AND ngay_huy IS NOT NULL AND ngay_huy>=ngay_dk))
);
CREATE UNIQUE INDEX UX_DK_active ON dbo.DANG_KY(ma_sv,ma_lhp) WHERE trang_thai='DA_DANG_KY';
CREATE INDEX IX_DK_lhp ON dbo.DANG_KY(ma_lhp,trang_thai) INCLUDE(ma_sv);
CREATE TABLE dbo.LICH_HOC (
 ma_lich bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_LICH_HOC PRIMARY KEY,
 ma_lhp varchar(20) NOT NULL,
 thu tinyint NOT NULL,
 tiet_bd tinyint NOT NULL,
 tiet_kt tinyint NOT NULL,
 ma_phong varchar(20) NOT NULL,
 ngay_bd date NOT NULL,
 ngay_kt date NOT NULL,
 CONSTRAINT FK_LICH_LHP FOREIGN KEY(ma_lhp) REFERENCES dbo.LOP_HOC_PHAN(ma_lhp),
 CONSTRAINT FK_LICH_PHONG FOREIGN KEY(ma_phong) REFERENCES dbo.PHONG_HOC(ma_phong),
 CONSTRAINT CK_LICH_thu CHECK(thu BETWEEN 2 AND 8),
 CONSTRAINT CK_LICH_tiet CHECK(tiet_bd BETWEEN 1 AND 15 AND tiet_kt BETWEEN tiet_bd AND 15),
 CONSTRAINT CK_LICH_ngay CHECK(ngay_bd<=ngay_kt)
);
CREATE TABLE dbo.KET_QUA (
 ma_kq bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_KET_QUA PRIMARY KEY,
 ma_sv varchar(20) NOT NULL,
 ma_hp varchar(20) NOT NULL,
 diem_qt decimal(4,2) NULL,
 diem_gk decimal(4,2) NULL,
 diem_ck decimal(4,2) NULL,
 diem_tk decimal(4,2) NULL,
 diem_he4 decimal(3,2) NULL,
 diem_chu varchar(2) NULL,
 ket_qua varchar(10) NULL,
 CONSTRAINT UQ_KQ_sv_hp UNIQUE(ma_sv,ma_hp),
 CONSTRAINT FK_KQ_SV FOREIGN KEY(ma_sv) REFERENCES dbo.SINH_VIEN(ma_sv),
 CONSTRAINT FK_KQ_HP FOREIGN KEY(ma_hp) REFERENCES dbo.HOC_PHAN(ma_hp),
 CONSTRAINT CK_KQ_diem CHECK((diem_qt IS NULL OR diem_qt BETWEEN 0 AND 10) AND (diem_gk IS NULL OR diem_gk BETWEEN 0 AND 10) AND (diem_ck IS NULL OR diem_ck BETWEEN 0 AND 10) AND (diem_tk IS NULL OR diem_tk BETWEEN 0 AND 10) AND (diem_he4 IS NULL OR diem_he4 BETWEEN 0 AND 4)),
 CONSTRAINT CK_KQ_chu CHECK(diem_chu IS NULL OR diem_chu IN('A+','A','B+','B','C+','C','D+','D','F')),
 CONSTRAINT CK_KQ_kq CHECK(ket_qua IS NULL OR ket_qua IN('DAT','KHONG_DAT'))
);
CREATE INDEX IX_LHP_hp_hk ON dbo.LOP_HOC_PHAN(ma_hp,ma_hk);
CREATE INDEX IX_LHP_hk ON dbo.LOP_HOC_PHAN(ma_hk);
CREATE INDEX IX_LICH_lhp ON dbo.LICH_HOC(ma_lhp);
CREATE INDEX IX_SV_lop ON dbo.SINH_VIEN(ma_lop,ma_nganh);
COMMIT;
PRINT N'Đã tạo đủ 13 bảng theo file quy ước.';
GO
