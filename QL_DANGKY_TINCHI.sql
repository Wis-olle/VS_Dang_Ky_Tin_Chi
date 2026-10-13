/* =========================================================
   DATABASE: QL_DANGKY_TINCHI
   MODULE: QUAN LY SINH VIEN - TAI KHOAN
   SERVER: .\SQLEXPRESS
   ========================================================= */

USE master;
GO

/* =========================================================
   1. TAO DATABASE
   ========================================================= */

IF DB_ID(N'QL_DANGKY_TINCHI') IS NULL
BEGIN
    CREATE DATABASE QL_DANGKY_TINCHI;
END
GO

USE QL_DANGKY_TINCHI;
GO


/* =========================================================
   2. TAO BANG KHOA
   ========================================================= */

IF OBJECT_ID(N'dbo.KHOA', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KHOA
    (
        ma_khoa      VARCHAR(20) NOT NULL,
        ten_khoa     NVARCHAR(100) NOT NULL,
        mo_ta        NVARCHAR(255) NULL,
        trang_thai   BIT NOT NULL
            CONSTRAINT DF_KHOA_TRANG_THAI DEFAULT 1,

        CONSTRAINT PK_KHOA
            PRIMARY KEY (ma_khoa),

        CONSTRAINT UQ_KHOA_TEN_KHOA
            UNIQUE (ten_khoa)
    );
END
GO


/* =========================================================
   3. TAO BANG NGANH
   ========================================================= */

IF OBJECT_ID(N'dbo.NGANH', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NGANH
    (
        ma_nganh     VARCHAR(20) NOT NULL,
        ten_nganh    NVARCHAR(100) NOT NULL,
        ma_khoa      VARCHAR(20) NOT NULL,
        mo_ta        NVARCHAR(255) NULL,
        trang_thai   BIT NOT NULL
            CONSTRAINT DF_NGANH_TRANG_THAI DEFAULT 1,

        CONSTRAINT PK_NGANH
            PRIMARY KEY (ma_nganh),

        CONSTRAINT FK_NGANH_KHOA
            FOREIGN KEY (ma_khoa)
            REFERENCES dbo.KHOA(ma_khoa)
            ON DELETE NO ACTION
            ON UPDATE NO ACTION
    );
END
GO


/* =========================================================
   4. TAO BANG LOP
   ========================================================= */

IF OBJECT_ID(N'dbo.LOP', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LOP
    (
        ma_lop       VARCHAR(20) NOT NULL,
        ten_lop      NVARCHAR(100) NOT NULL,
        ma_nganh     VARCHAR(20) NOT NULL,
        khoa_hoc     INT NOT NULL,
        nien_khoa    VARCHAR(20) NULL,
        trang_thai   BIT NOT NULL
            CONSTRAINT DF_LOP_TRANG_THAI DEFAULT 1,

        CONSTRAINT PK_LOP
            PRIMARY KEY (ma_lop),

        CONSTRAINT FK_LOP_NGANH
            FOREIGN KEY (ma_nganh)
            REFERENCES dbo.NGANH(ma_nganh)
            ON DELETE NO ACTION
            ON UPDATE NO ACTION,

        CONSTRAINT CK_LOP_KHOA_HOC
            CHECK (khoa_hoc > 0)
    );
END
GO


/* =========================================================
   5. TAO BANG SINH VIEN
   ========================================================= */

IF OBJECT_ID(N'dbo.SINH_VIEN', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SINH_VIEN
    (
        ma_sv        VARCHAR(20) NOT NULL,
        ho_ten       NVARCHAR(100) NOT NULL,
        ngay_sinh    DATE NOT NULL,
        gioi_tinh    NVARCHAR(10) NOT NULL,
        email        VARCHAR(100) NOT NULL,
        sdt          VARCHAR(15) NULL,
        dia_chi      NVARCHAR(255) NULL,
        ma_nganh     VARCHAR(20) NOT NULL,
        ma_lop       VARCHAR(20) NOT NULL,
        trang_thai   BIT NOT NULL
            CONSTRAINT DF_SINH_VIEN_TRANG_THAI DEFAULT 1,

        CONSTRAINT PK_SINH_VIEN
            PRIMARY KEY (ma_sv),

        CONSTRAINT FK_SINH_VIEN_NGANH
            FOREIGN KEY (ma_nganh)
            REFERENCES dbo.NGANH(ma_nganh)
            ON DELETE NO ACTION
            ON UPDATE NO ACTION,

        CONSTRAINT FK_SINH_VIEN_LOP
            FOREIGN KEY (ma_lop)
            REFERENCES dbo.LOP(ma_lop)
            ON DELETE NO ACTION
            ON UPDATE NO ACTION,

        CONSTRAINT UQ_SINH_VIEN_EMAIL
            UNIQUE (email),

        CONSTRAINT CK_SINH_VIEN_GIOI_TINH
            CHECK (gioi_tinh IN (N'Nam', N'Nữ', N'Khác'))
    );
END
GO


/* =========================================================
   6. TAO BANG TAI KHOAN
   ========================================================= */

IF OBJECT_ID(N'dbo.TAI_KHOAN', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TAI_KHOAN
    (
        ma_tk                 VARCHAR(20) NOT NULL,
        ten_dang_nhap         VARCHAR(50) NOT NULL,
        mat_khau_hash         VARCHAR(255) NOT NULL,
        vai_tro               VARCHAR(20) NOT NULL,
        trang_thai            BIT NOT NULL
            CONSTRAINT DF_TAI_KHOAN_TRANG_THAI DEFAULT 1,
        email                 VARCHAR(100) NOT NULL,
        ma_sv                 VARCHAR(20) NULL,
        ma_gv                 VARCHAR(20) NULL,
        ngay_tao              DATETIME NOT NULL
            CONSTRAINT DF_TAI_KHOAN_NGAY_TAO DEFAULT GETDATE(),
        lan_dang_nhap_cuoi    DATETIME NULL,

        CONSTRAINT PK_TAI_KHOAN
            PRIMARY KEY (ma_tk),

        CONSTRAINT UQ_TAI_KHOAN_TEN_DANG_NHAP
            UNIQUE (ten_dang_nhap),

        CONSTRAINT UQ_TAI_KHOAN_EMAIL
            UNIQUE (email),

        CONSTRAINT FK_TAI_KHOAN_SINH_VIEN
            FOREIGN KEY (ma_sv)
            REFERENCES dbo.SINH_VIEN(ma_sv)
            ON DELETE NO ACTION
            ON UPDATE NO ACTION,

        CONSTRAINT CK_TAI_KHOAN_VAI_TRO
            CHECK (
                vai_tro IN
                (
                    'Admin',
                    'DaoTao',
                    'SinhVien',
                    'GiangVien'
                )
            ),

        CONSTRAINT CK_TAI_KHOAN_DOI_TUONG
            CHECK
            (
                (vai_tro = 'SinhVien'
                    AND ma_sv IS NOT NULL
                    AND ma_gv IS NULL)

                OR

                (vai_tro = 'GiangVien'
                    AND ma_sv IS NULL
                    AND ma_gv IS NOT NULL)

                OR

                (vai_tro IN ('Admin', 'DaoTao')
                    AND ma_sv IS NULL
                    AND ma_gv IS NULL)
            )
    );
END
GO


/* =========================================================
   7. TAO UNIQUE FILTERED INDEX
      Moi sinh vien chi co toi da 1 tai khoan.
      Moi giang vien sau nay cung chi co toi da 1 tai khoan.
   ========================================================= */

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_TAI_KHOAN_MA_SV'
      AND object_id = OBJECT_ID(N'dbo.TAI_KHOAN')
)
BEGIN
    CREATE UNIQUE INDEX UX_TAI_KHOAN_MA_SV
    ON dbo.TAI_KHOAN(ma_sv)
    WHERE ma_sv IS NOT NULL;
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_TAI_KHOAN_MA_GV'
      AND object_id = OBJECT_ID(N'dbo.TAI_KHOAN')
)
BEGIN
    CREATE UNIQUE INDEX UX_TAI_KHOAN_MA_GV
    ON dbo.TAI_KHOAN(ma_gv)
    WHERE ma_gv IS NOT NULL;
END
GO


/* =========================================================
   8. INDEX PHUC VU TIM KIEM
   ========================================================= */

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_NGANH_MA_KHOA'
      AND object_id = OBJECT_ID(N'dbo.NGANH')
)
BEGIN
    CREATE INDEX IX_NGANH_MA_KHOA
    ON dbo.NGANH(ma_khoa);
END
GO


IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_LOP_MA_NGANH'
      AND object_id = OBJECT_ID(N'dbo.LOP')
)
BEGIN
    CREATE INDEX IX_LOP_MA_NGANH
    ON dbo.LOP(ma_nganh);
END
GO


IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_SINH_VIEN_HO_TEN'
      AND object_id = OBJECT_ID(N'dbo.SINH_VIEN')
)
BEGIN
    CREATE INDEX IX_SINH_VIEN_HO_TEN
    ON dbo.SINH_VIEN(ho_ten);
