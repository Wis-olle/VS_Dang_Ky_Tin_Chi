# Bàn giao session — UI phần Minh

Cập nhật ngày **15/10/2026**, sau ghép các phân hệ hiện có của nhóm.

## Ghép nhóm — trạng thái hiện tại

Người dùng yêu cầu commit/push bản chạy đủ cổng đào tạo và sinh viên lên **main** của `Wis-olle/VS_Dang_Ky_Tin_Chi`, kèm database để nhóm clone chạy; **không push README**. Đã tạo local commit sửa học kỳ **894d460** không chứa README. Bản đưa lên repo nhóm được chuẩn bị từ main remote để giữ nguyên README hiện có và không đẩy các commit cũ có sửa README; mã SQL/migration/seed, `scripts/Setup-Team.ps1` và hướng dẫn `QuanLyGiangVien/docs/CLONE_AND_RUN.md` được đóng gói. Hai catalog demo tách vai trò cho cùng Windows login; hướng dẫn ghi rõ dữ liệu hai catalog không tự đồng bộ, triển khai chung cần identity riêng trên cùng catalog. Không sửa clone nhóm đang có thay đổi riêng tại `VS_Dang_Ky_Tin_Chi/`.

Sau commit **882e936**, người dùng báo mở `ThoiKhoaBieuSinhVien` có lịch nhưng `DangKyHocPhanSinhVien` không có. Audit live xác nhận cùng DB preview/SV2024001; dữ liệu Huệ/Minh khớp (HK1: 4 lớp/12 tín chỉ/6 buổi; HK2: 0), nhưng `usp_LayHocKy` trả HK2 trước còn snapshot Minh trả HK1 trước. Đã sửa migration `07`: danh mục học kỳ ưu tiên khoảng ngày học chứa ngày hiện tại theo giờ SQL Việt Nam, sau đó kỳ sắp tới gần nhất/kỳ đã học gần nhất; bổ sung ngày học vào output (giữ hai cột cũ). Cổng sinh viên lấy lựa chọn ban đầu qua cùng danh mục SQL cho cả hai profile, giữ lựa chọn thủ công khi chuyển tab; không âm thầm mở lịch mặc định khác khi danh mục lỗi. Đã áp dụng `07` bằng UTF-8 lên main/preview, hashes dữ liệu học kỳ/đăng ký, counts và identity trước/sau không đổi. SQL gốc Huệ giữ checksum. **429 tests qua/2 opt-in bỏ qua**, native đăng ký/lịch **25/27 qua** ở 96 DPI, nhánh lỗi truy vấn **2/2 qua**. Chi tiết tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md). Lượt sửa này chưa commit/push.

Người dùng xác nhận ghép thành **hai tab Đăng ký / Thời khóa biểu trong form sinh viên**. Đã thêm `StudentPortalPage` vào shell; hai tab dùng chung actual SQL identity/học kỳ, giữ controls tra cứu và view lịch (tuần/cuộn), nạp SQL mới mỗi lần vào lịch để phản ánh đăng ký/hủy. Khi tác vụ đang chạy không cho đổi tab/ngắt kết nối. Profile **ThoiKhoaBieuSinhVien** và **DangKyHocPhanSinhVien** cùng mở cổng sinh viên và tự kết nối, chọn trước lịch/đăng ký tương ứng; demo lịch cũ chuyển sang profile **ThoiKhoaBieuDemo** (CLI demo không đổi). Giữ bố cục/palette/asset/motion lịch hiện tại. RED→GREEN native, suite **427 qua/2 opt-in bỏ qua**, portal **24/24** từ đăng ký và **25/25** từ lịch, shell **35/35**, lịch demo **30/30** ở 96 DPI; phương pháp/giới hạn tại tài liệu kiểm chứng.

`scripts/Initialize-StudentPreview.ps1` tạo fixture SQL local riêng **DangKyTinChi_Minh_Test_StudentPreview** đủ migration/seed và ánh xạ Windows thực tế sang SV2024001/Bùi Quang Minh chỉ khi DB mới được tạo; rerun không ghi đè ánh xạ khác. **Giữ database mẫu này để người dùng chạy lại profile**, không dọn cùng DB kiểm thử dùng một lần. Main DB vẫn DAO_TAO/5 đăng ký. Theo phản hồi không đăng ký được, đã mở riêng HK1-2627 trong preview đến 22/10/2026 qua switch -OpenRegistration; không đổi hạn ở database làm việc. Người dùng đã yêu cầu commit cục bộ; không push.

Theo yêu cầu mới, đã nhúng **Minh + Phong + Huệ** vào một cửa sổ `Features/Shell/CampusForm`, mặc định chạy từ project `QuanLyGiangVien`. Dùng chung database Huệ `DangKyTinChi_Minh`; đã nâng cấp thực tế thêm `06_Catalogs.sql`, `07_RegistrationIntegration.sql`, giữ số bản ghi/quyền tài khoản làm việc và checksum SQL gốc. Điều hướng theo vai trò thật SQL, F5/chuyển trang đọc lại dữ liệu, bảo vệ thay đổi chưa lưu và vòng đời async/dispose. Demo và `--standalone` giữ riêng. Hướng dẫn chạy và bảng thiếu theo người tại [TEAM_INTEGRATION.md](QuanLyGiangVien/docs/TEAM_INTEGRATION.md).

**Tùng chưa ghép được:** đã kiểm tra GitHub `Function_1` lẫn thư mục mới người dùng đưa `Quanly_tinchi (2)/Quanly_tinchi`; chỉ có SQL và `c#.slnf`, không có project/code form `WinFormsApp3` mà filter trỏ tới. Cần thư mục project thật; chưa có UI đăng nhập/tài khoản, sinh viên, khoa/ngành/lớp hành chính. Màn hình Kết nối hệ thống hiện dùng tài khoản Windows/SQL đã cấp quyền, không phải đăng nhập của Tùng. Adapter/model/CSV còn thiếu từ source Huệ đã được bổ sung trong project chung, không còn chặn chạy các chức năng Huệ đã ghép. Không tự xây các chức năng còn thiếu của Tùng.

Build sạch; suite **427 qua, 2 opt-in bỏ qua**; luồng SQL Phong → Minh → Huệ → thời khóa biểu → hủy **1/1 qua riêng**. Native shell **35/35**, database chưa có học kỳ **5/5**, hồi quy standalone giảng viên/lớp-phòng/sinh viên **119/218/30 qua** ở 96 DPI. Phạm vi coverage, giới hạn UI và bằng chứng tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md). Database kiểm thử dùng một lần được dọn sau lưu bằng chứng; không commit/push.

## Database SQL phần Minh — mới nhất

Theo yêu cầu người dùng, đã lấy **bộ SQL Huệ** ở branch `SQL_Function_4`, commit `e4b04e1`, làm chuẩn; giữ nguyên 13 bảng, mã trạng thái và các thủ tục đăng ký. Nguồn/hash, migration, contract và giới hạn tương thích tại [database/README.md](QuanLyGiangVien/database/README.md). Tạo thực tế **`localhost / DangKyTinChi_Minh`** bằng Windows Authentication; ánh xạ login Windows hiện tại sang phòng đào tạo trong database riêng. Không sửa/push repo nhóm hoặc database khác của người dùng.

