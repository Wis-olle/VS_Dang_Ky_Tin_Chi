# Đăng ký tín chỉ — Cổng Đào tạo và Sinh viên

Ứng dụng WinForms C# .NET 10 gồm cổng phòng đào tạo và form sinh viên có hai tab **Đăng ký học phần** / **Thời khóa biểu**. Repository có sẵn SQL tạo database, stored procedures, migration và dữ liệu mẫu theo bộ SQL của Huệ.

## 1. Chuẩn bị

- Windows, Git và **.NET SDK 10.0**.
- **SQL Server** chạy instance mặc định trên máy local, kết nối được bằng `localhost` và Windows Authentication.
- Công cụ **sqlcmd** có trong PATH. Tài khoản Windows chạy setup cần quyền tạo database và áp dụng SQL trên SQL Server local.
- Internet để restore package NuGet lần đầu.
- Nếu chạy bằng Visual Studio: dùng phiên bản hỗ trợ .NET 10, cài workload **.NET desktop development**.

Kiểm tra trong PowerShell:

```powershell
dotnet --version
sqlcmd -S localhost -E -C -Q "SELECT @@SERVERNAME"
```

## 2. Clone và khởi tạo database

Mở PowerShell và chạy:

```powershell
git clone https://github.com/Wis-olle/VS_Dang_Ky_Tin_Chi.git
cd VS_Dang_Ky_Tin_Chi
.\scripts\Setup-Team.ps1
```

Script tự restore/build solution, tạo database, chạy SQL và nạp dữ liệu mẫu. Font, avatar và hình dùng trong ứng dụng đã có sẵn trong repository.

Nếu database đã tồn tại trên máy, chạy:

```powershell
.\scripts\Setup-Team.ps1 -UpgradeExisting
```

Lệnh nâng cấp không xóa database hoặc tự đổi ánh xạ tài khoản đã có; dữ liệu mẫu chỉ thêm các mã còn thiếu. Không chạy các bộ SQL khác schema chồng lên database này.

## 3. Chạy cổng Đào tạo

Từ thư mục repository:

```powershell
dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --launch-profile QuanLyGiangVien
```

Bấm **Kết nối hệ thống** để vào các trang giảng viên, lớp/phòng/lịch, học phần/học kỳ, kết quả và thống kê đăng ký. Cổng dùng database `DangKyTinChi_Minh`, với tài khoản Windows được ánh xạ vai trò `DAO_TAO` khi setup mới.

## 4. Chạy cổng Sinh viên

Mở tab **Đăng ký** trước:

```powershell
dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --launch-profile DangKyHocPhanSinhVien
```

Hoặc mở tab **Thời khóa biểu** trước:

```powershell
dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --launch-profile ThoiKhoaBieuSinhVien
```

Hai profile mở cùng form và tự kết nối database sinh viên mẫu. Có thể chuyển qua lại hai tab trong ứng dụng. Sinh viên mẫu là **SV2024001 — Bùi Quang Minh**; học kỳ `HK1-2627` ban đầu có 4 lớp, 12 tín chỉ và 6 buổi học mỗi tuần. Học kỳ 2 ban đầu chưa có đăng ký.

Đăng ký lớp:

1. Chọn học kỳ, tìm lớp có lịch và còn chỗ.
2. Bấm vào dòng lớp, rồi bấm **Đăng ký lớp đã chọn** và xác nhận.
3. Xem hoặc hủy ở **Kết quả đăng ký**; chuyển sang **Thời khóa biểu** để xem lịch sau thay đổi.

Server kiểm tra hạn đăng ký, trùng học phần/lịch, sĩ số và quyền. Setup mở hạn đăng ký học kỳ mẫu trong 7 ngày. Nếu báo ngoài hạn, chạy rồi bấm **Làm mới**:

```powershell
.\scripts\Initialize-StudentPreview.ps1 -OpenRegistration
```

Lệnh này chỉ mở lại hạn trong database sinh viên mẫu.

## 5. Chạy bằng Visual Studio

1. Mở `QuanLyGiangVien/QuanLyGiangVien.slnx`.
2. Đặt project `QuanLyGiangVien` làm Startup Project nếu cần.
3. Ở danh sách cạnh nút chạy, chọn `QuanLyGiangVien` cho cổng Đào tạo hoặc `DangKyHocPhanSinhVien` / `ThoiKhoaBieuSinhVien` cho cổng Sinh viên.
4. Nhấn **F5**. Cần chạy setup ở bước 2 trước lần mở đầu tiên.

## Database và phạm vi bản ghép

| Database local | Mục đích |
| --- | --- |
| `DangKyTinChi_Minh` | Cổng Đào tạo |
| `DangKyTinChi_Minh_Test_StudentPreview` | Cổng Sinh viên mẫu |

**Hai database demo có dữ liệu riêng, chưa tự đồng bộ với nhau.** Cách này cho phép cùng một tài khoản Windows thử cả hai vai trò. Hai profile của cổng Sinh viên dùng chung database sinh viên nên đăng ký và thời khóa biểu đọc cùng dữ liệu.

Để triển khai hai cổng dùng chung dữ liệu thật, cần SQL/Windows identity riêng cho từng người, ánh xạ bảng `TAI_KHOAN` đúng vai trò/mã sinh viên và cấu hình `MINH_DB_CONNECTION` của các profile trỏ cùng một database. Luồng đăng nhập tên người dùng/mật khẩu của Tùng còn thiếu source, chưa nằm trong bản ghép.

Bộ SQL nằm trong [`QuanLyGiangVien/database`](QuanLyGiangVien/database), gồm SQL gốc Huệ, các migration và seed. Không cần gửi kèm file MDF/BAK. Xem thêm [hướng dẫn database và chạy ứng dụng](QuanLyGiangVien/docs/CLONE_AND_RUN.md).
