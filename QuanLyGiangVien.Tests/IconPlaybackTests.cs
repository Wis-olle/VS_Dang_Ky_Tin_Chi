using QuanLyGiangVien.Ui;
using Xunit;

namespace QuanLyGiangVien.Tests;

public sealed class IconPlaybackTests
{
    private static IconTiming Timing() => new(12, 600, 3);

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void DisabledOrHiddenPlaybackRemainsIdle(bool enabled, bool visible)
    {
        var state = IconPlayback.Start(Timing(), enabled, visible);
        Assert.False(state.IsPlaying);
        Assert.Equal(3, state.FrameIndex);
    }

    [Fact]
    public void StartingAndAdvancingDoNotChangePreviousState()
    {
        var idle = IconPlayback.Idle(Timing());
        var playing = idle.Start();
        var advanced = playing.Advance(250);
        Assert.False(idle.IsPlaying);
        Assert.Equal(3, idle.FrameIndex);
        Assert.True(playing.IsPlaying);
        Assert.Equal(0, playing.FrameIndex);
        Assert.Equal(5, advanced.FrameIndex);
        Assert.True(advanced.IsPlaying);
    }

    [Fact]
    public void DisablingDuringPlaybackStopsAndDoesNotRestart()
    {
        var playing = IconPlayback.Start(Timing()).Advance(300);
        var stopped = playing.Advance(350, allowed: false);
        Assert.False(stopped.IsPlaying);
        Assert.Equal(3, stopped.FrameIndex);
        Assert.Equal(stopped, stopped.Advance(400, allowed: true));
        Assert.True(playing.IsPlaying);
        Assert.Equal(6, playing.FrameIndex);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(49, 0, true)]
    [InlineData(50, 1, true)]
    [InlineData(599, 11, true)]
    [InlineData(600, 3, false)]
    [InlineData(2400, 3, false)]
    [InlineData(long.MaxValue, 3, false)]
    public void PlaybackUsesElapsedTimeAndFinishesWithoutLooping(long elapsed, int expectedFrame, bool expectedPlaying)
    {
        var state = IconPlayback.Start(Timing()).Advance(elapsed);
        Assert.Equal(expectedFrame, state.FrameIndex);
        Assert.Equal(expectedPlaying, state.IsPlaying);
    }

    [Fact]
    public void RapidRestartResetsTheTimelineAndRetainsOneState()
    {
        var previous = IconPlayback.Start(Timing()).Advance(599);
        var restarted = previous.Start();
        Assert.Equal(0, restarted.FrameIndex);
        Assert.True(restarted.IsPlaying);
        Assert.Equal(11, previous.FrameIndex);
        Assert.Equal(1, restarted.Advance(50).FrameIndex);
    }

    [Fact]
    public void StopUsesConfiguredIdleFrame()
    {
        var state = IconPlayback.Start(Timing()).Advance(450).Stop();
        Assert.False(state.IsPlaying);
        Assert.Equal(3, state.FrameIndex);
        Assert.Equal(state, state.Stop());
    }

    [Fact]
    public void SingleFrameAnimationRemainsBounded()
    {
        var state = IconPlayback.Start(new(1, 1));
        Assert.Equal(0, state.Advance(0).FrameIndex);
        Assert.False(state.Advance(1).IsPlaying);
    }

    [Fact]
    public void NegativeElapsedIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IconPlayback.Start(Timing()).Advance(-1));
    }

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(-1, 100, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(2, 100, -1)]
    [InlineData(2, 100, 2)]
    public void InvalidTimingMetadataIsRejected(int frames, int duration, int idle)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IconTiming(frames, duration, idle));
    }

    [Fact]
    public void PlaybackRequiresTiming()
    {
        Assert.Throws<ArgumentNullException>(() => IconPlayback.Idle(null!));
        Assert.Throws<ArgumentNullException>(() => IconPlayback.Start(null!));
    }
}
