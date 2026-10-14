# Hợp đồng tích hợp database của nhóm

## Quy ước kiểu và trạng thái

| Đối tượng | Quy ước |
|---|---|
| Mã khoa, ngành, lớp, sinh viên, giảng viên, học phần, học kỳ, phòng, lớp học phần | `varchar(20)`, mã do ứng dụng cấp; không tự tăng |
| `ma_tk`, `ma_dk`, `ma_lich`, `ma_kq` | `bigint IDENTITY(1,1)`; không truyền trong INSERT thông thường |
| Tên, mô tả, địa chỉ, giới tính | Unicode `nvarchar`; literal SQL có tiền tố `N` |
| Trạng thái KHOA/NGANH/LOP/SINH_VIEN/GIANG_VIEN/TAI_KHOAN/HOC_PHAN/PHONG_HOC | `bit`: 1 hoạt động, 0 ngừng/khóa |
| `HOC_PHAN.loai_hp` | `BAT_BUOC`, `TU_CHON` |
| `HOC_KY.trang_thai` | `MO`, `DONG`; không chỉ dựa vào cột này để quyết định hạn |
| `LOP_HOC_PHAN.trang_thai` | `MO`, `DONG`, `HUY`; `FULL` là trạng thái hiệu lực suy ra từ sĩ số |
| `DANG_KY.trang_thai` | `DA_DANG_KY`, `DA_HUY` |
| `TAI_KHOAN.vai_tro` | `SINH_VIEN`, `GIANG_VIEN`, `DAO_TAO`, `QUAN_TRI` |
| Giới tính | `N'Nam'`, `N'Nữ'`, `N'Khác'` |
| Thứ trong `LICH_HOC` | 2=thứ hai, …, 7=thứ bảy, 8=chủ nhật |
| Tiết học | 1–15, khoảng gồm cả tiết bắt đầu và kết thúc |
| Ngày giờ nghiệp vụ | `datetime2(0)`, giờ Việt Nam UTC+7 |
| Kết quả | `DAT`, `KHONG_DAT`, hoặc NULL khi chưa đủ dữ liệu |

## Tùng — tài khoản và hồ sơ

- `SINH_VIEN.ma_lop` và `ma_nganh` phải thuộc cùng một lớp/ngành; khóa ngoại kép ngăn lưu sai ngành so với lớp.
- Một sinh viên hoặc giảng viên chỉ có một tài khoản liên kết. Tài khoản đào tạo/quản trị có `ma_sv` và `ma_gv` đều NULL. Tài khoản sinh viên/giảng viên phải liên kết đúng đối tượng tương ứng.
- Lưu mật khẩu đã băm có salt và thông số thuật toán vào `mat_khau_hash` (tối đa 512 ký tự). Không lưu mật khẩu rõ hoặc chỉ dùng một SHA đơn lẻ. Module này chưa hiện thực cơ chế băm/đăng nhập.
- Không xóa hồ sơ đã có đăng ký/kết quả bằng cascade. Dùng ngừng hoạt động/khóa, hoặc xây luồng xử lý lịch sử trước nếu nhóm thực sự muốn xóa.
- Dashboard phần Phong chỉ dành cho vai trò đào tạo/quản trị. Project demo hiện không thực hiện xác thực.

## Minh — lớp học phần và lịch học

- Chọn học phần đang sử dụng, học kỳ hợp lệ, giảng viên/phòng phù hợp trước khi mở lớp. Đây là kiểm tra dịch vụ của module Minh; FK chỉ kiểm tra đối tượng tồn tại.
- Khi tạo lớp, `si_so_hien_tai=0`. Sau đó không tự gán/tăng/giảm `si_so_hien_tai`: trigger `tr_DangKy_SiSo` cập nhật số này từ các thay đổi đăng ký.
- CHECK không cho giảm `si_so_max` xuống dưới sĩ số hiện tại. UI phải thông báo rõ lý do lỗi.
- `ma_phong` ở lớp là phòng mặc định, còn phòng mỗi buổi nằm trong `LICH_HOC.ma_phong`. Khi xếp lịch có thể dùng phòng khác theo buổi.
- Trùng phòng/trùng giảng viên được kiểm tra theo giao của: thứ, khoảng ngày, khoảng tiết. Database này chỉ có FK/CHECK cho từng dòng lịch; module Minh phải làm kiểm tra xung đột trong transaction. Không tuyên bố rằng FK sẽ tự phát hiện lịch trùng.
- `FULL` không INSERT vào `LOP_HOC_PHAN.trang_thai`. Đọc trạng thái hiệu lực từ view `vw_LopHocPhanMo`.

## Huệ — đăng ký, hủy và tra cứu

### Đăng ký

View hỗ trợ tra cứu:

