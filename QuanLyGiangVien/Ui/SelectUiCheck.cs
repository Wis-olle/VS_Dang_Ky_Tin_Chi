using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;

namespace QuanLyGiangVien.Ui;

/// <summary>Regression checks on the actual selects, popup windows and the page's native clock.</summary>
internal static class SelectUiCheck
{
    internal static async Task Run(MainForm form, string folder, Action<bool, string> expect)
    {
        var controls = Descendants(form).OfType<AnimatedSelect>().ToArray();
        expect(controls.Length == 2, "Both toolbar filters use the native animated select");
        var faculty = controls.Single(c => c.AccessibleName == "Lọc theo khoa");
        var status = controls.Single(c => c.AccessibleName == "Lọc theo trạng thái");
        var clock = PageMotionCoordinator.Current!;
        form.SetFiltersForCheck("");
        await Task.Delay(400);
        var bounds = controls.Select(c => c.Bounds).ToArray();
        int events = 0;
        void Count(object? sender, EventArgs e) => events++;
        faculty.SelectedIndexChanged += Count;
        try
        {
            Click(faculty);
            var popup = faculty.PopupForCheck!;
            expect(faculty.IsDropDownOpen && popup.Visible && popup.IsAnimating && popup.Opacity < 1,
                "Click opens a fading native popup on the shared clock");
            MouseClick(popup, 1);
            expect(faculty.SelectedIndex == 0 && faculty.IsDropDownOpen, "Pointer selection waits for reveal to settle so sliding rows cannot select a neighbor");
            var popupBounds = popup.Bounds;
            await Task.Delay(75);
            expect(faculty.CaretProgress is > 0 and < 1 && popup.RevealProgress is > 0 and < 1,
                "Caret rotation and dropdown reveal have intermediate float frames");
            expect(popup.Bounds == popupBounds && faculty.Bounds == bounds[0], "Reveal keeps popup and trigger pointer bounds fixed");
            await Task.Delay(240);
            expect(faculty.CaretProgress == 1 && popup.RevealProgress == 1 && !popup.IsAnimating, "Open motion settles at 180 degrees and full opacity");
            expect(popup.AccessibilityObject.GetChildCount() == 5 && popup.AccessibilityObject.GetChild(1)?.Name == "Công nghệ thông tin",
                "Popup exposes named option children for assistive technology");
            expect(popup.Left >= Screen.FromControl(faculty).WorkingArea.Left && popup.Right <= Screen.FromControl(faculty).WorkingArea.Right &&
                popup.Top >= Screen.FromControl(faculty).WorkingArea.Top && popup.Bottom <= Screen.FromControl(faculty).WorkingArea.Bottom,
                "Dropdown stays within the monitor working area");
            Capture(form, folder, "21-animated-select");
            faculty.HandleSelectionKey(Keys.Down);
            expect(popup.HighlightedIndex == 1 && faculty.SelectedIndex == 0 && events == 0,
                "Arrow navigation highlights an option without changing the filter");
            faculty.HandleSelectionKey(Keys.Enter);
            expect(form.ResultCount == 4 && faculty.SelectedValue == "Công nghệ thông tin" && events == 1,
                "Enter commits one selection event and applies faculty filtering");
            expect(faculty.AccessibilityObject.Value == "Công nghệ thông tin" && faculty.LabelPosition < 1,
                "Accessible value updates immediately while the label rolls");
            await Task.Delay(95);
            expect(faculty.LabelPosition is > 0 and < 1, "Selected text rolls using fractional coordinates");
            await Task.Delay(300);
            expect(faculty.LabelPosition == 1 && faculty.CaretProgress == 0 && !faculty.HasPopup && !faculty.HasRunningMotion,
                "Selection finishes and disposes the popup and finite motion jobs");
            faculty.HandleSelectionKey(Keys.Space);
            faculty.HandleSelectionKey(Keys.End);
            expect(faculty.PopupForCheck!.HighlightedIndex == 4, "End navigates to the last option");
            faculty.HandleSelectionKey(Keys.Home);
            expect(faculty.PopupForCheck!.HighlightedIndex == 0, "Home navigates to the first option");
            faculty.HandleSelectionKey(Keys.Escape);
            await Task.Delay(250);
            expect(faculty.SelectedIndex == 1 && events == 1 && !faculty.HasPopup, "Escape cancels navigation without a selection event");
            faculty.OpenDropDown(); await Task.Delay(65);
            var interruptedPopup = faculty.PopupForCheck!;
            faculty.CloseDropDown(); await Task.Delay(30);
            faculty.OpenDropDown();
            await Task.Delay(260);
            expect(faculty.IsDropDownOpen && faculty.PopupForCheck == interruptedPopup && faculty.CaretProgress == 1,
                "Rapid close and reopen reverses the current motion without replacing the popup");
            var outsideAfterReopen = Message.Create(form.Handle, 0x0201, IntPtr.Zero, IntPtr.Zero);
            Application.FilterMessage(ref outsideAfterReopen);
            expect(!faculty.IsDropDownOpen, "Rapid reopen restores the outside-click message filter");
            await Task.Delay(250);
            faculty.OpenDropDown();
            Click(status);
            expect(!faculty.HasPopup && status.IsDropDownOpen, "Opening another select immediately disposes the previous popup");
            status.HandleSelectionKey(Keys.End);
            status.HandleSelectionKey(Keys.Enter);
            expect(status.SelectedValue == "Tạm nghỉ" && form.ResultCount == 1, "Both selected filters preserve the combined query");
            await Task.Delay(360);
            status.OpenDropDown();
            await Task.Delay(240);
            MouseClick(status.PopupForCheck!, 0);
            expect(status.SelectedIndex == 0 && form.ResultCount == 4, "Native popup mouse-up commits the clicked row and refreshes the filter");
            await Task.Delay(360);
            status.OpenDropDown();
            bool tabConsumed = status.HandleSelectionKey(Keys.Tab);
            expect(!tabConsumed && !status.HasPopup, "Tab closes the popup and allows normal focus traversal");
            faculty.OpenDropDown();
            var outside = Message.Create(form.Handle, 0x0201, IntPtr.Zero, IntPtr.Zero);
            expect(!faculty.PopupForCheck!.PreFilterMessage(ref outside) && !faculty.IsDropDownOpen,
                "Outside mouse click closes the popup without consuming the original click");
            await Task.Delay(250);
            faculty.OpenDropDown();
            form.ClientSize = new(form.ClientSize.Width - 1, form.ClientSize.Height);
            expect(!faculty.HasPopup, "Window resize closes the popup before its anchor can become stale");
            form.ClientSize = new(form.ClientSize.Width + 1, form.ClientSize.Height);
            form.SetFiltersForCheck("", "Công nghệ thông tin");
            await Task.Delay(360);
            faculty.OpenDropDown();
            faculty.SelectedIndex = 3;
            clock.SetReducedMotion(true);
            expect(faculty.CaretProgress == 1 && faculty.LabelPosition == 3 && !faculty.HasRunningMotion,
                "Reduced motion completes active select animations immediately");
            faculty.CloseDropDown();
            expect(!faculty.HasPopup && faculty.CaretProgress == 0, "Reduced motion closes and disposes without a fade");
            faculty.OpenDropDown();
            expect(faculty.CaretProgress == 1 && faculty.PopupForCheck!.Opacity == 1 && !faculty.HasRunningMotion,
                "Reduced motion opens at the final visual state");
            faculty.CloseDropDown(true);
            clock.SetReducedMotion(false);
            form.SetFiltersForCheck("");
            await Task.Delay(400);
            expect(controls.Select(c => c.Bounds).SequenceEqual(bounds), "Filter selection preserves both original trigger bounds");
            using (var disposable = new AnimatedSelect { Font = faculty.Font, Bounds = new(20, 40, 180, 42) })
            {
                disposable.SetItems(["Một", "Hai"]);
                form.Controls.Add(disposable);
                disposable.OpenDropDown();
                var ownedPopup = disposable.PopupForCheck!;
                var retainedOption = ownedPopup.AccessibilityObject.GetChild(1)!;
                disposable.Dispose();
                expect(ownedPopup.IsDisposed && !disposable.HasRunningMotion, "Disposing an active select disposes its popup and shared-clock jobs");
                expect(retainedOption.Name == "Hai" && retainedOption.Bounds.IsEmpty && retainedOption.State.HasFlag(AccessibleStates.Invisible),
                    "Retained accessible options remain safe after popup disposal");
            }
            await CaptureMotion(form, faculty, status, folder);
        }
        finally
        {
            faculty.SelectedIndexChanged -= Count;
            faculty.CloseDropDown(true); status.CloseDropDown(true);
            clock.SetReducedMotion(false);
            form.SetFiltersForCheck("");
        }
    }
    private static async Task CaptureMotion(MainForm form, AnimatedSelect faculty, AnimatedSelect status, string folder)
    {
        string frames = Path.Combine(folder, "select-motion-frames");
        Directory.CreateDirectory(frames);
        var timestamps = new List<long>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int frame = 0; frame < 48; frame++)
        {
            if (frame == 1) Click(faculty);
            if (frame == 12) { faculty.HandleSelectionKey(Keys.End); faculty.HandleSelectionKey(Keys.Enter); }
            if (frame == 24) Click(status);
            if (frame == 36) { status.HandleSelectionKey(Keys.End); status.HandleSelectionKey(Keys.Enter); }
            timestamps.Add(watch.ElapsedMilliseconds);
            using var full = Capture(form);
            var left = form.PointToClient(faculty.PointToScreen(Point.Empty));
            var right = form.PointToClient(status.PointToScreen(Point.Empty));
            var region = Rectangle.Intersect(new(left.X - AppTheme.S(12), left.Y - AppTheme.S(8),
                right.X + status.Width - left.X + AppTheme.S(24), AppTheme.S(235)), new(Point.Empty, full.Size));
            using var crop = full.Clone(region, PixelFormat.Format32bppArgb);
            crop.Save(Path.Combine(frames, $"frame-{frame:00}.png"), ImageFormat.Png);
            await Task.Delay(30);
        }
        File.WriteAllText(Path.Combine(frames, "timings.json"), JsonSerializer.Serialize(timestamps));
    }
    private static void Click(Control control) => typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [EventArgs.Empty]);
    private static void MouseClick(SelectPopup popup, int row) => typeof(SelectPopup).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(popup, [new MouseEventArgs(MouseButtons.Left, 1, AppTheme.S(12), AppTheme.S(14 + row * 28 + 14), 0)]);
    private static void Capture(MainForm form, string folder, string name)
    {
        using var bitmap = Capture(form);
        bitmap.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png);
    }
    private static Bitmap Capture(MainForm form)
    {
        var bitmap = UiCapture.Client(form);
        using var canvas = Graphics.FromImage(bitmap);
        foreach (var popup in form.OwnedForms.OfType<SelectPopup>().Where(p => p.Visible))
        {
            using var surface = new Bitmap(popup.Width, popup.Height);
            popup.DrawToBitmap(surface, popup.ClientRectangle);
            using var alpha = new ImageAttributes();
            alpha.SetColorMatrix(new ColorMatrix { Matrix33 = (float)popup.Opacity });
            var location = form.PointToClient(popup.Location);
            var state = canvas.Save();
            if (popup.Region != null)
            {
                using var region = popup.Region.Clone();
                region.Translate(location.X, location.Y);
                canvas.SetClip(region, System.Drawing.Drawing2D.CombineMode.Intersect);
            }
            canvas.DrawImage(surface, new Rectangle(location, surface.Size), 0, 0, surface.Width, surface.Height, GraphicsUnit.Pixel, alpha);
            canvas.Restore(state);
        }
        return bitmap;
    }
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
