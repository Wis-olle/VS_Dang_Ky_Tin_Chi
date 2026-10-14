# Hướng dẫn làm việc trong dự án

## Đọc trước khi sửa

- Làm việc tại `E:\AI Agents\Dangkytinchi`; đọc `SESSION_HANDOFF.md` và `PHAM_VI_VA_PHAN_CONG.md` để nắm trạng thái và phạm vi.
- Khi sửa UI, đọc thêm `QuanLyGiangVien/README.md`, `QuanLyGiangVien/docs/ASSET_SOURCES.md` và xem ảnh hiện tại trong `QuanLyGiangVien/docs/screenshots/`.
- Thư mục `E:\AI Agents\visual` là vị trí cũ và nơi chứa dự án tác giả dùng làm tham khảo. Mã nguồn giảng viên hiện tại nằm tại thư mục mới.
- Tài liệu bàn giao phản ánh trạng thái ngày 10/10/2026. Nếu yêu cầu mới hoặc code mới thay đổi quyết định, cập nhật tài liệu liên quan theo kết quả thực tế.

## Ưu tiên đã được người dùng xác nhận

- Trao đổi bằng tiếng Việt. Thực hiện yêu cầu đến khi có kết quả kiểm chứng; chỉ hỏi khi thiếu thông tin thiết yếu.
- Dựng WinForms bằng code C#, ưu tiên không dùng kéo thả Designer.
- Hiện làm phần của **Minh**, bắt đầu từ quản lý giảng viên. Không tự triển khai phần của các thành viên khác hoặc chức năng mới ngoài yêu cầu tiếp theo.
- Giữ phong cách Student Dashboard pastel hiện tại, font Plus Jakarta Sans, bộ 48 avatar Notionists và minh họa Overflow Design.
- Khi cần thiết kế hoặc tài nguyên mới, ưu tiên tìm nguồn trên Dribbble, Awwwards, Framer, 21st.dev, Codrops; kiểm tra nguồn và giấy phép, đóng gói asset dùng cục bộ. Người dùng đã yêu cầu designer subagent cho công việc thiết kế.
- Chuyển động phải mượt và phù hợp WinForms. Giữ cache, tọa độ float, double buffering và clock chung khi sửa các control có animation.
- Các phần đã bị yêu cầu bỏ được liệt kê trong `SESSION_HANDOFF.md`; không tự thêm lại.

## Quy trình phát triển

Người dùng đã cung cấp hướng dẫn ECC: agent-first, lập kế hoạch cho tính năng phức tạp, TDD cho tính năng/bug fix, review sau sửa, bảo mật và kiểm tra đầu vào. Áp dụng trong phạm vi phù hợp với dự án này:

- Phân công subagent chuyên môn khi cần: planner cho tính năng phức tạp, designer cho thiết kế, code-reviewer sau sửa code, build-error-resolver khi build lỗi. Ghi rõ phạm vi file để tránh sửa đè.
- Với thay đổi logic hoặc bug cần tái hiện, viết kiểm tra thất bại trước sửa rồi xác nhận qua sau sửa. Với thay đổi giao diện, tận dụng harness native và ảnh render thật.
- Chạy build và kiểm tra phù hợp với phần thay đổi. Không cần chạy lại toàn bộ UI chỉ vì sửa Markdown.
- Mục tiêu coverage do người dùng đặt là 80%+. Ghi rõ phạm vi được đo; số coverage domain hiện có không chứng minh coverage toàn ứng dụng WinForms.
- Giữ record domain bất biến; kiểm tra đầu vào ở biên. Không ghi credential vào code. Control WinForms vẫn cập nhật trạng thái UI theo mô hình của framework.
- Giải phóng bitmap, Font, Brush, Pen, timer và handler khi không còn dùng. Không làm animation bằng cách dịch vị trí control tương tác mỗi frame.
- Không báo FPS hoặc độ mượt toàn hệ thống dựa trên GIF/DrawToBitmap; báo đúng phương pháp và giới hạn phép đo.
- Giữ nguồn và giấy phép asset dù credit đã bỏ khỏi UI.
- Tôn trọng tệp và thay đổi của người dùng. Không tự đóng Visual Studio, xóa trạng thái người dùng hoặc ghi đè executable đang chạy; dùng output kiểm chứng riêng khi cần.
- Các thao tác bên ngoài như push, publish, gửi tin nhắn hoặc thay đổi tài nguyên dịch vụ cần được người dùng cho phép. Không tự commit/push khi chỉ được yêu cầu sửa UI hoặc tài liệu.

## Cập nhật tài liệu

- Trạng thái bàn giao và quyết định mới: `SESSION_HANDOFF.md`.
- Phân công hoặc phạm vi chức năng: `PHAM_VI_VA_PHAN_CONG.md`.
- Cách chạy và thao tác app: `QuanLyGiangVien/README.md`.
- Kiểm chứng và benchmark: `QuanLyGiangVien/docs/UI_VERIFICATION.md`.
- Nguồn thiết kế/asset: `QuanLyGiangVien/docs/ASSET_SOURCES.md`.
- Không nhân bản các chi tiết giấy phép hoặc benchmark sang nhiều tài liệu. Các Markdown bàn giao nên trỏ về nguồn chi tiết đã có.

