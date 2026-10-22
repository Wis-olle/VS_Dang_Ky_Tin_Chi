namespace QuanLyGiangVien;

// Phần bổ sung cho MainForm khi được đặt bên trong khung điều hướng của ứng dụng.
public sealed partial class MainForm
{
    // Cờ này được gán ở hàm khởi tạo của file chính: true nghĩa là dùng thanh điều hướng của khung cha.
    private readonly bool embedded;
    internal bool IsEmbedded => embedded;
    // Bộ kiểm chứng dùng thuộc tính này để phát hiện thanh tiêu đề/điều hướng riêng còn hiển thị khi nhúng.
    internal bool HasOwnChromeForCheck => titlebar.Visible || nav.Visible;
}