App mặc định dùng SQL. Giảng viên/phòng/lớp/lịch lưu SQL trước khi đổi bộ nhớ; hồ sơ giảng viên dùng lớp phân công thật. Sĩ số lấy từ `DANG_KY`, kết quả lưu lớp trả số thực tế cho UI. **F5 trên màn hình quản lý** nạp lại snapshot từ phiên khác; mở lại app cũng nạp SQL. Có `--demo` riêng và profile demo để kiểm chứng, lỗi SQL không tự fallback. Cách khởi tạo/nâng cấp/chạy/profile tại [README app](QuanLyGiangVien/README.md).

Migration bổ sung học vị/avatar, khoa học phần, ngày học học kỳ; hạn đăng ký Huệ giữ nguyên ý nghĩa. Lịch có sinh viên đăng ký không được thay đổi cấu trúc; kiểm tra xung đột và quyền ở server. Thời khóa biểu SQL lấy danh tính/đăng ký của chính tài khoản `SINH_VIEN` qua `ORIGINAL_LOGIN()`. Bootstrap local là phòng đào tạo, không giả danh sinh viên. Lượt ghép nhóm ở trên đã nối UI Huệ/Phong; đăng nhập Tùng còn thiếu source. Giữ UI/palette/asset/motion đã chốt.

Seed: 12 giảng viên, 4 khoa, 8 phòng, 8 học phần, 2 học kỳ, 10 lớp; 5 dòng đăng ký của 2 sinh viên, Bùi Quang Minh 4 lớp/12 tín chỉ. Không dùng số sĩ số giả của demo cũ. Build sạch; **336/336 tests** gồm CRUD SQL và hai phiên đồng thời, native SQL quản lý **11/11**, sinh viên SQL **30/30**, hồi quy demo giảng viên **119/119**, lớp/phòng **218/218** tại 96 DPI. Coverage và bằng chứng chi tiết duy nhất tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md); không coi đó là coverage toàn WinForms. Chưa commit/push.

## Thời khóa biểu sinh viên — mới nhất

Lượt mới nhất ngày 15/10: thay nhãn Thời khóa biểu và gạch chân bằng **nút pill có icon lịch**, cùng `AppTheme.Button`/radius/nền/chữ với nav giảng viên. Cổng sinh viên giữ là nhãn vai trò; không thêm trang khác. Icon `calendar-days` được tìm và đối chiếu lại trên Lucide/repo chính thức, dùng SVG cục bộ có license; bọc group `fill=none` để AntdUI giữ nét viền. Bấm mục đang chọn về đầu trang và giữ học kỳ/tuần. Build sạch, **30/30 native checks ở 96 DPI**, bốn snapshot nội dung body giữ nguyên. Ảnh mới `docs/screenshots/student-nav-15/`; nguồn và kiểm chứng tại các tài liệu chuyên biệt.

Tên sinh viên hiển thị đã sửa theo người dùng thành **Bùi Quang Minh** tại `StudentTimetableDemo.StudentName`, áp dụng chung cho header và hồ sơ. Build `.tmp/student-name-15/build` sạch, native 28/28 qua; snapshot xác nhận hai nhãn đúng tên mới và không còn nhãn tên cũ. Ảnh mới `docs/screenshots/student-header-15/06-correct-student-name.png`.

Phản hồi mới nhất ngày 15/10: thanh phía trên còn trắng. Đã đặt nền cả panel điều hướng và `PageHeader` thành `Canvas` hồng phấn, chỉ sửa hai dòng màu trong `BuildHeader`. Build sạch, native **28/28 ở 96 DPI**; pixel nền caption/nav đúng `#FFEFEF`, bốn snapshot layout trùng trước sửa. Reviewer PASS; ảnh tại `docs/screenshots/student-header-15/`, chi tiết tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md).

Phản hồi tiếp theo ngày 15/10: người dùng không thích cam trong **các ô lịch/nhãn phòng**. Đã đổi riêng buổi chọn và pill sang tím `#7662C9`, chữ pill chọn `#6551BE`, viền lavender và hover cyan→lilac; các phần còn lại giữ palette trước đó. Khung/font/nội dung/thao tác/animation không đổi; bốn snapshot layout trùng lượt trước. Build sạch, native **28/28 ở 96 DPI**, designer/reviewer PASS. Ảnh/GIF mới tại `docs/screenshots/student-session-lilac-15/`; kiểm chứng chi tiết tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md).

Ngày 15/10 người dùng yêu cầu **chỉ phối lại màu theo hai form quản lý, giữ nguyên khung thiết kế**. Đã chuyển palette sang alias `AppTheme`: nền blush, chữ/viền ấm, điểm nhấn coral, thẻ chào peach, lịch sắp tới cyan, học kỳ lilac, buổi thường cyan và buổi chọn coral. Mũ tốt nghiệp render SVG gốc bằng màu primary; minh họa Programming dùng bản phối màu `coding.png` sẵn có của lớp/phòng, cùng SHA256 SVG gốc nên giữ hình. Không chỉnh font, bố cục, nội dung, dữ liệu hoặc animation. Build sạch, **28/28 native checks ở 96 DPI**; bốn snapshot bounds/font/text/tab/accessibility giống trước sửa, 36 tệp quản lý/domain được đối chiếu SHA256 không đổi. Ảnh/GIF ở `docs/screenshots/student-palette-15/`; phương pháp tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md), màu/nguồn tại [ASSET_SOURCES.md](QuanLyGiangVien/docs/ASSET_SOURCES.md). Chưa commit/push.

Người dùng xác nhận trang mới dành cho **sinh viên**, lịch lấy từ các lớp đã đăng ký; tab lịch tuần lớp/phòng vẫn phục vụ phòng đào tạo. Ngày 14/10 đã dựng form độc lập `Features/StudentTimetable/` theo [Student Dashboard — Alireza Attari](https://dribbble.com/shots/8097669-Student-Dashboard): bản đầu dùng nền lavender xám nhẹ, topnav/thẻ trắng và tím nhấn; hiện dùng palette ngày 15/10 ở trên, giữ lịch lớn và hồ sơ bên phải. Mở bằng `--student-timetable` hoặc profile Visual Studio `ThoiKhoaBieuSinhVien`.

Chọn học kỳ/chuyển tuần/tuần hiện tại, chọn buổi bằng chuột hoặc Space để xem chi tiết, rỗng khi không có đăng ký; lịch đủ7ngày/12tiết, giới hạn tuần theo học kỳ. Projection bất biến `Domain/StudentTimetable.cs` lọc mã sinh viên, học kỳ và trạng thái hủy. Fixture `StudentTimetableDemo` có4lớp/12tín chỉ/6buổi HKI; HKII chưa đăng ký. Đây là dữ liệu minh họa, chưa nối đăng nhập/phần Huệ/DB, chưa xác thực hoặc phân quyền thật. Không thêm quản lý điểm, học phí, tin nhắn hoặc đăng ký.

Nguồn online Lucide/unDraw và motion 21st/Motion Primitives được ghi tại [ASSET_SOURCES.md](QuanLyGiangVien/docs/ASSET_SOURCES.md). Asset cục bộ; chuyển động finite dùng clock chung; Plus Jakarta Sans/Notionists giữ bộ hiện có. Build sạch, **259/259 tests**, **28/28 native checks ở96DPI**; có RED→GREEN tên môn buổi1tiết. Coverage domain mới và giới hạn phép đo tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md). Ảnh/GIF ở `docs/screenshots/student-timetable-14/`. Designer và code-reviewer đã phản hồi; lỗi card thấp/card hẹp/harness phụ thuộc ngày đã sửa, reviewer xác nhận. Không sửa UI giảng viên/lớp-phòng trong lượt này; chưa commit/push.

