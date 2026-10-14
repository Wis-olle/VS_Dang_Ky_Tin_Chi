using QuanLyGiangVien.Ui;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class SelectMotionMathTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    // CSS cubic-bezier(0.42, 0, 0.58, 1), independently calculated reference points.
    [InlineData(0.25f, 0.1291619f)]
    [InlineData(0.75f, 0.8708381f)]
    public void EasingMatchesCssCurve(float progress, float expected) =>
        Assert.Equal(expected, SelectMotionMath.EaseInOut(progress), 5);

    [Fact]
    public void EasingStaysBoundedMonotonicAndSymmetric()
    {
        float previous = 0;
        for (int index = 0; index <= 100; index++)
        {
            float progress = index / 100f;
            float value = SelectMotionMath.EaseInOut(progress);
            Assert.InRange(value, previous, 1);
            Assert.InRange(Math.Abs(1 - value - SelectMotionMath.EaseInOut(1 - progress)), 0, 0.000001f);
            previous = value;
        }
    }

    [Theory]
    [InlineData(-1, 20)]
    [InlineData(0, 20)]
    [InlineData(0.25f, 30)]
    [InlineData(1, 60)]
    [InlineData(2, 60)]
    public void InterpolationClampsAndSupportsFractionalProgress(float progress, float expected) =>
        Assert.Equal(expected, SelectMotionMath.Lerp(20, 60, progress));

    [Fact]
    public void ReversedMotionContinuesFromCurrentPosition()
    {
        float current = SelectMotionMath.Lerp(0, 1, SelectMotionMath.EaseInOut(0.4f));
        Assert.Equal(current, SelectMotionMath.Lerp(current, 0, 0));
        Assert.InRange(SelectMotionMath.Lerp(current, 0, 0.5f), 0, current);
        Assert.Equal(0, SelectMotionMath.Lerp(current, 0, 1));
    }

    [Fact]
    public void InterpolationOfExtremeFiniteEndpointsDoesNotOverflow() =>
        Assert.Equal(0, SelectMotionMath.Lerp(-float.MaxValue, float.MaxValue, 0.5f));

    [Theory]
    [InlineData(0, 24, 4, 0)]
    [InlineData(1, 24, 4, -28)]
    [InlineData(1.5f, 24, 4, -42)]
    [InlineData(-0.5f, 24, 4, 14)]
    [InlineData(2.25f, 24, 0, -54)]
    public void RollingListUsesFractionalPositions(float position, float height, float gap, float expected) =>
        Assert.Equal(expected, SelectMotionMath.ListOffset(position, height, gap));

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 10)]
    [InlineData(0.25f, 7.5f)]
    [InlineData(0.5f, 5)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    public void PopupMovesFromDistanceToRest(float progress, float expected) =>
        Assert.Equal(expected, SelectMotionMath.PopupOffset(progress));

    [Fact]
    public void PopupAcceptsCustomAndZeroDistance()
    {
        Assert.Equal(6, SelectMotionMath.PopupOffset(0.5f, 12));
        Assert.Equal(0, SelectMotionMath.PopupOffset(0.5f, 0));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void RejectsNonFiniteValues(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SelectMotionMath.EaseInOut(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => SelectMotionMath.Lerp(value, 1, 0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => SelectMotionMath.Lerp(0, value, 0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => SelectMotionMath.Lerp(0, 1, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => SelectMotionMath.ListOffset(value, 24, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => SelectMotionMath.ListOffset(0, value, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => SelectMotionMath.ListOffset(0, 24, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => SelectMotionMath.PopupOffset(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => SelectMotionMath.PopupOffset(0, value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveRowHeight(float height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SelectMotionMath.ListOffset(0, height, 4));

    [Fact]
    public void RejectsNegativeSpacingAndDistance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SelectMotionMath.ListOffset(0, 24, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SelectMotionMath.PopupOffset(0, -1));
    }

    [Fact]
    public void RejectsOverflowingListOffset() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SelectMotionMath.ListOffset(float.MaxValue, 24, 4));
}
