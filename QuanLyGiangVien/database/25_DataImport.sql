-- Atomic, sourced imports through the existing guarded application procedures.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('dbo.DK4_IMPORT_BATCH','U') IS NULL
 CREATE TABLE dbo.DK4_IMPORT_BATCH(actor int NOT NULL,batch_id uniqueidentifier NOT NULL,payload_hash binary(32) NOT NULL,
 source nvarchar(500) NOT NULL,row_count int NOT NULL,applied_utc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),PRIMARY KEY(actor,batch_id));
GO
CREATE OR ALTER PROCEDURE dbo.usp_Import_ApplyRow @kind varchar(20),@id varchar(20),@version varchar(64),@payload nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @v8 binary(8)=TRY_CONVERT(binary(8),@version,2),@v32 binary(32)=TRY_CONVERT(binary(32),@version,2),@new bit=CASE WHEN @version IS NULL THEN 1 ELSE 0 END;
 IF @kind IN('PROGRAM','GROUP','COURSE','REQUIREMENT','ASSIGNMENT','RESULT')
  EXEC dbo.usp_HocVu_Save @kind,@id,@v8,@payload;
 ELSE IF @kind='STUDENT' BEGIN
  DECLARE @name nvarchar(4000)=JSON_VALUE(@payload,'$.name'),@birthText varchar(100)=JSON_VALUE(@payload,'$.birthDate'),
   @birth date=TRY_CONVERT(date,JSON_VALUE(@payload,'$.birthDate'),23),@gender nvarchar(128)=JSON_VALUE(@payload,'$.gender'),
   @email varchar(1000)=JSON_VALUE(@payload,'$.email'),@phone varchar(128)=JSON_VALUE(@payload,'$.phone'),@address nvarchar(4000)=JSON_VALUE(@payload,'$.address'),
   @major varchar(128)=JSON_VALUE(@payload,'$.major'),@class varchar(128)=JSON_VALUE(@payload,'$.class'),
   @active bit=CASE JSON_VALUE(@payload,'$.active') WHEN 'true' THEN 1 WHEN 'false' THEN 0 END;
  IF @birthText IS NOT NULL AND (@birth IS NULL OR LEN(@birthText)<>10) THROW 53302,N'Ngày sinh cần định dạng yyyy-MM-dd hoặc null.',1;
  EXEC dbo.usp_SinhVien_Luu @new,@id,@name,@birth,@gender,@email,@phone,@address,@major,@class,@active,@v32;
 END ELSE IF @kind='CLASS' BEGIN
  DECLARE @courseText nvarchar(4000)=JSON_VALUE(@payload,'$.course'),@lecturerText nvarchar(4000)=JSON_VALUE(@payload,'$.lecturer'),
   @termText nvarchar(4000)=JSON_VALUE(@payload,'$.term'),@statusText nvarchar(4000)=JSON_VALUE(@payload,'$.status');
  IF LEN(@courseText)>20 OR LEN(@lecturerText)>20 OR LEN(@termText)>20 OR LEN(@statusText)>12
   THROW 53302,N'Mã tham chiếu lớp vượt giới hạn.',1;
  DECLARE @course varchar(20)=@courseText,@lecturer varchar(20)=@lecturerText,@term varchar(20)=@termText,@status varchar(12)=@statusText,
   @capacity int=TRY_CONVERT(int,JSON_VALUE(@payload,'$.capacity')),@meetings nvarchar(max)=JSON_QUERY(@payload,'$.meetings');
  EXEC dbo.usp_Minh_LuuLop @id,@course,@lecturer,@term,@capacity,@status,@meetings,@new,@v8;
 END ELSE THROW 53302,N'Loại import không được phép.',1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Import_Batch @document nvarchar(max),@commit bit=0,@confirmed bit=0
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @@TRANCOUNT<>0 THROW 53301,N'Import cần giao dịch riêng.',1;
 EXEC dbo.usp_Campus_RequirePermission 'IMPORT_DATA';
 IF @document IS NULL OR DATALENGTH(@document)>4000000 OR ISJSON(@document)<>1 OR LEFT(LTRIM(@document),1)<>'{'
  THROW 53302,N'Tệp JSON rỗng, sai định dạng hoặc vượt giới hạn.',1;
 DECLARE @batch uniqueidentifier=TRY_CONVERT(uniqueidentifier,JSON_VALUE(@document,'$.batchId')),@source nvarchar(max)=JSON_VALUE(@document,'$.source'),
  @rows nvarchar(max)=JSON_QUERY(@document,'$.rows'),@hash binary(32)=HASHBYTES('SHA2_256',@document),@actor int=dbo.ufn_Campus_AccountId();
 IF @batch IS NULL OR @batch='00000000-0000-0000-0000-000000000000' OR NULLIF(TRIM(@source),N'') IS NULL OR LEN(@source)>500
  OR LEFT(LTRIM(ISNULL(@rows,'')),1)<>'[' OR (SELECT COUNT(*) FROM OPENJSON(@rows)) NOT BETWEEN 1 AND 100
  OR @commit IS NULL OR (@commit=1 AND ISNULL(@confirmed,0)<>1)
  THROW 53302,N'Cần UUID, nguồn xác nhận, 1–100 dòng và xác nhận nguồn trước khi ghi.',1;
 DECLARE @input TABLE(idx int PRIMARY KEY,kind nvarchar(max),id nvarchar(max),version nvarchar(max),payload nvarchar(max));
 INSERT @input SELECT CONVERT(int,j.[key]),r.kind,r.id,r.version,r.payload FROM OPENJSON(@rows) j
  CROSS APPLY OPENJSON(j.value) WITH(kind nvarchar(max),id nvarchar(max),version nvarchar(max),payload nvarchar(max) AS JSON) r;
 IF (SELECT COUNT(*) FROM @input)<>(SELECT COUNT(*) FROM OPENJSON(@rows)) OR EXISTS(SELECT 1 FROM OPENJSON(@rows) WHERE type<>5)
  OR EXISTS(SELECT 1 FROM @input WHERE kind NOT IN('PROGRAM','GROUP','COURSE','REQUIREMENT','ASSIGNMENT','RESULT','STUDENT','CLASS') OR kind IS NULL
   OR LEN(id) NOT BETWEEN 1 AND 20 OR id IS NULL OR id COLLATE Latin1_General_100_BIN2 LIKE '%[^-A-Z0-9_]%' OR id<>TRIM(id)
   OR ISJSON(payload)<>1 OR payload IS NULL OR LEFT(LTRIM(payload),1)<>'{' OR LEN(payload)>32000
   OR (version IS NOT NULL AND (LEN(version)<>CASE WHEN kind='STUDENT' THEN 64 ELSE 16 END OR version COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9A-Fa-f]%')))
  OR EXISTS(SELECT 1 FROM @input GROUP BY kind,id HAVING COUNT(*)>1)
  THROW 53302,N'Dòng import có loại, mã, payload, phiên bản sai hoặc trùng định danh.',1;
 DECLARE @out TABLE(row_index int PRIMARY KEY,kind varchar(20),id varchar(20),valid bit,message nvarchar(1500),committed bit);
 DECLARE @accepted TABLE(idx int PRIMARY KEY);
 DECLARE @i int=0,@n int=(SELECT COUNT(*) FROM @input),@k varchar(20),@id varchar(20),@v varchar(64),@p nvarchar(max),@j int;
 BEGIN TRY
  BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;EXEC dbo.usp_Campus_RequirePermission 'IMPORT_DATA';
  IF EXISTS(SELECT 1 FROM dbo.DK4_IMPORT_BATCH WHERE actor=@actor AND batch_id=@batch) BEGIN
   IF NOT EXISTS(SELECT 1 FROM dbo.DK4_IMPORT_BATCH WHERE actor=@actor AND batch_id=@batch AND payload_hash=@hash)
    THROW 53303,N'Batch-id đã dùng với nội dung khác. Không gửi lại bằng tệp đã sửa.',1;
   COMMIT;
   SELECT idx row_index,kind,id,CONVERT(bit,1) valid,N'Batch đã ghi; trả kết quả cũ.' message,CONVERT(bit,1) committed FROM @input ORDER BY idx;RETURN;
  END;
  WHILE @i<@n BEGIN
   SELECT @k=kind,@id=id,@v=version,@p=payload FROM @input WHERE idx=@i;
   BEGIN TRY
    EXEC dbo.usp_Import_ApplyRow @k,@id,@v,@p;
    INSERT @accepted VALUES(@i); INSERT @out VALUES(@i,@k,@id,1,N'Hợp lệ trong bản xem trước.',0);
   END TRY BEGIN CATCH
    DECLARE @number int=ERROR_NUMBER(),@message nvarchar(1500)=CASE WHEN ERROR_NUMBER() BETWEEN 52000 AND 53499 THEN ERROR_MESSAGE() ELSE N'Dòng không hợp lệ hoặc dữ liệu đã thay đổi. Kiểm tra nguồn và phiên bản.' END;
    IF XACT_STATE()<>0 ROLLBACK;
    INSERT @out VALUES(@i,@k,@id,0,@message,0);
    -- A legacy guarded procedure may roll back its entire transaction. Rebuild the valid
    -- prefix in a fresh transaction so later rows can still receive dependency-aware errors.
    BEGIN TRAN;EXEC dbo.usp_HocVu_Lock;EXEC dbo.usp_Minh_KhoaLapLich;EXEC dbo.usp_Campus_RequirePermission 'IMPORT_DATA';
    SET @j=0;
    WHILE @j<@i BEGIN
     IF EXISTS(SELECT 1 FROM @accepted WHERE idx=@j) BEGIN
      SELECT @k=kind,@id=id,@v=version,@p=payload FROM @input WHERE idx=@j;
      EXEC dbo.usp_Import_ApplyRow @k,@id,@v,@p;
     END;
     SET @j+=1;
    END;
   END CATCH;
   SET @i+=1;
  END;
  IF @commit=1 AND NOT EXISTS(SELECT 1 FROM @out WHERE valid=0) BEGIN
   INSERT dbo.DK4_IMPORT_BATCH(actor,batch_id,payload_hash,source,row_count) VALUES(@actor,@batch,@hash,@source,@n);
   DECLARE @entity varchar(100)=CONCAT('IMPORT:',@batch),@reason nvarchar(500)=@source,
    @after nvarchar(max)=(SELECT @n row_count,@source source FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
   EXEC dbo.usp_Campus_Emit 'IMPORT',@entity,@reason,NULL,@after,@batch;
   COMMIT;UPDATE @out SET committed=1,message=N'Đã ghi toàn bộ batch.';
  END ELSE BEGIN
   ROLLBACK;
   UPDATE @out SET message=CASE WHEN valid=1 THEN N'Hợp lệ; chưa ghi. Batch chỉ ghi khi mọi dòng hợp lệ.' ELSE message END;
  END;
  SELECT * FROM @out ORDER BY row_index;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK;THROW;END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.usp_Import_History
AS
BEGIN
 SET NOCOUNT ON;EXEC dbo.usp_Campus_RequirePermission 'IMPORT_DATA';
 SELECT TOP(100) batch_id,source,row_count,applied_utc FROM dbo.DK4_IMPORT_BATCH WHERE actor=dbo.ufn_Campus_AccountId() ORDER BY applied_utc DESC;
END;
GO
DENY EXECUTE ON dbo.usp_Import_ApplyRow TO public;
DENY SELECT,INSERT,UPDATE,DELETE ON dbo.DK4_IMPORT_BATCH TO public;
GRANT EXECUTE ON dbo.usp_Import_Batch TO app_minh;
GRANT EXECUTE ON dbo.usp_Import_History TO app_minh;
GO
