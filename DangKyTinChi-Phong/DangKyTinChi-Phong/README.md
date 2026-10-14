# Đăng ký tín chỉ — database chung và module của Phong

Bộ source này dành cho C# Windows Forms, .NET Framework 4.8 và SQL Server 2016 SP1 trở lên (Express hoặc LocalDB đều được). Không cần cài package NuGet. Solution chạy độc lập để demo phần Phong; có thể đưa hai UserControl vào dashboard của nhóm.

## 1. Phạm vi đúng theo hai ảnh

| Phần giao cho Phong | Đã triển khai ở đâu |
|---|---|
| Thêm, sửa, tìm kiếm học phần theo mã/tên | Tab **Học phần**, `HocPhanControl.cs` |
| Phân loại bắt buộc / tự chọn | `loai_hp`: `BAT_BUOC` / `TU_CHON`; bộ lọc trên danh sách |
| Quản lý số tín chỉ và giới hạn tín chỉ | `so_tin_chi`, `tin_chi_min`, `tin_chi_max`; kiểm tra ở C# và SQL |
| Tạo năm học, học kỳ | Tab **Học kỳ • Đợt đăng ký** |
| Thiết lập bắt đầu, kết thúc đăng ký | DateTimePicker có ngày, giờ, phút, giây |
| Mở / đóng đợt đăng ký | Hai nút riêng; procedure kiểm tra tồn tại, phiên bản, hạn kết thúc |
| Thiết lập hạn hủy đăng ký | `han_huy_dk`, cho phép sau ngày kết thúc đăng ký |

Database chung có đúng **13 bảng và đúng tên các cột trong file bảng quy ước SQL**. Các view, index, constraint, procedure và trigger là đối tượng hỗ trợ, không làm đổi bộ cột quy ước.

Đăng nhập và quản lý sinh viên/khoa/ngành/lớp thuộc Tùng. Giảng viên, phòng học, mở lớp, lịch học thuộc Minh. Đăng ký/hủy/tra cứu kết quả đăng ký thuộc Huệ. Bộ này có bảng và quan hệ để tích hợp các phần đó, nhưng giao diện triển khai tập trung vào phần Phong. Module chạy riêng chưa có đăng nhập; khi ghép, phải mở từ dashboard đào tạo sau khi xác thực và kiểm tra vai trò.

## 2. Các quyết định nghiệp vụ cần cả nhóm dùng thống nhất

1. `tin_chi_min`/`tin_chi_max` được hiểu theo đúng tên cột là giới hạn tín chỉ **của học phần**. Môn 3 tín chỉ cố định: nhập `3 / 3 / 3`. Không dùng hai cột này cho sĩ số hoặc tổng tín chỉ của sinh viên. Sĩ số lớp dùng `LOP_HOC_PHAN.si_so_max`. Giới hạn tổng tín chỉ theo học kỳ chưa có cột tương ứng trong quy ước, nên không tự thêm.
2. Học kỳ gồm `1`, `2`, `3` (hè). Một cặp năm học/học kỳ chỉ có một bản ghi. File quy ước gộp thông tin đợt vào `HOC_KY`, vì vậy mỗi học kỳ có một khoảng đăng ký, không tự thêm bảng đợt đăng ký riêng.
3. Giờ trong SQL là giờ Việt Nam **UTC+7**, được lấy từ `DATEADD(HOUR,7,SYSUTCDATETIME())`, không phụ thuộc server đang đặt múi giờ nào. Giá trị nhập trên form cũng được hiểu là giờ Việt Nam.
4. Đợt mới mặc định `DONG`. Đăng ký mới chỉ có hiệu lực khi `trang_thai='MO'` **và** `tg_bat_dau <= giờ hiện tại <= tg_ket_thuc`. Mở trước ngày bắt đầu là lên lịch mở, không nhận đăng ký ngay. Hết hạn, view tự trả `cho_phep_dang_ky=0`; không cần chương trình chạy nền đổi cột trạng thái.
5. Hủy theo khoảng `tg_bat_dau <= giờ hiện tại <= han_huy_dk`, độc lập với mở/đóng nhận đăng ký mới. Nếu nhóm muốn nút đóng chặn cả hủy, phải sửa quy tắc này tại `vw_HocKyPhong` và module Huệ.
6. Chính sách của bộ demo là **1 ≤ min ≤ số tín chỉ ≤ max ≤ 30**. Đây là giới hạn kiểm tra được chọn cho dự án, không phải quy định tín chỉ của UTT. Nếu giảng viên yêu cầu khác, sửa đồng thời `Validation.cs`, `Ui.Number()`, `CK_HP_tc` và `usp_HocPhan_Luu`.
7. Mã học phần/học kỳ có 1–20 ký tự ASCII: chữ, số, `_`, `-`; được chuyển thành chữ hoa. Mã không được đổi khi sửa. Tên hỗ trợ Unicode tiếng Việt.
8. Không xóa học phần trên UI vì ảnh giao phần Phong yêu cầu thêm/sửa/tìm. Có thể bỏ chọn **Đang sử dụng** để ngừng dùng; dữ liệu lịch sử và liên kết được giữ. Khi đã có đăng ký hoặc kết quả, không đổi số tín chỉ của học phần.
9. Học kỳ đã có lớp không được đổi năm học hoặc số học kỳ. Có thể sửa khoảng đăng ký và hạn hủy. Việc sửa khoảng thời gian không tự xóa đăng ký trước đó.
10. `KET_QUA` theo bộ cột đã cho lưu một kết quả hiện tại cho mỗi sinh viên/học phần; chưa biểu diễn nhiều lần học hay lịch thi riêng. Không tự thêm cột/bảng hoặc tự đặt công thức tính điểm khi chưa có yêu cầu.

