using System.Data;
using System.Globalization;
using System.Text;
using QuanLyGiangVien.Domain.Registration;

namespace QuanLyGiangVien.Features.Registration;

/// <summary>Completes the exporter referenced but absent from the original Function_4 branch.</summary>
internal static class CsvExporter
{
    public static void Save(IWin32Window owner, DataTable? table, string defaultName)
    {
        if (table is null || table.Rows.Count == 0)
            throw new InvalidOperationException("Chưa có dữ liệu để xuất. Hãy tải kết quả trước.");
        using var dialog = new SaveFileDialog { FileName = defaultName, Filter = "CSV (*.csv)|*.csv",
            AddExtension = true, DefaultExt = "csv", OverwritePrompt = true };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return;
        try
        {
            using var writer = new StreamWriter(dialog.FileName, false, new UTF8Encoding(true));
            writer.WriteLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => RegistrationCsv.Escape(c.ColumnName))));
            foreach (DataRow row in table.Rows)
                writer.WriteLine(string.Join(",", row.ItemArray.Select(value => RegistrationCsv.Escape(value switch {
                    null or DBNull => "",
                    DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    IFormattable item => item.ToString(null, CultureInfo.InvariantCulture) ?? "",
                    _ => value.ToString() ?? ""
                }))));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { throw new InvalidOperationException("Không thể ghi tệp CSV. Kiểm tra quyền thư mục và đóng tệp đang mở."); }
    }
}
