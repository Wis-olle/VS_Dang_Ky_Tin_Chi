SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Minh_DocDuLieu
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @sv varchar(20),@role varchar(20);
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 -- Serializable shared locks give all resultsets one consistent database view.
 BEGIN TRY
  BEGIN TRAN;
  DECLARE @identity int,@lock varchar(20),@rc int;
  EXEC @rc=sys.sp_getapplock @Resource=N'Minh:Scheduling',@LockMode='Shared',@LockOwner='Transaction',@LockTimeout=10000;
  IF @rc<0 THROW 52002,N'Dữ liệu đang được cập nhật. Vui lòng thử lại.',1;
  SELECT @identity=ma_tk FROM dbo.TAI_KHOAN WITH(HOLDLOCK) WHERE ten_dang_nhap=ORIGINAL_LOGIN();
  SELECT @lock=ma_hk FROM dbo.HOC_KY WITH(HOLDLOCK) ORDER BY ma_hk;
  IF EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ngay_hoc_bd IS NULL OR ngay_hoc_kt IS NULL)
   THROW 52033,N'Học kỳ chưa có ngày bắt đầu/kết thúc học. Cần bổ sung từ phần học kỳ.',1;
  SELECT @lock=ma_lhp FROM dbo.LOP_HOC_PHAN WITH(HOLDLOCK) ORDER BY ma_lhp;
  SELECT t.ma_tk,t.ma_sv,t.vai_tro,COALESCE(s.ho_ten,N'Phòng đào tạo') ho_ten,s.ma_lop,n.ten_nganh
   FROM dbo.TAI_KHOAN t WITH(HOLDLOCK) LEFT JOIN dbo.SINH_VIEN s WITH(HOLDLOCK) ON s.ma_sv=t.ma_sv
   LEFT JOIN dbo.NGANH n WITH(HOLDLOCK) ON n.ma_nganh=s.ma_nganh WHERE t.ma_tk=@identity;
  SELECT ma_khoa,ten_khoa FROM dbo.KHOA WITH(HOLDLOCK) WHERE trang_thai=1 ORDER BY ten_khoa,ma_khoa;
  SELECT g.ma_gv,g.ho_ten,g.ngay_sinh,g.gioi_tinh,g.email,g.sdt,k.ten_khoa,
   COALESCE(g.hoc_vi,N'Thạc sĩ') hoc_vi,COALESCE(g.avatar_key,'portrait-01') avatar_key,g.trang_thai,
   (SELECT COUNT(*) FROM dbo.LOP_HOC_PHAN l WHERE l.ma_gv=g.ma_gv AND l.trang_thai<>'HUY') active_classes
   FROM dbo.GIANG_VIEN g WITH(HOLDLOCK) JOIN dbo.KHOA k WITH(HOLDLOCK) ON k.ma_khoa=g.ma_khoa ORDER BY g.ma_gv;
  SELECT h.ma_hp,h.ten_hp,h.so_tin_chi,COALESCE(k.ten_khoa,N'Chưa phân khoa') ten_khoa
   FROM dbo.HOC_PHAN h WITH(HOLDLOCK) LEFT JOIN dbo.KHOA k WITH(HOLDLOCK) ON k.ma_khoa=h.ma_khoa ORDER BY h.ma_hp;
  SELECT ma_hk,CONCAT(nam_hoc,N' • Học kỳ ',hoc_ky) ten_hk,ngay_hoc_bd,ngay_hoc_kt
   FROM dbo.HOC_KY WITH(HOLDLOCK) ORDER BY ngay_hoc_bd,ma_hk;
  SELECT ma_phong,toa_nha,suc_chua,loai_phong,trang_thai FROM dbo.PHONG_HOC WITH(HOLDLOCK) ORDER BY ma_phong;
  SELECT v.ma_lhp,v.ma_hp,v.ma_gv,v.ma_hk,v.si_so_max,v.si_so_hien_tai,v.trang_thai
   FROM dbo.v_ThongTinLopHocPhan v WHERE @role<>'SINH_VIEN' OR EXISTS(
    SELECT 1 FROM dbo.DANG_KY d WHERE d.ma_lhp=v.ma_lhp AND d.ma_sv=@sv) ORDER BY v.ma_lhp;
  SELECT c.ma_lhp,c.ma_phong,c.thu,c.tiet_bd,c.tiet_kt,c.ngay_bd,c.ngay_kt FROM dbo.LICH_HOC c WITH(HOLDLOCK)
   WHERE @role<>'SINH_VIEN' OR EXISTS(SELECT 1 FROM dbo.DANG_KY d WHERE d.ma_lhp=c.ma_lhp AND d.ma_sv=@sv)
   ORDER BY c.ma_lhp,c.thu,c.tiet_bd,c.ma_lich;
  SELECT ma_sv,ma_lhp,trang_thai FROM dbo.DANG_KY WITH(HOLDLOCK)
   WHERE @role<>'SINH_VIEN' OR ma_sv=@sv ORDER BY ma_sv,ma_lhp,ma_dk;
  COMMIT;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH
END;
GO
GRANT EXECUTE ON dbo.usp_Minh_DocDuLieu TO app_minh;
GO
