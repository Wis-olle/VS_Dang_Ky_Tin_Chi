using System.Reflection;
using System.Text.Json;
using QuanLyGiangVien.Ui;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        AppTheme.Initialize(); AppTheme.SetScale(96);
        var checks = new List<string>();
        void Expect(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks.Add(message); }
        try
        {
            using var form = new Form { ClientSize = new(760, 560), BackColor = AppTheme.Canvas, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(5000, 0) };
            using var font = AppTheme.Font(9);
            form.Font = font;
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new(20) };
            table.ColumnStyles.Add(new(SizeType.Percent, 50)); table.ColumnStyles.Add(new(SizeType.Percent, 50));
            var input = new TextBox { Name = "txtNativeDraft", Text = "Bản nháp tiếng Việt", Dock = DockStyle.Top, TabIndex = 2, AccessibleName = "Tên chương trình", MaxLength = 100 };
            var password = new TextBox { Name = "txtNativePassword", UseSystemPasswordChar = true, Dock = DockStyle.Top, TabIndex = 3 };
            var multi = new TextBox { Name = "txtNativeNotes", Multiline = true, Height = 90, Dock = DockStyle.Top, ScrollBars = ScrollBars.Vertical, Text = "Dòng 1\r\nDòng 2" };
            var output = new TextBox { Name = "txtNativeOutput", Multiline = true, ReadOnly = true, Height = 104, Dock = DockStyle.Bottom, Visible = false };
            var combo = new ComboBox { Name = "cboNativeChoice", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
            var date = new DateTimePicker { Name = "dtpNativeDate", ShowCheckBox = true, Value = new(2008, 10, 20), Checked = false, Dock = DockStyle.Top };
            combo.Items.AddRange(["Kỳ một", "Kỳ hai"]); combo.SelectedIndex = 1;
            table.Controls.Add(input, 0, 0); table.SetColumnSpan(input, 2);
            table.Controls.Add(password, 0, 1); table.Controls.Add(combo, 1, 1); table.Controls.Add(multi, 0, 2);
            table.Controls.Add(date, 1, 2);
            form.Controls.Add(table); form.Controls.Add(output); form.Show(); Application.DoEvents();
            var edits = 0; input.TextChanged += (_, _) => edits++;
            var style = typeof(AppTheme).Assembly.GetType("QuanLyGiangVien.Features.Registration.ShowcaseStyle")!.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic)!;
            style.Invoke(null, [form]); Application.DoEvents();
            Expect(input.Text == "Bản nháp tiếng Việt" && edits == 0, "Applying presentation preserves the draft and raises no edit event.");
            Expect(input.Parent != table && input.Parent!.Height >= 34, "Single-line field has a padded surface at least 34px high.");
            var surface = input.Parent!;
            Expect(table.GetColumnSpan(surface) == 2 && table.GetCellPosition(surface) == new TableLayoutPanelCellPosition(0, 0), "Spanning field retains its original table cell and span.");
            Expect(surface.TabIndex == 2 && input.CanSelect && input.AccessibleName == "Tên chương trình", "Tab sequence, native focus and accessible label survive presentation.");
            Expect(password.UseSystemPasswordChar && password.CanSelect, "Password masking and native input remain available.");
            Expect(combo.SelectedIndex == 1 && combo.DropDownStyle == ComboBoxStyle.DropDownList, "Choice selection and dropdown behavior are preserved.");
            combo.FlatStyle = FlatStyle.Standard; combo.FlatStyle = FlatStyle.Flat; Application.DoEvents();
            Expect(combo.IsHandleCreated && combo.SelectedIndex == 1 && combo.CanSelect, "Recreating the native combo handle preserves selection and focusability.");
            combo.DroppedDown = true; Application.DoEvents();
            Expect(combo.DroppedDown, "The wrapped native combo can open its actual popup.");
            combo.DroppedDown = false;
            Expect(date.Value == new DateTime(2008, 10, 20) && !date.Checked && date.ShowCheckBox && date.Parent != table, "Date field retains its optional checkbox and original date inside the padded surface.");
            date.Checked = true; date.Value = new(2009, 10, 21); Application.DoEvents();
            Expect(date.Checked && date.Value == new DateTime(2009, 10, 21) && date.CanSelect, "Native date selection remains editable after presentation.");
            Expect(multi.Multiline && multi.ScrollBars == ScrollBars.Vertical && multi.Text.Contains("Dòng 2"), "Multiline input keeps its content and scrollbar.");
            Expect(!output.Visible && output.Parent == form, "Hidden read-only output keeps its own visibility and reserves no empty field surface.");
            output.Visible = true; output.Height = 48; Application.DoEvents();
            Expect(output.Height == 48 && output.Parent == form, "Responsive read-only output can shrink from 104 to 48px.");
            output.Visible = false; Application.DoEvents();
            style.Invoke(null, [form]); Application.DoEvents();
            Expect(input.Parent == surface && table.Controls.Count == 5, "Repeated styling does not nest fields or duplicate table cells.");
            input.Select(0, 3); input.SelectedText = "Nội dung";
            Expect(edits == 1 && input.Text.StartsWith("Nội dung"), "Native selection editing still raises exactly one text-change event.");
            input.ReadOnly = true; Application.DoEvents();
            Expect(input.ReadOnly && input.Text.StartsWith("Nội dung"), "Read-only transition keeps edited content.");
            form.ClientSize = new(640, 500); Application.DoEvents();
            Expect(surface.Right <= table.ClientSize.Width && input.Right <= surface.ClientSize.Width && multi.Height >= 60, "Resize keeps fields and multiline content inside their surfaces.");
            using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height)) { form.DrawToBitmap(bitmap, form.ClientRectangle); bitmap.Save(Path.Combine(args[0], "inputs.png")); }
            File.WriteAllText(Path.Combine(args[0], "results.json"), JsonSerializer.Serialize(new { passed = true, checks }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"PASS {checks.Count} native input checks");
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(args[0], "results.json"), JsonSerializer.Serialize(new { passed = false, checks, error = e.ToString() }, new JsonSerializerOptions { WriteIndented = true })); Console.WriteLine(e.Message); Environment.ExitCode = 1; }
        finally { AppTheme.Release(); }
    }
}
