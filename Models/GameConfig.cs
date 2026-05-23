using SQLite;
using System.Text.Json;

namespace SpyGame.Models;

public class GameConfig
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int Players { get; set; }
    public int Spies { get; set; }
    public int Minutes { get; set; }

    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    public string SecretWord { get; set; } = string.Empty;

    // اندیس بازیکن‌هایی که جاسوس‌اند (۰-مبنـا) - به صورت JSON ذخیره می‌شود
    public string SpyIndicesJson { get; set; } = "[]";

    // کنترل جریان نمایش
    public int CurrentPlayerIndex { get; set; } = 0;

    // پیش‌فرض انتخاب‌شده برای «درجه سختی» (null = درهم)
    // برای سازگاری با SQLite، مقدار عددی نگه می‌داریم و یک پراپرتی Ignore برای Enum ارائه می‌دهیم.
    public int? SelectedDifficultyValue { get; set; } = null;

    [Ignore]
    public DifficultyLevel? SelectedDifficulty
    {
        get => SelectedDifficultyValue.HasValue ? (DifficultyLevel?)SelectedDifficultyValue.Value : null;
        set => SelectedDifficultyValue = value.HasValue ? (int)value.Value : (int?)null;
    }

    // 🔹 استفاده راحت از اندیس‌ها — در DB ذخیره نمی‌شود
    [Ignore]
    public List<int> SpyIndices
    {
        get
        {
            try { return JsonSerializer.Deserialize<List<int>>(SpyIndicesJson) ?? new List<int>(); }
            catch { return new List<int>(); }
        }
        set
        {
            SpyIndicesJson = JsonSerializer.Serialize(value ?? new List<int>());
        }
    }

    public bool SpiesKnowEachOther { get; set; } = false;

    // برای مرتب‌سازی آخرین کانفیگ
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
