using QuanLyGiangVien.Ui;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class AuthMotionMathTests
{
    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(.25f, 1.0125f)]
    [InlineData(.5f, 1.025f)]
    [InlineData(1, 1.05f)]
    [InlineData(2, 1.05f)]
    public void HoverScaleIsSmallClampedAndFractional(float progress, float expected) =>
        Assert.Equal(expected, AuthMotionMath.Scale(progress), 6);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HoverScaleIsMonotonicWithoutOvershoot(bool entering)
    {
        var scales = Enumerable.Range(0, 101).Select(i => AuthMotionMath.Scale(
            SelectMotionMath.Lerp(entering ? 0 : 1, entering ? 1 : 0, SelectMotionMath.EaseInOut(i / 100f)))).ToArray();
        Assert.All(scales, scale => Assert.InRange(scale, 1, 1.05f));
        Assert.Equal(entering ? scales.OrderBy(scale => scale) : scales.OrderByDescending(scale => scale), scales);
        Assert.Equal(entering ? 1 : 1.05f, scales[0]);
        Assert.Equal(entering ? 1.05f : 1, scales[^1]);
    }

    [Fact]
    public void ZoomReversalStartsAtTheCurrentFractionalScale()
    {
        float current = SelectMotionMath.EaseInOut(.37f);
        Assert.Equal(AuthMotionMath.Scale(current), AuthMotionMath.Scale(SelectMotionMath.Lerp(current, 0, 0)));
        Assert.Equal(AuthMotionMath.Scale(current), AuthMotionMath.Scale(SelectMotionMath.Lerp(current, 1, 0)));
        Assert.InRange(AuthMotionMath.Scale(SelectMotionMath.Lerp(current, 0, .5f)), 1, AuthMotionMath.Scale(current));
    }
    [Theory]
    [InlineData(-1, 150, 75)]
    [InlineData(0, 150, 75)]
    [InlineData(.25f, 75, 37.5f)]
    [InlineData(.5f, 0, 0)]
    [InlineData(.75f, -75, -37.5f)]
    [InlineData(1, -150, -75)]
    [InlineData(2, -150, -75)]
    public void EllipseTracksTheReferenceCssTranslation(float progress, float x, float y) =>
        Assert.Equal(new AuthEllipse(x, y, 500, 250), AuthMotionMath.Ellipse(200, 100, progress));

    [Theory]
    [InlineData(-1, 25)]
    [InlineData(0, 25)]
    [InlineData(.25f, 18.75f)]
    [InlineData(.5f, 12.5f)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    public void EntranceClampsAndSupportsFractionalFrames(float progress, float offset)
    {
        Assert.Equal(offset, AuthMotionMath.EntranceOffset(progress));
    }

    [Theory]
    [InlineData(291, 108)]
    [InlineData(450, 108)]
    [InlineData(1, 1000)]
    [InlineData(1000, 1)]
    public void FinalEllipseCoversAllCardCorners(float width, float height)
    {
        AuthEllipse ellipse = AuthMotionMath.Ellipse(width, height, 1);
        double radiusX = ellipse.Width / 2d;
        double radiusY = ellipse.Height / 2d;
        double centerX = ellipse.X + radiusX;
        double centerY = ellipse.Y + radiusY;
        foreach (float x in new[] { 0f, width })
        foreach (float y in new[] { 0f, height })
        {
            double normalizedX = (x - centerX) / radiusX;
            double normalizedY = (y - centerY) / radiusY;
            Assert.InRange(normalizedX * normalizedX + normalizedY * normalizedY, 0, 1);
        }
    }

    [Fact]
    public void GeometryKeepsFractionalDimensionsAndSymmetricTranslation()
    {
        AuthEllipse forward = AuthMotionMath.Ellipse(291.5f, 108.25f, .3f);
        AuthEllipse backward = AuthMotionMath.Ellipse(291.5f, 108.25f, .7f);
        Assert.Equal(728.75f, forward.Width);
        Assert.Equal(270.625f, forward.Height);
        Assert.Equal(-forward.X, backward.X, 4);
        Assert.Equal(-forward.Y, backward.Y, 4);
    }

    [Fact]
    public void ReversalUsesTheSameCurrentGeometryWithoutAJumptoAnEndpoint()
    {
        float current = SelectMotionMath.Lerp(0, 1, SelectMotionMath.EaseInOut(.4f));
        AuthEllipse before = AuthMotionMath.Ellipse(200, 100, current);
        float reverseStart = SelectMotionMath.Lerp(current, 0, 0);
        Assert.Equal(before, AuthMotionMath.Ellipse(200, 100, reverseStart));
        float reverseNext = SelectMotionMath.Lerp(current, 0, .5f);
        Assert.InRange(AuthMotionMath.Ellipse(200, 100, reverseNext).X, before.X, 150);
        Assert.Equal(AuthMotionMath.Ellipse(200, 100, 0), AuthMotionMath.Ellipse(200, 100, SelectMotionMath.Lerp(current, 0, 1)));
    }

    [Fact]
    public void EntranceAcceptsCustomAndZeroDistance()
    {
        Assert.Equal(6, AuthMotionMath.EntranceOffset(.5f, 12));
        Assert.Equal(0, AuthMotionMath.EntranceOffset(.5f, 0));
        Assert.Equal(float.MaxValue, AuthMotionMath.EntranceOffset(0, float.MaxValue));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void RejectsEveryNonFiniteInput(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthMotionMath.Ellipse(value, 100, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthMotionMath.Ellipse(200, value, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthMotionMath.Ellipse(200, 100, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthMotionMath.Scale(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthMotionMath.EntranceOffset(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthMotionMath.EntranceOffset(0, value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveDimensions(float dimension)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthMotionMath.Ellipse(dimension, 100, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthMotionMath.Ellipse(200, dimension, 0));
    }

    [Fact]
    public void RejectsNegativeEntranceDistance() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthMotionMath.EntranceOffset(.5f, -1));

    [Theory]
    [InlineData(float.MaxValue, 100, 1)]
    [InlineData(200, float.MaxValue, 1)]
    // Ellipse size fits a float, but its right/bottom edge cannot be drawn.
    [InlineData(float.MaxValue / 3, 100, 0)]
    [InlineData(200, float.MaxValue / 3, 0)]
    public void RejectsOverflowingDrawingCoordinates(float width, float height, float progress) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AuthMotionMath.Ellipse(width, height, progress));
}
