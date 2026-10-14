# Clone và chạy hai cổng

Bản ghép gồm cổng phòng đào tạo và cổng sinh viên trên WinForms .NET 10. Database được cung cấp bằng SQL tạo schema, stored procedures, migration và dữ liệu mẫu trong `QuanLyGiangVien/database/`; không cần copy file MDF theo đường dẫn máy tác giả.

## Chuẩn bị máy

- Windows và .NET SDK 10.0. Nếu chạy trong Visual Studio, cần workload phát triển ứng dụng desktop .NET và phiên bản hỗ trợ .NET 10.
- SQL Server local chạy ở instance mặc định, kết nối được bằng `localhost` và Windows Authentication.
- Công cụ `sqlcmd`; tài khoản chạy setup có quyền tạo database/user và áp dụng thủ tục SQL trên máy local.
- Kết nối mạng để restore package NuGet lần đầu. Font, avatar, hình và icon runtime đã đóng gói cục bộ.

## Khởi tạo sau khi clone

Chạy PowerShell ở thư mục repository:

```powershell
git clone https://github.com/Wis-olle/VS_Dang_Ky_Tin_Chi.git
cd VS_Dang_Ky_Tin_Chi
.\scripts\Setup-Team.ps1
```

Script restore/build solution trước khi tạo database, rồi dựng SQL theo bộ Huệ và bổ sung dữ liệu mẫu. Nếu database đã tồn tại trên máy phát triển, chạy `Setup-Team.ps1 -UpgradeExisting`; không xóa dữ liệu cũ hoặc tự đổi ánh xạ tài khoản đã có. Seed chỉ thêm mã còn thiếu. Không chạy riêng các bộ SQL khác schema của các thành viên chồng lên database này.

## Cổng phòng đào tạo

```powershell
dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --launch-profile QuanLyGiangVien
```

Bấm **Kết nối hệ thống**. Tài khoản Windows thực tế được ánh xạ DAO_TAO trong `DangKyTinChi_Minh`. Các trang gồm giảng viên; lớp/phòng/lịch; học phần/học kỳ; kết quả và thống kê đăng ký. Dữ liệu ghi SQL; F5/chuyển trang đọc lại dữ liệu mới.

## Cổng sinh viên

```powershell
dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --launch-profile ThoiKhoaBieuSinhVien
```

App tự kết nối và hiện hai tab **Đăng ký** / **Thời khóa biểu**. Profile `DangKyHocPhanSinhVien` mở cùng form, chọn trước tab đăng ký. Sinh viên mẫu là SV2024001/Bùi Quang Minh; HK1 có 4 lớp/12 tín chỉ/6 buổi mỗi tuần, HK2 ban đầu chưa đăng ký. Hai profile chọn cùng học kỳ đang học và giữ lựa chọn tay khi chuyển tab.

Để đăng ký: chọn lớp có lịch và còn chỗ → **Đăng ký lớp đã chọn** → xác nhận. Xem/hủy trong **Kết quả đăng ký**; sang tab lịch để thấy thay đổi. Luật server vẫn kiểm thời hạn, trùng học phần/lịch, sĩ số và quyền. Lớp chưa có lịch không đăng ký được.

Setup mở hạn HK1 mẫu trong 7 ngày theo giờ SQL Việt Nam. Khi hết hạn, chạy lại `.\scripts\Initialize-StudentPreview.ps1 -OpenRegistration`, rồi bấm **Làm mới**. Mở hạn này chỉ tác động database sinh viên mẫu.

## Database và giới hạn tài khoản

Máy clone có hai catalog local: `DangKyTinChi_Minh` dành cho phòng đào tạo và `DangKyTinChi_Minh_Test_StudentPreview` dành cho sinh viên mẫu. Catalog sinh viên tách riêng để cùng một tài khoản Windows thử được hai vai trò, giữ quyền quản lý trên catalog chính. Hai catalog dùng cùng schema/thủ tục, nhưng dữ liệu thay đổi ở một catalog không tự sao sang catalog kia.

Khi triển khai dùng chung dữ liệu thật, cấp SQL/Windows identity riêng cho từng người, ánh xạ `TAI_KHOAN` đúng vai trò/mã sinh viên và cấu hình `MINH_DB_CONNECTION` của các profile cùng trỏ một catalog. Không dùng tham số mã sinh viên để giả danh, không đưa mật khẩu vào Git. Luồng đăng nhập tên người dùng/mật khẩu của Tùng còn thiếu source, chưa nằm trong bản ghép.

SQL gốc Huệ được giữ nguyên byte/checksum. Script khởi tạo đọc các migration `01`–`07` và seed; đầy đủ bảng/dữ liệu mẫu đã có trong Git. Các file báo cáo native và tests tại `QuanLyGiangVien/docs/verification/` ghi rõ phạm vi kiểm chứng.
