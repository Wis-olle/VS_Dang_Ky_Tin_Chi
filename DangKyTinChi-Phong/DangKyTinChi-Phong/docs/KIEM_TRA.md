# Kết quả kiểm tra bộ bàn giao

Ngày kiểm tra: 14/10/2026. Các kết quả dưới đây áp dụng cho source trong bộ ZIP.

| Kiểm tra | Kết quả |
|---|---|
| Trích tên bảng/cột từ file bảng quy ước SQL.docx và so với CREATE TABLE | Khớp 13 bảng, 95 cột; khớp tên và thứ tự cột từng bảng |
| Đối chiếu lời gọi repository với tham số procedure | Khớp cả 5 procedure |
| File Compile được khai báo trong csproj | Đủ; không tham chiếu file bị thiếu |
| Biên dịch toàn bộ ứng dụng C# | Thành công bằng Mono C# compiler 6.8, chế độ cảnh báo thành lỗi; không có lỗi/cảnh báo |
| Kiểm tra validation | 28/28 đạt, gồm mã trống/quá dài/ký tự sai, tên trống, loại sai, tín chỉ ngoài khoảng, năm học sai, thời gian đảo, hạn hủy sai |
| Parser SQL chính thức Microsoft ScriptDom TSql160Parser | Không có lỗi cú pháp ở 01, 02, 03 và SmokeTests.sql |
| Kiểm tra hành vi hai UserControl với dữ liệu giả | 13/13 đạt: sạch/bẩn/reset, mã chỉ đọc khi sửa, chọn dòng, dữ liệu dòng thứ hai, trạng thái nút mở/đóng |
| Quan sát bố cục hai UserControl qua Mono/X11 | Đã xem hình render thực tế; vùng danh sách/editor tách rõ, không chồng chữ hoặc nút; danh sách rộng có thanh cuộn ngang |

## Chưa thể khẳng định đã kiểm tra

- Chưa chạy tạo database/procedure/trigger hoặc SmokeTests.sql trên SQL Server thật vì môi trường kiểm tra không có SQL Server.
- Chưa chạy bằng Visual Studio trên Windows hoặc kiểm tra mọi mức DPI. Mono là kiểm tra biên dịch và bố cục bổ sung, không thay thế Windows + .NET Framework 4.8.
- Chưa ghép vào source của nhóm vì chưa được cung cấp project hiện có, connection string hay schema đang sử dụng.
- Chưa kiểm tra các chức năng Tùng/Minh/Huệ hoặc tranh chấp đăng ký đồng thời của module Huệ.

## Kiểm tra khi nhận source

1. Trên database thử nghiệm, chạy lần lượt 01 → 02 → 03.
2. Chạy `tests/SmokeTests.sql`; Messages phải có PASS cho mọi case và dòng PASS cuối cùng.
3. Build solution trong Visual Studio rồi thử thêm/sửa/tìm học phần, tạo học kỳ và mở/đóng đợt.
4. Mở hai cửa sổ cùng chọn một học phần, sửa ở cửa sổ A, sau đó sửa/lưu ở B. B phải bị từ chối do phiên bản cũ; bấm tải lại rồi sửa lại.
5. Thử học kỳ bắt đầu ngày mai: trạng thái cấu hình MO, nhưng hiệu lực chưa đến thời gian và không cho đăng ký. Thử đợt hết hạn: không thể mở lại nếu chưa chỉnh thời gian.
6. Sau khi ghép, chạy luồng đăng ký/hủy của Huệ và kiểm tra sĩ số chỉ được cập nhật một lần bởi trigger.

`tests/Run-ValidationTests.ps1` chạy được trên Windows có .NET Framework để kiểm tra lại 28 case nhập liệu; không cần package test bên ngoài.
