-- Synthetic local data only. Never apply this script to a real student database.
SET NOCOUNT ON; SET XACT_ABORT ON;
DECLARE @expectedDatabase sysname=N'__SHOWCASE_DATABASE__';
IF DB_NAME()<>@expectedDatabase OR @expectedDatabase NOT LIKE N'DangKyTinChi_Minh_Test_Showcase[_]%' OR LEN(@expectedDatabase)<>64 OR RIGHT(@expectedDatabase,32) COLLATE Latin1_General_100_BIN2 LIKE '%[^a-f0-9]%'
 THROW 54980,N'Full demo requires the exact owned showcase database.',1;
IF EXISTS(SELECT 1 FROM dbo.SINH_VIEN WHERE ma_sv='SV2024003') THROW 54983,N'Full demo already populated; create a new UUID.',1;
BEGIN TRY BEGIN TRAN;
DECLARE @today date=CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())),@start date,@finish date;
SELECT @start=ngay_hoc_bd,@finish=ngay_hoc_kt FROM dbo.HOC_KY WHERE ma_hk='HK1-2627';
INSERT dbo.LOP(ma_lop,ten_lop,ma_nganh,khoa_hoc,nien_khoa) VALUES('CNTT24B',N'Công nghệ thông tin 24B','CNTT',2024,'2024-2028');
DECLARE @students TABLE(n int,name nvarchar(100),gender nvarchar(10));
INSERT @students VALUES
 (3,N'Trần Ngọc Anh',N'Nữ'),(4,N'Lê Quốc Bảo',N'Nam'),(5,N'Phạm Thùy Linh',N'Nữ'),(6,N'Đặng Đức Huy',N'Nam'),
 (7,N'Vũ Mai Phương',N'Nữ'),(8,N'Bùi Tuấn Kiệt',N'Nam'),(9,N'Đỗ Hải Yến',N'Nữ'),(10,N'Hoàng Anh Dũng',N'Nam'),
 (11,N'Ngô Thanh Hà',N'Nữ'),(12,N'Dương Gia Khánh',N'Nam'),(13,N'Mai Khánh Chi',N'Nữ'),(14,N'Phan Minh Châu',N'Nữ'),
 (15,N'Đinh Bảo Long',N'Nam'),(16,N'Võ Ngọc Hân',N'Nữ'),(17,N'Hồ Nhật Nam',N'Nam'),(18,N'Lý Thanh Tâm',N'Nữ'),
 (19,N'Nguyễn Đức Anh',N'Nam'),(20,N'Trần Khánh Vy',N'Nữ'),(21,N'Lê Hoài An',N'Nữ'),(22,N'Phạm Gia Huy',N'Nam'),
 (23,N'Đặng Minh Thư',N'Nữ'),(24,N'Vũ Quang Hưng',N'Nam'),(25,N'Tống Bảo Ngọc',N'Nữ');
INSERT dbo.SINH_VIEN(ma_sv,ho_ten,ngay_sinh,gioi_tinh,email,sdt,ma_nganh,ma_lop,dia_chi,trang_thai)
 SELECT CONCAT('SV2024',RIGHT(CONCAT('000',n),3)),name,DATEADD(day,n*11,CONVERT(date,'20060101')),gender,
 CONCAT('sv2024',RIGHT(CONCAT('000',n),3),'@example.test'),CONCAT('090000',RIGHT(CONCAT('0000',n),4)),
 'CNTT',CASE WHEN n%2=0 THEN 'CNTT24B' ELSE 'CNTT24A' END,N'Hồ sơ mô phỏng · TP. Hồ Chí Minh',CASE WHEN n=25 THEN 0 ELSE 1 END FROM @students;
UPDATE dbo.SINH_VIEN SET email=LOWER(ma_sv)+'@example.test',sdt='0900000001',dia_chi=N'Hồ sơ mô phỏng · TP. Hồ Chí Minh' WHERE ma_sv IN('SV2024001','SV2024002');
INSERT dbo.GIANG_VIEN(ma_gv,ho_ten,ngay_sinh,gioi_tinh,email,sdt,ma_khoa,hoc_vi,avatar_key,trang_thai)
 SELECT CONCAT('GV00',n),N'ThS. '+name,DATEADD(year,-20,DATEADD(day,n*11,CONVERT(date,'20060101'))),gender,
 CONCAT('gv',n,'@example.test'),CONCAT('09100000',n),CASE n%4 WHEN 0 THEN 'CNTT' WHEN 1 THEN 'KT' WHEN 2 THEN 'NN' ELSE 'KTH' END,
 CASE WHEN n%3=0 THEN N'Tiến sĩ' ELSE N'Thạc sĩ' END,CONCAT('portrait-',n),CASE WHEN n=24 THEN 0 ELSE 1 END FROM @students WHERE n BETWEEN 13 AND 24;