## Giảng viên — lượt mới nhất

Theo yêu cầu mới, ba thẻ thống kê và từng ô Theo khoa có thể bấm ở nền/icon/số/chữ hoặc dùng Tab + Enter/Space. Tổng giảng viên đặt lại bộ lọc; Khoa có giảng viên mở dropdown khoa hiện có; ô khoa lọc đúng khoa và xóa query/status cũ. Người dùng chọn Lượt phân công lớp → danh sách giảng viên có `ActiveClasses > 0`, nhãn **Giảng viên có lớp**; kết hợp được với tìm/khoa/trạng thái và thoát bằng Tổng giảng viên/Đặt lại bộ lọc. Dữ liệu phân công mẫu giảng viên giữ riêng như trước, không mở trang lớp/phòng.

Giữ layout/palette/avatar/motion; mới chỉ thêm vùng tương tác và mode lọc UI. RED tái hiện thẻ Tổng chưa kích hoạt; build sạch, **119/119 native ở96DPI** gồm click/bàn phím, rebuild ô khoa, hồi quy Auth Card/Faculty Spread. Ảnh/GIF tại `QuanLyGiangVien/docs/screenshots/lecturer-click-14/`; phạm vi và giới hạn tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md). Mã scheduling/domain không đổi trong lượt này; chưa commit/push.

## Trang gộp Lớp học & Phòng học — mới nhất

Toàn bộ bốn KPI có thể bấm để mở mục tương ứng (lớp/phòng/lịch/lớp), gồm nền, chữ/số và ảnh. Con trỏ bàn tay trên thẻ/ảnh; CTA vẫn là nút native dùng được bằng bàn phím. Callback gọi một lần, không kích hoạt khi disabled hoặc không có action. Không đổi bố cục/ảnh/motion. RED bắt vùng nền chưa kích hoạt; GREEN **218/218 ở96DPI**, build sạch. Kiểm chứng tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md); không sửa giảng viên, chưa commit/push.

Khung **Lớp trong tuần** đã sửa góc đen ở ba dòng: `SchedulingPreview.OnPaint` tô toàn bộ nền trước đường bo góc. Bỏ hình lịch nhỏ ở header và mở rộng vùng tiêu đề/ngày; không tự thêm lại. Giữ dữ liệu, badge ngày, metadata, callback và animation. RED tái hiện owner repaint trên nền đen; GREEN **203/203 native ở96DPI**, build sạch. Ảnh `docs/screenshots/agenda-fix-14/30-agenda-clean.png`; phạm vi/giới hạn tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md). Không sửa giảng viên, chưa commit/push.

Lượt mới bỏ icon heading và dòng trạng thái nhỏ dưới số trên cả bốn KPI, title căn trái cùng số/CTA, mũi tên sát nhãn với gap6px; hạ mũi tên2px để cân với chữ. Trạng thái vẫn trong accessibility; không đổi ảnh/vị trí/renderer, dữ liệu/ratio/callback hoặc giảng viên/Ui/domain. Build sạch, native **200/200 ở96DPI, scale1**; ảnh/GIF mới ở `QuanLyGiangVien/docs/screenshots/scheduling-kpi-clean-14/`. Không tự thêm lại icon hoặc note. Chi tiết và giới hạn tại [ASSET_SOURCES.md](QuanLyGiangVien/docs/ASSET_SOURCES.md), [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md). Chưa commit/push các lượt sau **bac2761**.

Lượt trước sửa hover mờ và cắt góc trên ảnh3D của bốn Service Card: cache giữ pixel nguồn và vẽ một lần, vùng ảnh chứa toàn bộ zoom/xoay/dịch. Người dùng cuối cùng chọn quay lại ảnh có bốn hình nằm trọn trong thẻ; đã khôi phục bố cục đó, không dùng bản ảnh tràn ra mép theo21st thử giữa lượt. Native199/199 ở96DPI; ảnh lúc zoom rộng/compact ở `docs/screenshots/scheduling-hover-restored-14/`.

Trước lượt sửa hover, đã khôi phục về commit **bac2761** theo yêu cầu người dùng. Các thử nghiệm2D bị loại được lưu riêng ở `.tmp/rollback-2d-20261014-143514/`; không dùng trong runtime hiện tại và không tự đưa lại.

Lượt trước thay cả bốn ảnh trắng bằng ảnh màu nguyên bản từ 3dicons V1: sổ tím, chìa khóa gradient tím–hồng–cam, lịch đỏ và chart tím–cam. Master clay được lưu lại cùng nguồn. Build riêng sạch, native171/171 ở96DPI; designer/reviewer PASS ảnh rộng/compact. Ảnh/GIF ở `docs/screenshots/scheduling-service-color-14/`. Theo yêu cầu ngày14/10 đã commit bản này bằng **bac2761** trên `main`; chưa push.

Lượt trước dùng Service Card từ21st/LaviKatiyar: bốn thẻ cao180, toàn hàng188/gap12, ảnh3D clay cục bộ ở góc phải, CTA native tới các tab0/1/2/0. Sổ/chìa khóa/lịch/chart từ3dicons V1 CC0, giữ master/metadata/hash/license; sourceReactuser-pasted chỉ làm reference. Nhịp học tuần chuyển xuống sidebar cạnh workspace (calendar thì sau lịch), giữ UI/số liệu. Hoverảnh400ms/arrow300ms hữu hạn, chữ/hitbox cố định. Native167/167 ở96DPI, designer/reviewer PASS full/compact; build riêng sạch. Ảnh/GIF `docs/screenshots/scheduling-service-14/`. Đã sửa PNG29DPI tạo alpha rỗng bằngClone pixel.

Lượt mới chỉ đổi UI bốn KPI: card trắng phẳng, icon trái cạnh nhãn, số bên trái/microviz tỷ lệ bên phải, dải trạng thái pastel ở footer. Ba matrix2×4 là thang phần trăm liên tục kèm label; vòng đăng ký giữ số liệu thật. Giữ124/116px/gap8 và mọi vùng khác; source Dribbble Jon Moore/Blocks.so+MIT lưu cục bộ. Build sạch, native136/136 ở96DPI, ảnh/GIF `docs/screenshots/scheduling-kpi-13/`. Chi tiết nguồn và giới hạn tại [ASSET_SOURCES.md](QuanLyGiangVien/docs/ASSET_SOURCES.md), [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md). Snapshot đầu lượt chỉ đổiStat vàharness; chart/layout/giảng viên/domain không đổi, chưa commit/push.

Lượt mới chỉ cải thiện hai khối overview/chart: bốn tile riêng cao116px trong hàng124px, gap8, số lớn đồng đều, icon Lucide cũ và rail/ring tỷ lệ thật. Nhịp học tuần có badge, cột18–22px bo phía trên, autoscale theo số buổi thật, nhãn/tooltip/focus/keyboard và chuyển dữ liệu500ms từ current float; ngày0 không fill, dữ liệu không đổi không restart, hidden/reduced/dispose không giữ job. Workspace bắt đầu y138 trong content. Tham khảo Dribbble Statistics Cards và 21st Fancy Components/Corsair; source MIT pinned và nguồn/giấy phép tại [ASSET_SOURCES.md](QuanLyGiangVien/docs/ASSET_SOURCES.md). Build `.tmp/scheduling-metrics-13/final-build`0lỗi/cảnh báo; fullnative129/129 vàchart21/21 tại96DPI; designer PASS ảnh rộng/compact. Ảnh/GIF `docs/screenshots/scheduling-metrics-13/`; giới hạn tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md). SHA25639tệp giảng viên không đổi, không sửa domain, chưa commit/push thêm.

