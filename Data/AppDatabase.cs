namespace SpyGame.Data;

using SQLite;
using SpyGame.Models;
using System.Text.Json;
using System.Globalization;

public class AppDatabase
{
    private readonly SQLiteAsyncConnection _conn;

    // نام عمومی در سیستم: «درهم»
    public const string GeneralCategoryName = "درهم";

    public AppDatabase(string dbPath)
    {
        _conn = new SQLiteAsyncConnection(dbPath);
    }

    public async Task InitAsync()
    {
        await _conn.CreateTableAsync<Category>();
        await _conn.CreateTableAsync<WordItem>();
        await _conn.CreateTableAsync<GameConfig>();
        await _conn.CreateTableAsync<WordHistory>();

        // مهاجرت های لازم
        await Migrate_AddWordItemDifficultyAsync();
        await Migrate_AddGameConfigSelectedDifficultyAsync();
        await Migrate_RenameGeneralToMixedAsync();
        // (اختیاری) اگر ستونی برای متن در تاریخچه داشته باشید
        await Migrate_AddWordHistoryTextAsync();

        // 1) حداقل دسته‌بندی‌های پایه
        await EnsureBaseCategoriesAsync(new[]
        {
            GeneralCategoryName, "اماکن", "شغل", "اشیا", "مفاهیم انتزاعی", "طبیعت", "غذا و خوراکی‌ها"
        });

        await EnsureGameConfigAsync();

        // 3) سید از JSON بزرگ (در صورت نیاز)
        await SeedFromJsonAsync(LargeSeedJson);
    }

    // ------------------------- Migrations -------------------------

    private async Task Migrate_AddWordItemDifficultyAsync()
    {
        try
        {
            await _conn.ExecuteAsync("ALTER TABLE WordItem ADD COLUMN Difficulty INTEGER NOT NULL DEFAULT 1");
        }
        catch
        {
            // ستون وجود داشته باشد خطا می‌دهد؛ نادیده بگیر
        }
    }

    private async Task Migrate_AddGameConfigSelectedDifficultyAsync()
    {
        try
        {
            // null = درهم
            await _conn.ExecuteAsync("ALTER TABLE GameConfig ADD COLUMN SelectedDifficultyValue INTEGER NULL");
        }
        catch
        {
            // اگر ستون موجود باشد، نادیده بگیر
        }
    }

    private async Task Migrate_RenameGeneralToMixedAsync()
    {
        try
        {
            // هر جا «عمومی» بوده → «درهم»
            await _conn.ExecuteAsync("UPDATE Category SET Name = ? WHERE Name = ? OR Name LIKE ?", GeneralCategoryName, "عمومی", "%عمومی%");
            await _conn.ExecuteAsync("UPDATE GameConfig SET CategoryName = ? WHERE CategoryName = ? OR CategoryName LIKE ?", GeneralCategoryName, "عمومی", "%عمومی%");
        }
        catch { /* ignore */ }
    }

    private async Task Migrate_AddWordHistoryTextAsync()
    {
        try
        {
            await _conn.ExecuteAsync("ALTER TABLE WordHistory ADD COLUMN WordText TEXT NULL");
        }
        catch { /* ignore if exists */ }
    }

    // ------------------------- Helpers -------------------------

    private async Task EnsureBaseCategoriesAsync(IEnumerable<string> names)
    {
        var existing = await _conn.Table<Category>().ToListAsync();
        var existingSet = new HashSet<string>(existing.Select(c => c.Name), StringComparer.InvariantCulture);
        foreach (var n in names)
        {
            if (!existingSet.Contains(n))
                await _conn.InsertAsync(new Category { Name = n });
        }
    }

    private async Task<int> EnsureCategoryAsync(string name)
    {
        name = name.Trim();
        var cat = await _conn.Table<Category>().Where(c => c.Name == name).FirstOrDefaultAsync();
        if (cat == null)
        {
            cat = new Category { Name = name };
            await _conn.InsertAsync(cat);
        }
        return cat.Id;
    }

    private async Task<GameConfig> EnsureGameConfigAsync()
    {
        var config = await _conn.Table<GameConfig>().FirstOrDefaultAsync();
        if (config == null)
        {
            config = new GameConfig
            {
                Players = 6,
                Spies = 2,
                Minutes = 3,
                CategoryId = null,
                CategoryName = GeneralCategoryName,
                SecretWord = string.Empty,
                SpyIndices = new List<int>(),
                SelectedDifficulty = null, // درهم
                CurrentPlayerIndex = 0
            };
            await _conn.InsertAsync(config);
        }
        return config;
    }

