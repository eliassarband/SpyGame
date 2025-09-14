using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using SpyGame.Data;
using SpyGame.Models;

namespace SpyGame.Views;

public partial class SetupPage : ContentPage
{
    private readonly AppDatabase _db;

    public SetupPage(AppDatabase db)
    {
        InitializeComponent();
        _db = db;

        PlayersStepper.ValueChanged += (_, __) => PlayersLabel.Text = ((int)PlayersStepper.Value).ToString();
        SpiesStepper.ValueChanged += (_, __) => SpiesLabel.Text = ((int)SpiesStepper.Value).ToString();
        MinutesStepper.ValueChanged += (_, __) => MinutesLabel.Text = ((int)MinutesStepper.Value).ToString();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // انیمیشن لطیف کارت
        if (Card != null)
        {
            Card.Opacity = 0;
            Card.Scale = 0.98;
            _ = Task.WhenAll(
                Card.FadeTo(1, 220, Easing.CubicOut),
                Card.ScaleTo(1, 260, Easing.CubicOut)
            );
        }

        // گزینه‌های سختی (پیش‌فرض: درهم)
        var difficulties = new[] { "درهم", "آسان", "متوسط", "سخت" };
        DifficultyPicker.ItemsSource = difficulties;
        DifficultyPicker.SelectedItem = difficulties[0];

        // بارگذاری دسته‌ها از DB
        var categories = await _db.GetCategoriesAsync();

        // «درهم» را همیشه اول لیست بیاور
        CategoryPicker.ItemsSource = categories
            .OrderByDescending(c => c.Name == "درهم")
            .ThenBy(c => c.Name)
            .ToList();

        // انتخاب پیش‌فرض
        var mixed = categories.FirstOrDefault(c => c.Name == "درهم");
        CategoryPicker.SelectedItem = mixed ?? categories.FirstOrDefault();

        // بازیابی آخرین تنظیمات (در صورت وجود)
        var lastConfig = await _db.GetLastConfigAsync();
        if (lastConfig != null)
        {
            PlayersStepper.Value = lastConfig.Players;
            MinutesStepper.Value = lastConfig.Minutes;
            CategoryPicker.SelectedItem =
                categories.FirstOrDefault(c => c.Id == lastConfig.CategoryId)
                ?? mixed
                ?? categories.FirstOrDefault();
            SpiesStepper.Value = lastConfig.Spies;

            // بازگردانی درجه سختی ذخیره‌شده
            var lastDiffFa = ToFa(lastConfig.SelectedDifficulty);
            if (difficulties.Contains(lastDiffFa))
                DifficultyPicker.SelectedItem = lastDiffFa;
        }
        else
        {
            PlayersStepper.Value = 6;
            MinutesStepper.Value = 3;
            CategoryPicker.SelectedItem = mixed ?? categories.FirstOrDefault();
            SpiesStepper.Value = 2;
            DifficultyPicker.SelectedItem = "درهم";
        }

        // اطمینان از نمایش مقادیر
        PlayersLabel.Text = ((int)PlayersStepper.Value).ToString();
        SpiesLabel.Text = ((int)SpiesStepper.Value).ToString();
        MinutesLabel.Text = ((int)MinutesStepper.Value).ToString();
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        if (_db is null)
        {
            await DisplayAlert("خطا", "دسترسی به دیتابیس برقرار نشد.", "باشه");
            return;
        }

        var selectedCategory = CategoryPicker.SelectedItem as Category;
        var selectedDifficultyFa = (DifficultyPicker.SelectedItem as string) ?? "درهم";
        DifficultyLevel? diff = FromFa(selectedDifficultyFa);

        int players = (int)PlayersStepper.Value;
        int spies = (int)SpiesStepper.Value;
        int minutes = (int)MinutesStepper.Value;

        // اعتبارسنجی
        if (players < 3)
        {
            await DisplayAlert("خطا", "حداقل ۳ بازیکن لازم است.", "باشه");
            return;
        }
        if (spies < 1)
        {
            await DisplayAlert("خطا", "حداقل ۱ جاسوس لازم است.", "باشه");
            return;
        }
        int maxSpies = players / 2;
        if (spies > maxSpies)
        {
            await DisplayAlert("خطا", $"تعداد جاسوس‌ها نباید بیش از {maxSpies} باشد.", "باشه");
            return;
        }
        if (minutes < 1)
        {
            await DisplayAlert("خطا", "زمان حداقل باید ۱ دقیقه باشد.", "باشه");
            return;
        }

        // انتخاب کلمه با فیلتر Difficulty از DB + جلوگیری از تکرار 100 کلمه آخر (بر پایه متن کلمه)
        List<WordItem> wordPool;
        if (selectedCategory != null && selectedCategory.Name != "درهم")
        {
            wordPool = await _db.GetWordsByCategoryAsync(selectedCategory.Id, diff, 100);
            if (wordPool.Count == 0)
            {
                // تمام کلمات دسته در 100 تای اخیر مصرف شده‌اند → بدون محدودیت تاریخچه (fallback)
                wordPool = await _db.GetWordsByCategoryAsync(selectedCategory.Id, diff, 0);
            }
        }
        else
        {
            wordPool = await _db.GetAllWordsAsync(diff, 100);
            if (wordPool.Count == 0)
            {
                // تمام کلمات در 100 تای اخیر مصرف شده‌اند → بدون محدودیت تاریخچه (fallback)
                wordPool = await _db.GetAllWordsAsync(diff, 0);
            }
        }

        if (wordPool.Count == 0)
        {
            await DisplayAlert("خطا", "برای این ترکیب دسته/درجه سختی، کلمه‌ای پیدا نشد.", "باشه");
            return;
        }

        var rand = new Random();
        var word = wordPool[rand.Next(wordPool.Count)];
        var secret = word.Text;

        // ثبت در تاریخچه برای جلوگیری از تکرار در دورهای بعدی
        await _db.AddWordHistoryAsync(new WordHistory { WordItemId = word.Id });

        // ---------- انتخاب جاسوس‌ها با قانون پنجره‌ای ----------
        // اگر بازیکنان ≥ 7 → پنجره 5 دور آخر (شامل همین دور)،
        // در غیر این صورت → پنجره 4 دور آخر (شامل همین دور).
        int windowSize = players >= 7 ? 5 : 4; // پنجره شامل دور جاری است
        int cap = 3; // در پنجره‌ی شامل دور جاری، هر نفر حداکثر 3 بار جاسوس باشد

        // فقط به تعداد دورهای قبل از دور جاری نیاز داریم (windowSize - 1)
        var lastConfigs = await _db.GetLastNConfigsAsync(windowSize - 1);

        // تاریخچه: فقط ایندکس‌های معتبرِ 0..players-1 را لحاظ کن
        var lastSpiesHistory = (lastConfigs ?? new List<GameConfig>())
            .Select(c => (c.SpyIndices ?? new List<int>())
                .Where(i => i >= 0 && i < players)
                .Distinct()
                .ToList())
            .ToList();

        List<int> spyIndices;
        try
        {
            spyIndices = PickSpiesWindowCapped(players, spies, lastSpiesHistory, cap, rand);
        }
        catch (InvalidOperationException)
        {
            await DisplayAlert(
                "ناممکن",
                $"با قانون «در {windowSize} دور آخر هر نفر حداکثر {cap} بار جاسوس»، تعداد افراد واجد شرایط کمتر از تعداد جاسوس‌های درخواستی است.\n" +
                "یا تعداد جاسوس‌ها را کاهش دهید یا یک دور بدون این محدودیت بازی کنید.",
                "باشه");
            return;
        }
        // -------------------------------------------------------

        // ثبت دور جدید به‌صورت رکورد تازه (برای هیستوری)
        var newConfig = new GameConfig
        {
            Players = players,
            Spies = spies,
            Minutes = minutes,
            CategoryId = selectedCategory?.Id,
            CategoryName = selectedCategory?.Name ?? "درهم",
            SecretWord = secret,
            SpyIndices = spyIndices,
            SelectedDifficulty = diff,
            CurrentPlayerIndex = 0,
            CreatedOn = DateTime.UtcNow
        };
        await _db.AddGameConfigAsync(newConfig);

        await Shell.Current.GoToAsync(nameof(RevealPage));
    }

