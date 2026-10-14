using QuanLyGiangVien.Ui;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class OverviewMotionMathTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 12, 0)]
    [InlineData(12, 12, 1)]
    [InlineData(10, 12, 10f / 12)]
    public void FractionPreservesActualCounts(int active, int total, float expected) =>
        Assert.Equal(expected, OverviewMotionMath.Fraction(active, total));

    [Theory]
    [InlineData(-1, 12)]
    [InlineData(0, -1)]
    [InlineData(13, 12)]
    [InlineData(1, 0)]
    public void ImpossibleCountsAreRejected(int active, int total) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => OverviewMotionMath.Fraction(active, total));

    [Fact]
    public void HighlightMovesWithinTheActualArcAndReturnsWithoutAJump()
    {
        const float fraction = 10f / 12;
        Assert.Equal(fraction * .1f, OverviewMotionMath.HighlightProgress(0, fraction), 5);
        Assert.Equal(fraction * .5f, OverviewMotionMath.HighlightProgress(1.5, fraction), 5);
        Assert.Equal(fraction * .9f, OverviewMotionMath.HighlightProgress(3, fraction), 5);
        Assert.Equal(fraction * .5f, OverviewMotionMath.HighlightProgress(4.5, fraction), 5);
        Assert.Equal(fraction * .1f, OverviewMotionMath.HighlightProgress(6, fraction), 5);
        Assert.Equal(OverviewMotionMath.HighlightProgress(.7, fraction),
            OverviewMotionMath.HighlightProgress(6.7, fraction), 5);
        Assert.True(Math.Abs(OverviewMotionMath.HighlightProgress(5.999, fraction)
            - OverviewMotionMath.HighlightProgress(6.001, fraction)) < .001f);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(.01f)]
    [InlineData(10f / 12)]
    [InlineData(1)]
    public void HighlightAlwaysStaysInsideZeroPartialAndFullArcs(float fraction)
    {
        for (int index = -100; index <= 100; index++)
            Assert.InRange(OverviewMotionMath.HighlightProgress(index / 10d, fraction), 0, fraction);
        Assert.InRange(OverviewMotionMath.HighlightProgress(double.MaxValue, fraction), 0, fraction);
        Assert.InRange(OverviewMotionMath.HighlightProgress(double.MinValue, fraction), 0, fraction);
    }

    [Fact]
    public void HighlightUsesTheConfiguredPeriod()
    {
        Assert.Equal(.9f, OverviewMotionMath.HighlightProgress(2, 1, 4), 5);
        Assert.Equal(OverviewMotionMath.HighlightProgress(.7, .5f, 4),
            OverviewMotionMath.HighlightProgress(4.7, .5f, 4), 5);
    }

    [Theory]
    [InlineData(double.NaN, .8f, 6)]
    [InlineData(double.PositiveInfinity, .8f, 6)]
    [InlineData(double.NegativeInfinity, .8f, 6)]
    [InlineData(1, .8f, 0)]
    [InlineData(1, .8f, -1)]
    [InlineData(1, .8f, double.NaN)]
    [InlineData(1, .8f, double.PositiveInfinity)]
    [InlineData(1, -.1f, 6)]
    [InlineData(1, 1.1f, 6)]
    [InlineData(1, float.NaN, 6)]
    [InlineData(1, float.PositiveInfinity, 6)]
    public void InvalidAmbientInputsCannotCreateInvalidDrawingCoordinates(double seconds, float fraction, double period) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => OverviewMotionMath.HighlightProgress(seconds, fraction, period));

    [Theory]
    [InlineData(0, 0, -1)]
    [InlineData(.25f, 1, 0)]
    [InlineData(.5f, 0, 1)]
    [InlineData(.75f, -1, 0)]
    [InlineData(1, 0, -1)]
    public void ArcCoordinatesFollowTheClockwiseDataArc(float progress, float x, float y)
    {
        var position = OverviewMotionMath.ArcPosition(progress);
        Assert.Equal(x, position.X, 5);
        Assert.Equal(y, position.Y, 5);
    }

    [Fact]
    public void ArcCoordinatesStayOnTheUnitCircle()
    {
        for (int index = 0; index <= 100; index++)
        {
            var (x, y) = OverviewMotionMath.ArcPosition(index / 100f);
            Assert.InRange(x, -1, 1);
            Assert.InRange(y, -1, 1);
            Assert.Equal(1, x * x + y * y, 5);
        }
    }

    [Theory]
    [InlineData(-.1f)]
    [InlineData(1.1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void InvalidArcProgressIsRejected(float progress) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => OverviewMotionMath.ArcPosition(progress));
}
