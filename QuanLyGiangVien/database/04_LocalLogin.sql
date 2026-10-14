-- Optional local bootstrap. Windows integrated auth; no password or SQL login is created.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'DangKyTinChi_Minh' AND DB_NAME() NOT LIKE N'DangKyTinChi_Minh_Test[_]%'
 THROW 52090,N'Ánh xạ tự động chỉ dành cho database Minh riêng hoặc kiểm thử dùng một lần.',1;
DECLARE @login sysname=ORIGINAL_LOGIN(),@principal sysname,@sql nvarchar(max);
IF CONVERT(varchar(20),CONNECTIONPROPERTY('auth_scheme')) NOT IN ('NTLM','KERBEROS')
 THROW 52091,N'Bootstrap yêu cầu tài khoản Windows thực tế; không tạo tài khoản/mật khẩu SQL.',1;
IF CONNECTIONPROPERTY('net_transport')<>N'Shared memory'
 AND COALESCE(CONVERT(varchar(48),CONNECTIONPROPERTY('client_net_address')),'') NOT IN ('127.0.0.1','::1','<local machine>')
 THROW 52092,N'Chỉ cho phép bootstrap từ kết nối cục bộ.',1;
BEGIN TRY
 BEGIN TRAN;
 IF EXISTS(SELECT 1 FROM dbo.TAI_KHOAN WHERE ten_dang_nhap=@login AND (vai_tro<>'DAO_TAO' OR trang_thai<>1))
  THROW 52093,N'Tài khoản đã có ánh xạ khác; không tự đổi vai trò hoặc mở khóa.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.TAI_KHOAN WHERE ten_dang_nhap=@login)
  INSERT dbo.TAI_KHOAN(ten_dang_nhap,vai_tro,trang_thai) VALUES(@login,'DAO_TAO',1);
 SELECT @principal=name FROM sys.database_principals WHERE sid=SUSER_SID(@login) AND type IN ('U','G');
 IF @principal IS NULL
 BEGIN
  SET @principal=@login;
  -- Windows group membership can grant server access without a personal server login.
  -- Sysadmin already resolves as dbo and needs no additional database principal.
  IF IS_SRVROLEMEMBER('sysadmin')=1 SET @principal=N'dbo';
  ELSE IF EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@login AND type IN ('U','G'))
   SET @sql=N'CREATE USER '+QUOTENAME(@principal)+N' FOR LOGIN '+QUOTENAME(@login)+N';';
  ELSE SET @sql=N'CREATE USER '+QUOTENAME(@principal)+N' FROM WINDOWS;';
  IF @sql IS NOT NULL EXEC sys.sp_executesql @sql;
 END;
 IF @principal<>N'dbo' AND IS_ROLEMEMBER('app_minh',@principal)<>1
 BEGIN
  SET @sql=N'ALTER ROLE app_minh ADD MEMBER '+QUOTENAME(@principal)+N';';
  EXEC sys.sp_executesql @sql;
 END;
 COMMIT;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
GO
