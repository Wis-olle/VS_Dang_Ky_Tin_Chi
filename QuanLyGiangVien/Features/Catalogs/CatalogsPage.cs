using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Ui;

namespace QuanLyGiangVien.Features.Catalogs;

public sealed class CatalogsPage : UserControl
{
    private readonly HocPhanControl courses;
    private readonly HocKyControl semesters;
    private readonly Font pageFont;
    public CatalogsPage(string connectionString, Action dataChanged)
    {
        Dock=DockStyle.Fill; BackColor=AppTheme.Canvas; pageFont=AppTheme.Font(); Font=pageFont;
        void NotifyChanged() {
            if(IsDisposed||Disposing||!IsHandleCreated)return;
            if(InvokeRequired) {
                try { BeginInvoke((Action)NotifyChanged); }
                catch(InvalidOperationException) when(IsDisposed||Disposing||!IsHandleCreated) { /* View closed after the SQL operation completed. */ }
            }else dataChanged();
        }
        var repository=new CatalogRepository(connectionString,NotifyChanged);
        courses=new(()=>repository); semesters=new(()=>repository);
        var tabs=new TabControl {Dock=DockStyle.Fill};
        var courseTab=new TabPage("Học phần"); var semesterTab=new TabPage("Học kỳ và đợt đăng ký");
        courseTab.Controls.Add(courses);semesterTab.Controls.Add(semesters);
        tabs.TabPages.Add(courseTab);tabs.TabPages.Add(semesterTab);
        tabs.Selecting+=(_,e)=> {if(!CanLeave())e.Cancel=true;};
        Controls.Add(tabs);
    }
    public bool IsBusy=>Ui.IsBusy(courses)||Ui.IsBusy(semesters);
    public bool HasUnsavedChanges=>courses.HasUnsavedChanges||semesters.HasUnsavedChanges;
    public bool CanLeave()
    {
        if(IsBusy)return false;
        if(!HasUnsavedChanges)return true;
        if(!Ui.Confirm(this,"Có thay đổi danh mục chưa lưu. Bỏ các thay đổi này?"))return false;
        courses.ResetEditor();semesters.ResetEditor();return true;
    }
    public async Task ReloadAsync()
    {
        await courses.ReloadAsync();await semesters.ReloadAsync();
    }
    protected override void Dispose(bool disposing)
    {
        var ownedFonts=new HashSet<Font>();
        if(disposing) {
            void Collect(Control control) {
                ownedFonts.Add(control.Font);
                if(control is DataGridView grid) {
                    if(grid.DefaultCellStyle.Font is Font body)ownedFonts.Add(body);
                    if(grid.ColumnHeadersDefaultCellStyle.Font is Font heading)ownedFonts.Add(heading);
                }
                foreach(Control child in control.Controls)Collect(child);
            }
            Collect(this);ownedFonts.Add(pageFont);
        }
        base.Dispose(disposing);
        foreach(var font in ownedFonts)if(!font.IsSystemFont)font.Dispose();
    }
}
