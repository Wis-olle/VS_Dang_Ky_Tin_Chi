namespace QuanLyGiangVien.Domain.Registration;

public static class RegistrationCsv
{
    public static string Escape(string? value)
    {
        var text = value ?? "";
        var meaningful = text.TrimStart();
        if ((meaningful.Length > 0 && meaningful[0] is '=' or '+' or '-' or '@') ||
            (text.Length > 0 && text[0] is '\t' or '\r' or '\n')) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