    private static DifficultyLevel ParseDifficulty(string fa)
    {
        fa = (fa ?? "").Trim();
        return fa switch
        {
            "آسان" => DifficultyLevel.Easy,
            "متوسط" => DifficultyLevel.Medium,
            "سخت" => DifficultyLevel.Hard,
            _ => DifficultyLevel.Medium
        };
    }

    // مدلِ JSON ورودی: { "اماکن": { "آسان": ["..."], "متوسط": [...], "سخت": [...] }, ... }
    private sealed class JsonCategoryMap : Dictionary<string, Dictionary<string, string[]>> { }

    private async Task SeedFromJsonAsync(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        JsonCategoryMap? data;
        try
        {
            data = JsonSerializer.Deserialize<JsonCategoryMap>(json);
        }
        catch
        {
            return;
        }
        if (data == null || data.Count == 0) return;

        var existingWords = new HashSet<string>(
            (await _conn.Table<WordItem>().ToListAsync()).Select(w => w.Text),
            StringComparer.InvariantCultureIgnoreCase
        );

        var toInsert = new List<WordItem>(4096);

        foreach (var (categoryName, diffMap) in data)
        {
            if (string.IsNullOrWhiteSpace(categoryName) || diffMap == null) continue;

            var catId = await EnsureCategoryAsync(categoryName);

            foreach (var (diffFa, words) in diffMap)
            {
                var level = ParseDifficulty(diffFa);
                if (words == null) continue;

                foreach (var raw in words)
                {
                    var w = (raw ?? "").Trim();
                    if (w.Length == 0) continue;

                    if (existingWords.Contains(w)) continue;
                    if (w.Length > 30) continue;

                    toInsert.Add(new WordItem
                    {
                        CategoryId = catId,
                        Text = w,
                        Difficulty = level
                    });
                    existingWords.Add(w);
                }
            }
        }

        if (toInsert.Count > 0)
            await _conn.InsertAllAsync(toInsert);
    }

    // ------------------------- Normalization -------------------------

    private static string NormalizeKey(string? s)
    {
        s = (s ?? string.Empty).Trim();
        // یکسان‌سازی یونیکدهای عربی/فارسی رایج
        s = s.Replace('ي', 'ی').Replace('ك', 'ک').Replace("\u200c", ""); // حذف نیم‌فاصله
        return s.ToLowerInvariant();
    }

    private sealed class TextRow { public string Text { get; set; } = string.Empty; }

