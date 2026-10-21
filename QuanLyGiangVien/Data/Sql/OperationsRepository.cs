using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Academics;

namespace QuanLyGiangVien.Data.Sql;

public sealed class OperationsRepository(string connectionString)
{
    public Task<DataSet> SnapshotAsync(string term) => ReadAsync("Snapshot", term);
    public Task<DataSet> RunOnceAsync(string term) => ReadAsync("Run", term);
    private async Task<DataSet> ReadAsync(string operation, string term)
    {
        term = AcademicValidation.Code(term);
        try {
            await using var c = new SqlConnection(connectionString); await AppSqlConnection.OpenAsync(c).ConfigureAwait(false);
            using var q = new SqlCommand("dbo.usp_Operations_" + operation, c) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 };
            q.Parameters.Add("@term", SqlDbType.VarChar, 20).Value = term;
            using var reader = await q.ExecuteReaderAsync().ConfigureAwait(false); var data = new DataSet();
            string[] names = operation == "Run" ? ["Worker"] : ["Demand", "PlanRisks", "Health", "Worker", "Imports", "Outbox"];
            var i = 0;
            do {
                if (reader.FieldCount == 0) continue;
                var table = new DataTable(i < names.Length ? names[i] : "Table" + i);
                for (var j = 0; j < reader.FieldCount; j++) table.Columns.Add(reader.GetName(j), reader.GetFieldType(j));
                if (operation == "Run") table.TableName = table.Columns.Contains("status") && table.Columns.Contains("duration_ms") ? "Worker" : "SeatWorker" + i;
                while (await reader.ReadAsync().ConfigureAwait(false)) { var values = new object[reader.FieldCount]; reader.GetValues(values); table.Rows.Add(values); }
                data.Tables.Add(table); i++;
            } while (await reader.NextResultAsync().ConfigureAwait(false));
            return data;
        } catch (SqlException e) {
            if (e.Number == 52405) { AppSqlConnection.BlockSession(connectionString); throw new PersistenceException("Phiên đăng nhập đã hết hạn. Đăng nhập lại để tiếp tục.", PersistenceFailure.SessionExpired); }
            throw new PersistenceException(e.Number is >= 53000 and <= 53499 ? e.Message : SqlErrorMessages.ForNumber(e.Number));
        }
    }
}
