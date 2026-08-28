using System;
using System.Collections.Generic;
using System.IO;
using OfficeOpenXml;
using TorobScanner.Models;

namespace TorobScanner.Services;

/// <summary>
/// سرویس Import/Export:
/// ✅ رفع باگ ۶: Import هوشمند — خط اول فقط اگر واقعاً سرصفحه باشد رد می‌شود
///    (هر خطی که لینک http معتبر داشته باشد پردازش می‌شود)
/// ✅ خروجی اکسل: ستون‌های قیمت قبلی و تغییر قیمت اضافه شد
/// </summary>
public class ImportExportService
{
    public List<SavedProduct> ImportLinks(string filePath)
    {
        var list = new List<SavedProduct>();
        var lines = File.ReadAllLines(filePath);

        foreach (var rawLine in lines)
        {
            var line = rawLine?.Trim() ?? "";
            if (line.Length == 0) continue;

            var parts = line.Split(new[] { ',', '\t' }, 2);
            if (parts.Length == 2 && parts[1].Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var name = parts[0].Trim().Trim('"');
                list.Add(new SavedProduct
                {
                    ProductName = name.Length > 0 ? name : "محصول جدید",
                    TorobUrl = parts[1].Trim()
                });
            }
            else if (parts.Length == 1 && line.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new SavedProduct { ProductName = "محصول جدید", TorobUrl = line });
            }
            // خطوط بدون لینک معتبر (مثل سرصفحه) به‌طور طبیعی نادیده گرفته می‌شوند
        }
        return list;
    }

    public void ExportToExcel(List<SavedProduct> data, string filePath)
    {
        ExcelPackage.License.SetNonCommercialPersonal("TorobIntelligence");
        using var package = new ExcelPackage(new FileInfo(filePath));
        var ws = package.Workbook.Worksheets.Add("گزارش قیمت‌ها");

        ws.Cells["A1"].Value = "نام محصول";
        ws.Cells["B1"].Value = "کمترین قیمت (تومان)";
        ws.Cells["C1"].Value = "قیمت قبلی";
        ws.Cells["D1"].Value = "تغییر قیمت";
        ws.Cells["E1"].Value = "فروشگاه";
        ws.Cells["F1"].Value = "دسته‌بندی";
        ws.Cells["G1"].Value = "تاریخ بروزرسانی";
        ws.Cells["H1"].Value = "لینک";

        using (var range = ws.Cells["A1:H1"])
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(10, 12, 16));
            range.Style.Font.Color.SetColor(System.Drawing.Color.White);
        }

        int row = 2;
        foreach (var item in data)
        {
            ws.Cells[$"A{row}"].Value = item.ProductName;
            ws.Cells[$"B{row}"].Value = (double)item.LastPrice;
            ws.Cells[$"C{row}"].Value = (double)item.PreviousPrice;
            ws.Cells[$"D{row}"].Value = (double)item.PriceDelta;
            if (item.PriceDelta < 0)
                ws.Cells[$"D{row}"].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(0, 160, 80));
            else if (item.PriceDelta > 0)
                ws.Cells[$"D{row}"].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(220, 60, 50));
            ws.Cells[$"E{row}"].Value = item.StoreName;
            ws.Cells[$"F{row}"].Value = item.CategoryName;
            ws.Cells[$"G{row}"].Value = item.LastUpdate.ToString("yyyy-MM-dd HH:mm");
            ws.Cells[$"H{row}"].Value = item.TorobUrl;
            row++;
        }
        ws.Cells.AutoFitColumns(0);
        package.Save();
    }
}
