namespace qldiemm
{
    partial class txtHoTen
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            lblTieuDe = new Label();
            lblMaSV = new Label();
            txtMaSV = new TextBox();
            lblHoTen = new Label();
            textBox1 = new TextBox();
            lblMaMon = new Label();
            cboMaMon = new ComboBox();
            lblNgayThi = new Label();
            dtpNgayThi = new DateTimePicker();
            lblDiemThi = new Label();
            txtDiemThi = new TextBox();
            btnLuu = new Button();
            grpTimKiem = new GroupBox();
            lblTimMaMon = new Label();
            txtTimMaMon = new TextBox();
            lblTimHoTen = new Label();
            txtTimHoTen = new TextBox();
            btnTimKiem = new Button();
            dgvKetQua = new DataGridView();
            grpTimKiem.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvKetQua).BeginInit();
            SuspendLayout();
            // 
            // lblTieuDe
            // 
            lblTieuDe.AutoSize = true;
            lblTieuDe.Location = new Point(460, 25);
            lblTieuDe.Name = "lblTieuDe";
            lblTieuDe.Size = new Size(160, 20);
            lblTieuDe.TabIndex = 0;
            lblTieuDe.Text = "QUẢN LÝ KẾT QUẢ THI";
            // 
            // lblMaSV
            // 
            lblMaSV.AutoSize = true;
            lblMaSV.Location = new Point(62, 84);
            lblMaSV.Name = "lblMaSV";
            lblMaSV.Size = new Size(94, 20);
            lblMaSV.TabIndex = 1;
            lblMaSV.Text = "Mã sinh viên:";
            // 
            // txtMaSV
            // 
            txtMaSV.Location = new Point(162, 77);
            txtMaSV.Name = "txtMaSV";
            txtMaSV.Size = new Size(255, 27);
            txtMaSV.TabIndex = 2;
            // 
            // lblHoTen
            // 
            lblHoTen.AutoSize = true;
            lblHoTen.Location = new Point(610, 84);
            lblHoTen.Name = "lblHoTen";
            lblHoTen.Size = new Size(57, 20);
            lblHoTen.TabIndex = 3;
            lblHoTen.Text = "Họ tên:";
            // 
            // textBox1
            // 
            textBox1.Location = new Point(673, 77);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(295, 27);
            textBox1.TabIndex = 4;
            // 
            // lblMaMon
            // 
            lblMaMon.AutoSize = true;
            lblMaMon.Location = new Point(62, 125);
            lblMaMon.Name = "lblMaMon";
            lblMaMon.Size = new Size(67, 20);
            lblMaMon.TabIndex = 5;
            lblMaMon.Text = "Mã môn:";
            // 
            // cboMaMon
            // 
            cboMaMon.DropDownStyle = ComboBoxStyle.DropDownList;
            cboMaMon.FormattingEnabled = true;
            cboMaMon.Location = new Point(162, 122);
            cboMaMon.Name = "cboMaMon";
            cboMaMon.Size = new Size(250, 28);
            cboMaMon.TabIndex = 6;
            // 
            // lblNgayThi
            // 
            lblNgayThi.AutoSize = true;
            lblNgayThi.Location = new Point(610, 130);
            lblNgayThi.Name = "lblNgayThi";
            lblNgayThi.Size = new Size(68, 20);
            lblNgayThi.TabIndex = 7;
            lblNgayThi.Text = "Ngày thi:";
            lblNgayThi.Click += lblNgayThi_Click;
            // 
            // dtpNgayThi
            // 
            dtpNgayThi.Format = DateTimePickerFormat.Short;
            dtpNgayThi.Location = new Point(684, 130);
            dtpNgayThi.Name = "dtpNgayThi";
            dtpNgayThi.Size = new Size(149, 27);
            dtpNgayThi.TabIndex = 8;
            // 
            // lblDiemThi
            // 
            lblDiemThi.AutoSize = true;
            lblDiemThi.Location = new Point(64, 177);
            lblDiemThi.Name = "lblDiemThi";
            lblDiemThi.Size = new Size(69, 20);
            lblDiemThi.TabIndex = 9;
            lblDiemThi.Text = "Điểm thi:";
            // 
            // txtDiemThi
            // 
            txtDiemThi.Location = new Point(162, 174);
            txtDiemThi.Name = "txtDiemThi";
            txtDiemThi.Size = new Size(100, 27);
            txtDiemThi.TabIndex = 10;
            // 
            // btnLuu
            // 
            btnLuu.Location = new Point(610, 167);
            btnLuu.Name = "btnLuu";
            btnLuu.Size = new Size(130, 40);
            btnLuu.TabIndex = 11;
            btnLuu.Text = "LƯU";
            btnLuu.UseVisualStyleBackColor = true;
            // 
            // grpTimKiem
            // 
            grpTimKiem.Controls.Add(dgvKetQua);
            grpTimKiem.Controls.Add(btnTimKiem);
            grpTimKiem.Controls.Add(txtTimHoTen);
            grpTimKiem.Controls.Add(lblTimHoTen);
            grpTimKiem.Controls.Add(txtTimMaMon);
            grpTimKiem.Controls.Add(lblTimMaMon);
            grpTimKiem.Location = new Point(68, 213);
            grpTimKiem.Name = "grpTimKiem";
            grpTimKiem.Size = new Size(900, 365);
            grpTimKiem.TabIndex = 12;
            grpTimKiem.TabStop = false;
            grpTimKiem.Text = "TÌM KIẾM";
            // 
            // lblTimMaMon
            // 
            lblTimMaMon.AutoSize = true;
            lblTimMaMon.Location = new Point(24, 37);
            lblTimMaMon.Name = "lblTimMaMon";
            lblTimMaMon.Size = new Size(67, 20);
            lblTimMaMon.TabIndex = 0;
            lblTimMaMon.Text = "Mã môn:";
            lblTimMaMon.Click += lblTimMaMon_Click;
            // 
            // txtTimMaMon
            // 
            txtTimMaMon.Location = new Point(122, 34);
            txtTimMaMon.Name = "txtTimMaMon";
            txtTimMaMon.Size = new Size(125, 27);
            txtTimMaMon.TabIndex = 1;
            // 
            // lblTimHoTen
            // 
            lblTimHoTen.AutoSize = true;
            lblTimHoTen.Location = new Point(570, 34);
            lblTimHoTen.Name = "lblTimHoTen";
            lblTimHoTen.Size = new Size(57, 20);
            lblTimHoTen.TabIndex = 2;
            lblTimHoTen.Text = "Họ tên:";
            // 
            // txtTimHoTen
            // 
            txtTimHoTen.Location = new Point(644, 27);
            txtTimHoTen.Name = "txtTimHoTen";
            txtTimHoTen.Size = new Size(125, 27);
            txtTimHoTen.TabIndex = 3;
            // 
            // btnTimKiem
            // 
            btnTimKiem.Location = new Point(633, 54);
            btnTimKiem.Name = "btnTimKiem";
            btnTimKiem.Size = new Size(90, 46);
            btnTimKiem.TabIndex = 4;
            btnTimKiem.Text = "TÌM KIẾM";
            btnTimKiem.UseVisualStyleBackColor = true;
            // 
            // dgvKetQua
            // 
            dgvKetQua.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvKetQua.Location = new Point(94, 108);
            dgvKetQua.Name = "dgvKetQua";
            dgvKetQua.RowHeadersWidth = 51;
            dgvKetQua.Size = new Size(900, 250);
            dgvKetQua.TabIndex = 5;
            // 
            // txtHoTen
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1165, 609);
            Controls.Add(grpTimKiem);
            Controls.Add(btnLuu);
            Controls.Add(txtDiemThi);
            Controls.Add(lblDiemThi);
            Controls.Add(dtpNgayThi);
            Controls.Add(lblNgayThi);
            Controls.Add(cboMaMon);
            Controls.Add(lblMaMon);
            Controls.Add(textBox1);
            Controls.Add(lblHoTen);
            Controls.Add(txtMaSV);
            Controls.Add(lblMaSV);
            Controls.Add(lblTieuDe);
            Name = "txtHoTen";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Quản lý kết quả thi";
            Load += Form1_Load;
            grpTimKiem.ResumeLayout(false);
            grpTimKiem.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvKetQua).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblTieuDe;
        private Label lblMaSV;
        private TextBox txtMaSV;
        private Label lblHoTen;
        private TextBox textBox1;
        private Label lblMaMon;
        private ComboBox cboMaMon;
        private Label lblNgayThi;
        private DateTimePicker dtpNgayThi;
        private Label lblDiemThi;
        private TextBox txtDiemThi;
        private Button btnLuu;
        private GroupBox grpTimKiem;
        private Label lblTimMaMon;
        private Label lblTimHoTen;
        private TextBox txtTimMaMon;
        private Button btnTimKiem;
        private TextBox txtTimHoTen;
        private DataGridView dgvKetQua;
    }
}
