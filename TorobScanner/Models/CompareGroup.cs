namespace TorobScanner.Models;

/// <summary>
/// ✨ v3.1 — گروه مقایسه: محصولاتِ لینک‌شده‌ی یک کالای واحد از فروشگاه‌های مختلف
/// (مثلا «ارپیچیو سام‌کیش» + «ارپیچیو کافی‌کالا») که قیمت‌هایشان در یک تب کنار هم دیده می‌شود.
/// </summary>
public class CompareGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>تعداد محصولاتِ عضو گروه (از دیتابیس پر می‌شود)</summary>
    public int ItemCount { get; set; }
}
