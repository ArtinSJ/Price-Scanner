using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TorobScanner.Services;
using TorobScanner.Views;

namespace TorobScanner;

/// <summary>
/// نقطه شروع برنامه — با هندلرهای سراسری خطا:
/// ✅ هیچ خطای مدیریت‌نشده‌ای دیگر برنامه را بی‌صدا نمی‌بندد (رفع باگ ۱ در سطح برنامه)
/// ✅ رفع باگ ۱۷ (v2.5.1): پیام خطا حالا «علت واقعی» را نشان می‌دهد، نه پوسته‌ی XamlParseException را.
/// ✨ v2.7: بارگذاری تنظیمات + اعمال تم ذخیره‌شده قبل از باز شدن پنجره‌ها
/// ✨ v2.7: بررسی خودکار بروزرسانی هنگام شروع (قابل خاموش‌کردن از تنظیمات)
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += App_UnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += App_DomainUnhandledException;

        // ✨ v2.7: تنظیمات + تم باید قبل از ساخت MainWindow اعمال شوند
        SettingsService.Load();
        try { ThemeService.ApplySaved(); }
        catch (Exception ex) { Logger.Error("Theme/ApplySaved", "", ex.ToString()); }

        // ✨ v2.7: بررسی خودکار بروزرسانی — با تاخیر ۴ ثانیه تا UI کاملاً جا بیفتد
        StartAutoUpdateCheck();
    }

    /// <summary>
    /// ✨ v2.7: اگر «بررسی خودکار هنگام شروع» روشن باشد:
    ///   • نصب خودکار روشن → پنجره‌ی بروزرسانی در حالت AutoMode باز می‌شود
    ///     (دانلود و نصب بدون هیچ سوال و ری‌استارت خودکار)
    ///   • نصب خودکار خاموش → پنجره‌ی بروزرسانی عادی باز می‌شود تا کاربر تصمیم بگیرد
    /// هیچ خطای شبکه‌ای مزاحم کاربر نمی‌شود — فقط لاگ.
    /// </summary>
    private async void StartAutoUpdateCheck()
    {
        try
        {
            if (!SettingsService.Current.AutoCheckUpdates) return;

            await Task.Delay(4000);
            if (Application.Current == null) return;

            var service = new UpdateService();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var info = await service.CheckForUpdateAsync(timeout.Token);

            if (!info.UpdateAvailable) return;

            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var win = new UpdateWindow
                    {
                        // AutoMode=true → دانلود و نصب بدون سوال؛ false → تصمیم با کاربر
                        AutoMode = SettingsService.Current.AutoInstallUpdates,
                        Owner = Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible)
                    };
                    win.Show();
                }
                catch (Exception ex)
                {
                    Logger.Error("Update/AutoOpen", "", ex.ToString());
                }
            });
        }
        catch (OperationCanceledException)
        {
            // اینترنت کند/فیلترشکن خاموش — عادی است، لاگ لازم نیست
        }
        catch (Exception ex)
        {
            Logger.Error("Update/AutoCheck", "", ex.ToString());
        }
    }

    /// <summary>باز کردن زنجیره InnerException — پیامِ گویاترین لایه (علت واقعی) را برمی‌گرداند</summary>
    internal static string RootMessage(Exception? ex)
    {
        if (ex == null) return "نامشخص";
        var best = ex.Message;
        var cur = ex.InnerException;
        int depth = 0;
        while (cur != null && depth++ < 10)
        {
            if (!string.IsNullOrWhiteSpace(cur.Message))
                best = cur.Message;
            cur = cur.InnerException;
        }
        return best;
    }

    /// <summary>مسیر کامل فایل لاگ برای نمایش به کاربر</summary>
    internal static string LogPath =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "error_log.txt");

    /// <summary>
    /// نمایش خطا + تصمیم درباره ادامه کار:
    /// اگر هنوز هیچ پنجره‌ای باز نشده (کرش هنگام استارت‌آپ)، برنامه با پیام بسته می‌شود
    /// تا پروسه‌ی بدون‌پنجره باقی نماند.
    /// </summary>
    private void ReportFatal(Exception ex, string title)
    {
        Logger.Error("Global", "", ex.ToString());

        bool noWindow = Windows.OfType<Window>().All(w => !w.IsVisible);
        string hint = noWindow
            ? "\n\n💡 راهنمای سریع:\n" +
              "• اگر فقط فایل exe را کپی کرده‌اید، همه‌ی فایل‌های کنار آن (مخصوصاً e_sqlite3.dll) باید همراه برنامه باشند.\n" +
              "• اگر برنامه داخل پوشه‌ی محافظت‌شده است (مثل Program Files)، آن را به پوشه‌ای مثل D:\\TorobScanner منتقل کنید.\n"
            : "";
        string extra = noWindow
            ? "\nبرنامه اکنون بسته می‌شود."
            : "";

        MessageBox.Show(
            $"خطای غیرمنتظره‌ای رخ داد:\n\n{RootMessage(ex)}{hint}{extra}\n" +
            $"جزئیات فنی کامل در این فایل ثبت شد:\n{LogPath}",
            title, MessageBoxButton.OK, noWindow ? MessageBoxImage.Error : MessageBoxImage.Warning);

        if (noWindow)
        {
            // بدون پنجره = ادامه دادن بی‌معنی است؛ بستن تمیز (نه پروسه‌ی شبح)
            try { Shutdown(-1); } catch { }
            Environment.Exit(1);
        }
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        ReportFatal(e.Exception, "خطا");
        e.Handled = true;
    }

    private void App_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error("Global/Task", "", e.Exception.ToString());
        e.SetObserved();
    }

    private void App_DomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Logger.Error("Global/Domain", "", ex?.ToString() ?? "unknown");
        MessageBox.Show(
            $"خطای بحرانی:\n\n{RootMessage(ex)}\n\n" +
            $"جزئیات فنی کامل در این فایل ثبت شد:\n{LogPath}",
            "خطای بحرانی", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
