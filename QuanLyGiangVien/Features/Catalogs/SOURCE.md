# Nguồn module Phong

Nhập từ repository nhóm [Wis-olle/VS_Dang_Ky_Tin_Chi](https://github.com/Wis-olle/VS_Dang_Ky_Tin_Chi/tree/Phong_Function), nhánh `Phong_Function`, commit `da04d8b2b34ee2dcfb934edff874bcfb7d9dd672`.

Nguồn gốc: `Phong.WinForms/Forms/{HocPhanControl,HocKyControl,Ui}.cs`, `Models.cs`, `Validation.cs`, `Data/PhongRepository.cs` dưới `DangKyTinChi-Phong/DangKyTinChi-Phong/`. Giữ luồng thêm/sửa/tìm/lọc, kiểm tra năm liên tiếp và tín chỉ, bảo vệ thay đổi chưa lưu, mở/đóng đợt và token đồng thời.

Bản tích hợp dùng .NET 10, Microsoft.Data.SqlClient, record bất biến, font/palette ứng dụng; giới hạn tên 150 và tín chỉ 1–12 theo schema Huệ và module Minh. Khoa học phần chọn danh mục thực, giữ mã khoa cũ; khoảng ngày học tách riêng thời hạn đăng ký. Trạng thái học kỳ `DANG_MO`/`DONG`, không nhập trigger/seed hoặc schema riêng của Phong. Đóng đợt cũng chặn hủy đúng procedure Huệ. Các thủ tục bổ sung nằm ở `database/06_Catalogs.sql`; không ghi đè bảng hay đăng ký hiện có.

Repository nhóm chưa kèm giấy phép riêng tại nhánh đã nhập; đây là tích hợp mã nội bộ của nhóm theo yêu cầu người dùng, không coi là thư viện được cấp phép phân phối công khai.
