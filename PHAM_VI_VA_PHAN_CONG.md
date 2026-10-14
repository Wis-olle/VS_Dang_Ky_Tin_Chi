# Phạm vi và phân công nhóm

Cập nhật ngày **15/10/2026**. Đây là phạm vi người dùng đã nêu; phần đã triển khai được ghi riêng bên dưới.

## Phân công 4 người

| Thành viên | Phần phụ trách |
| --- | --- |
| **Tùng** | Đăng nhập và tài khoản; thêm/sửa/xóa/tìm kiếm sinh viên; khoa, ngành, lớp hành chính; hồ sơ sinh viên |
| **Phong** | Học phần; bắt buộc/tự chọn, số tín chỉ; năm học, học kỳ, đợt đăng ký và các thời hạn |
| **Minh** | Giảng viên; phòng học; mở lớp học phần theo học kỳ; sĩ số; lịch học; kiểm tra trùng phòng/lịch giảng viên; thời khóa biểu |
| **Huệ** | Tra cứu lớp đang mở; đăng ký/hủy đăng ký; kiểm tra thời hạn; kết quả đăng ký; danh sách sinh viên theo lớp và thống kê liên quan |

**Phạm vi hiện tại:** theo yêu cầu tiếp theo, đã ghép phần có nguồn của **Minh, Phong và Huệ** trong app `QuanLyGiangVien`, một cửa sổ/điều hướng chung, dùng database `DangKyTinChi_Minh` theo **SQL Huệ (`SQL_Function_4`)**. Phần Minh giữ giảng viên, lớp/phòng/lịch và thời khóa biểu; Phong cung cấp danh mục học phần/học kỳ; Huệ cung cấp tra cứu/đăng ký/hủy/kết quả/thống kê. Tùng chưa có project/code form để ghép: cả GitHub và thư mục `Quanly_tinchi (2)` chỉ có SQL + `.slnf`. Không tự triển khai thay nghiệp vụ thiếu của Tùng. Bảng thiếu chi tiết và cách lấy source tại [TEAM_INTEGRATION.md](QuanLyGiangVien/docs/TEAM_INTEGRATION.md); schema tại [database/README.md](QuanLyGiangVien/database/README.md); cách chạy tại [README app](QuanLyGiangVien/README.md).

Yêu cầu mới nhất: form sinh viên ghép **hai tab Đăng ký (Huệ) / Thời khóa biểu (Minh)**, dùng cùng sinh viên/học kỳ; mở tab lịch nạp lại SQL sau đăng ký/hủy. Profile `ThoiKhoaBieuSinhVien` và `DangKyHocPhanSinhVien` cùng mở form này; demo ngoại tuyến giữ ở `ThoiKhoaBieuDemo`. Phân công nghiệp vụ các thành viên không đổi.

Ngày14/10 người dùng xác nhận thời khóa biểu mới là trang **sinh viên** và chọn mẫu Student Dashboard8097669 của Alireza Attari. Đã dựng form riêng theo mẫu, đọc đăng ký minh họa theo sinh viên/học kỳ. Tab lịch tuần quản lý lớp/phòng vẫn dành cho phòng đào tạo. Phần đăng ký của Huệ sẽ cung cấp dữ liệu thật; form này không triển khai thao tác đăng ký/hủy thay Huệ.

Ngày 12/10 người dùng làm rõ chỉ dùng bảng màu giảng viên, còn bố cục lớp/phòng có thiết kế riêng theo hai shot Dribbble đã gửi: header gọn, dải số liệu, bảng lớn/tab gạch chân và inspector/agenda bên cạnh. Bỏ hero/avatar của bản đồng bộ trước; thanh dọc vẫn đã bỏ. Giữ ba tab và nút Giảng viên để quay lại; giao diện giảng viên không sửa.

Sau phản hồi “nhìn hơi thô”, chỉ tinh chỉnh chữ/viền/khoảng cách, tag/bo góc và calendar của module này; không mở rộng chức năng hoặc nguồn thiết kế.

