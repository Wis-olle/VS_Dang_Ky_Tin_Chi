using System.Data;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Features.Registration;
using QuanLyGiangVien.Features.Shell;

namespace QuanLyGiangVien.Ui;

/// <summary>Opt-in component checks; no authentication, SQL execution or business writes.</summary>
internal static class UnifiedSelectUiCheck
{
    internal static void Attach(CampusForm form, string connection)
    {
        var output = Environment.GetEnvironmentVariable("CAMPUS_UNIFIED_SELECT_UI_CHECK");
        if (string.IsNullOrWhiteSpace(output)) return;
        var target = new SqlConnectionStringBuilder(connection);
        if (!Regex.IsMatch(target.InitialCatalog, @"^DangKyTinChi_Minh_Test_Showcase_[a-f0-9]{32}$", RegexOptions.CultureInvariant)
            || target.DataSource is not ("." or "localhost" or "(local)"))
            throw new InvalidOperationException("Unified select checks require a local dedicated Showcase UUID.");
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new(SystemInformation.VirtualScreen.Right + 60, 0);
        form.Shown += async (_, _) => await Run(form, output);
    }

    private static async Task Run(CampusForm owner, string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        var errors = new List<string>();
        var screenshots = new List<string>();
        void Expect(bool condition, string description)
        {
            if (condition) checks.Add(description); else errors.Add(description);
        }
        async Task Test(string name, Func<Task> action)
        {
            try { await action(); }
            catch (Exception error) { errors.Add(name + ": " + error.GetType().Name + ": " + error.Message); }
        }
        AnimatedSelect? Facade(ComboBox source)
        {
            var candidates = source.Parent?.Controls.OfType<AnimatedSelect>().ToArray() ?? [];
            Expect(candidates.Length == 1, source.Name + " retains the original ComboBox and has exactly one AnimatedSelect facade.");
            return candidates.Length == 1 ? candidates[0] : null;
        }
        async Task Settle(Control control)
        {
            control.Parent?.PerformLayout(); control.Invalidate(true); control.Update();
            await Task.Delay(100);
        }
        using var font = AppTheme.Font(10);
        using var form = new Form
        {
            Text = "Unified select native component verification", Font = font, AutoScaleMode = AutoScaleMode.None,
            StartPosition = FormStartPosition.Manual, Location = new(SystemInformation.VirtualScreen.Right + 100, 40),
            ClientSize = new(AppTheme.S(700), AppTheme.S(780)), BackColor = AppTheme.Canvas,
            ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.FixedToolWindow
        };
        var rows = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(AppTheme.S(20)), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        form.Controls.Add(rows);
        ComboBox Add(string name, string title, params object[] items)
        {
            var panel = new Panel { Width = AppTheme.S(650), Height = AppTheme.S(82), Margin = Padding.Empty };
            var label = new Label { Text = title, AutoSize = true, ForeColor = AppTheme.Ink, Location = new(0, 0) };
            var combo = new ComboBox
            {
                Name = name, AccessibleName = title, DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new(0, AppTheme.S(28)), Width = AppTheme.S(380), TabIndex = rows.Controls.Count,
                Font = font, FormattingEnabled = true
            };
            combo.Items.AddRange(items);
            if (items.Length > 0) combo.SelectedIndex = 0;
            panel.Controls.Add(label); panel.Controls.Add(combo); rows.Controls.Add(panel);
            return combo;
        }
        var plain = Add("plain", "Bộ lọc loại học phần", "Tất cả loại", "Bắt buộc", "Tự chọn");
        var bound = Add("bound", "Học kỳ có DisplayMember / ValueMember");
        var formatted = Add("formatted", "Tuần dùng sự kiện Format", new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 26));
        var empty = Add("empty", "Danh sách rỗng / chưa chọn");
        var mutable = Add("mutable", "Items.Add / Remove / Clear", "Một", "Hai");
        var parent = Add("cascadeParent", "Khoa → ngành", "Khoa A", "Khoa B");
        var child = Add("cascadeChild", "Ngành theo khoa", "Ngành A1", "Ngành A2");
        var next = new Button { Text = "Điểm focus kế tiếp", Width = AppTheme.S(220), Height = AppTheme.S(36) };
        rows.Controls.Add(next);
        using var data = Choices(("HK1", "Học kỳ một"), ("HK2", "Học kỳ hai"));
        using var replacement = Choices(("HK3", "Học kỳ hè"), ("HK4", "Học kỳ kế tiếp"));
        bound.DisplayMember = "caption"; bound.ValueMember = "code"; bound.DataSource = data;
        formatted.Format += (_, args) => { if (args.ListItem is DateOnly date) args.Value = $"Tuần {date:dd/MM/yyyy}"; };
        int cascadeEvents = 0;
        parent.SelectedIndexChanged += (_, _) =>
        {
            cascadeEvents++;
            child.Items.Clear();
            child.Items.AddRange(parent.SelectedIndex == 1 ? ["Ngành B1", "Ngành B2", "Ngành B3"] : ["Ngành A1", "Ngành A2"]);
            child.SelectedIndex = child.Items.Count - 1;
        };
        try
        {
            form.Show(owner); await Task.Delay(100); plain.Focus();
            Capture(form, output, "01-native-before", screenshots);
            ShowcaseStyle.Apply(form); await Settle(form);
            Capture(form, output, "02-unified-idle", screenshots);

            await Test("plain and focus", async () =>
            {
                var face = Facade(plain); if (face is null) return;
                Expect(face.Items.SequenceEqual(plain.Items.Cast<object>().Select(plain.GetItemText)), "Plain labels match source captions without replacing its items.");
                Expect(face.SelectedIndex == plain.SelectedIndex && face.Text == plain.Text, "Facade initially matches native selection and text.");
                Expect(!plain.Bounds.IntersectsWith(face.Bounds) && plain.Visible,
                    "The native blue selection and gray arrow cannot paint over the visible facade; its binding model stays logically visible.");
                Expect(face.AccessibleRole == AccessibleRole.ComboBox && face.AccessibleName == plain.AccessibleName, "Facade exposes the source's named combobox accessibility role.");
                face.Focus(); await Task.Delay(50);
                Expect(face.Focused && !plain.Focused, "Visible facade owns native focus instead of the blue native text selection.");
                Capture(form, output, "03-unified-focused", screenshots);
                face.OpenDropDown(); await Task.Delay(260);
                Expect(face.HasPopup && face.IsDropDownOpen, "Focused facade opens a native owned popup.");
                Capture(form, output, "04-unified-popup", screenshots, face);
                Expect(face.PopupForCheck?.AccessibilityObject.GetChildCount() == plain.Items.Count, "Popup exposes all source options as accessible children.");
                face.CloseDropDown(true);
            });

            await Test("programmatic native selection", async () =>
            {
                var face = Facade(plain); if (face is null) return;
                int changed = 0, committed = 0;
                void Changed(object? sender, EventArgs args) => changed++;
                void Committed(object? sender, EventArgs args) => committed++;
                plain.SelectedIndexChanged += Changed; plain.SelectionChangeCommitted += Committed;
                try
                {
                    plain.SelectedIndex = 1; await Settle(plain);
                    Expect(changed == 1 && committed == 0, "Programmatic native selection emits one change and no user-commit event.");
                    Expect(face.SelectedIndex == 1 && face.Text == plain.Text, "Programmatic selection immediately updates the facade model.");
                    plain.SelectedIndex = -1; await Settle(plain);
                    Expect(face.SelectedIndex == -1 && face.Text.Length == 0 && plain.SelectedIndex == -1, "Native SelectedIndex -1 remains unselected instead of choosing the first item.");
                    plain.SelectedIndex = 0; await Settle(plain);
                }
                finally { plain.SelectedIndexChanged -= Changed; plain.SelectionChangeCommitted -= Committed; }
            });

            await Test("native binding", async () =>
            {
                var face = Facade(bound); if (face is null) return;
                bound.SelectedValue = "HK2"; await Settle(bound);
                Expect(ReferenceEquals(bound.DataSource, data) && bound.DisplayMember == "caption" && bound.ValueMember == "code", "Binding metadata and the original DataTable remain intact.");
                Expect(Convert.ToString(bound.SelectedValue) == "HK2" && face.Text == "Học kỳ hai", "SelectedValue maps to the displayed caption rather than DataRowView.ToString.");
                data.Rows[1]["caption"] = "Học kỳ hai đã cập nhật"; await Settle(bound);
                Expect(face.Items[1] == bound.GetItemText(bound.Items[1]), "DataRow caption changes update the facade's formatted item list.");
                bound.DataSource = replacement; bound.SelectedValue = "HK4"; await Settle(bound);
                Expect(ReferenceEquals(bound.DataSource, replacement) && Convert.ToString(bound.SelectedValue) == "HK4" && face.Text == "Học kỳ kế tiếp", "Rebinding preserves source identity, selected value and current caption.");
                bound.DataSource = null; bound.Items.Clear(); await Settle(bound);
                Expect(face.Items.Count == 0 && face.SelectedIndex == -1, "Unbinding and clearing do not fabricate an option or selection.");
            });

            await Test("formatted captions", async () =>
            {
                var face = Facade(formatted); if (face is null) return;
                formatted.SelectedIndex = 1; await Settle(formatted);
                Expect(face.Text == "Tuần 26/10/2026" && face.Items[0] == "Tuần 19/10/2026", "Source Format events control both selected and popup captions.");
            });

            await Test("empty and mutable items", async () =>
            {
                var emptyFace = Facade(empty); var face = Facade(mutable);
                if (emptyFace is not null)
                {
                    Expect(emptyFace.Items.Count == 0 && emptyFace.SelectedIndex == -1, "A truly empty source is represented by an empty facade.");
                    emptyFace.OpenDropDown(); Expect(!emptyFace.IsDropDownOpen, "Empty sources do not open a phantom popup.");
                    empty.Items.Add(""); empty.SelectedIndex = 0; await Settle(empty);
                    Expect(emptyFace.Items.Count == 1 && emptyFace.SelectedIndex == 0 && emptyFace.Text.Length == 0, "An intentionally blank caption is a valid source item.");
                }
                if (face is null) return;
                mutable.Items.Add("Ba"); await Settle(mutable);
                Expect(face.Items.SequenceEqual(["Một", "Hai", "Ba"]), "Items.Add updates popup choices without rebuilding the original ComboBox.");
                mutable.SelectedIndex = 2; mutable.Items.RemoveAt(0); await Settle(mutable);
                Expect(face.Items.Count == 2 && face.SelectedIndex == mutable.SelectedIndex && face.Text == mutable.Text, "Items.Remove preserves the native source's resulting selection.");
                mutable.Items.Clear(); await Settle(mutable);
                Expect(face.Items.Count == 0 && face.SelectedIndex == -1, "Items.Clear removes cached facade labels and selection.");
                mutable.Items.AddRange(["Một", "Hai"]); mutable.SelectedIndex = 0; await Settle(mutable);
            });

            await Test("keyboard and commit events", async () =>
            {
                var face = Facade(plain); if (face is null) return;
                plain.SelectedIndex = 0; await Settle(plain);
                int changed = 0, committed = 0;
                void Changed(object? sender, EventArgs args) => changed++;
                void Committed(object? sender, EventArgs args) => committed++;
                plain.SelectedIndexChanged += Changed; plain.SelectionChangeCommitted += Committed;
                try
                {
                    face.Focus(); face.HandleSelectionKey(Keys.Enter); await Task.Delay(250);
                    face.HandleSelectionKey(Keys.Down);
                    Expect(plain.SelectedIndex == 0 && changed == 0 && committed == 0, "Popup keyboard highlight leaves the source unchanged until commit.");
                    face.HandleSelectionKey(Keys.Enter); await Settle(plain);
                    Expect(plain.SelectedIndex == 1 && changed == 1 && committed == 1, "Enter commits exactly one native selection-change and one user-commit event.");
                    face.HandleSelectionKey(Keys.Space); face.HandleSelectionKey(Keys.End); face.HandleSelectionKey(Keys.Escape); await Task.Delay(250);
                    Expect(plain.SelectedIndex == 1 && changed == 1 && committed == 1 && !face.HasPopup, "Escape cancels popup navigation without native change or commit events.");
                    face.OpenDropDown(); bool consumed = face.HandleSelectionKey(Keys.Tab);
                    Expect(!consumed && !face.HasPopup, "Tab disposes the popup and leaves normal focus traversal available.");
                    form.SelectNextControl(face, true, true, true, true);
                    Expect(!plain.Focused, "Tab traversal does not focus the covered native blue selection face.");
                }
                finally { plain.SelectedIndexChanged -= Changed; plain.SelectionChangeCommitted -= Committed; }
            });

            await Test("cascaded source events", async () =>
            {
                var parentFace = Facade(parent); var childFace = Facade(child);
                if (parentFace is null || childFace is null) return;
                parent.SelectedIndex = 1; await Settle(parent);
                Expect(cascadeEvents == 1 && child.Items.Count == 3 && child.SelectedIndex == 2, "One source selection executes the existing cascade handler exactly once.");
                Expect(childFace.Items.SequenceEqual(["Ngành B1", "Ngành B2", "Ngành B3"]) && childFace.SelectedIndex == 2, "Dependent facade reads the final cascade item list and selection.");
            });

            await Test("native popup requests and type-ahead", async () =>
            {
                var face = Facade(plain); if (face is null) return;
                plain.Focus(); await Settle(plain);
                Expect(face.Focused && !plain.Focused, "Calling Focus on the source redirects focus to the visible select.");
                plain.DroppedDown = true; await Task.Delay(250);
                Expect(face.IsDropDownOpen && plain.DroppedDown, "A native dropdown request and state query use the shared lecturer popup.");
                plain.DroppedDown = false;
                Expect(!face.HasPopup, "Closing the native dropdown request disposes the shared popup.");
                face.HandleSelectionKey(Keys.F4);
                Expect(face.IsDropDownOpen, "F4 opens the shared popup.");
                face.HandleSelectionKey(Keys.F4); await Task.Delay(250);
                Expect(!face.HasPopup, "F4 closes the shared popup.");
                typeof(AnimatedSelect).GetMethod("OnKeyPress", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(face, [new KeyPressEventArgs('B')]);
                Expect(plain.Text == "Bắt buộc", "Typing a prefix selects the matching native option through the facade.");
            });

            await Test("unselected popup and shared prefixes", async () =>
            {
                var face = Facade(parent); if (face is null) return;
                parent.SelectedIndex = -1; await Settle(parent);
                face.HandleSelectionKey(Keys.F4); face.HandleSelectionKey(Keys.Enter); await Task.Delay(250);
                Expect(parent.SelectedIndex == -1 && !face.IsDropDownOpen, "Enter on an untouched unselected popup does not invent a first selection.");
                face.HandleSelectionKey(Keys.F4); face.HandleSelectionKey(Keys.Down); face.HandleSelectionKey(Keys.Enter); await Settle(parent);
                Expect(parent.SelectedIndex == 0, "Down from an unselected popup highlights and commits the first option.");
                var keypress = typeof(AnimatedSelect).GetMethod("OnKeyPress", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                keypress.Invoke(face, [new KeyPressEventArgs('K')]);
                int firstMatch = parent.SelectedIndex;
                foreach (char key in "hoa") keypress.Invoke(face, [new KeyPressEventArgs(key)]);
                Expect(parent.SelectedIndex == firstMatch, "Extending a common prefix keeps its current match instead of cycling through identical prefixes.");
            });

            await Test("enabled, geometry and disposal", async () =>
            {
                var face = Facade(plain); if (face is null) return;
                plain.Enabled = false; await Settle(plain); var previous = plain.SelectedIndex;
                face.OpenDropDown(); face.HandleSelectionKey(Keys.Down);
                Expect(!face.Enabled && !face.HasPopup && plain.SelectedIndex == previous, "Disabled source disables its facade and blocks pointer/keyboard changes.");
                Capture(form, output, "05-unified-disabled", screenshots);
                plain.Enabled = true; await Settle(plain);
                face.Focus(); face.OpenDropDown(); form.ClientSize = new(form.ClientSize.Width - AppTheme.S(20), form.ClientSize.Height);
                Expect(!face.HasPopup, "Owner resize closes the popup before its anchor becomes stale.");
                face.OpenDropDown(); plain.Parent!.Location = new(plain.Parent.Left + AppTheme.S(1), plain.Parent.Top);
                Expect(!face.HasPopup, "Moving the source host disposes the anchored popup.");
                Expect(plain.Name == "plain" && plain.DropDownStyle == ComboBoxStyle.DropDownList && plain.Items.Count == 3,
                    "Styling preserves the exact native model control and its behavior metadata.");
                mutable.Dispose(); await Settle(form);
                Expect(mutable.IsDisposed, "Disposing the original model remains safe after facade creation.");
                Capture(form, output, "06-unified-final", screenshots);
            });
        }
        catch (Exception error) { errors.Add("harness: " + error.GetType().Name + ": " + error.Message); }
        finally
        {
            foreach (var face in Descendants(form).OfType<AnimatedSelect>().ToArray()) face.CloseDropDown(true);
            File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new
            {
                passed = errors.Count == 0, checks, errors, screenshots,
                scope = "Owned native component form with original ComboBox models; no SQL calls or authentication. Offscreen 96-DPI renders and native control event/key dispatch, not physical input or all-DPI certification. Popup composite uses anchor-relative placement for visual review."
            }, new JsonSerializerOptions { WriteIndented = true }));
            if (errors.Count > 0) File.WriteAllLines(Path.Combine(output, "error.txt"), errors);
            form.Close(); owner.Close();
        }
    }

    private static DataTable Choices(params (string Code, string Caption)[] items)
    {
        var table = new DataTable(); table.Columns.Add("code"); table.Columns.Add("caption");
        foreach (var item in items) table.Rows.Add(item.Code, item.Caption);
        return table;
    }

    private static void Capture(Form form, string output, string name, List<string> screenshots, AnimatedSelect? anchor = null)
    {
        using var bitmap = UiCapture.Client(form);
        if (anchor?.PopupForCheck is { Visible: true } popup)
        {
            using var surface = new Bitmap(popup.Width, popup.Height);
            popup.DrawToBitmap(surface, popup.ClientRectangle);
            surface.Save(Path.Combine(output, name + "-popup-native.png"));
            screenshots.Add(name + "-popup-native.png");
            using var graphics = Graphics.FromImage(bitmap);
            using var alpha = new ImageAttributes(); alpha.SetColorMatrix(new ColorMatrix { Matrix33 = (float)popup.Opacity });
            var position = form.PointToClient(anchor.PointToScreen(new Point(0, anchor.Height)));
            position.X = Math.Clamp(position.X, 0, Math.Max(0, bitmap.Width - surface.Width));
            position.Y = Math.Clamp(position.Y, 0, Math.Max(0, bitmap.Height - surface.Height));
            graphics.DrawImage(surface, new Rectangle(position, surface.Size), 0, 0, surface.Width, surface.Height, GraphicsUnit.Pixel, alpha);
        }
        bitmap.Save(Path.Combine(output, name + ".png")); screenshots.Add(name + ".png");
    }

    private static IEnumerable<Control> Descendants(Control root) =>
        root.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descendants(child)));
}