Phản hồi mới “vẫn đơn điệu” đã được xử lý bằng bảy minh họa unDraw (Programming/Finance/Books/Electricity/Teaching/Server/Lecture), dùng ở thumbnail bảng, cover chi tiết theo khoa/loại phòng và hàng mã lớp của thẻ lịch. Source/SVG gốc/bản palette/PNG/license/SHA256 tại `Assets/scheduling/resource-visuals/Sources.json`. Page sở hữu/cache ảnh, child controls mượn ảnh; chỉ sửa scheduling, không sửa giảng viên/domain. Bảng hàng56px/cột hình62px; inspector466px và dịch nội dung80px để giữ vùng lịch. Calendar ảnh24px ở hàng mã lớp, title/metadata giữ toàn chiều rộng. Build `.tmp/scheduling-imagery-13/final2-build` 0 lỗi/cảnh báo; native100/100 ở96DPI, review cuối không còn HIGH/MEDIUM được xác nhận. Ảnh/GIF tại `docs/screenshots/scheduling-imagery-13/`, chi tiết/giới hạn tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md); vẫn chưa commit/push thêm.

Yêu cầu mới cho phép tìm rộng toàn mạng, không chỉ hai shot: ưu tiên 21st, Awwwards, Codrops và nguồn chính thức có giấy phép. Đã thêm bốn icon động Lucide ở overview, icon/tab underline trượt 220ms, minh họa unDraw Books/Date Picker dùng PNG ngoại tuyến và fill sĩ số 360ms. Giữ bố cục/palette, không sửa giảng viên/domain hoặc thêm dependency runtime. Shared Motion hữu hạn, fixed hitbox, cache bitmap/layer; nguồn/license/SHA256/adaptation tại [ASSET_SOURCES.md](QuanLyGiangVien/docs/ASSET_SOURCES.md).

Build `.tmp/scheduling-assets-13/final-build` 0 lỗi/cảnh báo; full form **82/82 native checks qua ở 96 DPI**, harness icon riêng **41/41 qua ở 96 DPI**. Ảnh/GIF tại `QuanLyGiangVien/docs/screenshots/scheduling-assets-13/`. RED→GREEN bắt job fill khi ẩn và bitmap SVG minh họa rỗng; runtime dùng PNG đã rasterize trước và crossfade riêng vùng lịch để không che fill. Designer/reviewer kiểm tra code/ảnh; giới hạn phương pháp tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md). Chưa commit/push thêm.

Ngày 13/10 đã tái hiện góc đen ngoài thẻ bo góc qua kiểm tra owner paint có bitmap nền đen. `SessionButton.OnPaint` hiện tô toàn bộ nền trắng trước khi vẽ thẻ, giữ nguyên bố cục/màu/chữ/motion và thao tác. Build `.tmp/calendar-corners-13/build` 0 lỗi/cảnh báo; native **61/61 qua ở 96 DPI**, gồm repaint, resize và disabled. Ảnh/kết quả tại `QuanLyGiangVien/docs/screenshots/calendar-corners-13/`; phương pháp và giới hạn tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md). Không sửa giảng viên/domain; thay đổi vẫn chưa commit.

Người dùng đã làm rõ: **chỉ dùng mã màu của trang giảng viên, bố cục và cách thiết kế phải khác**. Lượt mới chỉ nghiên cứu hai shot Dribbble người dùng gửi: Class Management của Sakib N và Class Panel của UIX Alii. Trang hiện có header thao tác gọn, dải bốn số liệu có separator và biểu đồ tuần; bảng lớn/tab gạch chân bên trái, inspector tài nguyên và agenda bên phải. Bỏ banner gradient, artwork, campus./Minh và cách trình bày hồ sơ/avatar của bản đồng bộ trước. Lịch tuần dùng toàn chiều rộng workspace. Màn hình 1100 vẫn giữ hai cột, cuộn dọc nếu cần. Palette alias từ AppTheme; font Plus Jakarta Sans giữ bộ hiện có. **Thanh dọc vẫn đã bỏ**. Nút **Giảng viên** ở header quay về trang cũ; giao diện giảng viên giữ nguyên.

Chọn **Lớp & phòng học** trên nav giảng viên hoặc dùng `--scheduling`. Source tại `QuanLyGiangVien/Features/Scheduling/`, domain tại `Domain/Scheduling/`; dữ liệu **8 phòng, 10 lớp, 2 học kỳ** trong `SchedulingDemo`. Có cửa sổ thêm/sửa phòng, lớp, buổi học; validation sức chứa/trùng phòng–giảng viên theo ngày/tiết thực tế, lịch tuần và bảo vệ tham chiếu. Store được giữ độc lập với view, giữ tab/học kỳ/từ khóa/bộ lọc/tuần/lựa chọn khi dựng lại theo DPI. Giảng viên tham chiếu được chặn xóa hoặc tạm nghỉ tương ứng; lớp mẫu ở hồ sơ cũ chưa đồng bộ thành danh sách lớp mới.

Lượt bố cục ngày 12/10 không tải asset mới và chỉ học hai shot Dribbble, vốn là ảnh tĩnh không có timing animation để xác minh. Yêu cầu ngày 13/10 đã mở rộng nguồn và bổ sung Lucide/unDraw/motion như phần mới nhất ở trên. Không lấy media Dribbble/Awwwards làm asset app; không dùng Notionists/hero trong scheduling. Runtime ngoại tuyến, motion hữu hạn và không ambient job.

Lượt cuối làm mềm heading/chữ phụ, bỏ lưới ô bảng, tag gọn/bo góc, KPI dot nhỏ, chart dịu, agenda có ô thứ pastel và calendar trắng/thẻ bo12. Build `.tmp/scheduling-polish-12/build` 0 lỗi/cảnh báo; ảnh tại `docs/screenshots/scheduling-polish-12/`. Native **58/58 qua ở96DPI**; designer/reviewer đã xem ảnh cuối, không còn HIGH/MEDIUM. Click/keyboard/scroll/metadata giữ nguyên; lỗi dispose font cùng giá trị đã có RED→GREEN và sửa bằng so sánh tham chiếu. SHA25639tệp giảng viên không đổi, domain không sửa. Suite thuần254/254 thuộc lượt trước, không chạy lại cho UI này. Giới hạn DPI, ảnh/GIF và coverage tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md). Chưa ghép module vào bộ khung nhóm; dữ liệu vẫn trong phiên, chưa có DB hoặc chức năng thành viên khác.

Theo yêu cầu commit ngày 12/10, đã khởi tạo Git cục bộ tại workspace, branch `main`, lưu snapshot app/tests/asset/tài liệu trong commit đầu **4431a3e** (bản đồng bộ phong cách trước khi người dùng làm rõ). Snapshot redesign mới được commit tiếp theo yêu cầu ngày 14/10. `.gitignore` loại `.tmp`, build output, trạng thái IDE, credential cục bộ và clone độc lập `VS_Dang_Ky_Tin_Chi/`. Không cấu hình remote hoặc push; không thay đổi lịch sử/working tree repo nhóm. Trước commit mới, suite thuần **254/254 qua** và NuGet không báo package có lỗ hổng ở app hoặc tests theo nguồn hiện hành.