Ngày 13/10, người dùng mở rộng nguồn tìm kiếm ra toàn mạng để thêm ảnh/icon/asset/animation. Đã bổ sung tài nguyên cục bộ và motion hữu hạn vào form lớp/phòng; vẫn không mở rộng nghiệp vụ hoặc sửa giao diện giảng viên.

Sau phản hồi còn đơn điệu, tiếp tục thêm hình theo nhóm môn/loại phòng vào bảng, khung chi tiết và thẻ tuần. Bộ cuối dùng unDraw với nguồn/giấy phép cục bộ; mapping lấy từ khoa và loại phòng đã có, không thêm danh mục hoặc nghiệp vụ.

Yêu cầu tiếp theo ngày13/10 chỉ cải thiện dải thống kê và Nhịp học tuần: tile có rail/ring theo dữ liệu thật, chart rõ hơn và tương tác native. Tham khảo thêm Dribbble/21st, giữ source MIT cục bộ; vẫn giữ phạm vi, palette và UI giảng viên.

Ngày14/10 người dùng chọn Service Card21st cho bốn KPI. Đã chuyển sang thẻ lớn với ảnh3D CC0 và CTA tới các tab hiện có; không thêm đăng ký sinh viên hoặc nghiệp vụ khác. UI giảng viên giữ nguyên.

Ngày **11/10/2026**, theo yêu cầu mới đã tạo thêm [bộ khung nhóm](VS_Dang_Ky_Tin_Chi/README.md) trong clone riêng `VS_Dang_Ky_Tin_Chi/`. Bốn vùng chức năng chỉ có màn hình chờ tích hợp; không được tính là nghiệp vụ hoàn thành. App giảng viên vẫn giữ riêng. Phạm vi và các điểm cần thống nhất của bộ khung tại [INTEGRATION.md](VS_Dang_Ky_Tin_Chi/docs/INTEGRATION.md).

## Đối tượng và thuật ngữ

- **Sinh viên:** mã, họ tên, ngày sinh, giới tính, email, ngành học, lớp hành chính; các lớp học phần đang học được lấy từ đăng ký.
- **Phòng đào tạo:** vai trò quản lý dữ liệu, tổ chức đăng ký, mở lớp và thống kê.
- **Giảng viên:** mã, họ tên, ngày sinh, giới tính, email, số điện thoại. UI hiện bổ sung khoa, học vị, trạng thái giảng dạy, avatar và số lớp phân công mẫu.
- **Lớp hành chính:** lớp ổn định của sinh viên theo khóa/ngành, phục vụ quản lý hồ sơ.
- **Học phần:** môn trong chương trình, có mã, tên, số tín chỉ và loại bắt buộc/tự chọn.
- **Lớp học phần:** một lần tổ chức dạy học phần trong một học kỳ, có mã lớp, giảng viên, phòng, lịch và sĩ số. Sinh viên đăng ký lớp học phần.

## Chức năng dự kiến của toàn chương trình

| Nhóm chức năng | Nội dung yêu cầu |
| --- | --- |
| Tài khoản | Trang đăng nhập riêng; đăng nhập/đăng xuất, đổi mật khẩu, quên mật khẩu; quản trị khóa hoặc xóa tài khoản |
| Sinh viên | Thêm/sửa/xóa, tìm kiếm và xem hồ sơ |
| Giảng viên | Thêm/sửa/xóa, tìm kiếm và xem hồ sơ |
| Học phần | Thêm/sửa/tìm kiếm; mã, tên, tín chỉ; bắt buộc/tự chọn; các giới hạn cần thống nhất khi tích hợp |
| Học kỳ/đợt đăng ký | Tạo năm học/học kỳ; thời gian bắt đầu/kết thúc; mở/đóng; thời hạn hủy đăng ký |
| Lớp học phần | Chọn học phần, giảng viên; thiết lập sĩ số; xếp lịch/phòng; theo dõi số đăng ký và trạng thái còn chỗ/đầy |
| Tra cứu/thống kê | Danh sách sinh viên theo lớp; lớp một sinh viên đã đăng ký; số sinh viên, tổng tín chỉ, tỷ lệ lấp đầy; lớp ít sinh viên để xem xét hủy |
| Sinh viên đăng ký | Xem/tìm lớp đang mở; chọn đăng ký; xem kết quả và tổng tín chỉ; hủy trong thời hạn |
| Sinh viên xem thông tin | Thời khóa biểu do Minh phụ trách, đã có UI với đăng ký mẫu. Lịch thi, điểm, GPA và các môn đã học chưa triển khai, chưa chốt người phụ trách |

