// Adapted from Huệ Function_4 @ 77b70bc; original snapshot: Upstream/ModulePage.cs.txt.
namespace QuanLyGiangVien.Features.Registration;

internal abstract class ModulePage : UserControl
{
    protected RegistrationWorkspace Host { get; }
    protected DangKyService Service => Host.Service;
    protected ModulePage(RegistrationWorkspace host)
    { Host = host; Dock = DockStyle.Fill; Padding = new Padding(12); }
    public abstract Task RefreshDataAsync();
    public abstract void ClearData();
    public virtual void ApplyDeadline() { }
    protected override void Dispose(bool disposing)
    {
        if (disposing) ClearData();
        base.Dispose(disposing);
    }
}