END
GO


IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_SINH_VIEN_MA_LOP'
      AND object_id = OBJECT_ID(N'dbo.SINH_VIEN')
)
BEGIN
    CREATE INDEX IX_SINH_VIEN_MA_LOP
    ON dbo.SINH_VIEN(ma_lop);
END
GO


IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_SINH_VIEN_MA_NGANH'
      AND object_id = OBJECT_ID(N'dbo.SINH_VIEN')
)
BEGIN
    CREATE INDEX IX_SINH_VIEN_MA_NGANH
    ON dbo.SINH_VIEN(ma_nganh);
END
GO


IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_TAI_KHOAN_VAI_TRO'
      AND object_id = OBJECT_ID(N'dbo.TAI_KHOAN')
)
BEGIN
    CREATE INDEX IX_TAI_KHOAN_VAI_TRO
    ON dbo.TAI_KHOAN(vai_tro, trang_thai);
END
GO


/* =========================================================
   9. KIEM TRA CAC BANG
   ========================================================= */

SELECT
    TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME;
GO

INSERT INTO KHOA (ma_khoa, ten_khoa, mo_ta)
VALUES
('CNTT', N'Khoa Công nghệ thông tin', N'Quản lý các ngành thuộc lĩnh vực công nghệ thông tin'),
('CK',   N'Khoa Cơ khí', N'Quản lý các ngành thuộc lĩnh vực cơ khí'),
('KT',   N'Khoa Kinh tế', N'Quản lý các ngành thuộc lĩnh vực kinh tế');
GO


