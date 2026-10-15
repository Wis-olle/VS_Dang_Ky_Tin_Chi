using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;

namespace QLDIEM
{
    public partial class Form1 : Form
    {
        // =====================================================
        // 1. KẾT NỐI SQL SERVER
        // =====================================================

        private readonly string connectionString =
            @"Server=.\SQLEXPRESS;
              Database=QLDIEM;
              Trusted_Connection=True;
              Encrypt=False;
              TrustServerCertificate=True;";

        // =====================================================
        // 2. KHAI BÁO CONTROL
        // =====================================================

        private Label lblTieuDe;

        private Label lblMaSV;
        private Label lblHoTen;
        private Label lblMaMon;
        private Label lblNgayThi;
        private Label lblDiemThi;

        private TextBox txtMaSV;
        private TextBox txtHoTen;
        private ComboBox cboMaMon;
        private DateTimePicker dtpNgayThi;
        private TextBox txtDiemThi;

        private Button btnLuu;

        private GroupBox grpTimKiem;

        private Label lblTimMaMon;
        private Label lblTimHoTen;

        private TextBox txtTimMaMon;
        private TextBox txtTimHoTen;

        private Button btnTimKiem;
        private Button btnHienThiTatCa;

        private DataGridView dgvKetQua;

        // =====================================================
        // 3. CONSTRUCTOR
        // =====================================================

        public Form1()
        {
            InitializeComponent();

            TaoGiaoDien();

            LoadMonHoc();

            LoadKetQua();
        }

        // =====================================================
        // 4. TẠO GIAO DIỆN
        // =====================================================

