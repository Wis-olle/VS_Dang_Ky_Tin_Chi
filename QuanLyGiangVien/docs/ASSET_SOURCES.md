# Nguồn thiết kế và tài nguyên

Nguồn và tài nguyên được cập nhật theo từng lượt bên dưới. Tài nguyên tải sẵn để giao diện chạy ngoại tuyến.

## Thời khóa biểu sinh viên — phối màu 15/10/2026

### Navigation theo form giảng viên — mới nhất

Thay active label/underline bằng pill từ cùng `AppTheme.Button` của trang giảng viên: radius 12, fill `AccentSoft`, chữ `PrimaryPressed`, icon lịch 14 logical px. Nút đặt trong header hiện có, không đổi nội dung bên dưới; nhãn Cổng sinh viên vẫn là thông tin vai trò.

Đã tìm và kiểm tra [calendar-days trên Lucide](https://lucide.dev/icons/calendar-days), [giấy phép ISC](https://lucide.dev/license), rồi tải đối chiếu [SVG tại commit đã đóng gói](https://raw.githubusercontent.com/lucide-icons/lucide/70562c1ee1c4fdcf736fe97bc893fb8511927934/icons/calendar-days.svg). SHA256 nguồn online trùng `Assets/student-timetable/calendar-days.svg`: `9330eb8d7c4989e97b6361314abbf02953243c602874725c9e58ceca00da3bd7`. Vì vậy dùng lại asset nguyên bản, không thêm dependency hoặc mạng runtime. Nguồn/license đã có trong manifest và `Lucide-LICENSE.txt`.

Ở bước tích hợp, AntdUI tô fill root SVG làm icon thành ô vuông; đã bọc nội dung bằng group `fill=none` và đặt stroke màu chữ để giữ icon nét viền. Đây là chỉnh markup khi render, không đổi file SVG gốc. [Ảnh header cuối](screenshots/student-nav-15/06-navigation.png); designer đã xem wide/compact và PASS.

Thanh tiêu đề cửa sổ và panel điều hướng phía trên hiện cùng dùng `AppTheme.Canvas` (`#FFEFEF`) theo phản hồi mới nhất; giữ nguyên khung và nội dung. Không thêm tài nguyên.

Phản hồi mới nhất: **ô lịch và nhãn phòng dùng tím lilac, không dùng cam**. Màu riêng cho thẻ buổi học: selected/pill `#7662C9`, chữ trên pill chọn `#6551BE`, viền `#E2D6FC`, nền thường giữ cyan và hover sang `AppTheme.Lilac`. Chữ trắng trên tím khoảng 4,82:1. Các màu chung bên dưới vẫn áp dụng cho header và phần còn lại của form. Không thêm nguồn/asset hoặc đổi thiết kế.

Theo yêu cầu mới, chỉ đổi màu theo giảng viên và lớp/phòng; giữ bố cục Alireza Attari. `TimetableStyle` dùng alias từ `AppTheme`: canvas `#FFEFEF`, ink `#211A1C`, muted `#6E6267`, line `#EEDADD`, primary/selected/pill `#D44423`, emphasis `#A43019`, soft `#F6D9D5`, trạng thái xanh `#286644`. Thẻ chào peach `#FCCAC1`, lịch sắp tới cyan `#E4F5F7`, học kỳ lilac `#F6EDFE`; các khung còn lại trắng. Buổi thường cyan, hover nội suy đến soft; thời lượng giữ nguyên. Chữ trắng trên primary tương phản khoảng 4,52:1.

Không tải hoặc tạo hình mới. Icon mũ dùng SVG Lucide gốc hiện có, render một lần vào cache 64×64 theo `AppTheme.Primary` bằng AntdUI; vẫn là PictureBox tại cùng bounds. Minh họa dùng `Assets/scheduling/resource-visuals/coding.png` đã phối màu trong trang lớp/phòng: SVG gốc có SHA256 `9e6585f30ccff2fee6ef34a9139a53b9eef2487a3b2472be29edb7000479ec3c`, giống nguyên byte với `Assets/student-timetable/programming.svg`. Vì vậy hình và nguồn giữ nguyên, chỉ dùng phiên bản màu hiện có. Nguồn/license/biến đổi chi tiết của bản coding ở `Assets/scheduling/resource-visuals/Sources.json`; PNG cũ và script export ban đầu được giữ làm bản tham chiếu, không dùng cho hai slot này trong runtime mới.

Giữ font, avatar, renderer/clock, vùng bấm và animation. Snapshot native trước/sau xác nhận bounds/font/text/tab/accessibility bằng nhau; xem [UI_VERIFICATION.md](UI_VERIFICATION.md).

### Bản ban đầu — 14/10/2026

Tham chiếu người dùng chọn: [Student Dashboard — Alireza Attari, shot8097669](https://dribbble.com/shots/8097669-Student-Dashboard). Đã xem ảnh đầy đủ của tác giả, không chỉ đọc palette. Mẫu là dashboard tiếng Ba Tư, bố cục RTL; bản tiếng Việt chuyển sang LTR, vẫn giữ thanh ngang trắng, vùng chính rộng và hồ sơ bên phải. Nhịp thẻ trắng, bo vừa, nền xám lavender, khoảng cách thoáng và một event tím nổi bật là các đặc trưng được áp dụng. Mẫu có bóng rất nhẹ; bản native dùng viền mờ/bo góc để giảm chi phí vẽ. Không đóng gói ảnh Dribbble vào runtime hoặc dùng logo/chân dung tác giả.

- Palette riêng của form sinh viên: canvas`#F6F6FA`, ink`#424550`, muted`#6F7586`, purple`#8E7BEA`, purple-dark`#6551BE`, selected-fill`#7662C9`, soft`#F0EDFC`, teal`#008D7C`, line`#EEEEF4`. Đây là lựa chọn triển khai dựa trên quan sát, không tuyên bố lấy được design tokens gốc.
- Calendar bảy cột ngày, hàng tiết1–12; sự kiện chọn tô tím, các sự kiện còn lại nền gần trắng với pill phòng tím. Hồ sơ/khối học kỳ/chi tiết ở cột phải; hàng trên có minh họa nhỏ và lịch gần nhất. Không đưa điểm/học phí/tin nhắn trong shot vào phạm vi sản phẩm.
- Font Plus Jakarta Sans/OFL và avatar Notionists/CC0 tái sử dụng bộ đã đóng gói; nguồn/license chuẩn ở các mục hiện có bên dưới.
- **Icon Lucide:** tải SVG từ [repo chính thức tại commit70562c1](https://github.com/lucide-icons/lucide/tree/70562c1ee1c4fdcf736fe97bc893fb8511927934/icons). [Giấy phép ISC + Feather MIT](https://lucide.dev/license) lưu nguyên byte trong `Assets/student-timetable/Lucide-LICENSE.txt`. Graduation-cap dùng PNG raster64px; chevron trái/phải dùng SVG trong nút native. Bộ icon học tập bổ sung được giữ cùng nguồn; không cần mạng khi chạy.
- **Minh họa Programming:** [unDraw](https://undraw.co/search/programming), [SVG chính thức](https://cdn.undraw.co/illustration/programming_j1zw.svg?type=svg), [giấy phép](https://undraw.co/license). SVG gốc và PNG trong `Assets/student-timetable/`; giữ màu/geometry gốc, chỉ raster bằng Sharp. Dùng trang trí trong sản phẩm; không phân phối thành bộ ảnh hoặc dùng huấn luyệnAI. Văn bản license được giữ cục bộ.
- **Motion:** [Transition Panel trên21st](https://21st.dev/@ibelick/components/transition-panel), đối chiếu [nguồn Motion Primitives](https://github.com/ibelick/motion-primitives/blob/120f64f6ca60348e251f929e9c81f11ccbe45eda/components/core/transition-panel.tsx). Bản C# dùng `SurfaceTransition` và clock chung hiện có: entrance320/360ms, chuyển tuần/học kỳ200ms, tint hover160ms, hitbox cố định. Thời lượng là lựa chọn desktop, không suy ra từ ảnh Dribbble. Nguồn/license MIT đã lưu tại `Assets/licenses/PageMotion-ReferenceSource/`, `Motion-Primitives-MIT.txt` và `Page-Motion-Sources.json`; không chạy React/WebView.

Sau designer review, nền event chọn và pill dùng`#7662C9` để chữ trắng đạt tỷ lệ tương phản xấp xỉ4,82:1; muted trên thẻ trắng dùng`#6F7586` khoảng4,60:1. Đây là kiểm tra cặp màu, không phải chứng nhận WCAG toàn ứng dụng.

Manifest URL/SHA256/biến đổi: [`Assets/student-timetable/Sources.json`](../Assets/student-timetable/Sources.json). Script`export_assets.cjs` raster lại từ SVG ngoại tuyến, dùng`sharp` qua`NODE_PATH`/`SHARP_MODULE_PATH`. Ảnh và giới hạn kiểm chứng xem [UI_VERIFICATION.md](UI_VERIFICATION.md).

## Trang gộp Lớp học & Phòng học

### Làm gọn Lớp trong tuần — lượt mới nhất 14/10

Bỏ minh họa `date-picker-pastel.png` khỏi header agenda theo yêu cầu người dùng. Tệp nguồn/giấy phép vẫn giữ trong lịch sử; runtime không tạo control hoặc tải ảnh này. Sửa nền ngoài đường bo góc của các dòng lớp để tránh góc đen khi repaint, giữ palette/chữ/badge/animation. Không thêm nguồn hoặc dependency; ảnh native và kiểm chứng tại [UI_VERIFICATION.md](UI_VERIFICATION.md).

### Làm gọn bốn thẻ — lượt trước 14/10

Theo ảnh chỉ định, bỏ icon heading và dòng trạng thái nhỏ dưới số trên cả bốn KPI. Tiêu đề căn trái18px cùng số/CTA; mũi tên lấy vị trí theo chiều rộng nhãn đã đo, gap6px và dịch thêm tối đa5px khi hover; hạ mũi tên2px để cân với chữ. Dòng trạng thái vẫn có trong mô tả accessibility, không đổi dữ liệu/ratio/callback. Bỏ control/icon motion và font của note khỏi thẻ; giữ asset/giấy phép cũ cho lịch sử và các phần khác.

Giữ nguyên bốn ảnh màu, vùng ảnh140/112px/đệm10px và renderer chống mờ/cắt góc của bản người dùng chọn. Không có tài nguyên hoặc dependency mới, không sửa giao diện giảng viên. Ảnh rộng/compact và kiểm chứng tại [UI_VERIFICATION.md](UI_VERIFICATION.md).

### Sửa hover mờ và cắt góc — lượt trước 14/10

Giữ cả bốn PNG màu gốc của 3dicons V1 và giấy phép/manifest cũ. Sửa cách vẽ: cache alpha-crop ở độ phân giải gốc, không thu nhỏ thành thumbnail rồi phóng lại khi hover; GDI+ vẽ một lần từ bitmap đó bằng HighQualityBicubic với TileFlipXY và tọa độ float. Cache dùng lại trong chuyển động, không tạo bitmap mỗi frame.

Vùng ảnh nằm trong thẻ, đệm phải/dưới10px, rộng140px ở bản rộng và112px ở compact. Kích thước vẽ chừa đủ vùng bao cho zoom1.1, xoay3° và dịch10px trong400ms; giữ chữ/nút/hitbox cố định. Không sửa ảnh nguồn, bảng màu hoặc giao diện giảng viên. Ảnh native lúc zoom tối đa, kiểm tra pixel cạnh và giới hạn kiểm chứng tại [UI_VERIFICATION.md](UI_VERIFICATION.md).

### Ảnh Service Card có màu — lượt trước 14/10

Theo yêu cầu thay các khối trắng, bốn thẻ dùng các PNG màu nguyên bản từ [3dicons V1 của Vijay Verma](https://3dicons.co/collection/b68cf8-v1): Notebook màu tím cho lớp, Key gradient tím–hồng–cam cho phòng, Calender đỏ cho lịch và Chart tím–cam cho đăng ký. Notebook/Calender/Chart dùng variant `color`; Key dùng `gradient` vì bản `color` của tác giả vẫn chủ yếu là màu kim loại. Đã xem ảnh gốc và đối chiếu URL trong metadata tác giả pinned ở commit `8884d59e68a7bae0e0e2163af5b6c2ad992c01c8`.

Giấy phép vẫn là CC0 của V1. [Manifest](../Assets/scheduling/service-cards/Sources.json) ghi variant, URL, bytecount và SHA256; runtime PNG giống nguyên byte với master, không recolor. Bốn master clay trước được giữ cùng nguồn trong `history-clay/`. Chart là hình trang trí, không biểu diễn tỷ lệ đăng ký thực tế. Bố cục Service Card, palette AppTheme, icon, CTA và chuyển động giữ nguyên; không sửa code runtime hoặc giao diện giảng viên. Ảnh native rộng/compact và kiểm chứng màu tại [UI_VERIFICATION.md](UI_VERIFICATION.md).

### Service Card theo mẫu người dùng chọn — lượt trước 14/10

Người dùng chọn [Service Card — LaviKatiyar trên 21st](https://21st.dev/@lavikatiyar/components/service-card) và cung cấp source React. Đã xem preview thật cùng source: title phía trên/trái, CTA phía dưới/trái, vật thể3D ở góc phải/dưới được crop, hover ảnh scale1.1/rotate3°/x10 trong400ms và shadow300ms. C# tự triển khai pattern cho KPI hiện có; không tạo project React, không cài Tailwind/shadcn hoặc thêm dependency runtime. Trang listing đã xem không nêu license component; không gán MIT cho nó. Bản user-provided được giữ làm reference, không chạy trong app.

Bộ ảnh là **3dicons V1 của Vijay Verma/realvjy**, bản clay: Notebook→lớp, Key→phòng, Calender→lịch, Chart→đăng ký. Đã xem từng ảnh trên trang chính thức và đối chiếu [collection V1](https://3dicons.co/collection/b68cf8-v1), [About](https://3dicons.co/about), [LICENSE CC0 pinned](https://github.com/realvjy/3dicons/blob/8884d59e68a7bae0e0e2163af5b6c2ad992c01c8/LICENSE). [Manifest và PNG](../Assets/scheduling/service-cards/Sources.json) giữ URL gốc, metadata tác giả, master400×400, bản runtime nguyên byte, CC0, bytecount/SHA256 và source component người dùng gửi. Không recolor; alpha bounds chỉ crop khi vẽ để vật thể đủ lớn. Hình Chart là trang trí cố định, không dùng lát pie của ảnh để biểu diễn64%.

Thiings trong demo đã được xem nhưng không dùng: [điều khoản hiện hành](https://www.thiings.co/terms) yêu cầu visible attribution cho tải miễn phí và giới hạn non-commercial; không gọi bộ đó là CC0 hoặc suy ra quyền từ demo21st. Bộ3dicons V1 không yêu cầu attribution trong UI; nguồn/giấy phép vẫn giữ cục bộ.

Bốn card dùng toàn hàng rộng, cao180px trong hàng188px, gap12px; card đầu coral từAppTheme và chữ trắng, ba card còn lại dùng palette pastel hiện có. Icon Lucide đã đóng gói tiếp tục dùng ở heading; CTA native đi tới tab0/1/2/0 (lớp/phòng/lịch/bảng sĩ số), không thêm nghiệp vụ đăng ký. Nhịp học tuần giữ control/UI và số liệu, chuyển xuống cột bên phải workspace; tab lịch giữ calendar toàn chiều rộng nên mini chart ở sau calendar. Native giữ card/chữ/hitbox cố định: ảnh transform400ms, viền/arrow300ms thay vì scale cả card và shadow; arrow không lặp Infinity. PNG dùng alpha gốc và opacity1 để giữ màu clay, không chuyển90→100 như demo. Motion/cache/disposal và ảnh native được ghi tại [UI_VERIFICATION.md](UI_VERIFICATION.md).

### Thiết kế lại riêng bốn KPI — lượt trước 14/10

Sau ảnh chỉ định mới, chuyển bốn KPI sang card trắng phẳng, icon cạnh nhãn, giá trị bên trái và vùng tỷ lệ riêng bên phải. Tham khảo ảnh thật [Stat Cards 15250082](https://dribbble.com/shots/15250082-Stat-Cards) và [Stat Cards 15332259](https://dribbble.com/shots/15332259-Stat-Cards) của Jon Moore/Innovatemap: học phân cấp icon–label–value và tách vùng dữ liệu/biểu đồ. Không tải hoặc tái sử dụng media Dribbble; không giả định giấy phép asset cho shot.

Pattern ratio lấy từ [Blocks.so Stats](https://blocks.so/stats) / [Stats cards trên 21st](https://21st.dev/@ephraimduncan/components/stats-cards-with-links/stats-cards-with-circular-progress): Stats07 circular và Stats13 segmented progress. [Manifest](../Assets/scheduling/kpi-sources/Sources.json) giữ TSX tham khảo và MIT ©2025 Ephraim Duncan, pin commit `3f759d5171c2391253ec4c772e9e5b3db2bc7c7f`, bytecount/SHA256. Runtime là C# native, không chạy TSX hoặc thêm dependency. Icon Lucide Animated/ISC cũ tiếp tục dùng từ gói motion đã lưu.

Card vẫn cao116px/hàng124px/gap8, bo14px; icon28px ở x12/y10, caption x48, số có cùng vùng y43–88 và fit GDI từ24pt. Footer là dải pastel phẳng y88–115, note8pt căn giữa. Ba chỉ số đầu dùng matrix2×4 segment kèm chữ75%/88%/86%; đây là thang tỷ lệ0–100% liên tục, **không phải tám bản ghi**. Ô đăng ký giữ vòng42px cho225/350, chữ64% làm tròn. Ratio, dữ liệu và animation650/180ms giữ nguyên; accessibility mô tả rõ mẫu số của từng tỷ lệ. Nhịp học tuần, Page layout, domain và giảng viên không sửa trong lượt này. Kiểm chứng tại [UI_VERIFICATION.md](UI_VERIFICATION.md).

### Dải thống kê và Nhịp học tuần — lượt trước 13/10

Theo hai ảnh người dùng chỉ định, lượt này chỉ thiết kế lại overview và mini chart. Đã tìm/xem [Statistics Cards — Nick Deva trên Dribbble](https://dribbble.com/shots/26372938-Statistics-Cards), [Number Ticker — Fancy Components trên 21st](https://21st.dev/@danielpetho/components/basic-number-ticker), demo [Analytics stats cards](https://preview.21st.dev/@danielpetho/components/basic-number-ticker/analytics-stats-cards) và [Bar Chart — Kevin Girelli trên 21st](https://21st.dev/@kevingirelli/components/bar-chart). Học phân cấp caption/icon → số lớn → thông tin phụ, wash pastel dưới thẻ, count-up hữu hạn, cột bo phía trên và hover/focus/keyboard. Không lấy ảnh chụp hoặc artwork Dribbble; demo stats dùng làm tham khảo bố cục, không chép code/media.

Bốn tile riêng bo12px, gap8px, cao116px trong hàng124px; nền trắng chuyển pastel rất nhẹ, viền mảnh. Caption8.5pt, số mục tiêu24pt đo/fit GDI, footer8pt; icon Lucide Animated28px cũ tiếp tục dùng. Ba rail4px lần lượt lấy `OpenClasses/TotalClasses`, `AvailableRooms/TotalRooms`, `ScheduledClasses/NonCanceledClasses`; vòng42px lấy `Enrolled/Capacity`, còn chữ phần trăm là `FillPercent` được làm tròn. Mẫu hiện tại tương ứng6/8,7/8,6/7 và225/350, không phải số tăng trưởng. Mẫu rỗng trả tỷ lệ0. Cập nhật số/ratio650ms, hover180ms trên clock chung; retarget từ giá trị đang vẽ, hidden/reduced/dispose kết thúc job.

Chart cao108px bên trong surface124px padding8px; badge tổng buổi, guides/baseline nhẹ, bảy cột rộng18–22px, nhãn ngày/số và tooltip ngày cụ thể. Thang đo `max(1, số buổi cao nhất)` thay floor4 cũ; tuần mẫu2/2/2/1/1/0/0 vì vậy có cột đủ cao, ngày0 không có fill. Giá trị/thang đo nội suy500ms, dữ liệu không đổi không restart; hover180ms, bàn phím ←/→/Home/End, today marker lấy ngày thật. Surface workspace lùi từ114 tới138px để chừa hàng overview lớn hơn; các vùng còn lại giữ nguyên.

Native C# chart được chuyển từ pattern source [Corsair UI](https://github.com/KevinGirelli/corsair-ui/blob/b21f13922504933eaf6b2bb174cad789d46c3acb/registry/default/ui/bar-chart.tsx), commit `b21f13922504933eaf6b2bb174cad789d46c3acb`, MIT ©2026 Kevin Girelli. [Manifest](../Assets/scheduling/metrics-sources/Sources.json) giữ source TSX tham khảo, MIT đầy đủ, URLs và SHA256; TSX không chạy trong app, không thêm React/dependency/network runtime. Count/ratio vẫn dùng `Ui.Motion` native đã có. Nguồn icon/ảnh trước giữ nguyên; không sửa UI giảng viên/domain. Ảnh, chuyển động và giới hạn kiểm chứng tại [UI_VERIFICATION.md](UI_VERIFICATION.md).

### Minh họa theo môn học và loại phòng — lượt trước 13/10

Sau phản hồi còn đơn điệu, đã dùng bảy minh họa có ngữ cảnh từ [unDraw](https://undraw.co/illustrations): Programming, Finance, Books, Electricity, Teaching, Server và Lecture. Hình xuất hiện thường xuyên trong dữ liệu: thumbnail 48×36px trong bảng lớp/phòng, cover 68px ở inspector theo khoa/loại phòng, và thumbnail 24×24px ở hàng mã lớp trên lịch tuần. Không dùng ảnh như avatar giảng viên hoặc ảnh chụp phòng thật; mapping lấy từ `Course.Faculty` và `Room.Type` hiện có, không đổi record/domain.

Đã mở và xem hình ở gallery/modal chính thức trước khi đóng gói. [Manifest mới](../Assets/scheduling/resource-visuals/Sources.json) giữ URL thực của SVG, tên tác giả/giấy phép, SVG nguyên bản, bản thay màu và PNG trong suốt 512px; có bytecount/SHA256 và mapping màu. Books tái dùng nguồn đã xác minh từ lượt trước. Runtime dùng PNG/cache cục bộ, không network/dependency mới. Giấy phép unDraw là giấy phép riêng, cho dùng/sửa trong sản phẩm miễn phí/no attribution; không gán MIT/CC0 cho ảnh.

Bảng thêm cột hình 62px, hàng 56px; nội dung/nút thao tác giữ nguyên. Inspector cao466px thay386px và dịch nội dung80px để giữ body lịch tối thiểu80px. Lịch tuần đặt mã lớp và ảnh ở hàng riêng, tên môn dùng toàn chiều rộng, phòng/tiết ở dưới; đo header/title/metadata native cùng renderer để tránh ảnh làm tên bị chia thành từng từ. Ảnh được mượn từ catalog do page sở hữu; thumbnail cache theo size/DPI, giải phóng sau child controls. Nguồn/code giảng viên không sửa.

Đã xem ManyPixels nhưng không dùng bộ đó trong bản này; tài nguyên cuối cùng đều lấy từ unDraw có nguồn/giấy phép lưu cục bộ. Ảnh/kiểm chứng mới tại [UI_VERIFICATION.md](UI_VERIFICATION.md).

### Tài nguyên và chuyển động bổ sung — 13/10/2026

Người dùng đã mở rộng yêu cầu tìm kiếm trên toàn mạng; Dribbble, 21st, Awwwards và Codrops là nguồn ưu tiên. Bố cục hiện tại vẫn giữ bảng màu giảng viên; chỉ bổ sung vào scheduling, không sửa giảng viên/domain và không thêm sidebar/hero/avatar.

| Tài nguyên / nguồn | Áp dụng trong form |
|---|---|
| [Lucide Animated](https://lucide-animated.com/) và [Lucide ISC](https://lucide.dev/license) | Bốn chip icon 28px ở overview, nét 20px; hover phát hữu hạn. Sách/phòng dùng keyframe `book-text` 600ms trên geometry SVG hiện có; lịch dùng opacity sáu dot 900ms, nhóm người dùng companion path/spring 1.100ms. Geometry/chuyển động và giới hạn adaptation được ghi riêng trong manifest |
| [unDraw Books](https://undraw.co/search/study), [Date Picker](https://undraw.co/search/calendar) | Sách 112×76px ở trạng thái không có kết quả; lịch 54×38px cạnh heading agenda. Chỉ thay màu sang palette AppTheme, giữ geometry/alpha. Giữ SVG gốc/biến thể; runtime dùng PNG trong suốt được rasterize bằng sharp/libvips để tránh lỗi parser SVG theo culture. Ảnh được scale giữ tỷ lệ và cache theo size/DPI |
| [SmoothUI Animated Tabs trên 21st](https://preview.21st.dev/%40educalvolpz/components/animated-tabs), [tài liệu tác giả](https://smoothui.dev/docs/components/animated-tabs) | Icon 14px trên tab và underline trượt vị trí/chiều rộng 220ms EaseOut, retarget từ rect float hiện tại. Live source spring 250ms/bounce 0.05 là nguồn tham khảo; thời lượng native là adaptation. Button không dịch bounds; chỉ tab đang chọn là TabStop |
| [SmoothUI Animated Progress Bar](https://smoothui.dev/docs/components/animated-progress-bar) | Thanh sĩ số nội suy giá trị thật trong 360ms; crossfade chỉ vùng lịch để không che animation fill. Ẩn/dispose/reduced motion kết thúc job |
| [Codrops Icon Hover Effects](https://tympanus.net/Development/IconHoverEffects/#set-1) | Đã xem demo fill/color/ring 200ms, học cách đặt icon chip pastel và phản hồi hover hữu hạn. Không lấy EcoIco font hoặc source code; glyph motion dùng Lucide Animated |
| [Awwwards Hover animation](https://www.awwwards.com/inspiration/hover-animation-4) | Đã xem video menu underline/typography, dùng làm tham khảo trạng thái điều hướng rõ. Không lấy video/media hoặc suy ra quyền tái sử dụng |

[Manifest icon và TSX/license](../Assets/scheduling/motion/Sources.json), [manifest minh họa và SHA256 SVG/PNG](../Assets/scheduling/illustrations/Sources.json), [manifest interaction](../Assets/scheduling/interaction-sources/Sources.json) chứa nguồn chi tiết. unDraw cho dùng/sửa trong sản phẩm cá nhân/thương mại, miễn phí và không yêu cầu attribution; giấy phép đầy đủ được giữ cục bộ, không chuyển thành MIT/CC0. SmoothUI MIT được lưu riêng. Không đưa media Dribbble/Awwwards vào app.

Runtime ngoại tuyến, không thêm dependency. Các motion mới dùng `Ui.Motion`/clock chung, không ambient loop; bitmap/layer cache được tái dùng mỗi frame và giải phóng khi đổi size/DPI/dispose. Nguồn/timing của các lượt trước bên dưới là lịch sử; giới hạn cũ “chỉ hai shot” đã được yêu cầu mới này thay thế. Kiểm chứng và ảnh tại [UI_VERIFICATION.md](UI_VERIFICATION.md).

Yêu cầu cuối ngày 12/10/2026: chỉ giữ mã màu AppTheme và Plus Jakarta Sans hiện có của trang giảng viên; scheduling có bố cục vận hành riêng. Header gọn chứa breadcrumb/title, nút quay lại Giảng viên, học kỳ và CTA theo tab. Không sidebar, campus./Minh/avatar, hero gradient hoặc minh họa Overflow trong module hiện hành. MainForm, Ui và Panels giảng viên không thay đổi. Các mục thiết kế/asset giảng viên và lịch sử bên dưới không phải nguồn thiết kế mới của scheduling.

### Bố cục trước khi bổ sung asset — hai nguồn ngày 12/10

Đã đối chiếu trang tác giả và xem ảnh thật bằng browser ngày 12/10/2026; lượt này chỉ lấy pattern từ đúng hai shot dưới đây, không dùng Solomiia hoặc nguồn thiết kế mới khác.

| Nguồn | Pattern áp dụng vào WinForms |
|---|---|
| [Class Management Dashboard Design — Sakib N](https://dribbble.com/shots/16464251-Class-Management-Dashboard-Design) | Dải bốn chỉ số có separator; chia vùng nội dung chính và lịch lớp bên phải; mỗi vùng có heading riêng. Agenda dùng cấu trúc Today's Classes nhưng nội dung là các buổi học thật của module |
| [Class Panel — Dashboard SaaS UI Design Concept — UIX Alii](https://dribbble.com/shots/26840183-Class-Panel-Dashboard-SaaS-UI-Design-Concept) | Thứ tự label/value/footer trong overview; heading/CTA gọn; search/filter sát bảng, hàng dữ liệu có status/actions rõ. Đã xem cả ảnh dashboard và slide Students/table trong cùng shot |

Không lấy sidebar, banner nâng gói, Students/Payments/Attendance/Settings hoặc số tăng trưởng/attendance giả từ mẫu. Calendar dùng bảy ngày và occurrences của module, không thêm chức năng domain hoặc tab mới. Không sao chép ảnh chụp, artwork, avatar, media hay mã nguồn từ shot. Hai trang không cung cấp giấy phép tái sử dụng media/mã rõ ràng trong nội dung đã xem; không gán MIT/ISC cho chúng. Cả hai mẫu là ảnh tĩnh, không phải bằng chứng animation của tác giả.

### Palette và bố cục hiện hành

`SchedulingTheme` alias AppTheme: Canvas `#FFEFEF`, Ink `#211A1C`, Muted `#6E6267`, Line `#EEDADD`, Primary coral `#D44423`, AccentSoft `#F6D9D5`, Cyan `#E4F5F7`, Peach `#FCCAC1`, Lilac `#F6EDFE`, Mint `#E9F9EF`. Không dùng mã blue/dark/lavendergray trong hai shot. Plus Jakarta Sans là font đã có, giấy phép tại mục Font bên dưới.

Lượt làm mềm sau phản hồi “nhìn hơi thô” vẫn dùng đúng hai nguồn trên: giảm độ nặng chữ, bỏ lưới ô của bảng, phân cấp thông tin bằng màu Muted, tăng khoảng thở và bo góc. Header x28/y8/h70 nằm ngoài vùng cuộn; content x28/y84, width W−56/height H−108 và reserve gutter trước khi chia cột. Header 18pt, heading workspace 12pt; body bảng 9pt, header bảng 8.5pt regular, hàng 50px và cột thao tác 96px căn trái để chừa scrollbar. Surface overview/agenda bo18px, overview100px chứa bốn control96px: nhãn trên, số, footer dưới, separator nhạt và dot pastel6px. Số bắt đầu từ mục tiêu24pt, đo TextRenderer/GDI và fit/cache theo vùng chữ/DPI. Mini chart cùng hàng hiển thị bảy số buổi thật; cột tối đa14px, số cùng baseline và track nhạt.

Workspace bắt đầu tại y114 trong content, padding24px. Bảng lớn bên trái, inspector320px ở bản rộng/300px ở compact bên phải, gap18; vẫn giữ hai cột ở1100px. Inspector386px bo18px không viền: ID/status đo rộng theo chữ, tên học phần/phòng12pt tối đa hai dòng, facts gọn, sĩ số/sức chứa, row44px trắng không viền với marker pastel. Mọi lịch/lớp liên quan xem bằng native scroll, footer9pt cố định. Agenda202px dưới inspector, gap18, có ba preview buổi thật với ô thứ pastel bo8px; toàn bộ buổi xem trong tab Lịch tuần. Calendar fullwidth có các cột ngày trắng, separator nhẹ và thẻ pastel bo12px; SessionButton kế thừa Button native, đo/vẽ riêng title9pt bold và metadata8pt để giữ học phần/mã/phòng/tiết. Tabs8.5pt regular, selected bold và underline coral ngắn theo nhãn, đầu tròn.

Đã đối chiếu ảnh native cuối ở96DPI `01-classes`, `02-rooms`, `03-week`, `08-compact`, `11-compact-detail`, `13-compact-week` trong `docs/screenshots/scheduling-polish-12/`: số KPI đủ glyph, tên học phần/khoảng tiết đầy đủ, nút thao tác có gutter trước scrollbar và header giữ nguyên khi cuộn; bản compact1100×720. Lịch từng ngày dùng native scroll khi nội dung dài hơn viewport. Kiểm chứng/DPI tại [UI_VERIFICATION.md](UI_VERIFICATION.md); ảnh của bố cục trước nằm trong `scheduling-distinct-12/`.

### Tài nguyên của bản ngày 12/10 và lịch sử

Module ở bản ngày 12/10 vẽ separator, marker, chart và progress bằng C# native theo các pattern trên; icon thao tác nhỏ dùng AntdUI đã có như dependency kỹ thuật. Lượt đó không thêm asset hoặc media ngoài hai shot. Tài nguyên mới ngày 13/10 được ghi ở đầu mục này. Overflow/Notionists vẫn phục vụ giảng viên nhưng không render trong scheduling; nguồn và giấy phép của chúng được giữ tại các mục riêng bên dưới.

Bộ SVG `Assets/scheduling/{book-open,door-open,calendar-days,users-round,layout-dashboard}.svg` được giữ làm lịch sử các bản trước, hiện không render trong overview/tabs. Nguồn chính thức [lucide-icons/lucide](https://github.com/lucide-icons/lucide/tree/70562c1ee1c4fdcf736fe97bc893fb8511927934/icons), commit `70562c1ee1c4fdcf736fe97bc893fb8511927934`; nguyên byte và `fill="none"` vẫn giữ. [Giấy phép chính thức](https://lucide.dev/license) là ISC kèm thông báo MIT cho hình kế thừa Feather. Văn bản đầy đủ tại [Lucide-LICENSE.txt](../Assets/scheduling/Lucide-LICENSE.txt), URL raw/bytes/SHA256 tại [Sources.json](../Assets/scheduling/Sources.json); không đổi hoặc xóa manifest/giấy phép và không gán chúng cho Dribbble. Helper tô outline đã có là lịch sử triển khai, không nguồn thiết kế mới.

### Chuyển động WinForms thực tế

| Vùng | Hành vi |
|---|---|
| Số overview | Count-up hữu hạn 650 ms khi xuất hiện/cập nhật; glyph được đo/cache bằng renderer native theo DPI |
| Overview và preview agenda | Hover tint hữu hạn 180 ms, bounds/hitbox giữ nguyên |
| Mini chart | Chuyển dữ liệu 500 ms từ occurrences tuần thật |
| Các surface nội dung | Reveal 360 ms theo thứ tự |
| Tabs | Underline chọn bằng native button, giữ keyboard/focus; không Segmented pill |
| Inspector lớp/phòng | Crossfade 200 ms; native scroll và footer cố định |

Các thời lượng là lựa chọn triển khai C# hiện có, không animation/media/mã lấy từ hai ảnh tĩnh. Job dùng Motion và clock UI chung PageMotionCoordinator, không ambient loop; cache/font/bitmap/job được giải phóng theo lifecycle. Nguồn Framer/21st/Codrops/Awwwards của lượt nghiên cứu trước là lịch sử, không nguồn thiết kế/motion mới cho revision này. Manifest trước vẫn giữ tại [Page-Motion-Sources.json](../Assets/licenses/Page-Motion-Sources.json) và các mục lịch sử bên dưới; không thêm nguồn hoặc giấy phép mới. Kiểm chứng và giới hạn phép đo tại [UI_VERIFICATION.md](UI_VERIFICATION.md).

## Mẫu thiết kế hiện tại

- **Student Dashboard UI Design for EdTech Platform**: https://dribbble.com/shots/27591274-Student-Dashboard-UI-Design-for-EdTech-Platform
- Áp dụng canvas hồng nhạt `#FFEFEF`, thẻ trắng, trạng thái điều hướng `#F6D9D5`, điểm nhấn coral `#FE6B4A`, gradient tím nhạt `#E2D6FC` → đào `#FCCAC1`, mint `#E9F9EF` và lilac `#F6EDFE`; avatar hiện tại dùng đúng bộ 48 Notionists từ QuanLyTacGia, nét đen trắng trên 5 nền pastel.
- Nút CTA trong ảnh mẫu có màu gần `#F14D28`; bản WinForms dùng `#D44423` đậm hơn để chữ trắng đạt độ tương phản khoảng 4.52:1. Dòng ký hiệu `#FF8798H` nhìn thấy trong style guide của ảnh mẫu không phải mã HEX hợp lệ nên không sử dụng.
- Link Dribbble là tài liệu tham khảo bảng màu và cách trình bày. Không đưa artwork hoặc ảnh chụp màn hình của tác giả vào app.
- Bố cục hiện tại theo ba hàng của Student Dashboard: điều hướng trên nền blush; banner gradient và card tỷ lệ; ba thống kê và phân bố khoa; danh sách trắng và hồ sơ bên cạnh. Thẻ giảng viên cao 220px, avatar 64px. Card tỷ lệ và thống kê lấy từ dữ liệu thật trong phiên, không dùng biểu đồ mẫu.
- Bố cục bản đầu tham khảo **Teacher Dashboard Design — Nixtio**: https://dribbble.com/shots/22377090-Teacher-Dashboard-Design ; bản hiện tại đã chuyển bố cục sang Student Dashboard mới.

## Minh họa 2D: Overflow Design — Office Club

- **On The Laptop**, bản **Regular**, nguồn chính thức: https://www.overflow.design/notion-illustrations/office-club/ . Asset được đánh dấu **Free** trong bộ Office Club. Tải SVG gốc qua MCP công khai của nhà cung cấp; không lấy ảnh preview để thay nguồn vector.
- Minh họa có nét đen mảnh, tóc/laptop/quần đen, gần phong cách Notionists hơn banner Storyset trước đó. Mặt và áo không có fill trắng trong SVG gốc; chúng giữ nền trong suốt và hiện màu gradient lavender–peach của UI. Artwork được giữ nguyên.
- Asset gốc: `Assets/illustrations/on-the-laptop.svg`. Runtime dùng PNG RGBA 1063×1011 `Assets/illustrations/on-the-laptop.png`, chuyển định dạng bằng Sharp/librsvg, cắt lề alpha và thêm 20px đệm trong suốt mỗi cạnh. Script tái xuất: `Assets/illustrations/export_on_the_laptop.cjs`.
- Giấy phép: https://www.overflow.design/license/ , phiên bản ngày 29/09/2026, kiểm tra ngày 10/10/2026. Free items được dùng trong sản phẩm cá nhân/thương mại và trong repo công khai của ứng dụng khi là một phần của giao diện; không phân phối thành thư viện hoặc bộ asset riêng. Không bắt buộc attribution; nguồn **Overflow Design** được giữ trong tài liệu và manifest dù footer credit đã bỏ khỏi UI.
- Nguồn, SHA256 và thông tin chuyển định dạng: `Assets/licenses/Overflow-OfficeClub-Sources.json`; bản ghi quyền sử dụng: `Assets/licenses/Overflow-Design-License.txt`. Asset giữ giấy phép Overflow Design, không chuyển thành CC0 của avatar.
- App tải bitmap cục bộ, không gửi dữ liệu hoặc tải tài nguyên khi chạy. Các minh họa Storyset Teacher/Studying và manifest cũ được giữ làm lưu vết phiên bản trước, không được banner hiện tại sử dụng.

## Avatar minh họa 2D: Notionists — bộ QuanLyTacGia

- Bộ gốc **Notionists** của **Zoish**, nguồn tác giả: https://heyzoish.gumroad.com/l/notionists . Phiên bản DiceBear: https://www.dicebear.com/styles/notionists/ .
- Sao chép nguyên 48 PNG từ `../QuanLyTacGia/Assets/avatars/notionists-01.png` đến `notionists-48.png`, đổi tên thành `Assets/avatars/portrait-01.png` đến `portrait-48.png`. Giữ nguyên byte, nét vẽ, màu và kích thước 128×128 RGBA; SHA256 của từng ảnh khớp ảnh tác giả tương ứng. Không phóng ảnh hoặc tải lại mẫu khác khi áp dụng bộ này.
- Các ảnh gốc do QuanLyTacGia chuẩn bị bằng DiceBear API 9.x, seed `TG0001`…`TG0048`, 5 nền `fef3c7 / e3f1fa / ede9fe / fce7f3 / dcfce7`. Chỉ chọn seed và nền nên nét mặt, tóc và trang phục thay đổi giữa các ảnh.
- Tài nguyên được cung cấp theo **CC0 1.0**, xác nhận trên trang style chính thức; văn bản: https://creativecommons.org/publicdomain/zero/1.0/ . Nguồn Notionists vẫn được giữ trong tài liệu và manifest; footer liên kết đã bỏ khỏi UI.
- 12 giảng viên demo dùng các mẫu khác nhau: `04, 16, 10, 01, 06, 25, 20, 27, 23, 31, 08, 11`. Thẻ, bảng, hồ sơ và form thêm/sửa đọc cùng ảnh từ `Portraits`. Nút **Đổi ảnh** quay vòng đủ 48 mẫu.
- Manifest: `Assets/licenses/Notionists-AuthorSet-Sources.json`; giấy phép: `Assets/licenses/Notionists-License.txt`; bảng xem nhanh: `Assets/licenses/Notionists-AuthorSet-ContactSheet.png`, có bản sao trong `docs/screenshots/14-author-avatar-assets.png`. Tài liệu nguồn từ dự án tác giả cũng được lưu kèm.
- Seed và tham số được dùng khi chuẩn bị asset, không gửi tên, email hoặc hồ sơ giảng viên khi chạy. App chỉ đọc PNG cục bộ.
- Tên và thông tin giảng viên trong dữ liệu mẫu đều hư cấu; avatar minh họa không xác định danh tính người thật.

Các manifest Notionists cũ (bộ 12), Open Peeps, Unsplash và giấy phép Icons8 là lưu vết phiên bản trước. Ảnh/SVG Open Peeps đã chuyển vào `Assets/licenses/OpenPeeps-Archive/`; PNG trong `Assets/avatars/` hiện chỉ dùng bộ 48 Notionists của QuanLyTacGia.

## Font: Plus Jakarta Sans

- Tác giả: The Plus Jakarta Sans Project Authors / Tokotype.
- Kho chính thức: https://github.com/tokotype/PlusJakartaSans
- Font static dùng được trong WinForms; metadata Google Fonts xác nhận hỗ trợ tiếng Việt: https://raw.githubusercontent.com/google/fonts/main/ofl/plusjakartasans/METADATA.pb
- Giấy phép **SIL Open Font License 1.1**; bản đi kèm: `Assets/licenses/OFL-PlusJakartaSans.txt`.
- Bản gốc giấy phép: https://raw.githubusercontent.com/tokotype/PlusJakartaSans/master/OFL.txt

| File | URL tải chính thức |
|---|---|
| `Assets/fonts/PlusJakartaSans-Regular.ttf` | https://raw.githubusercontent.com/tokotype/PlusJakartaSans/master/fonts/ttf/PlusJakartaSans-Regular.ttf |
| `Assets/fonts/PlusJakartaSans-SemiBold.ttf` | https://raw.githubusercontent.com/tokotype/PlusJakartaSans/master/fonts/ttf/PlusJakartaSans-SemiBold.ttf |
| `Assets/fonts/PlusJakartaSans-Bold.ttf` | https://raw.githubusercontent.com/tokotype/PlusJakartaSans/master/fonts/ttf/PlusJakartaSans-Bold.ttf |

## Icon động: lucide-animated / Lucide

- Nguồn chính thức: https://lucide-animated.com/ ; repo https://github.com/pqoqubbw/icons . Animation **MIT**, commit đã giữ `cf847c1f6ba0e1f0695e2ded6bf6d86df0a09232`. Hình Lucide có **ISC** và thông báo MIT với các icon kế thừa Feather. Toàn bộ văn bản giấy phép nằm trong `Assets/licenses/Lucide-Animated-MIT.txt`, `Lucide-ISC-MIT.txt` và `Motion-MIT.txt`.
- Chỉ thay 3 icon thống kê và 4 icon khoa; các icon thao tác nhỏ vẫn dùng Ant Design của AntdUI. Mapping:

| Vị trí | Icon nguồn |
|---|---|
| Tổng giảng viên | `users` |
| Khoa có giảng viên | `graduation-cap` |
| Lượt phân công lớp | `book-text` |
| Công nghệ thông tin | `folder-code` |
| Kinh tế | `chart-column-increasing` |
| Ngoại ngữ | `languages` |
| Kỹ thuật | `cog` |

- Geometry và keyframe lấy từ TSX gốc; lưu nguyên byte trong `Assets/animated-icons/sources/`. Script `Assets/animated-icons/export_frames.cjs` dùng Sharp/librsvg và generator **Motion 13.1.1** đã lưu để lấy mẫu animation khoảng 30 FPS. Không tạo bộ icon mới bằng AI hoặc thêm hiệu ứng bounce chung thay chuyển động nguồn.
- PNG RGBA: mỗi ô 128×128, viewBox `-2 -2 28 28`, stroke gốc 2 đơn vị, màu `#211A1C`. App vẽ ảnh 86% vùng icon; đệm trong SVG làm nét chiếm khoảng 72% chip pastel. Bản nghỉ riêng ở ô cuối hiển thị đầy đủ glyph.
- Mỗi icon có PNG và JSON cục bộ chứa số frame, thời lượng và ô nghỉ. Nguồn chính xác, pinned commit và SHA256: `Assets/licenses/Lucide-Animated-Sources.json`.
- `AnimatedDashboardIcon` phát một lượt khi rê chuột lên thẻ/khoa, khoảng 0,5–1,2 giây; không chạy loop khi nghỉ. Timer dừng khi kết thúc, ẩn, cuộn ra khỏi viewport hoặc dispose. Chế độ giảm chuyển động trong code dừng animation đang chạy và ngăn lượt mới; được native harness kiểm tra qua API nội bộ, chưa có checkbox trong UI. Nếu file bị thiếu/hỏng, UI dùng icon tĩnh và ghi lỗi qua `Trace`.
- Runtime chỉ đọc sprite PNG bằng C# WinForms, không chạy TSX/JavaScript, không dùng WebView và không có yêu cầu mạng. Chạy lại `node Assets/animated-icons/export_frames.cjs` chỉ khi cần tái xuất asset; cần Node.js + Sharp qua `NODE_PATH`/`SHARP_MODULE_PATH`.

## Chuyển động và interaction

### Ba thẻ thống kê và từng ô khoa — Auth Form

- Tham chiếu theo yêu cầu: [Auth Form — Vaibhav Kumar Singh, 21st.dev](https://21st.dev/@vaib215/components/auth-form), đối chiếu ngày 10/10/2026 với mã TSX người dùng đính kèm. Trang nguồn ghi MIT License. Triển khai C# riêng theo hành vi, không đóng gói TSX hoặc thêm asset/dependency của component React.
- Người dùng chọn cả entrance và hover cho ba thẻ thống kê cùng Theo khoa; yêu cầu ngày 11/10/2026 chuyển hover sang **từng ô khoa**. Entrance của ba thống kê và khung ngoài vẫn opacity 0→1 và y=25→0 logical px trong 1.250 ms, ease-in-out. Hover trên ba thống kê và bốn ô khoa có ellipse 2,5 lần kích thước thẻ chạy chéo từ góc phải dưới tới tâm trong 550 ms (người dùng chỉnh lại từ 450 ms ngày 11/10/2026), đồng thời phần vẽ zoom 1→1,05 trong 500 ms. Khung ngoài không có hover fill/zoom. Sau yêu cầu bỏ rung, người dùng xác nhận vẫn muốn zoom nhẹ theo mẫu; zoom tăng/giảm đơn điệu bằng ease-in-out, không spring, bounce hoặc chu kỳ rung. Đảo hover bắt đầu từ tiến độ hiện tại của cả vòng màu và zoom.
- Vòng màu pha 50% trắng với Peach/Lilac/Cyan cho ba thống kê. Theo yêu cầu mới ngày 11/10/2026, bốn ô khoa dùng màu loang riêng pha 50% trắng từ chip: CNTT `#F1FAFB`, Kinh tế `#FDE4E0`, Ngoại ngữ `#FAF6FE`, Kỹ thuật `#F4FCF7`; khung ngoài trắng. Giữ font Plus Jakarta Sans, màu chữ và chip icon Cyan/Peach/Lilac/Mint. Vòng màu thay tint đồng đều 35%/160 ms trước đó, không chạy hai hover cùng lúc.
- `AuthCardMotion` vẽ trên overlay ngoài vùng AutoScroll, clip theo viewport và khoảng trống giữa sibling để không che control cạnh thẻ. Khi thẻ nằm hoàn toàn trong viewport theo chiều ngang, padding zoom được vẽ vào gutter Canvas trống 32 logical px của MainForm để giữ góc bo; thẻ bị cuộn khuất vẫn clip sát viewport, clip dọc và host giữ nguyên. Ô khoa phục hồi nền trắng của parent và chỉ có padding zoom, không có padding slide của entrance. Control và hitbox giữ nguyên bounds. Chữ được tách khỏi bitmap nền/icon bị zoom; raster bằng chính `AntdUI.Label.OnPaint` qua `InvokePaint` ở kích thước zoom hiện tại, cache theo font/DPI/text/geometry/zoom, rồi chép nguyên pixel tại tọa độ nguyên. Không đổi font/control sống; số đếm và frame icon vẫn cập nhật. Hover ô khoa trong lúc khung đang entrance được chờ đến khi entrance hoàn tất; rời chuột/ẩn/dispose hủy lượt chờ.
- Ba job hữu hạn dùng clock trang chung, không thêm timer. Resize/cuộn/ẩn/dispose hủy overlay; giảm chuyển động chốt trạng thái cuối ngay. Preview, kiểm tra pixel và phạm vi coverage xem [UI_VERIFICATION.md](UI_VERIFICATION.md).

### Select Khoa / Trạng thái

- Tham chiếu người dùng yêu cầu: [Animated Select — Evans Liaudet, 21st.dev](https://21st.dev/community/components/evansliaudet/animated-select-1/default), nguồn demo được trang công bố: [animated-select.vercel.app](https://animated-select.vercel.app/). Đối chiếu ngày 10/10/2026 với TSX người dùng đính kèm.
- Chuyển hành vi thành C# native trong `Ui/AnimatedSelect.cs` và `Ui/SelectPopup.cs`: dropdown opacity 0↔1 và phần vẽ y=-10↔0 logical px trong 200 ms; caret quay 0↔180° trong 200 ms; cột nhãn cuộn theo index, gap 8 logical px, trong 300 ms với CSS ease-in-out `cubic-bezier(0.42, 0, 0.58, 1)`. Menu có gap 8px, hàng 28px, cùng font/palette của dashboard.
- Đây là triển khai C# riêng theo hành vi tham khảo; không sao chép/đóng gói TSX, không đưa asset mới hoặc dependency React/Motion/Phosphor vào ứng dụng. Trang tham chiếu không cung cấp giấy phép component rõ ràng trong nội dung truy cập được; không gán MIT cho mã nguồn đó. Các giấy phép asset hiện có giữ nguyên.
- Mỗi hiệu ứng là job hữu hạn trên `PageMotionCoordinator`; bitmap chữ được cache theo geometry/font/DPI. Trigger và popup giữ bounds cố định, chỉ phần vẽ dịch float. Popup native không chiếm focus, đóng khi click ngoài, Escape/Tab, cuộn, resize, di chuyển hoặc owner mất activation; tái mở giữa fade khôi phục đầy đủ handler. Trong reveal, chỉ nhận commit chuột sau khi hàng ổn định để không chọn nhầm hàng đang trượt; bàn phím vẫn dùng được.
- Chế độ giảm chuyển động nội bộ đặt ngay trạng thái cuối. Popup, bitmap, motion và message filter/handler được giải phóng khi đóng/dispose. Trigger expose giá trị/trạng thái accessibility; popup expose từng option có tên và thông báo focus. Chi tiết kiểm chứng và preview ở [UI_VERIFICATION.md](UI_VERIFICATION.md).

### Danh sách giảng viên — Interactive List Preview

**Đã gỡ khỏi ứng dụng theo yêu cầu ngày 11/10/2026.** Thông tin nguồn và kết quả triển khai dưới đây được giữ làm lịch sử; không còn preview, highlight/parallax tùy biến hoặc hiệu ứng lồng thẻ khi hover bảng.

- Tham chiếu theo yêu cầu: [Interactive List Preview — Hyperiux Vault, 21st.dev](https://21st.dev/@hyperiux/components/interactive-list-preview) và [demo chính thức](https://vault.hyperiux.com/demo/interactive-list-preview), kiểm tra ngày 10/10/2026. Đối chiếu [mã nguồn](https://raw.githubusercontent.com/Hyperiux-Immersion-Labs/hyperiux-components/628c6d084bdda656ac188c679ba86c502b114589/registry/effects/components/interactive-list-preview/index.tsx) tại commit `628c6d084bdda656ac188c679ba86c502b114589`, SHA256 `38c22bae4601a982a9960767c5c13dd47b91c3460dddd288e6a8d0066550aad3`; registry ghi phiên bản 1.0.1, tier Free.
- Trang 21st.dev ghi MIT License, nhưng repo chính thức tại commit trên áp [Hyperiux Effects License](https://raw.githubusercontent.com/Hyperiux-Immersion-Labs/hyperiux-components/628c6d084bdda656ac188c679ba86c502b114589/LICENSE_EFFECTS) cho các effect và tách MIT của CLI. Không gán MIT cho effect. Bản WinForms là triển khai C# riêng theo hành vi tham khảo; không sao chép hoặc đóng gói TSX, ảnh, GSAP hay dependency React của nguồn.
- Người dùng chọn áp dụng riêng cho chế độ **Bảng**. Hover trên hàng hiện thông tin thật của giảng viên: tên, mã, khoa, trạng thái, học vị, số lớp phụ trách, ngày sinh, giới tính, email và điện thoại; không có ảnh hoặc avatar trong preview. Thẻ thông tin dùng font Plus Jakarta Sans, nền trắng và header hồng phấn `#FCE7F3`; màu các khoa giữ nguyên. Hover chỉ xem nhanh, không đổi giảng viên đang chọn hoặc hồ sơ bên phải. Chế độ **Thẻ** giữ hiệu ứng nâng nhẹ hiện có.
- Clip mở từ tâm `inset(50%)` → `inset(0%)`, giữ curve `power2.inOut` của nguồn. Theo yêu cầu ngày 11/10/2026, bản native tăng tốc mở/đóng từ 600 xuống 320 ms. Mỗi mã giảng viên khác mở một lượt phía trên thẻ trước; nền thẻ trước chỉ giải phóng khi thẻ mới mở đầy đủ. Chuyển nhanh gộp phần các thẻ đang thấy thành một bitmap cục bộ duy nhất, giữ chữ nguyên pixel và không chứa highlight dòng. Một surface native và cache nội dung hiện tại được tái sử dụng, tránh tích lũy nhiều bitmap/thẻ. Rê trong cùng hàng không restart; reenter cùng giảng viên trong exit đảo từ tiến độ hiện tại, giảng viên khác mở từ 0 trên nền các thẻ trước. Highlight pastel trượt tới hàng đang hover trong 350 ms, `power3.out`; opacity highlight vẫn đi cùng reveal. Preview neo ở 35% chiều rộng và giữa chiều cao viewport bảng, parallax tối đa ±10 logical px. Bản C# retarget bằng job hữu hạn 160 ms trên clock chung thay vòng RAF liên tục. Vị trí được clamp để vừa viewport; chỉ hiện khi vùng khả dụng cao ít nhất 200 logical px, tránh phần thông tin bị cắt thành dải nhỏ.
- Layered Form phủ viewport cố định, click-through và không chiếm activation. Nền thẻ và parallax vẫn vẽ tọa độ float; chữ dùng các tile nền opaque được cache bằng ClearType, căn theo pixel và chép nguyên kích thước để giữ nét khi thẻ dịch. Cache theo dữ liệu, geometry, font và DPI. Bố cục nhỏ giảm khoảng cách giữa các dòng thay vì scale toàn bộ chữ; tên 12pt, trạng thái 8pt, chữ phụ 8,5pt. Phần vẽ được clip theo mặt thẻ bo góc và reveal từ tâm.
- Không dịch control danh sách hoặc hitbox mỗi frame. Cuộn, lọc, resize, di chuyển cửa sổ, đổi Thẻ/Bảng, thao tác hàng, menu, dialog, ẩn, mất activation hoặc dispose đều đóng preview; giảm chuyển động áp dụng trạng thái cuối ngay. Cache, Form, job và handler được giải phóng khi không còn dùng. Kiểm chứng và preview native xem [UI_VERIFICATION.md](UI_VERIFICATION.md).

Các component dưới đây được tìm trên **21st.dev**, sau đó đối chiếu mã nguồn chính thức của Motion Primitives và Magic UI. Hiệu ứng được chuyển thành C# native, thời lượng được điều chỉnh cho màn hình quản lý desktop; đây không phải bản chạy nguyên component React.

| Tham chiếu chính xác | Phần áp dụng trong WinForms |
|---|---|
| [Animated Group — Motion Primitives](https://21st.dev/@ibelick/components/animated-group) | Reveal các vùng còn lại khi mở trang: 360 ms mỗi vùng, cách nhau 60 ms; dùng fade/slide nhẹ trên snapshot, giữ nguyên vị trí control tương tác. Ba thống kê và Theo khoa dùng Auth Form ở trên; lưới thẻ giữ entrance có stagger. |
| [Number Ticker — Magic UI](https://21st.dev/@dillionverma/components/number-ticker) | Ba thống kê count-up 650 ms, số cuối lấy từ dữ liệu thật; cùng cách tiến triển được áp dụng cho vòng tỷ lệ giảng viên đang dạy. Giá trị accessibility giữ kết quả cuối. |
| [Shimmer Button — Magic UI](https://21st.dev/@dillionverma/components/shimmer-button) | Vệt sáng mềm 600 ms trên nút primary khi hover/focus, giới hạn trong vùng bo góc; không chạy shimmer liên tục khi nút nghỉ. |
| [Animated Background — Motion Primitives](https://21st.dev/@ibelick/components/animated-background) | Phản hồi tint pastel 160 ms khi hover các vùng và thẻ; control giữ kích thước/vị trí, không dịch hitbox hoặc thay dữ liệu. |
| [Transition Panel — Motion Primitives](https://21st.dev/@ibelick/components/transition-panel) | Hồ sơ chuyển nội dung bằng crossfade 220 ms; chuyển Thẻ/Bảng và danh sách sau khi đổi bộ lọc dùng transition 160 ms. Snapshot được giải phóng sau chuyển trạng thái. |
| [Aurora Background — Aceternity UI](https://21st.dev/@manuarora700/components/aurora-background) | **Chỉ tham khảo hình ảnh**. Hero giữ gradient lavender–peach cố định; artwork Overflow Design nổi ±3 logical px chu kỳ 5 giây (tăng tốc độ 20% từ chu kỳ 6 giây theo yêu cầu ngày 10/10/2026). Bản blend màu chu kỳ 9 giây đã bỏ để tránh repaint parent khi ảnh chuyển động. Không sao chép component Aurora hay shader. |

### Mã nguồn và giấy phép đã giữ

- **Motion Primitives**, MIT, Copyright (c) 2024 ibelick: commit [`120f64f6ca60348e251f929e9c81f11ccbe45eda`](https://github.com/ibelick/motion-primitives/tree/120f64f6ca60348e251f929e9c81f11ccbe45eda). Văn bản MIT nguyên byte ở `Assets/licenses/Motion-Primitives-MIT.txt`; nguồn license: [LICENCE.md](https://raw.githubusercontent.com/ibelick/motion-primitives/120f64f6ca60348e251f929e9c81f11ccbe45eda/LICENCE.md).
- **Magic UI**, MIT, Copyright (c) Magic UI: commit [`cdb348cb4c72a9b54b554d8617801e479fbc8714`](https://github.com/magicuidesign/magicui/tree/cdb348cb4c72a9b54b554d8617801e479fbc8714). Văn bản MIT nguyên byte ở `Assets/licenses/Magic-UI-MIT.txt`; nguồn license: [LICENSE.md](https://raw.githubusercontent.com/magicuidesign/magicui/cdb348cb4c72a9b54b554d8617801e479fbc8714/LICENSE.md).
- Các TSX gốc được giữ nguyên tại `Assets/licenses/PageMotion-ReferenceSource/` để đối chiếu: `animated-group`, `animated-background`, `transition-panel`, `number-ticker`, `shimmer-button`. Chúng là tài liệu nguồn, không được biên dịch hoặc thực thi trong app.
- Manifest `Assets/licenses/Page-Motion-Sources.json` ghi trang 21st.dev, URL raw tại commit cố định, SHA256 của từng bản nguồn và giấy phép. Hai giấy phép trên khác với `Motion-MIT.txt` của thư viện Motion dùng ở bước xuất sprite icon.
- Aurora chỉ là nguồn tham khảo thị giác; giấy phép minh họa hero vẫn là Overflow Design. Bộ motion không thay giấy phép avatar, font hoặc icon hiện có.

### Cơ chế chạy ngoại tuyến

`PageMotionCoordinator` dùng Timer WinForms nominal 15 ms và thời gian thực để điều phối hiệu ứng trang. Các job hữu hạn kết thúc và bỏ callback khi xong; ambient hero tạm dừng khi app bị ẩn/minimized và bỏ cập nhật khi artwork ra khỏi viewport. API giảm chuyển động nội bộ dừng các hiệu ứng đang chạy, đặt số và tỷ lệ về kết quả cuối, trả artwork về vị trí chuẩn và ngăn animation mới; native harness gọi API này, UI hiện không có hộp Thông tin/checkbox. Khi cuộn/resize/DPI thay đổi, snapshot cũ bị hủy để giữ bố cục và vị trí bấm đúng.

Ứng dụng chỉ chạy C#/GDI+/AntdUI; không chạy React, CSS, JavaScript, WebView hoặc gọi mạng lúc chạy. Các hình minh họa và sprite đọc từ asset cục bộ. Hộp Thông tin, footer credit và các đường mở trình duyệt đã bỏ khỏi UI; nguồn, liên kết tham khảo, giấy phép và manifest trong dự án vẫn được giữ.

### Điều chỉnh render cho chuyển động liên tục

- Hero trước đây làm tròn offset ±3 px rồi di chuyển control `Avatar`: tại 100% DPI chỉ có 7 vị trí trong một chu kỳ. `FloatingArtwork` hiện giữ một slot opaque, double-buffered, tự vẽ đúng gradient phía sau và bitmap đã scale/cache bằng tọa độ float. Artwork gốc vẫn không thay đổi; chỉ vị trí phần vẽ chuyển động.
- Lượt sửa nhịp hero sau đó giữ nền gradient tĩnh, tránh thay `BackExtend` và dựng lại brush nền trong ambient tick; raster artwork được tái sử dụng. Timer được đổi từ nominal 16 sang 15 ms sau phép đo invalidation trong môi trường kiểm tra. Không suy ra rằng interval này luôn tạo nhịp frame 15 ms trên mọi máy.
- Hover thẻ trước đây ép mức nâng 4 px thành `int`, tạo 5 vị trí. Bản hiện tại dùng lift float 3 logical px trong 180 ms; chữ, avatar và metadata được vẽ vào cache để không dựng lại Font/Brush/StringFormat ở mọi frame. Bounds và điểm bấm của thẻ giữ nguyên.
- Card Tình hình giảng dạy đã bỏ chấm và arc sáng chạy trên vòng theo yêu cầu. Ring giữ sweep theo tỷ lệ thật khi đổi dữ liệu và hover thay độ dày nét trong 160 ms; cache font/brush/pen, dùng clock trang chung cho hiệu ứng hữu hạn, không repaint định kỳ khi nghỉ. Ẩn panel hoặc giảm chuyển động reset hover.
- Raster/cache được dựng lại khi size, scale, font hoặc DPI render thay đổi, và được dispose khi thay cache hoặc đóng control. Benchmark không được dùng để suy ra FPS hay độ mượt trên mọi cấu hình; các kết quả có phạm vi cụ thể ở `docs/UI_VERIFICATION.md`.

Nguồn kỹ thuật chính thức: [Microsoft — Double buffering cho Form/control](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/how-to-reduce-graphics-flicker-with-double-buffering-for-forms-and-controls) cho custom paint qua buffer; [Graphics.DrawImage, overload RectangleF](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.graphics.drawimage?view=windowsdesktop-10.0#system-drawing-graphics-drawimage(system-drawing-image-system-drawing-rectanglef-system-drawing-rectanglef-system-drawing-graphicsunit)) cho vị trí và kích thước vẽ float. Đây là hướng triển khai WinForms; các pattern thị giác 21st.dev và giấy phép nguồn ở trên vẫn được giữ riêng.

[Microsoft — WM_TIMER](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-timer) xác nhận message timer có ưu tiên thấp trong UI queue. Vì vậy số đo nhịp invalidation được ghi riêng khỏi thời gian paint và FPS compositor; lượt đo hero hiện không ghi nhận `WM_PAINT` trong môi trường khởi động ẩn. Manifest nguồn motion giữ nguyên lịch sử bản tham khảo; mô tả triển khai hiện tại theo phần này.

Nguồn tham khảo interaction của bản trước: [Framer — Create light and hover effects](https://www.framer.com/academy/lessons/light-effects), [Framer — How animations and effects work](https://www.framer.com/help/articles/how-animations-and-effects-work-in-framer/), [Animated Profile Card — Isaiah](https://21st.dev/isaiahbjork/profile-card), [Codrops Creative Hub](https://tympanus.net/codrops/hub/). Bản chuyển động toàn trang hiện tại dùng mapping cụ thể trong bảng trên.

## Tải lại

Script `Assets/download_assets.py` chuẩn bị bộ 48 Notionists tương ứng với QuanLyTacGia, tạo manifest/contact sheet; font vẫn lấy từ kho Tokotype chính thức. Seed và 5 nền giữ đúng bộ tác giả. Asset đóng gói cục bộ, ứng dụng không tải ảnh khi chạy. Minh họa Overflow Design giữ SVG gốc và PNG chuyển định dạng trong `Assets/illustrations/`; chạy `node Assets/illustrations/export_on_the_laptop.cjs` để xuất lại PNG từ SVG cục bộ và cập nhật manifest.

Chạy `python Assets/download_assets.py --avatars-only` để sao chép nguyên bộ từ dự án QuanLyTacGia bên cạnh và giữ font hiện có. Dùng `--source-dir` cho thư mục ảnh tác giả ở checkout khác. Khi thiếu bộ nguồn hoặc chọn `--download`, script tải đúng URL PNG API 9.x được ghi trong manifest; nguồn online có thể thay đổi, nên bản PNG đã đóng gói là bản tham chiếu đang dùng.

Sau khi tách dự án sang `E:\AI Agents\Dangkytinchi`, bộ 48 PNG đã đóng gói vẫn dùng độc lập. Nếu cần chép lại từ dự án tác giả tại chỗ cũ, chạy `python Assets/download_assets.py --avatars-only --source-dir "E:\AI Agents\visual\QuanLyTacGia\Assets\avatars"` từ thư mục `QuanLyGiangVien`.

Chỉ khi chuẩn bị lại avatar/font mới cần **Python + Pillow**; xuất lại minh họa Overflow Design cần **Node.js + Sharp**, nhận `NODE_PATH` hoặc `SHARP_MODULE_PATH`. App WinForms dùng các PNG đã có, không cần những công cụ này để chạy.
