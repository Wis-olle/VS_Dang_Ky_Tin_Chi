# Campus — Đăng ký tín chỉ

Ứng dụng WinForms .NET 10 có hai cổng đăng nhập trên cùng database `DangKyTinChi_Minh`:

- **Sinh viên**: đăng ký học phần, kết quả đăng ký và thời khóa biểu của chính sinh viên đang đăng nhập.
- **Phòng đào tạo**: giảng viên, lớp/phòng/lịch, học phần/học kỳ, thống kê và **quản lý sinh viên**.

## Cài đặt

Cần **Windows**, **.NET SDK 10**, **PowerShell 7.4 trở lên** (`pwsh`), **SQL Server Express** và **sqlcmd**. Chạy các lệnh bên dưới trong PowerShell 7, tại thư mục gốc repository. Script tạo mật khẩu dùng API .NET mới, không chạy bằng Windows PowerShell 5.1.

Cấu hình mặc định là `.\SQLEXPRESS`, database `DangKyTinChi_Minh`, kết nối SQL bằng Windows Authentication. Tài khoản Windows chạy setup cần có quyền tạo database và áp dụng schema; dịch vụ SQL Server (SQLEXPRESS) phải đang chạy. Đóng các phiên Campus đang dùng bản Release trước khi chạy setup để tránh khóa file build.

```powershell
git clone https://github.com/Wis-olle/VS_Dang_Ky_Tin_Chi.git
cd VS_Dang_Ky_Tin_Chi
.\scripts\Setup-Team.ps1 -Server '.\SQLEXPRESS'
```

Nếu database đã tồn tại:

```powershell
.\scripts\Setup-Team.ps1 -Server '.\SQLEXPRESS' -UpgradeExisting
```

Nâng cấp chỉ áp dụng migration và seed các mã còn thiếu, không đặt lại mật khẩu mẫu. Để tạo hai tài khoản mẫu lần đầu sau khi nâng cấp:

```powershell
.\scripts\Initialize-LoginDemo.ps1 -Server '.\SQLEXPRESS'
```

Script tạo `sv2024001` (Sinh viên, liên kết `SV2024001 — Bùi Quang Minh`) và `daotao` (Phòng đào tạo). Mỗi tài khoản có mật khẩu ngẫu nhiên riêng được ghi vào **`.tmp/TaiKhoanMau.txt`**, đã được loại khỏi Git. Muốn đặt lại hai mật khẩu mẫu, chủ động chạy script với `-ResetPasswords`.

| Cổng đăng nhập | Tên đăng nhập | Mật khẩu |
| --- | --- | --- |
| Sinh viên | `sv2024001` | Xem `.tmp/TaiKhoanMau.txt` sau setup |
| Phòng đào tạo | `daotao` | Xem `.tmp/TaiKhoanMau.txt` sau setup |

```powershell
# Xem mật khẩu trên máy vừa cài
Get-Content .tmp/TaiKhoanMau.txt

# Chỉ dùng khi muốn đặt lại cả hai mật khẩu mẫu (thu hồi các phiên cũ)
.\scripts\Initialize-LoginDemo.ps1 -Server '.\SQLEXPRESS' -ResetPasswords
```

Mỗi máy cài mới sinh mật khẩu riêng. Hai tài khoản trên là tài khoản ứng dụng; không dùng chúng để kết nối SQL Server trong SSMS hoặc đổi sang SQL Authentication.

## Chạy và đăng nhập

```powershell
# Mở cổng Phòng đào tạo
dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --launch-profile QuanLyGiangVien
# Mở cổng Sinh viên
dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --launch-profile DangKyHocPhanSinhVien
```

Trong Visual Studio mở `QuanLyGiangVien/QuanLyGiangVien.slnx`, chọn một trong hai profile trên và bấm F5. Profile `ThoiKhoaBieuSinhVien` mở thời khóa biểu sau khi sinh viên đăng nhập. Cả hai cổng đều cần tài khoản đúng vai trò; có thể đổi cổng ở màn hình đăng nhập. **Đăng xuất** trở về màn hình này và xóa phiên hiện tại.

### SQL Server có tên khác

Ví dụ dùng instance mặc định `localhost`: thay `-Server` khi setup, rồi đặt chuỗi kết nối khi chạy. Dùng `--no-launch-profile` để profile mặc định không ghi đè biến môi trường.