INSERT INTO NGANH (ma_nganh, ten_nganh, ma_khoa)
VALUES
('CNTT',  N'Công nghệ thông tin', 'CNTT'),
('HTTT',  N'Hệ thống thông tin', 'CNTT'),
('CK01',  N'Cơ khí', 'CK'),
('QTKD',  N'Quản trị kinh doanh', 'KT');
GO


INSERT INTO LOP (ma_lop, ten_lop, ma_nganh, khoa_hoc, nien_khoa)
VALUES
('71D1', N'Công nghệ thông tin 71D1', 'CNTT', 71, '2025-2029'),
('71D2', N'Công nghệ thông tin 71D2', 'CNTT', 71, '2025-2029'),
('71HT1', N'Hệ thống thông tin 71', 'HTTT', 71, '2025-2029'),
('71QKD1', N'Quản trị kinh doanh 71', 'QTKD', 71, '2025-2029');
GO


INSERT INTO SINH_VIEN
(
    ma_sv,
    ho_ten,
    ngay_sinh,
    gioi_tinh,
    email,
    sdt,
    dia_chi,
    ma_nganh,
    ma_lop
)
VALUES
(
    'SV001',
    N'Nguyễn Văn An',
    '2007-01-15',
    N'Nam',
    'an.sv@example.com',
    '0912345001',
    N'Bắc Giang',
    'CNTT',
    '71D1'
),
(
    'SV002',
    N'Trần Văn Bình',
    '2007-03-20',
    N'Nam',
    'binh.sv@example.com',
    '0912345002',
    N'Bắc Ninh',
    'CNTT',
    '71D1'
),
(
    'SV003',
    N'Lê Thị Hoa',
    '2007-05-12',
    N'Nữ',
    'hoa.sv@example.com',
    '0912345003',
    N'Hà Nội',
    'CNTT',
    '71D2'
),
(
    'SV004',
    N'Phạm Văn Nam',
    '2007-08-09',
    N'Nam',
    'nam.sv@example.com',
    '0912345004',
    N'Nam Định',
    'HTTT',
    '71HT1'
);
GO