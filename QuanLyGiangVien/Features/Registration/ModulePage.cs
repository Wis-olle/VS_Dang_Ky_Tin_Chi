// Adapted from Huệ Function_4 @ 77b70bc; original snapshot: Upstream/ModulePage.cs.txt.
namespace QuanLyGiangVien.Features.Registration;

internal abstract class ModulePage : UserControl
{
    protected RegistrationWorkspace Host { get; }
    protected DangKyService Service => Host.Service;
    protected ModulePage(RegistrationWorkspace host)
    { Host = host; AutoScaleMode = AutoScaleMode.None; Dock = DockStyle.Fill; Padding = new Padding(QuanLyGiangVien.Ui.AppTheme.S(12)); }
    public abstract Task RefreshDataAsync();
    public abstract void ClearData();
    public virtual void ApplyDeadline() { }
    public virtual bool HasUnsavedChanges => false;
    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        ShowcaseStyle.Apply(this);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { ClearData(); Ui.DisposeOwnedFonts(this); }
        base.Dispose(disposing);
    }
}