## Bộ khung GitHub dùng chung — 11/10/2026

Theo yêu cầu thử tạo bộ khung nhóm, đã clone `https://github.com/Wis-olle/VS_Dang_Ky_Tin_Chi` vào `VS_Dang_Ky_Tin_Chi/`, tạo branch **local** `chore/bo-khung-chung`. Clone này chưa commit hoặc push; là repo riêng và không thay app giảng viên đang có. Workspace cha có Git cục bộ từ 12/10 và loại clone này khỏi tracking.

Bộ khung có solution `VS_Dang_Ky_Tin_Chi/DangKyTinChi.sln`, WinForms .NET 10/AntdUI 2.4.8 bằng code, Core, tests và 4 view placeholder theo phân công. Chưa tích hợp `QuanLyGiangVien`, chưa triển khai nghiệp vụ của các thành viên hoặc database. Font Plus Jakarta Sans và license được đóng gói cục bộ; không cần asset mới.

Build Release 0 lỗi/cảnh báo; 9/9 tests Core; 12 native smoke checks qua. Phạm vi coverage, ảnh và giới hạn tại [kiểm chứng bộ khung](VS_Dang_Ky_Tin_Chi/docs/VERIFICATION.md). Cách chạy tại [README bộ khung](VS_Dang_Ky_Tin_Chi/README.md); quy trình branch/PR tại [TEAM_WORKFLOW.md](VS_Dang_Ky_Tin_Chi/docs/TEAM_WORKFLOW.md). Cấu trúc/schema/đầu mối tích hợp là đề xuất, cần nhóm thống nhất trước khi ghép các phần.

## Trạng thái giảng viên được giữ từ lượt trước

Yêu cầu mới nhất: sửa góc thẻ thống kê đầu bị cắt khi hover. Center zoom được giữ; clip ngang cho thẻ nằm hoàn toàn trong viewport được nới bằng padding zoom vào gutter Canvas trống của MainForm. Thẻ bị cuộn khuất vẫn clip sát viewport; clip dọc/host/sibling giữ nguyên. Bounds và chữ native không đổi.

Màu loang trên ba thống kê và bốn ô khoa hiện **550 ms** cho cả vào/ra; giữ zoom 500 ms, entrance 1.250 ms, easing và màu riêng từng khoa.

Đã bỏ hiệu ứng preview thông tin khi rê chuột lên bảng giảng viên. Đã gỡ controller, surface native, reveal/lồng thẻ, highlight/parallax tùy biến và capture hook. Chọn giảng viên hoặc nút Xem vẫn mở hồ sơ; dạng Thẻ giữ hover nâng nhẹ hiện có. Mã nguồn cũ được lưu tại `.tmp/list-preview-removed/source/` để bảo toàn thay đổi trong workspace chưa có Git.

Bốn ô Theo khoa có màu loang riêng theo khoa, nhạt hơn chip icon; bảng màu tại [ASSET_SOURCES.md](QuanLyGiangVien/docs/ASSET_SOURCES.md). Giữ zoom nhẹ và chữ native sắc nét khi màu phủ kín.

Đã hoàn thành màu loang riêng cho bốn ô khoa, không chạy hover trên khung ngoài Theo khoa. Ba thống kê và từng ô khoa giữ zoom đơn điệu 1→1,05, không rung/bounce; giữ màu chip gốc. Đã sửa chữ mờ khi màu phủ kín: chữ native được raster theo zoom hiện tại rồi chép nguyên pixel, tách khỏi bitmap nền/icon bị scale. Khung ngoài vẫn có entrance; hover con chờ entrance hoàn tất và hủy khi rời/ẩn/dispose.

Nguồn và các lượt kiểm chứng Interactive List Preview trước khi gỡ được giữ làm lịch sử trong tài liệu chuyên biệt; không xem GIF hoặc kết quả cũ là hành vi hiện tại.

Các thay đổi trước vẫn giữ: hero nổi chu kỳ 5 giây; hai select có animation native. Harness toàn UI trước đó gặp select chập chờn, chưa xử lý ngoài phạm vi yêu cầu này. Không tự triển khai module mới.

Ứng dụng hiện là **UI giảng viên, lớp và phòng của Minh** trong bài tập nhóm đăng ký tín chỉ, không phải hệ thống đăng ký tín chỉ hoàn chỉnh. Có dữ liệu mẫu trong bộ nhớ; sửa dữ liệu chỉ tồn tại trong phiên chạy.

## Vị trí và công nghệ

| Mục | Giá trị |
| --- | --- |
| Workspace | `E:\AI Agents\Dangkytinchi` |
| Solution | `QuanLyGiangVien/QuanLyGiangVien.slnx` |
| App | `QuanLyGiangVien/QuanLyGiangVien.csproj` |
| Test | `QuanLyGiangVien.Tests/QuanLyGiangVien.Tests.csproj` |
| Stack | C# WinForms, .NET 10 Windows, AntdUI 2.4.8 |
| Cách dựng UI | Bằng code; không có `.Designer.cs` |
| Dữ liệu | 12 giảng viên, 4 khoa; model record, kho trong bộ nhớ |
| Git | Repo cục bộ tại workspace, branch `main`; snapshot đầu theo yêu cầu 12/10, chưa có remote/push |

Hai dự án phải giữ cạnh nhau vì solution và test dùng đường dẫn tương đối. App đọc asset qua `AppContext.BaseDirectory`, không phụ thuộc dự án tác giả lúc chạy.

## Thiết kế đã chốt

