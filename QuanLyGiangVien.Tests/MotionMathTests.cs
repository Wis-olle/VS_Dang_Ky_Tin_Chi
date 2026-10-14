using QuanLyGiangVien.Ui;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class MotionMathTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void EaseOutClampsAndSettlesExactly(float progress, float expected) =>
        Assert.Equal(expected, MotionMath.EaseOut(progress));

    [Fact]
    public void EaseOutIsMonotonicAndDecelerates()
    {
        var values = Enumerable.Range(0, 101).Select(index => MotionMath.EaseOut(index / 100f)).ToArray();
        Assert.All(values, value => Assert.InRange(value, 0, 1));
        Assert.True(values.Zip(values.Skip(1), (first, second) => second >= first).All(result => result));
        Assert.True(values[50] > .5f);
        Assert.True(values[25] - values[0] > values[100] - values[75]);
    }

    [Fact]
    public void StaggerDelaysLaterRegionsAndSettlesEveryRegion()
    {
        var halfway = Enumerable.Range(0, 5).Select(index => MotionMath.Stagger(.5f, index, 5)).ToArray();
        Assert.True(halfway.Zip(halfway.Skip(1), (first, second) => first >= second).All(result => result));
        Assert.Equal(0, MotionMath.Stagger(.1f, 4, 5));
        Assert.Equal(0, MotionMath.Stagger(0, 0, 5));
        for (int index = 0; index < 5; index++)
        {
            Assert.Equal(1, MotionMath.Stagger(1, index, 5));
            Assert.Equal(1, MotionMath.Stagger(5, index, 5));
            Assert.Equal(0, MotionMath.Stagger(-1, index, 5));
        }
    }

    [Fact]
    public void SingleRegionOrZeroLagHasNoDelay()
    {
        Assert.Equal(.4f, MotionMath.Stagger(.4f, 0, 1));
        Assert.Equal(.4f, MotionMath.Stagger(.4f, 3, 4, 0));
    }

    [Theory]
    [InlineData(0, 37)]
    [InlineData(37, 0)]
    [InlineData(-13, 25)]
    [InlineData(25, -13)]
    [InlineData(int.MinValue, int.MaxValue)]
    public void CounterStaysBoundedAndHitsEndpointsInBothDirections(int from, int to)
    {
        Assert.Equal(from, MotionMath.Counter(from, to, -1));
        Assert.Equal(from, MotionMath.Counter(from, to, 0));
        Assert.Equal(to, MotionMath.Counter(from, to, 1));
        Assert.Equal(to, MotionMath.Counter(from, to, 2));
        for (int index = 0; index <= 100; index++)
            Assert.InRange(MotionMath.Counter(from, to, index / 100f), Math.Min(from, to), Math.Max(from, to));
    }

    [Fact]
    public void CounterRoundsSmallDeltasAndUnchangedValues()
    {
        Assert.Equal(1, MotionMath.Counter(0, 1, .5f));
        Assert.Equal(7, MotionMath.Counter(7, 7, .4f));
    }

    [Fact]
    public void WavePreservesPeriodAmplitudeAndNeutralOrigin()
    {
        Assert.Equal(0, MotionMath.Wave(0, 6, 3));
        Assert.Equal(3, MotionMath.Wave(1.5, 6, 3), 5);
        Assert.Equal(-3, MotionMath.Wave(4.5, 6, 3), 5);
        Assert.Equal(MotionMath.Wave(.7, 6, 3), MotionMath.Wave(6.7, 6, 3), 5);
        Assert.Equal(0, MotionMath.Wave(1.5, 6, 0));
        Assert.True(float.IsFinite(MotionMath.Wave(double.MaxValue, 6, 3)));
        for (int index = -60; index <= 60; index++)
            Assert.InRange(MotionMath.Wave(index / 10d, 6, 3), -3, 3);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void NonfiniteProgressIsRejected(float progress)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MotionMath.EaseOut(progress));
        Assert.Throws<ArgumentOutOfRangeException>(() => MotionMath.Stagger(progress, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MotionMath.Counter(0, 10, progress));
    }

    [Theory]
    [InlineData(0, 0, .08f)]
    [InlineData(-1, 3, .08f)]
    [InlineData(3, 3, .08f)]
    [InlineData(0, 3, -.1f)]
    [InlineData(0, 3, .5f)]
    [InlineData(0, 3, float.NaN)]
    [InlineData(0, 3, float.PositiveInfinity)]
    public void InvalidStaggerCannotCreateAnUnfinishedRegion(int index, int count, float lag) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MotionMath.Stagger(.5f, index, count, lag));

    [Theory]
    [InlineData(double.NaN, 6, 3)]
    [InlineData(double.PositiveInfinity, 6, 3)]
    [InlineData(1, 0, 3)]
    [InlineData(1, -1, 3)]
    [InlineData(1, double.PositiveInfinity, 3)]
    [InlineData(1, double.NaN, 3)]
    [InlineData(1, 6, -1)]
    [InlineData(1, 6, float.NaN)]
    [InlineData(1, 6, float.PositiveInfinity)]
    public void InvalidWaveParametersAreRejected(double seconds, double period, float amplitude) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MotionMath.Wave(seconds, period, amplitude));
}
