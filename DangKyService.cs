using System;
using System.Data;
using System.Threading.Tasks;
using DangKyTinChi.Data;
using DangKyTinChi.Models;

namespace DangKyTinChi.Services
{
    public sealed class DangKyService
    {
        private readonly DangKyRepository repository;
        public NguoiDung User { get; private set; }
        public DangKyService(DangKyRepository repository) { this.repository = repository; }
        public async Task InitializeAsync()
        {
            var table = await repository.NguoiDungAsync();
            if (table.Rows.Count != 1) throw new InvalidOperationException("Không xác định được người dùng.");
            User = NguoiDung.FromRow(table.Rows[0]);
        }
        private static string RequiredCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 20)
                throw new ArgumentException("Mã không được để trống và dài tối đa 20 ký tự.");
            return value.Trim();
        }
        public Task<DataTable> HocKyAsync() { return repository.HocKyAsync(); }
        public Task<DataTable> ThoiHanAsync(string hk) { return repository.ThoiHanAsync(RequiredCode(hk)); }
        public Task<DataTable> TraCuuAsync(string hk, string keyword, bool conCho)
        {
            keyword = (keyword ?? "").Trim();
            if (keyword.Length > 100) throw new ArgumentException("Từ khóa tối đa 100 ký tự.");
            return repository.TraCuuAsync(RequiredCode(hk), keyword, conCho);
        }
        public Task<DataTable> DangKyAsync(string lhp)
        {
            if (!User.LaSinhVien) throw new InvalidOperationException("Chỉ sinh viên được đăng ký.");
            return repository.DangKyAsync(RequiredCode(lhp));
        }
        public Task<DataTable> HuyAsync(long id)
        {
            if (!User.LaSinhVien || id <= 0) throw new InvalidOperationException("Không thể hủy đăng ký này.");
            return repository.HuyAsync(id);
        }
        public Task<DataTable> KetQuaAsync(string hk, string sv, bool daHuy)
        { return repository.KetQuaAsync(RequiredCode(hk), User.LaSinhVien ? null : RequiredCode(sv), daHuy); }
        public Task<DataTable> ThongKeAsync(string hk, int? threshold)
        {
            if (!User.LaDaoTao) throw new InvalidOperationException("Bạn không có quyền thống kê.");
            if (threshold < 0) throw new ArgumentException("Ngưỡng sinh viên không được âm.");
            return repository.ThongKeAsync(RequiredCode(hk), threshold);
        }
        public Task<DataTable> DanhSachAsync(string lhp)
        {
            if (!User.LaDaoTao) throw new InvalidOperationException("Bạn không có quyền xem danh sách lớp.");
            return repository.DanhSachAsync(RequiredCode(lhp));
        }
    }
}