- Mẫu hiện tại: [Student Dashboard UI Design for EdTech Platform](https://dribbble.com/shots/27591274-Student-Dashboard-UI-Design-for-EdTech-Platform). Mẫu Teacher Dashboard là tham khảo ban đầu, đã được thay bằng phong cách hiện tại.
- Điều hướng trên nền blush; hero gradient lavender–peach; thẻ trắng; hàng thống kê và phân bố theo khoa; danh sách bên trái và hồ sơ bên phải. Cửa sổ nhỏ dùng cuộn/drawer.
- Font **Plus Jakarta Sans**, màu và scale/DPI tập trung trong `Ui/AppTheme.cs`.
- Màu chính: canvas `#FFEFEF`, chữ `#211A1C`, chữ phụ `#6E6267`, CTA `#D44423`, accent `#FE6B4A`; gradient hero `#E2D6FC` → `#FCCAC1`; lilac `#F6EDFE`, peach `#FCCAC1`, mint `#E9F9EF`, cyan `#E4F5F7`.
- Avatar: **48 Notionists** dùng cùng bộ với QuanLyTacGia, PNG gốc 128×128, nét đen trắng trên 5 nền pastel. 12 giảng viên mẫu có avatar khác nhau; nút Đổi ảnh duyệt cả bộ.
- Minh họa hero: **On The Laptop / Regular**, Overflow Design — Office Club. Dùng PNG trong suốt và giữ SVG gốc/manifest/giấy phép.
- 7 icon lớn dùng sprite từ **lucide-animated**, màu Ink trên nền pastel, phát một lượt khi hover.
- Ưu tiên tài nguyên online có nguồn/giấy phép rõ ràng khi cần thay mới; runtime dùng asset cục bộ, không cần React/WebView/mạng.

Nguồn chi tiết và license nằm trong [ASSET_SOURCES.md](QuanLyGiangVien/docs/ASSET_SOURCES.md) và `QuanLyGiangVien/Assets/licenses/`.

## Những phần người dùng đã yêu cầu bỏ

Không tự thêm lại các phần sau:

1. Chữ trên nút chuyển **Thẻ/Bảng**: chỉ giữ icon; vẫn có tooltip và accessibility.
2. Chữ `Campus · Quản lý giảng viên` trên thanh cửa sổ của UI.
3. Nhãn học kỳ `HỌC KỲ I / 2026–2027`.
4. Dòng `12 kết quả · 10 đang giảng dạy` dưới tiêu đề danh sách.
5. Footer dữ liệu mẫu/thay đổi chỉ lưu trong phiên.
6. Footer liên kết credit Avatar/Minh họa.
7. Nút thông tin hình `i` và dialog/link handler của nó.
8. Badge tím **12** cạnh `Danh sách giảng viên`.
9. Chấm/vệt sáng chạy qua lại trên vòng tỷ lệ **83%**.
10. Preview thông tin giảng viên khi hover bảng, gồm reveal/lồng thẻ và highlight/parallax tùy biến.

Số **12** trong thẻ thống kê **Tổng giảng viên** vẫn có. Vòng tỷ lệ vẫn có số phần trăm thật, sweep khi xuất hiện/đổi dữ liệu và hover nhẹ; đứng yên sau khi sweep kết thúc. Tỷ lệ 83% hiện đến từ 10/12 giảng viên đang dạy, không hardcode 83.

Credit bỏ khỏi UI nhưng nguồn và giấy phép vẫn được giữ trong tài liệu/asset.

Yêu cầu bỏ rung đã được làm rõ: người dùng vẫn muốn zoom nhẹ 1,05 lần khi hover. Phạm vi mới là ba thống kê và từng ô khoa; khung ngoài Theo khoa chỉ entrance. Không đưa zoom vào danh sách phần bị cấm thêm lại; giữ zoom đơn điệu, không spring/bounce.

## Sửa hiệu năng và lifecycle cần giữ

- `PageMotionCoordinator`: một clock UI chung, WinForms Timer nominal 15 ms; tính tiến độ theo thời gian thực, không theo số tick. Không diễn giải 15 ms thành FPS đảm bảo.
- Hero dùng `FloatingArtwork`: slot cố định, nền gradient tĩnh, raster cache, vẽ tọa độ float, nổi ±3 logical px; tốc độ hiện tại xem `ASSET_SOURCES.md`. Không thay `BackExtend` hoặc dịch control mỗi tick.
- Hover giảng viên: lift float 3 logical px trong 180 ms; cache chữ/avatar/metadata. Bounds và điểm bấm giữ nguyên.
- Bảng dùng hover sẵn có của AntdUI và thao tác chọn/Xem/Sửa/Xóa; không tạo lớp preview hoặc message filter riêng khi rê chuột.
- Hover từng ô khoa: góc bo 12px, màu pastel riêng theo khoa và zoom nhẹ; đệm icon/count 4px; di chuột giữa các child không restart hiệu ứng. Không còn `SurfaceHover` tint đồng đều trên ô.
- Ba thống kê, entrance khung ngoài và hover từng ô khoa dùng `AuthCardMotion`; overlay ở ngoài AutoScroll, clip theo viewport/gap để không đổi scroll extent hoặc che hero/list/ô bên cạnh. Ô khoa phục hồi nền parent trắng. Chữ cache font/DPI/text/geometry/zoom, raster native theo zoom rồi chép unscaled; icon/số lấy trạng thái sống. Pending hover con phải hủy khi rời chuột/reset/ẩn/dispose. Thời lượng và nguồn xem `ASSET_SOURCES.md`.
- Ring: bỏ ambient marker và tài nguyên Pen/Brush của marker; chỉ repaint cho dữ liệu/hover hữu hạn. `DashboardOverview.OnVisibleChanged` reset ring trực tiếp vì không thể dựa hoàn toàn vào AntdUI truyền sự kiện xuống child.
- Giảm chuyển động tồn tại qua API nội bộ và được harness kiểm tra; hiện không có checkbox/dialog cho người dùng.
- Bitmap cache phải giữ đúng scale/DPI và được hủy khi geometry/scale/DPI đổi. Giải phóng tài nguyên GDI, snapshot và job khi dispose.
- Hai select toolbar dùng `AnimatedSelect`/`SelectPopup`, cùng clock trang. Popup không lấy focus; giữ bounds và dịch phần vẽ, hỗ trợ bàn phím/click ngoài, khôi phục handler khi tái mở trong fade. Chỉ nhận commit chuột sau reveal 200 ms để không chọn sai hàng đang trượt; reduced motion áp dụng ngay. Nguồn và chi tiết kiểm chứng xem hai tài liệu chuyên biệt hiện có.

Lưu ý riêng: `MainForm` giữ `PageHeader.Text = " "` (một khoảng trắng) và font nhỏ để AntdUI còn đủ font metrics vẽ glyph thu nhỏ/phóng to/đóng. Không thay thành chuỗi rỗng mà chưa kiểm tra lại ba glyph bằng render thật. `Form.Text` vẫn giữ tên app cho nhận diện hệ điều hành; không hiện chữ trên PageHeader.

## Bản đồ code

| File/khu vực | Trách nhiệm |
| --- | --- |
| `QuanLyGiangVien/Program.cs` | Khởi tạo WinForms/theme, chạy MainForm, giải phóng tài nguyên chung |
| `MainForm.cs` | Khung trang, layout, bộ lọc, chuyển thẻ/bảng, nối thao tác dữ liệu và hồ sơ |
| `Ui/AppTheme.cs` | Palette, font, DPI/scale |
| `Ui/DashboardHero.cs`, `Ui/FloatingArtwork.cs` | Banner và minh họa nổi |
| `Ui/DashboardOverview.cs` | Tình hình giảng dạy và ring tỷ lệ |
| `Ui/DashboardStats.cs`, `Ui/DashboardFaculty.cs` | Thống kê và các ô khoa |
| `Ui/LecturerCard.cs`, `Ui/LecturerGrid.cs`, `Ui/LecturerRow.cs` | Thẻ/lưới/bảng và thao tác danh sách |
| `Ui/ListHoverUiCheck.cs` | Kiểm tra hover không mở preview và thao tác danh sách/hồ sơ |
| `Panels/LecturerProfile.cs` | Hồ sơ và lớp phân công mẫu |
| `Dialogs/LecturerEditor.cs` | Thêm/sửa, kiểm tra trường nhập, đổi avatar |
| `Domain/Lecturer.cs`, `LecturerStore.cs`, `LecturerQuery.cs` | Record, kho dữ liệu và tìm kiếm |
| `Data/DemoData.cs` | Dữ liệu mẫu |
| `Ui/PageMotionCoordinator.cs`, `Motion.cs`, `MotionMath.cs` | Clock, job và tính toán chuyển động |
| `Ui/AnimatedSelect.cs`, `SelectPopup.cs`, `SelectMotionMath.cs`, `SelectUiCheck.cs` | Hai bộ lọc animated native, popup, easing/geometry và kiểm tra riêng |
| `Ui/AuthCardMotion.cs`, `AuthMotionMath.cs`, `AuthCardUiCheck.cs`, `AuthTextUiCheck.cs`, `FacultySpreadUiCheck.cs` | Entrance dashboard, vòng màu/zoom ba thống kê và bốn ô khoa, cache chữ native và kiểm tra riêng |
| `Ui/SurfaceTransition.cs`, `SurfaceHover.cs`, `MotionButton.cs` | Reveal/crossfade, tint và shine |
| `Ui/Portraits.cs`, `IconSprite.cs`, `IconPlayback.cs`, `AnimatedDashboardIcon.cs` | Asset và playback |
| `Ui/UiCheck.cs`, `Ui/UiCapture.cs` | Harness native và capture control thật |
| `Ui/OverviewMotionMath.cs` | Tính tỷ lệ; còn helper đường đi marker cũ nhưng render không gọi |
| `QuanLyGiangVien.Tests/` | Kiểm thử domain và tính toán thuần |

## Chạy và kiểm chứng

Visual Studio: mở solution tại thư mục mới, chọn **QuanLyGiangVien** làm startup project. Nếu F5 chỉ mở console rồi thoát code 0, kiểm tra có đang chọn **QuanLyGiangVien.Tests** hay không; đó là nguyên nhân trong ảnh người dùng gửi trước đây.

Chạy PowerShell từ workspace:

```powershell
Set-Location -LiteralPath 'E:\AI Agents\Dangkytinchi'
dotnet restore 'QuanLyGiangVien\QuanLyGiangVien.slnx'
dotnet build 'QuanLyGiangVien\QuanLyGiangVien.csproj' --no-restore
dotnet test 'QuanLyGiangVien.Tests\QuanLyGiangVien.Tests.csproj' --no-restore --verbosity minimal
```

Harness UI opt-in mở app kiểm chứng, render bằng DrawToBitmap, lưu ảnh/kết quả rồi tự đóng. Không chụp desktop và không thao tác ứng dụng khác. Luôn xóa biến môi trường sau khi dùng để lần chạy thường không tự đóng:

```powershell
$env:LECTURER_UI_CHECK = 'E:\AI Agents\Dangkytinchi\QuanLyGiangVien\docs\screenshots'
try {
    dotnet run --project 'E:\AI Agents\Dangkytinchi\QuanLyGiangVien\QuanLyGiangVien.csproj'
} finally {
    Remove-Item Env:LECTURER_UI_CHECK -ErrorAction SilentlyContinue
}
```

Nếu executable đang chạy bị khóa, build vào output riêng rồi chạy bản kiểm chứng đó; không dừng app người dùng:

```powershell
dotnet build 'E:\AI Agents\Dangkytinchi\QuanLyGiangVien\QuanLyGiangVien.csproj' --no-restore -o 'E:\AI Agents\Dangkytinchi\QuanLyGiangVien\bin\UiVerification'
```

Kết quả gần nhất sau sửa góc trái thẻ thống kê:

- Build `.tmp/auth-corner-fix/final-build`: **0 lỗi, 0 cảnh báo**, **102/102** kiểm tra native riêng qua ở 120 DPI. RED tái hiện cạnh zoom bị cắt; GREEN bao gồm góc bo, hitbox/thẻ cạnh bên và cuộn ngang thật không tràn màu. Nét chữ cuối vẫn 0 sai khác trên 14 nhãn, cache dùng lại; designer/reviewer không có lỗi cần sửa trong layout hiện tại. Ảnh/GIF/kết quả và giới hạn tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md).

