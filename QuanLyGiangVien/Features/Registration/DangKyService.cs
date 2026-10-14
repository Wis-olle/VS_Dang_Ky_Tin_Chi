// Adapted from Huệ Function_4 @ 77b70bc; original snapshot: Upstream/DangKyService.cs.txt.
using System.Data;
using QuanLyGiangVien.Data.Sql;
using QuanLyGiangVien.Domain.Registration;

namespace QuanLyGiangVien.Features.Registration;

internal sealed class DangKyService(RegistrationRepository repository)
{
    public RegistrationUser User { get; private set; } = new("", null, "", "");

    public async Task InitializeAsync(DatabaseIdentity expected)
    {
        using var table = await repository.NguoiDungAsync();
        if (table.Rows.Count != 1) throw new InvalidOperationException("Không xác định được người dùng.");
        var row = table.Rows[0];
        var current = new RegistrationUser(Convert.ToString(row["ma_tk"])!,
            row.IsNull("ma_sv") ? null : Convert.ToString(row["ma_sv"]),
            Convert.ToString(row["vai_tro"])!, Convert.ToString(row["ho_ten"])!);
        if (current.MaTk != expected.AccountId || current.MaSv != expected.StudentId || current.VaiTro != expected.Role)
            throw new InvalidOperationException("Danh tính đăng nhập đã thay đổi. Vui lòng đăng nhập lại.");
        User = current;
    }

    public Task<DataTable> HocKyAsync() => repository.HocKyAsync();
    public Task<DataTable> ThoiHanAsync(string hk) => repository.ThoiHanAsync(hk);
    public Task<DataTable> TraCuuAsync(string hk, string keyword, bool conCho) => repository.TraCuuAsync(hk, keyword, conCho);
    public Task<DataTable> DangKyAsync(string lhp)
    {
        if (!User.LaSinhVien) throw new InvalidOperationException("Chỉ sinh viên được đăng ký.");
        return repository.DangKyAsync(lhp);
    }
    public Task<DataTable> HuyAsync(long id)
    {
        if (!User.LaSinhVien) throw new InvalidOperationException("Chỉ sinh viên được hủy đăng ký.");
        return repository.HuyAsync(id);
    }
    public Task<DataTable> KetQuaAsync(string hk, string? sv, bool history) =>
        repository.KetQuaAsync(hk, User.LaSinhVien ? null : RegistrationValidation.RequiredCode(sv), history);
    public Task<DataTable> ThongKeAsync(string hk, int? threshold)
    {
        if (!User.LaDaoTao) throw new InvalidOperationException("Bạn không có quyền thống kê.");
        return repository.ThongKeAsync(hk, threshold);
    }
    public Task<DataTable> DanhSachAsync(string lhp)
    {
        if (!User.LaDaoTao) throw new InvalidOperationException("Bạn không có quyền xem danh sách lớp.");
        return repository.DanhSachAsync(lhp);
    }
}
