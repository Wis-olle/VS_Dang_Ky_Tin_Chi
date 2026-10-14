-- Local demo fixture using existing app IDs; independent from other members' live data.
-- Does not create accounts/passwords. Real enrollment rows replace invented UI counts.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
IF DB_NAME()<>N'DangKyTinChi_Minh' AND DB_NAME() NOT LIKE N'DangKyTinChi_Minh_Test[_]%'
 THROW 52090,N'Seed chỉ dành cho database Minh riêng hoặc kiểm thử dùng một lần.',1;
BEGIN TRY
 BEGIN TRAN;
 EXEC dbo.usp_Minh_KhoaLapLich;
 INSERT dbo.KHOA(ma_khoa,ten_khoa)
 SELECT x.id,x.ten FROM (VALUES ('CNTT',N'Công nghệ thông tin'),('KT',N'Kinh tế'),('NN',N'Ngoại ngữ'),('KTH',N'Kỹ thuật'))x(id,ten)
 WHERE NOT EXISTS(SELECT 1 FROM dbo.KHOA k WHERE k.ma_khoa=x.id);
 INSERT dbo.NGANH(ma_nganh,ten_nganh,ma_khoa)
 SELECT 'CNTT',N'Công nghệ thông tin','CNTT' WHERE NOT EXISTS(SELECT 1 FROM dbo.NGANH WHERE ma_nganh='CNTT');
 INSERT dbo.LOP(ma_lop,ten_lop,ma_nganh,khoa_hoc,nien_khoa)
 SELECT 'CNTT24A',N'Công nghệ thông tin 24A','CNTT',2024,'2024-2028' WHERE NOT EXISTS(SELECT 1 FROM dbo.LOP WHERE ma_lop='CNTT24A');
 INSERT dbo.SINH_VIEN(ma_sv,ho_ten,ngay_sinh,gioi_tinh,ma_nganh,ma_lop)
 SELECT x.id,x.ten,'20060101',N'Nam','CNTT','CNTT24A'
 FROM (VALUES ('SV2024001',N'Bùi Quang Minh'),('SV2024002',N'Nguyễn Văn An')) x(id,ten)
 WHERE NOT EXISTS(SELECT 1 FROM dbo.SINH_VIEN s WHERE s.ma_sv=x.id);
 INSERT dbo.GIANG_VIEN(ma_gv,ho_ten,ngay_sinh,gioi_tinh,email,sdt,ma_khoa,hoc_vi,avatar_key,trang_thai)
 SELECT x.* FROM (VALUES
('GV0001',N'Nguyễn Hoàng Minh','1986-04-12',N'Nam','minh.nh@campus.edu.vn','0901234567','CNTT',N'Tiến sĩ','portrait-04',1),
('GV0002',N'Trần Ngọc Anh','1990-08-21',N'Nữ','anh.tn@campus.edu.vn','0912345678','NN',N'Thạc sĩ','portrait-16',1),
('GV0003',N'Lê Quốc Bảo','1982-11-03',N'Nam','bao.lq@campus.edu.vn','0923456789','KT',N'PGS. Tiến sĩ','portrait-10',1),
('GV0004',N'Phạm Thùy Linh','1992-03-16',N'Nữ','linh.pt@campus.edu.vn','0934567890','CNTT',N'Thạc sĩ','portrait-01',1),
('GV0005',N'Đặng Đức Huy','1987-06-09',N'Nam','huy.dd@campus.edu.vn','0945678901','KTH',N'Tiến sĩ','portrait-06',1),
('GV0006',N'Vũ Mai Phương','1991-09-27',N'Nữ','phuong.vm@campus.edu.vn','0956789012','KT',N'Thạc sĩ','portrait-25',0),
('GV0007',N'Bùi Tuấn Kiệt','1988-02-18',N'Nam','kiet.bt@campus.edu.vn','0967890123','CNTT',N'Tiến sĩ','portrait-20',1),
('GV0008',N'Đỗ Hải Yến','1993-12-05',N'Nữ','yen.dh@campus.edu.vn','0978901234','NN',N'Thạc sĩ','portrait-27',1),
('GV0009',N'Hoàng Anh Dũng','1981-07-22',N'Nam','dung.ha@campus.edu.vn','0989012345','KTH',N'PGS. Tiến sĩ','portrait-23',1),
('GV0010',N'Ngô Thanh Hà','1994-05-11',N'Nữ','ha.nt@campus.edu.vn','0890123456','KT',N'Thạc sĩ','portrait-31',1),
('GV0011',N'Dương Gia Khánh','1989-10-30',N'Nam','khanh.dg@campus.edu.vn','0881234567','CNTT',N'Thạc sĩ','portrait-08',0),
('GV0012',N'Mai Khánh Chi','1990-01-08',N'Nữ','chi.mk@campus.edu.vn','0872345678','NN',N'Tiến sĩ','portrait-11',1)
) x(ma_gv,ho_ten,ngay_sinh,gioi_tinh,email,sdt,ma_khoa,hoc_vi,avatar_key,trang_thai)
 WHERE NOT EXISTS(SELECT 1 FROM dbo.GIANG_VIEN g WHERE g.ma_gv=x.ma_gv);
 INSERT dbo.HOC_PHAN(ma_hp,ten_hp,so_tin_chi,loai_hp,ma_khoa)
 SELECT x.id,x.ten,x.tc,'BAT_BUOC',x.khoa FROM (VALUES
 ('IT101',N'Lập trình hướng đối tượng',3,'CNTT'),('IT102',N'Cơ sở dữ liệu',3,'CNTT'),('IT103',N'Công nghệ phần mềm',3,'CNTT'),
 ('EN101',N'Tiếng Anh chuyên ngành',3,'NN'),('EN102',N'Tiếng Anh học thuật',2,'NN'),
 ('EC101',N'Kinh tế vi mô',3,'KT'),('EC102',N'Marketing căn bản',3,'KT'),('ET101',N'Kỹ thuật điện',3,'KTH')) x(id,ten,tc,khoa)
 WHERE NOT EXISTS(SELECT 1 FROM dbo.HOC_PHAN h WHERE h.ma_hp=x.id);
 INSERT dbo.HOC_KY(ma_hk,nam_hoc,hoc_ky,tg_bat_dau,tg_ket_thuc,han_huy_dk,trang_thai,ngay_hoc_bd,ngay_hoc_kt)
 SELECT x.id,'2026-2027',x.hk,x.dkbd,x.dkkt,x.dkkt,'DONG',x.bd,x.kt FROM (VALUES
 ('HK1-2627',1,'20260801','20260907','20260907','20261227'),
 ('HK2-2627',2,'20261201','20270111','20270111','20270523'))x(id,hk,dkbd,dkkt,bd,kt)
 WHERE NOT EXISTS(SELECT 1 FROM dbo.HOC_KY h WHERE h.ma_hk=x.id);
 INSERT dbo.PHONG_HOC(ma_phong,ten_phong,toa_nha,suc_chua,loai_phong,trang_thai)
 SELECT x.id,x.id,x.toa,x.sc,x.loai,x.tt FROM (VALUES
 ('A201',N'Tòa A',60,N'Lý thuyết',1),('A202',N'Tòa A',50,N'Lý thuyết',1),('A301',N'Tòa A',80,N'Lý thuyết',1),
 ('B101',N'Tòa B',40,N'Thực hành',1),('B102',N'Tòa B',40,N'Thực hành',1),('B201',N'Tòa B',60,N'Lý thuyết',1),
 ('C101',N'Tòa C',120,N'Hội trường',1),('C202',N'Tòa C',45,N'Lý thuyết',0))x(id,toa,sc,loai,tt)
 WHERE NOT EXISTS(SELECT 1 FROM dbo.PHONG_HOC p WHERE p.ma_phong=x.id);
 DECLARE @new TABLE(id varchar(20));
 INSERT dbo.LOP_HOC_PHAN(ma_lhp,ma_hp,ma_gv,ma_hk,si_so_max,trang_thai)
 OUTPUT inserted.ma_lhp INTO @new
 SELECT x.id,x.hp,x.gv,x.hk,x.sc,x.tt FROM (VALUES
 ('LHP0001','IT101','GV0001','HK1-2627',40,'DANG_MO'),('LHP0002','IT102','GV0004','HK1-2627',40,'DANG_MO'),
 ('LHP0003','EN101','GV0002','HK1-2627',50,'DANG_MO'),('LHP0004','EC101','GV0003','HK1-2627',60,'DANG_MO'),
 ('LHP0005','ET101','GV0005','HK1-2627',60,'DANG_MO'),('LHP0006','EN102','GV0008','HK1-2627',40,'DANG_MO'),
 ('LHP0007','IT103','GV0007','HK1-2627',60,'DONG'),('LHP0008','EC102','GV0010','HK1-2627',50,'HUY'),
 ('LHP0009','IT101','GV0001','HK2-2627',40,'DANG_MO'),('LHP0010','EN101','GV0012','HK2-2627',50,'DANG_MO'))x(id,hp,gv,hk,sc,tt)
 WHERE NOT EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN l WHERE l.ma_lhp=x.id);
 INSERT dbo.LICH_HOC(ma_lhp,ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt)
 SELECT x.id,x.phong,x.thu,x.bd,x.kt,h.ngay_hoc_bd,h.ngay_hoc_kt
 FROM (VALUES
 ('LHP0001','A201',2,1,3),('LHP0001','B101',5,4,6),('LHP0002','B102',3,1,3),('LHP0002','A202',6,4,6),
 ('LHP0003','A202',2,4,6),('LHP0004','A301',4,1,3),('LHP0005','B201',3,7,9),
 ('LHP0007','A201',4,4,6),('LHP0008','A202',2,4,6),('LHP0009','B101',2,1,3))x(id,phong,thu,bd,kt)
 JOIN @new n ON n.id=x.id JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=x.id JOIN dbo.HOC_KY h ON h.ma_hk=l.ma_hk;
 -- Add enrollments only when the class was just inserted; never resurrect a canceled enrollment on rerun.
 INSERT dbo.DANG_KY(ma_sv,ma_lhp,ngay_dk,trang_thai)
 SELECT x.sv,x.lhp,'20260815','DANG_KY'
 FROM (VALUES ('SV2024001','LHP0001'),('SV2024001','LHP0002'),('SV2024001','LHP0003'),
 ('SV2024001','LHP0007'),('SV2024002','LHP0004'))x(sv,lhp) JOIN @new n ON n.id=x.lhp;
 UPDATE l SET si_so_hien_tai=d.cnt,trang_thai=CASE WHEN l.trang_thai IN ('DONG','HUY') THEN l.trang_thai
  WHEN d.cnt>=l.si_so_max THEN 'FULL' ELSE 'DANG_MO' END
 FROM dbo.LOP_HOC_PHAN l JOIN @new n ON n.id=l.ma_lhp
 CROSS APPLY(SELECT COUNT(*) cnt FROM dbo.DANG_KY d WHERE d.ma_lhp=l.ma_lhp AND d.trang_thai='DANG_KY')d;
 COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
GO
