using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using TorobScanner.Models;

namespace TorobScanner.Data;

/// <summary>
/// مدیریت دیتابیس SQLite — نسخه بهینه‌شده:
/// ✅ مسیر مطلق و پایدار (رفع باگ دیتابیس‌های پراکنده)
/// ✅ حالت WAL برای هم‌نویسی امن چندتسکی
/// ✅ حفظ تاریخچه PreviousPrice فقط وقتی قیمت واقعاً تغییر کرده
/// ✅ مهاجرت خودکار از دیتابیس‌های قدیمی پراکنده
/// </summary>
public class DatabaseManager
{
    private readonly string _connectionString;

    public DatabaseManager()
    {
        var dbPath = ResolveDbPath();
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using (var pragma = new SqliteCommand("PRAGMA journal_mode=WAL;", connection))
            pragma.ExecuteNonQuery();

        string tableCategories = "CREATE TABLE IF NOT EXISTS Categories (Name TEXT PRIMARY KEY);";
        string tableProducts = @"
            CREATE TABLE IF NOT EXISTS SavedProducts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductName TEXT, TorobUrl TEXT UNIQUE, CategoryName TEXT DEFAULT 'عمومی',
                LastPrice REAL, StoreName TEXT, PreviousPrice REAL, LastUpdate TEXT, CreatedAt TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_products_category ON SavedProducts(CategoryName);
            CREATE INDEX IF NOT EXISTS idx_products_lastupdate ON SavedProducts(LastUpdate);";

        using (var cmd1 = new SqliteCommand(tableCategories, connection)) cmd1.ExecuteNonQuery();
        using (var cmd2 = new SqliteCommand(tableProducts, connection)) cmd2.ExecuteNonQuery();
        using (var cmd3 = new SqliteCommand("INSERT OR IGNORE INTO Categories (Name) VALUES ('عمومی');", connection))
            cmd3.ExecuteNonQuery();
    }

    /// <summary>
    /// ✅ رفع باگ ۱۱: مسیر دیتابیس مطلق کنار فایل اجرایی — دیگر وابسته به Working Directory نیست.
    /// اضافه‌تر: اگر دیتابیس جدید وجود نداشته باشد، بزرگ‌ترین دیتابیس قدیمی پیدا و کپی می‌شود
    /// (کاربر چند فایل db پراکنده داشت؛ داده‌هایشان نجات پیدا می‌کند).
    /// </summary>
    private static string ResolveDbPath()
    {
        var primary = Path.Combine(AppContext.BaseDirectory, "torob_pro_v2.db");
        if (File.Exists(primary)) return primary;

        try
        {
            var legacyNames = new[] { "torob_pro_v2.db", "torob_intelligence.db", "premium_scanner.db", "coffee_intelligence.db", "torob_pro.db" };
            var candidates = legacyNames
                .Select(n => Path.Combine(Environment.CurrentDirectory, n))
                .Where(File.Exists)
                .OrderByDescending(f => new FileInfo(f).Length)
                .ToList();

            if (candidates.Count > 0)
                File.Copy(candidates[0], primary);
        }
        catch { /* مهاجرت اختیاری است — هرگز برنامه را نیندازیم */ }

        return primary;
    }

