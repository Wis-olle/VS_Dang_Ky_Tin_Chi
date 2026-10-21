using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyGiangVien.Domain;
using QuanLyGiangVien.Domain.Registration;

namespace QuanLyGiangVien.Data.Sql;

public sealed class ExceptionWorkflowRepository(string connectionString)
{
    public Task<DataTable> ListAsync(string? term = null)
    {
        if (term is not null) RegistrationValidation.RequiredCode(term);
        return RunAsync(Command("List", P("@term", SqlDbType.VarChar, term, 20)));
    }

    public Task<DataTable> SubmitAsync(ExceptionApplication application) => RunAsync(SubmitCommand(application));
    public Task<DataTable> DecideAsync(ExceptionDecision decision) => RunAsync(DecideCommand(decision));

    public static SqlCommand SubmitCommand(ExceptionApplication application)
    {
        ExceptionWorkflowValidation.Application(application);
        return Command("Submit", P("@kind", SqlDbType.VarChar, application.Kind, 20),
            P("@term", SqlDbType.VarChar, application.Term, 20), P("@course", SqlDbType.VarChar, application.Course, 20),
            P("@class", SqlDbType.VarChar, application.ClassId, 20), P("@rule", SqlDbType.VarChar, application.RuleCode, 30),
            P("@reason", SqlDbType.NVarChar, application.Reason.Trim(), 500));
    }

    public static SqlCommand DecideCommand(ExceptionDecision decision)
    {
        ExceptionWorkflowValidation.Decision(decision);
        return Command("Decide", P("@id", SqlDbType.BigInt, decision.Id), P("@version", SqlDbType.Binary, Convert.FromHexString(decision.Version), 8),
            P("@decision", SqlDbType.VarChar, decision.Decision, 20), P("@reason", SqlDbType.NVarChar, decision.Reason.Trim(), 500),
            P("@expires_utc", SqlDbType.DateTime2, decision.ExpiresUtc), P("@uses", SqlDbType.Int, decision.Uses),
            P("@max_credits", SqlDbType.Int, decision.MaxCredits));
    }

    private async Task<DataTable> RunAsync(SqlCommand command)
    {
        using (command)
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await AppSqlConnection.OpenAsync(connection).ConfigureAwait(false);
            command.Connection = connection;
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            var table = new DataTable { Locale = System.Globalization.CultureInfo.InvariantCulture };
            for (var i = 0; i < reader.FieldCount; i++) table.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var values = new object[reader.FieldCount]; reader.GetValues(values); table.Rows.Add(values);
            }
            return table;
        }
        catch (SqlException error)
        {
            throw new PersistenceException(error.Number is >= 52900 and <= 52909 or 52801 ? error.Message : SqlErrorMessages.ForNumber(error.Number),
                error.Number == 52904 ? PersistenceFailure.DataConflict : PersistenceFailure.General);
        }
        catch (InvalidOperationException) { throw new PersistenceException("Không thể đọc quy trình ngoại lệ. Hãy tải lại dữ liệu."); }
    }

    private static SqlCommand Command(string action, params SqlParameter[] parameters)
    {
        var command = new SqlCommand("dbo.usp_DK3_Exception" + action) { CommandType = CommandType.StoredProcedure, CommandTimeout = 30 };
        command.Parameters.AddRange(parameters); return command;
    }
    private static SqlParameter P(string name, SqlDbType type, object? value, int size = 0) => new(name, type, size) { Value = value ?? DBNull.Value };
}