Kết quả trước đó sau tăng tốc màu loang:

- Build `.tmp/fill-550/final-build`: **0 lỗi, 0 cảnh báo**, **95/95** kiểm tra native riêng qua ở 96 DPI sau chỉnh màu loang lên 550 ms. Assertion hoàn tất có 100 ms đệm sau chuỗi tiến độ đơn điệu; lượt 450 ms trước đã RED với duration 1.000 ms cũ. Nét chữ cuối vẫn không có sai khác trên 14 nhãn, cache dùng lại. Reviewer không có lỗi cần sửa; GIF/kết quả tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md). Helper thuần không đổi, không có coverage mới.

Kết quả trước đó sau bỏ preview hover giảng viên:

- Build riêng `.tmp/list-preview-removed/final-build`: **0 lỗi, 0 cảnh báo**, **185/185** kiểm thử thuần còn lại và **17/17** kiểm tra native riêng qua ở 96 DPI. Native RED xác nhận preview vẫn mở trước khi gỡ, GREEN xác nhận hover không mở cửa sổ, giữ focus/selection/bounds và thao tác hồ sơ/lọc/sort/cuộn/chuyển chế độ. 34 kiểm tra helper preview được gỡ cùng tính năng. Ảnh/kết quả và giới hạn tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md).

Kết quả trước đó sau tăng tốc và lồng preview giảng viên:

- Build riêng `.tmp/list-stack/final2-build`: **0 lỗi, 0 cảnh báo**, **79/79** kiểm tra native riêng qua ở 96 DPI. RED xác nhận mất thẻ trước và hai trường hợp opacity nhảy; GREEN xác nhận pixel stack ở zero/intermediate clip, đổi nhanh, thoát/quay lại, hoàn tất trong 400 ms và giải phóng nền. Reviewer cuối không có lỗi cần sửa. Nét chữ/cache và hồi quy bảng/input/lifecycle qua. GIF/kết quả và giới hạn tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md); không có coverage toàn WinForms mới.

Kết quả trước đó sau đổi bốn màu loang riêng theo khoa:

- Build riêng `.tmp/faculty-palette/final-build`: **0 lỗi, 0 cảnh báo**, **95/95** kiểm tra native riêng qua ở 96 DPI. Fixture palette và tham chiếu chữ được cập nhật; 14 nhãn ở zoom cuối không có sai khác cạnh chữ, cache dùng lại. Designer/reviewer không có lỗi cần sửa. Đây là thay đổi màu, không thêm test/coverage mới hoặc chạy lại toàn UI. Ảnh/GIF/kết quả tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md).

Kết quả trước đó sau mở lại preview khi đổi giảng viên:

- Build riêng `.tmp/list-row-reveal/final-build`: **0 lỗi, 0 cảnh báo**, **59/59** kiểm tra native riêng qua. Đã RED khi đổi hàng trong reveal rồi GREEN; thêm kiểm tra đổi khi đã mở kín, đổi nhanh/quay lại hàng cũ, rê cùng hàng, reenter cùng/khác giảng viên trong exit. Kiểm tra nét chữ, cache, bảng-only, input, sort/cuộn và lifecycle vẫn qua. Reviewer không có lỗi cần sửa thêm. GIF/kết quả và giới hạn kiểm chứng ở [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md).

Kết quả trước đó sau màu loang từng ô khoa và sửa chữ hover:

- Build riêng `.tmp/faculty-spread/final-build`: **0 lỗi, 0 cảnh báo**, **95/95** kiểm tra native riêng qua ở 120 DPI. Đã RED cho chữ phủ kín/zoom cuối, phạm vi hover của khung ngoài và hover con trong entrance rồi GREEN sau sửa. Có kiểm tra chữ của cả thống kê/ô khoa, vùng lân cận, cache, dữ liệu sống và lifecycle. Reviewer cuối không còn finding cần sửa. Ảnh/GIF/kết quả và giới hạn kiểm chứng ở [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md); không chạy lại toàn UI ngoài phạm vi.

Kết quả trước đó sau sửa chữ mờ trong preview:

