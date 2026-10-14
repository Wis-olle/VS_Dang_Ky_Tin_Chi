namespace QuanLyGiangVien.Ui;

public readonly record struct AuthEllipse(float X, float Y, float Width, float Height);

/// <summary>Pure geometry for the auth-form inspired dashboard card motion.</summary>
public static class AuthMotionMath
{
    public static float Scale(float progress) => 1 + .05f * ClampProgress(progress);
    public static AuthEllipse Ellipse(float width, float height, float progress)
    {
        ValidateFinite(width, nameof(width));
        ValidateFinite(height, nameof(height));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        double translation = .75 - 1.5 * ClampProgress(progress);
        double x = translation * width;
        double y = translation * height;
        double ellipseWidth = 2.5 * width;
        double ellipseHeight = 2.5 * height;
        ValidateCoordinates(x, ellipseWidth, nameof(width));
        ValidateCoordinates(y, ellipseHeight, nameof(height));
        return new AuthEllipse((float)x, (float)y, (float)ellipseWidth, (float)ellipseHeight);
    }

    public static float EntranceOffset(float progress, float distance = 25)
    {
        ValidateFinite(distance, nameof(distance));
        ArgumentOutOfRangeException.ThrowIfNegative(distance);
        return distance * (1 - ClampProgress(progress));
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

    private static void ValidateCoordinates(double origin, double length, string parameterName)
    {
        if (length > float.MaxValue || Math.Abs(origin) > float.MaxValue || Math.Abs(origin + length) > float.MaxValue)
            throw new ArgumentOutOfRangeException(parameterName, "Ellipse exceeds the rendering coordinate range.");
    }
}
