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
/// ✅ رفع باگ ۱۳ (v2.5): حذف Shared Cache — با ۳ نویسنده همزمان (سیمافور اسکنر)
///    Shared Cache خطای «database table is locked» (SQLITE_LOCKED) می‌داد که
///    توسط busy-timeout تکرار نمی‌شود. حالا: Pooling + WAL + Timeout ۳۰ ثانیه.
/// ✅ رفع باگ ۱۸ (v2.5.1): اگر پوشه‌ی برنامه اجازه‌ی نوشتن نداشته باشد (مثل Program Files)
///    به‌جای کرش، دیتابیس به %APPDATA%\TorobScanner منتقل می‌شود (با کپی خودکار داده‌های قبلی).
/// ✅ رفع باگ ۱۸ (v2.5.1): اگر e_sqlite3.dll کنار برنامه نباشد، به‌جای پیام انگلیسیِ
///    نامفهوم DllNotFoundException، پیام فارسیِ گام‌به‌گام نمایش داده می‌شود.
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
            Pooling = true,
            DefaultTimeout = 30
        }.ToString();

        try
        {
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

            // ✨ v3.1: گروه‌های مقایسه — لینک محصولات فروشگاه‌های مختلف در یک تب
            string tableCompare = @"
                CREATE TABLE IF NOT EXISTS CompareGroups (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    CreatedAt TEXT
                );
                CREATE TABLE IF NOT EXISTS CompareItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GroupId INTEGER NOT NULL,
                    ProductId INTEGER NOT NULL,
                    AddedAt TEXT,
                    UNIQUE(GroupId, ProductId)
                );
                CREATE INDEX IF NOT EXISTS idx_compareitems_group ON CompareItems(GroupId);";
            using (var cmd4 = new SqliteCommand(tableCompare, connection)) cmd4.ExecuteNonQuery();
        }
        catch (DllNotFoundException ex) when (IsNativeMissing(ex))
        {
            // فایل native دیتابیس کنار برنامه نیست — کاربر معمولاً فقط exe را کپی کرده است
            throw new InvalidOperationException(
                "فایل موتور دیتابیس (e_sqlite3.dll) کنار برنامه پیدا نشد.\n\n" +
                "راه‌حل: همه‌ی محتویات فایل ZIP را در یک پوشه استخراج کنید و برنامه را از همان‌جا اجرا کنید — " +
                "نه فقط فایل exe را.\n\nجزئیات فنی: " + ex.Message, ex);
        }
        catch (SqliteException ex)
        {
            // پوشه قابل نوشتن نیست یا آنتی‌ویروس فایل را قفل کرده
            throw new InvalidOperationException(
                $"باز کردن دیتابیس ممکن نشد:\n{ex.Message}\n\n" +
                "راه‌حل‌های پیشنهادی:\n" +
                "۱) برنامه را از پوشه‌های محافظت‌شده (Program Files ویندوز) خارج و در پوشه‌ای مثل D:\\TorobScanner اجرا کنید.\n" +
                "۲) اگر برنامه روی درایو OneDrive/Desktop همگام‌سازی است، آن را به پوشه‌ای محلی منتقل کنید.\n" +
                "۳) موقتاً آنتی‌ویروس را بررسی کنید که فایل دیتابیس را قفل نکرده باشد.", ex);
        }
    }

    /// <summary>تشخیص اینکه DllNotFoundException مربوط به SQLitePCLRaw/e_sqlite3 است</summary>
    private static bool IsNativeMissing(DllNotFoundException ex)
        => (ex.Message ?? "").Contains("e_sqlite3", StringComparison.OrdinalIgnoreCase)
        || (ex.Message ?? "").Contains("SQLitePCL", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// ✅ رفع باگ ۱۱ + ۱۸: مسیر دیتابیس مطلق کنار فایل اجرایی — دیگر وابسته به Working Directory نیست.
    /// اضافه‌تر: اگر دیتابیس جدید وجود نداشته باشد، بزرگ‌ترین دیتابیس قدیمی پیدا و کپی می‌شود
    /// (کاربر چند فایل db پراکنده داشت؛ داده‌هایشان نجات پیدا می‌کند).
    /// ✅ v2.5.1: اگر کنار برنامه قابل نوشتن نباشد (Program Files)، از %APPDATA% استفاده می‌شود
    /// و اگر دیتابیس قبلی کنار برنامه بود، خودکار به مکان جدید منتقل می‌شود.
    /// </summary>
    private static string ResolveDbPath()
    {
        var primary = Path.Combine(AppContext.BaseDirectory, "torob_pro_v2.db");

        if (!IsDirectoryWritable(AppContext.BaseDirectory))
        {
            // پوشه‌ی برنامه فقط-خواندنی است → به APPDATA پناه می‌بریم و دیتابیس قبلی را مهاجرت می‌دهیم
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TorobScanner");
            try { Directory.CreateDirectory(appDataDir); } catch { }
            var fallback = Path.Combine(appDataDir, "torob_pro_v2.db");

            try
            {
                if (!File.Exists(fallback))
                {
                    if (File.Exists(primary))
                        File.Copy(primary, fallback);          // حفظ داده‌های فعلی کاربر
                    else
                        TryMigrateLegacyDb(fallback);          // داده‌های پراکنده‌ی قدیمی
                }
            }
            catch { /* مهاجرت اختیاری است — هرگز برنامه را نیندازیم */ }

            return fallback;
        }

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

    /// <summary>بزرگ‌ترین دیتابیس قدیمی پراکنده را در مسیر مقصد کپی می‌کند</summary>
    private static void TryMigrateLegacyDb(string destination)
    {
        var legacyNames = new[] { "torob_pro_v2.db", "torob_intelligence.db", "premium_scanner.db", "coffee_intelligence.db", "torob_pro.db" };
        var candidates = legacyNames
            .Select(n => Path.Combine(AppContext.BaseDirectory, n))
            .Concat(legacyNames.Select(n => Path.Combine(Environment.CurrentDirectory, n)))
            .Where(File.Exists)
            .OrderByDescending(f => new FileInfo(f).Length)
            .ToList();

        if (candidates.Count > 0)
            File.Copy(candidates[0], destination);
    }

    /// <summary>تست امنِ قابلیت نوشتن در پوشه — بدون استثنا</summary>
    private static bool IsDirectoryWritable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, $".write_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
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
            list.Add(MapProduct(reader));
        return list;
    }

    /// <summary>✨ v3.1: مپر مشترک رکورد محصول (GetAllProducts و GetGroupProducts)</summary>
    private static SavedProduct MapProduct(SqliteDataReader reader)
    {
        return new SavedProduct
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
        };
    }

    // ═══════════ گروه‌های مقایسه — لینک محصولات فروشگاه‌های مختلف (✨ v3.1) ═══════════

    /// <summary>ساخت گروه مقایسه جدید و برگرداندن Id آن</summary>
    public int CreateCompareGroup(string name)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = new SqliteCommand(
            "INSERT INTO CompareGroups (Name, CreatedAt) VALUES (@Name, @CreatedAt); SELECT last_insert_rowid();",
            connection);
        cmd.Parameters.AddWithValue("@Name", name);
        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("o"));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>همه‌ی گروه‌های مقایسه همراه با تعداد اعضا</summary>
    public List<CompareGroup> GetCompareGroups()
    {
        var list = new List<CompareGroup>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = new SqliteCommand(@"
            SELECT g.Id, g.Name, g.CreatedAt, COUNT(i.Id) AS Cnt
            FROM CompareGroups g
            LEFT JOIN CompareItems i ON i.GroupId = g.Id
            GROUP BY g.Id
            ORDER BY g.Id DESC", connection);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(MapGroup(reader));
        return list;
    }

    /// <summary>گروه‌هایی که این محصول در آن‌ها عضویت دارد</summary>
    public List<CompareGroup> GetGroupsForProduct(int productId)
    {
        var list = new List<CompareGroup>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = new SqliteCommand(@"
            SELECT g.Id, g.Name, g.CreatedAt,
                   (SELECT COUNT(*) FROM CompareItems x WHERE x.GroupId = g.Id) AS Cnt
            FROM CompareGroups g
            JOIN CompareItems i ON i.GroupId = g.Id AND i.ProductId = @Pid
            ORDER BY g.Id DESC", connection);
        cmd.Parameters.AddWithValue("@Pid", productId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(MapGroup(reader));
        return list;
    }

    private static CompareGroup MapGroup(SqliteDataReader reader)
    {
        return new CompareGroup
        {
            Id = Convert.ToInt32(reader["Id"]),
            Name = reader["Name"]?.ToString() ?? "",
            CreatedAt = DateTime.TryParse(reader["CreatedAt"]?.ToString(), out var ca) ? ca : DateTime.Now,
            ItemCount = Convert.ToInt32(reader["Cnt"])
        };
    }

    /// <summary>اعضای گروه — ارزان‌ترین اول (محصولات حذف‌شده از لیست اصلی خودکار کنار می‌روند)</summary>
    public List<SavedProduct> GetGroupProducts(int groupId)
    {
        var list = new List<SavedProduct>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = new SqliteCommand(@"
            SELECT p.*
            FROM SavedProducts p
            JOIN CompareItems i ON i.ProductId = p.Id
            WHERE i.GroupId = @Gid
            ORDER BY CASE WHEN p.LastPrice > 0 THEN p.LastPrice ELSE 999999999999 END ASC, p.Id ASC", connection);
        cmd.Parameters.AddWithValue("@Gid", groupId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(MapProduct(reader));
        return list;
    }

    /// <summary>افزودن محصول به گروه — اگر قبلا عضو باشد تکرار نمی‌شود</summary>
    public void AddProductToGroup(int groupId, int productId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = new SqliteCommand(
            "INSERT OR IGNORE INTO CompareItems (GroupId, ProductId, AddedAt) VALUES (@Gid, @Pid, @AddedAt)", connection);
        cmd.Parameters.AddWithValue("@Gid", groupId);
        cmd.Parameters.AddWithValue("@Pid", productId);
        cmd.Parameters.AddWithValue("@AddedAt", DateTime.Now.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void RemoveProductFromGroup(int groupId, int productId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = new SqliteCommand(
            "DELETE FROM CompareItems WHERE GroupId=@Gid AND ProductId=@Pid", connection);
        cmd.Parameters.AddWithValue("@Gid", groupId);
        cmd.Parameters.AddWithValue("@Pid", productId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>حذف گروه + لینک‌هایش (خود محصولات حذف نمی‌شوند)</summary>
    public void DeleteCompareGroup(int groupId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            using (var cmd1 = new SqliteCommand("DELETE FROM CompareItems WHERE GroupId=@Gid", connection, tx))
            {
                cmd1.Parameters.AddWithValue("@Gid", groupId);
                cmd1.ExecuteNonQuery();
            }
            using (var cmd2 = new SqliteCommand("DELETE FROM CompareGroups WHERE Id=@Gid", connection, tx))
            {
                cmd2.Parameters.AddWithValue("@Gid", groupId);
                cmd2.ExecuteNonQuery();
            }
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    private static decimal ToDecimal(object? value)
    {
        if (value == null || value == DBNull.Value) return 0;
        try { return Convert.ToDecimal(value); } catch { return 0; }
    }

    /// <summary>
    /// ✅ رفع باگ ۷: PreviousPrice فقط وقتی قیمت واقعاً تغییر کند بازنویسی می‌شود؛
    /// بعد از هر اسکن مجدد، نشانگر «افزایش/کاهش» پاک نمی‌شود.
    /// ✅ رفع باگ جدید (v2.3): Import مجدد یک لینک موجود، قیمت و اسم واقعی را نابود نمی‌کند —
    ///    قیمت ۰ ورودی هرگز قیمت معتبر قبلی را بازنویسی نمی‌کند و
    ///    اسم placeholder («محصول جدید») اسم واقعی موجود را پاک نمی‌کند.
    /// </summary>
    public void SaveProduct(SavedProduct product)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        string query = @"
            INSERT INTO SavedProducts (ProductName, TorobUrl, CategoryName, LastPrice, StoreName, PreviousPrice, LastUpdate, CreatedAt)
            VALUES (@ProductName, @TorobUrl, @CategoryName, @LastPrice, @StoreName, @PreviousPrice, @LastUpdate, @CreatedAt)
            ON CONFLICT(TorobUrl) DO UPDATE SET
                ProductName=CASE
                    WHEN (@ProductName = '' OR @ProductName = 'محصول جدید')
                         AND ProductName != '' THEN ProductName
                    ELSE @ProductName END,
                CategoryName=@CategoryName,
                PreviousPrice=CASE
                    WHEN @LastPrice > 0 AND LastPrice > 0 AND LastPrice != @LastPrice THEN LastPrice
                    ELSE PreviousPrice END,
                LastPrice=CASE WHEN @LastPrice > 0 THEN @LastPrice ELSE LastPrice END,
                StoreName=CASE
                    WHEN (@StoreName = '' OR @StoreName = 'نامشخص')
                         AND StoreName != '' THEN StoreName
                    ELSE @StoreName END,
                LastUpdate=CASE WHEN @LastPrice > 0 THEN @LastUpdate ELSE LastUpdate END";
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
