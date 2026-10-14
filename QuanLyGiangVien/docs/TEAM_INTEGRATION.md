# Tích hợp các phần của nhóm — 15/10/2026

App chạy chung trong `QuanLyGiangVien.csproj`, mặc định mở `Features/Shell/CampusForm`. Chỉ có một vòng `Application.Run`, một cửa sổ chính và một thanh điều hướng. Các trang được nhúng vào vùng nội dung; dialog thêm/sửa vẫn là dialog của từng phân hệ.

## Đã ghép và còn thiếu

| Thành viên | Nguồn đã kiểm tra | Đã sử dụng | Còn thiếu từ nguồn được cung cấp |
| --- | --- | --- | --- |
| **Minh** | App hiện tại | Giảng viên, lớp/phòng/lịch và thời khóa biểu sinh viên; giữ palette, font, asset và motion đã chốt | Không thiếu nguồn để chạy các chức năng này |
| **Phong** | `Phong_Function@da04d8b` | Học phần, tín chỉ/loại/khoa, học kỳ/năm học, hạn đăng ký/hủy và mở/đóng kỳ | Không thiếu file cho phân hệ được ghép; source hiện coi đợt đăng ký là thời hạn của học kỳ, chưa có nhiều đợt riêng trong một học kỳ |
| **Huệ** | `Function_4@77b70bc`, SQL `SQL_Function_4@e4b04e1` | Tra cứu, đăng ký/hủy, kết quả, danh sách sinh viên theo lớp, thống kê và CSV | Source gốc thiếu `DangKyRepository`, `NguoiDung`, `CsvExporter`, `.csproj` và entrypoint. Bản ghép đã bổ sung adapter/model/CSV và dùng project/entrypoint chung, nên các thiếu sót này không còn chặn chạy |
| **Tùng** | `Function_1@565b505` và thư mục người dùng đưa `Quanly_tinchi (2)/Quanly_tinchi` | Đã đối chiếu SQL và cấu hình solution filter; chưa có form để nhúng | **Thiếu project WinFormsApp3 thật:** `.csproj`, `.cs` (form/logic), `.Designer.cs`/`.resx` nếu form dùng Designer. Chưa ghép được đăng nhập/tài khoản, sinh viên/hồ sơ, khoa/ngành/lớp hành chính |

Thư mục Tùng chỉ có `QL_DANGKY_TINCHI.sql` và `c#.slnf`. File `.slnf` là bộ lọc solution, không chứa code form; nó trỏ tới `..\..\source\repos\WinFormsApp3\WinFormsApp3.slnx` và project `WinFormsApp3\WinFormsApp3.csproj`. Đường dẫn giải ra trong workspace là `E:\AI Agents\Dangkytinchi\source\repos\WinFormsApp3\WinFormsApp3.slnx`, hiện không tồn tại. SQL trong thư mục có SHA-256 `5DD026354D1BDBCAA971F42BC9E21BA851D2B49C278B06871B0B61C84FD9F3EA`; schema không đồng nhất bộ Huệ đã chọn, không chạy script tạo bảng đó chồng lên database hiện tại.

Để ghép tiếp Tùng, cần lấy **cả thư mục project WinFormsApp3** từ máy tác giả, gồm code logic/data đi kèm, thay vì chỉ xuất `.slnf`. Chưa có bằng chứng để kết luận nghiệp vụ của Tùng đã hoàn thành hay chưa.

## Cách chạy và quyền truy cập

Chạy profile `QuanLyGiangVien` trong Visual Studio hoặc `dotnet run --project QuanLyGiangVien/QuanLyGiangVien.csproj` từ workspace. Bấm **Kết nối hệ thống**. App dùng SQL Server local / `DangKyTinChi_Minh`; cấu hình khác qua `MINH_DB_CONNECTION`. Màn hình này dùng tài khoản Windows/SQL thực tế đã được cấp quyền, **chưa có đăng nhập tên người dùng/mật khẩu của Tùng**.

