# Campus — Đăng ký tín chỉ

Ứng dụng desktop **C# WinForms · .NET 10**, gồm cổng **Sinh viên** và **Phòng đào tạo**. Kho này chứa mã nguồn để build và chạy trên Windows.

- **Sinh viên:** tìm lớp, giỏ đăng ký, đăng ký/hủy/đổi lớp, gợi ý lịch, kế hoạch học tập và thời khóa biểu cá nhân.
- **Phòng đào tạo:** quản lý giảng viên, sinh viên, học phần, lớp/phòng, học vụ và đăng ký; xem thời khóa biểu theo giảng viên, sinh viên hoặc lớp.

## 1. Tải mã nguồn

**Dùng Git:**

```powershell
git clone https://github.com/Wis-olle/VS_Dang_Ky_Tin_Chi.git
cd VS_Dang_Ky_Tin_Chi
```

**Không dùng Git:** [Tải ZIP nhánh main](https://github.com/Wis-olle/VS_Dang_Ky_Tin_Chi/archive/refs/heads/main.zip), giải nén rồi mở Terminal tại thư mục có `README.md`, `scripts` và `QuanLyGiangVien`.

Các lệnh bên dưới chạy từ **thư mục gốc vừa tải**, trong **PowerShell 7**.

## 2. Cài công cụ cần thiết

| Công cụ | Khi nào cần | Tải/cài |
| --- | --- | --- |
| Windows | Mọi chế độ; ứng dụng dùng WinForms | Chạy trên máy Windows |
| .NET SDK 10 | Build và chạy ứng dụng | [Tải .NET 10 SDK cho Windows](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) — chọn **SDK**, không chỉ Runtime |
| PowerShell 7.4 trở lên | Chạy script cài đặt, tạo tài khoản và bản trình diễn | [Hướng dẫn cài PowerShell 7](https://learn.microsoft.com/en-us/powershell/scripting/install/install-powershell-on-windows) |
| SQL Server cục bộ | Hai cổng đầy đủ và bản trình diễn có database | [SQL Server Express hoặc Developer](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) |
| sqlcmd | Các script thao tác database | [Cài sqlcmd](https://learn.microsoft.com/en-us/sql/tools/sqlcmd/sqlcmd-utility) — các script sử dụng cú pháp sqlcmd ODBC |
| Git | Chỉ cần nếu tải bằng `git clone` | Có thể tải ZIP thay thế |

Sau khi cài, mở lại Terminal. Kiểm tra:

```powershell
dotnet --list-sdks
$PSVersionTable.PSVersion
Get-Command sqlcmd
```

Cần thấy SDK `10.0.x` và PowerShell từ `7.4`. Nếu chỉ xem giao diện không SQL ở mục 3, có thể bỏ qua SQL Server và `sqlcmd`. Lần build đầu cần Internet để tải package NuGet.

> Windows PowerShell 5.1 không thay thế PowerShell 7. Mở ứng dụng **PowerShell 7** hoặc gõ `pwsh` sau khi cài.

## 3. Chạy thử giao diện ngay, không cần SQL

**Giảng viên, lớp/phòng và lịch tuần:**

```powershell
dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --launch-profile GiangVienDemo
```

**Thời khóa biểu sinh viên:**

```powershell
dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --launch-profile ThoiKhoaBieuDemo
```

Đóng cửa sổ ứng dụng trước khi chạy lệnh tiếp theo. Đây là các form độc lập dùng dữ liệu mẫu trong bộ nhớ; thay đổi mất khi đóng app. Muốn đăng nhập hai cổng và sử dụng nghiệp vụ lưu SQL, làm tiếp mục 4.

## 4. Cài bản đầy đủ với SQL Server

### Chọn đúng instance SQL

Cài SQL Server với **Windows Authentication** và bảo đảm dịch vụ SQL đang chạy. Tài khoản Windows chạy setup cần quyền tạo database và áp dụng schema.

Trong PowerShell 7, chọn tên instance đúng với máy bạn:

```powershell
# Instance mặc định:
$server = '.'

# Nếu SQL Server Express được cài với tên SQLEXPRESS,
# dùng dòng dưới thay cho dòng trên:
# $server = '.\SQLEXPRESS'

sqlcmd -S $server -E -C -Q 'SELECT @@SERVERNAME AS MayChu;'
```

Nếu lệnh kiểm tra chưa kết nối được, kiểm tra lại instance/dịch vụ trước khi tiếp tục. Dùng đúng giá trị `$server` cho các bước dưới.

### Tạo database và tài khoản lần đầu

```powershell
.\scripts\Setup-Team.ps1 -Server $server
```

Script restore package, build solution, tạo database **`DangKyTinChi_Minh`**, áp dụng schema 4, nạp dữ liệu mẫu và tạo hai tài khoản ứng dụng:

| Cổng | Tên đăng nhập | Mật khẩu |
| --- | --- | --- |
| Sinh viên | `sv2024001` | Mật khẩu riêng được tạo trên máy bạn |
| Phòng đào tạo | `daotao` | Mật khẩu riêng được tạo trên máy bạn |

Xem mật khẩu sau khi setup thành công:

```powershell
Get-Content .\.tmp\TaiKhoanMau.txt
```

Tệp tài khoản nằm trong `.tmp`, không có trên GitHub. Không có mật khẩu dùng chung. Đây là tài khoản **đăng nhập Campus**, không phải tài khoản đăng nhập SQL Server/SSMS.

### Chạy và đăng nhập

Trong cùng cửa sổ PowerShell đã khai báo `$server`:

```powershell
$env:MINH_DB_CONNECTION = "Server=$server;Database=DangKyTinChi_Minh;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5"

# Mở cổng Phòng đào tạo:
dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --no-launch-profile -- --database
```

Đăng nhập bằng `daotao` và mật khẩu trong tệp trên. Để mở cổng Sinh viên, đóng cửa sổ app rồi chạy:

```powershell
dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --no-launch-profile -- --database --student-portal
```

Đăng nhập bằng `sv2024001`. Biến `MINH_DB_CONNECTION` chỉ có hiệu lực trong Terminal hiện tại; mở Terminal mới cần khai báo lại `$server` và chuỗi kết nối.

`--no-launch-profile` giúp dùng đúng instance bạn vừa cấu hình. Các profile có sẵn và `Mo-Campus.cmd` mặc định dùng server `.`; setup với `-Server '.\SQLEXPRESS'` không tự sửa các profile này.

`TrustServerCertificate=True` trong ví dụ phục vụ SQL Server phát triển cục bộ.

### Nếu database đã tồn tại

Dùng lệnh nâng cấp thay cho lệnh tạo mới:

```powershell
.\scripts\Setup-Team.ps1 -Server $server -UpgradeExisting
```

Script tạo backup `COPY_ONLY` có checksum và chạy `RESTORE VERIFYONLY` trước migration; nếu bước này lỗi thì dừng. SQL Server cần quyền ghi thư mục backup. Có thể thêm `-BackupDirectory 'D:\SQLBackups'` với thư mục hợp lệ.

Nâng cấp **không nạp lại dữ liệu mẫu và không đặt lại mật khẩu**. Nếu database có sinh viên mẫu `SV2024001` nhưng chưa có hai tài khoản mẫu, tạo riêng bằng:

```powershell
.\scripts\Initialize-LoginDemo.ps1 -Server $server
```

Nếu đã có tài khoản/mật khẩu thì dùng thông tin hiện có. Chỉ thêm `-ResetPasswords` vào script này khi chủ động muốn đặt lại **cả hai** mật khẩu mẫu. Nếu báo lỗi sau khi gửi SQL, giữ tệp `.pending` theo thông báo để đối soát trước khi chạy lại.

## 5. Mở bằng một lần bấm hoặc Visual Studio

**Với instance mặc định `.` đã setup:**

- Mở `QuanLyGiangVien/Mo-Campus.cmd` để build/chạy cổng đăng nhập.
- Hoặc mở `QuanLyGiangVien/QuanLyGiangVien.slnx` trong Visual Studio hỗ trợ .NET 10, cài workload **.NET desktop development**, chọn project `QuanLyGiangVien` và bấm **F5**.
- Profile **QuanLyGiangVien** mở cổng PĐT; **DangKyHocPhanSinhVien** mở cổng SV; **ThoiKhoaBieuSinhVien** mở thời khóa biểu sau đăng nhập SV.

Nếu dùng `SQLEXPRESS` hoặc instance khác, chạy lệnh ở mục 4 hoặc sửa `MINH_DB_CONNECTION` trong `QuanLyGiangVien/Properties/launchSettings.json` cho profile đã chọn.

**Tùy chọn: bật hai nút đăng nhập mẫu** sau setup:

```powershell
.\scripts\Enable-QuickLogin.ps1 -CredentialFile .\.tmp\TaiKhoanMau.txt
```

Tính năng này hỗ trợ instance mặc định `.` / `localhost` / `(local)`, chưa hỗ trợ named instance như `.\SQLEXPRESS`. Cấu hình được mã hóa cho người dùng Windows hiện tại, không chia sẻ sang máy khác. Khi chưa cấu hình, hai nút mẫu có thể bị vô hiệu hóa; vẫn có thể nhập tài khoản/mật khẩu thủ công.

## 6. Bản trình diễn đầy đủ với dữ liệu mô phỏng

Nếu muốn demo đăng ký, kế hoạch học và các màn hình vận hành với dữ liệu chuẩn bị sẵn, dùng bộ showcase riêng. Cần các công cụ SQL ở mục 2 và **instance cục bộ mặc định `.`**.

Không cần tạo database thông thường ở mục 4 trước khi tạo bộ này:

```powershell
pwsh -NoProfile -File .\scripts\Initialize-ShowcaseDemo.ps1
.\QuanLyGiangVien\Mo-BanTrinhDien.cmd
```

Script tạo database `DangKyTinChi_Minh_Test_Showcase_<UUID>`, tài khoản riêng, dữ liệu mô phỏng và executable riêng. Không thay dữ liệu/tài khoản của database thông thường. Chọn **Đăng nhập mẫu Sinh viên** hoặc **Đăng nhập mẫu PĐT** trong bản này để bắt đầu.

- Trên máy mới, phải chạy script khởi tạo trước; launcher không tự tạo database.
- Dữ liệu mô phỏng và các thay đổi của bạn được giữ qua lần mở sau.
- Mỗi lần khởi tạo tạo một bộ mới; không cần chạy lại chỉ để mở app.
- Launcher dùng executable được build lúc khởi tạo. Sau khi cập nhật source, tạo bộ mới nếu muốn demo mã mới.
- Bộ showcase chưa hỗ trợ `.\SQLEXPRESS`.

## 7. Xem thời khóa biểu

Trong cổng PĐT: **Lớp & phòng học → Lịch tuần → chọn loại → chọn mã/tên đối tượng**.

Có thể xem theo giảng viên, sinh viên, lớp học phần, lớp sinh viên hoặc phòng. Chưa chọn thì lịch chưa hiển thị. Lớp sinh viên là hợp lịch đăng ký của thành viên, không có nghĩa mọi người học cùng các môn. Ô có nhiều buổi giao giờ mở danh sách đầy đủ khi bấm vào.

Sinh viên mở **Thời khóa biểu** để xem lịch của chính mình. Lịch dựa trên các buổi học thực tế của tuần đang chọn.

## 8. Lỗi thường gặp

| Hiện tượng | Cách xử lý |
| --- | --- |
| Không nhận `dotnet` hoặc thiếu SDK | Cài .NET **SDK 10** cho Windows, mở lại Terminal; kiểm tra `dotnet --list-sdks`. |
| Script lỗi trên PowerShell 5.1 | Mở PowerShell 7 bằng `pwsh`; yêu cầu từ 7.4. |
| Script tải từ ZIP bị Windows chặn | Sau khi kiểm tra nguồn tải, nhấp phải file ZIP → Properties → Unblock (nếu có) → Apply, rồi giải nén lại. |
| Không nhận `sqlcmd` | Cài công cụ sqlcmd ODBC, thêm vào PATH rồi mở Terminal mới. SSMS không thay thế công cụ này. |
| Không kết nối SQL | Kiểm tra dịch vụ, Windows Authentication và tên instance; thử lại lệnh `sqlcmd` ở mục 4. |
| Setup đúng nhưng app kết nối sai server | Dùng `--no-launch-profile` và biến kết nối như mục 4; profile mặc định trỏ `.`. |
| Database đã tồn tại hoặc schema chưa tương thích | Dùng `Setup-Team.ps1 -UpgradeExisting` trên đúng server; không xóa database để cài lại. |
| Không tìm thấy tệp tài khoản | Tệp chỉ được tạo trên máy đã setup thành công; kiểm tra output của script và đường dẫn `-CredentialFile` nếu đã dùng. |
| Hai nút đăng nhập mẫu chưa sáng | Nhập tài khoản thủ công hoặc cấu hình quick login theo mục 5. |
| Sai cổng hoặc sai mật khẩu | SV dùng `sv2024001`, PĐT dùng `daotao`; đọc mật khẩu của chính máy vừa cài. |
| Bị khóa sau nhiều lần sai | Tài khoản khóa tạm 10 phút sau 5 lần sai; kiểm tra mật khẩu rồi chờ hết khóa. |
| Không đăng ký được do hết hạn | Setup không tự mở hạn đăng ký; kiểm tra học kỳ ở PĐT hoặc dùng showcase để demo. |
| Thao tác quản trị báo thiếu quyền | `daotao` không mặc nhiên có mọi quyền quản trị; cần được tài khoản quản trị cấp quyền phù hợp. |
| `Mo-BanTrinhDien.cmd` báo chưa có bản demo | Chạy `Initialize-ShowcaseDemo.ps1` trước như mục 6. |
| Build báo executable đang được sử dụng | Đóng cửa sổ Campus đang chạy rồi thử build lại. |

## 9. Chạy kiểm thử

```powershell
dotnet test QuanLyGiangVien.Tests/QuanLyGiangVien.Tests.csproj -c Release
```

SQL integration tests tự bỏ qua khi chưa cấu hình database fixture. Muốn nghiệm thu đầy đủ trên các database kiểm thử riêng:

```powershell
pwsh -NoProfile -File .\scripts\Verify-Campus.ps1 -Server '.'
```

Runner đầy đủ cần quyền tạo/xóa database fixture và ghi backup trên SQL Server cục bộ; output nằm trong `.tmp/verify-campus`. Không dùng database chính làm fixture.
