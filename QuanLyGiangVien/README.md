# Chạy ứng dụng Campus

Xem [hướng dẫn cài đặt và chạy tại README gốc](../README.md) để khởi tạo SQL Server, tạo hai tài khoản mẫu và chọn cổng Sinh viên / Phòng đào tạo.

Từ thư mục gốc repository, chạy trong PowerShell 7.4 trở lên:

```powershell
.\scripts\Setup-Team.ps1 -Server '.\SQLEXPRESS'
Get-Content .tmp/TaiKhoanMau.txt
dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj --launch-profile QuanLyGiangVien
```

Nếu database đã tồn tại, dùng `-UpgradeExisting` khi setup; xem hướng dẫn tạo/reset mật khẩu trong README gốc. Profile `DangKyHocPhanSinhVien` mở cổng Sinh viên. Hai cổng dùng chung database `DangKyTinChi_Minh`.

Kiểm chứng ở [UI_VERIFICATION.md](docs/UI_VERIFICATION.md); nguồn và giấy phép tài nguyên ở [ASSET_SOURCES.md](docs/ASSET_SOURCES.md).