- `DAO_TAO`/`QUAN_TRI`: Giảng viên, Lớp & phòng học, Học phần & học kỳ, Thống kê. Mở danh mục đầu tiên để khởi tạo học kỳ khi database còn rỗng.
- `SINH_VIEN`: Đăng ký học phần và Thời khóa biểu. Danh tính sinh viên lấy từ ánh xạ `ORIGINAL_LOGIN()` trong SQL; không có bộ chọn giả danh sinh viên.
- Bấm **F5** hoặc chuyển trang để đọc dữ liệu mới. Rời danh mục có thay đổi chưa lưu sẽ hỏi bỏ thay đổi. Khi đang tải/lưu, chặn chuyển trang và ngắt kết nối. Ngắt kết nối giải phóng trang và xóa danh tính đang hiển thị.

SQL đang ánh xạ tài khoản Windows trên máy này là phòng đào tạo. Không chuyển tài khoản làm việc sang sinh viên để trình diễn. Hai vai trò chỉ được đổi qua lại trong database kiểm thử dùng một lần.

Theo yêu cầu ghép hai tab, chọn profile **DangKyHocPhanSinhVien** hoặc **ThoiKhoaBieuSinhVien** → F5 để tự kết nối vào cùng form **Đăng ký / Thời khóa biểu**, chọn trước tab tương ứng. `StudentPortalPage` dùng chung danh tính, đồng bộ học kỳ giữa hai tab, giữ từ khóa tra cứu và tuần/vị trí cuộn lịch; đọc SQL mới khi vào lịch để cập nhật đăng ký/hủy. Profile dùng database mẫu local riêng `DangKyTinChi_Minh_Test_StudentPreview`, được giữ lại để chạy lần sau, với ánh xạ sinh viên SV2024001. Demo lịch độc lập còn ở **ThoiKhoaBieuDemo**. Script chuẩn bị trên máy mới: `scripts/Initialize-StudentPreview.ps1`. Không đổi vai trò database làm việc; không thêm bộ chọn giả danh hoặc bỏ kiểm tra quyền SQL. Seed gốc đóng kỳ; HK1-2627 của preview đã được mở đến 22/10/2026 sau phản hồi người dùng. Switch `scripts/Initialize-StudentPreview.ps1 -OpenRegistration` mở lại hạn 7 ngày chỉ trong database preview; SQL vẫn kiểm quyền, thời hạn, sĩ số và lịch trùng.

## Dữ liệu chung và adapter

Giữ nguyên `database/00_Hue.sql`. Chi tiết schema/quyền/migration tại [database README](../database/README.md). Bản chạy chung bổ sung `06_Catalogs.sql` cho Phong và `07_RegistrationIntegration.sql` cho Huệ; không nhập schema/trigger sĩ số riêng của Phong. Dùng chung mã học phần/học kỳ/lớp và bảng `DANG_KY`; sĩ số luôn lấy từ đăng ký còn hiệu lực.

`07` sửa điều kiện tìm kiếm khi từ khóa rỗng trong `usp_TraCuuLopDangMo`: kiểm thử SQL thật đã tái hiện danh sách rỗng trước sửa. Giữ nguyên các quy tắc đăng ký/hủy gốc của Huệ. Snapshot source Huệ và provenance Phong nằm lần lượt ở [Registration](../Features/Registration/README.md) và [Catalogs](../Features/Catalogs/SOURCE.md).

Huệ đăng ký/hủy xong, chuyển sang Thời khóa biểu sẽ đọc lại đăng ký thật; phòng đào tạo mở lại lớp/thống kê cũng đọc sĩ số mới. Luồng này đã kiểm chứng qua repository SQL thật, cùng kiểm tra điều hướng native. Không báo đã tự động thao tác toàn bộ luồng ghi đăng ký bằng UI.

Đã kiểm tra thêm lỗi lịch khác nhau giữa hai profile: database/sinh viên và dữ liệu đăng ký khớp, nhưng danh mục Huệ mặc định HK2 trong khi lịch Minh mặc định HK1. Migration `07` hiện trả danh mục có ngày học, ưu tiên kỳ đang học theo giờ SQL Việt Nam; cả hai entrypoint lấy cùng lựa chọn. Vẫn giữ học kỳ đổi thủ công giữa hai tab. Audit dữ liệu live và kiểm tra trước/sau nâng cấp được lưu tại tài liệu kiểm chứng; không chuyển dữ liệu HK2 sang HK1 để làm lịch xuất hiện.

Build, tests, coverage có phạm vi và ảnh native được ghi duy nhất tại [UI_VERIFICATION.md](UI_VERIFICATION.md). Không commit/push trong lượt tích hợp.
