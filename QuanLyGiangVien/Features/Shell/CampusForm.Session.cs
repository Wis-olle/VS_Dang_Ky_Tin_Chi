using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Shell;

internal sealed partial class CampusForm
{
    private readonly Panel sessionNotice = new() { Name = "sessionExpiredNotice", Visible = false };
    private AntdUI.Label? sessionNoticeText;
    private AntdUI.Button? recoverSession;
    private bool sessionBlocked, recovering;

    private void BuildSessionRecovery()
    {
        sessionNotice.BackColor = AppTheme.AccentSoft;
        sessionNoticeText = TextLabel("Phiên đã hết hạn. Bản nháp được giữ nguyên; đăng nhập lại để tiếp tục.", 9, true, AppTheme.PrimaryPressed);
        recoverSession = ActionButton("Đăng nhập lại", true);
        recoverSession.Name = "recoverSession";
        recoverSession.Click += (_, _) => RecoverSession();
        sessionNotice.Controls.AddRange([sessionNoticeText, recoverSession]);
        Controls.Add(sessionNotice);
        AppSqlConnection.SessionExpired += OnSessionExpired;
        AppSqlConnection.SessionRetryBlocked += OnSessionExpired;
    }

    private void OnSessionExpired()
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(new Action(() => { if (AppSqlConnection.IsBlocked(connectionString)) ShowSessionExpired(); })); }
        catch (InvalidOperationException) when (IsDisposed || Disposing) { }
    }

    internal void ShowSessionExpired()
    {
        if (identity == null || IsDisposed) return;
        sessionBlocked = true; sessionNotice.Visible = true; host.Enabled = false;
        if (currentPage != null) currentPage.Enabled = false;
        foreach (var button in moduleButtons.Values) button.Enabled = false;
        LayoutShell(); sessionNotice.BringToFront();
        // Owned editors run a modal loop, so a shell button alone would be unreachable.
        var editor = Application.OpenForms.Cast<Form>().LastOrDefault(f => f.Modal && f.Visible && f is not SessionRecoveryDialog);
        if (passwordLogin && editor != null) RecoverSession(editor);
    }

    private void RecoverSession() => RecoverSession(this);

    private void RecoverSession(IWin32Window owner)
    {
        if (!sessionBlocked || busy || recovering || identity == null) return;
        if (!passwordLogin)
        {
            AntdUI.Message.error(this, "Phiên kiểm thử không dùng mật khẩu. Kết nối lại bằng tài khoản được cấp.");
            return;
        }
        recovering = true;
        try
        {
            using var dialog = new SessionRecoveryDialog(connectionString, username.Text.Trim(), loginPortal, identity);
            if (dialog.ShowDialog(owner) != DialogResult.OK) return;
            sessionBlocked = false; sessionNotice.Visible = false; host.Enabled = true;
            if (owner is Features.Scheduling.SchedulingEditorBase schedulingEditor) schedulingEditor.SessionRecovered();
            if (owner is Dialogs.LecturerEditor lecturerEditor) lecturerEditor.SessionRecovered();
            SetBusy(false); LayoutShell();
            AntdUI.Message.success(this, "Đã đăng nhập lại. Bản nháp được giữ; hãy kiểm tra trước khi lưu.");
        }
        finally { recovering = false; }
    }

    private void LayoutSessionRecovery(int width)
    {
        int s(int n) => AppTheme.S(n);
        sessionNotice.SetBounds(0, s(identity != null ? 142 : 92), width, s(64));
        sessionNoticeText?.SetBounds(s(28), s(12), Math.Max(1, width - s(230)), s(40));
        recoverSession?.SetBounds(width - s(182), s(12), s(154), s(40));
    }
}

internal sealed class SessionRecoveryDialog : Form
{
    private readonly TextBox secret = new() { UseSystemPasswordChar = true, MaxLength = 256, Name = "sessionPassword" };
    private readonly Label error = new() { AutoSize = false };
    private readonly AntdUI.Button submit;
    private readonly Font textFont = AppTheme.Font(10), titleFont = AppTheme.Font(15, true);
    private readonly string connectionString, username;
    private readonly LoginPortal portal;
    private readonly DatabaseIdentity expected;
    private bool busy;

    internal SessionRecoveryDialog(string connectionString, string username, LoginPortal portal, DatabaseIdentity expected)
    {
        this.connectionString = connectionString; this.username = username; this.portal = portal; this.expected = expected;
        int s(int n) => AppTheme.S(n);
        Text = "Campus · Đăng nhập lại"; BackColor = AppTheme.Canvas;
        AutoScaleMode = AutoScaleMode.None; StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        ClientSize = new(s(560), s(370)); Font = textFont;
        var heading = new Label { Text = "Tiếp tục phiên làm việc", Font = titleFont, AutoSize = true };
        heading.SetBounds(s(28), s(24), s(500), s(34));
        var caption = new Label { Text = $"Tài khoản: {username}\nBản nháp được giữ nguyên. Thao tác lưu chưa được thực hiện lại.", AutoSize = false };
        caption.SetBounds(s(28), s(70), s(500), s(55));
        var label = new Label { Text = "Mật khẩu", AutoSize = true }; label.SetBounds(s(28), s(135), s(500), s(25));
        secret.SetBounds(s(28), s(165), s(500), s(32)); secret.Font = textFont;
        error.ForeColor = AppTheme.Danger; error.SetBounds(s(28), s(207), s(500), s(90));
        submit = new AntdUI.Button { Text = "Đăng nhập lại", Type = AntdUI.TTypeMini.Primary, Radius = 12, Font = textFont };
        submit.SetBounds(s(348), s(310), s(180), s(40));
        submit.Click += async (_, _) => await SignInAsync();
        Controls.AddRange([heading, caption, label, secret, error, submit]);
        AcceptButton = submit;
        FormClosing += (_, args) => { if (busy) args.Cancel = true; };
        Shown += (_, _) => secret.Focus();
    }

    private async Task SignInAsync()
    {
        if (busy) return;
        busy = true; secret.Enabled = submit.Enabled = false; error.Text = "";
        var auth = new PasswordLogin(connectionString);
        bool signedIn = false;
        try
        {
            await auth.SignInAsync(username, secret.Text, portal);
            signedIn = true;
            var current = await new SqlIdentityRepository(connectionString).LoadAsync();
            if (current.AccountId != expected.AccountId || current.Role != expected.Role || current.StudentId != expected.StudentId)
            {
                await auth.SignOutAsync();
                AppSqlConnection.BlockSession(connectionString);
                throw new PersistenceException("Quyền hoặc tài khoản đã thay đổi. Đóng cửa sổ này và đăng xuất để tiếp tục với quyền mới.");
            }
            busy = false; DialogResult = DialogResult.OK; Close();
        }
        catch (PersistenceException ex)
        {
            if (signedIn) { await auth.SignOutAsync(); AppSqlConnection.BlockSession(connectionString); }
            error.Text = ex.Message;
        }
        finally { secret.Clear(); busy = false; if (!IsDisposed) { secret.Enabled = submit.Enabled = true; secret.Focus(); } }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) secret.Clear();
        base.Dispose(disposing);
        if (disposing) { textFont.Dispose(); titleFont.Dispose(); }
    }
}
