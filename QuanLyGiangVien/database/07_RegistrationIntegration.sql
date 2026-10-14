-- Integration adapter for Huệ Function_4 @ 77b70bc and SQL_Function_4 @ e4b04e1.
-- Does not change registration rules/schema, create logins, seed data, or grant direct table access.
-- Existing Huệ procedures validate TAI_KHOAN + ORIGINAL_LOGIN() on every call.
SET XACT_ABORT ON;
IF DATABASE_PRINCIPAL_ID(N'app_minh') IS NULL
    THROW 57001,N'Hãy chạy các migration Minh trước khi cấp quyền tích hợp.',1;
GO
-- Student entrypoints share this default order instead of independently selecting
-- opposite ends of the semester catalog. Teaching dates are additive metadata;
-- original callers continue binding ma_hk/ten_hk by name.
CREATE OR ALTER PROCEDURE dbo.usp_LayHocKy
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20),
 @today date=CONVERT(date,DATEADD(HOUR,7,SYSUTCDATETIME()));
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 SELECT ma_hk,CONCAT(nam_hoc,N' • Học kỳ ',hoc_ky) AS ten_hk,ngay_hoc_bd,ngay_hoc_kt
 FROM dbo.HOC_KY
 ORDER BY CASE WHEN @today BETWEEN ngay_hoc_bd AND ngay_hoc_kt THEN 0
   WHEN ngay_hoc_bd>@today THEN 1 WHEN ngay_hoc_kt<@today THEN 2 ELSE 3 END,
 CASE WHEN @today BETWEEN ngay_hoc_bd AND ngay_hoc_kt THEN ngay_hoc_bd END DESC,
 CASE WHEN ngay_hoc_bd>@today THEN ngay_hoc_bd END,
 CASE WHEN ngay_hoc_kt<@today THEN ngay_hoc_kt END DESC,
 nam_hoc DESC,hoc_ky DESC,ma_hk;
END;
GO
-- Bug fix proven by RegistrationIntegrationTests: CHARINDEX(N'', value)=0 on SQL Server,
-- so the original blank initial lookup hid every open class. Preserve the original filter,
-- explicitly treating an empty keyword as no text filter. The checksum of 00_Hue.sql stays intact.
CREATE OR ALTER PROCEDURE dbo.usp_TraCuuLopDangMo
 @ma_hk varchar(20),@tu_khoa nvarchar(100)=N'',@chi_con_cho bit=0
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @sv varchar(20),@role varchar(20);
 EXEC dbo.usp_XacThucTaiKhoan @sv OUTPUT,@role OUTPUT;
 SELECT ma_lhp,ma_hp,ten_hp,so_tin_chi,loai_hp,ten_gv,lich_hoc,
 si_so_hien_tai,si_so_max,so_cho_con,trang_thai
 FROM dbo.v_ThongTinLopHocPhan
 WHERE ma_hk=@ma_hk AND trang_thai IN ('DANG_MO','FULL') AND hp_hoat_dong=1
 AND (@chi_con_cho=0 OR so_cho_con>0)
 AND (@tu_khoa=N'' OR CHARINDEX(@tu_khoa,ma_lhp)>0 OR CHARINDEX(@tu_khoa,ma_hp)>0 OR CHARINDEX(@tu_khoa,ten_hp)>0)
 ORDER BY ma_hp,ma_lhp;
END;
GO
GRANT EXECUTE ON dbo.usp_LayNguoiDung TO app_minh;
GRANT EXECUTE ON dbo.usp_LayHocKy TO app_minh;
GRANT EXECUTE ON dbo.usp_KiemTraThoiHan TO app_minh;
GRANT EXECUTE ON dbo.usp_TraCuuLopDangMo TO app_minh;
GRANT EXECUTE ON dbo.usp_DangKyHocPhan TO app_minh;
GRANT EXECUTE ON dbo.usp_HuyDangKy TO app_minh;
GRANT EXECUTE ON dbo.usp_XemKetQuaDangKy TO app_minh;
GRANT EXECUTE ON dbo.usp_ThongKeLopHocPhan TO app_minh;
GRANT EXECUTE ON dbo.usp_DanhSachSinhVienTheoLop TO app_minh;
GO