## 3. Chạy từng bước trên máy Windows

### Bước A — tạo database

1. Mở **SQL Server Management Studio (SSMS)**, kết nối instance bạn đã cài.
2. Nếu dùng SQL Server Express, server thường là `.\SQLEXPRESS`; nếu dùng LocalDB thì `(LocalDB)\MSSQLLocalDB`. Tên thực tế phải khớp instance trên máy bạn.
3. Mở `database/01_TaoDatabase.sql`, bấm **Execute / F5**. Script tạo database tên **DangKyTinChi**, đủ 13 bảng, khóa ngoại và CHECK. Nó không DROP hoặc ghi đè bảng cũ. Nếu database đã có bảng, script dừng; đừng xóa database hiện có chỉ để chạy lại.
4. Mở `database/02_NghiepVuPhong.sql`, **F5** để cài procedure/view/trigger. Có thể chạy lại file này khi cần cập nhật đối tượng hỗ trợ.
5. Mở `database/03_DuLieuMau.sql`, **F5**. Dữ liệu giả lập gồm 8 học phần, một học kỳ theo năm hiện tại, một lớp học phần và các bản ghi liên quan. Đợt mẫu mặc định đóng. Chạy lại không nhân bản các mã mẫu; thời gian mẫu cũ không bị tự chỉnh.
6. Có thể chạy `tests/SmokeTests.sql` trên database thử nghiệm để kiểm tra thêm/sửa, trùng mã, tín chỉ, token cũ, ngày đảo, mở/đóng, tương lai và hết hạn. Nếu PASS toàn bộ, script dọn các bản ghi test. Nếu có lỗi, dừng kiểm tra và xử lý các mã `ZTEST_` trước khi chạy lại.

Không phát hành `.bak` hoặc `.mdf` chưa kiểm chứng. Các script SQL là bộ cài database, có thể tạo trên instance thật của bạn.

### Bước B — chạy WinForms

1. Cài **Visual Studio** với workload **.NET desktop development**, cùng targeting pack **.NET Framework 4.8**. Đây là project WinForms cho Windows, không phải chương trình web.
2. Giải nén, mở `DangKyTinChi-Phong.sln` trong Visual Studio.
3. Bấm **Build → Build Solution** rồi **F5**.
4. Mặc định ứng dụng kết nối `(LocalDB)\MSSQLLocalDB`. Nếu database của bạn ở instance khác, bấm **Kết nối SQL Server** ở góc trên bên phải.
5. Nhập server và database `DangKyTinChi`, bấm **Kiểm tra** rồi **Lưu kết nối**. Hộp này dùng Windows Authentication, không yêu cầu tài khoản đăng nhập của ứng dụng.
6. Chọn một dòng học phần để sửa, hoặc **Thêm mới** rồi **Lưu**. Bấm **Tìm kiếm** hoặc Enter trong ô tìm; lọc bắt buộc/tự chọn/trạng thái.
7. Vào tab học kỳ, chọn dòng mẫu; kiểm tra các thời gian rồi **Mở đợt**. Nếu đợt hết hạn, sửa kết thúc/hạn hủy và lưu trước khi mở.
8. **Đóng đợt** dừng nhận đăng ký mới. Cột **Hiệu lực thực tế** cho thấy đợt đang mở, chờ đến thời gian, hết hạn hay đã đóng. Bấm **Tải lại** để lấy trạng thái hiện tại.

