namespace QuanLyGiangVien.Ui;

/// <summary>Pure geometry and easing used by the native animated selects.</summary>
public static class SelectMotionMath
{
    /// <summary>CSS ease-in-out: cubic-bezier(0.42, 0, 0.58, 1).</summary>
    public static float EaseInOut(float progress)
    {
        float value = ClampProgress(progress);
        if (value is 0 or 1) return value;
        double lower = 0;
        double upper = 1;
        // Invert the curve's x coordinate; its parameter is not elapsed progress.
        // Fixed bisection bounds the error and keeps frame work deterministic.
        for (int iteration = 0; iteration < 24; iteration++)
        {
            double parameter = (lower + upper) * 0.5;
            double inverse = 1 - parameter;
            double x = 3 * inverse * inverse * parameter * 0.42
                + 3 * inverse * parameter * parameter * 0.58 + parameter * parameter * parameter;
            if (x < value) lower = parameter;
            else upper = parameter;
        }
        double t = (lower + upper) * 0.5;
        return (float)(t * t * (3 - 2 * t));
    }

    public static float Lerp(float from, float to, float progress)
    {
        ValidateFinite(from, nameof(from));
        ValidateFinite(to, nameof(to));
        float value = ClampProgress(progress);
        return (float)(from + ((double)to - from) * value);
    }

    public static float ListOffset(float indexPosition, float rowHeight, float gap)
    {
        ValidateFinite(indexPosition, nameof(indexPosition));
        ValidateFinite(rowHeight, nameof(rowHeight));
        ValidateFinite(gap, nameof(gap));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(gap);
        double offset = -indexPosition * ((double)rowHeight + gap);
        if (Math.Abs(offset) > float.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(indexPosition), "List offset exceeds the rendering coordinate range.");
        return (float)offset;
    }

    public static float PopupOffset(float openProgress, float distance = 10)
    {
        ValidateFinite(distance, nameof(distance));
        ArgumentOutOfRangeException.ThrowIfNegative(distance);
        return Lerp(distance, 0, openProgress);
    }

    private static float ClampProgress(float progress)
    {
        ValidateFinite(progress, nameof(progress));
        return Math.Clamp(progress, 0, 1);
    }

    private static void ValidateFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(parameterName);
    }
}