## Phần thực tế đã có

- App `QuanLyGiangVien`: dashboard, thẻ/bảng, tìm kiếm, lọc khoa/trạng thái, hồ sơ giảng viên, cửa sổ thêm/sửa, đổi avatar, xóa có xác nhận và bảo vệ hồ sơ có lớp phân công.
- Trang gộp lớp/phòng: thêm/sửa/tìm/lọc, sĩ số tối đa, tối đa 7 buổi học, xem lịch theo tuần/học kỳ/phòng, kiểm tra trùng ngày–tiết–phòng–giảng viên và bảo vệ liên kết khi xóa/tạm nghỉ.
- Seed SQL: **12 giảng viên, 4 khoa, 8 phòng, 8 học phần, 10 lớp, 2 học kỳ**, cùng 5 đăng ký của 2 sinh viên. Hồ sơ giảng viên SQL lấy phân công thật; số đã đăng ký lấy từ `DANG_KY`, không dùng sĩ số giả của demo cũ. Các danh mục đang seed để kiểm tra; khi ghép Phong/Tùng cần dùng chung mã/schema Huệ đã chọn.
- Đã có asset, animation native, kiểm tra UI và kiểm thử logic.
- Thời khóa biểu sinh viên: chọn học kỳ/tuần, xem phòng/giảng viên/tiết và chi tiết buổi học, số học phần/tín chỉ từ đăng ký mẫu. Form riêng mở bằng `--student-timetable`.
- **Đã có SQL phần Minh:** thêm/sửa/xóa giảng viên, phòng, lớp và lịch qua thủ tục; kiểm tra xung đột/đăng ký/quyền ở server; thời khóa biểu đọc đăng ký thật theo sinh viên được ánh xạ. Giữ 13 bảng/thủ tục đăng ký Huệ, migration chỉ bổ sung dữ liệu cần cho UI Minh.
- **Đã ghép nhóm:** shell điều hướng theo vai trò SQL; UI học phần/học kỳ Phong và đăng ký/kết quả/thống kê Huệ dùng chung mã và dữ liệu với Minh. Adapter còn thiếu từ nguồn Huệ đã được bổ sung trong app chung. Phạm vi/nguồn và bằng chứng tại tài liệu tích hợp.
- **Chưa ghép:** đăng nhập/tài khoản, quản lý sinh viên/hồ sơ và khoa/ngành/lớp hành chính của Tùng vì chưa có project C#. Cơ chế SQL xác thực bằng `ORIGINAL_LOGIN()` của Huệ; bootstrap local là ánh xạ phòng đào tạo phục vụ máy phát triển. Đợt đăng ký hiện là thời hạn của học kỳ theo source Phong; không tự mở rộng nhiều đợt trong một kỳ.

## Điểm cần thống nhất khi tích hợp sau

Các mục sau là việc cần phối hợp trong tương lai, chưa phải quyết định schema hay API đã chốt:

- Dùng chung mã khoa/ngành, giảng viên, sinh viên, học phần, học kỳ và lớp học phần.
- Phân biệt bảng lớp hành chính với lớp học phần.
- Xác định nguồn dữ liệu giảng viên và phòng để Minh mở lớp, nguồn lớp đang mở để Huệ đăng ký.
- Thống nhất giới hạn tín chỉ, sĩ số, thời hạn hủy và quy tắc trùng lịch khi triển khai logic thực tế.
- Thống nhất cách lưu dữ liệu và giao diện tích hợp trước khi thay dữ liệu mẫu hiện tại.