Khi sửa cùng một bản ghi ở hai máy/cửa sổ, lần lưu với token cũ bị từ chối. Bấm **Tải lại** để nhận phiên bản mới rồi sửa lại. Nút tải lại hỏi trước khi bỏ phần đang nhập dở.

### Nếu dùng SQL Authentication

Hộp kết nối trên UI chỉ dùng Windows Authentication. Bạn có thể cấu hình `connectionStrings` trong `Phong.WinForms/App.config` với `User ID`/`Password` theo tài khoản SQL của nhóm. Nếu trước đó đã lưu kết nối trên UI, xóa `%LOCALAPPDATA%\DangKyTinChiPhong\connection.xml` để ứng dụng đọc lại App.config. Không đưa mật khẩu thật vào repo hoặc gửi file cấu hình đó cho người khác.

## 4. Cấu trúc source

```text
DangKyTinChi-Phong.sln
Phong.WinForms/
  App.config                  Chuỗi kết nối mặc định
  Models.cs                   DTO học phần, học kỳ
  Validation.cs               Kiểm tra nhập liệu
  Data/PhongRepository.cs     Truy vấn async bằng SqlParameter
  Data/ConnectionSettings.cs  Cấu hình kết nối theo người dùng Windows
  Forms/HocPhanControl.cs      UI học phần
  Forms/HocKyControl.cs        UI học kỳ/đợt đăng ký
  Forms/MainForm.cs            Dashboard demo phần Phong
  Forms/ConnectionForm.cs      Hộp cấu hình SQL
  Forms/Ui.cs                  Thành phần giao diện chung
  Program.cs                  Điểm khởi chạy
 database/                    Bộ cài SQL
 docs/                        Quy ước tích hợp và từ điển dữ liệu
 tests/                       Test validation và smoke test SQL
```

UI được viết bằng code, không phụ thuộc `.Designer.cs` hay `.resx`; không cần dán source vào Form Designer. Khi ghép, dùng hai UserControl với namespace `DangKyTinChi.Phong` để hạn chế trùng lớp của thành viên khác.

## 5. Ghép phần Phong vào project nhóm

Xem chi tiết tại `docs/TICH_HOP.md`. Nếu project nhóm cũng dùng .NET Framework 4.8, thêm project này vào solution rồi **Add Reference → Projects → Phong.WinForms**, hoặc copy `Models.cs`, `Validation.cs`, thư mục `Data` và `Forms` cần thiết. Không copy `Program.cs` nếu project nhóm đã có điểm khởi chạy.

```csharp
var repo = new DangKyTinChi.Phong.Data.PhongRepository(connectionString);
var hocPhan = new DangKyTinChi.Phong.Forms.HocPhanControl(() => repo);
hocPhan.Dock = DockStyle.Fill;
panelNoiDung.Controls.Clear();
panelNoiDung.Controls.Add(hocPhan);
```

Với học kỳ, thay bằng `HocKyControl`. Sau khi add vào form, gọi `await hocPhan.ReloadAsync();` trong event `Load`/`Shown`, bắt lỗi và thông báo cho người dùng. Kiểm tra vai trò `DAO_TAO`/`QUAN_TRI` trước khi mở module.

## 6. Kiểm tra và giới hạn xác minh

Xem `docs/KIEM_TRA.md` để biết chính xác những kiểm tra đã thực hiện và những việc phải chạy trên máy Windows + SQL Server của nhóm. Bộ SQL smoke test được cung cấp để kiểm tra trên engine thật; không coi việc đọc/parse SQL là đã chạy database thành công.