```powershell
.\scripts\Setup-Team.ps1 -Server 'localhost'
# Nếu database đã tồn tại, thêm -UpgradeExisting như phần trên.
$env:MINH_DB_CONNECTION = 'Server=localhost;Database=DangKyTinChi_Minh;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5'
dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --no-launch-profile -- --database
# Cổng Sinh viên: thêm --student-portal vào cuối lệnh chạy.
```

`TrustServerCertificate=True` phục vụ SQL Server phát triển cục bộ. Khi dùng Visual Studio với instance khác, sửa `MINH_DB_CONNECTION` trong `QuanLyGiangVien/Properties/launchSettings.json` của profile tương ứng.

### Khi không đăng nhập được

- **Không kết nối được SQL**: kiểm tra tên instance, dịch vụ SQL Server, Windows Authentication và database `DangKyTinChi_Minh`. Chạy setup với `-UpgradeExisting` nếu chưa áp dụng migration mới.
- **Tài khoản chưa có mật khẩu**: chạy `Initialize-LoginDemo.ps1` một lần sau khi nâng cấp database cũ; đọc file mật khẩu cục bộ. Nếu script báo tài khoản đã có mật khẩu, chỉ thêm `-ResetPasswords` khi muốn đặt lại.
- **Sai cổng**: `sv2024001` đăng nhập cổng Sinh viên; `daotao` đăng nhập cổng Phòng đào tạo.
- **Đăng nhập sai nhiều lần**: tài khoản bị khóa tạm 10 phút sau 5 lần sai. Kiểm tra lại mật khẩu và chờ hết thời gian khóa.
- **Ngoài hạn đăng ký**: đăng nhập Phòng đào tạo để kiểm tra thời hạn học kỳ; tạo tài khoản không tự thay đổi lịch đăng ký.

## Quản lý sinh viên

Đăng nhập Phòng đào tạo, mở tab **Sinh viên**. Có thể tìm theo mã/tên/email; thêm hoặc cập nhật hồ sơ; chọn khoa → ngành → lớp; xem hồ sơ và ngừng/kích hoạt sinh viên. Việc ngừng sinh viên sẽ chặn các nghiệp vụ đăng ký của sinh viên đó. Những thông tin được schema cho phép để trống vẫn có thể để trống.

Đăng ký học phần vẫn tuân theo thời hạn học kỳ được Phòng đào tạo cấu hình. Việc tạo tài khoản đăng nhập không tự mở lại hạn đăng ký đã đóng.

## Tích hợp và phân quyền

Nguồn đăng nhập, PBKDF2 và hành vi quản lý sinh viên được chuyển từ bộ `QL_DANGKY_TINCHI` được cung cấp tại máy vào cấu trúc hiện có; giữ schema Huệ và giao diện Campus. Không chạy SQL tạo database khác chồng lên schema hiện tại.

Migration `08_PasswordLogin.sql` bổ sung xác thực PBKDF2-SHA256 (600.000 vòng), thử thách dùng một lần, khóa tạm sau 5 lần sai và phiên ngẫu nhiên hết hạn sau 8 giờ. SQL xác định danh tính từ token được cấp và kiểm lại vai trò/trạng thái trên mỗi nghiệp vụ. `AppSqlConnection` gắn phiên vào từng kết nối trong pool. Không lưu mật khẩu trong source hoặc đổi authentication mode của SQL Server.

Migration `09_Students.sql` kiểm quyền Phòng đào tạo tại SQL; không cấp quyền sửa bảng trực tiếp. Bộ quản lý tài khoản/khôi phục mật khẩu demo của nguồn ngoài chưa được đưa vào menu chung.

Các harness Windows identity cũ vẫn dùng database riêng `DangKyTinChi_Minh_Test_*`; không dùng các biến môi trường harness này để mở database chính. `DangKyTinChi_Minh_Test_StudentPreview` chỉ còn là fixture kiểm chứng, không phải database của các profile đăng nhập thông thường.

## Kiểm chứng

```powershell
dotnet test QuanLyGiangVien.Tests/QuanLyGiangVien.Tests.csproj -c Release
```

Unit tests và kiểm thử native đăng nhập/quản lý sinh viên đã chạy trên máy. SQL integration tests đăng nhập cần `CAMPUS_LOGIN_TEST_CONNECTION`, `DEMO_STUDENT_PASSWORD`, `DEMO_TRAINING_PASSWORD`; không ghi mật khẩu vào test/source. Chi tiết và phạm vi kiểm chứng ở `QuanLyGiangVien/docs/UI_VERIFICATION.md`.
