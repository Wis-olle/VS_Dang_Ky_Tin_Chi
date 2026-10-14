using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace QuanLyGiangVien.Ui;

/// <summary>Offline playback of the source icon's animation, once per hover.</summary>
public sealed class AnimatedDashboardIcon : Control
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 25 };
    private readonly Stopwatch clock = new();
    private readonly IconSprite? sprite;
    private readonly Color tone;
    private readonly int radius;
    private readonly List<Control> hoverTargets = [];
    private Control? hoverSurface;
    private bool insideSurface;
    private bool resourcesDisposed;
    private IconPlayback playback = IconPlayback.Idle(new IconTiming(1, 1));

    public AnimatedDashboardIcon(string id, string caption, Color tone, string fallbackSvg, int radius = 12)
    {
        this.tone = tone;
        this.radius = radius;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        TabStop = false;
        AccessibleName = caption;
        AccessibleRole = AccessibleRole.Graphic;
        try { sprite = IconSprite.Load(id); }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or JsonException or ExternalException)
        {
            Trace.TraceError("Animated icon {0}: {1}", id, error);
            AccessibleDescription = "Không tải được icon động; đang dùng biểu tượng tĩnh.";
            Controls.Add(new AntdUI.Avatar { ImageSvg = fallbackSvg, BackColor = tone,
                ForeColor = AppTheme.Ink, Radius = radius, Dock = DockStyle.Fill });
        }
        playback = IconPlayback.Idle(sprite?.Timing ?? new IconTiming(1, 1));
        timer.Tick += (_, _) => Advance();
        BindHover(this);
    }

    public void BindHover(Control surface, params Control[] children)
    {
        hoverSurface = surface;
        foreach (var target in new[] { surface }.Concat(children).Append(this).Distinct())
        {
            if (hoverTargets.Contains(target)) continue;
            hoverTargets.Add(target);
            target.MouseEnter += HoverEntered;
            target.MouseLeave += HoverLeft;
        }
    }

    private void HoverEntered(object? sender, EventArgs e)
    {
        if (insideSurface) return;
        insideSurface = true;
        Play();
    }

    private void HoverLeft(object? sender, EventArgs e)
    {
        if (hoverSurface is null || !hoverSurface.IsHandleCreated ||
            !hoverSurface.RectangleToScreen(hoverSurface.ClientRectangle).Contains(Cursor.Position))
            insideSurface = false;
    }

    internal bool HasAnimation => sprite is not null;
    internal bool IsPlaying => playback.IsPlaying;
    internal bool TimerIsRunning => !IsDisposed && timer.Enabled;
    internal int FrameIndex => playback.FrameIndex;
    internal int IdleFrame => playback.Timing.IdleFrame;
    internal int DurationMilliseconds => playback.Timing.DurationMilliseconds;

    internal void Play()
    {
        if (IsDisposed || resourcesDisposed) return;
        timer.Stop();
        playback = playback.Start(sprite is not null && AntdUI.Config.Animation && Enabled, IsInViewport());
        clock.Restart();
        if (playback.IsPlaying) timer.Start();
        Invalidate();
    }

    private void Advance()
    {
        long elapsed = clock.ElapsedMilliseconds;
        var timing = playback.Timing;
        // The final cell is static idle, so it does not accelerate the source animation.
        long timelineElapsed = elapsed >= timing.DurationMilliseconds ? timing.DurationMilliseconds :
            elapsed * (timing.FrameCount - 1) / timing.FrameCount;
        playback = playback.Advance(timelineElapsed, AntdUI.Config.Animation && Enabled && IsInViewport());
        if (!playback.IsPlaying) { timer.Stop(); clock.Stop(); }
        Invalidate();
    }

    internal void Stop()
    {
        if (resourcesDisposed) return;
        timer.Stop();
        clock.Stop();
        playback = playback.Stop();
        if (!IsDisposed) Invalidate();
    }

    private bool IsInViewport()
    {
        if (!Visible || !Enabled || !IsHandleCreated) return false;
        var region = RectangleToScreen(ClientRectangle);
        for (Control? parent = Parent; parent is not null; parent = parent.Parent)
        {
            if (!parent.Visible || !parent.IsHandleCreated) return false;
            region.Intersect(parent.RectangleToScreen(parent.ClientRectangle));
            if (region.IsEmpty || region.Width <= 0 || region.Height <= 0) return false;
        }
        return FindForm() is { IsDisposed: false, WindowState: not FormWindowState.Minimized };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (ClientSize.Width <= 1 || ClientSize.Height <= 1) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = AppTheme.Round(new RectangleF(0, 0, Width - 1, Height - 1), AppTheme.S(radius));
        using var background = new SolidBrush(tone);
        e.Graphics.FillPath(background, shape);
        if (sprite is null) return;
        int size = Math.Max(1, (int)Math.Round(Math.Min(Width, Height) * .86));
        var target = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
        var source = new Rectangle(playback.FrameIndex * sprite.FrameSize, 0, sprite.FrameSize, sprite.FrameSize);
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(sprite.Bitmap, target, source, GraphicsUnit.Pixel);
    }
    internal void PaintForMotion(Graphics graphics)
    {
        OnPaint(new PaintEventArgs(graphics, ClientRectangle));
        foreach (Control child in Controls)
        {
            using var bitmap = new Bitmap(child.Width, child.Height);
            child.DrawToBitmap(bitmap, child.ClientRectangle);
            graphics.DrawImageUnscaled(bitmap, child.Location);
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) { insideSurface = false; Stop(); }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        if (!Enabled) Stop();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !resourcesDisposed)
        {
            Stop();
            timer.Dispose();
            foreach (var target in hoverTargets)
            {
                target.MouseEnter -= HoverEntered;
                target.MouseLeave -= HoverLeft;
            }
            sprite?.Dispose();
            resourcesDisposed = true;
        }
        base.Dispose(disposing);
    }
}
