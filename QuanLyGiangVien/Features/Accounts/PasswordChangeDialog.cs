using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Accounts;

internal sealed class PasswordChangeDialog : Form
{
    private readonly TextBox username = new() { MaxLength = 128 }, current = new() { UseSystemPasswordChar = true, MaxLength = 256 },
        next = new() { UseSystemPasswordChar = true, MaxLength = 256 }, confirm = new() { UseSystemPasswordChar = true, MaxLength = 256 };
    private readonly Label notice = new() { AutoSize = true, MaximumSize = new(AppTheme.S(420), 0), ForeColor = AppTheme.Primary };
    private readonly Font ownFont;
    private bool busy;
    internal PasswordChangeDialog(string cs)
    {
        Text = "Đổi mật khẩu của tôi"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false; ClientSize = new(AppTheme.S(480), AppTheme.S(480)); BackColor = AppTheme.Canvas;
        ownFont = AppTheme.Font(10); Font = ownFont;
        var root = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(AppTheme.S(24)), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        void Field(string caption, TextBox box) { root.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new(0, AppTheme.S(8), 0, AppTheme.S(4)) }); box.Width = AppTheme.S(420); box.AccessibleName = caption; root.Controls.Add(box); }
        Field("Tên đăng nhập của bạn", username); Field("Mật khẩu hiện tại", current); Field("Mật khẩu mới (12–256 ký tự)", next); Field("Nhập lại mật khẩu mới", confirm);
        root.Controls.Add(notice);
        var save = new Button { Text = "Đổi mật khẩu", AutoSize = true, Height = AppTheme.S(40), BackColor = AppTheme.Primary, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new(0, AppTheme.S(16), 0, 0) };
        root.Controls.Add(save); Controls.Add(root); AcceptButton = save;
        save.Click += async (_, _) => {
            if (busy) return;
            busy = true; save.Enabled = false;
            try {
                if (next.Text != confirm.Text) throw new ArgumentException("Hai mật khẩu mới chưa khớp.");
                await new AccountRepository(cs).ChangeOwnPasswordAsync(username.Text, current.Text, next.Text);
                DialogResult = DialogResult.OK; Close();
            }
            catch (Exception error) when (error is ArgumentException or PersistenceException) { notice.Text = error.Message; }
            finally { if (!IsDisposed) { busy = false; save.Enabled = true; } }
        };
        FormClosing += (_, e) => { if (busy && DialogResult != DialogResult.OK) e.Cancel = true; };
    }
    protected override void Dispose(bool disposing) { if (disposing) { current.Clear(); next.Clear(); confirm.Clear(); ownFont.Dispose(); } base.Dispose(disposing); }
}
