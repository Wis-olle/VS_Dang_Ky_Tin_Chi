USE DangKyTinChi;
GO
/* Token thay đổi chỉ ở VIEW, không thêm cột vào 13 bảng quy ước. */
CREATE OR ALTER VIEW dbo.vw_HocPhanPhong AS
SELECT *, CONVERT(binary(32),HASHBYTES('SHA2_256',CONCAT(
 LEN(ma_hp),':',ma_hp,LEN(ten_hp),':',ten_hp,'|',so_tin_chi,'|',loai_hp,'|',tin_chi_min,'|',tin_chi_max,'|',trang_thai))) AS phien_ban
FROM dbo.HOC_PHAN;
GO
CREATE OR ALTER VIEW dbo.vw_HocKyPhong AS
SELECT *, CONVERT(binary(32),HASHBYTES('SHA2_256',CONCAT(ma_hk,'|',nam_hoc,'|',hoc_ky,'|',
 CONVERT(varchar(19),tg_bat_dau,126),'|',CONVERT(varchar(19),tg_ket_thuc,126),'|',CONVERT(varchar(19),han_huy_dk,126),'|',trang_thai))) AS phien_ban,
 CONVERT(bit,CASE WHEN trang_thai='MO' AND DATEADD(HOUR,7,SYSUTCDATETIME()) BETWEEN tg_bat_dau AND tg_ket_thuc THEN 1 ELSE 0 END) AS cho_phep_dang_ky,
 CONVERT(bit,CASE WHEN DATEADD(HOUR,7,SYSUTCDATETIME()) BETWEEN tg_bat_dau AND han_huy_dk THEN 1 ELSE 0 END) AS cho_phep_huy,
 CASE WHEN trang_thai='DONG' THEN N'Đã đóng' WHEN DATEADD(HOUR,7,SYSUTCDATETIME())<tg_bat_dau THEN N'Chưa đến thời gian' WHEN DATEADD(HOUR,7,SYSUTCDATETIME())>tg_ket_thuc THEN N'Hết hạn đăng ký' ELSE N'Đang mở đăng ký' END AS trang_thai_hieu_luc
FROM dbo.HOC_KY;
GO
CREATE OR ALTER PROCEDURE dbo.usp_HocPhan_DanhSach
 @tu_khoa nvarchar(200)=N'', @loai_hp varchar(20)=NULL, @trang_thai bit=NULL
AS
BEGIN
 SET NOCOUNT ON;
 -- CHARINDEX coi %, _, [ là ký tự tìm kiếm bình thường, không phải wildcard.
 SELECT * FROM dbo.vw_HocPhanPhong
 WHERE (@tu_khoa=N'' OR CHARINDEX(@tu_khoa,ma_hp)>0 OR CHARINDEX(@tu_khoa,ten_hp)>0)
 AND (@loai_hp IS NULL OR loai_hp=@loai_hp)
 AND (@trang_thai IS NULL OR trang_thai=@trang_thai)
 ORDER BY ma_hp;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_HocPhan_Luu
 @la_moi bit, @ma_hp varchar(20), @ten_hp nvarchar(200), @so_tin_chi tinyint,
 @loai_hp varchar(20), @tin_chi_min tinyint, @tin_chi_max tinyint, @trang_thai bit,
 @phien_ban binary(32)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 SET @ma_hp=UPPER(LTRIM(RTRIM(@ma_hp))); SET @ten_hp=LTRIM(RTRIM(@ten_hp));
 IF @la_moi IS NULL OR @trang_thai IS NULL OR @ma_hp IS NULL OR @ma_hp='' OR @ma_hp LIKE '%[^A-Za-z0-9_-]%' COLLATE Latin1_General_100_BIN2
  THROW 51001,N'Mã học phần chỉ gồm chữ, số, dấu gạch ngang hoặc gạch dưới.',1;
 IF @ten_hp IS NULL OR LEN(@ten_hp)=0 THROW 51002,N'Chưa nhập tên học phần.',1;
 IF @loai_hp IS NULL OR @loai_hp NOT IN('BAT_BUOC','TU_CHON') THROW 51003,N'Loại học phần không hợp lệ.',1;
 IF @so_tin_chi IS NULL OR @tin_chi_min IS NULL OR @tin_chi_max IS NULL OR @tin_chi_min<1 OR @tin_chi_max>30 OR @tin_chi_min>@so_tin_chi OR @so_tin_chi>@tin_chi_max
  THROW 51004,N'Phải có 1 ≤ tín chỉ tối thiểu ≤ số tín chỉ ≤ tín chỉ tối đa ≤ 30.',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @token binary(32);
  SELECT @token=phien_ban FROM dbo.vw_HocPhanPhong WITH(UPDLOCK,HOLDLOCK) WHERE ma_hp=@ma_hp;
  IF @la_moi=1
  BEGIN
   IF @token IS NOT NULL THROW 51005,N'Mã học phần đã tồn tại.',1;
   INSERT dbo.HOC_PHAN(ma_hp,ten_hp,so_tin_chi,loai_hp,tin_chi_min,tin_chi_max,trang_thai)
   VALUES(@ma_hp,@ten_hp,@so_tin_chi,@loai_hp,@tin_chi_min,@tin_chi_max,@trang_thai);
  END
  ELSE
  BEGIN
   IF @token IS NULL THROW 51006,N'Học phần không còn tồn tại. Hãy tải lại danh sách.',1;
   IF @phien_ban IS NULL OR @token<>@phien_ban THROW 51007,N'Dữ liệu đã được người khác sửa. Hãy tải lại trước khi lưu.',1;
   IF EXISTS(SELECT 1 FROM dbo.HOC_PHAN WHERE ma_hp=@ma_hp AND so_tin_chi<>@so_tin_chi)
    AND (EXISTS(SELECT 1 FROM dbo.KET_QUA WHERE ma_hp=@ma_hp) OR EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN l JOIN dbo.DANG_KY d ON d.ma_lhp=l.ma_lhp WHERE l.ma_hp=@ma_hp))
    THROW 51008,N'Học phần đã có đăng ký/kết quả; không đổi số tín chỉ để bảo toàn dữ liệu cũ.',1;
   UPDATE dbo.HOC_PHAN SET ten_hp=@ten_hp,so_tin_chi=@so_tin_chi,loai_hp=@loai_hp,
    tin_chi_min=@tin_chi_min,tin_chi_max=@tin_chi_max,trang_thai=@trang_thai WHERE ma_hp=@ma_hp;
  END;
  COMMIT;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_HocKy_DanhSach @tu_khoa nvarchar(200)=N''
