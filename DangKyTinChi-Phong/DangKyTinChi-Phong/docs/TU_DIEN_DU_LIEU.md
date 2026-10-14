# Từ điển dữ liệu

Tên bảng/cột giữ đúng file quy ước. Kiểu dữ liệu và ràng buộc do bộ source này thiết kế. PK/FK/CHECK xem chi tiết trong `database/01_TaoDatabase.sql`.

## KHOA

| Cột | Kiểu | NULL |
|---|---|---|
| ma_khoa | `varchar(20)` | Không |
| ten_khoa | `nvarchar(150)` | Không |
| mo_ta | `nvarchar(500)` | Có |
| trang_thai | `bit` | Không |

## NGANH

| Cột | Kiểu | NULL |
|---|---|---|
| ma_nganh | `varchar(20)` | Không |
| ten_nganh | `nvarchar(150)` | Không |
| ma_khoa | `varchar(20)` | Không |
| mo_ta | `nvarchar(500)` | Có |
| trang_thai | `bit` | Không |

## LOP

| Cột | Kiểu | NULL |
|---|---|---|
| ma_lop | `varchar(20)` | Không |
| ten_lop | `nvarchar(100)` | Không |
| ma_nganh | `varchar(20)` | Không |
| khoa_hoc | `int` | Không |
| nien_khoa | `varchar(9)` | Không |
| trang_thai | `bit` | Không |

## SINH_VIEN

| Cột | Kiểu | NULL |
|---|---|---|
| ma_sv | `varchar(20)` | Không |
| ho_ten | `nvarchar(150)` | Không |
| ngay_sinh | `date` | Không |
| gioi_tinh | `nvarchar(10)` | Không |
| email | `varchar(254)` | Có |
| sdt | `varchar(15)` | Có |
| ma_nganh | `varchar(20)` | Không |
| ma_lop | `varchar(20)` | Không |
| dia_chi | `nvarchar(300)` | Có |
| trang_thai | `bit` | Không |

## GIANG_VIEN

| Cột | Kiểu | NULL |
|---|---|---|
| ma_gv | `varchar(20)` | Không |
| ho_ten | `nvarchar(150)` | Không |
| ngay_sinh | `date` | Không |
| gioi_tinh | `nvarchar(10)` | Không |
| email | `varchar(254)` | Có |
| sdt | `varchar(15)` | Có |
| ma_khoa | `varchar(20)` | Không |
| trang_thai | `bit` | Không |

## TAI_KHOAN

| Cột | Kiểu | NULL |
|---|---|---|
| ma_tk | `bigint IDENTITY` | Không |
| ten_dang_nhap | `varchar(100)` | Không |
| mat_khau_hash | `varchar(512)` | Không |
| vai_tro | `varchar(20)` | Không |
| trang_thai | `bit` | Không |
| email | `varchar(254)` | Có |
| ma_sv | `varchar(20)` | Có |
| ma_gv | `varchar(20)` | Có |
| ngay_tao | `datetime2(0)` | Không |
| lan_dang_nhap_cuoi | `datetime2(0)` | Có |

## HOC_PHAN

| Cột | Kiểu | NULL |
|---|---|---|
| ma_hp | `varchar(20)` | Không |
| ten_hp | `nvarchar(200)` | Không |
| so_tin_chi | `tinyint` | Không |
| loai_hp | `varchar(20)` | Không |
| tin_chi_min | `tinyint` | Không |
| tin_chi_max | `tinyint` | Không |
| trang_thai | `bit` | Không |

## HOC_KY

| Cột | Kiểu | NULL |
|---|---|---|
| ma_hk | `varchar(20)` | Không |
| nam_hoc | `varchar(9)` | Không |
| hoc_ky | `tinyint` | Không |
| tg_bat_dau | `datetime2(0)` | Không |
| tg_ket_thuc | `datetime2(0)` | Không |
| han_huy_dk | `datetime2(0)` | Không |
| trang_thai | `varchar(10)` | Không |

## PHONG_HOC

| Cột | Kiểu | NULL |
|---|---|---|
| ma_phong | `varchar(20)` | Không |
| ten_phong | `nvarchar(100)` | Không |
| toa_nha | `nvarchar(100)` | Không |
| suc_chua | `smallint` | Không |
| loai_phong | `nvarchar(50)` | Không |
| trang_thai | `bit` | Không |

## LOP_HOC_PHAN

| Cột | Kiểu | NULL |
|---|---|---|
| ma_lhp | `varchar(20)` | Không |
| ma_hp | `varchar(20)` | Không |
| ma_gv | `varchar(20)` | Không |
| ma_hk | `varchar(20)` | Không |
| ma_phong | `varchar(20)` | Có |
| si_so_max | `smallint` | Không |
| si_so_hien_tai | `smallint` | Không |
| trang_thai | `varchar(10)` | Không |

## DANG_KY

| Cột | Kiểu | NULL |
|---|---|---|
| ma_dk | `bigint IDENTITY` | Không |
| ma_sv | `varchar(20)` | Không |
| ma_lhp | `varchar(20)` | Không |
| ngay_dk | `datetime2(0)` | Không |
| trang_thai | `varchar(20)` | Không |
| ngay_huy | `datetime2(0)` | Có |

## LICH_HOC

| Cột | Kiểu | NULL |
|---|---|---|
| ma_lich | `bigint IDENTITY` | Không |
| ma_lhp | `varchar(20)` | Không |
| thu | `tinyint` | Không |
| tiet_bd | `tinyint` | Không |
| tiet_kt | `tinyint` | Không |
| ma_phong | `varchar(20)` | Không |
| ngay_bd | `date` | Không |
| ngay_kt | `date` | Không |

## KET_QUA

| Cột | Kiểu | NULL |
|---|---|---|
| ma_kq | `bigint IDENTITY` | Không |
| ma_sv | `varchar(20)` | Không |
| ma_hp | `varchar(20)` | Không |
| diem_qt | `decimal(4,2)` | Có |
| diem_gk | `decimal(4,2)` | Có |
| diem_ck | `decimal(4,2)` | Có |
| diem_tk | `decimal(4,2)` | Có |
| diem_he4 | `decimal(3,2)` | Có |
| diem_chu | `varchar(2)` | Có |
| ket_qua | `varchar(10)` | Có |
