using System.Threading.Tasks;
using System.Windows.Forms;
using DangKyTinChi.Services;

namespace DangKyTinChi.Forms
{
    public abstract class ModulePage : UserControl
    {
        protected MainForm Host { get; private set; }
        protected DangKyService Service { get { return Host.Service; } }
        protected ModulePage(MainForm host)
        { Host = host; Dock = DockStyle.Fill; Padding = new Padding(12); }
        public abstract Task RefreshDataAsync();
        public abstract void ClearData();
        public virtual void ApplyDeadline() { }
    }
}
