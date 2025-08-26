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

    // اندیس بازیکن‌هایی که جاسوس‌اند (۰-مبنـا)
    public string SpyIndicesJson { get; set; } = "[]";

    // کنترل جریان نمایش
    public int CurrentPlayerIndex { get; set; } = 0;

    // 🔹 استفاده راحت در کد — در DB ذخیره نمی‌شود
    [Ignore]
    public List<int> SpyIndices
    {
        get
        {
            try
            {
                return JsonSerializer.Deserialize<List<int>>(SpyIndicesJson) ?? new List<int>();
            }
            catch
            {
                return new List<int>();
            }
        }
        set
        {
            SpyIndicesJson = JsonSerializer.Serialize(value ?? new List<int>());
        }
    }

    // برای مرتب‌سازی آخرین کانفیگ
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
