using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Xml.Linq;
namespace DangKyTinChi.Phong.Data
{
 public static class ConnectionSettings
 {
  private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DangKyTinChiPhong","connection.xml");
  public static string Load()
  {
   if(File.Exists(FilePath))
   {
    try { return Build(XDocument.Load(FilePath)); }
    catch(Exception ex) when(ex is System.Xml.XmlException || ex is ArgumentException || ex is IOException || ex is FormatException || ex is UnauthorizedAccessException)
    { /* Cấu hình hỏng: dùng App.config, người dùng có thể mở Kết nối để lưu lại. */ }
   }
   var item=ConfigurationManager.ConnectionStrings["DangKyTinChi"];
   return item==null ? "Data Source=(LocalDB)\\MSSQLLocalDB;Initial Catalog=DangKyTinChi;Integrated Security=True;Connect Timeout=5" : item.ConnectionString;
  }
  private static string Build(XDocument doc)
  {
   var root=doc.Element("connection");
   if(root==null) throw new ArgumentException("Cấu hình không hợp lệ.");
   return Make((string)root.Element("server"),(string)root.Element("database"), (bool?)root.Element("encrypt") ?? false);
  }
  public static string Make(string server,string database,bool encrypt)
  {
   if(string.IsNullOrWhiteSpace(server)||string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Nhập tên server và database.");
   return new SqlConnectionStringBuilder { DataSource=server.Trim(),InitialCatalog=database.Trim(),IntegratedSecurity=true,
    ConnectTimeout=5,Encrypt=encrypt,TrustServerCertificate=!encrypt }.ConnectionString;
  }
  public static void Save(string server,string database,bool encrypt)
  {
   Make(server,database,encrypt);
   Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
   new XDocument(new XElement("connection",new XElement("server",server.Trim()),new XElement("database",database.Trim()),new XElement("encrypt",encrypt))).Save(FilePath);
  }
 }
}