    // ---- Helpers ----

    private static string ToFa(DifficultyLevel? d) => d switch
    {
        DifficultyLevel.Easy => "آسان",
        DifficultyLevel.Medium => "متوسط",
        DifficultyLevel.Hard => "سخت",
        _ => "درهم"
    };

    private static DifficultyLevel? FromFa(string s) => s switch
    {
        "آسان" => DifficultyLevel.Easy,
        "متوسط" => DifficultyLevel.Medium,
        "سخت" => DifficultyLevel.Hard,
        _ => (DifficultyLevel?)null // درهم
    };

    /// <summary>
    /// انتخاب وزن‌دار جاسوس‌ها با محدودیت «در پنجره‌ی آخر (بدون احتساب این دور) هر نفر حداکثر cap بار جاسوس بوده باشد».
    /// بعد از انتخاب این دور، جمع در پنجره‌ی شامل دور جاری از cap تجاوز نمی‌کند.
    /// lastSpies: لیست دورهای قبلی از جدید به قدیم (فقط windowSize-1 دور قبل را بده).
    /// وزن‌دهی: هرچه فرد در پنجره‌ی قبلی بیشتر جاسوس بوده، وزن کمتر؛ اگر به cap رسیده باشد وزن 0 (غیرمجاز).
    /// </summary>
    private static List<int> PickSpiesWindowCapped(
        int players,
        int spies,
        List<List<int>> lastSpies,
        int cap,
        Random rand)
    {
        lastSpies ??= new List<List<int>>();

        int CountInWindow(int idx)
        {
            int cnt = 0;
            for (int r = 0; r < lastSpies.Count; r++)
            {
                var spiesInRound = lastSpies[r] ?? new List<int>();
                if (spiesInRound.Contains(idx)) cnt++;
            }
            return cnt;
        }

        double WeightForCount(int cnt) => cnt switch
        {
            0 => 1.0,
            1 => 0.7,
            2 => 0.4,
            _ => 0.0 // cnt >= cap → در این دور انتخاب نشود
        };

        var candidates = Enumerable.Range(0, players).ToList();
        var counts = candidates.ToDictionary(i => i, CountInWindow);
        var weights = candidates.ToDictionary(i => i,
            i => counts[i] >= cap ? 0.0 : WeightForCount(counts[i]));

        var eligible = new HashSet<int>(weights.Where(kv => kv.Value > 0).Select(kv => kv.Key));
        if (eligible.Count < spies)
            throw new InvalidOperationException("Eligible less than requested spies.");

        // انتخاب وزن‌دار بدون‌جای‌گذاری از بین واجدین
        var picks = new List<int>();
        var pool = new HashSet<int>(eligible);

        while (picks.Count < spies && pool.Count > 0)
        {
            double sum = pool.Sum(i => weights[i]);
            if (sum <= 0) break;

            double r = rand.NextDouble() * sum;
            double acc = 0;
            int chosen = -1;
            foreach (var i in pool)
            {
                acc += weights[i];
                if (acc >= r) { chosen = i; break; }
            }
            if (chosen == -1) chosen = pool.Last();

            picks.Add(chosen);
            pool.Remove(chosen);
        }

        // اطمینان: اگر به هر دلیل کمتر شد، از eligible پر کن (بدون نقض cap)
        while (picks.Count < spies)
        {
            var rest = eligible.Except(picks).ToList();
            if (rest.Count == 0) break;
            var fallback = rest[rand.Next(rest.Count)];
            picks.Add(fallback);
        }

        return picks.OrderBy(x => x).ToList();
    }
}
