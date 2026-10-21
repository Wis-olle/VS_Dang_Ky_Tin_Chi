using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Operations;

namespace QuanLyGiangVien.Data.Sql;

public sealed class DataImportRepository(string connectionString)
{
    public Task<DataTable> PreviewAsync(string text) => ExecuteAsync(DataImport.Parse(text), false, false);
    public Task<DataTable> CommitAsync(string text, bool confirmedSource) {
        if (!confirmedSource) throw new ArgumentException("Xác nhận nguồn dữ liệu trước khi ghi batch.");
        return ExecuteAsync(DataImport.Parse(text), true, true);
    }
    private async Task<DataTable> ExecuteAsync(ImportBatch batch, bool commit, bool confirmed)
    {
        await using var c = new SqlConnection(connectionString);
        try {
            await AppSqlConnection.OpenAsync(c).ConfigureAwait(false);
            using var q = new SqlCommand("dbo.usp_Import_Batch", c) { CommandType = CommandType.StoredProcedure, CommandTimeout = 120 };
            q.Parameters.Add("@document", SqlDbType.NVarChar, -1).Value = batch.Json;
            q.Parameters.Add("@commit", SqlDbType.Bit).Value = commit; q.Parameters.Add("@confirmed", SqlDbType.Bit).Value = confirmed;
            using var r = await q.ExecuteReaderAsync().ConfigureAwait(false); DataTable? result = null;
            do {
                if (r.FieldCount == 0) continue;
                var table = new DataTable(); for (var i = 0; i < r.FieldCount; i++) table.Columns.Add(r.GetName(i), r.GetFieldType(i));
                while (await r.ReadAsync().ConfigureAwait(false)) { var values = new object[r.FieldCount]; r.GetValues(values); table.Rows.Add(values); }
                if (table.Columns.Contains("row_index")) { result?.Dispose(); result = table; } else table.Dispose();
            } while (await r.NextResultAsync().ConfigureAwait(false));
            return result ?? throw new PersistenceException("Không nhận được kết quả đối soát batch.");
        }
        catch (SqlException e) {
            if (e.Number == 52405) { AppSqlConnection.BlockSession(connectionString); throw new PersistenceException("Phiên đăng nhập đã hết hạn. Bản nháp import được giữ; đăng nhập lại trước khi đối soát batch-id.", PersistenceFailure.SessionExpired); }
            throw new PersistenceException(e.Number is >= 53300 and <= 53399 ? e.Message : SqlErrorMessages.ForNumber(e.Number), e.Number == 53303 ? PersistenceFailure.DataConflict : PersistenceFailure.General);
        }
    }
}
