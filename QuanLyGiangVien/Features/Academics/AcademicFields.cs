using static QuanLyGiangVien.Features.Academics.AcademicFieldType;

namespace QuanLyGiangVien.Features.Academics;

internal static class AcademicFields
{
    private static AcademicField Id() => new("id", "Mã bản ghi (tối đa 20 ký tự)", Required: true, Help: "Chữ A–Z, số, gạch dưới hoặc gạch ngang. Mã riêng cho từng phiên bản.");
    private static AcademicField Ref(string key, string caption, string table, bool required = true) =>
        new(key, caption, Choice, required, ReferenceTable: table, ReferenceId: "id", ReferenceName: "name");
    private static AcademicField Program() => Ref("program", "Chương trình đào tạo", "Programs");
    private static AcademicField Student() => Ref("student", "Sinh viên", "Students");
    private static AcademicField Course(string key = "course", string caption = "Học phần") => Ref(key, caption, "CourseChoices");
    private static AcademicField Term() => Ref("term", "Học kỳ", "TermChoices");
    internal static AcademicField[] Programs => [Id(), new("name", "Tên chương trình / phiên bản", Required: true),
        Ref("major", "Ngành", "MajorChoices"), new("cohort", "Khóa tuyển sinh", Integer, true, "2026"),
        new("requiredCredits", "Tổng tín chỉ cần tích lũy", Integer, true, "120")];
    internal static AcademicField[] Groups => [Id(), Program(), new("name", "Tên nhóm tự chọn", Required: true),
        new("requiredCredits", "Tín chỉ cần đạt trong nhóm", Integer, true, "6")];
    internal static AcademicField[] Courses => [Id(), Program(), Course(),
        Ref("group", "Nhóm tự chọn (nếu có)", "Groups", false), new("required", "Học phần bắt buộc", Check, Default: "true"),
        new("equivalence", "Mã nhóm tương đương", Help: "Các môn cùng mã nhóm được công nhận thay thế và chỉ cộng tín chỉ một lần. Để trống nếu không tương đương.")];
    internal static AcademicField[] Requirements => [Id(), Program(), Course(),
        new("group", "Nhóm lựa chọn OR", Integer, true, "1", "Trong cùng nhóm: đạt một điều kiện. Giữa các nhóm: phải đạt tất cả (AND)."),
        Course("requiredCourse", "Học phần điều kiện"),
        new("kind", "Loại điều kiện", Choice, true, "PRE", Options: [new("PRE", "Tiên quyết · cần đạt"), new("PRIOR", "Học trước · đã học"), new("CO", "Song hành · đã đạt hoặc cùng học")]),
        new("minGrade", "Điểm tối thiểu (thang 10)", AcademicFieldType.Decimal, true, "5")];
    internal static AcademicField[] Assignments => [Id(), Student(), Program()];
    internal static AcademicField[] Policies => [Id(), new("name", "Tên quy chế / phiên bản", Required: true),
        new("source", "Văn bản nguồn (bắt buộc khi công bố)", Help: "Ghi số quyết định và URL/vị trí văn bản đã xác minh; tối đa 500 ký tự."), Term(),
        new("standing", "Mã học lực áp dụng", Required: true, Default: "*", Help: "Dấu * áp dụng chung; mã học lực riêng phải khớp hồ sơ sinh viên."),
        new("minCredits", "Tín chỉ tối thiểu khi chốt kỳ", Integer, true, "14"), new("maxCredits", "Tín chỉ tối đa", Integer, true, "24"),
        new("passGrade", "Điểm đạt theo quy chế (thang 10)", AcademicFieldType.Decimal, true, "5"), new("allowImprove", "Cho phép học cải thiện", Check, Default: "true"),
        new("maxAttempts", "Số lần học tối đa (0 = không giới hạn)", Integer, true, "0"), new("enforceCurriculum", "Chỉ cho đăng ký môn thuộc CTĐT", Check, Default: "true"),
        new("excellentThreshold", "Ngưỡng điểm cho phép học vượt", AcademicFieldType.Decimal, Help: "Để trống nếu quy chế không có ngưỡng học vượt theo điểm."),
        new("excellentMaxCredits", "Tín chỉ tối đa khi đạt ngưỡng", Integer),
        new("warningRetakeOnly", "Học lực cảnh báo chỉ được học lại", Check)];
    internal static AcademicField[] Overrides => [Id(), Student(), Term(), new("minCredits", "Tín chỉ tối thiểu được duyệt", Integer, true, "0"),
        new("maxCredits", "Tín chỉ tối đa được duyệt", Integer, true, "30"), new("expires", "Hiệu lực đến hết ngày", Date, true),
        new("reason", "Lý do / số quyết định duyệt", Required: true)];
    internal static AcademicField[] Holds => [Id(), Student(),
        new("scope", "Thao tác bị khóa", Choice, true, "REGISTER", Options: [new("REGISTER", "Đăng ký học"), new("CANCEL", "Hủy đăng ký"), new("ALL", "Đăng ký và hủy")]),
        new("authority", "Đơn vị có quyền gỡ khóa", Required: true), new("reason", "Lý do khóa", Required: true),
        new("expires", "Hiệu lực đến hết ngày", Date, true), new("active", "Khóa đang hiệu lực", Check, Default: "true")];
    internal static AcademicField[] Standing => [Id(), Student(), Term(),
        new("status", "Trạng thái học vụ", Choice, true, "ACTIVE", Options: [new("ACTIVE", "Đang học"), new("SUSPENDED", "Tạm dừng / bảo lưu"), new("STOPPED", "Đã thôi học")]),
        new("standing", "Mã học lực", Required: true, Default: "NORMAL", Help: "Ví dụ NORMAL, WARNING; quy chế có thể áp dụng giới hạn riêng cho từng mã."),
        new("gpa", "Điểm trung bình chung đến lúc đăng ký (thang 10)", AcademicFieldType.Decimal), new("finalTerm", "Học kỳ cuối khóa", Check),
        new("reason", "Căn cứ / ghi chú học vụ", Required: true)];
    internal static AcademicField[] Results => [Id(), Student(), Course(), Term(), new("attempt", "Lần học", Integer, true, "1"),
        new("recognition", "Hình thức công nhận", Choice, true, "GRADED", Options: [new("GRADED", "Có điểm đánh giá"), new("TRANSFER", "Chuyển / công nhận tín chỉ"), new("EXEMPT", "Miễn học theo quyết định")]),
        new("grade", "Điểm được công nhận (thang 10)", AcademicFieldType.Decimal, Help: "Kết quả có điểm phải nhập điểm thật. Miễn/chuyển tín chỉ được để trống; không tạo điểm giả."), new("passed", "Được công nhận đạt", Check, Default: "true"),
        new("source", "Nguồn điểm chính thức / quyết định", Required: true), new("publication", "Ngày công bố (bỏ chọn = điểm nháp)", Date),
        new("reason", "Lý do sửa (bắt buộc khi cập nhật)", Help: "Mỗi lần sửa được giữ trong lịch sử; không thay đổi nguồn điểm âm thầm.")];
}
