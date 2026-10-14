namespace QuanLyGiangVien.Domain.Registration;

/// <summary>UI snapshot only. Every original Huệ procedure rechecks ORIGINAL_LOGIN() on SQL.</summary>
public sealed record RegistrationUser(string MaTk, string? MaSv, string VaiTro, string HoTen)
{
    public bool LaSinhVien => VaiTro == "SINH_VIEN";
    public bool LaDaoTao => VaiTro is "DAO_TAO" or "QUAN_TRI";
}

/// <summary>Server Vietnam wall clock plus monotonic elapsed time; never use workstation clock for eligibility.</summary>
public sealed record RegistrationDeadline(DateTime Start, DateTime End, DateTime CancelEnd,
    DateTime ServerTime, string Status)
{
    public DateTime Now(TimeSpan elapsed) => ServerTime.Add(elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed);
    public bool CanRegister(TimeSpan elapsed) => Status == "DANG_MO" && Now(elapsed) >= Start && Now(elapsed) < End;
    public bool CanCancel(TimeSpan elapsed) => Status == "DANG_MO" && Now(elapsed) >= Start && Now(elapsed) < CancelEnd;
}

public static class RegistrationValidation
{
    public static string RequiredCode(string? value)
    {
        var code = value?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length > 20 ||
            code.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException("Mã không được để trống, tối đa 20 ký tự chữ/số, gạch ngang hoặc gạch dưới.");
        return code;
    }

    public static string Keyword(string? value)
    {
        var keyword = (value ?? "").Trim();
        if (keyword.Length > 100) throw new ArgumentException("Từ khóa tối đa 100 ký tự.");
        return keyword;
    }

    public static int? Threshold(int? value) => value < 0
        ? throw new ArgumentException("Ngưỡng sinh viên không được âm.") : value;

    public static long RegistrationId(long value) => value <= 0
        ? throw new ArgumentException("Mã đăng ký không hợp lệ.") : value;
}
