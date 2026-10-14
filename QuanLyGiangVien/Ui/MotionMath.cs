namespace QuanLyGiangVien.Ui;

public static class MotionMath
{
    public static float EaseOut(float progress)
    {
        float value = ClampProgress(progress);
        return 1 - MathF.Pow(1 - value, 3);
    }
    public static float Stagger(float progress, int index, int count, float lag = .08f)
    {
        float value = ClampProgress(progress);
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
        if (!float.IsFinite(lag) || lag < 0 || lag * (count - 1d) >= 1)
            throw new ArgumentOutOfRangeException(nameof(lag));
        return Math.Clamp((value - index * lag) / (1 - (count - 1) * lag), 0, 1);
    }
    public static int Counter(int from, int to, float progress)
    {
        float value = ClampProgress(progress);
        return (int)Math.Round(from + ((double)to - from) * value, MidpointRounding.AwayFromZero);
    }
    public static float Wave(double seconds, double period, float amplitude)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (!double.IsFinite(period) || period <= 0) throw new ArgumentOutOfRangeException(nameof(period));
        if (!float.IsFinite(amplitude) || amplitude < 0) throw new ArgumentOutOfRangeException(nameof(amplitude));
        return (float)(Math.Sin(seconds % period / period * Math.PI * 2) * amplitude);
    }
    private static float ClampProgress(float progress)
    {
        if (!float.IsFinite(progress)) throw new ArgumentOutOfRangeException(nameof(progress));
        return Math.Clamp(progress, 0, 1);
    }
}