        private void TaoGiaoDien()
        {
            // ---------------------------------------------
            // FORM
            // ---------------------------------------------

            this.Text = "Quản lý kết quả thi";
            this.StartPosition = FormStartPosition.CenterScreen;

            this.ClientSize = new Size(1100, 700);

            this.BackColor = Color.White;

            // ---------------------------------------------
            // TIÊU ĐỀ
            // ---------------------------------------------

            lblTieuDe = new Label();

            lblTieuDe.Text = "QUẢN LÝ KẾT QUẢ THI";

            lblTieuDe.Font =
                new Font("Arial", 20, FontStyle.Bold);

            lblTieuDe.TextAlign =
                ContentAlignment.MiddleCenter;

            lblTieuDe.Location =
                new Point(0, 20);

            lblTieuDe.Size =
                new Size(1100, 45);

            this.Controls.Add(lblTieuDe);

            // ---------------------------------------------
            // LABEL MÃ SINH VIÊN
            // ---------------------------------------------

            lblMaSV = new Label();

            lblMaSV.Text = "Mã sinh viên:";

            lblMaSV.Location =
                new Point(80, 100);

            lblMaSV.Size =
                new Size(120, 30);

            lblMaSV.Font =
                new Font("Arial", 10);

            this.Controls.Add(lblMaSV);

            // ---------------------------------------------
            // TEXTBOX MÃ SINH VIÊN
            // ---------------------------------------------

            txtMaSV = new TextBox();

            txtMaSV.Name = "txtMaSV";

            txtMaSV.Location =
                new Point(210, 95);

            txtMaSV.Size =
                new Size(250, 30);

            this.Controls.Add(txtMaSV);

            // ---------------------------------------------
            // LABEL HỌ TÊN
            // ---------------------------------------------

            lblHoTen = new Label();

            lblHoTen.Text = "Họ tên:";

            lblHoTen.Location =
                new Point(550, 100);

            lblHoTen.Size =
                new Size(100, 30);

            lblHoTen.Font =
                new Font("Arial", 10);

            this.Controls.Add(lblHoTen);

            // ---------------------------------------------
            // TEXTBOX HỌ TÊN
            // ---------------------------------------------

            txtHoTen = new TextBox();

            txtHoTen.Name = "txtHoTen";

            txtHoTen.Location =
                new Point(650, 95);

            txtHoTen.Size =
                new Size(300, 30);

            this.Controls.Add(txtHoTen);

            // ---------------------------------------------
            // LABEL MÃ MÔN
            // ---------------------------------------------

            lblMaMon = new Label();

            lblMaMon.Text = "Mã môn:";

            lblMaMon.Location =
                new Point(80, 150);

            lblMaMon.Size =
                new Size(120, 30);

            lblMaMon.Font =
                new Font("Arial", 10);

            this.Controls.Add(lblMaMon);

            // ---------------------------------------------
            // COMBOBOX MÃ MÔN
            // ---------------------------------------------

            cboMaMon = new ComboBox();

            cboMaMon.Name = "cboMaMon";

            cboMaMon.Location =
                new Point(210, 145);

            cboMaMon.Size =
                new Size(250, 30);

            cboMaMon.DropDownStyle =
                ComboBoxStyle.DropDownList;

            this.Controls.Add(cboMaMon);

            // ---------------------------------------------
            // LABEL NGÀY THI
            // ---------------------------------------------

            lblNgayThi = new Label();

            lblNgayThi.Text = "Ngày thi:";

            lblNgayThi.Location =
                new Point(550, 150);

            lblNgayThi.Size =
                new Size(100, 30);

            lblNgayThi.Font =
                new Font("Arial", 10);

            this.Controls.Add(lblNgayThi);

            // ---------------------------------------------
            // DATE TIME PICKER
            // ---------------------------------------------

            dtpNgayThi = new DateTimePicker();

            dtpNgayThi.Name = "dtpNgayThi";

            dtpNgayThi.Location =
                new Point(650, 145);

            dtpNgayThi.Size =
                new Size(300, 30);

            dtpNgayThi.Format =
                DateTimePickerFormat.Short;

            this.Controls.Add(dtpNgayThi);

            // ---------------------------------------------
            // LABEL ĐIỂM THI
            // ---------------------------------------------

            lblDiemThi = new Label();

            lblDiemThi.Text = "Điểm thi:";

            lblDiemThi.Location =
                new Point(80, 200);

            lblDiemThi.Size =
                new Size(120, 30);

            lblDiemThi.Font =
                new Font("Arial", 10);

            this.Controls.Add(lblDiemThi);

            // ---------------------------------------------
            // TEXTBOX ĐIỂM
            // ---------------------------------------------

            txtDiemThi = new TextBox();

            txtDiemThi.Name = "txtDiemThi";

            txtDiemThi.Location =
                new Point(210, 195);

            txtDiemThi.Size =
                new Size(100, 30);

            this.Controls.Add(txtDiemThi);

            // ---------------------------------------------
            // BUTTON LƯU
            // ---------------------------------------------

            btnLuu = new Button();

            btnLuu.Name = "btnLuu";

            btnLuu.Text = "LƯU";

            btnLuu.Location =
                new Point(650, 195);

            btnLuu.Size =
                new Size(130, 35);

            btnLuu.Font =
                new Font("Arial", 10, FontStyle.Bold);

            btnLuu.Click += BtnLuu_Click;

            this.Controls.Add(btnLuu);

            // =================================================
            // GROUP TÌM KIẾM
            // =================================================

            grpTimKiem = new GroupBox();

            grpTimKiem.Text = "TÌM KIẾM";

            grpTimKiem.Location =
                new Point(70, 250);

            grpTimKiem.Size =
                new Size(900, 100);

            grpTimKiem.Font =
                new Font("Arial", 10, FontStyle.Bold);

            this.Controls.Add(grpTimKiem);

            // ---------------------------------------------
            // LABEL TÌM MÃ MÔN
            // ---------------------------------------------

            lblTimMaMon = new Label();

            lblTimMaMon.Text = "Mã môn:";

            lblTimMaMon.Location =
                new Point(20, 40);

            lblTimMaMon.Size =
                new Size(80, 25);

            grpTimKiem.Controls.Add(lblTimMaMon);

            // ---------------------------------------------
            // TEXTBOX TÌM MÃ MÔN
            // ---------------------------------------------

            txtTimMaMon = new TextBox();

            txtTimMaMon.Name = "txtTimMaMon";

            txtTimMaMon.Location =
                new Point(100, 35);

            txtTimMaMon.Size =
                new Size(180, 30);

            grpTimKiem.Controls.Add(txtTimMaMon);

            // ---------------------------------------------
            // LABEL TÌM HỌ TÊN
            // ---------------------------------------------

            lblTimHoTen = new Label();

            lblTimHoTen.Text = "Họ tên:";

            lblTimHoTen.Location =
                new Point(310, 40);

            lblTimHoTen.Size =
                new Size(70, 25);

            grpTimKiem.Controls.Add(lblTimHoTen);

            // ---------------------------------------------
            // TEXTBOX TÌM HỌ TÊN
            // ---------------------------------------------

            txtTimHoTen = new TextBox();

            txtTimHoTen.Name = "txtTimHoTen";

            txtTimHoTen.Location =
                new Point(380, 35);

            txtTimHoTen.Size =
                new Size(230, 30);

            grpTimKiem.Controls.Add(txtTimHoTen);

            // ---------------------------------------------
            // BUTTON TÌM KIẾM
            // ---------------------------------------------

            btnTimKiem = new Button();

            btnTimKiem.Name = "btnTimKiem";

            btnTimKiem.Text = "TÌM KIẾM";

            btnTimKiem.Location =
                new Point(630, 33);

            btnTimKiem.Size =
                new Size(110, 35);

            btnTimKiem.Click += BtnTimKiem_Click;

            grpTimKiem.Controls.Add(btnTimKiem);

            // ---------------------------------------------
            // BUTTON HIỂN THỊ TẤT CẢ
            // ---------------------------------------------

            btnHienThiTatCa = new Button();

            btnHienThiTatCa.Name =
                "btnHienThiTatCa";

            btnHienThiTatCa.Text =
                "HIỂN THỊ TẤT CẢ";

            btnHienThiTatCa.Location =
                new Point(755, 33);

            btnHienThiTatCa.Size =
                new Size(125, 35);

            btnHienThiTatCa.Click +=
                BtnHienThiTatCa_Click;

            grpTimKiem.Controls.Add(
                btnHienThiTatCa);

            // =================================================
            // DATAGRIDVIEW
            // =================================================

            dgvKetQua = new DataGridView();

            dgvKetQua.Name =
                "dgvKetQua";

            dgvKetQua.Location =
                new Point(70, 370);

            dgvKetQua.Size =
                new Size(900, 270);

            dgvKetQua.AutoSizeColumnsMode =
                DataGridViewAutoSizeColumnsMode.Fill;

            dgvKetQua.SelectionMode =
                DataGridViewSelectionMode.FullRowSelect;

            dgvKetQua.MultiSelect = false;

            dgvKetQua.AllowUserToAddRows = false;

            dgvKetQua.ReadOnly = true;

            dgvKetQua.RowHeadersVisible = false;

            dgvKetQua.CellContentClick +=
                DgvKetQua_CellContentClick;

            this.Controls.Add(dgvKetQua);
        }

