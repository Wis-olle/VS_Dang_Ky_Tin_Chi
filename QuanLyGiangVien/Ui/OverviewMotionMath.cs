namespace QuanLyGiangVien.Ui;

public static class OverviewMotionMath
{
    public static float Fraction(int active, int total)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(active);
        ArgumentOutOfRangeException.ThrowIfNegative(total);
        if (active > total) throw new ArgumentOutOfRangeException(nameof(active));
        return total == 0 ? 0 : active / (float)total;
    }

    /// <summary>A smooth return trip inside the data arc; the track and percentage never rotate.</summary>
    public static float HighlightProgress(double seconds, float fraction, double period = 6)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!double.IsFinite(period) || period <= 0) throw new ArgumentOutOfRangeException(nameof(period));
        ValidateProgress(fraction);
        double phase = seconds % period / period;
        float travel = (float)((1 - Math.Cos(phase * Math.PI * 2)) / 2);
        return fraction * (.1f + .8f * travel);
    }

    public static (float X, float Y) ArcPosition(float progress)
    {
        ValidateProgress(progress);
        double angle = progress * Math.PI * 2;
        return ((float)Math.Sin(angle), (float)-Math.Cos(angle));
    }

    private static void ValidateProgress(float progress)
    {
        if (!float.IsFinite(progress) || progress < 0 || progress > 1)
            throw new ArgumentOutOfRangeException(nameof(progress));
    }
}
