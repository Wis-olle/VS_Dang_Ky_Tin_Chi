-- Role-specific read model: no personal lecturer fields are sent to a student.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.usp_SinhVien_ThoiKhoaBieu AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @sv varchar(20),@role varchar(20);
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 IF @role<>'SINH_VIEN' THROW 51005,N'Thời khóa biểu này chỉ dành cho sinh viên.',1;
 BEGIN TRY
  BEGIN TRAN;
  DECLARE @rc int,@lock varchar(20),@identity int=dbo.ufn_Campus_AccountId();
  EXEC @rc=sys.sp_getapplock @Resource=N'Minh:Scheduling',@LockMode='Shared',@LockOwner='Transaction',@LockTimeout=10000;
  IF @rc<0 THROW 52002,N'Dữ liệu đang cập nhật. Vui lòng thử lại.',1;
  SELECT @lock=ma_hk FROM dbo.HOC_KY WITH(HOLDLOCK) ORDER BY ma_hk;
  SELECT @lock=ma_lhp FROM dbo.LOP_HOC_PHAN WITH(HOLDLOCK) ORDER BY ma_lhp;
  DECLARE @own TABLE(ma_lhp varchar(20) PRIMARY KEY);
  INSERT @own SELECT DISTINCT ma_lhp FROM dbo.DANG_KY WITH(HOLDLOCK) WHERE ma_sv=@sv;
  SELECT t.ma_tk,t.ma_sv,t.vai_tro,s.ho_ten,s.ma_lop,n.ten_nganh
   FROM dbo.TAI_KHOAN t WITH(HOLDLOCK) JOIN dbo.SINH_VIEN s WITH(HOLDLOCK) ON s.ma_sv=t.ma_sv
   LEFT JOIN dbo.NGANH n WITH(HOLDLOCK) ON n.ma_nganh=s.ma_nganh WHERE t.ma_tk=@identity;
  SELECT ma_hk,CONCAT(nam_hoc,N' • Học kỳ ',hoc_ky) ten_hk,ngay_hoc_bd,ngay_hoc_kt
   FROM dbo.HOC_KY WITH(HOLDLOCK) ORDER BY ngay_hoc_bd,ma_hk;
  SELECT g.ma_gv,g.ho_ten,g.trang_thai FROM dbo.GIANG_VIEN g WITH(HOLDLOCK)
   WHERE EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN l JOIN @own o ON o.ma_lhp=l.ma_lhp WHERE l.ma_gv=g.ma_gv) ORDER BY g.ma_gv;
  SELECT h.ma_hp,h.ten_hp,h.so_tin_chi,COALESCE(k.ten_khoa,N'Chưa phân khoa') ten_khoa
   FROM dbo.HOC_PHAN h WITH(HOLDLOCK) LEFT JOIN dbo.KHOA k WITH(HOLDLOCK) ON k.ma_khoa=h.ma_khoa
   WHERE EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN l JOIN @own o ON o.ma_lhp=l.ma_lhp WHERE l.ma_hp=h.ma_hp) ORDER BY h.ma_hp;
  SELECT p.ma_phong,p.toa_nha,p.suc_chua,p.loai_phong,p.trang_thai FROM dbo.PHONG_HOC p WITH(HOLDLOCK)
   WHERE EXISTS(SELECT 1 FROM dbo.LICH_HOC c JOIN @own o ON o.ma_lhp=c.ma_lhp WHERE c.ma_phong=p.ma_phong) ORDER BY p.ma_phong;
  SELECT v.ma_lhp,v.ma_hp,v.ma_gv,v.ma_hk,v.si_so_max,v.si_so_hien_tai,v.trang_thai
   FROM dbo.v_ThongTinLopHocPhan v JOIN @own o ON o.ma_lhp=v.ma_lhp ORDER BY v.ma_lhp;
  SELECT c.ma_lhp,c.ma_phong,c.thu,c.tiet_bd,c.tiet_kt,c.ngay_bd,c.ngay_kt
   FROM dbo.LICH_HOC c WITH(HOLDLOCK) JOIN @own o ON o.ma_lhp=c.ma_lhp ORDER BY c.ma_lhp,c.thu,c.tiet_bd,c.ma_lich;
  SELECT ma_sv,ma_lhp,trang_thai FROM dbo.DANG_KY WITH(HOLDLOCK) WHERE ma_sv=@sv ORDER BY ma_lhp,ma_dk;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
GRANT EXECUTE ON dbo.usp_SinhVien_ThoiKhoaBieu TO app_minh;
GO
-- Guard the old entry point as well: hiding UI fields does not protect data.
DECLARE @definition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'dbo.usp_Minh_DocDuLieu'));
IF @definition IS NULL THROW 52410,N'Thiếu snapshot quản trị.',1;
IF CHARINDEX(N'EXEC dbo.usp_Minh_KiemTraQuyen;',@definition)=0
BEGIN
 SET @definition=REPLACE(@definition,N'EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;',
  N'EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT; EXEC dbo.usp_Minh_KiemTraQuyen;');
 IF CHARINDEX(N'EXEC dbo.usp_Minh_KiemTraQuyen;',@definition)=0
  THROW 52410,N'Không thể bảo vệ snapshot quản trị. Dừng nâng cấp.',1;
 SET @definition=N'ALTER PROCEDURE'+SUBSTRING(@definition,CHARINDEX(N'PROCEDURE',UPPER(@definition))+9,LEN(@definition));
 EXEC sys.sp_executesql @definition;
END;
GO