        // =====================================================
        // 5. LOAD MÔN HỌC
        // =====================================================

        private void LoadMonHoc()
        {
            try
            {
                using (SqlConnection conn =
                       new SqlConnection(connectionString))
                {
                    conn.Open();

                    string sql =
                        "SELECT Mamh, Tenmh FROM mon_hoc";

                    using (SqlDataAdapter adapter =
                           new SqlDataAdapter(sql, conn))
                    {
                        DataTable dt =
                            new DataTable();

                        adapter.Fill(dt);

                        cboMaMon.DataSource = dt;

                        cboMaMon.DisplayMember =
                            "Mamh";

                        cboMaMon.ValueMember =
                            "Mamh";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Lỗi tải môn học:\n" +
                    ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // =====================================================
        // 6. LOAD KẾT QUẢ
        // =====================================================

        private void LoadKetQua()
        {
            try
            {
                using (SqlConnection conn =
                       new SqlConnection(connectionString))
                {
                    conn.Open();

                    string sql = @"
                        SELECT
                            masv,
                            hoten,
                            mamon,
                            ngaythi,
                            diemthi
                        FROM ket_qua
                        ORDER BY masv";

                    using (SqlDataAdapter adapter =
                           new SqlDataAdapter(sql, conn))
                    {
                        DataTable dt =
                            new DataTable();

                        adapter.Fill(dt);

                        dgvKetQua.DataSource = dt;
                    }
                }

                DinhDangBang();

                TaoCotSuaXoa();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Lỗi tải danh sách:\n" +
                    ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // =====================================================
        // 7. ĐỊNH DẠNG DATAGRIDVIEW
        // =====================================================

        private void DinhDangBang()
        {
            if (dgvKetQua.Columns.Count == 0)
                return;

            if (dgvKetQua.Columns.Contains("masv"))
                dgvKetQua.Columns["masv"].HeaderText =
                    "Mã sinh viên";

            if (dgvKetQua.Columns.Contains("hoten"))
                dgvKetQua.Columns["hoten"].HeaderText =
                    "Họ tên";

            if (dgvKetQua.Columns.Contains("mamon"))
                dgvKetQua.Columns["mamon"].HeaderText =
                    "Mã môn";

            if (dgvKetQua.Columns.Contains("ngaythi"))
            {
                dgvKetQua.Columns["ngaythi"].HeaderText =
                    "Ngày thi";

                dgvKetQua.Columns["ngaythi"]
                    .DefaultCellStyle
                    .Format = "dd/MM/yyyy";
            }

            if (dgvKetQua.Columns.Contains("diemthi"))
                dgvKetQua.Columns["diemthi"].HeaderText =
                    "Điểm thi";
        }

        // =====================================================
        // 8. TẠO CỘT SỬA / XÓA
        // =====================================================

        private void TaoCotSuaXoa()
        {
            if (!dgvKetQua.Columns.Contains("Sua"))
            {
                DataGridViewButtonColumn btnSua =
                    new DataGridViewButtonColumn();

                btnSua.Name = "Sua";

                btnSua.HeaderText = "Sửa";

                btnSua.Text = "Sửa";

                btnSua.UseColumnTextForButtonValue =
                    true;

                dgvKetQua.Columns.Add(btnSua);
            }

            if (!dgvKetQua.Columns.Contains("Xoa"))
            {
                DataGridViewButtonColumn btnXoa =
                    new DataGridViewButtonColumn();

                btnXoa.Name = "Xoa";

                btnXoa.HeaderText = "Xóa";

                btnXoa.Text = "Xóa";

                btnXoa.UseColumnTextForButtonValue =
                    true;

                dgvKetQua.Columns.Add(btnXoa);
            }
        }

        // =====================================================
        // 9. NÚT LƯU
        // =====================================================

        private void BtnLuu_Click(
            object sender,
            EventArgs e)
        {
            // Kiểm tra mã SV
            if (string.IsNullOrWhiteSpace(txtMaSV.Text))
            {
                MessageBox.Show(
                    "Vui lòng nhập mã sinh viên!");

                txtMaSV.Focus();

                return;
            }

            // Kiểm tra họ tên
            if (string.IsNullOrWhiteSpace(txtHoTen.Text))
            {
                MessageBox.Show(
                    "Vui lòng nhập họ tên!");

                txtHoTen.Focus();

                return;
            }

            // Kiểm tra mã môn
            if (cboMaMon.SelectedValue == null)
            {
                MessageBox.Show(
                    "Vui lòng chọn mã môn!");

                return;
            }

            // Kiểm tra điểm
            if (!double.TryParse(
                    txtDiemThi.Text,
                    out double diem))
            {
                MessageBox.Show(
                    "Điểm thi phải là số!");

                txtDiemThi.Focus();

                return;
            }

            if (diem < 0 || diem > 10)
            {
                MessageBox.Show(
                    "Điểm thi phải từ 0 đến 10!");

                txtDiemThi.Focus();

                return;
            }

            try
            {
                using (SqlConnection conn =
                       new SqlConnection(connectionString))
                {
                    conn.Open();

                    // -----------------------------------------
                    // KIỂM TRA MÃ SV ĐÃ TỒN TẠI CHƯA
                    // -----------------------------------------

                    string sqlCheck = @"
                        SELECT COUNT(*)
                        FROM ket_qua
                        WHERE masv = @masv";

                    using (SqlCommand cmdCheck =
                           new SqlCommand(
                               sqlCheck,
                               conn))
                    {
                        cmdCheck.Parameters.AddWithValue(
                            "@masv",
                            txtMaSV.Text.Trim());

                        int count =
                            Convert.ToInt32(
                                cmdCheck.ExecuteScalar());

                        if (count > 0)
                        {
                            MessageBox.Show(
                                "Mã sinh viên đã tồn tại!");

                            return;
                        }
                    }

                    // -----------------------------------------
                    // INSERT
                    // -----------------------------------------

                    string sql = @"
                        INSERT INTO ket_qua
                        (
                            masv,
                            hoten,
                            mamon,
                            ngaythi,
                            diemthi
                        )
                        VALUES
                        (
                            @masv,
                            @hoten,
                            @mamon,
                            @ngaythi,
                            @diemthi
                        )";

                    using (SqlCommand cmd =
                           new SqlCommand(
                               sql,
                               conn))
                    {
                        cmd.Parameters.AddWithValue(
                            "@masv",
                            txtMaSV.Text.Trim());

                        cmd.Parameters.AddWithValue(
                            "@hoten",
                            txtHoTen.Text.Trim());

                        cmd.Parameters.AddWithValue(
                            "@mamon",
                            cboMaMon.SelectedValue
                                .ToString());

                        cmd.Parameters.AddWithValue(
                            "@ngaythi",
                            dtpNgayThi.Value.Date);

                        cmd.Parameters.AddWithValue(
                            "@diemthi",
                            diem);

                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show(
                    "Lưu kết quả thành công!",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                LoadKetQua();

                XoaTrang();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Lỗi lưu dữ liệu:\n" +
                    ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // =====================================================
        // 10. TÌM KIẾM
        // =====================================================

        private void BtnTimKiem_Click(
            object sender,
            EventArgs e)
        {
            try
            {
                using (SqlConnection conn =
                       new SqlConnection(connectionString))
                {
                    conn.Open();

                    string sql = @"
                        SELECT
                            masv,
                            hoten,
                            mamon,
                            ngaythi,
                            diemthi
                        FROM ket_qua
                        WHERE
                            (
                                @mamon = ''
                                OR mamon LIKE
                                '%' + @mamon + '%'
                            )
                        AND
                            (
                                @hoten = ''
                                OR hoten LIKE
                                N'%' + @hoten + '%'
                            )
                        ORDER BY masv";

                    using (SqlCommand cmd =
                           new SqlCommand(
                               sql,
                               conn))
                    {
                        cmd.Parameters.AddWithValue(
                            "@mamon",
                            txtTimMaMon.Text.Trim());

                        cmd.Parameters.AddWithValue(
                            "@hoten",
                            txtTimHoTen.Text.Trim());

                        using (SqlDataAdapter adapter =
                               new SqlDataAdapter(cmd))
                        {
                            DataTable dt =
                                new DataTable();

                            adapter.Fill(dt);

                            dgvKetQua.DataSource = dt;
                        }
                    }
                }

                DinhDangBang();

                TaoCotSuaXoa();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Lỗi tìm kiếm:\n" +
                    ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // =====================================================
        // 11. HIỂN THỊ TẤT CẢ
        // =====================================================

        private void BtnHienThiTatCa_Click(
            object sender,
            EventArgs e)
        {
            txtTimMaMon.Clear();

            txtTimHoTen.Clear();

            LoadKetQua();
        }

        // =====================================================
        // 12. CLICK SỬA / XÓA
        // =====================================================

        private void DgvKetQua_CellContentClick(
            object sender,
            DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            if (dgvKetQua.Columns[e.ColumnIndex]
                .Name == "Sua")
            {
                string masv =
                    dgvKetQua.Rows[e.RowIndex]
                    .Cells["masv"]
                    .Value
                    .ToString();

                SuaKetQua(masv);
            }

            if (dgvKetQua.Columns[e.ColumnIndex]
                .Name == "Xoa")
            {
                string masv =
                    dgvKetQua.Rows[e.RowIndex]
                    .Cells["masv"]
                    .Value
                    .ToString();

                DialogResult result =
                    MessageBox.Show(
                        "Bạn có chắc muốn xóa kết quả " +
                        masv + "?",
                        "Xác nhận xóa",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    XoaKetQua(masv);
                }
            }
        }

        // =====================================================
        // 13. XÓA
        // =====================================================

        private void XoaKetQua(string masv)
        {
            try
            {
                using (SqlConnection conn =
                       new SqlConnection(connectionString))
                {
                    conn.Open();

                    string sql = @"
                        DELETE FROM ket_qua
                        WHERE masv = @masv";

                    using (SqlCommand cmd =
                           new SqlCommand(
                               sql,
                               conn))
                    {
                        cmd.Parameters.AddWithValue(
                            "@masv",
                            masv);

                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show(
                    "Xóa kết quả thành công!",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                LoadKetQua();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Lỗi xóa dữ liệu:\n" +
                    ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // =====================================================
        // 14. SỬA
        // =====================================================

        private void SuaKetQua(string masv)
        {
            try
            {
                using (SqlConnection conn =
                       new SqlConnection(connectionString))
                {
                    conn.Open();

                    string sql = @"
                        SELECT
                            masv,
                            hoten,
                            mamon,
                            ngaythi,
                            diemthi
                        FROM ket_qua
                        WHERE masv = @masv";

                    using (SqlCommand cmd =
                           new SqlCommand(
                               sql,
                               conn))
                    {
                        cmd.Parameters.AddWithValue(
                            "@masv",
                            masv);

                        using (SqlDataReader reader =
                               cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                txtMaSV.Text =
                                    reader["masv"]
                                    .ToString();

                                txtHoTen.Text =
                                    reader["hoten"]
                                    .ToString();

                                cboMaMon.SelectedValue =
                                    reader["mamon"]
                                    .ToString();

                                dtpNgayThi.Value =
                                    Convert.ToDateTime(
                                        reader["ngaythi"]);

                                txtDiemThi.Text =
                                    reader["diemthi"]
                                    .ToString();
                            }
                        }
                    }
                }

                // ---------------------------------------------
                // ĐỔI NÚT LƯU THÀNH CẬP NHẬT
                // ---------------------------------------------

                btnLuu.Text = "CẬP NHẬT";

                btnLuu.Click -= BtnLuu_Click;

                btnLuu.Click += BtnCapNhat_Click;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Lỗi lấy dữ liệu:\n" +
                    ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // =====================================================
        // 15. CẬP NHẬT
        // =====================================================

        private void BtnCapNhat_Click(
            object sender,
            EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtHoTen.Text))
            {
                MessageBox.Show(
                    "Vui lòng nhập họ tên!");

                return;
            }

            if (!double.TryParse(
                    txtDiemThi.Text,
                    out double diem))
            {
                MessageBox.Show(
                    "Điểm thi phải là số!");

                return;
            }

            if (diem < 0 || diem > 10)
            {
                MessageBox.Show(
                    "Điểm phải từ 0 đến 10!");

                return;
            }

            try
            {
                using (SqlConnection conn =
                       new SqlConnection(connectionString))
                {
                    conn.Open();

                    string sql = @"
                        UPDATE ket_qua
                        SET
                            hoten = @hoten,
                            mamon = @mamon,
                            ngaythi = @ngaythi,
                            diemthi = @diemthi
                        WHERE masv = @masv";

                    using (SqlCommand cmd =
                           new SqlCommand(
                               sql,
                               conn))
                    {
                        cmd.Parameters.AddWithValue(
                            "@masv",
                            txtMaSV.Text.Trim());

                        cmd.Parameters.AddWithValue(
                            "@hoten",
                            txtHoTen.Text.Trim());

                        cmd.Parameters.AddWithValue(
                            "@mamon",
                            cboMaMon.SelectedValue
                                .ToString());

                        cmd.Parameters.AddWithValue(
                            "@ngaythi",
                            dtpNgayThi.Value.Date);

                        cmd.Parameters.AddWithValue(
                            "@diemthi",
                            diem);

                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show(
                    "Cập nhật thành công!",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                LoadKetQua();

                XoaTrang();

                // ---------------------------------------------
                // TRẢ LẠI NÚT LƯU
                // ---------------------------------------------

                btnLuu.Text = "LƯU";

                btnLuu.Click -= BtnCapNhat_Click;

                btnLuu.Click += BtnLuu_Click;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Lỗi cập nhật:\n" +
                    ex.Message,
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // =====================================================
        // 16. XÓA TRẮNG FORM
        // =====================================================

        private void XoaTrang()
        {
            txtMaSV.Clear();

            txtHoTen.Clear();

            txtDiemThi.Clear();

            dtpNgayThi.Value =
                DateTime.Now;

            if (cboMaMon.Items.Count > 0)
            {
                cboMaMon.SelectedIndex = 0;
            }

            txtMaSV.Focus();
        }
    }
}
