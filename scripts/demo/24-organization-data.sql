-- Synthetic organization branches for visual testing; never apply to a real database.
SET NOCOUNT ON; SET XACT_ABORT ON;
DECLARE @expectedDatabase sysname=N'__SHOWCASE_DATABASE__';
IF DB_NAME()<>@expectedDatabase OR @expectedDatabase NOT LIKE N'DangKyTinChi_Minh_Test_Showcase[_]%' OR LEN(@expectedDatabase)<>64 OR RIGHT(@expectedDatabase,32) COLLATE Latin1_General_100_BIN2 LIKE '%[^a-f0-9]%'
 THROW 54980,N'Organization demo requires the exact owned showcase database.',1;
BEGIN TRY BEGIN TRAN;
DECLARE @majors TABLE(code varchar(20),name nvarchar(100),faculty varchar(20));
INSERT @majors VALUES ('QTKD',N'Quản trị kinh doanh','KT'),('CNKT',N'Công nghệ kỹ thuật','KTH'),('NNA',N'Ngôn ngữ Anh','NN');
IF EXISTS(SELECT 1 FROM @majors m JOIN dbo.NGANH n ON n.ma_nganh=m.code WHERE n.ma_khoa<>m.faculty)
 THROW 54984,N'Existing demo major belongs to another faculty; no data changed.',1;
INSERT dbo.NGANH(ma_nganh,ten_nganh,ma_khoa,mo_ta,trang_thai)
 SELECT m.code,m.name,m.faculty,N'SYNTHETIC DEMO - Ngành dùng để kiểm thử danh mục.',1 FROM @majors m
 WHERE NOT EXISTS(SELECT 1 FROM dbo.NGANH n WHERE n.ma_nganh=m.code);
DECLARE @classes TABLE(code varchar(20),name nvarchar(100),major varchar(20),cohort smallint,years varchar(9));
INSERT @classes VALUES
 ('QTKD24A',N'Quản trị kinh doanh 24A','QTKD',2024,'2024-2028'),('QTKD25A',N'Quản trị kinh doanh 25A','QTKD',2025,'2025-2029'),
 ('CNKT24A',N'Công nghệ kỹ thuật 24A','CNKT',2024,'2024-2028'),('CNKT25A',N'Công nghệ kỹ thuật 25A','CNKT',2025,'2025-2029'),
 ('NNA24A',N'Ngôn ngữ Anh 24A','NNA',2024,'2024-2028'),('NNA25A',N'Ngôn ngữ Anh 25A','NNA',2025,'2025-2029');
IF EXISTS(SELECT 1 FROM @classes c JOIN dbo.LOP l ON l.ma_lop=c.code WHERE l.ma_nganh<>c.major)
 THROW 54984,N'Existing demo class belongs to another major; no data changed.',1;
INSERT dbo.LOP(ma_lop,ten_lop,ma_nganh,khoa_hoc,nien_khoa,trang_thai)
 SELECT c.code,c.name,c.major,c.cohort,c.years,1 FROM @classes c WHERE NOT EXISTS(SELECT 1 FROM dbo.LOP l WHERE l.ma_lop=c.code);
COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