    private async Task<HashSet<string>> GetRecentWordKeysAsync(int take)
    {
        if (take <= 0) return new HashSet<string>();
        var rows = await _conn.QueryAsync<TextRow>(
            @"SELECT wi.Text AS Text
              FROM WordHistory wh
              INNER JOIN WordItem wi ON wi.Id = wh.WordItemId
              ORDER BY wh.Id DESC
              LIMIT ?", take);
        return rows.Select(r => NormalizeKey(r.Text)).ToHashSet();
    }

    // ------------------------- Queries (با Difficulty + ضد تکرار متنی) -------------------------

    public Task<List<Category>> GetCategoriesAsync() =>
        _conn.Table<Category>().OrderBy(c => c.Name).ToListAsync();

    public async Task<List<WordItem>> GetWordsByCategoryAsync(int categoryId, DifficultyLevel? difficulty = null, int recentWindow = 100)
    {
        var recentKeys = await GetRecentWordKeysAsync(recentWindow);

        var query = _conn.Table<WordItem>()
                         .Where(w => w.CategoryId == categoryId);

        if (difficulty.HasValue)
            query = query.Where(w => w.Difficulty == difficulty.Value);

        var words = await query.ToListAsync();

        // حذف تکراری‌های داخل دیتابیس و سپس فیلتر بر اساس 100 کلمه آخر (بر پایه متن نرمال‌شده)
        var filtered = words
            .GroupBy(w => NormalizeKey(w.Text))
            .Where(g => !recentKeys.Contains(g.Key))
            .Select(g => g.First())
            .OrderBy(w => w.Text)
            .ToList();

        return filtered;
    }

    public async Task<List<WordItem>> GetAllWordsAsync(DifficultyLevel? difficulty = null, int recentWindow = 100)
    {
        var recentKeys = await GetRecentWordKeysAsync(recentWindow);

        var query = _conn.Table<WordItem>();
        if (difficulty.HasValue)
            query = query.Where(w => w.Difficulty == difficulty.Value);

        var words = await query.ToListAsync();

        var filtered = words
            .GroupBy(w => NormalizeKey(w.Text))
            .Where(g => !recentKeys.Contains(g.Key))
            .Select(g => g.First())
            .OrderBy(w => w.Text)
            .ToList();

        return filtered;
    }

    // نسخه‌های بدون Difficulty برای سازگاری
    public async Task<List<WordItem>> GetWordsByCategoryAsync(int categoryId)
        => await GetWordsByCategoryAsync(categoryId, null, 100);

    public async Task<List<WordItem>> GetAllWordsAsync()
        => await GetAllWordsAsync(null, 100);

    // در صورت نیاز هنوز می‌توان به‌صورت ID هم گرفت
    private async Task<HashSet<int>> GetRecentWordIdsAsync(int take)
    {
        if (take <= 0) return new HashSet<int>();
        var history = await _conn.Table<WordHistory>()
                                 .OrderByDescending(x => x.Id)
                                 .Take(take)
                                 .ToListAsync();
        return history.Select(x => x.WordItemId).ToHashSet();
    }

    public Task<int> AddCategoryAsync(Category c) => _conn.InsertAsync(c);
    public Task<int> AddWordAsync(WordItem w) => _conn.InsertAsync(w);
    public Task<int> AddWordHistoryAsync(WordHistory w) => _conn.InsertAsync(w);

    public Task<int> CountWordsInCategoryAsync(int categoryId) =>
        _conn.Table<WordItem>().Where(w => w.CategoryId == categoryId).CountAsync();

    public Task<Category?> GetGeneralCategoryAsync() =>
        _conn.Table<Category>().Where(c => c.Name == GeneralCategoryName).FirstOrDefaultAsync();

    public Task<int> GetWordCountAsync() =>
        _conn.Table<WordItem>().CountAsync();

    public Task<int> GetCategoryCountAsync() =>
        _conn.Table<Category>().CountAsync();

    public async Task<GameConfig?> GetLastConfigAsync()
    {
        return await _conn.Table<GameConfig>()
            .OrderByDescending(c => c.CreatedOn)
            .FirstOrDefaultAsync();
    }

    // 🔹 جدید: گرفتن آخرین N کانفیگ (برای محاسبه وزن‌ها)
    public Task<List<GameConfig>> GetLastNConfigsAsync(int n)
    {
        return _conn.Table<GameConfig>()
            .OrderByDescending(c => c.CreatedOn)
            .Take(n)
            .ToListAsync();
    }

    public Task<int> AddGameConfigAsync(GameConfig g) => _conn.InsertAsync(g);
    public Task<int> UpdateGameConfigAsync(GameConfig g) => _conn.UpdateAsync(g);


    private const string LargeSeedJson = /* زبان: JSON */ @"
{
  ""اماکن"": {
    ""آسان"": [ ""خانه"", ""مدرسه"", ""پارک"", ""بیمارستان"", ""فروشگاه"", ""سینما"", ""ایستگاه قطار"", ""فرودگاه"", ""ساحل"", ""کتابخانه"", ""خیابان"", ""شهر"", ""روستا"", ""مسجد"", ""کلیسا"", ""هتل"", ""رستوران"", ""بازار"", ""پاساژ"", ""دانشگاه"", ""باغ"", ""باغ وحش"", ""موزه"", ""دادگاه"", ""کلانتری"", ""زندان"", ""پارکینگ"", ""گاراژ"", ""جاده"", ""بزرگراه"", ""کوچه"", ""میدان"", ""پل"", ""کارخانه"", ""باشگاه ورزشی"", ""آبشار"", ""برج"", ""پمپ بنزین"", ""حمام"", ""درمانگاه"", ""آزمایشگاه"" ],
    ""متوسط"": [ ""جزیره"", ""تالار عروسی"", ""تئاتر"", ""کوهستان"", ""غار"", ""پالایشگاه"", ""استخر"", ""اقیانوس"", ""دریاچه"", ""رودخانه"", ""صحرا"", ""کوه"", ""شهرستان"", ""فرانسه"", ""آلمان"", ""هلند"", ""اسپانیا"", ""ایتالیا"", ""یونان"", ""پرتغال"", ""دانمارک"", ""امارات"", ""قطر"", ""مصر"" ],
    ""سخت"":   [ ""سنگاپور"", ""توکیو"", ""مسکو"", ""پکن"", ""آمستردام"", ""استکهلم"", ""پادگان"", ""پاسگاه"", ""مرداب"", ""تالاب"", ""دامنه"", ""فلات"", ""جلگه"", ""تنگه"", ""صخره"", ""اسکله"", ""نیزار"", ""چمنزار"", ""بیابان"" ]
  },
  ""شغل"": {
    ""آسان"": [ ""معلم"", ""پزشک"", ""پلیس"", ""آتش‌نشان"", ""راننده"", ""آشپز"", ""فروشنده"", ""کشاورز"", ""پرستار"", ""مکانیک"", ""عکاس"", ""خبرنگار"", ""مهندس"", ""وکیل"", ""حسابدار"", ""طراح"", ""کتابدار"", ""خلبان"", ""دامپزشک"", ""نجار"", ""آرایشگر"" ],
    ""متوسط"": [ ""مدیر"", ""مربی"", ""معمار"", ""مشاور"", ""نگهبان"", ""معدنچی"", ""بازرگان"", ""قاضی"", ""ملوان"", ""کاپیتان"", ""صندوقدار"", ""دانشمند"", ""پژوهشگر"" ],
    ""سخت"":   [ ""باستان‌شناس"", ""ستاره‌شناس"", ""زیست‌فناور"", ""جرم‌شناس"", ""مهندس هوافضا"", ""اقیانوس‌شناس"", ""تحلیلگر داده"", ""کارگردان تئاتر"" ]
  },
  ""اشیا"": {
    ""آسان"": [ ""میز"", ""صندلی"", ""کتاب"", ""مداد"", ""تلفن"", ""تلویزیون"", ""ماشین"", ""توپ"", ""ساعت"", ""کیف"", ""عینک"", ""کلید"", ""یخچال"", ""پرینتر"", ""دوربین"" ],
    ""متوسط"": [ ""تلسکوپ"", ""میکروسکوپ"", ""قطب‌نما"", ""چکش"", ""چرخ خیاطی"", ""دستگاه قهوه‌ساز"", ""اسکیت‌بورد"", ""ویولن"", ""گیتار"", ""پیانو"", ""دریل"", ""جرثقیل"" ],
    ""سخت"":   [ ""سانتریفیوژ"", ""ژنراتور"", ""اسیلوسکوپ"", ""چاپگر سه‌بعدی"", ""دستگاه دیالیز"", ""رادار"", ""لیزر صنعتی"" ]
  },
  ""مفاهیم انتزاعی"": {
    ""آسان"": [ ""عشق"", ""دوستی"", ""شادی"", ""غم"", ""ترس"", ""امید"", ""آزادی"", ""عدالت"", ""صلح"", ""موفقیت"", ""وفاداری"", ""شجاعت"", ""صداقت"", ""اعتماد"", ""خلاقیت"", ""مسئولیت"", ""همدلی"", ""انگیزه"" ],
    ""متوسط"": [ ""پارادوکس"", ""بی‌نهایت"", ""هستی‌شناسی"", ""معرفت‌شناسی"", ""آنتروپی"", ""دیالکتیک"", ""پوچ‌گرایی"", ""خودآگاهی"" ],
    ""سخت"":   [ ""گسل تکتونیکی"", ""سنگ آذرین"", ""اکوسیستم"", ""چرخه نیتروژن"", ""زیست‌کره"" ]
  },
  ""طبیعت"": {
    ""آسان"": [ ""درخت"", ""گل"", ""رودخانه"", ""کوه"", ""دریا"", ""جنگل"", ""خورشید"", ""ماه"", ""ابر"", ""باد"", ""آبشار"", ""چشمه"", ""ساحل"" ],
    ""متوسط"": [ ""تالاب"", ""صخره"", ""بیابان"", ""جزیره"", ""تپه"", ""دره"", ""کوهستان"", ""گردباد"", ""مرداب"", ""نیزار"" ],
    ""سخت"":   [ ""گسل تکتونیکی"", ""سنگ آذرین"", ""اکوسیستم"", ""چرخه نیتروژن"", ""زیست‌کره"" ]
  },
  ""غذا و خوراکی‌ها"": {
    ""آسان"": [ ""نان"", ""برنج"", ""سیب"", ""موز"", ""شیر"", ""تخم‌مرغ"", ""گوشت"", ""ماهی"", ""سیب‌زمینی"", ""پیتزا"", ""قورمه‌سبزی"", ""کباب"", ""فلافل"", ""پاستا"", ""سالاد"", ""خوراک لوبیا"", ""کیک"" ],
    ""متوسط"": [ ""زرشک‌پلو"", ""فسنجان"", ""قیمه"", ""کتلت"", ""کوکو"", ""الویه"", ""آش رشته"", ""حلیم"", ""ژله"" ],
    ""سخت"":   [ ""فوآگرا"", ""ترافل"", ""کاسو مارسو"", ""دورین"" ]
  }
}
";
}
