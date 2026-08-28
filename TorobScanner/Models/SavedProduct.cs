using System;

namespace TorobScanner.Models;

/// <summary>مدل محصول ذخیره‌شده در دیتابیس</summary>
public class SavedProduct
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string TorobUrl { get; set; } = string.Empty;
    public string CategoryName { get; set; } = "عمومی";
    public decimal LastPrice { get; set; }
    public string StoreName { get; set; } = "نامشخص";
    public DateTime LastUpdate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public decimal PreviousPrice { get; set; }

    /// <summary>تفاکت قیمت فعلی با قبلی (مثبت = افزایش)</summary>
    public decimal PriceDelta => LastPrice - PreviousPrice;
}