    public List<string> GetAllCategories()
    {
        var list = new List<string>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = new SqliteCommand("SELECT Name FROM Categories ORDER BY Name", connection);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader["Name"]?.ToString();
            if (!string.IsNullOrEmpty(name)) list.Add(name);
        }
        return list;
    }

    public void AddCategory(string name)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = new SqliteCommand("INSERT OR IGNORE INTO Categories (Name) VALUES (@Name)", connection);
        cmd.Parameters.AddWithValue("@Name", name);
        cmd.ExecuteNonQuery();
    }

    public void DeleteCategory(string name)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            using (var cmd = new SqliteCommand("DELETE FROM Categories WHERE Name=@Name", connection, tx))
            {
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.ExecuteNonQuery();
            }
            using (var cmd2 = new SqliteCommand("UPDATE SavedProducts SET CategoryName='عمومی' WHERE CategoryName=@Name", connection, tx))
            {
                cmd2.Parameters.AddWithValue("@Name", name);
                cmd2.ExecuteNonQuery();
            }
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    public void DeleteProductsInCategory(string name)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = new SqliteCommand("DELETE FROM SavedProducts WHERE CategoryName=@Name", connection);
        cmd.Parameters.AddWithValue("@Name", name);
        cmd.ExecuteNonQuery();
    }

    public List<SavedProduct> GetAllProducts()
    {
        var list = new List<SavedProduct>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = new SqliteCommand("SELECT * FROM SavedProducts ORDER BY LastUpdate DESC", connection);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new SavedProduct
            {
                Id = Convert.ToInt32(reader["Id"]),
                ProductName = reader["ProductName"]?.ToString() ?? "",
                TorobUrl = reader["TorobUrl"]?.ToString() ?? "",
                CategoryName = reader["CategoryName"]?.ToString() ?? "عمومی",
                LastPrice = ToDecimal(reader["LastPrice"]),
                StoreName = reader["StoreName"]?.ToString() ?? "نامشخص",
                PreviousPrice = ToDecimal(reader["PreviousPrice"]),
                LastUpdate = DateTime.TryParse(reader["LastUpdate"]?.ToString(), out var lu) ? lu : DateTime.Now,
                CreatedAt = DateTime.TryParse(reader["CreatedAt"]?.ToString(), out var ca) ? ca : DateTime.Now
            });
        }
        return list;
    }

    private static decimal ToDecimal(object? value)
    {
        if (value == null || value == DBNull.Value) return 0;
        try { return Convert.ToDecimal(value); } catch { return 0; }
    }

    /// <summary>
    /// ✅ رفع باگ ۷: PreviousPrice فقط وقتی قیمت واقعاً تغییر کند بازنویسی می‌شود؛
    /// بعد از هر اسکن مجدد، نشانگر «افزایش/کاهش» پاک نمی‌شود.
    /// </summary>
    public void SaveProduct(SavedProduct product)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        string query = @"
            INSERT INTO SavedProducts (ProductName, TorobUrl, CategoryName, LastPrice, StoreName, PreviousPrice, LastUpdate, CreatedAt)
            VALUES (@ProductName, @TorobUrl, @CategoryName, @LastPrice, @StoreName, @PreviousPrice, @LastUpdate, @CreatedAt)
            ON CONFLICT(TorobUrl) DO UPDATE SET
                ProductName=@ProductName, CategoryName=@CategoryName,
                PreviousPrice=CASE WHEN LastPrice != @LastPrice THEN LastPrice ELSE PreviousPrice END,
                LastPrice=@LastPrice, StoreName=@StoreName, LastUpdate=@LastUpdate";
        using var cmd = new SqliteCommand(query, connection);
        cmd.Parameters.AddWithValue("@ProductName", product.ProductName ?? "");
        cmd.Parameters.AddWithValue("@TorobUrl", product.TorobUrl ?? "");
        cmd.Parameters.AddWithValue("@CategoryName", product.CategoryName ?? "عمومی");
        cmd.Parameters.AddWithValue("@LastPrice", product.LastPrice);
        cmd.Parameters.AddWithValue("@StoreName", product.StoreName ?? "نامشخص");
        cmd.Parameters.AddWithValue("@PreviousPrice", product.PreviousPrice);
        cmd.Parameters.AddWithValue("@LastUpdate", (product.LastUpdate == default ? DateTime.Now : product.LastUpdate).ToString("o"));
        cmd.Parameters.AddWithValue("@CreatedAt", (product.CreatedAt == default ? DateTime.Now : product.CreatedAt).ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void DeleteProduct(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = new SqliteCommand("DELETE FROM SavedProducts WHERE Id=@Id", connection);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }
}
