using System.Data;
using QuanLyGiangVien.Ui;
using static QuanLyGiangVien.Features.Academics.AcademicUi;

namespace QuanLyGiangVien.Features.Academics;

internal sealed class AcademicHistory : UserControl
{
    private readonly DataGridView grid = Grid();
    private readonly TextBox search = new();
    private readonly Font ownedFont;
    internal AcademicHistory()
    {
        Dock = DockStyle.Fill; BackColor = Color.White; Padding = new(S(14)); ownedFont = AppTheme.Font(9); Font = ownedFont;
        var tip = new Label { Dock = DockStyle.Top, Height = S(48), Text = "Mỗi thay đổi có người thực hiện, thời điểm và lý do. Nhập mã kết quả để lọc lịch sử.", ForeColor = AppTheme.Muted };
        search.Dock = DockStyle.Top; search.PlaceholderText = "Mã kết quả…"; search.AccessibleName = "Lọc lịch sử theo mã kết quả";
        grid.AccessibleName = "Lịch sử sửa kết quả được công nhận";
        foreach (var c in new[] { ("id", "Mã kết quả", 100), ("changed_at", "Thời điểm (UTC)", 140), ("actor", "Người thực hiện", 110), ("reason", "Lý do thay đổi", 250) })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = c.Item1, DataPropertyName = c.Item1, HeaderText = c.Item2, FillWeight = c.Item3, MinimumWidth = S(90) });
        Controls.Add(grid); Controls.Add(search); Controls.Add(tip);
        search.TextChanged += (_, _) => Filter();
    }
    internal void Bind(DataSet snapshot)
    {
        var old = grid.DataSource as DataTable;
        if (snapshot.Tables.Contains("ResultHistory"))
        {
            var table = snapshot.Tables["ResultHistory"]!.Copy();
            if (table.Columns.Contains("kind")) foreach (var row in table.AsEnumerable().Where(r => AcademicSection.RowText(r, "kind") != "RESULT").ToArray()) table.Rows.Remove(row);
            grid.DataSource = table; Filter();
        }
        else grid.DataSource = null;
        old?.Dispose();
    }
    private void Filter()
    {
        if (grid.DataSource is not DataTable table || !table.Columns.Contains("id")) return;
        var text = search.Text.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
        table.DefaultView.RowFilter = text.Length == 0 ? "" : $"[id] LIKE '%{text}%'";
        if (table.Columns.Contains("sequence")) table.DefaultView.Sort = "sequence DESC";
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) (grid.DataSource as DataTable)?.Dispose(); base.Dispose(disposing); if (disposing) ownedFont.Dispose();
    }
}
