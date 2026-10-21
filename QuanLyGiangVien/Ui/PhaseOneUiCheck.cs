using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Shell;
using QuanLyGiangVien.Features.Shell;
using QuanLyGiangVien.Features.Scheduling;
using QuanLyGiangVien.Features.Students;

namespace QuanLyGiangVien.Ui;

/// <summary>Opt-in native regression on an isolated test database, with synthetic student data only.</summary>
internal static class PhaseOneUiCheck
{
    internal static void Attach(CampusForm form, string connectionString)
    {
        var output = Environment.GetEnvironmentVariable("CAMPUS_PHASE1_UI_CHECK");
        if (string.IsNullOrWhiteSpace(output)) return;
        var settings = new SqlConnectionStringBuilder(connectionString);
        if (!settings.InitialCatalog.StartsWith("DangKyTinChi_Minh_Test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Phase-one UI verification requires an isolated test database.");
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new(SystemInformation.VirtualScreen.Right + 50, 0);
        form.Shown += async (_, _) => await RunAsync(form, connectionString, output);
    }

    private static async Task RunAsync(CampusForm form, string connectionString, string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        var repository = new StudentRepository(connectionString);
        string studentCode = "SV_UI_" + Guid.NewGuid().ToString("N")[..10];
        bool added = false;
        void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add(message);
        }
        async Task Shot(string name, int width = 1440)
        {
            form.ClientSize = new(AppTheme.S(width), AppTheme.S(960));
            await Task.Delay(250);
            using var bitmap = UiCapture.Client(form);
            bitmap.Save(Path.Combine(output, name + ".png"));
        }
        try
        {
            string credential = Environment.GetEnvironmentVariable("DEMO_TRAINING_PASSWORD")
                ?? throw new InvalidOperationException("Missing isolated training test credential.");
            form.SelectLoginPortal(LoginPortal.Training);
            Find<TextBox>(form, "txtLoginUsername").Text = "daotao";
            Find<TextBox>(form, "txtLoginPassword").Text = credential;
            await form.AuthenticateAsync();
            Expect(form.ConnectedIdentity?.Role == "DAO_TAO", "Real password login opens the training portal.");
            using (var sourceTable = await repository.ListAsync("", true))
            {
                var source = StudentRepository.FromRow(sourceTable.Rows[0]);
                await repository.SaveAsync(new(studentCode, "Sinh viên kiểm thử giao diện", null, null, null, null,
                    null, source.Major, source.Class, true), true);
                added = true;
            }
            await form.OpenModuleAsync(CampusModule.Students);
            var page = form.CurrentPage as StudentsPage ?? throw new InvalidOperationException("Student page did not open.");
            var grid = Field<DataGridView>(page, "grid");
            grid.ClearSelection();
            var selected = grid.Rows.Cast<DataGridViewRow>().Single(row => Convert.ToString(row.Cells["ma_sv"].Value) == studentCode);
            grid.CurrentCell = selected.Cells[0]; selected.Selected = true;
            var name = Field<TextBox>(page, "name");
            var phone = Field<TextBox>(page, "phone");
            string draftName = "Bản nháp sinh viên cần giữ";
            string draftPhone = "0901234567";
            name.Text = draftName; phone.Text = draftPhone;
            Expect(page.HasUnsavedChanges, "Editing name and phone creates a real unsaved student draft.");
            using (var latestTable = await repository.ListAsync(studentCode, true))
                await repository.SetStatusAsync(StudentRepository.FromRow(latestTable.Rows[0]), false);

            int warningsClosed = 0;
            var saveButton = Children(page).OfType<Button>().Single(button => button.Text == "Lưu sinh viên");
            var editor = saveButton.Parent?.Parent as ScrollableControl;
            editor?.ScrollControlIntoView(saveButton.Parent!);
            bool saveEventObserved = false;
            saveButton.Click += (_, _) => saveEventObserved = true;
            void SaveDiagnostics() => File.WriteAllText(Path.Combine(output, "conflict-diagnostics.json"), JsonSerializer.Serialize(new
            {
                saveEventObserved, warningsClosed, saveEnabled = saveButton.Enabled, saveButton.Visible, saveButton.CanSelect,
                pageEnabled = page.Enabled, busy = Field<bool>(page, "busy"), noticeVisible = Field<Label>(page, "editorNotice").Visible,
                noticeText = Field<Label>(page, "editorNotice").Text,
                baselineCode = Field<StudentRecord>(page, "baseline").Code,
                baselineHasVersion = Field<StudentRecord>(page, "baseline").Version?.Length == 64
            }, new JsonSerializerOptions { WriteIndented = true }));
            using (var dismiss = new System.Windows.Forms.Timer { Interval = 70 })
            {
                uint thread = GetCurrentThreadId();
                dismiss.Tick += (_, _) => warningsClosed += DismissOwnedWarning(thread, form.Handle);
                dismiss.Start();
                Click(saveButton);
                try
                {
                    await WaitUntilAsync(() => !Field<bool>(page, "busy") && Field<Label>(page, "editorNotice").Visible,
                        "The stale-write warning did not appear.");
                }
                finally { SaveDiagnostics(); }
                dismiss.Stop();
            }
            Expect(warningsClosed > 0, "The actual Save button displays a native warning for a stale profile.");
            Expect(name.Text == draftName && phone.Text == draftPhone && page.HasUnsavedChanges,
                "The stale-write warning keeps both edited fields and unsaved state.");
            Expect(Field<Label>(page, "editorNotice").Text.Contains("Bản nháp"), "The editor retains a visible instruction to reload after a conflict.");
            var conflictNotice = Field<Label>(page, "editorNotice");
            Expect(conflictNotice.Top >= 0 && conflictNotice.Bottom <= conflictNotice.Parent!.ClientSize.Height,
                "The conflict instruction scrolls into the editor viewport after saving from the bottom.");
            await Shot("01-student-conflict-wide");
            await Shot("02-student-conflict-compact", 1100);

            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using var expire = new SqlCommand("UPDATE dbo.APP_SESSION SET expires=DATEADD(SECOND,-1,SYSUTCDATETIME()) WHERE ma_tk=@id", connection);
                expire.Parameters.Add("@id", SqlDbType.Int).Value = int.Parse(form.ConnectedIdentity!.AccountId);
                Expect(await expire.ExecuteNonQueryAsync() > 0, "Only the isolated account's SQL sessions are expired for the regression.");
            }
            try
            {
                using var unexpected = await new CatalogRepository(connectionString).FacultiesAsync();
                throw new InvalidOperationException("An expired SQL session unexpectedly remained usable.");
            }
            catch (PersistenceException error) when (error.Kind == PersistenceFailure.SessionExpired) { }
            var notice = Find<Panel>(form, "sessionExpiredNotice");
            await WaitUntilAsync(() => notice.Visible, "The session recovery notice did not appear.");
            Expect(!page.Enabled && ReferenceEquals(form.CurrentPage, page), "Session expiry disables the same page instead of replacing its editor.");
            Expect(name.Text == draftName && phone.Text == draftPhone, "Session expiry preserves the student's pending draft.");
            await Shot("03-session-expired-wide");
            await Shot("04-session-expired-compact", 1100);

            bool dialogSubmitted = false;
            Exception? recoveryError = null;
            var recoveryClock = Stopwatch.StartNew();
            using (var recovery = new System.Windows.Forms.Timer { Interval = 80 })
            {
                recovery.Tick += (_, _) =>
                {
                    var dialog = Application.OpenForms.OfType<SessionRecoveryDialog>().FirstOrDefault(window => window.Owner == form);
                    if (dialog is null) return;
                    try
                    {
                        if (recoveryClock.Elapsed > TimeSpan.FromSeconds(30))
                        {
                            recoveryError = new InvalidOperationException("Password recovery did not complete: " + Field<Label>(dialog, "error").Text);
                            dialog.Close(); return;
                        }
                        if (dialogSubmitted) return;
                        using (var bitmap = UiCapture.Client(dialog)) bitmap.Save(Path.Combine(output, "05-session-recovery-dialog.png"));
                        Find<TextBox>(dialog, "sessionPassword").Text = credential;
                        dialogSubmitted = true;
                        Click(Field<AntdUI.Button>(dialog, "submit"));
                    }
                    catch (Exception error) { recoveryError = error; dialog.Close(); }
                };
                recovery.Start();
                Click(Find<AntdUI.Button>(form, "recoverSession"));
                recovery.Stop();
            }
            if (recoveryError is not null) throw recoveryError;
            Expect(dialogSubmitted && !notice.Visible && page.Enabled, "The real same-account password dialog restores the session and enabled page.");
            Expect(ReferenceEquals(form.CurrentPage, page) && name.Text == draftName && phone.Text == draftPhone && page.HasUnsavedChanges,
                "Reauthentication preserves the exact same page, draft and unsaved state.");
            using (var unchanged = await repository.ListAsync(studentCode, true))
            {
                var persisted = StudentRepository.FromRow(unchanged.Rows[0]);
                Expect(!persisted.Active && persisted.Name == "Sinh viên kiểm thử giao diện" && persisted.Phone is null,
                    "Conflict, session expiry and password recovery never replay or save the draft automatically.");
            }
            await Shot("06-session-restored-wide");
            await Shot("07-session-restored-compact", 1100);
            await CheckModalRecoveryAsync(form, connectionString, output, credential, Expect);
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = true, dpi = form.DeviceDpi, checks }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            using (var bitmap = UiCapture.Client(form)) bitmap.Save(Path.Combine(output, "failure-state.png"));
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = false, checks, error = error.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.ExitCode = 1;
        }
        finally
        {
            if (added)
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                using var cleanup = new SqlCommand("DELETE dbo.SINH_VIEN WHERE ma_sv=@code", connection);
                cleanup.Parameters.Add("@code", SqlDbType.VarChar, 20).Value = studentCode;
                await cleanup.ExecuteNonQueryAsync();
            }
            if (form.CurrentPage is StudentsPage page)
                typeof(StudentsPage).GetMethod("NewStudent", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null);
            form.Close();
        }
    }

    private static async Task CheckModalRecoveryAsync(CampusForm form, string connectionString, string output,
        string credential, Action<bool, string> expect)
    {
        var snapshot = await new SqlMinhRepository(connectionString).LoadAsync();
        var store = snapshot.CreateStores().Scheduling;
        var original = store.Rooms[0];
        using var editor = new RoomEditor(store, original);
        const string draftBuilding = "Tòa nhà bản nháp chưa lưu";
        var input = Field<AntdUI.Input>(editor, "building");
        input.Text = draftBuilding;
        Exception? failure = null;
        int dialogsSeen = 0;
        var seen = new HashSet<SessionRecoveryDialog>();
        using var recover = new System.Windows.Forms.Timer { Interval = 80 };
        recover.Tick += (_, _) =>
        {
            var dialog = Application.OpenForms.OfType<SessionRecoveryDialog>().FirstOrDefault(window => window.Owner == editor);
            if (dialog is null || !seen.Add(dialog)) return;
            dialogsSeen++;
            try
            {
                if (dialogsSeen == 1) { dialog.Close(); return; }
                if (dialogsSeen != 2) throw new InvalidOperationException("Unexpected repeated modal recovery dialog.");
                using (var image = UiCapture.Client(dialog)) image.Save(Path.Combine(output, "08-modal-retry-recovery-dialog.png"));
                Find<TextBox>(dialog, "sessionPassword").Text = credential;
                Click(Field<AntdUI.Button>(dialog, "submit"));
            }
            catch (Exception error) { failure = error; dialog.Close(); editor.Close(); }
        };
        editor.Shown += async (_, _) =>
        {
            try
            {
                await using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using var expire = new SqlCommand("UPDATE dbo.APP_SESSION SET expires=DATEADD(SECOND,-1,SYSUTCDATETIME()) WHERE ma_tk=@id", connection);
                    expire.Parameters.Add("@id", SqlDbType.Int).Value = int.Parse(form.ConnectedIdentity!.AccountId);
                    await expire.ExecuteNonQueryAsync();
                }
                expect(!editor.TrySave(), "A real modal room editor rejects Save after its session expires.");
                await WaitUntilAsync(() => dialogsSeen == 1 && !Application.OpenForms.OfType<SessionRecoveryDialog>().Any(),
                    "The modal editor did not open and cancel the first recovery dialog.");
                expect(input.Text == draftBuilding && AppSqlConnection.IsBlocked(connectionString),
                    "Canceling modal password recovery keeps the draft and the session blocked.");
                expect(!editor.TrySave(), "Retrying Save from the same modal editor is blocked before writing.");
                await WaitUntilAsync(() => dialogsSeen == 2 && !AppSqlConnection.IsBlocked(connectionString)
                    && !Application.OpenForms.OfType<SessionRecoveryDialog>().Any(),
                    "Retrying a blocked modal Save did not reopen password recovery.");
                expect(input.Text == draftBuilding && editor.Result is null && store.Rooms[0].Building == original.Building,
                    "Modal reauthentication preserves the room draft without committing or replaying Save.");
                expect(!editor.ValidationSummary.Contains("hết hạn", StringComparison.OrdinalIgnoreCase)
                    && editor.ValidationSummary.Contains("Đã đăng nhập lại", StringComparison.Ordinal),
                    "Successful modal recovery replaces the obsolete expiry error with an unsaved-draft confirmation.");
                await Task.Delay(240);
                using (var image = UiCapture.Client(editor)) image.Save(Path.Combine(output, "09-modal-draft-restored.png"));
                var persisted = await new SqlMinhRepository(connectionString).LoadAsync();
                expect(persisted.Rooms.Single(room => room.Id == original.Id).Building == original.Building,
                    "SQL confirms that canceled and retried modal saves never changed the room.");
            }
            catch (Exception error) { failure = error; }
            finally { editor.Close(); }
        };
        recover.Start();
        editor.ShowDialog(form);
        recover.Stop();
        if (failure is not null) throw failure;
    }

    private static async Task WaitUntilAsync(Func<bool> ready, string failure)
    {
        var clock = Stopwatch.StartNew();
        while (!ready())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(20)) throw new InvalidOperationException(failure);
            await Task.Delay(50);
        }
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static T Find<T>(Control parent, string name) where T : Control => parent.Controls.Find(name, true).OfType<T>().Single();
    private static IEnumerable<Control> Children(Control parent) => parent.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Children(child)));
    private static void Click(Control control) => typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [EventArgs.Empty]);
    private static int DismissOwnedWarning(uint thread, nint owner)
    {
        int closed = 0;
        EnumThreadWindows(thread, (window, _) =>
        {
            var className = new StringBuilder(64); GetClassName(window, className, className.Capacity);
            nint dialogOwner = GetWindow(window, 4);
            if ((dialogOwner == owner || GetAncestor(dialogOwner, 2) == owner) && className.ToString() == "#32770")
            { PostMessage(window, 0x0010, 0, 0); closed++; }
            return true;
        }, 0);
        return closed;
    }
    private delegate bool WindowCallback(nint window, nint state);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint thread, WindowCallback callback, nint state);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder text, int maxCount);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint window, uint message, nint word, nint data);
}
