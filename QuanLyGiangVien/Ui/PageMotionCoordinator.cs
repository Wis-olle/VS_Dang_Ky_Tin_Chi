using System.Diagnostics;

namespace QuanLyGiangVien.Ui;

/// <summary>One UI-thread clock for entrances, counters, hover feedback and the decorative hero.</summary>
internal sealed class PageMotionCoordinator : IDisposable
{
    private readonly Form owner;
    // Stay below a typical 15.6 ms timer quantum; 16 ms often misses the next tick.
    // Elapsed time, rather than tick count, still determines every animation's progress.
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
    private readonly Stopwatch clock = new();
    private readonly HashSet<Motion> jobs = [];
    private Action<double, bool>? ambient;
    private bool disposed;
    internal static PageMotionCoordinator? Current { get; private set; }
    internal double ElapsedMilliseconds => clock.Elapsed.TotalMilliseconds;
    internal int ActiveCount => jobs.Count;
    internal bool TimerIsRunning => timer.Enabled;
    internal int AmbientTicks { get; private set; }
    internal PageMotionCoordinator(Form form)
    {
        owner = form;
        Current = this;
        timer.Tick += Tick;
        owner.VisibleChanged += OwnerStateChanged;
        owner.Resize += OwnerStateChanged;
    }
    internal void SetAmbient(Action<double, bool>? callback) { ambient = callback; RefreshPreference(); }
    internal void Add(Motion motion) { if (disposed) return; jobs.Add(motion); ResumeIfVisible(); }
    internal void Remove(Motion motion) => jobs.Remove(motion);
    internal void SetReducedMotion(bool reduced) { AntdUI.Config.Animation = !reduced; RefreshPreference(); }
    internal void RefreshPreference()
    {
        if (disposed) return;
        if (!AntdUI.Config.Animation)
        {
            foreach (var job in jobs.ToArray()) job.Cancel();
            ambient?.Invoke(0, false);
            timer.Stop(); clock.Stop();
        }
        else ResumeIfVisible();
    }
    private bool CanRun => owner.Visible && owner.WindowState != FormWindowState.Minimized && !owner.IsDisposed;
    private void ResumeIfVisible()
    {
        if (!disposed && AntdUI.Config.Animation && CanRun && (jobs.Count > 0 || ambient != null))
        { clock.Start(); timer.Start(); }
    }
    private void OwnerStateChanged(object? sender, EventArgs e)
    {
        if (!CanRun) { timer.Stop(); clock.Stop(); }
        else RefreshPreference();
    }
    private void Tick(object? sender, EventArgs e)
    {
        if (!AntdUI.Config.Animation) { RefreshPreference(); return; }
        if (!CanRun) { timer.Stop(); clock.Stop(); return; }
        foreach (var job in jobs.ToArray()) job.Advance(ElapsedMilliseconds);
        if (ambient != null) { ambient(clock.Elapsed.TotalSeconds, true); AmbientTicks++; }
        else if (jobs.Count == 0) { timer.Stop(); clock.Stop(); }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; timer.Stop(); clock.Stop();
        owner.VisibleChanged -= OwnerStateChanged; owner.Resize -= OwnerStateChanged;
        foreach (var job in jobs.ToArray()) job.Cancel(false);
        jobs.Clear(); ambient = null; timer.Dispose();
        if (Current == this) Current = null;
    }
}