- Build riêng `.tmp/lecturer-preview-text/final-build`: **0 lỗi, 0 cảnh báo**, **52/52** kiểm tra native riêng qua. Kiểm tra pixel đã RED với renderer cũ rồi GREEN khi chữ chép nguyên pixel; có kiểm tra khung nhỏ, cache, opacity và raster DPI. Đã sửa hai vấn đề bố cục nhỏ từ review: tên compact chồng khoa và tile chữ vượt góc bo. Reviewer không còn lỗi nghiêm trọng. Ảnh/GIF và kết quả riêng đã cập nhật; phương pháp, số đo và giới hạn ở [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md). Không chạy lại toàn UI ngoài phạm vi preview.

Kết quả trước đó sau giới hạn Interactive List Preview chỉ Bảng:

- Build riêng `.tmp/lecturer-list-preview/table-only-green-build`: **0 lỗi, 0 cảnh báo**, **46/46** kiểm tra native riêng qua sau giới hạn preview chỉ Bảng. Đã RED với bản mở preview ở Thẻ, rồi GREEN sau bỏ binding card/grid; có kiểm tra thẻ tạo lại, bảng ẩn, đổi chế độ, click/Enter và lifecycle. Reviewer không có finding mới. Kiểm thử thuần lượt triển khai trước **219/219** qua; helper không đổi trong lượt thu hẹp phạm vi. Preview, coverage và giới hạn xác minh ở [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md).

Kết quả lượt trước sau zoom nhẹ đồng thời màu lan:

- Build riêng `.tmp/auth-smooth-zoom/final-build`: **0 lỗi, 0 cảnh báo**, **185/185** kiểm thử thuần và **66/66** kiểm tra native riêng qua. Zoom có kiểm tra đơn điệu/đảo nhanh/qua child; cache chữ đã dùng renderer AntdUI thật để tránh đổi nét. Reviewer độc lập không có finding CRITICAL/HIGH. Preview và `auth-card-results.json` cập nhật theo bản cuối. Coverage helper và giới hạn capture/full UI tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md).

Kết quả lượt trước sau đổi màu vòng lan Theo khoa:

- Build riêng `.tmp/faculty-fill/green-build`: **0 lỗi, 0 cảnh báo**; **62/62** kiểm tra native riêng qua. PNG/GIF và `auth-card-results.json` cập nhật theo màu mới. Chỉ đổi màu khung ngoài, không đổi logic hoặc màu từng khoa; không chạy lại kiểm thử thuần cho thay đổi màu. Chi tiết tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md).

Kết quả lượt trước sau bỏ phóng/thu khi hover:

- Build riêng `.tmp/auth-no-scale/green-build`: **0 lỗi, 0 cảnh báo**, **176/176** kiểm thử thuần và **62/62** kiểm tra native riêng qua. Assertion giữ nguyên kích thước phần vẽ đã RED với bản zoom rồi GREEN sau bỏ; pixel ngoài thẻ không đổi. Preview và `auth-card-results.json` cập nhật theo bản mới. Chi tiết tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md).

Kết quả lượt trước sau Auth Form:

- Build output riêng `.tmp/auth-card-motion/build`: **0 lỗi, 0 cảnh báo**; kiểm thử thuần **176/176 qua**.
- Harness riêng Auth Card/hover khoa/clock: **62/62 qua**, [auth-card-results.json](QuanLyGiangVien/docs/screenshots/auth-card-results.json). Coverage helper mới 100% dòng/nhánh, không phải toàn ứng dụng. Chi tiết RED/GREEN, phương pháp và giới hạn trong [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md).
- Review đã xử lý vấn đề scroll extent và padding che nội dung cạnh thẻ; không còn finding CRITICAL/HIGH/MEDIUM trong phạm vi sửa. Không chạy lại toàn UI do select chập chờn đã ghi ở lượt trước; kết quả 62 kiểm tra riêng không thay trạng thái toàn UI.

Kết quả lượt trước sau tăng tốc độ hero:

- Build tại đường dẫn mới: **0 lỗi, 0 cảnh báo**, output kiểm chứng `.tmp/hero-speed/green-build` để giữ phiên người dùng.
- Kiểm thử thuần: **146/146 qua**.
- Harness riêng hero/render/clock: **42/42 qua**, `.tmp/hero-speed/ui-green/results.json` có `passed: true`. Harness toàn UI ở lượt này bị dừng tại kiểm tra select; không báo toàn bộ UI GREEN. Lượt select trước từng qua **129/129**. Chi tiết và log xem [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md).
- Coverage helper select mới: **100% dòng/nhánh**, không phải coverage toàn ứng dụng; phương pháp và log tại [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md).
- Coverage domain đo ở lượt trước: **97,67% dòng, 82,43% nhánh**. Đây không phải coverage toàn ứng dụng. Chi tiết phương pháp trong [UI_VERIFICATION.md](QuanLyGiangVien/docs/UI_VERIFICATION.md).
- Các GIF/capture native và benchmark mô tả hiệu ứng/chi phí render trong môi trường kiểm tra, không chứng minh FPS màn hình hoặc độ mượt trên mọi máy.

Ảnh quan trọng:

- [Toàn trang](QuanLyGiangVien/docs/screenshots/01-cards.png).
- [Animation toàn trang](QuanLyGiangVien/docs/screenshots/16-page-motion.gif).
- [Hover khoa](QuanLyGiangVien/docs/screenshots/18-faculty-hover.png).
- [Nút thẻ/bảng chỉ icon](QuanLyGiangVien/docs/screenshots/19-view-toggle-icons.png).
- [Vòng tỷ lệ đã bỏ marker](QuanLyGiangVien/docs/screenshots/20-teaching-overview.png).
- [Animation select](QuanLyGiangVien/docs/screenshots/22-animated-select.gif).
- [Preview thông tin giảng viên trước khi gỡ — lịch sử](QuanLyGiangVien/docs/screenshots/29-interactive-list-preview.gif).
- [Auth Form — entrance và hover](QuanLyGiangVien/docs/screenshots/26-auth-card-motion.gif).

## Chuyển thư mục đã hoàn tất

- Chuyển app, tests và 5 thư mục nghiên cứu: `.tmp/animated-icon-research`, `hero-research`, `overview-motion-coverage`, `overview-motion-coverage-measured`, `page-motion-research`.
- Đã đối chiếu SHA-256 cho **1.045 tệp** khi chuyển; cập nhật lệnh README và đường dẫn trong script preview icon, rồi restore/build/test lại tại chỗ mới.
- Thư mục cũ `E:\AI Agents\visual\QuanLyGiangVien` chỉ còn `.vs/bin/obj` do Visual Studio giữ hoặc tạo lại. Không dùng các cache này làm source cho session mới; không tự đóng VS hoặc xóa chúng.
- Dự án QuanLyTacGia, QuanLyTacGiaDesign, study và các Markdown về tác giả vẫn ở `visual`, không thuộc app đăng ký tín chỉ hiện tại.
- App có đủ avatar cục bộ. Script tái tạo avatar có nguồn mặc định là sibling QuanLyTacGia; khi cần chép lại đúng bộ gốc, xem lệnh `--source-dir` trong `ASSET_SOURCES.md`. Không cần chạy script này để mở UI.
- Log/coverage cũ có thể còn đường dẫn `visual` vì là lịch sử. Không sửa hàng loạt hoặc hiểu chúng là phụ thuộc runtime.
- Các subagent của session cũ đã hoàn tất nhiệm vụ; ID của chúng không phải cấu hình dự án và không cần phục hồi trong session mới.