INSERT dbo.HOC_PHAN(ma_hp,ten_hp,so_tin_chi,loai_hp,ma_khoa) VALUES
 ('IT201',N'Lập trình trực quan C#',3,'BAT_BUOC','CNTT'),('IT202',N'Cấu trúc dữ liệu và giải thuật',4,'BAT_BUOC','CNTT'),
 ('IT203',N'Mạng máy tính',3,'BAT_BUOC','CNTT'),('IT204',N'Hệ điều hành',3,'BAT_BUOC','CNTT'),
 ('IT205',N'Phát triển ứng dụng web',3,'TU_CHON','CNTT'),('IT206',N'Kiểm thử phần mềm',3,'TU_CHON','CNTT'),
 ('IT207',N'Trí tuệ nhân tạo cơ bản',3,'TU_CHON','CNTT'),('IT208',N'An toàn thông tin',3,'TU_CHON','CNTT'),
 ('EC201',N'Khởi nghiệp đổi mới sáng tạo',2,'TU_CHON','KT'),('EN201',N'Giao tiếp tiếng Anh',2,'TU_CHON','NN'),
 ('ET201',N'Internet vạn vật',3,'TU_CHON','KTH'),('IT209',N'Đồ án chuyên ngành',4,'BAT_BUOC','CNTT');
INSERT dbo.PHONG_HOC(ma_phong,ten_phong,toa_nha,suc_chua,loai_phong,trang_thai) VALUES
 ('D301',N'Phòng máy D301',N'Tòa D',40,N'Thực hành',1),('D302',N'Phòng máy D302',N'Tòa D',40,N'Thực hành',1),
 ('D303',N'Phòng D303',N'Tòa D',60,N'Lý thuyết',1),('D304',N'Phòng D304',N'Tòa D',60,N'Lý thuyết',1),
 ('D305',N'Phòng D305',N'Tòa D',50,N'Lý thuyết',1),('D306',N'Phòng D306',N'Tòa D',50,N'Lý thuyết',1),
 ('E101',N'Hội trường E101',N'Tòa E',120,N'Hội trường',1),('E102',N'Phòng E102 bảo trì',N'Tòa E',45,N'Lý thuyết',0);
DECLARE @classes TABLE(n int,course varchar(20),lecturer varchar(20),room varchar(20),day tinyint,first tinyint,capacity int);
INSERT @classes VALUES
 (30,'IT201','GV0013','D301',2,7,30),(31,'IT202','GV0014','D302',3,4,30),(32,'IT203','GV0015','D303',4,7,30),
 (33,'IT204','GV0016','D304',5,7,30),(34,'IT205','GV0017','D305',6,10,30),(35,'IT206','GV0018','D306',2,10,25),
 (36,'IT207','GV0019','D301',3,10,25),(37,'IT208','GV0020','D302',4,10,25),(38,'EC201','GV0021','D303',5,10,25),
 (39,'EN201','GV0022','D304',6,7,25),(40,'EC102','GV0023','E101',7,1,1),(41,'EC102','GV0023','E101',7,4,1),
 (42,'EC102','GV0023','E101',7,7,1),(43,'EC102','GV0023','E101',7,10,1),(44,'EC102','GV0023','E101',8,1,1),
 (45,'EC102','GV0023','E101',8,4,1);
INSERT dbo.LOP_HOC_PHAN(ma_lhp,ma_hp,ma_gv,ma_hk,si_so_max,trang_thai,lifecycle)
 SELECT CONCAT('LHP00',n),course,lecturer,'HK1-2627',capacity,'DANG_MO','REGISTRATION' FROM @classes;
INSERT dbo.LICH_HOC(ma_lhp,ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt)
 SELECT CONCAT('LHP00',n),room,day,first,first+2,@start,@finish FROM @classes;
-- Synthetic enrollments share five nonoverlapping weekly slots; waitlist classes remain free.
INSERT dbo.DANG_KY(ma_sv,ma_lhp,ngay_dk,trang_thai)
 SELECT CONCAT('SV2024',RIGHT(CONCAT('000',s.n),3)),CONCAT('LHP00',c.n),DATEADD(day,-1,@today),'DANG_KY'
 FROM @students s CROSS JOIN @classes c WHERE s.n BETWEEN 3 AND 24 AND c.n BETWEEN 30 AND 34 AND (s.n+c.n)%5<>0;
INSERT dbo.DANG_KY(ma_sv,ma_lhp,ngay_dk,trang_thai,ngay_huy) VALUES
 ('SV2024001','LHP0005',DATEADD(day,-3,@today),'DA_HUY',DATEADD(day,-2,@today)),
 ('SV2024003','LHP0035',DATEADD(day,-3,@today),'DA_HUY',DATEADD(day,-2,@today)),
 ('SV2024004','LHP0036',DATEADD(day,-3,@today),'DA_HUY',DATEADD(day,-2,@today));
UPDATE l SET si_so_hien_tai=x.cnt,trang_thai=CASE WHEN l.trang_thai IN('DONG','HUY') THEN l.trang_thai WHEN x.cnt>=si_so_max THEN 'FULL' ELSE 'DANG_MO' END
 FROM dbo.LOP_HOC_PHAN l CROSS APPLY(SELECT COUNT(*) cnt FROM dbo.DANG_KY d WHERE d.ma_lhp=l.ma_lhp AND d.trang_thai='DANG_KY')x;
COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
