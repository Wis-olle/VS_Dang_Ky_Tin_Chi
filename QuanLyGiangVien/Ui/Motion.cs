namespace QuanLyGiangVien.Ui;

/// <summary>Finite native motion driven by the page's shared clock. See docs/ASSET_SOURCES.md.</summary>
public sealed class Motion : IDisposable
{
    private PageMotionCoordinator? coordinator;
    private Action<float>? update;
    private Func<float, float> easing = MotionMath.EaseOut;
    private double started;
    private int duration;
    private bool disposed;
    public bool IsRunning => update != null;
    public void Run(int milliseconds, Action<float> step, int delayMilliseconds = 0, Func<float, float>? easing = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(milliseconds);
        ArgumentOutOfRangeException.ThrowIfNegative(delayMilliseconds);
        Cancel(false);
        coordinator = PageMotionCoordinator.Current;
        if (!AntdUI.Config.Animation || coordinator == null) { step(1); return; }
        duration = milliseconds;
        this.easing = easing ?? MotionMath.EaseOut;
        started = coordinator.ElapsedMilliseconds + delayMilliseconds;
        update = step;
        step(0);
        if (update != null) coordinator.Add(this);
    }
    internal void Advance(double elapsed)
    {
        if (elapsed < started || update == null) return;
        float progress = (float)Math.Clamp((elapsed - started) / duration, 0, 1);
        if (progress >= 1) Cancel();
        else update(easing(progress));
    }
    public void Cancel(bool finish = true)
    {
        var previous = update;
        update = null;
        coordinator?.Remove(this);
        coordinator = null;
        if (finish) previous?.Invoke(1);
    }
    public void Dispose() { if (disposed) return; disposed = true; Cancel(false); }
}
