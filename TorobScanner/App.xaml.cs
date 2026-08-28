using System;
using System.Threading.Tasks;
using System.Windows;
using TorobScanner.Services;

namespace TorobScanner;

/// <summary>
/// نقطه شروع برنامه — با هندلرهای سراسری خطا:
/// ✅ هیچ خطای مدیریت‌نشده‌ای دیگر برنامه را بی‌صدا نمی‌بندد (رفع باگ ۱ در سطح برنامه)
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += App_UnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += App_DomainUnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error("Global/UI", "", e.Exception.ToString());
        MessageBox.Show(
            $"خطای غیرمنتظره‌ای رخ داد اما برنامه بسته نمی‌شود:\n\n{e.Exception.Message}\n\nجزئیات در error_log.txt ثبت شد.",
            "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            $"خطای بحرانی:\n\n{ex?.Message ?? "نامشخص"}\n\nجزئیات در error_log.txt ثبت شد.",
            "خطای بحرانی", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
