using System;
using System.Runtime.InteropServices;

namespace TorobScanner.Services;

/// <summary>
/// ✨ v3.5 — نگهبان پروسه‌ی ویندوز (دو اصلاح P0 از بازبینی پروژه):
///
/// ۱) تک‌نمونه‌ای (Single Instance):
///    تا امروز اگر کاربر برنامه را دو بار باز می‌کرد، دو نویسنده‌ی همزمان روی
///    یک دیتابیس SQLite می‌نشستید → SQLITE_BUSY / از دست رفتن نوشته‌ها.
///    حالا نمونه‌ی دوم با پیام فارسی بسته می‌شود.
///
/// ۲) Job Object با KILL_ON_JOB_CLOSE — ریشه‌کنی پروسسه‌های زامبی:
///    درایور Playwright (node.exe) و کرومیوم، فرزندان پروسه‌ی برنامه‌اند؛ اگر برنامه
///    وسط اسکن کرش کند یا بسته شود، تا امروز این فرزندان زامبی می‌ماندند و
///    فایل‌های برنامه را قفل می‌کردند → آپدیت خودکار شکست می‌خورد (باگ ۳۶).
///    راه‌حل ریشه‌ای: کل پروسه‌ی برنامه در یک Job ویندوزی قرار می‌گیرد؛ هر فرزندی که
///    از این پس spawn شود (node / chrome / ...) به‌طور خودکار عضو همان Job است و
///    به محض خروج برنامه (هر نوع خروجی — حتی کرش یا End Task)، ویندوز کل
///    اعضای Job را می‌کشد. دیگر هیچ زامبی‌ای باقی نمی‌ماند.
///
/// نکته‌ی ایمنی: هر دو قابلیت در try/catch هستند — اگر به هر دلیلی (سیاست
/// آنتی‌ویروس، ویندوز قدیمی، محیط مجازی) شکست بخورند، رفتار برنامه عین قبل است.
/// </summary>
internal static class WindowsProcessGuard
{
    private static IntPtr _jobHandle;
    private static Mutex? _singleInstanceMutex;
    private const string MutexName = @"Local\BazarSanj.SingleInstance";

    // ═══════════ ۱) تک‌نمونه‌ای ═══════════

    /// <summary>
    /// اگر نمونه‌ی دیگری از برنامه از قبل باز باشد false برمی‌گرداند
    /// (تماس‌گیرنده باید با پیام فارسی خارج شود).
    /// پیشوند Local = هر جلسه‌ی ویندوز جداگانه (چند کاربر همزمان مشکلی ندارند)
    /// </summary>
    public static bool EnsureSingleInstance()
    {
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
            return createdNew;
        }
        catch
        {
            return true;   // شکست مکانیزم قفل → رفتار عین قبل (بدون محدودیت)
        }
    }

    // ═══════════ ۲) Job Object ضدزامبی ═══════════

    public static void AttachKillOnCloseJob()
    {
        try
        {
            _jobHandle = CreateJobObjectW(IntPtr.Zero, null);
            if (_jobHandle == IntPtr.Zero) return;

            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            if (!SetInformationJobObject(_jobHandle, JobObjectExtendedLimitInformation,
                    ref info, (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()))
                return;

            // عضویت خود پروسه‌ی برنامه — همه‌ی فرزندان آینده (node/chrome) به ارث می‌برند
            AssignProcessToJobObject(_jobHandle, GetCurrentProcess());
            // هندل عمداً بسته نمی‌شود — بستنش یعنی کشتن خود برنامه!
            // ویندوز هنگام خروج پروسه، هندل را خودش می‌بندد → پاکسازی نهایی.
        }
        catch (Exception ex)
        {
            Logger.Warn("ProcessGuard/Job", ex.Message);   // هیچ‌وقت مسیر استارت‌آپ را نینداز
        }
    }

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
    private const int JobObjectExtendedLimitInformation = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        IntPtr hJob, int JobObjectInformationClass,
        ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInformation, uint cbJobObjectInformationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
}
