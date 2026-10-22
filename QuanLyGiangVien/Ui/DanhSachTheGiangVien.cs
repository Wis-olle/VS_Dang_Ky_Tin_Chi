using QuanLyGiangVien.Domain;

namespace QuanLyGiangVien.Ui;

public sealed class LecturerGrid : ScrollableControl
{
    private readonly List<LecturerCard> cards = new();
    private readonly Motion entrance = new();
    public event Action<Lecturer>? Selected;
    public event Action<Lecturer>? EditRequested;
    public event Action<Lecturer, Control>? MenuRequested;
    public LecturerGrid()
    {
        AutoScroll = true;
        BackColor = Color.White;
        DoubleBuffered = true;
        Resize += (_, _) => Relayout();
    }
    public void ShowItems(IReadOnlyList<Lecturer> items, string? selectedId, bool animate)
    {
        entrance.Cancel(false);
        SuspendLayout();
        AutoScrollPosition = Point.Empty;
        foreach (var card in cards) card.Dispose();
        cards.Clear();
        foreach (var item in items)
        {
            var card = new LecturerCard(item) { IsSelected = item.Id == selectedId };
            card.Selected += value => Selected?.Invoke(value);
            card.EditRequested += value => EditRequested?.Invoke(value);
            card.MenuRequested += (value, control) => MenuRequested?.Invoke(value, control);
            cards.Add(card);
            Controls.Add(card);
        }
        Relayout();
        ResumeLayout();
        if (animate) entrance.Run(480, p =>
        {
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].Entrance = MotionMath.EaseOut(MotionMath.Stagger(p, Math.Min(i, 8), 9, .035f));
                cards[i].Invalidate();
            }
        });
    }
    internal void RevealCards()
    {
        int index = 0;
        foreach (var card in cards.Where(card => card.Bounds.IntersectsWith(ClientRectangle)))
            SurfaceTransition.Reveal(card, 320, 260 + Math.Min(index++, 6) * 35);
    }
    public void SelectId(string? id)
    {
        foreach (var card in cards) card.IsSelected = card.Lecturer.Id == id;
    }
    public void Relayout()
    {
        if (cards.Count == 0) { AutoScrollMinSize = Size.Empty; return; }
        SuspendLayout();
        int gap = AppTheme.S(14);
        int available = Math.Max(AppTheme.S(120), Width - SystemInformation.GetVerticalScrollBarWidthForDpi(DeviceDpi) - AppTheme.S(4));
        int columns = available >= AppTheme.S(770) ? 3 : available >= AppTheme.S(530) ? 2 : 1;
        int width = (available - gap * (columns - 1)) / columns;
        int height = AppTheme.S(LecturerCard.CardHeight);
        int pitch = height + gap;
        AutoScrollMinSize = new(0, ((cards.Count + columns - 1) / columns) * pitch - gap);
        var offset = AutoScrollPosition;
        for (int i = 0; i < cards.Count; i++)
            cards[i].SetBounds(i % columns * (width + gap) + offset.X,
                i / columns * pitch + offset.Y, width, height);
        ResumeLayout(true);
        PerformLayout();
    }
    protected override void Dispose(bool disposing) { if (disposing) entrance.Dispose(); base.Dispose(disposing); }
}
