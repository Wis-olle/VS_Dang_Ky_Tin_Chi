SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Minh_LuuLop
 @ma_lhp varchar(20),@ma_hp varchar(20),@ma_gv varchar(20),@ma_hk varchar(20),
 @si_so_max int,@trang_thai varchar(12),@lich_json nvarchar(max),@la_moi bit
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 EXEC dbo.usp_Minh_KiemTraQuyen;
 IF NULLIF(TRIM(@ma_lhp),'') IS NULL OR @ma_lhp LIKE '%[^A-Za-z0-9_-]%' COLLATE Latin1_General_100_BIN2
  OR @si_so_max NOT BETWEEN 1 AND 1000 OR @si_so_max IS NULL OR @la_moi IS NULL
  OR @trang_thai NOT IN ('DANG_MO','DONG','HUY') OR @trang_thai IS NULL
  THROW 52030,N'Mã lớp, sĩ số tối đa hoặc trạng thái không hợp lệ.',1;
 IF ISJSON(@lich_json)<>1 OR @lich_json IS NULL OR LEFT(LTRIM(@lich_json),1)<>'[' OR DATALENGTH(@lich_json)>16000
  THROW 52031,N'Danh sách buổi học không hợp lệ.',1;
 IF (SELECT COUNT(*) FROM OPENJSON(@lich_json))>7 OR EXISTS(SELECT 1 FROM OPENJSON(@lich_json) WHERE type<>5)
  THROW 52031,N'Mỗi lớp chỉ có tối đa 7 buổi học.',1;
 DECLARE @raw TABLE(room nvarchar(max),thu nvarchar(max),bd nvarchar(max),kt nvarchar(max),nb nvarchar(max),nk nvarchar(max));
 INSERT @raw SELECT RoomId,Thu,StartPeriod,EndPeriod,StartDate,EndDate FROM OPENJSON(@lich_json)
 WITH(RoomId nvarchar(max),Thu nvarchar(max),StartPeriod nvarchar(max),EndPeriod nvarchar(max),StartDate nvarchar(max),EndDate nvarchar(max));
 IF EXISTS(SELECT 1 FROM @raw WHERE NULLIF(TRIM(room),N'') IS NULL OR LEN(room)>20
  OR TRY_CONVERT(int,thu) NOT BETWEEN 2 AND 8 OR TRY_CONVERT(int,thu) IS NULL
  OR TRY_CONVERT(int,bd) NOT BETWEEN 1 AND 12 OR TRY_CONVERT(int,bd) IS NULL
  OR TRY_CONVERT(int,kt) NOT BETWEEN TRY_CONVERT(int,bd) AND 12 OR TRY_CONVERT(int,kt) IS NULL
  OR TRY_CONVERT(date,nb,23) IS NULL OR TRY_CONVERT(date,nk,23) IS NULL
  OR LEN(nb)<>10 OR LEN(nk)<>10
  OR TRY_CONVERT(date,nb,23)<'20000101' OR TRY_CONVERT(date,nb,23)>TRY_CONVERT(date,nk,23))
  THROW 52032,N'Buổi học cần phòng, thứ 2–CN, tiết 1–12 và khoảng ngày hợp lệ.',1;
 DECLARE @lich TABLE(id int IDENTITY(1,1),ma_phong varchar(20),thu tinyint,tiet_bd tinyint,tiet_kt tinyint,ngay_bd date,ngay_kt date);
 INSERT @lich(ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt)
 SELECT room,CONVERT(tinyint,thu),CONVERT(tinyint,bd),CONVERT(tinyint,kt),CONVERT(date,nb,23),CONVERT(date,nk,23) FROM @raw;
 IF EXISTS(SELECT 1 FROM @lich WHERE dbo.ufn_Minh_CoNgayHoc(thu,ngay_bd,ngay_kt)=0)
  THROW 52032,N'Khoảng ngày không chứa ngày học đã chọn.',1;
 BEGIN TRY
  BEGIN TRAN; EXEC dbo.usp_Minh_KhoaLapLich;
  DECLARE @old_hk varchar(20),@old_hp varchar(20),@old_gv varchar(20),@lock varchar(20),@count int,
   @start date,@end date,@tc int;
  SELECT @old_hk=ma_hk FROM dbo.LOP_HOC_PHAN WHERE ma_lhp=@ma_lhp;
  -- Same order as Huệ registration/cancellation: semester HOLDLOCK, class UPDLOCK/HOLDLOCK.
  SELECT @lock=ma_hk FROM dbo.HOC_KY WITH(HOLDLOCK) WHERE ma_hk IN (@ma_hk,@old_hk) ORDER BY ma_hk;
  SELECT @start=ngay_hoc_bd,@end=ngay_hoc_kt FROM dbo.HOC_KY WHERE ma_hk=@ma_hk;
  IF @start IS NULL OR @end IS NULL THROW 52033,N'Học kỳ chưa có ngày bắt đầu/kết thúc học. Cần bổ sung từ phần học kỳ.',1;
  SELECT @old_hp=ma_hp,@old_gv=ma_gv FROM dbo.LOP_HOC_PHAN WITH(UPDLOCK,HOLDLOCK) WHERE ma_lhp=@ma_lhp;
  IF @la_moi=1 AND @old_hp IS NOT NULL THROW 52034,N'Mã lớp học phần đã tồn tại.',1;
  IF @la_moi=0 AND @old_hp IS NULL THROW 52035,N'Lớp không còn tồn tại. Hãy tải lại.',1;
  SELECT @tc=so_tin_chi FROM dbo.HOC_PHAN WITH(HOLDLOCK) WHERE ma_hp=@ma_hp AND trang_thai=1;
  IF @tc IS NULL OR @tc>12 THROW 52036,N'Học phần cần đang hoạt động và có từ 1 đến 12 tín chỉ.',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.GIANG_VIEN WITH(HOLDLOCK) WHERE ma_gv=@ma_gv AND trang_thai=1)
   THROW 52037,N'Giảng viên không tồn tại hoặc đang tạm nghỉ.',1;
  IF EXISTS(SELECT 1 FROM @lich c LEFT JOIN dbo.PHONG_HOC p WITH(HOLDLOCK) ON p.ma_phong=c.ma_phong
   WHERE p.ma_phong IS NULL OR p.trang_thai=0 OR p.suc_chua<@si_so_max)
   THROW 52038,N'Phòng không sẵn sàng hoặc không đủ sức chứa cho sĩ số tối đa.',1;
  IF EXISTS(SELECT 1 FROM @lich WHERE ngay_bd<@start OR ngay_kt>@end)
   THROW 52039,N'Ngày buổi học phải nằm trong thời gian học của học kỳ.',1;
  SELECT @count=COUNT(*) FROM dbo.DANG_KY WHERE ma_lhp=@ma_lhp AND trang_thai='DANG_KY';
  IF @count>@si_so_max THROW 52040,N'Sĩ số tối đa không được thấp hơn số đăng ký thực tế.',1;
  IF @count>0 AND (@old_hp<>@ma_hp OR @old_gv<>@ma_gv OR @old_hk<>@ma_hk OR
   EXISTS(SELECT ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt FROM @lich
    EXCEPT SELECT ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt FROM dbo.LICH_HOC WHERE ma_lhp=@ma_lhp) OR
   EXISTS(SELECT ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt FROM dbo.LICH_HOC WHERE ma_lhp=@ma_lhp
    EXCEPT SELECT ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt FROM @lich))
   THROW 52041,N'Lớp đã có sinh viên đăng ký; không thể đổi học phần, học kỳ, giảng viên hoặc buổi học.',1;
  IF @trang_thai='HUY' AND @count>0 THROW 52042,N'Lớp còn đăng ký hợp lệ; cần xử lý đăng ký trước khi hủy lớp.',1;
  IF EXISTS(SELECT 1 FROM @lich a JOIN @lich b ON a.id<b.id AND a.thu=b.thu AND a.tiet_bd<=b.tiet_kt AND b.tiet_bd<=a.tiet_kt
   CROSS APPLY(SELECT CASE WHEN a.ngay_bd>b.ngay_bd THEN a.ngay_bd ELSE b.ngay_bd END bd,
    CASE WHEN a.ngay_kt<b.ngay_kt THEN a.ngay_kt ELSE b.ngay_kt END kt) x
   WHERE dbo.ufn_Minh_CoNgayHoc(a.thu,x.bd,x.kt)=1)
   THROW 52043,N'Các buổi học của lớp trùng nhau.',1;
  IF @trang_thai<>'HUY' AND EXISTS(
   SELECT 1 FROM @lich a JOIN dbo.LICH_HOC b WITH(HOLDLOCK)
    ON a.thu=b.thu AND a.tiet_bd<=b.tiet_kt AND b.tiet_bd<=a.tiet_kt
   JOIN dbo.LOP_HOC_PHAN l ON l.ma_lhp=b.ma_lhp
   CROSS APPLY(SELECT CASE WHEN a.ngay_bd>b.ngay_bd THEN a.ngay_bd ELSE b.ngay_bd END bd,
    CASE WHEN a.ngay_kt<b.ngay_kt THEN a.ngay_kt ELSE b.ngay_kt END kt) x
   WHERE l.ma_lhp<>@ma_lhp AND l.trang_thai<>'HUY' AND (a.ma_phong=b.ma_phong OR l.ma_gv=@ma_gv)
    AND dbo.ufn_Minh_CoNgayHoc(a.thu,x.bd,x.kt)=1)
   THROW 52044,N'Trùng phòng hoặc lịch giảng viên vào ngày học thực tế.',1;
  DECLARE @state varchar(12)=CASE WHEN @trang_thai='DANG_MO' AND @count>=@si_so_max THEN 'FULL' ELSE @trang_thai END;
  IF @la_moi=1 INSERT dbo.LOP_HOC_PHAN(ma_lhp,ma_hp,ma_gv,ma_hk,ma_phong,si_so_max,si_so_hien_tai,trang_thai)
   VALUES(@ma_lhp,@ma_hp,@ma_gv,@ma_hk,NULL,@si_so_max,@count,@state);
  ELSE UPDATE dbo.LOP_HOC_PHAN SET ma_hp=@ma_hp,ma_gv=@ma_gv,ma_hk=@ma_hk,
   si_so_max=@si_so_max,si_so_hien_tai=@count,trang_thai=@state WHERE ma_lhp=@ma_lhp;
  -- Keep schedule row identities when registration is active and the schedule is unchanged.
  IF @count=0
  BEGIN
   DELETE dbo.LICH_HOC WHERE ma_lhp=@ma_lhp;
   INSERT dbo.LICH_HOC(ma_lhp,ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt)
    SELECT @ma_lhp,ma_phong,thu,tiet_bd,tiet_kt,ngay_bd,ngay_kt FROM @lich;
  END;
  COMMIT;
  SELECT @count AS si_so_hien_tai;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Minh_XoaLop @ma_lhp varchar(20)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 EXEC dbo.usp_Minh_KiemTraQuyen;
 BEGIN TRY
  BEGIN TRAN; EXEC dbo.usp_Minh_KhoaLapLich;
  DECLARE @hk varchar(20),@lock varchar(20);
  SELECT @hk=ma_hk FROM dbo.LOP_HOC_PHAN WHERE ma_lhp=@ma_lhp;
  SELECT @lock=ma_hk FROM dbo.HOC_KY WITH(HOLDLOCK) WHERE ma_hk=@hk;
  SELECT @lock=ma_lhp FROM dbo.LOP_HOC_PHAN WITH(UPDLOCK,HOLDLOCK) WHERE ma_lhp=@ma_lhp;
  IF @lock IS NULL THROW 52035,N'Lớp không còn tồn tại. Hãy tải lại.',1;
  IF EXISTS(SELECT 1 FROM dbo.DANG_KY WHERE ma_lhp=@ma_lhp)
   THROW 52045,N'Lớp có lịch sử đăng ký; không thể xóa.',1;
  DELETE dbo.LICH_HOC WHERE ma_lhp=@ma_lhp;
  DELETE dbo.LOP_HOC_PHAN WHERE ma_lhp=@ma_lhp;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
END;
GO
GRANT EXECUTE ON dbo.usp_Minh_LuuLop TO app_minh;
GRANT EXECUTE ON dbo.usp_Minh_XoaLop TO app_minh;
GO