```sql
SELECT * FROM dbo.vw_LopHocPhanMo
WHERE ma_hk=@ma_hk AND cho_phep_dang_ky=1 AND trang_thai_hieu_luc='MO';
```

Khi thực hiện INSERT đăng ký, không tin điều kiện UI đã đọc từ trước. Trong **cùng một transaction**, kiểm tra lại:

1. Sinh viên đang hoạt động, học phần đang sử dụng, lớp `MO`, học kỳ `MO`, giờ hiện tại trong khoảng bắt đầu/kết thúc.
2. Lớp còn chỗ. CHECK và trigger sĩ số là lớp bảo vệ cuối; xử lý lỗi 547 bằng thông báo lớp đã hết chỗ/ràng buộc không hợp lệ.
3. Sinh viên chưa có đăng ký hoạt động cho học phần đó trong cùng học kỳ, kể cả ở lớp học phần khác. Unique index hiện chỉ chặn cùng sinh viên/cùng lớp; kiểm tra xuyên lớp là trách nhiệm module đăng ký.
4. Không trùng lịch các lớp sinh viên đã đăng ký; không vượt tổng tín chỉ nếu nhóm có quy tắc bổ sung.
5. INSERT `DANG_KY` với `trang_thai='DA_DANG_KY'`, `ngay_huy=NULL`; trigger tự tăng sĩ số. **Không UPDATE sĩ số lần nữa.**

Để tránh tranh chấp, thống nhất khóa học kỳ/lớp trước khi kiểm tra và ghi. Nếu dùng `UPDLOCK,HOLDLOCK`, mọi thao tác trong module đăng ký phải theo cùng một thứ tự khóa. Có thể dùng khóa ứng dụng theo sinh viên và lớp để chặn đăng ký hai lớp cùng học phần song song; triển khai và test ở module Huệ.

### Hủy

- Trong transaction, kiểm tra lại người sở hữu đăng ký và `cho_phep_huy` của học kỳ theo giờ hiện tại.
- UPDATE đăng ký đang hoạt động thành `DA_HUY`, đồng thời gán `ngay_huy=DATEADD(HOUR,7,SYSUTCDATETIME())`. Không DELETE để giữ lịch sử.
- Trigger tự giảm sĩ số. Không giảm sĩ số thủ công.
- Đăng ký lại sau hủy: tạo dòng mới; unique index lọc chỉ chặn dòng `DA_DANG_KY`.

Các kiểm tra đăng ký/hủy và xung đột lịch nói trên chưa phải chức năng giao cho Phong; cần được Huệ/Minh hiện thực khi ghép. View chỉ cung cấp dữ liệu, không thay thế transaction nghiệp vụ.

## Module Phong — cách gọi và chống ghi đè

- UI đọc `vw_HocPhanPhong`/`vw_HocKyPhong` qua procedure danh sách. Cột `phien_ban` kiểu `binary(32)` là dấu vân tay SHA-256 của các trường bản ghi, không thêm cột vào bảng.
- Khi sửa hoặc mở/đóng, truyền token bản đã đọc. Procedure khóa và so sánh; token cũ trả thông báo cần tải lại. Khi lưu thành công, đọc lại bản ghi để có token mới.
- Token kiểm tra đồng thời, không phải token đăng nhập hay phân quyền.
- Procedure học kỳ lưu thời gian/năm học, không tự mở đợt. Muốn đổi trạng thái gọi `usp_HocKy_DoiTrangThai`.

| Procedure | Công dụng |
|---|---|
| `usp_HocPhan_DanhSach` | Tìm mã/tên, lọc loại và trạng thái |
| `usp_HocPhan_Luu` | Thêm/sửa học phần, xác thực tín chỉ, token khi sửa |
| `usp_HocKy_DanhSach` | Tìm mã/năm học, trả trạng thái hiệu lực |
| `usp_HocKy_Luu` | Thêm/sửa học kỳ, mặc định đóng khi thêm |
| `usp_HocKy_DoiTrangThai` | Mở/đóng với token và kiểm tra hết hạn |

Không cấp quyền ghi toàn bộ database cho tài khoản người dùng cuối nếu triển khai thật. Project này là module học tập chạy bằng tài khoản Windows có quyền với database; luồng đăng nhập và thiết kế quyền DB chung cần ghép với phần Tùng.

## Database đã có từ trước

Không chạy `01_TaoDatabase.sql` để ghi đè database có dữ liệu. So sánh schema của nhóm với `docs/TU_DIEN_DU_LIEU.md`; nếu khác kiểu dữ liệu/định danh/trạng thái, lập migration có kiểm tra dữ liệu và backup. Bộ này không có migration vì chưa được cung cấp schema/source hiện có của nhóm.