AS
BEGIN
 SET NOCOUNT ON;
 SELECT * FROM dbo.vw_HocKyPhong WHERE @tu_khoa=N'' OR CHARINDEX(@tu_khoa,ma_hk)>0 OR CHARINDEX(@tu_khoa,nam_hoc)>0
 ORDER BY nam_hoc DESC,hoc_ky DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_HocKy_Luu
 @la_moi bit,@ma_hk varchar(20),@nam_hoc varchar(9),@hoc_ky tinyint,
 @tg_bat_dau datetime2(0),@tg_ket_thuc datetime2(0),@han_huy_dk datetime2(0),
 @phien_ban binary(32)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 SET @ma_hk=UPPER(LTRIM(RTRIM(@ma_hk)));
 IF @la_moi IS NULL OR @ma_hk IS NULL OR @ma_hk='' OR @ma_hk LIKE '%[^A-Za-z0-9_-]%' COLLATE Latin1_General_100_BIN2
  THROW 51101,N'Mã học kỳ không hợp lệ.',1;
 IF @nam_hoc IS NULL OR @nam_hoc NOT LIKE '[12][0-9][0-9][0-9]-[12][0-9][0-9][0-9]' OR TRY_CONVERT(int,RIGHT(@nam_hoc,4))<>TRY_CONVERT(int,LEFT(@nam_hoc,4))+1
  THROW 51102,N'Năm học phải có dạng 2026-2027, hai năm liên tiếp.',1;
 IF @hoc_ky IS NULL OR @hoc_ky NOT IN(1,2,3) THROW 51103,N'Học kỳ phải là 1, 2 hoặc 3 (hè).',1;
 IF @tg_bat_dau IS NULL OR @tg_ket_thuc IS NULL OR @han_huy_dk IS NULL OR @tg_bat_dau>=@tg_ket_thuc OR @han_huy_dk<@tg_bat_dau
  THROW 51104,N'Bắt đầu phải trước kết thúc; hạn hủy không được trước bắt đầu.',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @token binary(32);
  SELECT @token=phien_ban FROM dbo.vw_HocKyPhong WITH(UPDLOCK,HOLDLOCK) WHERE ma_hk=@ma_hk;
  IF EXISTS(SELECT 1 FROM dbo.HOC_KY WITH(UPDLOCK,HOLDLOCK) WHERE nam_hoc=@nam_hoc AND hoc_ky=@hoc_ky AND ma_hk<>@ma_hk)
   THROW 51105,N'Năm học này đã có học kỳ được chọn.',1;
  IF @la_moi=1
  BEGIN
   IF @token IS NOT NULL THROW 51106,N'Mã học kỳ đã tồn tại.',1;
   INSERT dbo.HOC_KY(ma_hk,nam_hoc,hoc_ky,tg_bat_dau,tg_ket_thuc,han_huy_dk,trang_thai)
    VALUES(@ma_hk,@nam_hoc,@hoc_ky,@tg_bat_dau,@tg_ket_thuc,@han_huy_dk,'DONG');
  END
  ELSE
  BEGIN
   IF @token IS NULL THROW 51107,N'Học kỳ không còn tồn tại.',1;
   IF @phien_ban IS NULL OR @token<>@phien_ban THROW 51108,N'Học kỳ đã được người khác sửa. Hãy tải lại trước khi lưu.',1;
   IF EXISTS(SELECT 1 FROM dbo.LOP_HOC_PHAN WHERE ma_hk=@ma_hk) AND EXISTS(SELECT 1 FROM dbo.HOC_KY WHERE ma_hk=@ma_hk AND (nam_hoc<>@nam_hoc OR hoc_ky<>@hoc_ky))
    THROW 51109,N'Học kỳ đã có lớp; không đổi năm học hoặc số học kỳ.',1;
   UPDATE dbo.HOC_KY SET nam_hoc=@nam_hoc,hoc_ky=@hoc_ky,tg_bat_dau=@tg_bat_dau,
    tg_ket_thuc=@tg_ket_thuc,han_huy_dk=@han_huy_dk WHERE ma_hk=@ma_hk;
  END;
  COMMIT;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_HocKy_DoiTrangThai
 @ma_hk varchar(20),@trang_thai varchar(10),@phien_ban binary(32)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @trang_thai IS NULL OR @trang_thai NOT IN('MO','DONG') THROW 51201,N'Trạng thái phải là MO hoặc DONG.',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @token binary(32),@ket_thuc datetime2(0);
  SELECT @token=phien_ban,@ket_thuc=tg_ket_thuc FROM dbo.vw_HocKyPhong WITH(UPDLOCK,HOLDLOCK) WHERE ma_hk=@ma_hk;
  IF @token IS NULL THROW 51202,N'Không tìm thấy học kỳ.',1;
  IF @phien_ban IS NULL OR @token<>@phien_ban THROW 51203,N'Dữ liệu đã thay đổi. Hãy tải lại danh sách.',1;
  IF @trang_thai='MO' AND DATEADD(HOUR,7,SYSUTCDATETIME())>@ket_thuc THROW 51204,N'Đợt đã hết hạn; chỉnh thời gian trước khi mở.',1;
  UPDATE dbo.HOC_KY SET trang_thai=@trang_thai WHERE ma_hk=@ma_hk;
  COMMIT;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH;
