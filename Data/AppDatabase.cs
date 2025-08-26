namespace SpyGame.Data;

using SQLite;
using SpyGame.Models;
using System.Text.Json;

public class AppDatabase
{
    private readonly SQLiteAsyncConnection _conn;

    public const string GeneralCategoryName = "عمومی";

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

        // 1) حداقل دسته‌بندی‌های پایه
        await EnsureBaseCategoriesAsync(new[]
        {
            GeneralCategoryName, "اماکن", "شغل", "اشیا", "مفاهیم انتزاعی", "طبیعت", "غذا و خوراکی‌ها"
        });

        await EnsureGameConfigAsync();

        // 2) سید پایه‌ی قبلی (اگر لازم داری نگه‌دار؛ اگر نه می‌تونی حذفش کنی)
        // -- در صورت نیاز، همینجا AddWords(...) های قبلی‌ات می‌تونن باقی بمونن --

        // 3) سید از JSON بزرگ (لیستی که فرستادی)
        await SeedFromJsonAsync(LargeSeedJson);
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
                CategoryId = null, // یا ID دسته‌بندی عمومی
                CategoryName = GeneralCategoryName,
                SecretWord = string.Empty,
                SpyIndices = new List<int>(),
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
            // اگر JSON مشکل داشت، بی‌سروصدا رد می‌شه تا اپ نپره
            return;
        }
        if (data == null || data.Count == 0) return;

        // کش کلمات موجود برای حذف تکراری (Case-insensitive)
        var existingWords = new HashSet<string>(
            (await _conn.Table<WordItem>().ToListAsync()).Select(w => w.Text),
            StringComparer.InvariantCultureIgnoreCase
        );

        var toInsert = new List<WordItem>(4096);

        foreach (var (categoryName, diffMap) in data)
        {
            if (string.IsNullOrWhiteSpace(categoryName) || diffMap == null) continue;

            // اگر دسته نبود، بساز
            var catId = await EnsureCategoryAsync(categoryName);

            foreach (var (diffFa, words) in diffMap)
            {
                var level = ParseDifficulty(diffFa);
                if (words == null) continue;

                foreach (var raw in words)
                {
                    var w = (raw ?? "").Trim();
                    if (w.Length == 0) continue;

                    // حذف تکراری‌ها و آیتم‌های خیلی غیرعادی
                    if (existingWords.Contains(w)) continue;

                    // نمونه فیلتر ساده برای نویزهای خیلی خارج از بازی (دلخواه)
                    // می‌تونی این‌ها رو حذف کنی اگر همهٔ آیتم‌ها رو می‌خوای نگه داری.
                    if (w.Length > 30) continue; // واژه‌های خیلی طولانیِ متنی

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

    // ------------------------- Queries -------------------------

    public Task<List<Category>> GetCategoriesAsync() =>
        _conn.Table<Category>().OrderBy(c => c.Name).ToListAsync();

    public async Task<List<WordItem>> GetWordsByCategoryAsync(int categoryId)
    {
        var history = (await _conn.Table<WordHistory>()
        .OrderByDescending(x => x.Id)
        .Take(100)
        .ToListAsync())
        .Select(x => x.WordItemId)
        .ToHashSet();

        var words = await _conn.Table<WordItem>()
        .Where(w => w.CategoryId == categoryId)
        .OrderBy(w => w.Text)
        .ToListAsync();

        return words.Where(w => !history.Contains(w.Id)).ToList();
    }
        

    public async Task<List<WordItem>> GetAllWordsAsync()
    {
        var history = (await _conn.Table<WordHistory>()
        .OrderByDescending(x => x.Id)
        .Take(100)
        .ToListAsync())
        .Select(x => x.WordItemId)
        .ToHashSet();

        var words = await _conn.Table<WordItem>()
        .OrderBy(w => w.Text)
        .ToListAsync();

        return words.Where(w => !history.Contains(w.Id)).ToList();
    }

    public Task<int> AddCategoryAsync(Category c) => _conn.InsertAsync(c);
    public Task<int> AddWordAsync(WordItem w) => _conn.InsertAsync(w);

    public Task<int> AddWordHistoreAsync(WordHistory w) => _conn.InsertAsync(w);

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

    public Task<int> AddGameConfigAsync(GameConfig g) => _conn.InsertAsync(g);
    public Task<int> UpdateGameConfigAsync(GameConfig g) => _conn.UpdateAsync(g);


    // ------------------------- JSON بزرگت را اینجا بچسبان -------------------------
    // حتماً کوتیشن‌های فارسی را با " تبدیل کن و فرمت JSON معتبر باشد.
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
    ""سخت"":   [ ""اکسیمورون"", ""اپوستریوری"", ""اتوپوئیسیس"", ""اپیستمولوژی"" ]
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
