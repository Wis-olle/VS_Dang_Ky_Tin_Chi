using System.Data;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Registration;

internal sealed class NotificationsPage : ModulePage
{
    private readonly DataGridView grid = Ui.Grid();
    private readonly Label summary = Ui.Label("");
    private DataTable? data;
    public NotificationsPage(RegistrationWorkspace host) : base(host)
    {
        Name = "NotificationsPage"; grid.Name = "dgvNotifications";
        var bar = Ui.Toolbar(); var refresh = Ui.Button("Tải thông báo"); var read = Ui.Button("Đánh dấu đã đọc");
        bar.Controls.AddRange([refresh, read, summary]); Controls.Add(grid); Controls.Add(bar);
        refresh.Click += async (_, _) => await Host.ExecuteAsync(RefreshDataAsync);
        read.Click += async (_, _) => await Host.ExecuteAsync(async () =>
        {
            if (Ui.Current(grid) is not { } row) return;
            using var result = await Host.EventLog.NotificationsAsync(Convert.ToInt64(row["id"]));
            await RefreshDataAsync();
        });
    }
    public override async Task RefreshDataAsync()
    {
        var fresh = await Host.EventLog.NotificationsAsync(); if (IsDisposed) { fresh.Dispose(); return; }
        ClearData(); data = fresh; Ui.Bind(grid, data);
        summary.Text = $"{data.Rows.Cast<DataRow>().Count(r => r.IsNull("read_utc"))} chưa đọc • Giờ UTC";
    }
    public override void ClearData() { Ui.Bind(grid, null); data?.Dispose(); data = null; summary.Text = ""; }
}
