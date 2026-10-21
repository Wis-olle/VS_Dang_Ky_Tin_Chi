[CmdletBinding()]
param(
    [ValidatePattern('^(localhost|\.|\(local\))(\\[A-Za-z0-9_]+)?$')][string]$Server = '.',
    [ValidatePattern('^DangKyTinChi_Minh(_Test_[A-Za-z0-9_]+)?$')][string]$DatabaseName = 'DangKyTinChi_Minh',
    [switch]$ApplyUitReference
)
$ErrorActionPreference = 'Stop'
if (-not $ApplyUitReference) { throw 'Chọn -ApplyUitReference để áp dụng bộ tham khảo UIT (790/2022 + 1393/2023). Xem docs/QUY_CHE_HOC_VU_VIET_NAM.md trước khi dùng.' }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = Join-Path $repoRoot '.tmp/academic-policy'
[IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
$sqlPath = Join-Path $artifactRoot ([Guid]::NewGuid().ToString('N') + '.sql')
$sql = @'
SET NOCOUNT ON; SET XACT_ABORT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.CAMPUS_SCHEMA_VERSION WHERE id=1 AND version IN(2,3,4))
 THROW 52503,N'Nâng cấp schema giai đoạn 2 trước khi áp dụng bộ tham khảo.',1;
-- This is an explicitly selected reference policy, not an official curriculum or imported grades.
-- No student assignment, result, standing, hold, credential or registration is fabricated.
DECLARE @term varchar(20),@semester int,@standing varchar(20),@id varchar(20),@payload nvarchar(max),@version binary(8),@created int=0;
BEGIN TRY
 BEGIN TRAN;
 EXEC dbo.usp_HocVu_Lock;
 DECLARE policies CURSOR LOCAL FAST_FORWARD FOR
  SELECT h.ma_hk,h.hoc_ky,s.standing FROM dbo.HOC_KY h CROSS JOIN (VALUES('*'),('WARNING')) s(standing) ORDER BY h.ma_hk,s.standing;
 OPEN policies; FETCH NEXT FROM policies INTO @term,@semester,@standing;
 WHILE @@FETCH_STATUS=0
 BEGIN
  SET @id='UIT_'+LEFT(CONVERT(varchar(64),HASHBYTES('SHA2_256',@term),2),12)+CASE WHEN @standing='*' THEN '_N' ELSE '_W' END;
  SET @payload=(SELECT CONCAT(N'Tham khảo UIT · ',@term,CASE WHEN @standing='WARNING' THEN N' · Cảnh báo' ELSE N' · Chung' END) name,
   @term term,@standing standing,CASE WHEN @semester=3 THEN 0 ELSE 14 END minCredits,
   CASE WHEN @semester=3 THEN 12 ELSE 24 END maxCredits,CONVERT(decimal(4,2),5) passGrade,
   CONVERT(bit,1) allowImprove,0 maxAttempts,CONVERT(bit,0) enforceCurriculum,
   CASE WHEN @semester<>3 AND @standing='*' THEN CONVERT(decimal(4,2),8) END excellentThreshold,
   CASE WHEN @semester<>3 AND @standing='*' THEN 30 END excellentMaxCredits,
   CONVERT(bit,CASE WHEN @standing='WARNING' THEN 1 ELSE 0 END) warningRetakeOnly,
   N'UIT QĐ790/2022, Điều 14; sửa đổi QĐ1393/2023. https://oep.uit.edu.vn/vi/quy-che-dao-tao-theo-hoc-che-tin-chi' source
   FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  IF EXISTS(SELECT 1 FROM dbo.HV_DOCUMENT WHERE kind='POLICY' AND id=@id)
  BEGIN
   IF NOT EXISTS(SELECT 1 FROM dbo.HV_DOCUMENT WHERE kind='POLICY' AND id=@id AND published=1 AND payload=@payload)
    THROW 52603,N'Bộ mẫu đã có thay đổi hoặc đang Nháp; không tự ghi đè. Kiểm tra màn Học vụ.',1;
  END
  ELSE
  BEGIN
   EXEC dbo.usp_HocVu_Save @kind='POLICY',@id=@id,@payload=@payload;
   SELECT @version=version FROM dbo.HV_DOCUMENT WHERE kind='POLICY' AND id=@id;
   EXEC dbo.usp_HocVu_Publish @kind='POLICY',@id=@id,@version=@version;
   SET @created=@created+1;
  END;
  FETCH NEXT FROM policies INTO @term,@semester,@standing;
 END;
 CLOSE policies; DEALLOCATE policies;
 COMMIT;
 SELECT @created AS so_quy_che_moi;
 PRINT N'Đã áp dụng bộ tham khảo UIT: giới hạn TC/điểm/học lại. Chưa áp dụng kiểm CTĐT khi chưa có chương trình và nguồn điểm thật; ứng dụng hiển thị cảnh báo.';
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
'@
[IO.File]::WriteAllText($sqlPath, $sql, [Text.UTF8Encoding]::new($false))
& sqlcmd -S $Server -E -C -I -x -l 5 -t 60 -b -f 65001 -d $DatabaseName -i $sqlPath
if ($LASTEXITCODE -ne 0) { throw 'Chưa áp dụng bộ quy chế tham khảo. Xem lỗi phía trên; không tự sửa dữ liệu học vụ hiện có.' }
