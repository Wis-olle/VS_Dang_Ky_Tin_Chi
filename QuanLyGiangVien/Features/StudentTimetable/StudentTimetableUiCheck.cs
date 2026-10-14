using System.Text.Json;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.StudentTimetable;

/// <summary>Opt-in native harness; captures only this form, never the user's desktop.</summary>
internal static class StudentTimetableUiCheck
{
    internal static void Attach(StudentTimetableForm form)
    {
        var output = Environment.GetEnvironmentVariable("STUDENT_TIMETABLE_UI_CHECK");
        if (string.IsNullOrWhiteSpace(output)) return;
        form.Shown += async (_, _) =>
        {
            var checks = new List<string>();
            void Expect(bool ok, string name) { if (!ok) throw new InvalidOperationException(name); checks.Add(name); }
            async Task Shot(string name)
            {
                await Task.Delay(450);
                using var bitmap = UiCapture.Client(form);
                bitmap.Save(Path.Combine(output, name + ".png"));
                WriteLayout(form, Path.Combine(output, name + "-layout.json"));
                if (name == "01-student-timetable")
                {
                    using var header = UiCapture.Client(form.HeaderSurface);
                    header.Save(Path.Combine(output, "06-navigation.png"));
                }
            }
            try
            {
                Directory.CreateDirectory(output);
                form.Location = new(SystemInformation.VirtualScreen.Right + 50, SystemInformation.VirtualScreen.Top);
                form.ClientSize = new(AppTheme.S(1440), AppTheme.S(1040));
                await Task.Delay(700);
                form.SelectDate(new(2026, 10, 14));
                await Task.Delay(250);
                Expect(form.Snapshot.Classes.Length == 4, "Only four classes registered by the demo student");
                Expect(form.Snapshot.TotalCredits == 12, "Credits derived from registered courses");
                Expect(form.Calendar.Cards.Count == 6, "Six scheduled sessions in the active semester week");
                Expect(!form.HasHorizontalScroll, "Wide form has no horizontal page scroll");
                Expect(form.Calendar.Cards.All(c => !string.IsNullOrWhiteSpace(c.AccessibleName) && c.TabStop), "Every session has native keyboard focus and accessible details");
                Expect(form.Calendar.Cards.All(c => form.Calendar.ClientRectangle.Contains(c.Bounds)), "All session bounds inside the calendar");
                var original = form.Snapshot.Occurrences[0];
                var single = original with { Meeting = original.Meeting with { EndPeriod = original.Meeting.StartPeriod } };
                using (var shortA = new StudentWeekCalendar.SessionCard(single, "Môn A", "Giảng viên"))
                using (var shortB = new StudentWeekCalendar.SessionCard(single, "Môn B", "Giảng viên"))
                {
                    shortA.Size = shortB.Size = new(AppTheme.S(180), AppTheme.S(30));
                    using var a = new Bitmap(shortA.Width, shortA.Height);
                    using var b = new Bitmap(shortB.Width, shortB.Height);
                    shortA.DrawToBitmap(a, shortA.ClientRectangle); shortB.DrawToBitmap(b, shortB.ClientRectangle);
                    bool differs = false;
                    for (int y = 0; y < a.Height; y++) for (int x = 0; x < a.Width; x++)
                        differs |= a.GetPixel(x, y) != b.GetPixel(x, y);
                    Expect(differs, "Single-period session renders its course name within the card");
                    shortA.Size = new(AppTheme.S(10), AppTheme.S(100));
                    using var narrow = new Bitmap(shortA.Width, shortA.Height);
                    shortA.DrawToBitmap(narrow, shortA.ClientRectangle);
                    Expect(true, "Narrow overlapping session paints without invalid rounded geometry");
                }
                var selected = form.Calendar.Cards.Last(); selected.PerformClick();
                Expect(form.SelectedDetails.Contains(selected.Occurrence.Class.Id), "Click shows selected class details");
                Expect(form.Calendar.Cards.Count(c => c.Selected) == 1, "One selected session highlighted");
                var keyboardCard = form.Calendar.Cards[1]; keyboardCard.Select(); keyboardCard.SpaceForCheck();
                Expect(form.SelectedDetails.Contains(keyboardCard.Occurrence.Class.Id) && keyboardCard.Selected,
                    "Native Space key handler selects focused session");
                var bounds = keyboardCard.Bounds;
                keyboardCard.HoverForCheck(true); await Task.Delay(220);
                Expect(!keyboardCard.IsAnimating && keyboardCard.Bounds == bounds, "Hover completes and keeps interactive bounds fixed");
                keyboardCard.HoverForCheck(false); await Task.Delay(220);
                await Shot("01-student-timetable");
                var week = form.Snapshot.WeekStart;
                form.ChangeWeek(1);
                Expect(form.Snapshot.WeekStart == week.AddDays(7), "Next week advances seven days");
                form.ChangeWeek(-1);
                Expect(form.Snapshot.WeekStart == week, "Previous week restores original date");
                form.SemesterPicker.SelectedIndex = 1;
                Expect(form.Calendar.Cards.Count == 0 && form.Snapshot.TotalCredits == 0, "Unregistered semester shows no other student's classes");
                await Shot("02-empty-semester");
                var frames = Path.Combine(output, "motion-frames"); Directory.CreateDirectory(frames);
                var timestamps = new List<long>(); var clock = System.Diagnostics.Stopwatch.StartNew();
                form.SemesterPicker.SelectedIndex = 0;
                for (int frame = 0; frame < 12; frame++)
                {
                    if (frame == 6) form.SemesterPicker.SelectedIndex = 1;
                    timestamps.Add(clock.ElapsedMilliseconds);
                    using var bitmap = UiCapture.Client(form.CalendarSurface);
                    bitmap.Save(Path.Combine(frames, $"frame-{frame:00}.png"));
                    await Task.Delay(40);
                }
                File.WriteAllText(Path.Combine(frames, "timings.json"), JsonSerializer.Serialize(timestamps));
                form.SemesterPicker.SelectedIndex = 0;
                form.SelectDate(new(2026, 10, 14));
                for (int i = 0; i < 60; i++) form.ChangeWeek(-1);
                Expect(!form.Snapshot.CanGoPrevious, "Navigation clamps at first semester week");
                var first = form.Snapshot.WeekStart; form.ChangeWeek(-1);
                Expect(form.Snapshot.WeekStart == first, "Disabled previous cannot leave semester");
                for (int i = 0; i < 60; i++) form.ChangeWeek(1);
                Expect(!form.Snapshot.CanGoNext, "Navigation clamps at last semester week");
                form.SemesterPicker.SelectedIndex = 1; form.SemesterPicker.SelectedIndex = 0;
                form.SelectDate(new(2026, 10, 14));
                form.ClientSize = new(AppTheme.S(1100), AppTheme.S(820));
                form.SemesterPicker.Focus(); form.ScrollPage(0);
                await Shot("03-compact");
                Expect(!form.HasHorizontalScroll, "Compact 1100 form has no horizontal page scroll");
                Expect(form.Calendar.Cards.All(c => form.Calendar.ClientRectangle.Contains(c.Bounds)), "Compact session bounds stay in grid");
                form.ScrollPage(1000); await Shot("05-compact-scrolled");
                Expect(form.ScrollOffset < 0, "Compact form scrolls to lower periods and session details");
                var selectedWeek = form.Snapshot.WeekStart;
                typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(form.NavigationButton, [EventArgs.Empty]);
                Expect(form.ScrollOffset == 0 && form.Snapshot.WeekStart == selectedWeek,
                    "Current-page navigation returns to top without changing selected week");
                Expect(form.NavigationButton.CanSelect && !string.IsNullOrWhiteSpace(form.NavigationButton.AccessibleName),
                    "Navigation pill is keyboard focusable with an accessible name");
                AntdUI.Config.Animation = false; form.ChangeWeek(1);
                Expect(SurfaceTransition.ActiveCount == 0, "Reduced motion commits immediately with no overlay");
                AntdUI.Config.Animation = true;
                await Task.Delay(200);
                Expect(form.MotionIdle, "No periodic motion jobs remain at rest");
                foreach (var asset in new[] { "programming.png", "graduation-cap.png", "calendar-days.svg", "Lucide-LICENSE.txt", "unDraw-LICENSE.txt" })
                    Expect(File.Exists(TimetableStyle.Asset(asset)), "Offline asset: " + asset);
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = true, dpi = form.DeviceDpi, checks }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "error.txt"), error.ToString());
                File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed = false, checks }));
                Environment.ExitCode = 1;
            }
            finally { form.Close(); }
        };
    }

    private static void WriteLayout(Control root, string path)
    {
        var rows = new List<object>();
        void Collect(Control control, string key)
        {
            rows.Add(new { key, type = control.GetType().FullName, control.Name, control.Text,
                bounds = new { control.Left, control.Top, control.Width, control.Height },
                font = new { control.Font.Name, control.Font.Size, control.Font.Style },
                control.TabIndex, control.TabStop, control.AccessibleName });
            for (int index = 0; index < control.Controls.Count; index++)
                Collect(control.Controls[index], key + "/" + index);
        }
        Collect(root, "form");
        File.WriteAllText(path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
