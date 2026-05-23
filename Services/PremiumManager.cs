using SpyGame.Models;

namespace SpyGame.Services;

public class PremiumManager
{
    // ---- تنظیمات ثابت ----

    // در حین توسعه: true کن تا همه قابلیت‌ها باز بشن
    // قبل از انتشار: حتماً false کن
    private const bool DEV_UNLOCK_ALL = false;

    public const int FreeCustomWordLimit = 5;

    // دسته‌بندی‌هایی که نیاز به بسته ویژه دارن
    public static readonly HashSet<string> PremiumCategoryNames = new(StringComparer.Ordinal)
    {
        "ورزش",
        "فیلم و سریال",
        "تاریخ و مشاهیر"
    };

    private const string StorageKey = "premium_unlocked";

    // ---- بررسی وضعیت ----

    public bool IsUnlocked(PremiumFeature feature)
    {
        if (DEV_UNLOCK_ALL) return true;
        return ReadFlag();
    }

    public bool IsPremium => IsUnlocked(PremiumFeature.Premium);

    // ---- فعال‌سازی (بعداً SDK مارکت اینجا صدا زده می‌شه) ----

    public async Task UnlockAsync(PremiumFeature feature)
    {
        await WriteFlag(true);
    }

    // ---- ابزار توسعه (قبل از production حذف نشه، فقط از UI پنهان بشه) ----

    public async Task DevUnlockAsync() => await WriteFlag(true);

    public void DevLock()
    {
        try { SecureStorage.Default.Remove(StorageKey); } catch { }
        Preferences.Remove(StorageKey);
    }

    // ---- ذخیره‌سازی ----

    private bool ReadFlag()
    {
        try
        {
            // SecureStorage مقدار sync نداره — از Preferences بخون
            return Preferences.Get(StorageKey, false);
        }
        catch
        {
            return false;
        }
    }

    private async Task WriteFlag(bool value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(StorageKey, value.ToString());
        }
        catch { }

        // همیشه در Preferences هم بنویس (fallback سریع برای sync read)
        Preferences.Set(StorageKey, value);
    }
}