END;
GO
/* si_so_hien_tai là cache do trigger quản lý; module đăng ký KHÔNG tự tăng/giảm. */
CREATE OR ALTER TRIGGER dbo.tr_DangKy_SiSo ON dbo.DANG_KY AFTER INSERT,UPDATE,DELETE
AS
BEGIN
 SET NOCOUNT ON;
 -- Cập nhật theo delta giữ đúng sĩ số khi có đăng ký đồng thời.
 ;WITH delta AS (
  SELECT ma_lhp,SUM(so_luong) AS so_luong FROM (
   SELECT ma_lhp,1 AS so_luong FROM inserted WHERE trang_thai='DA_DANG_KY'
   UNION ALL SELECT ma_lhp,-1 FROM deleted WHERE trang_thai='DA_DANG_KY'
  ) s GROUP BY ma_lhp
 )
 UPDATE l SET si_so_hien_tai=l.si_so_hien_tai+d.so_luong
 FROM dbo.LOP_HOC_PHAN l JOIN delta d ON d.ma_lhp=l.ma_lhp;
 -- CHECK CK_LHP_ss sẽ rollback cả đăng ký nếu vượt sĩ số tối đa.
END;
GO
CREATE OR ALTER VIEW dbo.vw_LopHocPhanMo AS
SELECT l.*,h.ten_hp,h.so_tin_chi,h.loai_hp,k.nam_hoc,k.hoc_ky,
 k.cho_phep_dang_ky,k.cho_phep_huy,
 CASE WHEN l.trang_thai='MO' AND l.si_so_hien_tai>=l.si_so_max THEN 'FULL' ELSE l.trang_thai END AS trang_thai_hieu_luc
FROM dbo.LOP_HOC_PHAN l JOIN dbo.HOC_PHAN h ON h.ma_hp=l.ma_hp
JOIN dbo.vw_HocKyPhong k ON k.ma_hk=l.ma_hk
WHERE h.trang_thai=1 AND l.trang_thai='MO';
GO
PRINT N'Đã cài 5 procedure phần Phong, các view và trigger sĩ số.';
