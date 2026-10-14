namespace QuanLyGiangVien.Ui;

/// <summary>Validated frame metadata independent of WinForms and native timers.</summary>
public sealed record IconTiming
{
    public int FrameCount { get; }
    public int DurationMilliseconds { get; }
    public int IdleFrame { get; }

    public IconTiming(int frameCount, int durationMilliseconds, int idleFrame = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationMilliseconds);
        ArgumentOutOfRangeException.ThrowIfNegative(idleFrame);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(idleFrame, frameCount);
        FrameCount = frameCount;
        DurationMilliseconds = durationMilliseconds;
        IdleFrame = idleFrame;
    }
}

/// <summary>A single, finite playback timeline. Elapsed time starts at the latest Start call.</summary>
public sealed record IconPlayback
{
    public IconTiming Timing { get; }
    public int FrameIndex { get; }
    public bool IsPlaying { get; }

    private IconPlayback(IconTiming timing, int frameIndex, bool isPlaying)
    {
        Timing = timing;
        FrameIndex = frameIndex;
        IsPlaying = isPlaying;
    }

    public static IconPlayback Idle(IconTiming timing)
    {
        ArgumentNullException.ThrowIfNull(timing);
        return new(timing, timing.IdleFrame, false);
    }

    public static IconPlayback Start(IconTiming timing, bool enabled = true, bool visible = true)
    {
        ArgumentNullException.ThrowIfNull(timing);
        return enabled && visible ? new(timing, 0, true) : Idle(timing);
    }

    public IconPlayback Start(bool enabled = true, bool visible = true) => Start(Timing, enabled, visible);

    public IconPlayback Advance(long elapsedMilliseconds, bool allowed = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedMilliseconds);
        if (!IsPlaying || !allowed || elapsedMilliseconds >= Timing.DurationMilliseconds)
            return Stop();
        // Completion is checked first, bounding multiplication even for long-delayed timer callbacks.
        int index = (int)(elapsedMilliseconds * Timing.FrameCount / Timing.DurationMilliseconds);
        return new(Timing, index, true);
    }

    public IconPlayback Stop() => Idle(Timing);
}
