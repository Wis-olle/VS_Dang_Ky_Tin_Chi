# Bàn giao 15/10/2026 — đăng nhập hợp nhất

Đã ghép đăng nhập Sinh viên / Phòng đào tạo và quản lý sinh viên từ bộ source QL_DANGKY_TINCHI do người dùng cung cấp. Cả hai profile dùng DangKyTinChi_Minh trên .\SQLEXPRESS; không còn dùng StudentPreview cho đăng nhập thông thường.

Tài khoản mẫu sv2024001 và daotao đã tạo tại máy. Mật khẩu nằm ở .tmp/TaiKhoanMau.txt, không đưa vào Git. Sinh viên liên kết SV2024001. Không tự đổi thời hạn học kỳ khi tạo tài khoản.

Nguồn auth: Data/Sql/PasswordLogin.cs, AppSqlConnection.cs, database/08_PasswordLogin.sql. Source sinh viên: Features/Students, Data/Sql/StudentRepository.cs, database/09_Students.sql. Khi thêm repository mới cần mở SQL qua AppSqlConnection để giữ đúng danh tính ứng dụng. ORIGINAL_LOGIN fallback chỉ dùng cho quyền SQL có sẵn và fixture; các luồng chạy ứng dụng thông thường yêu cầu đăng nhập.

Giữ các sửa bố cục Registration từ lượt trước. Đã sửa thêm việc trang đăng ký bị trống ở lần đăng nhập đầu: cần hiển thị workspace native trước khi truy vấn tab đang chọn.

Cách chạy: README.md và QuanLyGiangVien/README.md. Kiểm chứng: QuanLyGiangVien/docs/UI_VERIFICATION.md. Người dùng đã yêu cầu commit và push lên main; tài khoản/mật khẩu cục bộ trong .tmp không được đưa vào commit.
